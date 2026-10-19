using System.Text.Json;
using Jint;
using Jint.Native;
using Jint.Native.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Domain.Game.Scripting;

/// <summary>A snake as a script sees it: plain numbers and text, nothing of the server.</summary>
public sealed record ScriptSnake(
    string Name, int Length, long Mass, int FoodsEaten, double Health, double LowestHealth, bool Boosting, IReadOnlyList<string> Perks,
    long Now = 0, long AgeMs = 0, int Kills = 0);

/// <summary>The room a snake plays in, as a script sees it.</summary>
public sealed record ScriptRoom(int Players, string Name, string Code, int Width, int Height, long Now = 0);

/// <summary>The snake that was just killed, as the killer's script sees it.</summary>
public sealed record ScriptVictim(string Name, int Length, long Mass);

/// <summary>
/// What a script remembers between calls: a small JSON object, <c>snake.state</c> or <c>room.state</c> in the script. The script
/// changes it in place and it is kept as long as the snake (or the room) lives.
/// </summary>
public sealed class ScriptState
{
    public const int MaxLength = 2_000;

    public string Json { get; set; } = "{}";
}

/// <summary>The state of every perk script for one snake, or for one room, by perk id.</summary>
public sealed class ScriptStates
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ScriptState> _states = new();

    public ScriptState Of(string perkId) => _states.GetOrAdd(perkId, _ => new ScriptState());
}

/// <summary>What <c>onTick</c> asked the room for.</summary>
public sealed record ScriptWorldActions(IReadOnlyList<string> DropPrizes);

/// <summary>The food a snake just ate, as a script sees it.</summary>
public sealed record ScriptFood(long Value, bool IsPrize);

/// <summary>Who is asking: the snake and the room it plays in.</summary>
public sealed record ScriptContext(ScriptSnake Snake, ScriptRoom Room, ScriptStates? SnakeStates = null, ScriptStates? RoomStates = null);

/// <summary>What <c>modify</c> returned, in range. A field the script left out, or got wrong, is null.</summary>
public sealed record ScriptEffects
{
    public int? ExtraShots { get; init; }
    public int? BurstExtraShots { get; init; }
    public double? BoostSpeedBonus { get; init; }
    public double? BoostLengthFactor { get; init; }
    public double? ReloadFactor { get; init; }
    public double? ShotSpeedFactor { get; init; }
    public double? DamageFactor { get; init; }
    public double? RegenPerSecond { get; init; }
    public double? MagnetRadius { get; init; }
    public bool? AutoFire { get; init; }
    public bool? Piercing { get; init; }
    public bool? Invulnerable { get; init; }
    public bool? FreeShots { get; init; }
}

/// <summary>What <c>onEat</c> returned, in range.</summary>
public sealed record ScriptRewards(long Mass, double Heal);

public sealed class ScriptException(string message) : Exception(message);

/// <summary>
/// A perk's JavaScript, run in a sandbox: no .NET, no files, no network, and limits on time, work, memory and recursion.
/// A call that breaks a limit or fails is skipped and counted; after <see cref="MaxFailuresInARow"/> failures in a row the
/// script is switched off. Safe to call from several threads: calls take turns.
/// </summary>
public sealed class PerkScript
{
    public const int MaxSourceLength = 20_000;
    public const int MaxFailuresInARow = 3;

    private static readonly TimeSpan CallTimeout = TimeSpan.FromMilliseconds(5);
    private const int MaxStatementsPerCall = 5_000;
    private const long MaxMemoryBytes = 2_000_000;
    private const int MaxRecursion = 32;

    private static readonly JsonSerializerOptions DataOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly ScriptSnake SampleSnake = new("sample", 10, 10, 5, 1.0, 1.0, false, ["sample"]);
    private static readonly ScriptRoom SampleRoom = new(2, "sample", "SAMPLE", 1920, 1080);
    private static readonly ScriptFood SampleFood = new(1, false);
    private static readonly ScriptVictim SampleVictim = new("victim", 10, 10);

    private readonly Engine _engine;
    private readonly object _turn = new();
    private readonly bool _hasModify;
    private readonly bool _hasOnEat;
    private readonly bool _hasOnKill;
    private readonly bool _hasOnTick;

    private PerkScript(Engine engine, bool hasModify, bool hasOnEat, bool hasOnKill, bool hasOnTick)
    {
        _engine = engine;
        _hasModify = hasModify;
        _hasOnEat = hasOnEat;
        _hasOnKill = hasOnKill;
        _hasOnTick = hasOnTick;
    }

    /// <summary>True when the script defines <c>onTick</c>, which runs once a second for the room, not for a snake.</summary>
    public bool HasOnTick => _hasOnTick;

    /// <summary>Failures in a row, counting from the last good call.</summary>
    public int Failures { get; private set; }

    public bool Failed => Failures > 0;

    /// <summary>True once the script failed too often in a row. It is skipped from then on.</summary>
    public bool Disabled => Failures >= MaxFailuresInARow;

    public string? LastError { get; private set; }

    /// <summary>Reads the script and finds what it defines. Throws <see cref="ScriptException"/> when it cannot be used.</summary>
    public static PerkScript Compile(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new ScriptException("The script is empty.");
        if (source.Length > MaxSourceLength) throw new ScriptException($"The script is too long: {source.Length} characters, at most {MaxSourceLength}.");

        var engine = NewEngine();
        try
        {
            engine.Execute(source);
        }
        catch (Exception exception)
        {
            throw new ScriptException(Describe(exception));
        }

        var hasModify = IsFunction(engine, "modify");
        var hasOnEat = IsFunction(engine, "onEat");
        var hasOnKill = IsFunction(engine, "onKill");
        var hasOnTick = IsFunction(engine, "onTick");
        if (!hasModify && !hasOnEat && !hasOnKill && !hasOnTick)
            throw new ScriptException("A script needs a function named modify, onEat, onKill or onTick.");
        return new PerkScript(engine, hasModify, hasOnEat, hasOnKill, hasOnTick);
    }

    /// <summary>The extra effects for a snake, or null when the script does not define <c>modify</c> or the call failed.</summary>
    public ScriptEffects? Modify(ScriptSnake snake, ScriptRoom room, ScriptState? state = null)
        => _hasModify ? Call("modify", false, state, snake, room) is { } result ? ReadEffects(result) : null : null;

    /// <summary>What the script gives for eating, or null when it does not define <c>onEat</c> or the call failed.</summary>
    public ScriptRewards? OnEat(ScriptSnake snake, ScriptFood food, ScriptRoom room, ScriptState? state = null)
        => _hasOnEat ? Call("onEat", false, state, snake, food, room) is { } result ? ReadRewards(result) : null : null;

    /// <summary>What the script gives the snake that killed another, or null when it does not define <c>onKill</c> or the call failed.</summary>
    public ScriptRewards? OnKill(ScriptSnake snake, ScriptVictim victim, ScriptRoom room, ScriptState? state = null)
        => _hasOnKill ? Call("onKill", false, state, snake, victim, room) is { } result ? ReadRewards(result) : null : null;

    /// <summary>What the script asks of the room, or null when it does not define <c>onTick</c> or the call failed.</summary>
    public ScriptWorldActions? OnTick(ScriptRoom room, ScriptState? state = null)
        => _hasOnTick ? Call("onTick", false, state, room) is { } result ? ReadActions(result) : null : null;

    /// <summary>Runs each function once with a sample, and throws <see cref="ScriptException"/> naming the one that fails.</summary>
    public void Check()
    {
        if (_hasModify) Call("modify", true, new ScriptState(), SampleSnake, SampleRoom);
        if (_hasOnEat) Call("onEat", true, new ScriptState(), SampleSnake, SampleFood, SampleRoom);
        if (_hasOnKill) Call("onKill", true, new ScriptState(), SampleSnake, SampleVictim, SampleRoom);
        if (_hasOnTick) Call("onTick", true, new ScriptState(), SampleRoom);
    }

    /// <summary>Calls a function. The first of <paramref name="data"/> is the one that carries <c>state</c> when a state is given.</summary>
    private JsValue? Call(string function, bool throwOnFailure, ScriptState? state, params object[] data)
    {
        lock (_turn)
        {
            if (Disabled && !throwOnFailure) return null;
            try
            {
                var arguments = data.Select(item => Parse(JsonSerializer.Serialize(item, item.GetType(), DataOptions))).ToArray();
                if (state is not null) arguments[0].AsObject().Set("state", Parse(state.Json));
                var result = _engine.Invoke(function, arguments.Cast<object>().ToArray());
                if (state is not null) state.Json = ReadState(arguments[0].AsObject().Get("state"));
                if (!throwOnFailure) Failures = 0;
                return result;
            }
            catch (Exception exception)
            {
                var message = Describe(exception);
                if (throwOnFailure) throw new ScriptException($"{function}: {message}");
                Failures++;
                LastError = message;
                return null;
            }
        }
    }

    private JsValue Parse(string json) => new JsonParser(_engine).Parse(json);

    /// <summary>The state after a call, as JSON. Something that is not an object starts over; a state that grew too big is a failure.</summary>
    private string ReadState(JsValue state)
    {
        if (!state.IsObject() || state.IsArray()) return "{}";
        var stringify = _engine.Evaluate("JSON").AsObject();
        var json = _engine.Invoke(stringify.Get("stringify"), stringify, [state]);
        var text = json.IsString() ? json.AsString() : "{}";
        if (text.Length > ScriptState.MaxLength) throw new ScriptException($"The state is too big: {text.Length} characters, at most {ScriptState.MaxLength}.");
        return text;
    }

    private static Engine NewEngine() => new(options =>
    {
        options.Strict();
        options.TimeoutInterval(CallTimeout);
        options.MaxStatements(MaxStatementsPerCall);
        options.LimitMemory(MaxMemoryBytes);
        options.LimitRecursion(MaxRecursion);
    });

    private static bool IsFunction(Engine engine, string name) => engine.Evaluate($"typeof {name} === 'function'").AsBoolean();

    private static string Describe(Exception exception) => exception.Message.Trim();

    private static ScriptEffects? ReadEffects(JsValue result)
    {
        if (!result.IsObject()) return null;
        var source = result.AsObject();
        return new ScriptEffects
        {
            ExtraShots = Whole(source, "extraShots", 0, 4),
            BurstExtraShots = Whole(source, "burstExtraShots", 0, 6),
            BoostSpeedBonus = Number(source, "boostSpeedBonus", 0, 3),
            BoostLengthFactor = Number(source, "boostLengthFactor", 1, 4),
            ReloadFactor = Number(source, "reloadFactor", 0.2, 1),
            ShotSpeedFactor = Number(source, "shotSpeedFactor", 1, 4),
            DamageFactor = Number(source, "damageFactor", 0.2, 1),
            RegenPerSecond = Number(source, "regenPerSecond", 0, 50),
            MagnetRadius = Number(source, "magnetRadius", 0, 300),
            AutoFire = Flag(source, "autoFire"),
            Piercing = Flag(source, "piercing"),
            Invulnerable = Flag(source, "invulnerable"),
            FreeShots = Flag(source, "freeShots"),
        };
    }

    private static ScriptRewards? ReadRewards(JsValue result)
    {
        if (!result.IsObject()) return null;
        var source = result.AsObject();
        return new ScriptRewards(Whole(source, "mass", 0, 50) ?? 0, Number(source, "heal", 0, 500) ?? 0);
    }

    private const int MaxDropsPerTick = 3;

    private static ScriptWorldActions? ReadActions(JsValue result)
    {
        if (!result.IsObject()) return null;
        var asked = result.AsObject().Get("dropPrize");
        var kinds = asked.IsString() ? [asked.AsString()]
            : asked.IsArray() ? asked.AsArray().Select(item => item.IsString() ? item.AsString() : string.Empty).ToList() : [];
        return new ScriptWorldActions(kinds.Where(kind => kind.Length > 0).Distinct().Take(MaxDropsPerTick).ToArray());
    }

    private static double? Number(Jint.Native.Object.ObjectInstance source, string name, double min, double max)
    {
        var value = source.Get(name);
        if (!value.IsNumber()) return null;
        var number = value.AsNumber();
        return double.IsFinite(number) ? Math.Clamp(number, min, max) : null;
    }

    private static int? Whole(Jint.Native.Object.ObjectInstance source, string name, int min, int max)
        => Number(source, name, min, max) is { } number ? (int)Math.Round(number) : null;

    private static bool? Flag(Jint.Native.Object.ObjectInstance source, string name)
    {
        var value = source.Get(name);
        return value.IsBoolean() ? value.AsBoolean() : null;
    }
}

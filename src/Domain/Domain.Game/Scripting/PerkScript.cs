using System.Text.Json;
using Jint;
using Jint.Native;
using Jint.Native.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Domain.Game.Scripting;

/// <summary>A snake as a script sees it: plain numbers and text, nothing of the server.</summary>
public sealed record ScriptSnake(
    string Name, int Length, long Mass, int FoodsEaten, double Health, double LowestHealth, bool Boosting, IReadOnlyList<string> Perks);

/// <summary>The room a snake plays in, as a script sees it.</summary>
public sealed record ScriptRoom(int Players, string Name, string Code, int Width, int Height);

/// <summary>The food a snake just ate, as a script sees it.</summary>
public sealed record ScriptFood(long Value, bool IsPrize);

/// <summary>Who is asking: the snake and the room it plays in.</summary>
public sealed record ScriptContext(ScriptSnake Snake, ScriptRoom Room);

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

    private readonly Engine _engine;
    private readonly object _turn = new();
    private readonly bool _hasModify;
    private readonly bool _hasOnEat;

    private PerkScript(Engine engine, bool hasModify, bool hasOnEat)
    {
        _engine = engine;
        _hasModify = hasModify;
        _hasOnEat = hasOnEat;
    }

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
        if (!hasModify && !hasOnEat) throw new ScriptException("A script needs a function named modify or onEat.");
        return new PerkScript(engine, hasModify, hasOnEat);
    }

    /// <summary>The extra effects for a snake, or null when the script does not define <c>modify</c> or the call failed.</summary>
    public ScriptEffects? Modify(ScriptSnake snake, ScriptRoom room)
        => _hasModify ? Call("modify", false, snake, room) is { } result ? ReadEffects(result) : null : null;

    /// <summary>What the script gives for eating, or null when it does not define <c>onEat</c> or the call failed.</summary>
    public ScriptRewards? OnEat(ScriptSnake snake, ScriptFood food, ScriptRoom room)
        => _hasOnEat ? Call("onEat", false, snake, food, room) is { } result ? ReadRewards(result) : null : null;

    /// <summary>Runs each function once with a sample, and throws <see cref="ScriptException"/> naming the one that fails.</summary>
    public void Check()
    {
        if (_hasModify) Call("modify", true, SampleSnake, SampleRoom);
        if (_hasOnEat) Call("onEat", true, SampleSnake, SampleFood, SampleRoom);
    }

    private JsValue? Call(string function, bool throwOnFailure, params object[] data)
    {
        lock (_turn)
        {
            if (Disabled && !throwOnFailure) return null;
            try
            {
                var arguments = data.Select(item => Parse(JsonSerializer.Serialize(item, item.GetType(), DataOptions))).ToArray();
                var result = _engine.Invoke(function, arguments.Cast<object>().ToArray());
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
        };
    }

    private static ScriptRewards? ReadRewards(JsValue result)
    {
        if (!result.IsObject()) return null;
        var source = result.AsObject();
        return new ScriptRewards(Whole(source, "mass", 0, 50) ?? 0, Number(source, "heal", 0, 500) ?? 0);
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

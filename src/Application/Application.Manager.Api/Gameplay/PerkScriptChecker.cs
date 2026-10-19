using Domain.Game.Scripting;

namespace Application.Manager.Api.Gameplay;

public sealed record ScriptCheckResult(bool Ok, string? Error);

/// <summary>Reads a perk script and runs it once with a sample, so the editor can say at once whether it will be accepted.</summary>
public static class PerkScriptChecker
{
    public static ScriptCheckResult Check(string? script)
    {
        try
        {
            PerkScript.Compile(script ?? string.Empty).Check();
            return new ScriptCheckResult(true, null);
        }
        catch (ScriptException exception)
        {
            return new ScriptCheckResult(false, exception.Message);
        }
    }
}

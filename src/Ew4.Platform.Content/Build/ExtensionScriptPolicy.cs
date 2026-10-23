using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Build;

/// <summary>
/// 扩展包仅支持声明式 API 2（D8：不再接受 Lua / API 1）。
/// </summary>
internal static class ExtensionScriptPolicy
{
    public static void EnsureDeclarativeOnly(
        int apiVersion,
        string[]? manifestScripts,
        IEnumerable<ExtensionEffect> effects,
        IEnumerable<ExtensionVictoryRules> victories,
        IEnumerable<(string Script, string Function)> scenarioHandlers)
    {
        if (apiVersion < 2)
        {
            throw new RuleViolation(
                "extension.version",
                "Extension API 1 and Lua scripts are no longer supported; set apiVersion to 2 and use declarative JSON.");
        }

        if (manifestScripts is { Length: > 0 })
        {
            throw new RuleViolation(
                "extension.script",
                "API 2 extensions cannot list rewrite/scripts; use declarative JSON fields (scaleBy, targetCategory, stateKey, …).");
        }

        if (effects.Any(e => e.Script != null))
        {
            throw new RuleViolation(
                "extension.script",
                "API 2 skill/status effects cannot use script/function handlers.");
        }

        foreach (ExtensionVictoryRules rules in victories)
        {
            if (Conditions(rules.Win).Concat(rules.Lose == null ? [] : Conditions(rules.Lose)).Any(c => c.Type == "script"))
            {
                throw new RuleViolation(
                    "extension.script",
                    "API 2 victory rules cannot use type 'script'; use declarative victory conditions.");
            }
        }

        if (scenarioHandlers.Any())
        {
            throw new RuleViolation(
                "extension.script",
                "API 2 scenario events cannot use script/function handlers.");
        }
    }

    private static IEnumerable<VictoryCondition> Conditions(VictoryCondition root) => ExtensionVictory.Conditions(root);
}

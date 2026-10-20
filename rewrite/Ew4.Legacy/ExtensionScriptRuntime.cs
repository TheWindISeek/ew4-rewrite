using Ew4.Core;
using MoonSharp.Interpreter;

namespace Ew4.Legacy;

// Each invocation gets a fresh interpreter; persistent state is returned explicitly.
public sealed class ExtensionScriptRuntime : IRuleScriptRuntime
{
    private readonly IReadOnlyDictionary<string, string> sources;
    public ExtensionScriptRuntime(IReadOnlyDictionary<string, string> sources)
    {
        this.sources = sources;
        foreach (var pair in sources)
        {
            try { NewScript().LoadString(pair.Value, null, pair.Key); }
            catch (InterpreterException e) { throw new RuleViolation("extension.lua_syntax", pair.Key + ": " + e.DecoratedMessage); }
        }
    }
    private static Script NewScript() => new(CoreModules.Basic | CoreModules.Table | CoreModules.String | CoreModules.Math)
    { Options = { DebugPrint = _ => { } } };

    public RuleScriptResult Run(string file, string function, RuleScriptContext context, Ew4Random random)
    {
        if (!sources.TryGetValue(file, out var source)) throw new RuleViolation("extension.lua_missing", file);
        var script = NewScript();
        script.Globals.Set("load", DynValue.Nil); script.Globals.Set("loadfile", DynValue.Nil); script.Globals.Set("dofile", DynValue.Nil);
        var math = script.Globals.Get("math").Table;
        math.Set("random", DynValue.Nil); math.Set("randomseed", DynValue.Nil);
        // Remove native bulk allocation helpers that bypass the VM instruction counter.
        var strings = script.Globals.Get("string").Table;
        foreach (string name in new[] { "rep", "format", "gsub" }) strings.Set(name, DynValue.Nil);
        script.Globals.Get("table").Table.Set("concat", DynValue.Nil);
        var ctx = new Table(script);
        ctx.Set("event", DynValue.NewString(context.Event)); ctx.Set("level", DynValue.NewNumber(context.Level));
        ctx.Set("round", DynValue.NewNumber(context.Round)); ctx.Set("value", DynValue.NewNumber(context.Value));
        Table Combatant(Combatant? unit)
        {
            var t = new Table(script);
            if (unit == null) return t;
            t.Set("health", DynValue.NewNumber(unit.Health)); t.Set("maxHealth", DynValue.NewNumber(unit.Profile.MaxHealth));
            t.Set("category", DynValue.NewString(unit.Profile.Category)); t.Set("profile", DynValue.NewString(unit.Profile.Id));
            t.Set("atSea", DynValue.NewBoolean(unit.AtSea));
            t.Set("has_tag", DynValue.NewCallback((_, args) => DynValue.NewBoolean(unit.Profile.Traits.Contains(args[args.Count - 1].String))));
            return t;
        }
        ctx.Set("source", DynValue.NewTable(Combatant(context.Source))); ctx.Set("target", DynValue.NewTable(Combatant(context.Target)));
        var state = new Table(script);
        foreach (var pair in context.State) state.Set(pair.Key, DynValue.NewNumber(pair.Value));
        ctx.Set("state", DynValue.NewTable(state));
        ctx.Set("random", DynValue.NewCallback((_, args) =>
        {
            double n = args[args.Count - 1].Number;
            if (!double.IsFinite(n) || n < 1 || n > int.MaxValue || n != Math.Truncate(n))
                throw new ScriptRuntimeException("random(bound) requires a positive integer");
            return DynValue.NewNumber(random.Next((int)n));
        }));
        try
        {
            // Source initialization and handler execution share a deterministic instruction budget.
            int budget = 200_000;
            DynValue Bounded(DynValue callable, params DynValue[] args)
            {
                var coroutine = script.CreateCoroutine(callable).Coroutine;
                coroutine.AutoYieldCounter = 1000;
                DynValue result;
                do
                {
                    if ((budget -= 1000) < 0) throw new RuleViolation("extension.lua_budget", file + ": instruction budget exceeded");
                    result = coroutine.Resume(args); args = [];
                } while (coroutine.State != CoroutineState.Dead);
                return result;
            }
            Bounded(script.LoadString(source, null, file));
            var handler = script.Globals.Get(function);
            if (handler.Type != DataType.Function) throw new RuleViolation("extension.lua_function", file + ": " + function);
            var answer = Bounded(handler, DynValue.NewTable(ctx));
            var value = answer.Type == DataType.Table ? answer.Table.Get("value") : answer;
            if (value.Type == DataType.Boolean) value = DynValue.NewNumber(value.Boolean ? 1 : 0);
            if (value.Type != DataType.Number || !double.IsFinite(value.Number)) throw new RuleViolation("extension.lua_result", file + ": return a number or {value=number,state=table}");
            var saved = answer.Type == DataType.Table ? answer.Table.Get("state") : DynValue.Nil;
            Dictionary<string, long>? counters = null;
            if (!saved.IsNil())
            {
                if (saved.Type != DataType.Table) throw new RuleViolation("extension.lua_state", "Script state must be a table");
                counters = new(StringComparer.Ordinal);
                foreach (var pair in saved.Table.Pairs)
                {
                    if (counters.Count >= 128 || pair.Key.Type != DataType.String || pair.Value.Type != DataType.Number
                        || !double.IsFinite(pair.Value.Number) || Math.Abs(pair.Value.Number) > 9_007_199_254_740_991d || Math.Truncate(pair.Value.Number) != pair.Value.Number)
                        throw new RuleViolation("extension.lua_state", "State requires at most 128 integer values with string keys");
                    counters.Add(pair.Key.String, checked((long)pair.Value.Number));
                }
            }
            return new(value.Number, counters);
        }
        catch (InterpreterException e) { throw new RuleViolation("extension.lua_runtime", file + ": " + e.DecoratedMessage); }
    }
}

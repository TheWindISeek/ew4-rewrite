using Ew4.Interface.Common;
using Ew4.Interface.Editor;
using Ew4.Interface.Plans;

namespace Ew4.Service.Plans;

/// <summary>用委托定义 Step 的便捷工厂；复杂 Step 可直接实现 <see cref="IStep"/>。</summary>
public static class Steps
{
    public static IStep Of(string id, PlanKey[] reads, PlanKey[] writes, Action<PlanContext> run, TimeSpan? timeout = null) =>
        new DelegateStep(id, new StepSpec(reads, writes, timeout), run);

    /// <summary>带"可能不存在的输入"（<see cref="StepSpec.OptionalReads"/>）的 Step。</summary>
    public static IStep Of(string id, PlanKey[] reads, PlanKey[] optionalReads, PlanKey[] writes, Action<PlanContext> run) =>
        new DelegateStep(id, new StepSpec(reads, writes) { OptionalReads = optionalReads }, run);

    public static IStep Of(string id, Action<PlanContext> run) => Of(id, [], [], run);

    /// <summary>把 <see cref="EditorOutcome{T}"/> 的失败转成 <see cref="OperationException"/>（映射为 400 {code,message}）。</summary>
    public static T Unwrap<T>(EditorOutcome<T> outcome) =>
        outcome.Success
            ? outcome.Value!
            : throw new OperationException(outcome.Error!.Code, outcome.Error.Message);

    private sealed class DelegateStep(string id, StepSpec spec, Action<PlanContext> run) : IStep
    {
        public string Id => id;

        public StepSpec Spec => spec;

        public void Run(PlanContext context) => run(context);
    }
}

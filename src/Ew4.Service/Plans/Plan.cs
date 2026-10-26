using Ew4.Interface.Host;
using Ew4.Interface.Plans;

namespace Ew4.Service.Plans;

/// <summary>同一资源上后到的请求怎么办。</summary>
public enum ConflictPolicy
{
    /// <summary>排队等待（默认）。</summary>
    Queue,

    /// <summary>资源忙则立即拒绝（409）。</summary>
    Reject,
}

/// <summary>Plan 中的一项：Step、是否可选、执行条件。</summary>
/// <param name="Optional">可选 Step 失败只记 diagnostics，不中止 Plan。</param>
/// <param name="When">
/// 执行条件；返回 false 则跳过该 Step（正常路径，不记 diagnostics）。谓词只能读该 Step 已声明的
/// <see cref="Ew4.Interface.Plans.StepSpec.Reads"/> / <see cref="Ew4.Interface.Plans.StepSpec.OptionalReads"/>。
/// 可能被跳过的 Step，其写入等同"可能不存在"：下游只能经 OptionalReads 读取。
/// </param>
public sealed record PlanStep(IStep Step, bool Optional, Func<PlanContext, bool>? When = null);

/// <summary>一个 Request 对应的有序 Step 序列。</summary>
/// <param name="Resource">由请求载荷得到资源键；返回 null 表示不需要仲裁。</param>
/// <param name="OnFailure">必需 Step 抛异常时的响应转换：返回非 null 即作为该请求的响应，返回 null 则异常照常上抛。</param>
public sealed record Plan(
    string Request,
    IReadOnlyList<PlanStep> Steps,
    Func<OperationPayload, string?>? Resource,
    ConflictPolicy Conflict,
    Func<Exception, OperationResponse?>? OnFailure = null);

/// <summary>注册表定义有误（启动期抛出）。</summary>
public sealed class PlanDefinitionException(string message) : Exception(message);

/// <summary>资源忙且策略为 <see cref="ConflictPolicy.Reject"/>。</summary>
public sealed class PlanBusyException(string resource)
    : Ew4.Interface.Common.OperationException("plan.busy", "Resource is busy: " + resource);

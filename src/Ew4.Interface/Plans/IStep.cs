using System;
using System.Collections.Generic;
using System.Linq;
namespace Ew4.Interface.Plans;

/// <summary>Step 的读写声明，供注册时校验与运行时越权检查。</summary>
/// <param name="Reads">必定存在的输入键：注册时要求更早的必需 Step 写入（请求输入键无需声明提供者）。</param>
/// <param name="Writes">会写入的键。</param>
/// <param name="Timeout">非 null 时超过该时间视为失败。</param>
public sealed record StepSpec(
    IReadOnlyList<PlanKey> Reads,
    IReadOnlyList<PlanKey> Writes,
    TimeSpan? Timeout = null)
{
    public static readonly StepSpec None = new([], []);

    /// <summary>可能不存在的输入键（上游可选 Step 的产物）；必须用 <see cref="PlanContext.TryGet{T}"/> 读取。</summary>
    public IReadOnlyList<PlanKey> OptionalReads { get; init; } = [];
}

/// <summary>底层原子操作：模块暴露，Service 的注册表把它们组装成 Plan。对外同步；内部可以自行异步并等待完成。</summary>
public interface IStep
{
    /// <summary>全局唯一，形如 <c>module.action</c>。</summary>
    string Id { get; }

    StepSpec Spec { get; }

    /// <summary>失败用异常表示（<see cref="Common.OperationException"/> 等）；成功信息通过 <see cref="PlanContext"/> 写键传递。</summary>
    void Run(PlanContext context);
}

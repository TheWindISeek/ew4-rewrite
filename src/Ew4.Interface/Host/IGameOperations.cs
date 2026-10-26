using Ew4.Interface.Operations;

namespace Ew4.Interface.Host;

/// <summary>
/// Backend 唯一应对齐的 Service 契约。
/// 流程：组装 <see cref="OperationDescriptor"/> + <see cref="OperationPayload"/> → <see cref="Invoke"/>；
/// Service 内静态资源直接服务，其余交给 PlanRunner 按注册表展开 Step。
/// </summary>
public interface IGameOperations
{
    /// <summary>当前激活内容快照 revision；无激活 mod 时为 null（Play 通道前置条件）。</summary>
    string? ActiveContentRevision { get; }

    /// <summary>执行一次操作；不解析 HTTP，只认 Interface 身份与载荷。</summary>
    OperationResponse Invoke(OperationDescriptor descriptor, OperationPayload payload);
}

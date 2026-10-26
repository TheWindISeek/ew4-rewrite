using System;
using System.Collections.Generic;
using System.Linq;
using Ew4.Interface.Host;
using Ew4.Interface.Operations;

namespace Ew4.Interface.Plans;

/// <summary>框架预置键。</summary>
public static class PlanKeys
{
    /// <summary>请求身份（由运行器提供）。</summary>
    public static readonly PlanKey<OperationDescriptor> Descriptor = new("request.descriptor");

    /// <summary>请求载荷（由运行器提供）。</summary>
    public static readonly PlanKey<OperationPayload> Payload = new("request.payload");

    /// <summary>
    /// 整份响应（状态码、内容类型、字节）。存在时原样返回，不再按槽位合成；
    /// 用于保持既有 HTTP 契约不变的终端。
    /// </summary>
    public static readonly PlanKey<OperationResponse> Response = new("response", new(RawResponseSlot));

    /// <summary>可选 Step 失败信息；由运行器写入。</summary>
    public static readonly PlanKey<StepDiagnostic> Diagnostics = new("diagnostics", new("diagnostics", SlotKind.Append));

    public const string RawResponseSlot = "$response";

    public static readonly HashSet<PlanKey> Provided = new HashSet<PlanKey> { Descriptor, Payload };
}

/// <summary>可选 Step 失败的记录。</summary>
public sealed record StepDiagnostic(string Step, string Code, string Message);

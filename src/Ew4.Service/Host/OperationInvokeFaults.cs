using System.Text.Json;
using Ew4.Interface.Common;
using Ew4.Interface.Host;
using Ew4.Platform.Engine;
using Ew4.Service.Plans;

namespace Ew4.Service.Host;

/// <summary>将下层可预期失败映射为 Invoke 的 <see cref="OperationResponse"/>，避免 GameOperationExecutor 散落 catch。</summary>
internal static class OperationInvokeFaults
{
    public static OperationResponse? TryClientError(Exception error) =>
        error switch
        {
            PlanBusyException busy => OperationResponse.ConflictJson(JsonSerializer.Serialize(new { code = busy.Code, message = busy.Message })),
            OperationException fault => Client(fault.Code, fault.Message),
            RuleViolation rule => Client(rule.Code, rule.Message),
            _ => null,
        };

    private static OperationResponse Client(string code, string message) =>
        OperationResponse.ClientErrorJson(JsonSerializer.Serialize(new { code, message }));
}

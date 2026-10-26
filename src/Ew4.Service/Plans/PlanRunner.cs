using System.Collections.Concurrent;
using System.Text.Json;
using Ew4.Interface.Common;
using Ew4.Interface.Host;
using Ew4.Interface.Operations;
using Ew4.Interface.Plans;

namespace Ew4.Service.Plans;

/// <summary>按注册表执行 Plan：仲裁资源、顺序跑 Step、合成响应。对外同步。</summary>
public sealed class PlanRunner
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly PlanRegistry _registry;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    public PlanRunner(PlanRegistry registry) => _registry = registry;

    public bool CanRun(OperationDescriptor descriptor) =>
        RequestNames.TryFor(descriptor, out string? name) && _registry.Find(name!) != null;

    public OperationResponse Run(OperationDescriptor descriptor, OperationPayload payload)
    {
        if (!RequestNames.TryFor(descriptor, out string? name) || _registry.Find(name!) is not { } plan)
        {
            throw new ArgumentException("No plan registered for descriptor: " + descriptor, nameof(descriptor));
        }

        string? resource = plan.Resource?.Invoke(payload);
        if (resource == null)
        {
            return Execute(plan, descriptor, payload);
        }

        SemaphoreSlim gate = _gates.GetOrAdd(resource, _ => new SemaphoreSlim(1, 1));
        if (plan.Conflict == ConflictPolicy.Reject)
        {
            if (!gate.Wait(0))
            {
                throw new PlanBusyException(resource);
            }
        }
        else
        {
            gate.Wait();
        }

        try
        {
            return Execute(plan, descriptor, payload);
        }
        finally
        {
            gate.Release();
        }
    }

    private static OperationResponse Execute(Plan plan, OperationDescriptor descriptor, OperationPayload payload)
    {
        var context = new PlanContext();
        context.Provide(PlanKeys.Descriptor, descriptor);
        context.Provide(PlanKeys.Payload, payload);
        try
        {
            foreach (PlanStep entry in plan.Steps)
            {
                RunStep(context, entry);
            }
        }
        catch (Exception error)
        {
            OperationResponse? handled = plan.OnFailure?.Invoke(error);
            if (handled == null)
            {
                throw;
            }

            return handled;
        }

        return Assemble(context);
    }

    private static void RunStep(PlanContext context, PlanStep entry)
    {
        IStep step = entry.Step;
        if (entry.When != null)
        {
            bool run;
            using (context.Enter(step))
            {
                run = entry.When(context);
            }

            if (!run)
            {
                return;
            }
        }

        try
        {
            using (context.Enter(step))
            {
                if (step.Spec.Timeout is { } timeout)
                {
                    Task task = Task.Run(() => step.Run(context));
                    if (!task.Wait(timeout))
                    {
                        throw new OperationException("plan.timeout", $"Step '{step.Id}' timed out.");
                    }
                }
                else
                {
                    step.Run(context);
                }

                context.Commit();
            }
        }
        catch (Exception error) when (entry.Optional)
        {
            Exception inner = error is AggregateException { InnerException: { } first } ? first : error;
            string code = inner is OperationException fault ? fault.Code : "plan.step_failed";
            context.AddDiagnostic(new StepDiagnostic(step.Id, code, inner.Message));
        }
        catch (AggregateException error) when (error.InnerException != null)
        {
            // 超时包装下的真实异常：还原，让上层按原类型映射状态码。
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();
        }
    }

    private static OperationResponse Assemble(PlanContext context)
    {
        var exposed = context.Exposed();
        foreach ((PlanKey key, object value) in exposed)
        {
            if (key.Expose!.Slot == PlanKeys.RawResponseSlot)
            {
                return (OperationResponse)value;
            }
        }

        var slots = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach ((PlanKey key, object value) in exposed)
        {
            slots[key.Expose!.Slot] = value;
        }

        return OperationResponse.Json(JsonSerializer.Serialize(slots, Json));
    }
}

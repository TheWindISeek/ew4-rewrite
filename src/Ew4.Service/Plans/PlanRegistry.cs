using Ew4.Interface.Host;
using Ew4.Interface.Plans;

namespace Ew4.Service.Plans;

/// <summary>模块向注册表贡献自己的 Plan。</summary>
public interface IPlanModule
{
    void Register(PlanRegistry registry);
}

/// <summary>Request 名 → Plan 的显式对应表。构建完成后调用 <see cref="Validate"/>。</summary>
public sealed class PlanRegistry
{
    private readonly Dictionary<string, Plan> _plans = new(StringComparer.Ordinal);

    public IReadOnlyCollection<Plan> Plans => _plans.Values;

    public PlanBuilder Request(string name) => new(this, name);

    public Plan? Find(string request) => _plans.GetValueOrDefault(request);

    internal void Add(Plan plan)
    {
        if (!_plans.TryAdd(plan.Request, plan))
        {
            throw new PlanDefinitionException("Duplicate request: " + plan.Request);
        }
    }

    /// <summary>启动期检查；任何一条不通过都抛 <see cref="PlanDefinitionException"/>。</summary>
    public void Validate()
    {
        foreach (Plan plan in _plans.Values)
        {
            Validate(plan);
        }
    }

    private static void Validate(Plan plan)
    {
        if (plan.Steps.Count == 0)
        {
            throw new PlanDefinitionException($"Plan '{plan.Request}' has no steps.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var written = new HashSet<PlanKey>();
        var maybeWritten = new HashSet<PlanKey>();
        var singleWriters = new Dictionary<PlanKey, string>();
        foreach (PlanStep entry in plan.Steps)
        {
            IStep step = entry.Step;
            if (!ids.Add(step.Id))
            {
                throw new PlanDefinitionException($"Plan '{plan.Request}' uses step '{step.Id}' twice.");
            }

            foreach (PlanKey read in step.Spec.Reads)
            {
                if (!PlanKeys.Provided.Contains(read) && !written.Contains(read))
                {
                    throw new PlanDefinitionException(
                        $"Plan '{plan.Request}': step '{step.Id}' reads '{read.Name}' but no earlier step writes it.");
                }
            }

            foreach (PlanKey read in step.Spec.OptionalReads)
            {
                if (!PlanKeys.Provided.Contains(read) && !written.Contains(read) && !maybeWritten.Contains(read))
                {
                    throw new PlanDefinitionException(
                        $"Plan '{plan.Request}': step '{step.Id}' optionally reads '{read.Name}' but no earlier step can write it.");
                }
            }

            foreach (PlanKey write in step.Spec.Writes)
            {
                if (write.Expose is { Kind: SlotKind.Single })
                {
                    if (singleWriters.TryGetValue(write, out string? other))
                    {
                        throw new PlanDefinitionException(
                            $"Plan '{plan.Request}': slot '{write.Expose.Slot}' is written by both '{other}' and '{step.Id}'.");
                    }

                    singleWriters[write] = step.Id;
                }

                // 可选/条件 Step 的写入不保证存在，后续 Step 不能把它当作必有输入。
                (entry.Optional || entry.When != null ? maybeWritten : written).Add(write);
            }
        }
    }
}

/// <summary>Plan 的流式声明；<c>.Step()</c> 只是语法糖，底层是每个 Step 的读写声明。</summary>
public sealed class PlanBuilder
{
    private readonly PlanRegistry _registry;
    private readonly string _request;
    private readonly List<PlanStep> _steps = [];
    private Func<OperationPayload, string?>? _resource;
    private ConflictPolicy _conflict = ConflictPolicy.Queue;
    private Func<Exception, OperationResponse?>? _onFailure;

    internal PlanBuilder(PlanRegistry registry, string request)
    {
        _registry = registry;
        _request = request;
    }

    /// <param name="when">条件 Step：谓词为 false 时跳过。</param>
    public PlanBuilder Step(IStep step, bool optional = false, Func<PlanContext, bool>? when = null)
    {
        _steps.Add(new PlanStep(step, optional, when));
        return this;
    }

    /// <summary>资源键；同键的 Plan 串行。</summary>
    public PlanBuilder Resource(Func<OperationPayload, string?> resource, ConflictPolicy conflict = ConflictPolicy.Queue)
    {
        _resource = resource;
        _conflict = conflict;
        return this;
    }

    public PlanBuilder Resource(string key, ConflictPolicy conflict = ConflictPolicy.Queue) =>
        Resource(_ => key, conflict);

    /// <summary>把必需 Step 的特定异常转成该请求约定的响应（例如游玩接口的 <c>{ok:false}</c> 信封）。</summary>
    public PlanBuilder OnFailure(Func<Exception, OperationResponse?> handler)
    {
        _onFailure = handler;
        return this;
    }

    /// <summary>结束声明并登记。</summary>
    public void Done() => _registry.Add(new Plan(_request, _steps.ToArray(), _resource, _conflict, _onFailure));
}

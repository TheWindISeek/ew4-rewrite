using System;
using System.Collections.Generic;
using System.Linq;
namespace Ew4.Interface.Plans;

/// <summary>
/// 一次请求的键值存储，仅在服务端、本次请求内存活。
/// Step 只能读写自己在 <see cref="StepSpec"/> 里声明的键；写入先放暂存区，Step 成功后才提交，
/// 因此失败或超时的 Step 不会留下半成品。
/// </summary>
public sealed class PlanContext
{
    private readonly Dictionary<string, object> _committed = new(StringComparer.Ordinal);
    private readonly List<PlanKey> _written = [];
    private StepScope? _scope;

    /// <summary>运行器：放入请求输入键（不经过 Step）。</summary>
    public void Provide<T>(PlanKey<T> key, T value) => _committed[key.Name] = value!;

    public T Get<T>(PlanKey<T> key)
    {
        if (!TryGet(key, out T? value))
        {
            throw new InvalidOperationException("Plan key has no value: " + key.Name);
        }

        return value!;
    }

    public bool TryGet<T>(PlanKey<T> key, out T? value)
    {
        EnsureReadable(key);
        if (_scope != null && _scope.Pending.TryGetValue(key.Name, out object? pending))
        {
            value = Unwrap<T>(key, pending);
            return true;
        }

        if (_committed.TryGetValue(key.Name, out object? stored))
        {
            value = Unwrap<T>(key, stored);
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Single 键：覆盖写入；Append 键：追加一项。</summary>
    public void Set<T>(PlanKey<T> key, T value)
    {
        StepScope scope = _scope ?? throw new InvalidOperationException("Set is only allowed inside a step.");
        if (!scope.Step.Spec.Writes.Contains(key))
        {
            throw new InvalidOperationException($"Step '{scope.Step.Id}' did not declare write of '{key.Name}'.");
        }

        if (key.Expose is { Kind: SlotKind.Append })
        {
            if (!scope.Pending.TryGetValue(key.Name, out object? list))
            {
                list = new List<T>();
                scope.Pending[key.Name] = list;
            }

            ((List<T>)list).Add(value);
            return;
        }

        scope.Pending[key.Name] = value!;
    }

    /// <summary>Append 键的全部已提交项。</summary>
    public IReadOnlyList<T> GetAll<T>(PlanKey<T> key) =>
        _committed.TryGetValue(key.Name, out object? stored) ? (List<T>)stored : [];

    public IDisposable Enter(IStep step)
    {
        _scope = new StepScope(step);
        return new Leave(this);
    }

    /// <summary>Step 成功后提交其暂存写入。</summary>
    public void Commit()
    {
        StepScope scope = _scope ?? throw new InvalidOperationException("No active step.");
        foreach (PlanKey key in scope.Step.Spec.Writes)
        {
            if (!scope.Pending.TryGetValue(key.Name, out object? value))
            {
                continue;
            }

            if (key.Expose is { Kind: SlotKind.Append })
            {
                if (!_committed.TryGetValue(key.Name, out object? existing))
                {
                    _committed[key.Name] = value;
                }
                else
                {
                    // 同类型 List<T> 追加：用反射无关的方式合并
                    var target = (System.Collections.IList)existing;
                    foreach (object? item in (System.Collections.IList)value)
                    {
                        target.Add(item);
                    }
                }
            }
            else
            {
                _committed[key.Name] = value;
            }

            if (key.Expose != null && !_written.Contains(key))
            {
                _written.Add(key);
            }
        }
    }

    /// <summary>运行器：把 Diagnostics 追加进已提交区。</summary>
    public void AddDiagnostic(StepDiagnostic diagnostic)
    {
        if (!_committed.TryGetValue(PlanKeys.Diagnostics.Name, out object? list))
        {
            list = new List<StepDiagnostic>();
            _committed[PlanKeys.Diagnostics.Name] = list;
            _written.Add(PlanKeys.Diagnostics);
        }

        ((List<StepDiagnostic>)list).Add(diagnostic);
    }

    /// <summary>已写入且带 <see cref="Expose"/> 的键，按首次写入顺序。</summary>
    public IReadOnlyList<(PlanKey Key, object Value)> Exposed() =>
        _written.Select(k => (k, _committed[k.Name])).ToArray();

    private void EnsureReadable(PlanKey key)
    {
        if (_scope == null || PlanKeys.Provided.Contains(key))
        {
            return;
        }

        if (!_scope.Step.Spec.Reads.Contains(key) && !_scope.Step.Spec.OptionalReads.Contains(key) && !_scope.Step.Spec.Writes.Contains(key))
        {
            throw new InvalidOperationException($"Step '{_scope.Step.Id}' did not declare read of '{key.Name}'.");
        }
    }

    private static T Unwrap<T>(PlanKey<T> key, object stored)
    {
        if (key.Expose is { Kind: SlotKind.Append })
        {
            throw new InvalidOperationException("Use GetAll for append key: " + key.Name);
        }

        return (T)stored;
    }

    private sealed class StepScope(IStep step)
    {
        public IStep Step { get; } = step;

        public Dictionary<string, object> Pending { get; } = new(StringComparer.Ordinal);
    }

    private sealed class Leave(PlanContext owner) : IDisposable
    {
        public void Dispose() => owner._scope = null;
    }
}

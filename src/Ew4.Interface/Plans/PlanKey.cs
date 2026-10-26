using System;
using System.Collections.Generic;
using System.Linq;
namespace Ew4.Interface.Plans;

/// <summary>响应槽位类型。</summary>
public enum SlotKind
{
    /// <summary>只允许一个写入者；响应里是单个值。</summary>
    Single,

    /// <summary>可多次写入；响应里是按写入顺序的数组。</summary>
    Append,
}

/// <summary>声明某个键在 Plan 成功后复制进响应的哪个槽位。</summary>
public sealed record Expose(string Slot, SlotKind Kind = SlotKind.Single);

/// <summary>PlanContext 里的一个类型化键。<see cref="Expose"/> 为 null 表示仅服务端内部使用。</summary>
public abstract record PlanKey(string Name, Expose? Expose)
{
    public abstract Type ValueType { get; }
}

/// <inheritdoc cref="PlanKey"/>
public sealed record PlanKey<T>(string Name, Expose? Expose = null) : PlanKey(Name, Expose)
{
    public override Type ValueType => typeof(T);
}

namespace Ew4.Platform.Content;

/// <summary>合并视图中一条逻辑路径的物理来源（载入期确定）。</summary>
public enum ModPackLayer
{
    Primary,
    Parent,
}

public sealed record ContentRef(string LogicalPath, ModPackLayer Layer, string PhysicalPath);

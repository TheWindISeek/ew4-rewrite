namespace Ew4.Interface.Mod;

public enum ModContentLayer
{
    Primary,
    Parent,
}

public sealed record ModContentRef(string LogicalPath, ModContentLayer Layer, string PhysicalPath);

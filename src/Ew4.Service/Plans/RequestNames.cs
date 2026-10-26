using Ew4.Interface.Operations;
using Ew4.Interface.Play;

namespace Ew4.Service.Plans;

/// <summary>把 <see cref="OperationDescriptor"/>（用户层身份）映射成注册表里的 Request 名。</summary>
public static class RequestNames
{
    public const string PlayAction = "play.action";

    private static readonly Dictionary<EditorOperationKind, string> Editor = OperationCatalog.All
        .Where(t => t.Channel == OperationChannel.Editor && t.RouteOrKind != null
            && Enum.TryParse<EditorOperationKind>(t.RouteOrKind, out _))
        .ToDictionary(t => Enum.Parse<EditorOperationKind>(t.RouteOrKind!), t => t.Id);

    private static readonly Dictionary<PlatformModOperationKind, string> Platform = OperationCatalog.All
        .Where(t => t.Channel == OperationChannel.Platform && t.RouteOrKind != null
            && Enum.TryParse<PlatformModOperationKind>(t.RouteOrKind, out _))
        .ToDictionary(t => Enum.Parse<PlatformModOperationKind>(t.RouteOrKind!), t => t.Id);

    public static bool TryFor(OperationDescriptor descriptor, out string? name)
    {
        switch (descriptor)
        {
            case PlayEngineRouteOperation:
                name = PlayAction;
                return true;
            case PlayOperationDescriptor play:
                name = ForRoute(play.Route);
                return true;
            case EditorOperationDescriptor editor when Editor.TryGetValue(editor.Kind, out string? id):
                name = id;
                return true;
            case PlatformModOperationDescriptor mod:
                name = Platform[mod.Kind];
                return true;
            default:
                name = null;
                return false;
        }
    }

    public static string ForRoute(string route) => "play." + route;

    public static string For(PlatformModOperationKind kind) => Platform[kind];

    public static string For(EditorOperationKind kind) => Editor[kind];
}

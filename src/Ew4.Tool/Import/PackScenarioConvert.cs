using Ew4.Platform.Content;

namespace Ew4.Tool.Import;

/// <summary>
/// 离线工具：把包内 legacy 剧本转为嵌入 JSON + <c>rewrite/mod.json</c>。
/// </summary>
public static class PackScenarioConvert
{
    public sealed record Result(PackScenarioEmbedder.Result Embed);

    public static Result ToEmbeddedDocumentFormat(string packDirectory, bool removePackBtl = false)
    {
        PackScenarioEmbedder.Result embed = PackScenarioEmbedder.Embed(packDirectory, removePackBtl);
        return new(embed);
    }
}

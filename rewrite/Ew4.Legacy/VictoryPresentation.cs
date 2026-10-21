using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using Ew4.Core;

namespace Ew4.Legacy;

public sealed record VictoryLayoutNode
{
    public Dictionary<string, string> Attributes { get; init; } = new();
    public VictoryLayoutNode[] Children { get; init; } = [];
}

public sealed record VictoryPresentation
{
    public VictoryLayoutNode? Layout { get; init; }
    public VictoryLayoutNode? DefeatLayout { get; init; }
    public Dictionary<string, string?> Bindings { get; init; } = new()
    {
        ["name"] = "text_battle", ["round"] = "text_roundval", ["awardMedals"] = "text_award_val",
        ["collectedMedals"] = "text_gain_val", ["minimumRatingRound"] = "text_star", ["highestRatingRound"] = "text_star2",
        ["thresholds"] = "group_starlevel", ["stars"] = "lbox_stars", ["generals"] = "lbox_general",
        ["continue"] = "btn_continue", ["restart"] = "btn_restart", ["exit"] = "btn_exit"
    };
    public string VictoryTitle { get; init; } = "title_victory";
    public string DefeatTitle { get; init; } = "title_failure";
    public string StarImage { get; init; } = "star_middle.png";
    public string EmptyStarImage { get; init; } = "diffcult_1.png";

    public static void Validate(IReadOnlyDictionary<string, VictoryPresentation> screens, byte[] manifestBytes, Func<string, byte[]>? read = null)
    {
        using var manifest = JsonDocument.Parse(manifestBytes);
        var defaults = JsonSerializer.Deserialize<VictoryLayoutNode>(manifest.RootElement.GetProperty("layouts")
            .GetProperty("form_victory"), ExtensionLoader.Json)!;
        var atlases = manifest.RootElement.GetProperty("atlases");
        string[] atlasNames = ["image_ui_hd", "image_item_hd", "image_recruit_hd", "image_recruit_hd2", "image_recruit_hd3",
            "image_menu_hd", "image_newgame_hd", "commanders1_hd", "commanders2_hd", "princesses_hd", "princesses_sketch_hd", "image_ui"];
        var checkedImages = new HashSet<string>(StringComparer.Ordinal);
        bool Image(string name)
        {
            string? atlasName = atlasNames.FirstOrDefault(a => atlases.TryGetProperty(a, out var atlas) && atlas.TryGetProperty(name, out _));
            if (atlasName == null || name.Contains('/') || name.Contains('\\')) return false;
            if (read != null && checkedImages.Add(name))
            {
                byte[] bytes = read("client/ui/" + atlasName + "/" + name);
                if (bytes.Length is 0 or > 4 * 1024 * 1024) return false;
            }
            return true;
        }
        [DoesNotReturn] static void Fail(string detail) => throw new RuleViolation("extension.victory_presentation", detail);
        foreach (var (scenario, screen) in screens)
        {
            if (string.IsNullOrWhiteSpace(scenario) || screen == null || screen.Bindings == null
                || screen.VictoryTitle == null || screen.DefeatTitle == null || screen.StarImage == null || screen.EmptyStarImage == null)
                Fail("Invalid screen: " + scenario);
            if (!Image(screen!.StarImage) || !Image(screen.EmptyStarImage)) Fail("Unknown star image: " + scenario);
            foreach (var tree in new[] { screen.Layout ?? defaults, screen.DefeatLayout ?? screen.Layout ?? defaults })
            {
                var nodes = new List<KeyValuePair<string, VictoryLayoutNode>>();
                int count = 0;
                void Visit(VictoryLayoutNode node, string path, int depth)
                {
                    if (node == null || node.Attributes == null || node.Children == null || depth > 12 || ++count > 256)
                        Fail("Invalid layout tree: " + scenario);
                    var a = node!.Attributes;
                    if (a.Any(p => p.Value == null)) Fail("Null layout attribute: " + scenario);
                    foreach (string field in new[] { "x", "y", "w", "h", "scale", "hscale", "vscale" })
                        if (a.TryGetValue(field, out var number) && (!double.TryParse(number, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double n) || !double.IsFinite(n) || Math.Abs(n) > 100000
                            || field is "w" or "h" or "scale" or "hscale" or "vscale" && n <= 0))
                            Fail("Invalid layout number: " + field);
                    if (depth == 0)
                    {
                        foreach (string size in new[] { "w", "h" })
                            if (!a.TryGetValue(size, out string? value) || !int.TryParse(value, out int n) || n is < 80 or > 2048)
                                Fail("Layout size must be 80..2048: " + scenario);
                    }
                    else
                    {
                        string? id = a.GetValueOrDefault("id"), type = a.GetValueOrDefault("type");
                        if (string.IsNullOrWhiteSpace(id) || id.Contains('/')
                            || type is not ("group" or "groupbox" or "image" or "text" or "listbox" or "grid" or "tmp_button" or "button" or "tmp_intitle"))
                            Fail("Invalid layout control: " + scenario);
                        path = path.Length == 0 ? id! : path + "/" + id;
                        nodes.Add(new(path, node));
                        foreach (string field in new[] { "name", "frm1", "frm2", "frame" })
                            if (a.TryGetValue(field, out var image) && !Image(image)) Fail("Unknown layout image: " + image);
                    }
                    foreach (var child in node.Children) Visit(child, path, depth + 1);
                }
                Visit(tree, "", 0);
                var resolved = new Dictionary<string, string>();
                foreach (var (binding, target) in screen.Bindings)
                {
                    if (!new VictoryPresentation().Bindings.ContainsKey(binding)) Fail("Unknown binding: " + binding);
                    if (target == null) continue;
                    var matches = nodes.Where(p => p.Key == target || !target.Contains('/') && p.Key.EndsWith('/' + target, StringComparison.Ordinal)).ToArray();
                    if (matches.Length != 1) Fail("Missing or ambiguous binding: " + target);
                    resolved[binding] = matches[0].Key;
                    string type = matches[0].Value.Attributes["type"];
                    bool valid = binding is "continue" or "restart" or "exit" ? type.Contains("button")
                        : binding is "stars" or "generals" ? type is "listbox" or "group" or "groupbox" or "grid"
                        : binding == "thresholds" ? type is "group" or "groupbox" : type == "text";
                    if (!valid) Fail("Invalid binding type: " + binding);
                }
                foreach (string action in new[] { "continue", "restart", "exit" })
                    if (!screen.Bindings.TryGetValue(action, out var target) || target == null) Fail("Required action: " + action);
                if (resolved.Values.Distinct().Count() != resolved.Count)
                    Fail("Controls cannot have multiple bindings: " + scenario);
                foreach (string container in new[] { "stars", "generals", "thresholds" })
                    if (resolved.TryGetValue(container, out var parent) && resolved.Any(p => p.Key != container && p.Value.StartsWith(parent + "/", StringComparison.Ordinal)
                        && (container != "thresholds" || p.Key is "continue" or "restart" or "exit")))
                        Fail("Binding would remove or hide another control: " + container);
            }
        }
    }
}

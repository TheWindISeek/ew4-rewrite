using Ew4.Platform.Content;

namespace Ew4.Platform.Editor.Campaign;

public sealed record CampaignCatalogGroups(
    IReadOnlyList<string> Campaign,
    IReadOnlyList<string> Conquest,
    IReadOnlyList<string> Tutorial,
    IReadOnlyList<string> Other);

public static class CampaignCatalog
{
    public static CampaignCatalogGroups ListByMode(IModSource pack)
    {
        var campaign = new List<string>();
        var conquest = new List<string>();
        var tutorial = new List<string>();
        var other = new List<string>();

        foreach (ScenarioIndex index in ScenarioCatalog.List(pack))
        {
            if (index.JsonPath.Contains(".ew4scenario.", StringComparison.Ordinal))
            {
                continue;
            }

            int mode = ScenarioStartHelpers.ResolveMode(index.Id);
            switch (mode)
            {
                case 1:
                    campaign.Add(index.Id);
                    break;
                case 2:
                    conquest.Add(index.Id);
                    break;
                case 4:
                    tutorial.Add(index.Id);
                    break;
                default:
                    other.Add(index.Id);
                    break;
            }
        }

        campaign.Sort(StringComparer.Ordinal);
        conquest.Sort(StringComparer.Ordinal);
        tutorial.Sort(StringComparer.Ordinal);
        other.Sort(StringComparer.Ordinal);

        return new CampaignCatalogGroups(campaign, conquest, tutorial, other);
    }
}

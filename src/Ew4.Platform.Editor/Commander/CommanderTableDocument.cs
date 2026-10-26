using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Ew4.Platform.Content;
using Ew4.Platform.Content.CatalogLoad;

namespace Ew4.Platform.Editor.Commander;

internal static class CommanderTableDocument
{
    public static CommanderTableView Parse(byte[] utf8)
    {
        utf8 = LegacyTableXmlRepair.NormalizeCommanderTable(utf8);
        string xml = Encoding.UTF8.GetString(utf8);
        XElement root = XDocument.Parse(xml, LoadOptions.None).Root
            ?? throw new ContentError("commander.xml", "Missing def_commander.xml root.");
        if (root.Name.LocalName != "commanders")
        {
            throw new ContentError("commander.xml", "Unexpected root: " + root.Name.LocalName);
        }

        var entries = new List<CommanderEntry>();
        foreach (XElement element in root.Elements("commander"))
        {
            entries.Add(ReadEntry(element));
        }

        return new CommanderTableView(entries);
    }

    public static byte[] ApplyEdit(byte[] utf8, CommanderEdit edit)
    {
        utf8 = LegacyTableXmlRepair.NormalizeCommanderTable(utf8);
        string xml = Encoding.UTF8.GetString(utf8);
        XDocument document = XDocument.Parse(xml, LoadOptions.None);
        XElement root = document.Root
            ?? throw new ContentError("commander.xml", "Missing def_commander.xml root.");
        XElement? commander = root.Elements("commander")
            .FirstOrDefault(e => ReadIntAttribute(e, "id") == edit.LegacyId);
        if (commander == null)
        {
            throw new ContentError("commander.missing", "Commander id not found: " + edit.LegacyId);
        }

        switch (edit)
        {
            case CommanderEditSkills skills:
                ApplySkillPatch(commander, skills);
                break;
            case CommanderEditAptitudes aptitudes:
                ApplyAptitudePatch(commander, aptitudes);
                break;
            case CommanderEditLoadout loadout:
                SetIntAttribute(commander, "skill1", loadout.Skill1);
                SetIntAttribute(commander, "skill2", loadout.Skill2);
                SetIntAttribute(commander, "skill3", loadout.Skill3);
                SetIntAttribute(commander, "skill4", loadout.Skill4);
                SetIntAttribute(commander, "infantry", loadout.Infantry);
                SetIntAttribute(commander, "cavalry", loadout.Cavalry);
                SetIntAttribute(commander, "artillery", loadout.Artillery);
                SetIntAttribute(commander, "warship", loadout.Warship);
                SetIntAttribute(commander, "fort", loadout.Fort);
                break;
            default:
                throw new ContentError("commander.edit", "Unsupported commander edit type.");
        }

        return Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
    }

    private static void ApplySkillPatch(XElement commander, CommanderEditSkills skills)
    {
        if (skills.Skill1 is int s1)
        {
            SetIntAttribute(commander, "skill1", s1);
        }

        if (skills.Skill2 is int s2)
        {
            SetIntAttribute(commander, "skill2", s2);
        }

        if (skills.Skill3 is int s3)
        {
            SetIntAttribute(commander, "skill3", s3);
        }

        if (skills.Skill4 is int s4)
        {
            SetIntAttribute(commander, "skill4", s4);
        }
    }

    private static void ApplyAptitudePatch(XElement commander, CommanderEditAptitudes aptitudes)
    {
        if (aptitudes.Infantry is int infantry)
        {
            SetIntAttribute(commander, "infantry", infantry);
        }

        if (aptitudes.Cavalry is int cavalry)
        {
            SetIntAttribute(commander, "cavalry", cavalry);
        }

        if (aptitudes.Artillery is int artillery)
        {
            SetIntAttribute(commander, "artillery", artillery);
        }

        if (aptitudes.Warship is int warship)
        {
            SetIntAttribute(commander, "warship", warship);
        }

        if (aptitudes.Fort is int fort)
        {
            SetIntAttribute(commander, "fort", fort);
        }
    }

    private static CommanderEntry ReadEntry(XElement element)
    {
        int id = ReadIntAttribute(element, "id");
        string name = element.Attribute("name")?.Value ?? "";
        return new CommanderEntry(
            id,
            name,
            ReadIntAttribute(element, "infantry"),
            ReadIntAttribute(element, "cavalry"),
            ReadIntAttribute(element, "artillery"),
            ReadIntAttribute(element, "warship"),
            ReadIntAttribute(element, "fort"),
            ReadIntAttribute(element, "skill1"),
            ReadIntAttribute(element, "skill2"),
            ReadIntAttribute(element, "skill3"),
            ReadIntAttribute(element, "skill4"),
            ReadIntAttribute(element, "rank"),
            ReadIntAttribute(element, "nobilityrank"));
    }

    private static int ReadIntAttribute(XElement element, string name)
    {
        string? raw = element.Attribute(name)?.Value;
        if (string.IsNullOrEmpty(raw))
        {
            return 0;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
    }

    private static void SetIntAttribute(XElement element, string name, int value)
    {
        if (value < 0)
        {
            throw new ContentError("commander.edit", $"Attribute '{name}' must be >= 0.");
        }

        XAttribute? attribute = element.Attribute(name);
        if (attribute == null)
        {
            element.Add(new XAttribute(name, value.ToString(CultureInfo.InvariantCulture)));
        }
        else
        {
            attribute.Value = value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

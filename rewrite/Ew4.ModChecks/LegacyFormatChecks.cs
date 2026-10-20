using System.Text;
using Ew4.Mods;

internal static class LegacyFormatChecks
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; }
        void Reject(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException) { count++; return; }
            throw new Exception("Invalid legacy input accepted");
        }
        byte[] Normalize(string text) => LegacyXml.Normalize(Encoding.UTF8.GetBytes(text), out _);
        foreach (string declaration in new[] { "<?xml version=\"1\"?>", "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" })
        {
            var xml = ModFiles.Xml(Normalize(declaration + "<root><commander rank=\"3\"star=\"4\" name=\"甲乙\"/></root>"));
            Check((string?)xml.Root!.Element("commander")!.Attribute("star") == "4", "TinyXML attribute/declaration compatibility");
        }
        string valid = "<root><!-- a='x'b='y' --><text><![CDATA[<a x='y'z='w'>]]></text></root>";
        Check(Encoding.UTF8.GetString(Normalize(valid)) == valid, "comments and CDATA retained");
        byte[] validDeclared = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<root/>\r\n")).ToArray();
        Check(LegacyXml.Normalize(validDeclared, out bool changed).SequenceEqual(validDeclared) && !changed,
            "valid XML bytes, BOM and declaration preserved for baseline source hashes");
        var partial = ModFiles.Xml(Normalize("<root><layout id='complete'/><!--> abandoned </root>"));
        Check(partial.Root!.Elements().Count() == 1, "completed children before unterminated trailing comment retained");
        foreach (string invalid in new[] { "<root><a></root>", "<root><a><!-- missing", "<root><![CDATA[missing", "<!DOCTYPE root [<!ENTITY x SYSTEM 'file:///unread'>]><root>&x;</root>" })
            Reject(() => ModFiles.Xml(Normalize(invalid)));
        byte[] font = new byte[8 + 12 + 50];
        BitConverter.GetBytes(1).CopyTo(font, 0); BitConverter.GetBytes(18).CopyTo(font, 4);
        BitConverter.GetBytes((ushort)'A').CopyTo(font, 8); font[14] = 9; font[15] = 12;
        Encoding.UTF8.GetBytes("<old-font>trailing overwrite bytes</old-font>").CopyTo(font, 20);
        var decoded = LegacyBinary.Font(font);
        Check(decoded["glyphs"]!.AsObject().Count == 1 && (int)decoded["height"]! == 18, "FNT reads declared records and ignores stale tail");
        Reject(() => LegacyBinary.Font(font[..19]));
        Reject(() => LegacyBinary.Font([]));
        return count;
    }
}

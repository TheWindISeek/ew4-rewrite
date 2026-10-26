namespace Ew4.Platform.Mod.Import;

public sealed record ModImportIdentity(string Id, string Name);

public sealed record PreparedModImport(string Token, ModImportIdentity Identity);

namespace Ew4.Interface.Mod;

/// <summary>Platform 通道；与 <see cref="Operations.PlatformModOperationKind"/> 对应。</summary>
public interface IModOperations
{
    /// <summary>面板 JSON（与 Backend <c>/api/mods</c> 响应 mods 字段同源）。</summary>
    string GetPanelStateJson();

    void Activate(string? modId);

    void Remove(string modId);

    ModImportPrepared StageUploadedFile(string sourcePath, string fileName);

    void Discard(string? token);

    void Commit(string token, string id, string name);
}

/// <summary><see cref="PlatformModOperationKind.PrepareImport"/> 结果。</summary>
public sealed record ModImportPrepared(string Token, string IdentityJson);

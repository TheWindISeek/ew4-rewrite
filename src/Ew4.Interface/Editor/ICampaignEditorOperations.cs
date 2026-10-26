namespace Ew4.Interface.Editor;

/// <summary>战役 / 征服等内容文档操作（Runtime → Editor）。</summary>
public interface ICampaignEditorOperations
{
    EditorOutcome<byte[]> GetWorkspaceCampaignJson(string workspaceId, string campaignId);

    EditorOutcome<string> SaveWorkspaceCampaignJson(string workspaceId, string campaignId, byte[] jsonUtf8, bool smoke);

    EditorOutcome<string> RoundtripWorkspaceJson(string workspaceId, string campaignId);

    EditorOutcome<string> ApplyWorkspaceCampaignEditJson(string workspaceId, string campaignId, byte[] editUtf8);

    /// <summary>自由编辑工作区里新建空场景；body: {"id","name","mapId","crop":{"x","y","w","h"}?}。</summary>
    EditorOutcome<string> CreateWorkspaceCampaignJson(string workspaceId, byte[] createUtf8);

    EditorOutcome<byte[]> BtlBytesToDocumentJson(string campaignId, byte[] btlBytes);
}

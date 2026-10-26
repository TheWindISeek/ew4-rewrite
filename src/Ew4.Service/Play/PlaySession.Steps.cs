// PlaySession 的开局/读档阶段。每个方法对应 Plan 里的一个 Step（见 Ew4.Service/Plans/PlayPlans.cs）。
//
// 分两类：
//   · 计算类（Check*/Open*/Decode*）：只读内容与账号，不改会话状态，失败时会话保持原样；
//   · 落地类（Install*）：替换会话状态的唯一提交点（清队列、写账号、换 game）。
//
// Start/Load 的私有方法按同样顺序调用它们，因此 Plan 与旧 route 行为一致。

using Ew4.Platform.Content.StaticData;
using Ew4.Platform.Engine;
using Ew4.Platform.Persistence.Serialization;

namespace Ew4.Service.Play;

public sealed partial class PlaySession
{
    /// <summary>战役解锁校验。</summary>
    internal void CheckUnlocked(PlayCommand command)
    {
        if (!CampaignProgress.Accessible(content, Account(), command.Scenario!))
            throw new RuleViolation("campaign.locked", "请先完成对应黄圈目标并赢得战役，解锁此隐藏关卡");
    }

    /// <summary>由内容构造新局初始状态（只读）。</summary>
    internal StaticDataScenarioStart OpenStart(PlayCommand command) =>
        content.Start(command.Scenario!, command.Country, Account(), command.LocalPlayers);

    /// <summary>落地新局：清队列/撤销、领取阻塞项并结算、写账号、替换 game。</summary>
    internal void InstallStarted(PlayCommand command, StaticDataScenarioStart next)
    {
        flow.Clear();
        undo.Clear();
        next = next with { Session = SettleBattle(Adopt(next.Session with { BattleId = Guid.NewGuid().ToString("N") })) };
        SetAccount(next.Session.Account); CommitProfile();
        game = next; country = command.Country;
        aiRandom = NewAiSeed();
        tutorialCheckpoint = null;
    }

    /// <summary>读槽位并解码成局面（只读）：必要时迁移装备数值、合并账号解锁与辖区。</summary>
    internal StaticDataScenarioStart DecodeSlot(PlaySlot slot)
    {
        var start = content.Start(slot.Scenario, slot.Country, Account(), slot.LocalPlayers);
        GameSession state;
        try
        {
            state = SessionSave.Decode(slot.Session, new(start.Session.ContentRevision, start.Session.World.ContentRevision, start.Session.Account.ContentRevision));
        }
        catch (RuleViolation error) when (error.Code == "save.content_revision" && content.CorrectOriginalEquipment)
        {
            var prior = content.WithoutEquipmentCorrections().Start(slot.Scenario, slot.Country, localPlayers: slot.LocalPlayers);
            // Authenticate the old content binding before upgrading resolved bonuses.
            state = SessionSave.Decode(slot.Session, new(prior.Session.ContentRevision, prior.Session.World.ContentRevision, prior.Session.Account.ContentRevision));
            state = UpgradeEquipment(state, prior, start);
        }
        if (start.Session.World.Jurisdictions != null)
        {
            if (state.World.Jurisdictions == null)
                state = state with { World = state.World with { Jurisdictions = start.Session.World.Jurisdictions.Copy() with { CenterCaptureVersion = 0 } } };
            state = state with { World = JurisdictionClaims.UpgradeCapturedDistricts(state.World, start.Session.World) };
        }
        var profile = Account();
        var unlocks = profile.UnlockedCampaigns.Union(state.Account.UnlockedCampaigns, StringComparer.Ordinal).ToArray();
        if (profile.Revision > state.Account.Revision) state = state with { Account = profile };
        if (!state.Account.UnlockedCampaigns.SequenceEqual(unlocks))
            state = state with { Account = state.Account with { Revision = checked(Math.Max(profile.Revision, state.Account.Revision) + 1), UnlockedCampaigns = unlocks } };
        return start with { Session = state };
    }

    /// <summary>落地读档：恢复阻塞队列、结算、写账号、替换 game，并恢复教程检查点与 AI 随机状态。</summary>
    internal void InstallLoaded(PlaySlot slot, StaticDataScenarioStart decoded)
    {
        flow.Clear();
        undo.Clear();
        if (slot.Flow is { Count: > 0 }) flow.Restore(slot.Flow);
        GameSession state = SettleBattle(Adopt(decoded.Session));
        state.Validate();
        SetAccount(state.Account); CommitProfile();
        game = decoded with { Session = state }; country = slot.Country;
        tutorialCheckpoint = slot.Tutorial;
        aiRandom = slot.AiRandom is ulong saved and not 0 ? saved : NewAiSeed();
    }

    /// <summary>读取槽位文件。</summary>
    internal PlaySlot ReadSlotFor(PlayCommand command) => ReadSlot(SavePath(command.Slot, command.Mode, true));

    internal void ValidateSelection(PlayCommand command) => ValidateLocalSelection(command);

    internal void EndCurrentBattle() => EndBattleProfile();

    internal void SaveAuto() => AutoSave();

    /// <summary>当前局面的响应信封。</summary>
    internal string ViewEnvelope() => PlayJsonEnvelope.Success(View());
}

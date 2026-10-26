namespace Ew4.Interface.Play;

/// <summary>游玩门面：按职责拆分，由 PlayPlans 的 Step 调用。</summary>
public interface IPlayFacade
{
    /// <summary>任意 play route，与封存 <c>rpc(route)</c> 一致。</summary>
    IPlayJsonInvoker Json { get; }

    /// <summary>仅 save / load / saves。</summary>
    IPlayPersistence Persistence { get; }

    /// <summary>选局 catalog、start、大本营与商店类 route。</summary>
    IPlayContentRpc Content { get; }

    /// <summary>仅 route=action 及 <see cref="Operations.PlayActionOps"/> 子 op。</summary>
    IPlayEngine Engine { get; }
}

/// <summary>槽位读写；不经 Engine.Apply。</summary>
public interface IPlayPersistence
{
    string InvokeEnvelope(string contentRevision, string route, string body);
}

/// <summary>持局菜单与开局管线 RPC。</summary>
public interface IPlayContentRpc
{
    string InvokeEnvelope(string contentRevision, string route, string body);
}

/// <summary>地图回合 action RPC。</summary>
public interface IPlayEngine
{
    string InvokeEnvelope(string contentRevision, string body, string? actionOp = null);
}

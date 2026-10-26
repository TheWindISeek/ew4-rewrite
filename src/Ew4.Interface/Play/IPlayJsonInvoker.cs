namespace Ew4.Interface.Play;

/// <summary>
/// 封存游玩 JSON 协议（<c>{ ok, data }</c> / <c>{ ok, code, message }</c>）。
/// HTTP 宿主只做传输；强类型分平面见 <see cref="IPlayFacade"/>.
/// </summary>
public interface IPlayJsonInvoker
{
    /// <summary>
    /// 在当前激活内容快照下执行一条游玩路由，返回完整 JSON 信封字符串。
    /// </summary>
    /// <param name="contentRevision">当前激活内容快照的 revision（对账用）。</param>
    string InvokeEnvelope(string contentRevision, string route, string body);
}

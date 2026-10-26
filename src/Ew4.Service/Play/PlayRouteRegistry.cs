using System.IO;
using System.Text.Json;
using Ew4.Interface.Play;
using Ew4.Platform.Engine;

namespace Ew4.Service.Play;

/// <summary>
/// 游玩 route → <see cref="PlaySession"/> 用例；与 <see cref="PlayRoutes.All"/> 一一对应。
/// 无 HTTP、无 JSON 信封（信封由 <see cref="Invoke"/> 统一包装）。
/// </summary>
internal sealed class PlayRouteRegistry
{
    private delegate object RouteHandler(PlaySession session, string body);

    private readonly Dictionary<string, RouteHandler> _handlers;

    private PlayRouteRegistry(Dictionary<string, RouteHandler> handlers)
    {
        _handlers = handlers;
        foreach (string route in PlayRoutes.All)
        {
            if (!_handlers.ContainsKey(route))
            {
                throw new InvalidOperationException("Missing play route registration: " + route);
            }
        }
    }

    public static PlayRouteRegistry CreateDefault()
    {
        var handlers = new Dictionary<string, RouteHandler>(StringComparer.Ordinal)
        {
            [PlayRoutes.Catalog] = static (s, _) => s.RouteCatalog(),
            [PlayRoutes.CountryRegistry] = static (s, _) => s.RouteCountryRegistry(),
            [PlayRoutes.Countries] = static (s, body) => s.RouteCountries(body),
            [PlayRoutes.LocalPlayers] = static (s, body) => s.RouteLocalPlayers(body),
            [PlayRoutes.Headquarters] = static (s, _) => s.RouteHeadquarters(),
            [PlayRoutes.Deployment] = static (s, body) => s.RouteDeployment(body),
            [PlayRoutes.UnitInfo] = static (s, body) => s.RouteUnitInfo(body),
            [PlayRoutes.Commander] = static (s, body) => s.RouteCommander(body),
            [PlayRoutes.Commerce] = static (s, body) => s.RouteCommerce(body),
            [PlayRoutes.Items] = static (s, body) => s.RouteItems(body),
            [PlayRoutes.Exchange] = static (s, body) => s.RouteExchange(body),
            [PlayRoutes.Start] = static (s, body) => s.RouteStart(body),
            [PlayRoutes.Leave] = static (s, body) => s.RouteLeave(body),
            [PlayRoutes.Action] = static (s, body) => s.RouteAction(body),
            [PlayRoutes.Save] = static (s, body) => s.RouteSave(body),
            [PlayRoutes.Saves] = static (s, body) => s.RouteSaves(body),
            [PlayRoutes.Load] = static (s, body) => s.RouteLoad(body),
        };

        return new PlayRouteRegistry(handlers);
    }

    public string Invoke(PlayCoordinator coordinator, string route, string body)
    {
        if (!_handlers.TryGetValue(route, out RouteHandler? handler))
        {
            return PlayJsonEnvelope.Failure(new RuleViolation("client.route", "Unknown command"));
        }

        return coordinator.WithSession(session =>
        {
            try
            {
                object data = handler(session, body);
                return PlayJsonEnvelope.Success(data);
            }
            catch (Exception error) when (error is RuleViolation or IOException or JsonException or InvalidOperationException
                or ArgumentException or KeyNotFoundException or OverflowException)
            {
                return PlayJsonEnvelope.Failure(error);
            }
        });
    }
}

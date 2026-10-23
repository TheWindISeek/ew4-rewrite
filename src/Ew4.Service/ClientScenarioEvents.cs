using Ew4.Platform.Engine;

namespace Ew4.Service;

public sealed partial class ClientGame
{
    private object? ScenarioPopupView(WorldState world)
    {
        var ev = ScenarioRules.Pending(world);
        return ev == null ? null : new { ev.Id, title = content.Label(ev.Popup!.Title), text = content.Label(ev.Popup.Text),
            image = ev.Popup.Image, imageAlt = content.Label(ev.Popup.ImageAlt),
            choices = ev.Popup.Choices.Select(c => new { c.Id, text = content.Label(c.Text),
                available = c.Condition == null || ScenarioRules.Match(world, c.Condition, content.Extensions.Bus.Scripts) }).ToArray() };
    }
}

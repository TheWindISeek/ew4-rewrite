using System.Collections.Generic;

namespace Ew4.Platform.Engine;

public sealed record EngineResult(GameSession Session, IReadOnlyList<WorldNotice> Notices);

public sealed record NewGameOptions;

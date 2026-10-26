using Ew4.Interface.Common;

namespace Ew4.Interface.Editor;

public sealed record EditorWriteOutcome(string Path, IReadOnlyList<Diagnostic> Diagnostics);

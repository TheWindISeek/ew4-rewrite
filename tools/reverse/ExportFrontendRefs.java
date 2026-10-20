// Export callers of the original presentation resource names for layout verification.
// @category EW4
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.DecompInterface;
import java.nio.file.*;
import java.util.*;

public class ExportFrontendRefs extends GhidraScript {
    public void run() throws Exception {
        var names = Set.of("attack.png", "build_side.png", "image_line_1", "board_smallgenerals.png", "tex_conquest_1793", "form_selconquest");
        var functions = new LinkedHashSet<ghidra.program.model.listing.Function>();
        var data = currentProgram.getListing().getDefinedData(true);
        while (data.hasNext()) {
            var value = data.next();
            if (!value.hasStringValue() || !names.contains(value.getValue().toString())) continue;
            var refs = getReferencesTo(value.getAddress());
            for (var ref : refs) {
                var function = getFunctionContaining(ref.getFromAddress());
                if (function != null) functions.add(function);
            }
        }
        var out = Path.of(getScriptArgs()[0]);
        var decompiler = new DecompInterface(); decompiler.openProgram(currentProgram);
        try {
            for (var function : functions) {
                var result = decompiler.decompileFunction(function, 45, monitor);
                if (!result.decompileCompleted()) throw new IllegalStateException(result.getErrorMessage());
                var name = String.format("%08x.c", function.getEntryPoint().getOffset()-currentProgram.getImageBase().getOffset());
                Files.writeString(out.resolve(name), "/* Decompiled evidence; inferred types, not original source. */\n"+result.getDecompiledFunction().getC());
                println("FRONTEND_REF " + name);
            }
        } finally { decompiler.dispose(); }
    }
}

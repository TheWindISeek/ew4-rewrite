// Export only the map-border drawing neighborhood and its embedded float constants.
// @category EW4
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.DecompInterface;
import java.nio.file.*;

public class ExportFrontendCalibration extends GhidraScript {
    public void run() throws Exception {
        var output = Path.of(getScriptArgs()[0]);
        Files.createDirectories(output);
        var decompiler = new DecompInterface();
        decompiler.openProgram(currentProgram);
        long base = currentProgram.getImageBase().getOffset();
        int count = 0;
        try {
            var functions = currentProgram.getFunctionManager().getFunctions(toAddr(base + 0x95410), true);
            while (functions.hasNext()) {
                var function = functions.next();
                long relative = function.getEntryPoint().getOffset() - base;
                if (relative >= 0x96978) break;
                var result = decompiler.decompileFunction(function, 45, monitor);
                if (!result.decompileCompleted()) throw new IllegalStateException(result.getErrorMessage());
                Files.writeString(output.resolve(String.format("%08x.c", relative)),
                    "/* Decompiled evidence; inferred types, not original source. */\n" + result.getDecompiledFunction().getC());
                count++;
            }
            var constants = new StringBuilder("address\tfloat\n");
            for (long relative = 0x953bc; relative <= 0x95408; relative += 4)
                constants.append(String.format("%08x\t%s\n", relative,
                    Float.intBitsToFloat(getInt(toAddr(base + relative)))));
            Files.writeString(output.resolve("border_constants.tsv"), constants);
        } finally { decompiler.dispose(); }
        println("FRONTEND_CALIBRATION functions=" + count);
    }
}

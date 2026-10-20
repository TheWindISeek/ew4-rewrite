// Append only explicitly requested functions to an existing research export.
// @category EW4

import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.DecompInterface;
import ghidra.program.model.listing.Function;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.LinkedHashMap;

public class ExportSelectedEw4 extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length < 2) throw new IllegalArgumentException("Output directory and relative hex addresses required");
        Path output = Path.of(args[0]);
        Files.createDirectories(output);
        Path indexPath = output.resolve("functions.tsv");
        var rows = new LinkedHashMap<String, String>();
        if (Files.exists(indexPath)) {
            for (String line : Files.readAllLines(indexPath, StandardCharsets.UTF_8))
                if (!line.startsWith("address\t") && !line.isBlank()) rows.put(line.split("\t", 2)[0], line);
        }
        var decompiler = new DecompInterface();
        decompiler.openProgram(currentProgram);
        int exported = 0;
        try {
            for (int i = 1; i < args.length; i++) {
                long relative = Long.parseUnsignedLong(args[i].replaceFirst("^0x", ""), 16);
                Function function = getFunctionAt(toAddr(currentProgram.getImageBase().getOffset() + relative));
                if (function == null) function = getFunctionContaining(toAddr(currentProgram.getImageBase().getOffset() + relative));
                if (function == null) throw new IllegalArgumentException("No function at " + args[i]);
                relative = function.getEntryPoint().getOffset() - currentProgram.getImageBase().getOffset();
                var result = decompiler.decompileFunction(function, 45, monitor);
                if (!result.decompileCompleted()) throw new IllegalStateException(result.getErrorMessage());
                String address = String.format("%08x", relative);
                var callees = new ArrayList<String>();
                for (Function called : function.getCalledFunctions(monitor))
                    callees.add(String.format("%08x", called.getEntryPoint().getOffset() - currentProgram.getImageBase().getOffset()));
                Files.writeString(output.resolve(address + ".c"),
                    "/* Decompiled evidence; inferred types, not original source. */\n" + result.getDecompiledFunction().getC(), StandardCharsets.UTF_8);
                rows.put(address, address + "\t" + function.getName() + "\ttrue\tok\t" + String.join(",", callees));
                exported++;
            }
        } finally {
            decompiler.dispose();
        }
        var lines = new ArrayList<String>();
        lines.add("address\tname\troot\tstatus\tcallees");
        lines.addAll(rows.values());
        Files.write(indexPath, lines, StandardCharsets.UTF_8);
        println("EW4_SELECTED_EXPORT added=" + exported + " indexed=" + rows.size());
    }
}

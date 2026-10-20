// Export selected native functions and direct call relationships for review.
// @category EW4

import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.program.model.listing.Function;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.charset.StandardCharsets;
import java.util.LinkedHashSet;
import java.util.Set;
import java.util.ArrayList;

public class ExportEw4 extends GhidraScript {
    @Override
    public void run() throws Exception {
        Path output = Path.of(getScriptArgs()[0]);
        Files.createDirectories(output);
        long[] seeds = {
            0x2a9d8, 0x2ac94, 0x2ad68, 0x2a7f0,
            0x94e30, 0x84a0c, 0x50340, 0x7cb80, 0x6befc, 0xcd2d0,
            0xa7984, 0xa7974, 0xa7a0c, 0xa8960, 0xa9084,
            0x7c27c, 0x9ceec, 0x9d20c, 0x9d49c, 0xaa980,
            0x71038, 0x745b8, 0x84ae4, 0xad5ec,
            0x9db74, 0x9da00, 0x9e530, 0x76e28, 0x8145c,
            0x7e7b8, 0x5b70c, 0x5bc70, 0x4e948, 0x52b48,
            0x5aa98, 0x8dbd4, 0x8dd7c,
            0x5318c, 0x53b94, 0x53bcc, 0x676e8, 0x5a9f4, 0x5ba0c,
            0x50110, 0x502c0, 0x851c4, 0x85824, 0x859e0, 0x869ec,
            0x5ac5c, 0x6aeac, 0x6ae84, 0x9e00c, 0xef754,
            0xc332c, 0xc415c, 0xc2940, 0xc20e8
        };
        Set<Function> functions = new LinkedHashSet<>();
        Set<Function> roots = new LinkedHashSet<>();
        long imageBase = currentProgram.getImageBase().getOffset();
        for (long seed : seeds) {
            Function f = getFunctionContaining(toAddr(imageBase + seed));
            if (f != null) { roots.add(f); functions.add(f); }
        }
        for (long[] range : new long[][] {
                {0x4e000, 0x5e500}, {0x77100, 0x83200}, {0x85000, 0x86b80},
                {0x8d000, 0x8e070}, {0xa7000, 0xa9400}, {0xab000, 0xae100}, {0xc2940, 0xc4560}}) {
            var iterator = currentProgram.getFunctionManager().getFunctions(toAddr(imageBase + range[0]), true);
            while (iterator.hasNext()) {
                Function f = iterator.next();
                if (f.getEntryPoint().getOffset() >= imageBase + range[1]) break;
                roots.add(f);
                functions.add(f);
            }
        }
        for (long target : new long[] {0xad51c, 0xad944}) {
            for (var reference : getReferencesTo(toAddr(imageBase + target))) {
                Function caller = getFunctionContaining(reference.getFromAddress());
                if (caller != null) { roots.add(caller); functions.add(caller); }
            }
        }
        for (Function f : roots) {
            for (Function called : f.getCalledFunctions(monitor)) {
                long address = called.getEntryPoint().getOffset() - imageBase;
                if (address >= 0x2a000 && address < 0xb0000 && !called.isThunk()) {
                    functions.add(called);
                }
            }
        }
        DecompInterface decompiler = new DecompInterface();
        decompiler.openProgram(currentProgram);
        ArrayList<String> index = new ArrayList<>();
        index.add("address\tname\troot\tstatus\tcallees");
        int count = 0;
        try {
            for (Function f : functions) {
                if (monitor.isCancelled()) break;
                String address = String.format("%08x", f.getEntryPoint().getOffset() - imageBase);
                DecompileResults result = decompiler.decompileFunction(f, 45, monitor);
                String status = result.decompileCompleted() ? "ok" : "failed";
                ArrayList<String> callees = new ArrayList<>();
                for (Function called : f.getCalledFunctions(monitor)) {
                    callees.add(String.format("%08x", called.getEntryPoint().getOffset() - imageBase));
                }
                index.add(address + "\t" + f.getName() + "\t" + roots.contains(f)
                        + "\t" + status + "\t" + String.join(",", callees));
                String code = result.decompileCompleted()
                        ? result.getDecompiledFunction().getC() : result.getErrorMessage();
                Files.writeString(output.resolve(address + ".c"),
                        "/* Decompiled evidence; inferred types, not original source. */\n" + code,
                        StandardCharsets.UTF_8);
                count++;
            }
        } finally {
            decompiler.dispose();
        }
        Files.write(output.resolve("functions.tsv"), index, StandardCharsets.UTF_8);
        println("EW4_EXPORT functions=" + count + " roots=" + roots.size());
    }
}

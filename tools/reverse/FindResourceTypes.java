// Locate resource enum strings and their direct/table callers without changing the program.
// @category EW4

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Data;
import ghidra.program.model.listing.Function;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.Set;

public class FindResourceTypes extends GhidraScript {
    private final ArrayList<String> rows = new ArrayList<>();
    private long imageBase;

    private void callers(String name, Address target, int depth, Set<Address> seen) {
        if (depth > 3 || !seen.add(target)) return;
        for (var reference : getReferencesTo(target)) {
            Address from = reference.getFromAddress();
            Function function = getFunctionContaining(from);
            rows.add(name + "\t" + String.format("%08x", target.getOffset() - imageBase) + "\t"
                + String.format("%08x", from.getOffset() - imageBase) + "\t"
                + (function == null ? "" : String.format("%08x", function.getEntryPoint().getOffset() - imageBase)));
            if (function == null) callers(name, from, depth + 1, seen);
        }
    }

    @Override
    public void run() throws Exception {
        imageBase = currentProgram.getImageBase().getOffset();
        var wanted = Set.of("city", "industry", "stable", "port", "farmland", "def_construction.xml", "def_capital.xml");
        var iterator = currentProgram.getListing().getDefinedData(true);
        rows.add("name\ttarget\treference\tfunction");
        while (iterator.hasNext()) {
            Data data = iterator.next();
            Object value = data.getValue();
            if (value instanceof String && wanted.contains((String)value))
                callers((String)value, data.getAddress(), 0, new HashSet<>());
        }
        Files.write(Path.of(getScriptArgs()[0]), rows, StandardCharsets.UTF_8);
        println("EW4_RESOURCE_REFS rows=" + (rows.size() - 1));
    }
}

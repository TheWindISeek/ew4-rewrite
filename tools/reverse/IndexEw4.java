// Index strings and their code references to locate serialization and event logic.
// @category EW4

import ghidra.app.script.GhidraScript;
import ghidra.program.model.data.StringDataInstance;
import ghidra.program.model.listing.Data;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.Reference;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Locale;

public class IndexEw4 extends GhidraScript {
    @Override
    public void run() throws Exception {
        Path output = Path.of(getScriptArgs()[0]);
        Files.createDirectories(output);
        long base = currentProgram.getImageBase().getOffset();
        ArrayList<String> rows = new ArrayList<>();
        rows.add("address\tstring\treference\tfunction");
        var iterator = currentProgram.getListing().getDefinedData(true);
        while (iterator.hasNext() && !monitor.isCancelled()) {
            Data data = iterator.next();
            if (!StringDataInstance.isString(data)) continue;
            String value = StringDataInstance.getStringDataInstance(data).getStringValue();
            if (value == null) continue;
            String lower = value.toLowerCase(Locale.ROOT);
            if (!lower.matches(".*(save|load|victory|defeat|winner|gameover|round|turn|trigger|bile|frame|layer|motion|skill|supply|consumption).*")) continue;
            value = value.replace("\t", " ").replace("\r", " ").replace("\n", " ");
            boolean found = false;
            for (Reference ref : getReferencesTo(data.getAddress())) {
                Function f = getFunctionContaining(ref.getFromAddress());
                if (f == null) continue;
                rows.add(String.format("%08x\t%s\t%08x\t%08x", data.getAddress().getOffset() - base,
                        value, ref.getFromAddress().getOffset() - base, f.getEntryPoint().getOffset() - base));
                found = true;
            }
            if (!found) rows.add(String.format("%08x\t%s\t\t", data.getAddress().getOffset() - base, value));
        }
        Files.write(output.resolve("string_refs.tsv"), rows, StandardCharsets.UTF_8);
        println("EW4_INDEX rows=" + (rows.size() - 1));
    }
}

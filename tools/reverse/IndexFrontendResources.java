// Record resource strings, native references and callers without assuming usage.
// @category EW4
import ghidra.app.script.GhidraScript;
import ghidra.program.model.data.StringDataInstance;
import java.nio.file.*;
import java.util.*;

public class IndexFrontendResources extends GhidraScript {
    public void run() throws Exception {
        var rows = new ArrayList<String>();
        rows.add("address\tresource\treference\tfunction");
        long base = currentProgram.getImageBase().getOffset();
        var data = currentProgram.getListing().getDefinedData(true);
        while (data.hasNext() && !monitor.isCancelled()) {
            var item = data.next();
            if (!StringDataInstance.isString(item)) continue;
            var value = StringDataInstance.getStringDataInstance(item).getStringValue();
            if (value == null || !(value.matches("(?i).+\\.(png|pkm|pvr|webp|xml|fnt|wav|mp3|bin)") || value.startsWith("form_"))) continue;
            value = value.replace('\t', ' ').replace('\r', ' ').replace('\n', ' ');
            boolean found = false;
            for (var ref : getReferencesTo(item.getAddress())) {
                var function = getFunctionContaining(ref.getFromAddress());
                if (function == null) continue;
                rows.add(String.format("%08x\t%s\t%08x\t%08x", item.getAddress().getOffset()-base,
                    value, ref.getFromAddress().getOffset()-base, function.getEntryPoint().getOffset()-base));
                found = true;
            }
            if (!found) rows.add(String.format("%08x\t%s\t\t", item.getAddress().getOffset()-base, value));
        }
        Files.write(Path.of(getScriptArgs()[0]), rows);
        println("FRONTEND_RESOURCE_REFS rows=" + (rows.size()-1));
    }
}

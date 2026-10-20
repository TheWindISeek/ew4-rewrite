// @category EW4
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.DecompInterface;
import java.nio.file.*;
import java.util.*;

public class ExportFormRuntime extends GhidraScript {
    public void run() throws Exception {
        long base=currentProgram.getImageBase().getOffset();
        var functions=new LinkedHashSet<ghidra.program.model.listing.Function>();
        var data=currentProgram.getListing().getDefinedData(true);
        while(data.hasNext()){
            var value=data.next();String text=String.valueOf(value.getValue());
            if(!text.contains("group_gen_")&&!text.equals("form_recruitgeneral"))continue;
            for(var reference:getReferencesTo(value.getAddress())){
                var f=getFunctionContaining(reference.getFromAddress());
                if(f!=null)functions.add(f);
            }
        }
        var decompiler=new DecompInterface();decompiler.openProgram(currentProgram);
        try {
            for(var function:functions){
                var result=decompiler.decompileFunction(function,45,monitor);
                if(!result.decompileCompleted())throw new IllegalStateException(result.getErrorMessage());
                long relative=function.getEntryPoint().getOffset()-base;
                Files.writeString(Path.of("reverse/native/decompiled",String.format("%08x.c",relative)),
                    "/* Decompiled evidence; inferred types, not original source. */\n"+result.getDecompiledFunction().getC());
                println("FORM_RUNTIME "+Long.toHexString(relative));
            }
        } finally {decompiler.dispose();}
    }
}

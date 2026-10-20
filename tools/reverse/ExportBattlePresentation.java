// @category EW4
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.DecompInterface;
import java.nio.file.*;

public class ExportBattlePresentation extends GhidraScript {
    public void run() throws Exception {
        long base=currentProgram.getImageBase().getOffset();
        var output=Path.of("reverse/native/decompiled");
        var decompiler=new DecompInterface();decompiler.openProgram(currentProgram);
        int count=0;
        try {
            var functions=currentProgram.getFunctionManager().getFunctions(toAddr(base+0x51e00),true);
            while(functions.hasNext()){
                var function=functions.next();long relative=function.getEntryPoint().getOffset()-base;
                if(relative>=0x53c00)break;
                var result=decompiler.decompileFunction(function,45,monitor);
                if(!result.decompileCompleted())throw new IllegalStateException(result.getErrorMessage());
                Files.writeString(output.resolve(String.format("%08x.c",relative)),
                    "/* Decompiled evidence; inferred types, not original source. */\n"+result.getDecompiledFunction().getC());count++;
            }
        } finally {decompiler.dispose();}
        println("BATTLE_PRESENTATION functions="+count);
    }
}

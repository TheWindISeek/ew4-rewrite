// @category EW4
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.DecompInterface;
import java.nio.file.*;

public class ExportFrontendFlows extends GhidraScript {
    public void run() throws Exception {
        var output=Path.of(getScriptArgs()[0]);Files.createDirectories(output);
        long base=currentProgram.getImageBase().getOffset();int count=0;
        var decompiler=new DecompInterface();decompiler.openProgram(currentProgram);
        try {
            for(long[] range:new long[][]{{0x403a0,0x40b00}}){
                var functions=currentProgram.getFunctionManager().getFunctions(toAddr(base+range[0]),true);
                while(functions.hasNext()){
                    var function=functions.next();long relative=function.getEntryPoint().getOffset()-base;
                    if(relative>=range[1])break;
                    var result=decompiler.decompileFunction(function,45,monitor);
                    if(!result.decompileCompleted())throw new IllegalStateException(result.getErrorMessage());
                    Files.writeString(output.resolve(String.format("%08x.c",relative)),
                        "/* Decompiled evidence; inferred types, not original source. */\n"+result.getDecompiledFunction().getC());count++;
                }
            }
        } finally {decompiler.dispose();}
        println("FRONTEND_FLOWS functions="+count);
    }
}

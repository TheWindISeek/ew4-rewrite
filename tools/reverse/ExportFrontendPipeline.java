// @category EW4
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.DecompInterface;
import java.nio.file.*;
public class ExportFrontendPipeline extends GhidraScript {
    public void run() throws Exception {
        long base=currentProgram.getImageBase().getOffset();
        var output=Path.of("reverse/native/decompiled");
        var decompiler=new DecompInterface();decompiler.openProgram(currentProgram);
        int count=0;
        try {
            for(long[] range:new long[][]{{0x84e30,0x85190},{0x870cc,0x87600}}){
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
        println("FRONTEND_PIPELINE functions="+count);
    }
}

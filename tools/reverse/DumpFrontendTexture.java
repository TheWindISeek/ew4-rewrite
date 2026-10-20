// @category EW4
import ghidra.app.script.GhidraScript;
import java.nio.file.*;
public class DumpFrontendTexture extends GhidraScript {
    public void run() throws Exception {
        long base=currentProgram.getImageBase().getOffset();
        var output=new StringBuilder();
        for(String argument:getScriptArgs()){
            long offset=Long.parseLong(argument,16);
            var function=getFunctionAt(toAddr(base+offset));
            if(function==null)throw new IllegalStateException("Function missing: "+argument);
            output.append("FUNCTION ").append(argument).append('\n');
            var instructions=currentProgram.getListing().getInstructions(function.getBody(),true);
            while(instructions.hasNext()){
                var instruction=instructions.next();
                output.append(instruction.getAddress()).append(' ').append(instruction).append('\n');
            }
        }
        Files.writeString(Path.of("reverse/native/frontend_texture_disassembly.txt"),output);
    }
}

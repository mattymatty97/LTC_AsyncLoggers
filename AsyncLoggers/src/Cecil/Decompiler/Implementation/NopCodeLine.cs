using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class NopCodeLine : ICodeLine
{
    public bool HasReturn => Continuation?.HasReturn ?? true;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Continuation?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }
    
    public ICodeLine Continuation { get; private set; }
    
    public IEnumerable<ICodeLine> GetArguments()
    {
        return [Continuation];
    }
    
    public bool IsMissingArgument => Continuation?.IsMissingArgument ?? true;
    
    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsMissingArgument)
            return false;

        if (Continuation == null)
        {
            Continuation = codeLine;
        }
        else
        {
            return Continuation.SetMissingArgument(codeLine);
        }
        
        return true;
    }

    public NopCodeLine(MethodDefinition method, Instruction instruction)
    {
        Method = method;
        EndInstruction = instruction;

        //Find the rest of the call
        Continuation = ICodeLine.InternalParseInstruction(method, instruction.Previous);
        
    }

    public override string ToString()
    {
        return ToString(false);
    }
    
    public string ToString(bool isRoot)
    {
        return Continuation?.ToString(isRoot) ?? "|Nop|";
    }
}
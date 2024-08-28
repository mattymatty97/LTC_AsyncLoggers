using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class DupCodeLine : ICodeLine
{
    public bool HasReturn => true;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => EndInstruction;
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

    public DupCodeLine(MethodDefinition method, Instruction instruction)
    {
        Method = method;
        EndInstruction = instruction;
    }

    public override string ToString()
    {
        return ToString(false);
    }
    
    public string ToString(bool isRoot)
    {
        return Continuation?.ToString(isRoot) ?? "|Dup|";
    }
}
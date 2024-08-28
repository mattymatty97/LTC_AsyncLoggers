using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class ExceptionCodeLine : ICodeLine
{
    public bool HasReturn => true;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => EndInstruction;
    public Instruction EndInstruction { get; }
    
    public IEnumerable<ICodeLine> GetArguments()
    {
        return [];
    }

    public bool IsMissingArgument => false;

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        return false;
    }

    public string ToString(bool isRoot)
    {
        return "ex";
    }

    public ExceptionCodeLine(MethodDefinition method, Instruction instruction)
    {
        Method = method;
        EndInstruction = instruction;
    }
}
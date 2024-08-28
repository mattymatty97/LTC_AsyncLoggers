using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class NewArrCodeLine : ICodeLine
{
    public TypeReference Type { get; }

    public bool HasReturn => true;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Count?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }

    public bool IsMissingArgument => Count?.IsMissingArgument ?? true;
    public ICodeLine Count { get; private set; }
    
    public IEnumerable<ICodeLine> GetArguments()
    {
        return [Count];
    }

    public override string ToString()
    {
        return ToString(false);
    }
    
    public string ToString(bool isRoot)
    {
        return $"new {Type.FullName}[{Count?.ToString() ?? ""}]";
    }

    public NewArrCodeLine(MethodDefinition method, Instruction instruction)
    {
        Method = method;
        
        EndInstruction = instruction;
        
        Type = instruction.OpCode.Code switch
        {
            Code.Newarr => (TypeDefinition)instruction.Operand,
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };
        
        Count = ICodeLine.InternalParseInstruction(method, instruction.Previous);
    }

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsMissingArgument)
            return false;

        if (Count == null)
            Count = codeLine;
        else
            return Count.SetMissingArgument(codeLine);

        return IsMissingArgument;

    }
}
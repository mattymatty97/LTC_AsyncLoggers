using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class CastCodeLine : ICodeLine
{
    public bool HasReturn => true;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => EndInstruction;
    public Instruction EndInstruction { get; }

    public TypeReference Type { get; }
    public ICodeLine Value { get; private set; }
    
    public IEnumerable<ICodeLine> GetArguments()
    {
        return [Value];
    }

    public bool IsMissingArgument => Value?.IsMissingArgument ?? true;
    
    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsMissingArgument)
            return false;

        if (Value == null)
        {
            Value = codeLine;
        }
        else
        {
            return Value.SetMissingArgument(codeLine);
        }
        
        return true;
    }

    public CastCodeLine(MethodDefinition method, Instruction instruction)
    {
        Method = method;
        EndInstruction = instruction;

        Type = (TypeReference) instruction.Operand;
        
        Value = ICodeLine.InternalParseInstruction(method, instruction.Previous);
    }

    public override string ToString()
    {
        return ToString(false);
    }
    
    public string ToString(bool isRoot)
    {
        return $"(({Type.FullName}){Value?.ToString() ?? "|Value|"})";
    }
}
using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class TypeOfCodeLine : ICodeLine
{
    public bool HasReturn => Value?.HasReturn ?? true;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Value?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }
    
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

    public TypeOfCodeLine(MethodDefinition method, Instruction instruction)
    {
        Method = method;
        EndInstruction = instruction;

        //Find the rest of the call
        Value = ICodeLine.InternalParseInstruction(method, instruction.Previous);
        
    }

    public override string ToString()
    {
        return ToString(false);
    }
    
    public string ToString(bool isRoot)
    {
        return $"typeof({Value?.ToString() ?? "|Value|"})";
    }
}
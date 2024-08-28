using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class LocalCodeLine : ICodeLine
{
    public bool IsStoring { get; }
    
    public int Index { get; }

    public ICodeLine Value { get; private set; }

    public bool HasReturn => !IsStoring;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Value?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }
    
    public IEnumerable<ICodeLine> GetArguments()
    {
        return IsStoring ? [Value] : [];
    }

    public LocalCodeLine(MethodDefinition method, Instruction instruction)
    {
        Method = method;
        
        EndInstruction = instruction;

        IsStoring = instruction.OpCode.Code switch
        {
            Code.Ldloc or Code.Ldloca or Code.Ldloc_0 or Code.Ldloc_1  or Code.Ldloc_2  or Code.Ldloc_3  or Code.Ldloc_S or Code.Ldloca_S => false,
            Code.Stloc or Code.Stloc_0 or Code.Stloc_1 or Code.Stloc_2 or Code.Stloc_3 or Code.Stloc_S=> true,
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };
        
        Index = instruction.OpCode.Code switch
        {
            Code.Ldloc_0 or Code.Stloc_0 => 0,
            Code.Ldloc_1 or Code.Stloc_1 => 1,
            Code.Ldloc_2 or Code.Stloc_2 => 2,
            Code.Ldloc_3 or Code.Stloc_3 => 3,
            Code.Ldloc or Code.Ldloca or Code.Ldloc_S or Code.Ldloca_S or Code.Stloc or Code.Stloc_S=> ((VariableDefinition)instruction.Operand).Index,
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };

        if (!IsStoring) 
            return;
        
        Value = ICodeLine.InternalParseInstruction(method, instruction.Previous);
    }
    
    public bool IsMissingArgument => IsStoring && (Value?.IsMissingArgument ?? true);
    
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

        return IsMissingArgument;
    }

    public override string ToString()
    {
        return ToString(false);
    }
    
    public string ToString(bool isRoot)
    {
        var text = $"var_{Index}";
        return IsStoring ? $"{text} = {Value}" : text;
    }
}
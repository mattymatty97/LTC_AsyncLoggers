using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class ArrCodeLine : ICodeLine
{
    public ArrCodeLine(MethodDefinition method, Instruction instruction)
    {
        Method = method;

        EndInstruction = instruction;

        IsStoring = instruction.OpCode.Code switch
        {
            Code.Ldelema
                or Code.Ldelem_I1
                or Code.Ldelem_U1
                or Code.Ldelem_I2
                or Code.Ldelem_U2
                or Code.Ldelem_I4
                or Code.Ldelem_U4
                or Code.Ldelem_I8
                or Code.Ldelem_I
                or Code.Ldelem_R4
                or Code.Ldelem_R8
                or Code.Ldelem_Ref
                or Code.Ldelem_Any => false,
            Code.Stelem_I or
                Code.Stelem_I1 or
                Code.Stelem_I2 or
                Code.Stelem_I4 or
                Code.Stelem_I8 or
                Code.Stelem_R4 or
                Code.Stelem_R8 or
                Code.Stelem_Ref or
                Code.Stelem_Any => true,
            _ => throw new InvalidOperationException()
        };

        if (IsStoring)
        {
            
        }
        //TODO read value

        



    }

    public bool IsStoring { get; }

    private LinkedList<ICodeLine> Arguments { get; } = [];
    public bool HasReturn => true;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Arguments.First?.Value?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }

    public bool IsMissingArgument => (IsStoring ? Arguments.Count < 3 : Arguments.Count < 2) ||
                                     Arguments.Any(a => a.IsMissingArgument);

    public IEnumerable<ICodeLine> GetArguments()
    {
        return Arguments.ToArray();
    }

    public string ToString(bool isRoot)
    {
        var list = Arguments.ToList();
        
        if (IsStoring)
            return (isRoot ? $"{list[0]}[{list[1]}] = " : "") + list[2];

        return $"{list[0]}[{list[1]}]";
    }

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsMissingArgument)
            return false;

        if (IsStoring ? Arguments.Count < 3 : Arguments.Count < 2)
        {
            Arguments.AddFirst(codeLine);
            return IsMissingArgument;
        }
        
        var @fixed = true;
        
        foreach (var argument in Arguments)
        {
            @fixed &= argument?.SetMissingArgument(codeLine) ?? false;
        }

        return @fixed;
    }

    public override string ToString()
    {
        return ToString(false);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class ArrLenCodeLine : ICodeLine
{
    public ArrLenCodeLine(MethodDefinition method, Instruction instruction)
    {
        Method = method;

        EndInstruction = instruction;

        Array = ICodeLine.InternalParseInstruction(method, instruction.Previous);
    }

    public ICodeLine Array { get; private set; }
    public bool HasReturn => true;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Array?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }

    public bool IsMissingArgument => Array?.IsMissingArgument ?? true;

    public IEnumerable<ICodeLine> GetArguments()
    {
        return [Array];
    }

    public string ToString(bool isRoot)
    {
        return $"{Array?.ToString() ?? "|Array|"}.Count()";
    }

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsMissingArgument)
            return false;

        if (Array == null)
        {
            Array = codeLine;
        }
        else
        {
            Array.SetMissingArgument(codeLine);
        }

        return IsMissingArgument;
    }

    public override string ToString()
    {
        return ToString(false);
    }
}
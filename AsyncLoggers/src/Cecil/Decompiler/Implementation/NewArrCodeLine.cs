using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class NewArrCodeLine : ICodeLine
{
    public NewArrCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);
        Method = method;

        EndInstruction = instruction;


        Type = instruction.OpCode.Code switch
        {
            Code.Newarr => (TypeReference)instruction.Operand,
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };


        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - {Type.FullName}");

        Count = ICodeLine.InternalParseInstruction(method, instruction.Previous);

        if (Count != null)
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                () => $"{method.FullName}:{ICodeLine.PrintStack()} - Count found");

        ICodeLine.CurrentStack.Value.Pop();
    }

    public TypeReference Type { get; }
    public ICodeLine Count { get; private set; }

    public bool HasReturn => true;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Count?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }

    public bool IsIncomplete => Count?.IsIncomplete ?? true;

    public IEnumerable<ICodeLine> GetArguments()
    {
        return [Count];
    }

    public string ToString(bool isRoot)
    {
        return $"new {Type.FullName}[{Count?.ToString() ?? ""}]";
    }

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsIncomplete)
            return false;

        if (Count == null)
            Count = codeLine;
        else
            return Count.SetMissingArgument(codeLine);

        return IsIncomplete;
    }

    public override string ToString()
    {
        return ToString(false);
    }
}
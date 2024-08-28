using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class NegationCodeLine : ICodeLine
{
    public NegationCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);
        Method = method;
        EndInstruction = instruction;

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () => $"{method.FullName}:{ICodeLine.PrintStack()}");

        Value = ICodeLine.InternalParseInstruction(method, StartInstruction.Previous);
        if (Value != null)
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                () => $"{method.FullName}:{ICodeLine.PrintStack()} - Value found!");
        ICodeLine.CurrentStack.Value.Pop();
    }

    public ICodeLine Value { get; private set; }
    public bool HasReturn => true;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Value?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }

    public IEnumerable<ICodeLine> GetArguments()
    {
        return [Value];
    }

    public bool IsIncomplete => Value?.IsIncomplete ?? true;

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsIncomplete)
            return false;

        if (Value == null)
            Value = codeLine;
        else
            Value.SetMissingArgument(codeLine);

        return IsIncomplete;
    }

    public string ToString(bool isRoot)
    {
        return EndInstruction.OpCode.Code switch
        {
            Code.Neg => $"-({Value})",
            Code.Not => $"!({Value})",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public override string ToString()
    {
        return ToString(false);
    }
}
using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class TypeOfCodeLine : ICodeLine
{
    public TypeOfCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);

        Method = method;
        EndInstruction = instruction;

        IsDeReference = instruction.OpCode.Code switch
        {
            Code.Refanytype => false,
            Code.Mkrefany => true,
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };


        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()}{(IsDeReference ? " - Dereference" : "")}");

        //Find the rest of the call
        Value = ICodeLine.InternalParseInstruction(method, instruction.Previous);

        if (Value != null)
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                () => $"{method.FullName}:{ICodeLine.PrintStack()} - Value found");

        ICodeLine.CurrentStack.Value.Pop();
    }

    public bool IsDeReference { get; }

    public ICodeLine Value { get; private set; }
    public bool HasReturn => Value?.HasReturn ?? true;
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
            return Value.SetMissingArgument(codeLine);

        return true;
    }

    public string ToString(bool isRoot)
    {
        return $"typeof({(IsDeReference ? "* " : "")}{Value?.ToString() ?? "|Value|"})";
    }

    public override string ToString()
    {
        return ToString(false);
    }
}
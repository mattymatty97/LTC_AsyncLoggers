using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class DeReferenceCodeLine : ICodeLine
{
    protected internal DeReferenceCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);
        Method = method;
        EndInstruction = instruction;

        IsStoring = instruction.OpCode.Code switch
        {
            Code.Ldobj or
                Code.Ldind_I1 or
                Code.Ldind_U1 or
                Code.Ldind_I2 or
                Code.Ldind_U2 or
                Code.Ldind_I4 or
                Code.Ldind_U4 or
                Code.Ldind_I8 or
                Code.Ldind_I or
                Code.Ldind_R4 or
                Code.Ldind_R8 or
                Code.Ldind_Ref => false,
            Code.Stind_Ref or
                Code.Stind_I or
                Code.Stind_I1 or
                Code.Stind_I2 or
                Code.Stind_I4 or
                Code.Stind_I8 or
                Code.Stind_R4 or
                Code.Stind_R8 => true,
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };


        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - {(IsStoring ? "Storing" : "Loading")}");

        if (IsStoring)
        {
            Value = ICodeLine.InternalParseInstruction(method, StartInstruction.Previous);

            if (Value == null)
                throw new InvalidOperationException(
                    $"{Method.FullName}:IL_{EndInstruction.Offset:x4} Cannot find Value for {instruction.OpCode}");


            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                () => $"{method.FullName}:{ICodeLine.PrintStack()} - Value found");
        }

        Address = ICodeLine.InternalParseInstruction(method, StartInstruction.Previous);
        if (Address != null)
        {
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                () => $"{method.FullName}:{ICodeLine.PrintStack()} - Address found");
            Value?.SetMissingArgument(Address);
        }

        ICodeLine.CurrentStack.Value.Pop();
    }

    public bool IsStoring { get; }
    public ICodeLine Address { get; private set; }
    public ICodeLine Value { get; }
    public bool HasReturn => !IsStoring;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Address?.StartInstruction ?? Value?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }

    public IEnumerable<ICodeLine> GetArguments()
    {
        return IsStoring ? [Address, Value] : [Address];
    }

    public bool IsIncomplete => IsStoring
        ? (Address?.IsIncomplete ?? true) || (Value?.IsIncomplete ?? true)
        : Address?.IsIncomplete ?? true;

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsIncomplete)
            return false;

        if (Address == null)
            Address = codeLine;
        else
            return Address.SetMissingArgument(codeLine);

        return true;
    }

    public string ToString(bool isRoot)
    {
        var text = $"*({Address?.ToString() ?? "|Address|"})";
        if (IsStoring)
            return (isRoot ? $"{text} = " : "") + Value;
        return text;
    }

    public override string ToString()
    {
        return ToString(false);
    }
}
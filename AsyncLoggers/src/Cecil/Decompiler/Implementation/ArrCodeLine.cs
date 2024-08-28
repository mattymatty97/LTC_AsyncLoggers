using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class ArrCodeLine : ICodeLine
{
    public ArrCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);
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


        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - {(IsStoring ? "Storing" : "Loading")}");

        if (IsStoring)
        {
            Value = ICodeLine.InternalParseInstruction(method, StartInstruction.Previous);

            if (Value == null)
                throw new InvalidOperationException(
                    $"{Method.FullName}:IL_{EndInstruction.Offset:x4} Cannot find Value for Array Set");

            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                () => $"{method.FullName}:{ICodeLine.PrintStack()} - Value found");
        }

        Index = ICodeLine.InternalParseInstruction(method, StartInstruction.Previous);

        if (Index == null)
            throw new InvalidOperationException(
                $"{Method.FullName}:IL_{EndInstruction.Offset:x4} Cannot find Index for Array Operation");

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - Index found");

        Array = ICodeLine.InternalParseInstruction(method, StartInstruction.Previous);

        if (Array != null)
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                () => $"{method.FullName}:{ICodeLine.PrintStack()} - Array found");

        ICodeLine.CurrentStack.Value.Pop();
    }

    public bool IsStoring { get; }

    public ICodeLine Array { get; set; }
    public ICodeLine Index { get; set; }
    public ICodeLine Value { get; set; }

    public bool HasReturn => !IsStoring;
    public MethodDefinition Method { get; }

    public Instruction StartInstruction => Array?.StartInstruction ?? Index?.StartInstruction ??
        (IsStoring ? Value?.StartInstruction ?? EndInstruction : EndInstruction);

    public Instruction EndInstruction { get; }

    public bool IsIncomplete => (Array?.IsIncomplete ?? true) || (Index?.IsIncomplete ?? true) ||
                                (Value?.IsIncomplete ?? IsStoring);

    public IEnumerable<ICodeLine> GetArguments()
    {
        return IsStoring ? [Array, Index, Value] : [Array, Index];
    }

    public string ToString(bool isRoot)
    {
        if (IsStoring)
            return $"{Array}[{Index}] = {Value}";

        return $"{Array}[{Index}]";
    }

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsIncomplete)
            return false;

        if (Array == null)
            Array = codeLine;
        else
            Array.SetMissingArgument(codeLine);

        Index.SetMissingArgument(codeLine);

        Value?.SetMissingArgument(codeLine);

        return IsIncomplete;
    }

    public override string ToString()
    {
        return ToString(false);
    }
}
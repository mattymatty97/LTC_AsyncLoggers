using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation.Composite;

public class InlineArrayCodeLine : ICompoundCodeLine
{
    protected internal InlineArrayCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);

        Method = method;
        EndInstruction = instruction;

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - starting!");

        ICodeLine.CurrentVisitedInstructions.Value.Add(StartInstruction);
        var codeline = ICodeLine.InternalParseInstruction(method, StartInstruction, false);
        if (codeline is not ArrCodeLine { IsStoring: true } arrCodeLine)
        {
            Error = () => $"{method.FullName}:{ICodeLine.PrintStack()} Cannot previous store!";
            ICodeLine.CurrentStack.Value.Pop();
            return;
        }

        Arguments.AddFirst(codeline);
        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - {arrCodeLine.Index} = {arrCodeLine.Value}!");
        while (codeline is ArrCodeLine { IsStoring: true })
        {
            var prec = StartInstruction.Previous;
            ICodeLine.CurrentVisitedInstructions.Value.Add(prec);
            codeline = ICodeLine.InternalParseInstruction(method, prec, false);

            if (codeline is not ArrCodeLine { IsStoring: true } arrCodeLine2)
                continue;

            Arguments.AddFirst(codeline);
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                () => $"{method.FullName}:{ICodeLine.PrintStack()} - {arrCodeLine2.Index} = {arrCodeLine2.Value}!");
        }

        if (codeline is not NewArrCodeLine)
        {
            Error = () => $"{method.FullName}:{ICodeLine.PrintStack()} Cannot find array creation!";
            ICodeLine.CurrentStack.Value.Pop();
            return;
        }

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - creation!");
        Arguments.AddFirst(codeline);

        ICodeLine.CurrentStack.Value.Pop();
    }

    public Func<string> Error { get; }

    private LinkedList<ICodeLine> Arguments { get; } = [];
    public bool HasReturn => true;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Arguments.First?.Value?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }

    public IEnumerable<ICodeLine> GetArguments()
    {
        return Arguments.ToArray();
    }

    public bool IsIncomplete => Arguments.First()?.IsIncomplete ?? true;

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsIncomplete)
            return false;

        Arguments.First()?.SetMissingArgument(codeLine);
        return IsIncomplete;
    }

    public string ToString(bool isRoot)
    {
        if (Error != null)
            throw new ArrayException(Error());

        return $"[{string.Join(", ", Arguments.Skip(1).Select(cl => ((ArrCodeLine)cl).Value))}]";
    }

    public override string ToString()
    {
        return ToString(false);
    }

    internal class ArrayException(string text) : Exception(text)
    {
    }
}
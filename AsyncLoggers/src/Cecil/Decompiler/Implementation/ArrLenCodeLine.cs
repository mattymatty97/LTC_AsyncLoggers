using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class ArrLenCodeLine : ICodeLine
{
    protected internal ArrLenCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);
        Method = method;

        EndInstruction = instruction;


        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () => $"{method.FullName}:{ICodeLine.PrintStack()}");

        Array = ICodeLine.InternalParseInstruction(method, instruction.Previous);

        if (Array != null)
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                () => $"{method.FullName}:{ICodeLine.PrintStack()} - Array found");

        ICodeLine.CurrentStack.Value.Pop();
    }

    public ICodeLine Array { get; private set; }
    public bool HasReturn => true;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Array?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }

    public bool IsIncomplete => Array?.IsIncomplete ?? true;

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
        if (!IsIncomplete)
            return false;

        if (Array == null)
            Array = codeLine;
        else
            Array.SetMissingArgument(codeLine);

        return IsIncomplete;
    }

    public override string ToString()
    {
        return ToString(false);
    }
}
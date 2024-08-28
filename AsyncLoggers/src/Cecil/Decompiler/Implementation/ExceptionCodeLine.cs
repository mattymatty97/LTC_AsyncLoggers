using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class ExceptionCodeLine : ICodeLine
{
    public ExceptionCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);

        Method = method;
        EndInstruction = instruction;

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - is start of Catch Block!");

        ICodeLine.CurrentStack.Value.Pop();
    }

    public bool HasReturn => true;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => EndInstruction;
    public Instruction EndInstruction { get; }

    public IEnumerable<ICodeLine> GetArguments()
    {
        return [];
    }

    public bool IsIncomplete => false;

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        return false;
    }

    public string ToString(bool isRoot)
    {
        return "ex";
    }

    public override string ToString()
    {
        return ToString(false);
    }
}
using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class NopCodeLine : ICodeLine
{
    public NopCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);

        Method = method;
        EndInstruction = instruction;

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () => $"{method.FullName}:{ICodeLine.PrintStack()} - Nop!");

        //Find the rest of the call
        Continuation = ICodeLine.InternalParseInstruction(method, instruction.Previous);

        ICodeLine.CurrentStack.Value.Pop();
    }

    public ICodeLine Continuation { get; private set; }
    public bool HasReturn => Continuation?.HasReturn ?? true;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Continuation?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }

    public IEnumerable<ICodeLine> GetArguments()
    {
        return [Continuation];
    }

    public bool IsIncomplete => Continuation?.IsIncomplete ?? true;

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsIncomplete)
            return false;

        if (Continuation == null)
            Continuation = codeLine;
        else
            return Continuation.SetMissingArgument(codeLine);

        return true;
    }

    public string ToString(bool isRoot)
    {
        return Continuation?.ToString(isRoot) ?? "|Nop|";
    }

    public override string ToString()
    {
        return ToString(false);
    }
}
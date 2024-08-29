using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class ReferenceCodeLine : ICodeLine
{
    protected internal ReferenceCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);
        Method = method;
        EndInstruction = instruction;

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () => $"{method.FullName}:{ICodeLine.PrintStack()}");

        //Find the rest of the call
        Value = ICodeLine.InternalParseInstruction(method, instruction.Previous);
        if (Value != null)
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                () => $"{method.FullName}:{ICodeLine.PrintStack()} - Value found");

        ICodeLine.CurrentStack.Value.Pop();
    }

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
        return $"&({Value?.ToString() ?? "|Value|"})";
    }

    public override string ToString()
    {
        return ToString(false);
    }
}
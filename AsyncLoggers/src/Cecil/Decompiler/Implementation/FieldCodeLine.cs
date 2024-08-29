using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class FieldCodeLine : ICodeLine
{
    protected internal FieldCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);
        Method = method;

        EndInstruction = instruction;

        IsStoring = instruction.OpCode.Code switch
        {
            Code.Ldfld or Code.Ldflda or Code.Ldsfld or Code.Ldsflda => false,
            Code.Stfld or Code.Stsfld => true,
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };

        IsStatic = instruction.OpCode.Code switch
        {
            Code.Ldfld or Code.Ldflda or Code.Stfld => false,
            Code.Stsfld or Code.Ldsfld or Code.Ldsflda => true,
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };

        Field = (FieldReference)instruction.Operand;

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - {(IsStoring ? "Storing" : "Loading")} {Field.Name}");

        if (IsStoring)
        {
            Value = ICodeLine.InternalParseInstruction(method, instruction.Previous);
            if (Value == null)
                throw new InvalidOperationException(
                    $"{Method.FullName}:IL_{EndInstruction.Offset:x4} Cannot find Value for field store");

            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                () => $"{method.FullName}:{ICodeLine.PrintStack()} - Value found");
        }

        if (!IsStatic)
        {
            Instance = ICodeLine.InternalParseInstruction(method, instruction.Previous);
            if (Instance != null)
            {
                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                    () => $"{method.FullName}:{ICodeLine.PrintStack()} - Instance found");
                Value?.SetMissingArgument(Instance);
            }
        }

        ICodeLine.CurrentStack.Value.Pop();
    }

    public bool IsStoring { get; }

    public bool IsStatic { get; }
    public FieldReference Field { get; }

    public ICodeLine Value { get; }

    public ICodeLine Instance { get; private set; }
    public bool HasReturn => !IsStoring;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Instance?.StartInstruction ?? Value?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }

    public IEnumerable<ICodeLine> GetArguments()
    {
        var arguments = new LinkedList<ICodeLine>();

        if (IsStoring)
            arguments.AddFirst(Value);

        if (!IsStatic)
            arguments.AddFirst(Instance);

        return arguments.ToArray();
    }

    public bool IsIncomplete => (Instance?.IsIncomplete ?? true) || (Value?.IsIncomplete ?? true);

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsIncomplete)
            return false;

        if (Instance == null)
            Instance = codeLine;
        else
            Instance.SetMissingArgument(codeLine);

        Value?.SetMissingArgument(codeLine);

        return IsIncomplete;
    }

    public string ToString(bool isRoot)
    {
        var text = (IsStatic ? $"{Field.DeclaringType.FullName}" : Instance) + "." + Field.Name;
        return IsStoring ? $"{text} = {Value}" : text;
    }

    public override string ToString()
    {
        return ToString(false);
    }
}
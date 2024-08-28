using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class ParameterCodeLine : ICodeLine
{
    public ParameterCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);
        Method = method;

        EndInstruction = instruction;


        IsStoring = instruction.OpCode.Code switch
        {
            Code.Ldarg or Code.Ldarga or Code.Ldarg_0 or Code.Ldarg_1 or Code.Ldarg_2 or Code.Ldarg_3 or Code.Ldarg_S
                or Code.Ldarga_S => false,
            Code.Starg or Code.Starg_S => true,
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };

        IsThis = instruction.OpCode.Code switch
        {
            Code.Ldarg_0 => Method.HasThis,
            _ => false
        };

        Parameter = instruction.OpCode.Code switch
        {
            Code.Ldarg_0 => Method.HasThis ? null : Method.Parameters[0],
            Code.Ldarg_1 => Method.HasThis ? Method.Parameters[0] : Method.Parameters[1],
            Code.Ldarg_2 => Method.HasThis ? Method.Parameters[1] : Method.Parameters[2],
            Code.Ldarg_3 => Method.HasThis ? Method.Parameters[2] : Method.Parameters[3],
            Code.Ldarg or Code.Ldarga or Code.Ldarg_S or Code.Ldarga_S or Code.Starg or Code.Starg_S =>
                (ParameterDefinition)instruction.Operand,
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () =>
                $"{method.FullName}:{ICodeLine.PrintStack()} - {(IsStoring ? "Store" : "Load")} {(IsThis ? "this" : Parameter.Name)}");

        if (IsStoring)
        {
            Value = ICodeLine.InternalParseInstruction(method, instruction.Previous);
            if (Value != null)
                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                    () => $"{method.FullName}:{ICodeLine.PrintStack()} - Value found");
        }

        ICodeLine.CurrentStack.Value.Pop();
    }

    public bool IsStoring { get; }

    public bool IsThis { get; }

    public ParameterDefinition Parameter { get; }

    private ICodeLine Value { get; set; }
    public bool HasReturn => !IsStoring;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Value?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }

    public IEnumerable<ICodeLine> GetArguments()
    {
        return IsStoring ? [Value] : [];
    }

    public string ToString(bool isRoot)
    {
        var name = Parameter?.Name;
        if (IsStoring)
            return $"{name} = {Value}";
        if (IsThis)
            return "this";
        return name;
    }

    public bool IsIncomplete => IsStoring && (Value?.IsIncomplete ?? true);

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsIncomplete)
            return false;

        if (Value == null)
            Value = codeLine;
        else
            return Value.SetMissingArgument(codeLine);

        return IsIncomplete;
    }

    public override string ToString()
    {
        return ToString(false);
    }
}
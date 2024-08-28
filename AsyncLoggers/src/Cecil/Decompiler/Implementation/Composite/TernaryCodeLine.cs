using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation.Composite;

public class TernaryCodeLine : ICompoundCodeLine
{
    public TernaryCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);

        Method = method;
        EndInstruction = instruction;

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - starting!");

        ICodeLine.CurrentVisitedInstructions.Value.Add(instruction);
        True = ICodeLine.InternalParseInstruction(method, instruction);
        ICodeLine.CurrentVisitedInstructions.Value.Remove(instruction);

        if (True == null)
        {
            Error = () => $"{method.FullName}:{ICodeLine.PrintStack()} Cannot find Operand2 for Ternary";
            ICodeLine.CurrentStack.Value.Pop();
            return;
        }

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - operand2 found");

        var branchTarget = ICodeLine.GetBranchInstructionForTarget(instruction.Next).First();

        False = ICodeLine.InternalParseInstruction(method, branchTarget.Previous);

        if (False == null)
        {
            Error = () => $"{method.FullName}:{ICodeLine.PrintStack()} Cannot find Operand1 for Ternary";
            ICodeLine.CurrentStack.Value.Pop();
            return;
        }

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - operand1 found");

        var branchTarget2 = ICodeLine.GetBranchInstructionForTarget(branchTarget.Next).First();

        if (branchTarget2.OpCode.Code is Code.Brfalse or Code.Brfalse_S) (True, False) = (False, True);

        switch (branchTarget2.OpCode.Code)
        {
            case Code.Brtrue_S:
            case Code.Brtrue:
            {
                Operand = "{0}";
                Condition1 = ICodeLine.InternalParseInstruction(method, branchTarget2.Previous);
                if (Condition1 == null)
                {
                    Error = () => $"{method.FullName}:{ICodeLine.PrintStack()} Cannot find Condition for Ternary";
                    ICodeLine.CurrentStack.Value.Pop();
                    return;
                }

                True.SetMissingArgument(Condition1);
                False.SetMissingArgument(Condition1);

                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                    () => $"{method.FullName}:{ICodeLine.PrintStack()} - condition found");

                ICodeLine.CurrentStack.Value.Pop();
                return;
            }
            case Code.Brfalse_S:
            case Code.Brfalse:
            {
                if (branchTarget2.OpCode.Code is Code.Brfalse or Code.Brfalse_S) (True, False) = (False, True);

                Operand = "{0}";
                Condition1 = ICodeLine.InternalParseInstruction(method, branchTarget2.Previous);
                if (Condition1 == null)
                {
                    Error = () => $"{method.FullName}:{ICodeLine.PrintStack()} Cannot find Condition for Ternary";
                    ICodeLine.CurrentStack.Value.Pop();
                    return;
                }

                True.SetMissingArgument(Condition1);
                False.SetMissingArgument(Condition1);

                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                    () => $"{method.FullName}:{ICodeLine.PrintStack()} - condition found");

                ICodeLine.CurrentStack.Value.Pop();
                return;
            }

            case Code.Beq:
            case Code.Beq_S:
            {
                Operand = "({0} == {1})";
                break;
            }

            case Code.Bne_Un_S:
            case Code.Bne_Un:
            {
                Operand = "({0} != {1})";
                break;
            }

            case Code.Bge:
            case Code.Bge_S:
            case Code.Bge_Un:
            case Code.Bge_Un_S:
            {
                Operand = "({0} >= {1})";
                break;
            }

            case Code.Bgt:
            case Code.Bgt_S:
            case Code.Bgt_Un:
            case Code.Bgt_Un_S:
            {
                Operand = "({0} > {1})";
                break;
            }
            case Code.Ble:
            case Code.Ble_S:
            case Code.Ble_Un:
            case Code.Ble_Un_S:
            {
                Operand = "({0} <= {1})";
                break;
            }

            case Code.Blt:
            case Code.Blt_S:
            case Code.Blt_Un:
            case Code.Blt_Un_S:
            {
                Operand = "({0} < {1})";
                break;
            }

            default:
                Error = () => $"{method.FullName}:{ICodeLine.PrintStack()} Cannot find Condition for Ternary";
                ICodeLine.CurrentStack.Value.Pop();
                return;
        }

        Condition2 = ICodeLine.InternalParseInstruction(method, branchTarget2.Previous);
        if (Condition2 == null)
        {
            Error = () => $"{method.FullName}:{ICodeLine.PrintStack()} Cannot find Condition Operand2 for Ternary";
            ICodeLine.CurrentStack.Value.Pop();
            return;
        }

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - condition2 found");

        Condition1 = ICodeLine.InternalParseInstruction(method, Condition2.StartInstruction.Previous);
        if (Condition1 == null)
        {
            Error = () => $"{method.FullName}:{ICodeLine.PrintStack()} Cannot find Condition Operand1 for Ternary";
            ICodeLine.CurrentStack.Value.Pop();
            return;
        }

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - condition1 found");

        ICodeLine.CurrentStack.Value.Pop();
    }

    public Func<string> Error { get; }

    public ICodeLine True { get; }
    public ICodeLine False { get; }

    public ICodeLine Condition1 { get; }

    public string Operand { get; }
    public ICodeLine Condition2 { get; }
    public bool HasReturn => (False?.HasReturn ?? true) && (True?.HasReturn ?? true);
    public MethodDefinition Method { get; }

    public Instruction StartInstruction =>
        Condition1?.StartInstruction ?? Condition2.StartInstruction ?? EndInstruction;

    public Instruction EndInstruction { get; }

    public IEnumerable<ICodeLine> GetArguments()
    {
        List<ICodeLine> arguments = [Condition1];
        if (Condition2 != null)
            arguments.Add(Condition2);
        arguments.Add(True);
        arguments.Add(False);

        return arguments.ToArray();
    }

    public bool IsIncomplete => GetArguments().Any(a => a.IsIncomplete);

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsIncomplete)
            return false;

        foreach (var argument in GetArguments()) argument?.SetMissingArgument(codeLine);

        return IsIncomplete;
    }

    public string ToString(bool isRoot)
    {
        if (Error != null)
            throw new TernaryException(Error());

        return $"({string.Format(Operand, [Condition1, Condition2])} ? {True} : {False})";
    }

    public override string ToString()
    {
        return ToString(false);
    }

    internal class TernaryException(string text) : Exception(text)
    {
    }
}
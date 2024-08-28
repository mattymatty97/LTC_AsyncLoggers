using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class ArithmeticCodeLine : ICodeLine
{
    public enum ArithmeticOperation
    {
        Add,
        Sub,
        Mul,
        Div,
        Rem,
        And,
        Or,
        Xor,
        Shr,
        Shl,
        Eq,
        Gt,
        Lt
    }

    public ArithmeticCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);
        Method = method;
        EndInstruction = instruction;

        Operation = instruction.OpCode.Code switch
        {
            Code.Add_Ovf or
                Code.Add_Ovf_Un or
                Code.Add
                => ArithmeticOperation.Add,
            Code.Sub_Ovf or
                Code.Sub_Ovf_Un or
                Code.Sub
                => ArithmeticOperation.Sub,
            Code.Mul_Ovf or
                Code.Mul_Ovf_Un or
                Code.Mul
                => ArithmeticOperation.Mul,
            Code.Div or
                Code.Div_Un
                => ArithmeticOperation.Div,
            Code.Rem or
                Code.Rem_Un
                => ArithmeticOperation.Rem,
            Code.And
                => ArithmeticOperation.And,
            Code.Or
                => ArithmeticOperation.Or,
            Code.Xor
                => ArithmeticOperation.Xor,
            Code.Shl
                => ArithmeticOperation.Shl,
            Code.Shr or
                Code.Shr_Un
                => ArithmeticOperation.Shr,
            Code.Ceq
                => ArithmeticOperation.Eq,
            Code.Cgt or
                Code.Cgt_Un
                => ArithmeticOperation.Gt,
            Code.Clt or
                Code.Clt_Un
                => ArithmeticOperation.Lt,
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - {Operation}");

        Operand2 = ICodeLine.InternalParseInstruction(method, StartInstruction.Previous);
        if (Operand2 == null)
            throw new InvalidOperationException(
                $"{Method.FullName}:IL_{EndInstruction.Offset:x4} Cannot find Operand2 for {instruction.OpCode}");

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - Operand2 found");

        Operand1 = ICodeLine.InternalParseInstruction(method, StartInstruction.Previous);
        if (Operand1 != null)
        {
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                () => $"{method.FullName}:{ICodeLine.PrintStack()} - Operand1 found");
            Operand2.SetMissingArgument(Operand1);
        }

        ICodeLine.CurrentStack.Value.Pop();
    }

    public ArithmeticOperation Operation { get; }
    public ICodeLine Operand1 { get; private set; }
    public ICodeLine Operand2 { get; }
    public bool HasReturn => true;

    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Operand1?.StartInstruction ?? Operand2?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }

    public IEnumerable<ICodeLine> GetArguments()
    {
        return [Operand1, Operand2];
    }

    public bool IsIncomplete => (Operand1?.IsIncomplete ?? true) || (Operand2?.IsIncomplete ?? true);

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsIncomplete)
            return false;

        Operand2.SetMissingArgument(codeLine);
        if (Operand1 == null)
            Operand1 = codeLine;
        else
            Operand1.SetMissingArgument(codeLine);

        return IsIncomplete;
    }

    public string ToString(bool isRoot)
    {
        return Operation switch
        {
            ArithmeticOperation.Add => $"{Operand1} + {Operand2}",
            ArithmeticOperation.Sub => $"{Operand1} - {Operand2}",
            ArithmeticOperation.Mul => $"{Operand1} * {Operand2}",
            ArithmeticOperation.Div => $"{Operand1} / {Operand2}",
            ArithmeticOperation.Rem => $"{Operand1} % {Operand2}",
            ArithmeticOperation.And => $"{Operand1} & {Operand2}",
            ArithmeticOperation.Or => $"{Operand1} | {Operand2}",
            ArithmeticOperation.Xor => $"{Operand1} ^ {Operand2}",
            ArithmeticOperation.Shr => $"{Operand1} << {Operand2}",
            ArithmeticOperation.Shl => $"{Operand1} >> {Operand2}",
            ArithmeticOperation.Eq => $"{Operand1} == {Operand2}",
            ArithmeticOperation.Gt =>
                $"{Operand1} {(Operand2 is ConstCodeLine { Primitive: "null" } ? "!=" : ">")} {Operand2}",
            ArithmeticOperation.Lt => $"{Operand1} < {Operand2}",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public override string ToString()
    {
        return ToString(false);
    }
}
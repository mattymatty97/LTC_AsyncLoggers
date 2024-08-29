using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class ConstCodeLine : ICodeLine
{
    protected internal ConstCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);
        Method = method;

        EndInstruction = instruction;

        Primitive = instruction.OpCode.Code switch
        {
            // Null and primitives
            Code.Ldnull => "null",
            Code.Ldc_I4
                or Code.Ldc_I8
                or Code.Ldc_R4
                or Code.Ldc_R8
                or Code.Ldc_I4_S
                or Code.Ldftn
                or Code.Ldvirtftn
                or Code.Ldtoken
                => instruction.Operand,
            // Integer constants
            Code.Ldc_I4_0 => 0,
            Code.Ldc_I4_1 => 1,
            Code.Ldc_I4_2 => 2,
            Code.Ldc_I4_3 => 3,
            Code.Ldc_I4_4 => 4,
            Code.Ldc_I4_5 => 5,
            Code.Ldc_I4_6 => 6,
            Code.Ldc_I4_7 => 7,
            Code.Ldc_I4_8 => 8,
            Code.Ldc_I4_M1 => -1,
            // String constants
            Code.Ldstr => EscapeForCode((string)instruction.Operand),
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };


        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () => $"{method.FullName}:{ICodeLine.PrintStack()} - {Primitive}");

        ICodeLine.CurrentStack.Value.Pop();
    }

    public object Primitive { get; }

    public bool HasReturn => true;
    public MethodDefinition Method { get; }

    public Instruction StartInstruction => EndInstruction;
    public Instruction EndInstruction { get; }

    public IEnumerable<ICodeLine> GetArguments()
    {
        return [];
    }

    public string ToString(bool isRoot)
    {
        return Primitive.ToString();
    }

    public bool IsIncomplete => false;

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        return false;
    }

    public override string ToString()
    {
        return ToString(false);
    }

    private static string EscapeForCode(string str)
    {
        var sb = new StringBuilder();
        foreach (var c in str)
            switch (c)
            {
                case '\\':
                    sb.Append(@"\\");
                    break;
                case '\"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c < 32 || c > 126) // Non-printable or extended ASCII characters
                        sb.AppendFormat("\\u{0:X4}", (int)c);
                    else
                        sb.Append(c);

                    break;
            }

        return "\"" + sb + "\"";
    }
}
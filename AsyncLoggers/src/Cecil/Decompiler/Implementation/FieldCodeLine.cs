using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class FieldCodeLine : ICodeLine
{
    public bool IsStoring { get; }
    
    public bool IsStatic { get; }
    public FieldReference Field { get; }
    
    private LinkedList<ICodeLine> Arguments { get; } = [];

    public bool HasReturn => !IsStoring;
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Arguments.First()?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }
    
    public IEnumerable<ICodeLine> GetArguments()
    {
        return Arguments.ToArray();
    }

    public FieldCodeLine(MethodDefinition method, Instruction instruction)
    {
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
            Code.Ldfld or Code.Ldflda or Code.Stfld=> false,
            Code.Stsfld or Code.Ldsfld or Code.Ldsflda=> true,
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };

        Field = (FieldReference)instruction.Operand;

        
        if (IsStoring)
        {
            var argument = ICodeLine.InternalParseInstruction(method, instruction.Previous);
            if (argument == null)
                throw new InvalidOperationException($"{Method.FullName}:IL_{EndInstruction.Offset} Cannot find Value for field store");
            Arguments.AddFirst(argument);
        }
        
        if (!IsStatic)
        {
            var argument = ICodeLine.InternalParseInstruction(method, instruction.Previous);
            if (argument == null)
                throw new InvalidOperationException($"{Method.FullName}:IL_{EndInstruction.Offset} Cannot find object for instance field");
            Arguments.AddFirst(argument);
        }
        
    }
    
    public bool IsMissingArgument => Arguments.Any(a => a.IsMissingArgument);
    
    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsMissingArgument)
            return false;
        
        var @fixed = true;
        
        foreach (var argument in GetArguments())
        {
            @fixed &= argument?.SetMissingArgument(codeLine) ?? false;
        }

        return @fixed;
    }

    public override string ToString()
    {
        return ToString(false);
    }
    
    public string ToString(bool isRoot)
    {
        var text = (IsStatic ? $"{Field.DeclaringType.FullName}" : Arguments.First()) + "." + Field.Name;
        return IsStoring ? $"{text} = {Arguments.Last()}" : text;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation.Composite;

public class TernaryCodeLine : ICompoundCodeLine
{
    public bool HasReturn => (False?.HasReturn ?? true) && (True?.HasReturn ?? true);
    public MethodDefinition Method { get; }
    public Instruction StartInstruction => Condition?.StartInstruction ?? EndInstruction;
    public Instruction EndInstruction { get; }
    
    public ICodeLine True { get; }
    public ICodeLine False { get; }
    public ICodeLine Condition { get; }
    
    public IEnumerable<ICodeLine> GetArguments()
    {
        return [Condition, True, False];
    }

    public bool IsMissingArgument => GetArguments().Any(a => a.IsMissingArgument);
    
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

    public TernaryCodeLine(MethodDefinition method, Instruction instruction)
    {
        Method = method;
        EndInstruction = instruction;

        ICodeLine.CurrentVisitedBranches.Value.Add(instruction);
        True = ICodeLine.InternalParseInstruction(method, instruction);
        ICodeLine.CurrentVisitedBranches.Value.Remove(instruction);
        
        if (True == null)
            throw new InvalidOperationException($"{Method.FullName}:IL_{EndInstruction.Offset} Cannot find Operand1 for Ternary");

        var branchTarget = ICodeLine.GetBranchInstructionForTarget(instruction.Next).First();
        
        False = ICodeLine.InternalParseInstruction(method, branchTarget.Previous);
        
        if (False == null)
            throw new InvalidOperationException($"{Method.FullName}:IL_{EndInstruction.Offset} Cannot find Operand2 for Ternary");

        var branchTarget2 = ICodeLine.GetBranchInstructionForTarget(branchTarget.Next).First();

        if (branchTarget2.OpCode.Code is Code.Brfalse or Code.Brfalse_S)
        {
            (True, False) = (False, True);
        }
        
        Condition = ICodeLine.InternalParseInstruction(method, branchTarget2.Previous);
        
        if (Condition == null)
            throw new InvalidOperationException($"{Method.FullName}:IL_{EndInstruction.Offset} Cannot find Condition for Ternary");

        
    }

    public override string ToString()
    {
        return ToString(false);
    }

    public string ToString(bool isRoot)
    {
        return $"({Condition} ? {True} : {False})";
    }
}
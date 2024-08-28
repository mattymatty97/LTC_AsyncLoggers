using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AsyncLoggers.Cecil.Decompiler.Implementation;
using AsyncLoggers.Cecil.Decompiler.Implementation.Composite;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler;

public interface ICodeLine
{
    public bool HasReturn { get; }
    public MethodDefinition Method { get; }
    public Instruction StartInstruction { get; }
    public Instruction EndInstruction { get; }
    
    public IEnumerable<ICodeLine> GetArguments();

    public bool IsMissingArgument { get; }

    public bool SetMissingArgument(ICodeLine codeLine);

    public string ToString(bool isRoot);


    private static readonly ThreadLocal<ExceptionHandler> CurrentExceptionHandler = new(() => null);
    private static readonly ThreadLocal<Dictionary<Instruction, ICollection<Instruction>>> CurrentBranches = new(() => []);
    internal static readonly ThreadLocal<ISet<Instruction>> CurrentVisitedBranches = new(() => new HashSet<Instruction>());

    public static ICodeLine ParseInstruction(MethodDefinition method, Instruction instruction)
    {
        CurrentVisitedBranches.Value.Clear();
        CurrentBranches.Value.Clear();
        GenBranchInstructions(method);
        var exceptionHandler = GetCatchBlock(instruction, method.Body.ExceptionHandlers);
        CurrentExceptionHandler.Value = exceptionHandler;

        return InternalParseInstruction(method, instruction, false);
    }

    internal static ICodeLine InternalParseInstruction(MethodDefinition method, Instruction instruction, bool needsReturn = true)
    {
        var codeLine = _InternalParseInstruction(method, instruction);
        
        while (codeLine is { HasReturn: false } && needsReturn)
        {
            //TODO: Log retry!
            codeLine = _InternalParseInstruction(method, instruction);
        } 

        return codeLine;
    }
    
    private static ICodeLine _InternalParseInstruction(MethodDefinition method, Instruction instruction)
    {

        var exceptionHandler = GetCatchBlock(instruction, method.Body.ExceptionHandlers);

        if (CurrentExceptionHandler.Value != exceptionHandler)
        {
            return new ExceptionCodeLine(method, instruction.Next);
        }

        if (!CurrentVisitedBranches.Value.Contains(instruction))
        {
            var nextInstruction = instruction.Next;
            var branches = GetBranchInstructionForTarget(nextInstruction);
            if (branches.Count == 1 && branches.First().OpCode.FlowControl != FlowControl.Cond_Branch)
                return new TernaryCodeLine(method, instruction);
        }

        if (instruction.OpCode == OpCodes.Br || instruction.OpCode.FlowControl != FlowControl.Call)
        {
            return null;
        }

        if (instruction.OpCode.OpCodeType == OpCodeType.Prefix)
            return new NopCodeLine(method, instruction);
        

        switch (instruction.OpCode.Code)
        {
            case Code.Ldarg_0:
            case Code.Ldarg_1:
            case Code.Ldarg_2:
            case Code.Ldarg_3:
            case Code.Ldarg:
            case Code.Ldarga:
            case Code.Starg:
            case Code.Ldarg_S:
            case Code.Ldarga_S:
            case Code.Starg_S:
                return new ParameterCodeLine(method, instruction);

            case Code.Ldloc_0:
            case Code.Ldloc_1:
            case Code.Ldloc_2:
            case Code.Ldloc_3:
            case Code.Ldloc:
            case Code.Ldloca:
            case Code.Ldloc_S:
            case Code.Ldloca_S:
                var val = new LocalCodeLine(method, instruction);
                var prev = InternalParseInstruction(method, val.StartInstruction.Previous, false);
                if (prev is LocalCodeLine { IsStoring: true } codeLine && codeLine.Index == val.Index)
                    return codeLine.Value;
                return val;
            case Code.Stloc_0:
            case Code.Stloc_1:
            case Code.Stloc_2:
            case Code.Stloc_3:
            case Code.Stloc:
            case Code.Stloc_S:
                return new LocalCodeLine(method, instruction);
            
            case Code.Ldftn:
            case Code.Ldvirtftn:
            case Code.Ldtoken:
            case Code.Ldnull:
            case Code.Ldstr:
            case Code.Ldc_I4_M1:
            case Code.Ldc_I4_0:
            case Code.Ldc_I4_1:
            case Code.Ldc_I4_2:
            case Code.Ldc_I4_3:
            case Code.Ldc_I4_4:
            case Code.Ldc_I4_5:
            case Code.Ldc_I4_6:
            case Code.Ldc_I4_7:
            case Code.Ldc_I4_8:
            case Code.Ldc_I4_S:
            case Code.Ldc_I4:
            case Code.Ldc_I8:
            case Code.Ldc_R4:
            case Code.Ldc_R8:
                return new ConstCodeLine(method, instruction);
            
            case Code.Ldfld:
            case Code.Ldflda:
            case Code.Stfld:
            case Code.Ldsfld:
            case Code.Ldsflda:
            case Code.Stsfld:
                return new FieldCodeLine(method, instruction);
            
            case Code.Dup:
                return new DupCodeLine(method, instruction);
            
            case Code.Call:
            case Code.Calli:
            case Code.Callvirt:
            case Code.Newobj:
                return new MethodCodeLine(method, instruction);
            
            case Code.Add_Ovf:
            case Code.Add_Ovf_Un:
            case Code.Mul_Ovf:
            case Code.Mul_Ovf_Un:
            case Code.Sub_Ovf:
            case Code.Sub_Ovf_Un:
            case Code.Add:
            case Code.Sub:
            case Code.Mul:
            case Code.Div:
            case Code.Div_Un:
            case Code.And:
            case Code.Or:
            case Code.Xor:
            case Code.Shl:
            case Code.Shr:
            case Code.Shr_Un:
            case Code.Rem:
            case Code.Rem_Un:
            case Code.Ceq:
            case Code.Cgt:
            case Code.Cgt_Un:
            case Code.Clt:
            case Code.Clt_Un:
                break;
            
            case Code.Neg:
            case Code.Not:
                break;
            
            case Code.Refanyval:
                return new ReferenceCodeLine(method, instruction);
            
            case Code.Ldobj:
            case Code.Ldind_I1:
            case Code.Ldind_U1:
            case Code.Ldind_I2:
            case Code.Ldind_U2:
            case Code.Ldind_I4:
            case Code.Ldind_U4:
            case Code.Ldind_I8:
            case Code.Ldind_I:
            case Code.Ldind_R4:
            case Code.Ldind_R8:
            case Code.Ldind_Ref:
            case Code.Stind_Ref:
            case Code.Stind_I:
            case Code.Stind_I1:
            case Code.Stind_I2:
            case Code.Stind_I4:
            case Code.Stind_I8:
            case Code.Stind_R4:
            case Code.Stind_R8:
                return new DeReferenceCodeLine(method, instruction);
            
            case Code.Isinst:
            case Code.Castclass:
                return new CastCodeLine(method, instruction);
            
            case Code.Refanytype:
                return new TypeOfCodeLine(method, instruction);
            
            case Code.Newarr:
                return new NewArrCodeLine(method, instruction);
            
            case Code.Ldlen:
                return new ArrLenCodeLine(method, instruction);
            
            case Code.Ldelema:
            case Code.Ldelem_I1:
            case Code.Ldelem_U1:
            case Code.Ldelem_I2:
            case Code.Ldelem_U2:
            case Code.Ldelem_I4:
            case Code.Ldelem_U4:
            case Code.Ldelem_I8:
            case Code.Ldelem_I:
            case Code.Ldelem_R4:
            case Code.Ldelem_R8:
            case Code.Ldelem_Ref:
            case Code.Ldelem_Any:
            case Code.Stelem_I:
            case Code.Stelem_I1:
            case Code.Stelem_I2:
            case Code.Stelem_I4:
            case Code.Stelem_I8:
            case Code.Stelem_R4:
            case Code.Stelem_R8:
            case Code.Stelem_Ref:
            case Code.Stelem_Any:
                break;
        }
        
        if (instruction.OpCode is { StackBehaviourPop: StackBehaviour.Pop0, StackBehaviourPush: StackBehaviour.Push0 })
            return new NopCodeLine(method, instruction);

        return null;
    }
    
    private static ExceptionHandler GetCatchBlock(Instruction instruction,
        ICollection<ExceptionHandler> exceptionHandlers)
    {
        foreach (var handler in exceptionHandlers)
        {
            if (handler.HandlerType == ExceptionHandlerType.Catch)
            {
                // Check if the instruction is within the bounds of the catch block
                if (instruction.Offset >= handler.HandlerStart.Offset &&
                    instruction.Offset <= handler.HandlerEnd.Offset)
                {
                    return handler;
                }
            }
        }

        return null;
    }
    
    private static void GenBranchInstructions(MethodDefinition method)
    {
        var branchInstructions = CurrentBranches.Value;
        var instructions = method.Body.Instructions;

        foreach (var instruction in instructions)
        {
            // Check for branch instructions
            if (instruction.OpCode.FlowControl != FlowControl.Cond_Branch &&
                instruction.OpCode.FlowControl != FlowControl.Branch) 
                continue;
            
            // Get the target instruction
            Instruction target;
            switch (instruction.Operand)
            {
                case Instruction targetInstruction:
                    target = targetInstruction;
                    break;
                case int offset:
                {
                    // Calculate the target instruction for short branches
                    var targetOffset = instruction.Offset + offset;
                    target = instructions.FirstOrDefault(i => i.Offset == targetOffset);
                    break;
                }
                default:
                    continue;
            }

            // Map the target instruction to the branch instruction
            if (target == null) 
                continue;

            if (!branchInstructions.TryGetValue(target, out var list))
            {
                list = new List<Instruction>();
                branchInstructions[target] = list;
            }
                    
            list.Add(instruction);
        }
    }

    internal static ICollection<Instruction> GetBranchInstructionForTarget(Instruction target)
    {
        return CurrentBranches.Value?.GetValueOrDefault(target, []);
    }
    
}
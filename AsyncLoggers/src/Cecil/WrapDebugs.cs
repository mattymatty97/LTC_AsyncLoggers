using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using BepInEx;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using UnityEngine;

namespace AsyncLoggers.Cecil;

internal static class WrapDebugs
{
    internal static void ProcessAssembly(AssemblyDefinition assembly)
    {
        foreach (var moduleDefinition in assembly.Modules)
        {
            ProcessModule(assembly, moduleDefinition);
        }
    }

    private static void ProcessModule(AssemblyDefinition assembly, ModuleDefinition module)
    {
        foreach (var typeDefinition in module.Types)
        {
            ProcessType(assembly, typeDefinition);
        }
    }

    private static void ProcessType(AssemblyDefinition assembly, TypeDefinition type)
    {
        foreach (var methodDefinition in type.Methods)
        {
            ProcessMethod(assembly, type, methodDefinition);
        }
    }

    private static void ProcessMethod(AssemblyDefinition assembly, TypeDefinition type, MethodDefinition method)
    {
        if (!method.HasBody)
            return;

        var instructions = method.Body.Instructions;
        var exceptionHandlers = method.Body.ExceptionHandlers;

        for (int i = 0; i < instructions.Count; i++)
        {
            var instruction = instructions[i];

            if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
            {
                if (instruction.Operand is MethodReference methodReference &&
                    methodReference.DeclaringType.FullName == "UnityEngine.Debug" && methodReference.Name.StartsWith("Log"))
                {
                    // Check if the instruction is inside a catch block
                    if (!IsInsideCatchBlock(instruction, exceptionHandlers))
                    {
                        HandleLogCall(assembly, type, method, ref instructions, ref i);
                    }
                }
            }
        }
    }

    private static bool IsInsideCatchBlock(Instruction instruction, Collection<ExceptionHandler> exceptionHandlers)
    {
        foreach (var handler in exceptionHandlers)
        {
            if (handler.HandlerType == ExceptionHandlerType.Catch)
            {
                // Check if the instruction is within the bounds of the catch block
                if (instruction.Offset >= handler.HandlerStart.Offset &&
                    instruction.Offset <= handler.HandlerEnd.Offset)
                {
                    return true;
                }
            }
        }

        return false;
    }


    private static void HandleLogCall(AssemblyDefinition assembly, TypeDefinition type, MethodDefinition method,
        ref Collection<Instruction> instructions, ref int index)
    {
        var endIndex = index;
        var instruction = instructions[index];

        AsyncLoggers.Log.LogInfo(
            $"Method {method.DeclaringType.FullName}.{method.Name} has Log call at IL_{instruction.Offset:x4}.");

        var logDescription = new LinkedList<string>();

        var startIndex = FindFirstInstruction(method, index, logDescription, true);

        AsyncLoggers.Log.LogInfo(
            $"Method {method.DeclaringType.FullName}.{method.Name} has Log named \"{string.Join(", ",logDescription)}\"");

        AsyncLoggers.VerboseLog(
            $"Found span:\n{string.Join("\n", instructions.Skip(startIndex).Take(endIndex - startIndex + 1))}");
    }

    private static int FindFirstInstruction(MethodDefinition target, int index, LinkedList<string> arguments, bool isRoot = false)
    {
        var instructions = target.Body.Instructions;
        if (index < 0 || index >= instructions.Count)
            throw new IndexOutOfRangeException();

        var instruction = instructions[index];
        var opCode = instruction.OpCode;
        var operand = instruction.Operand;

        AsyncLoggers.VerboseLog($"{instruction}: Starting");
        try
        {
            if (opCode.OpCodeType == OpCodeType.Prefix)
            {
                AsyncLoggers.VerboseLog($"{instruction} is a Prefix ignoring!");
                return FindFirstInstruction(target, index - 1, arguments);
            }

            if (opCode == OpCodes.Dup)
            {
                arguments.AddLast("dup");
                return index;
            }

            // Handle method calls, including counting arguments
            if (opCode == OpCodes.Call || opCode == OpCodes.Callvirt)
            {
                if (operand is not MethodReference method)
                    throw new KeyNotFoundException();

                var lastIndex = index;
                var argumentCount = method.Parameters.Count + method.GenericParameters.Count +
                                    (method.HasThis ? 1 : 0);
                
                LinkedList<string> args = [];

                AsyncLoggers.VerboseLog($"{instruction}: has {argumentCount} arguments");
                for (int i = 0; i < argumentCount; i++)
                {
                    LinkedList<string> argumentDescriptions = [];
                    lastIndex = FindFirstInstruction(target, lastIndex - 1, argumentDescriptions);
                    args.AddFirst(string.Join(".", argumentDescriptions));
                }

                AsyncLoggers.VerboseLog($"{instruction}: startIndex {lastIndex}");

                if (!isRoot)
                {
                    if (method.ReturnType.MetadataType == MetadataType.Void)
                    {
                        AsyncLoggers.VerboseLog($"{instruction}: was a stub - continue search");
                        lastIndex = FindFirstInstruction(target, lastIndex - 1, arguments);
                        return lastIndex;
                    }
                }
                
                {
                    var property = FindPropertyByIL(instruction);
                    if (property != null)
                        if(args.Count > 0)
                            arguments.AddLast(args.First.Value + "." + property.Name);
                        else
                            arguments.AddLast($"({property.DeclaringType.FullName}::{property.Name})");
                    else
                    {
                        if (method.HasThis)
                        {
                            var @this = args.First.Value;
                            
                            arguments.AddLast($"{@this}.{method.Name}({string.Join(",", args.Skip(1))})");
                        }
                        else
                        {
                            arguments.AddLast(method.Name + "(" + string.Join(",", args) + ")");
                        }
                    }
                }

                return lastIndex;
            }

            int? ret_index = null;
            
            if (opCode.StackBehaviourPop == StackBehaviour.Pop0 && opCode.StackBehaviourPush != StackBehaviour.Push0)
            {
                AsyncLoggers.VerboseLog($"{instruction}: startIndex {index} is Ld*");
                ret_index = index;
            }else if (opCode is { StackBehaviourPop: StackBehaviour.Pop0, StackBehaviourPush: StackBehaviour.Push0 })
            {
                AsyncLoggers.VerboseLog($"{instruction}: is Nop");

                var startIndex = FindFirstInstruction(target, index - 1, arguments);

                AsyncLoggers.VerboseLog($"{instruction}: startIndex {startIndex}");

                ret_index = startIndex;
            }
            else
            {
                switch (opCode.StackBehaviourPop)
                {
                    case StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref:
                    {
                        AsyncLoggers.VerboseLog($"{instruction}: has 1 param");

                        LinkedList<string> subArguments = [];
                        var startIndex = FindFirstInstruction(target, index - 1, subArguments);

                        AsyncLoggers.VerboseLog($"{instruction}: startIndex {startIndex}");

                        if (!isRoot && opCode.StackBehaviourPush == StackBehaviour.Push0)
                        {
                            AsyncLoggers.VerboseLog($"{instruction}: was a stub - continue search");
                            startIndex = FindFirstInstruction(target, startIndex - 1, arguments);
                        }
                        else
                        {
                            foreach (var subArgument in subArguments)
                            {
                                arguments.AddLast(subArgument);
                            }
                        }

                        ret_index = startIndex;
                        break;
                    }
                    case StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1
                        or StackBehaviour.Popi_popi or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi:
                    {
                        AsyncLoggers.VerboseLog($"{instruction}: has 2 param");
                        var lastIndex = index;
                        LinkedList<string> subArguments = [];
                        lastIndex = FindFirstInstruction(target, lastIndex - 1, subArguments);
                        string indexArg = string.Join(".", subArguments);
                        subArguments.Clear();
                        lastIndex = FindFirstInstruction(target, lastIndex - 1, subArguments);

                        AsyncLoggers.VerboseLog($"{instruction}: startIndex {lastIndex}");
                        if (!isRoot && opCode.StackBehaviourPush == StackBehaviour.Push0)
                        {
                            AsyncLoggers.VerboseLog($"{instruction}: was a stub - continue search");
                            lastIndex = FindFirstInstruction(target, lastIndex - 1, arguments);
                        }
                        else
                        {

                            if (IsLdElem(opCode))
                            {
                            
                                foreach (var subArgument in subArguments.SkipLast(1))
                                {
                                    arguments.AddLast(subArgument);
                                }
                                
                                arguments.AddLast($"{subArguments.Last.Value}[{indexArg}]");
                            }
                            else
                            {
                                arguments.AddLast($"{opCode}({string.Join(".", subArguments)}, {indexArg})");
                            }
                        }

                        ret_index = lastIndex;
                        break;
                    }
                    case StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_popi
                        or StackBehaviour.Popref_popi_popref:
                    {
                        AsyncLoggers.VerboseLog($"{instruction}: has 3 param");
                        var lastIndex = index;
                        LinkedList<string> subArguments = [];
                        LinkedList<string> indexArgs = [];
                        lastIndex = FindFirstInstruction(target, lastIndex - 1, subArguments);
                        indexArgs.AddFirst(string.Join(".", subArguments));
                        subArguments.Clear();
                        lastIndex = FindFirstInstruction(target, lastIndex - 1, subArguments);
                        indexArgs.AddFirst(string.Join(".", subArguments));
                        subArguments.Clear();
                        lastIndex = FindFirstInstruction(target, lastIndex - 1, subArguments);
                        indexArgs.AddFirst(string.Join(".", subArguments));
                        
                        AsyncLoggers.VerboseLog($"{instruction}: startIndex {lastIndex}");

                        if (!isRoot && opCode.StackBehaviourPush == StackBehaviour.Push0)
                        {
                            AsyncLoggers.VerboseLog($"{instruction}: was a stub - continue search");
                            lastIndex = FindFirstInstruction(target, lastIndex - 1, arguments);
                        }
                        else
                        {
                            arguments.AddLast($"{opCode}({string.Join(",", indexArgs)})");
                        }

                        ret_index = lastIndex;
                        break;
                    }
                }
            }

            if (ret_index.HasValue)
            {
                var varName = FindParameterName(target, instruction);
                if (varName != null)
                {
                    arguments.AddLast(varName);
                }

                return ret_index.Value;
            }
        }
        catch (Exception ex)
        {
            AsyncLoggers.Log.LogFatal(instruction);
            ExceptionDispatchInfo.Capture(ex).Throw();
        }

        throw new KeyNotFoundException("Could not find first instruction");
    }

    private static string FindParameterName(MethodDefinition method, Instruction instruction)
    {
        if (instruction.OpCode == OpCodes.Ldloc ||
            instruction.OpCode == OpCodes.Ldloc_S ||
            instruction.OpCode == OpCodes.Ldloc_0 ||
            instruction.OpCode == OpCodes.Ldloc_1 ||
            instruction.OpCode == OpCodes.Ldloc_2 ||
            instruction.OpCode == OpCodes.Ldloc_3 ||
            instruction.OpCode == OpCodes.Ldloca ||
            instruction.OpCode == OpCodes.Ldloca_S )
        {
            int index = -1;

            if (instruction.OpCode == OpCodes.Ldloc || instruction.OpCode == OpCodes.Ldloc_S ||
                instruction.OpCode == OpCodes.Ldloca || instruction.OpCode == OpCodes.Ldloca_S)
            {
                return $"#{{{((VariableDefinition)instruction.Operand).Index}}}";
            }
            else if (instruction.OpCode == OpCodes.Ldloc_0)
            {
                index = 0;
            }
            else if (instruction.OpCode == OpCodes.Ldloc_1)
            {
                index = 1;
            }
            else if (instruction.OpCode == OpCodes.Ldloc_2)
            {
                index = 2;
            }
            else if (instruction.OpCode == OpCodes.Ldloc_3)
            {
                index = 3;
            }
            return $"#{{{index}}}";
        }
        else if (instruction.OpCode == OpCodes.Ldarg ||
                 instruction.OpCode == OpCodes.Ldarg_0 ||
                 instruction.OpCode == OpCodes.Ldarg_1 ||
                 instruction.OpCode == OpCodes.Ldarg_2 ||
                 instruction.OpCode == OpCodes.Ldarg_3 ||
                 instruction.OpCode == OpCodes.Ldarga ||
                 instruction.OpCode == OpCodes.Ldarg_S ||
                 instruction.OpCode == OpCodes.Ldarga_S)
        {
            int index = -1;

            if (instruction.OpCode == OpCodes.Ldarg || instruction.OpCode == OpCodes.Ldarg_S || 
                instruction.OpCode == OpCodes.Ldarga || instruction.OpCode == OpCodes.Ldarga_S)
            {
                return ((ParameterDefinition)instruction.Operand).Name;
            }
            else if (instruction.OpCode == OpCodes.Ldarg_0)
            {
                if (method.HasThis)
                    return "this";
                index = 0;
            }
            else if (instruction.OpCode == OpCodes.Ldarg_1)
            {
                index = 1;
            }
            else if (instruction.OpCode == OpCodes.Ldarg_2)
            {
                index = 2;
            }
            else if (instruction.OpCode == OpCodes.Ldarg_3)
            {
                index = 3;
            }

            if (method.HasThis)
                index--;

            if (index >= 0 && index < method.Parameters.Count)
            {
                var variable = method.Parameters[index];
                return variable.Name;
            }
        }
        else if (instruction.OpCode == OpCodes.Ldfld ||
                 instruction.OpCode == OpCodes.Ldflda ||
                 instruction.OpCode == OpCodes.Stfld ||
                 instruction.OpCode == OpCodes.Ldsfld ||
                 instruction.OpCode == OpCodes.Ldsflda ||
                 instruction.OpCode == OpCodes.Stsfld)
        {
            // Handling fields
            var fieldReference = (FieldReference)instruction.Operand;

            return fieldReference.Name;
        }
        else
        {
            var operand = instruction.Operand;
            if (operand == null)
                if (instruction.OpCode == OpCodes.Ldnull)
                    return "null";
                else
                    return null;
            
            if(operand.GetType().IsPrimitive)
            {
                return operand.ToString();
            }
            else if (operand is string s)
            {
                return '"' + s + '"';
            }
        }

        return null;
    }

    private static PropertyDefinition FindPropertyByIL(Instruction instruction)
    {
        if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt)
            return null;

        var targetMethod = instruction.Operand as MethodReference;

        var targetType = targetMethod!.DeclaringType.Resolve();

        foreach (var property in targetType.Properties)
        {
            if (property.GetMethod != null && targetMethod.FullName == property.GetMethod.FullName)
            {
                return property;
            }

            if (property.SetMethod != null && targetMethod.FullName == property.SetMethod.FullName)
            {
                return property;
            }
        }

        return null;
    }

    private static bool IsLdElem(OpCode opCode)
    {
        if (opCode == OpCodes.Ldelema)
            return true;
        if (opCode == OpCodes.Ldelem_Any)
            return true;
        if (opCode == OpCodes.Ldelem_I)
            return true;
        if (opCode == OpCodes.Ldelem_I1)
            return true;
        if (opCode == OpCodes.Ldelem_I2)
            return true;
        if (opCode == OpCodes.Ldelem_I4)
            return true;
        if (opCode == OpCodes.Ldelem_I8)
            return true;
        if (opCode == OpCodes.Ldelem_R4)
            return true;
        if (opCode == OpCodes.Ldelem_R8)
            return true;
        if (opCode == OpCodes.Ldelem_Ref)
            return true;
        if (opCode == OpCodes.Ldelem_U1)
            return true;
        if (opCode == OpCodes.Ldelem_U2)
            return true;
        return opCode == OpCodes.Ldelem_U4;
    }
}
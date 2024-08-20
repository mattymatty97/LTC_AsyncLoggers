using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

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
                    methodReference.DeclaringType.FullName == "UnityEngine.Debug" &&
                    methodReference.Name.StartsWith("Log"))
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

        var typeName = type.FullName;

        var methodName = method.Name;

        AsyncLoggers.VerboseLog(LogLevel.Info,
            $"Method {typeName}:{methodName} has Log call at IL_{instruction.Offset:x4}.");

        var logDescription = new LinkedList<string>();

        var startIndex = FindFirstInstruction(method, index, logDescription, true);

        var logline = string.Join(", ", logDescription);

        AsyncLoggers.VerboseLog(LogLevel.Info,
            $"Method {typeName}:{methodName} has Log named \"{logline}\"");

        AsyncLoggers.VerboseLog(LogLevel.Debug,
            $"Found span:\n{string.Join("\n", instructions.Skip(startIndex).Take(endIndex - startIndex + 1))}");

        var config = LogCallInfo.GetOrAdd(assembly.Name.Name, type.FullName, method.Name, logline);

        if (config.Status == LogCallInfo.CallStatus.Suppressed)
        {
            AsyncLoggers.VerboseLog(LogLevel.Warning,$"Suppressing \"{logline}\" from {config.ClassName}:{config.MethodName}");
            var args = ((MethodReference)instruction.Operand).Parameters.Count;
            instructions[index] = Instruction.Create(OpCodes.Pop);
            for (var i = 1; i < args; i++)
            {
                instructions.Insert(index, Instruction.Create(OpCodes.Pop));
            }
        }
        else if (config.Status == LogCallInfo.CallStatus.BepInEx)
        {
            var callReference = instruction.Operand as MethodReference;
            if (config.Delay.HasValue)
            {
                AsyncLoggers.VerboseLog(LogLevel.Warning,$"Throrling \"{logline}\" from {config.ClassName}:{config.MethodName}");
                var logKey = ThrothledLogWrapper.NextID;
                ThrothledLogWrapper.DelayMemory[logKey] = TimeSpan.FromMilliseconds(config.Delay.Value);
                ReplaceCallWithThrothledBepIn(method, instructions, ref index, ref startIndex, callReference, logKey);
            }
            else
            {
                AsyncLoggers.VerboseLog(LogLevel.Warning,$"Wrapping \"{logline}\" from {config.ClassName}:{config.MethodName}");
                ReplaceCallWithBepIn(method, instructions, ref index, callReference);
            }
            
            AsyncLoggers.VerboseLog(LogLevel.Debug,
                $"Modified span:\n{string.Join("\n", instructions.Skip(startIndex).Take(index - startIndex + 1))}");
        }
        
    }

    private static int FindFirstInstruction(MethodDefinition target, int index, LinkedList<string> arguments,
        bool isRoot = false)
    {
        // Retrieve the list of IL instructions from the method body
        var instructions = target.Body.Instructions;

        // Check if the provided index is within the bounds of the instruction list
        if (index < 0 || index >= instructions.Count)
            throw new IndexOutOfRangeException();

        // Get the current instruction, its OpCode, and operand
        var instruction = instructions[index];
        var opCode = instruction.OpCode;
        var operand = instruction.Operand;

        // Log the start of processing for the current instruction
        AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: Starting");

        try
        {
            // If the instruction is a prefix, ignore it and recurse on the previous instruction
            if (opCode.OpCodeType == OpCodeType.Prefix)
            {
                AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction} is a Prefix ignoring!");
                return FindFirstInstruction(target, index - 1, arguments);
            }

            // Handle duplication (Dup) instructions
            if (opCode == OpCodes.Dup)
            {
                arguments.AddLast("|dup|");
                return index;
            }

            // Handle method calls (Call, Callvirt, Newobj)
            if (opCode == OpCodes.Call || opCode == OpCodes.Callvirt || opCode == OpCodes.Newobj)
            {
                if (operand is not MethodReference method)
                    throw new KeyNotFoundException();

                var lastIndex = index;
                var argumentCount = method.Parameters.Count + (method.HasThis ? 1 : 0);

                // Initialize a list to store arguments for this method call
                LinkedList<string> args = new LinkedList<string>();

                AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: has {argumentCount} arguments");

                // Recursively find and process arguments for the method call
                for (int i = 0; i < argumentCount; i++)
                {
                    LinkedList<string> argumentDescriptions = new LinkedList<string>();
                    lastIndex = FindFirstInstruction(target, lastIndex - 1, argumentDescriptions);
                    args.AddFirst(string.Join(".", argumentDescriptions));
                }

                AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: startIndex {lastIndex}");

                // If this is not the root call and the method returns void, continue searching
                if (!isRoot && method.ReturnType.MetadataType == MetadataType.Void)
                {
                    AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: was a stub - continue search");
                    lastIndex = FindFirstInstruction(target, lastIndex - 1, arguments);
                    return lastIndex;
                }

                // Handle property or method calls based on the IL instruction
                ProcessMethodCallOrProperty(instruction, method, args, arguments);

                return lastIndex;
            }

            int? retIndex;

            // Handle stack behavior - instructions that don't pop but push to the stack
            if (opCode.StackBehaviourPop == StackBehaviour.Pop0 && opCode.StackBehaviourPush != StackBehaviour.Push0)
            {
                AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: startIndex {index} is Ld*");
                retIndex = index;
            }
            // Handle NOP instructions
            else if (opCode is { StackBehaviourPop: StackBehaviour.Pop0, StackBehaviourPush: StackBehaviour.Push0 })
            {
                AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: is Nop");

                var startIndex = FindFirstInstruction(target, index - 1, arguments);

                AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: startIndex {startIndex}");

                retIndex = startIndex;
            }
            else
            {
                // Handle instructions that pop 1, 2, or 3 parameters
                retIndex = HandlePopInstructions(opCode, target, index, arguments, isRoot, instruction);
            }

            // If we found an index to return, process any variable name and return it
            if (retIndex.HasValue)
            {
                var varName = FindParameterName(target, instruction);
                if (varName != null)
                {
                    arguments.AddLast(varName);
                }

                return retIndex.Value;
            }

            return FindFirstInstruction(target, index - 1, arguments);
        }
        catch (Exception ex)
        {
            // Log and rethrow any exceptions encountered during processing
            AsyncLoggers.Log.LogFatal(instruction);
            ExceptionDispatchInfo.Capture(ex).Throw();
        }

        // If no valid instruction was found, throw an exception
        throw new KeyNotFoundException("Could not find first instruction");
    }

    private static void ProcessMethodCallOrProperty(Instruction instruction, MethodReference method,
        LinkedList<string> args, LinkedList<string> arguments)
    {
        // Attempt to find a property based on the IL instruction
        var property = FindPropertyByIL(instruction);

        // If a property is found, format and add the property access to the arguments list
        if (property != null)
        {
            if (args.Count > 0)
                arguments.AddLast(args.First.Value + "." + property.Name); // Add property access with an argument
            else
                arguments.AddLast(
                    $"({property.DeclaringType.FullName}::{property.Name})"); // Add standalone property access
        }
        else
        {
            // If the instruction is a constructor call (Newobj), format it as an object creation
            if (instruction.OpCode == OpCodes.Newobj)
            {
                arguments.AddLast($"new {method.Name}({string.Join(",", args)})");
            }
            // If the method has a 'this' reference, format it as a method call on an instance
            else if (method.HasThis)
            {
                var @this = args.First.Value; // The first argument represents 'this'
                arguments.AddLast(
                    $"{@this}.{method.Name}({string.Join(",", args.Skip(1))})"); // Skip the first argument (this) in the method call
            }
            else
            {
                // Otherwise, format it as a static method call
                arguments.AddLast(method.Name + "(" + string.Join(",", args) + ")");
            }
        }
    }

    private static int? HandlePopInstructions(OpCode opCode, MethodDefinition target, int index,
        LinkedList<string> arguments, bool isRoot, Instruction instruction)
    {
        int? retIndex;

        switch (opCode.StackBehaviourPop)
        {
            case StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref:
            {
                AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: has 1 param");

                // Create a list to hold sub-arguments processed from previous instructions
                LinkedList<string> subArguments = new LinkedList<string>();

                // Recursively find and process the previous instruction
                var startIndex = FindFirstInstruction(target, index - 1, subArguments);

                AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: startIndex {startIndex}");

                // If not root and no value is pushed, continue searching backwards
                if (!isRoot && opCode.StackBehaviourPush == StackBehaviour.Push0)
                {
                    AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: was a stub - continue search");
                    retIndex = null;
                }
                else
                {
                    // Add the processed sub-arguments to the main arguments list
                    foreach (var subArgument in subArguments)
                    {
                        arguments.AddLast(subArgument);
                    }

                    retIndex = startIndex;
                }

                break;
            }
            case StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or StackBehaviour.Popi_popi
                or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi:
            {
                AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: has 2 param");
                var lastIndex = index;
                LinkedList<string> subArguments = new LinkedList<string>();

                // Process the first parameter by recursively finding and processing the previous instruction
                lastIndex = FindFirstInstruction(target, lastIndex - 1, subArguments);
                string indexArg = string.Join(".", subArguments);
                subArguments.Clear();

                // Process the second parameter
                lastIndex = FindFirstInstruction(target, lastIndex - 1, subArguments);

                AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: startIndex {lastIndex}");

                // If not root and no value is pushed, continue searching backwards
                if (!isRoot && opCode.StackBehaviourPush == StackBehaviour.Push0)
                {
                    AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: was a stub - continue search");
                    retIndex = null;
                }
                else
                {
                    // If the OpCode is a load element (LdElem), handle array indexing
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
                        // Otherwise, add the instruction with its parameters as arguments
                        arguments.AddLast($"{opCode}({string.Join(".", subArguments)}, {indexArg})");
                    }

                    retIndex = lastIndex;
                }

                break;
            }
            case StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_popi or StackBehaviour.Popref_popi_popref:
            {
                AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: has 3 param");
                var lastIndex = index;
                LinkedList<string> subArguments = new LinkedList<string>();
                LinkedList<string> indexArgs = new LinkedList<string>();

                // Process the three parameters by recursively finding and processing the previous instructions
                lastIndex = FindFirstInstruction(target, lastIndex - 1, subArguments);
                indexArgs.AddFirst(string.Join(".", subArguments));
                subArguments.Clear();

                lastIndex = FindFirstInstruction(target, lastIndex - 1, subArguments);
                indexArgs.AddFirst(string.Join(".", subArguments));
                subArguments.Clear();

                lastIndex = FindFirstInstruction(target, lastIndex - 1, subArguments);
                indexArgs.AddFirst(string.Join(".", subArguments));

                AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: startIndex {lastIndex}");

                // If not root and no value is pushed, continue searching backwards
                if (!isRoot && opCode.StackBehaviourPush == StackBehaviour.Push0)
                {
                    AsyncLoggers.VerboseLog(LogLevel.Debug, $"{instruction}: was a stub - continue search");
                    retIndex = null;
                }
                else
                {
                    // Add the processed parameters as arguments
                    arguments.AddLast($"{opCode}({string.Join(",", indexArgs)})");
                    retIndex = lastIndex;
                }

                break;
            }
            default:
                // Default behavior if no specific case is matched
                throw new KeyNotFoundException(instruction.ToString());
        }

        return retIndex;
    }

    private static string FindParameterName(MethodDefinition method, Instruction instruction)
    {
        switch (instruction.OpCode.Code)
        {
            // Local variable loading
            case Code.Ldloc:
            case Code.Ldloc_S:
            case Code.Ldloca:
            case Code.Ldloca_S:
                return $"#{{{((VariableDefinition)instruction.Operand).Index}}}";

            case Code.Ldloc_0:
                return "#0";
            case Code.Ldloc_1:
                return "#1";
            case Code.Ldloc_2:
                return "#2";
            case Code.Ldloc_3:
                return "#3";

            // Method arguments loading
            case Code.Ldarg:
            case Code.Ldarg_S:
            case Code.Ldarga:
            case Code.Ldarga_S:
                return ((ParameterDefinition)instruction.Operand).Name;

            case Code.Ldarg_0:
                return method.HasThis ? "this" : method.Parameters[0]?.Name;
            case Code.Ldarg_1:
                return method.Parameters[method.HasThis ? 0 : 1]?.Name;
            case Code.Ldarg_2:
                return method.Parameters[method.HasThis ? 1 : 2]?.Name;
            case Code.Ldarg_3:
                return method.Parameters[method.HasThis ? 2 : 3]?.Name;

            // Fields
            case Code.Ldfld:
            case Code.Ldflda:
            case Code.Stfld:
            case Code.Ldsfld:
            case Code.Ldsflda:
            case Code.Stsfld:
                return ((FieldReference)instruction.Operand).Name;

            // Array creation
            case Code.Newarr:
                return "new " + instruction.Operand.ToString() + "[]";

            // Null and primitives
            case Code.Ldnull:
                return "null";

            default:
                var operand = instruction.Operand;
                if (operand == null)
                    return null;

                if (operand.GetType().IsPrimitive)
                    return operand.ToString();

                if (operand is string s)
                    return $"\"{s}\"";

                break;
        }

        return null;
    }
    

    private static void ReplaceCallWithBepIn(MethodDefinition method, Collection<Instruction> instructions,
        ref int index, MethodReference methodRef)
    {
        // Import wrapper methods
        var logInfoMethod = method.Module.ImportReference(AccessTools.Method(typeof(LogWrapper),nameof(LogWrapper.LogInfo)));
        var logErrorMethod = method.Module.ImportReference(AccessTools.Method(typeof(LogWrapper),nameof(LogWrapper.LogError)));
        var logWarningMethod = method.Module.ImportReference(AccessTools.Method(typeof(LogWrapper),nameof(LogWrapper.LogWarning)));

        var logInfoWithContextMethod = method.Module.ImportReference(AccessTools.Method(typeof(LogWrapper),nameof(LogWrapper.LogInfoWithContext)));
        var logErrorWithContextMethod = method.Module.ImportReference(AccessTools.Method(typeof(LogWrapper),nameof(LogWrapper.LogErrorWithContext)));
        var logWarningWithContextMethod = method.Module.ImportReference(AccessTools.Method(typeof(LogWrapper),nameof(LogWrapper.LogWarningWithContext)));

        var logInfoFormatMethod = method.Module.ImportReference(AccessTools.Method(typeof(LogWrapper),nameof(LogWrapper.LogInfoFormat)));
        var logErrorFormatMethod = method.Module.ImportReference(AccessTools.Method(typeof(LogWrapper),nameof(LogWrapper.LogErrorFormat)));
        var logWarningFormatMethod = method.Module.ImportReference(AccessTools.Method(typeof(LogWrapper),nameof(LogWrapper.LogWarningFormat)));
        
        var logInfoFormatWithContextMethod = method.Module.ImportReference(AccessTools.Method(typeof(LogWrapper),nameof(LogWrapper.LogInfoFormatWithContext)));
        var logErrorFormatWithContextMethod = method.Module.ImportReference(AccessTools.Method(typeof(LogWrapper),nameof(LogWrapper.LogErrorFormatWithContext)));
        var logWarningFormatWithContextMethod = method.Module.ImportReference(AccessTools.Method(typeof(LogWrapper),nameof(LogWrapper.LogWarningFormatWithContext)));


        var instruction = instructions[index];
        MethodReference replacementMethod = null;
        var parametersCount = methodRef.Parameters.Count;

        switch (methodRef.Name)
        {
            case "Log":
                replacementMethod = parametersCount > 1 ? logInfoWithContextMethod : logInfoMethod;
                break;
            case "LogError":
                replacementMethod = parametersCount > 1 ? logErrorWithContextMethod : logErrorMethod;
                break;
            case "LogWarning":
                replacementMethod = parametersCount > 1 ? logWarningWithContextMethod : logWarningMethod;
                break;
            case "LogFormat":
                replacementMethod = parametersCount > 2 ? logInfoFormatWithContextMethod : logInfoFormatMethod;
                break;
            case "LogErrorFormat":
                replacementMethod = parametersCount > 2 ? logErrorFormatWithContextMethod : logErrorFormatMethod;
                break;
            case "LogWarningFormat":
                replacementMethod = parametersCount > 2 ? logWarningFormatWithContextMethod : logWarningFormatMethod;
                break;
        }

        if (replacementMethod != null)
        {
            // Replace the original Debug method call with the wrapper method
            var ilProcessor = method.Body.GetILProcessor();

            ilProcessor.Replace(instruction, Instruction.Create(OpCodes.Call, replacementMethod));
        }
    }
    
    private static void ReplaceCallWithThrothledBepIn(MethodDefinition method, Collection<Instruction> instructions,
        ref int index, ref int startIndex, MethodReference methodRef, long logkey)
    {
        // Import wrapper methods
        var logInfoMethod = method.Module.ImportReference(AccessTools.Method(typeof(ThrothledLogWrapper),nameof(LogWrapper.LogInfo)));
        var logErrorMethod = method.Module.ImportReference(AccessTools.Method(typeof(ThrothledLogWrapper),nameof(LogWrapper.LogError)));
        var logWarningMethod = method.Module.ImportReference(AccessTools.Method(typeof(ThrothledLogWrapper),nameof(LogWrapper.LogWarning)));

        var logInfoWithContextMethod = method.Module.ImportReference(AccessTools.Method(typeof(ThrothledLogWrapper),nameof(LogWrapper.LogInfoWithContext)));
        var logErrorWithContextMethod = method.Module.ImportReference(AccessTools.Method(typeof(ThrothledLogWrapper),nameof(LogWrapper.LogErrorWithContext)));
        var logWarningWithContextMethod = method.Module.ImportReference(AccessTools.Method(typeof(ThrothledLogWrapper),nameof(LogWrapper.LogWarningWithContext)));

        var logInfoFormatMethod = method.Module.ImportReference(AccessTools.Method(typeof(ThrothledLogWrapper),nameof(LogWrapper.LogInfoFormat)));
        var logErrorFormatMethod = method.Module.ImportReference(AccessTools.Method(typeof(ThrothledLogWrapper),nameof(LogWrapper.LogErrorFormat)));
        var logWarningFormatMethod = method.Module.ImportReference(AccessTools.Method(typeof(ThrothledLogWrapper),nameof(LogWrapper.LogWarningFormat)));
        
        var logInfoFormatWithContextMethod = method.Module.ImportReference(AccessTools.Method(typeof(ThrothledLogWrapper),nameof(LogWrapper.LogInfoFormatWithContext)));
        var logErrorFormatWithContextMethod = method.Module.ImportReference(AccessTools.Method(typeof(ThrothledLogWrapper),nameof(LogWrapper.LogErrorFormatWithContext)));
        var logWarningFormatWithContextMethod = method.Module.ImportReference(AccessTools.Method(typeof(ThrothledLogWrapper),nameof(LogWrapper.LogWarningFormatWithContext)));


        var instruction = instructions[index];
        MethodReference replacementMethod = null;
        var parametersCount = methodRef.Parameters.Count;

        switch (methodRef.Name)
        {
            case "Log":
                replacementMethod = parametersCount > 1 ? logInfoWithContextMethod : logInfoMethod;
                break;
            case "LogError":
                replacementMethod = parametersCount > 1 ? logErrorWithContextMethod : logErrorMethod;
                break;
            case "LogWarning":
                replacementMethod = parametersCount > 1 ? logWarningWithContextMethod : logWarningMethod;
                break;
            case "LogFormat":
                replacementMethod = parametersCount > 2 ? logInfoFormatWithContextMethod : logInfoFormatMethod;
                break;
            case "LogErrorFormat":
                replacementMethod = parametersCount > 2 ? logErrorFormatWithContextMethod : logErrorFormatMethod;
                break;
            case "LogWarningFormat":
                replacementMethod = parametersCount > 2 ? logWarningFormatWithContextMethod : logWarningFormatMethod;
                break;
        }

        if (replacementMethod != null)
        {
            // Replace the original Debug method call with the wrapper method
            
            var ilProcessor = method.Body.GetILProcessor();

            var startInstruction = instructions[startIndex];
            
            ilProcessor.InsertBefore(startInstruction, Instruction.Create(OpCodes.Ldc_I8, logkey));
            startIndex--;
            index++;
            
            ilProcessor.Replace(instruction, Instruction.Create(OpCodes.Call, replacementMethod));
        }
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using AsyncLoggers.Config;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

namespace AsyncLoggers.Cecil;

internal static class AssemblyAnalyzer
{
    private class LogRedirects
    {
        private readonly ModuleDefinition _moduleDefinition;
        private readonly Type _redirectType;

        private MethodReference _logInfoMethod;
        private MethodReference _logErrorMethod;
        private MethodReference _logWarningMethod;

        private MethodReference _logInfoWithContextMethod;
        private MethodReference _logErrorWithContextMethod;
        private MethodReference _logWarningWithContextMethod;

        private MethodReference _logInfoFormatMethod;
        private MethodReference _logErrorFormatMethod;
        private MethodReference _logWarningFormatMethod;

        private MethodReference _logInfoFormatWithContextMethod;
        private MethodReference _logErrorFormatWithContextMethod;
        private MethodReference _logWarningFormatWithContextMethod;

        public MethodReference LOGInfoMethod
        {
            get
            {
                _logInfoMethod ??=
                    _moduleDefinition.ImportReference(
                        AccessTools.Method(_redirectType, "LogInfo"));
                return _logInfoMethod;
            }
        }

        public MethodReference LOGErrorMethod
        {
            get
            {
                _logErrorMethod ??=
                    _moduleDefinition.ImportReference(AccessTools.Method(_redirectType,
                        "LogError"));
                return _logErrorMethod;
            }
        }

        public MethodReference LOGWarningMethod
        {
            get
            {
                _logWarningMethod ??=
                    _moduleDefinition.ImportReference(AccessTools.Method(_redirectType,
                        "LogWarning"));
                return _logWarningMethod;
            }
        }

        public MethodReference LOGInfoWithContextMethod
        {
            get
            {
                _logInfoWithContextMethod ??=
                    _moduleDefinition.ImportReference(AccessTools.Method(_redirectType,
                        "LogInfoWithContext"));
                return _logInfoWithContextMethod;
            }
        }

        public MethodReference LOGErrorWithContextMethod
        {
            get
            {
                _logErrorWithContextMethod ??=
                    _moduleDefinition.ImportReference(AccessTools.Method(_redirectType,
                        "LogErrorWithContext"));
                return _logErrorWithContextMethod;
            }
        }

        public MethodReference LOGWarningWithContextMethod
        {
            get
            {
                _logWarningWithContextMethod ??=
                    _moduleDefinition.ImportReference(AccessTools.Method(_redirectType,
                        "LogWarningWithContext"));
                return _logWarningWithContextMethod;
            }
        }

        public MethodReference LOGInfoFormatMethod
        {
            get
            {
                _logInfoFormatMethod ??=
                    _moduleDefinition.ImportReference(AccessTools.Method(_redirectType,
                        "LogInfoFormat"));
                return _logInfoFormatMethod;
            }
        }

        public MethodReference LOGErrorFormatMethod
        {
            get
            {
                _logErrorFormatMethod ??=
                    _moduleDefinition.ImportReference(AccessTools.Method(_redirectType,
                        "LogErrorFormat"));
                return _logErrorFormatMethod;
            }
        }

        public MethodReference LOGWarningFormatMethod
        {
            get
            {
                _logWarningFormatMethod ??=
                    _moduleDefinition.ImportReference(AccessTools.Method(_redirectType,
                        "LogWarningFormat"));
                return _logWarningFormatMethod;
            }
        }

        public MethodReference LOGInfoFormatWithContextMethod
        {
            get
            {
                _logInfoFormatWithContextMethod ??=
                    _moduleDefinition.ImportReference(AccessTools.Method(_redirectType,
                        "LogInfoFormatWithContext"));
                return _logInfoFormatWithContextMethod;
            }
        }

        public MethodReference LOGErrorFormatWithContextMethod
        {
            get
            {
                _logErrorFormatWithContextMethod ??=
                    _moduleDefinition.ImportReference(AccessTools.Method(_redirectType,
                        "LogErrorFormatWithContext"));
                return _logErrorFormatWithContextMethod;
            }
        }

        public MethodReference LOGWarningFormatWithContextMethod
        {
            get
            {
                _logWarningFormatWithContextMethod ??=
                    _moduleDefinition.ImportReference(AccessTools.Method(_redirectType,
                        "LogWarningFormatWithContext"));
                return _logWarningFormatWithContextMethod;
            }
        }

        internal LogRedirects(ModuleDefinition moduleDefinition, Type redirectType)
        {
            _moduleDefinition = moduleDefinition;
            _redirectType = redirectType;
        }
    }


    internal static void ProcessAssembly(AssemblyDefinition assembly, out long callCount)
    {
        callCount = 0;
        foreach (var moduleDefinition in assembly.Modules)
        {
            ProcessModule(assembly, moduleDefinition, ref callCount);
        }
    }

    private static void ProcessModule(AssemblyDefinition assembly, ModuleDefinition module, ref long callCount)
    {
        var normalRedirects = new LogRedirects(module, typeof(LogWrapper));
        var throttledRedirects = new LogRedirects(module, typeof(ThrothledLogWrapper));
        foreach (var typeDefinition in module.Types)
        {
            ProcessType(assembly, typeDefinition, normalRedirects, throttledRedirects, ref callCount);
        }
    }

    private static void ProcessType(AssemblyDefinition assembly, TypeDefinition type, LogRedirects normalRedirects,
        LogRedirects throttledRedirects, ref long callCount)
    {
        foreach (var methodDefinition in type.Methods)
        {
            ProcessMethod(assembly, type, methodDefinition, normalRedirects, throttledRedirects, ref callCount);
        }
    }

    private static void ProcessMethod(AssemblyDefinition assembly, TypeDefinition type, MethodDefinition method,
        LogRedirects normalRedirects, LogRedirects throttledRedirects, ref long callCount)
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
                    var exceptionBlock = GetCatchBlock(instruction, exceptionHandlers);

                    callCount++;
                    HandleLogCall(assembly, type, method, exceptionBlock, normalRedirects, throttledRedirects,
                        ref instructions, ref i);
                }
            }
        }
    }

    private static ExceptionHandler GetCatchBlock(Instruction instruction,
        Collection<ExceptionHandler> exceptionHandlers)
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


    private static void HandleLogCall(AssemblyDefinition assembly, TypeDefinition type, MethodDefinition method,
        ExceptionHandler exceptionBlock, LogRedirects normalRedirects, LogRedirects throttledRedirects,
        ref Collection<Instruction> instructions, ref int index)
    {
        var endIndex = index;
        var instruction = instructions[index];

        var typeName = type.FullName;

        var methodName = method.Name;

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Info, () =>
            $"Method {typeName}:{methodName} has Log call at IL_{instruction.Offset:x4}.");

        var logDescription = new LinkedList<string>();

        var startIndex = FindStartInstruction(method, exceptionBlock, index, logDescription, true).Index;

        var logline = logDescription.AsParameters();

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Info,
            () => $"Method {typeName}:{methodName} has Log named \"{logline}\"");

        var collection = instructions;
        var index1 = startIndex;
        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () =>
            index1 > 0
                ? $"Found span:\n{string.Join("\n", collection.Skip(index1).Take(endIndex - index1 + 1))}"
                : $"No span found: index is now {index1}");

        var config = XmlConfig.LogCallInfo.GetOrAdd(assembly.Name.Name, type.FullName, method.Name, logline);

        if (config.Status == XmlConfig.CallStatus.Suppressed)
        {
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Warning, () =>
                $"Suppressing \"{logline}\" from {config.ClassName}:{config.MethodName}");
            var args = ((MethodReference)instruction.Operand).Parameters.Count;
            instructions[index] = Instruction.Create(OpCodes.Pop);
            for (var i = 1; i < args; i++)
            {
                instructions.Insert(index, Instruction.Create(OpCodes.Pop));
            }
        }
        else if (config.Status == XmlConfig.CallStatus.BepInEx)
        {
            var callReference = instruction.Operand as MethodReference;
            if (startIndex != -1 && config.Cooldown.HasValue)
            {
                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Warning, () =>
                    $"Throttling \"{logline}\" from {config.ClassName}:{config.MethodName}");
                var logKey = ThrothledLogWrapper.NextID;
                ThrothledLogWrapper.CooldownMemory[logKey] = TimeSpan.FromMilliseconds(config.Cooldown.Value);
                ReplaceCallWithThrothledBepIn(method, throttledRedirects, instructions, ref index, ref startIndex,
                    callReference, logKey);
            }
            else
            {
                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Warning, () =>
                    $"Wrapping \"{logline}\" from {config.ClassName}:{config.MethodName}");
                ReplaceCallWithBepIn(method, normalRedirects, instructions, ref index, callReference);
            }

            var collection1 = instructions;
            var i = index;
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () =>
                startIndex > 0
                    ? $"Modified span:\n{string.Join("\n", collection1.Skip(startIndex).Take(i - startIndex + 1))}"
                    : $"No span found: index is now {startIndex}");
        }
    }

    private static StartIndex FindStartInstruction(MethodDefinition target, ExceptionHandler exceptionBlock, int index,
        LinkedList<string> arguments,
        bool isRoot = false)
    {
        // Retrieve the list of IL instructions from the method body
        var instructions = target.Body.Instructions;

        // Check if the provided index is within the bounds of the instruction list
        if (index < 0 || index >= instructions.Count)
            return new(Math.Min(-2, index));

        // Get the current instruction, its OpCode, and operand
        var instruction = instructions[index];
        var opCode = instruction.OpCode;
        var operand = instruction.Operand;

        if (exceptionBlock != null && exceptionBlock.HandlerStart.Offset > instruction.Offset)
        {
            var delta = instruction.Offset - exceptionBlock.HandlerStart.Offset;
            if (delta < 1)
            {
                if (delta != -1)
                    return new StartIndex(-1);

                arguments.AddLast("ex");
                return new StartIndex(index + 1);
            }
        }

        // Log the start of processing for the current instruction
        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () => $"{instruction}: Starting");

        try
        {
            // If the instruction is a prefix, ignore it and recurse on the previous instruction
            if (opCode.OpCodeType == OpCodeType.Prefix)
            {
                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () => $"{instruction} is a Prefix ignoring!");
                return FindStartInstruction(target, exceptionBlock, index - 1, arguments);
            }

            // Handle duplication (Dup) instructions
            if (opCode == OpCodes.Dup)
            {
                arguments.AddLast("|dup|");
                return new StartIndex(index, false, true);
            }


            StartIndex lastIndex;
            // Handle method calls (Call, Callvirt, Newobj)
            if (opCode == OpCodes.Call || opCode == OpCodes.Callvirt || opCode == OpCodes.Newobj)
            {
                if (operand is not MethodReference method)
                    throw new KeyNotFoundException();

                lastIndex = new StartIndex(index);
                var argumentCount = method.Parameters.Count + (method.HasThis && opCode != OpCodes.Newobj ? 1 : 0);

                // Initialize a list to store arguments for this method call
                var args = new LinkedList<string>();

                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                    () => $"{instruction}: has {argumentCount} arguments");

                // Recursively find and process arguments for the method call
                for (var i = 0; i < argumentCount; i++)
                {
                    var argumentDescriptions = new LinkedList<string>();
                    lastIndex = FindStartInstruction(target, exceptionBlock, lastIndex.Index - 1, argumentDescriptions);
                    args.AddFirst(argumentDescriptions.AsChain());
                }

                var index1 = lastIndex;
                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () => $"{instruction}: startIndex {index1}");

                // If this is not the root call and the method returns void, continue searching
                if (isRoot || method.ReturnType.MetadataType != MetadataType.Void)
                {
                    ProcessMethodCallOrProperty(instruction, method, args, arguments);

                    return lastIndex;
                }

                // Handle property or method calls based on the IL instruction
                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                    () => $"{instruction}: was a stub - continue search");
                lastIndex = new StartIndex(lastIndex.Index, true);
            }
            else
            {
                // Handle stack behavior - instructions that don't pop but push to the stack
                if (opCode.StackBehaviourPop == StackBehaviour.Pop0 &&
                    opCode.StackBehaviourPush != StackBehaviour.Push0)
                {
                    AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                        () => $"{instruction}: startIndex {index} is Ld*");
                    lastIndex = new StartIndex(index);
                }
                // Handle NOP instructions
                else if (opCode is { StackBehaviourPop: StackBehaviour.Pop0, StackBehaviourPush: StackBehaviour.Push0 })
                {
                    AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () => $"{instruction}: is Nop");
                    lastIndex = new StartIndex(index, true);
                }
                else
                {
                    Dictionary<int, string> arrayElements = new Dictionary<int, string>();
                    if (opCode == OpCodes.Stelem_Ref && HandleStlelemRef(
                            target, exceptionBlock, index, arrayElements,
                            instruction, out var retIndex
                        ))
                    {
                        var text = arrayElements.OrderBy(p => p.Key).Select(p => p.Value).FormatArray();
                        arguments.AddLast(text);
                        return retIndex;
                    }

                    // Handle instructions that pop 1, 2, or 3 parameters
                    lastIndex = HandlePopInstructions(opCode, target, exceptionBlock, index, arguments, isRoot,
                        instruction);
                }

                // If we found an index to return, process any variable name and return it
                if (!lastIndex.IsStub)
                {
                    var varName = FindParameterName(target, instruction);
                    if (varName != null)
                    {
                        arguments.AddLast(varName);
                    }

                    return lastIndex;
                }
            }

            return FindStartInstruction(target, exceptionBlock, lastIndex.Index - 1, arguments);
        }
        catch (Exception ex)
        {
            // Log and rethrow any exceptions encountered during processing
            AsyncLoggers.Log.LogFatal($"{target.FullName} - {instruction}");
            ExceptionDispatchInfo.Capture(ex).Throw();
        }

        // If no valid instruction was found, throw an exception
        return new StartIndex(-3);
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
                arguments.AddLast($"new {method.Name}{args.FormatArguments()}");
            }
            // If the method has a 'this' reference, format it as a method call on an instance
            else if (method.HasThis)
            {
                var @this = args.First.Value; // The first argument represents 'this'
                arguments.AddLast(
                    $"{@this}.{method.Name}{args.Skip(1).FormatArguments()}"); // Skip the first argument (this) in the method call
            }
            else
            {
                // Otherwise, format it as a static method call
                arguments.AddLast(method.Name + args.FormatArguments());
            }
        }
    }

    private static StartIndex HandlePopInstructions(OpCode opCode, MethodDefinition target,
        ExceptionHandler exceptionBlock, int index,
        LinkedList<string> arguments, bool isRoot, Instruction instruction)
    {
        StartIndex retIndex;

        switch (opCode.StackBehaviourPop)
        {
            case StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref:
            {
                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () => $"{instruction}: has 1 param");

                // Create a list to hold sub-arguments processed from previous instructions
                LinkedList<string> subArguments = new LinkedList<string>();

                // Recursively find and process the previous instruction
                var startIndex = FindStartInstruction(target, exceptionBlock, index - 1, subArguments).Index;

                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () => $"{instruction}: startIndex {startIndex}");

                // If not root and no value is pushed, continue searching backwards
                if (!isRoot && opCode.StackBehaviourPush == StackBehaviour.Push0)
                {
                    AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                        () => $"{instruction}: was a stub - continue search");
                    retIndex = new StartIndex(startIndex, true);
                }
                else
                {
                    // Add the processed sub-arguments to the main arguments list
                    foreach (var subArgument in subArguments)
                    {
                        arguments.AddLast(subArgument);
                    }

                    retIndex = new StartIndex(startIndex);
                }

                break;
            }
            case StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or StackBehaviour.Popi_popi
                or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi:
            {
                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () => $"{instruction}: has 2 param");
                var lastIndex = new StartIndex(index);
                LinkedList<string> subArguments = new LinkedList<string>();

                // Process the first parameter by recursively finding and processing the previous instruction
                lastIndex = FindStartInstruction(target, exceptionBlock, lastIndex.Index - 1, subArguments);
                string indexArg = subArguments.AsChain();
                subArguments.Clear();

                // Process the second parameter
                lastIndex = FindStartInstruction(target, exceptionBlock, lastIndex.Index - 1, subArguments);

                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () => $"{instruction}: startIndex {lastIndex}");

                // If not root and no value is pushed, continue searching backwards
                if (!isRoot && opCode.StackBehaviourPush == StackBehaviour.Push0)
                {
                    AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                        () => $"{instruction}: was a stub - continue search");
                    retIndex = new StartIndex(lastIndex.Index, true);
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

                        arguments.AddLast($"{subArguments.Last.Value}{ new[]{indexArg}.FormatArray() }");
                    }
                    else
                    {
                        // Otherwise, add the instruction with its parameters as arguments
                        arguments.AddLast($"{opCode}( {subArguments.AsChain()}, {indexArg} )");
                    }

                    retIndex = new StartIndex(lastIndex.Index);
                }

                break;
            }
            case StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_popi or StackBehaviour.Popref_popi_popref:
            {
                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () => $"{instruction}: has 3 param");
                var lastIndex = new StartIndex(index);
                LinkedList<string> subArguments = new LinkedList<string>();
                LinkedList<string> indexArgs = new LinkedList<string>();

                // Process the three parameters by recursively finding and processing the previous instructions
                lastIndex = FindStartInstruction(target, exceptionBlock, lastIndex.Index - 1, subArguments);
                indexArgs.AddFirst(subArguments.AsChain());
                subArguments.Clear();

                lastIndex = FindStartInstruction(target, exceptionBlock, lastIndex.Index - 1, subArguments);
                indexArgs.AddFirst(subArguments.AsChain());
                subArguments.Clear();

                lastIndex = FindStartInstruction(target, exceptionBlock, lastIndex.Index - 1, subArguments);
                indexArgs.AddFirst(subArguments.AsChain());

                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () => $"{instruction}: startIndex {lastIndex}");

                // If not root and no value is pushed, continue searching backwards
                if (!isRoot && opCode.StackBehaviourPush == StackBehaviour.Push0)
                {
                    AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                        () => $"{instruction}: was a stub - continue search");
                    retIndex = new StartIndex(lastIndex.Index, true);
                }
                else
                {
                    // Add the processed parameters as arguments
                    arguments.AddLast($"{opCode}{indexArgs.FormatArguments()}");
                    retIndex = new StartIndex(lastIndex.Index);
                }

                break;
            }
            default:
                retIndex = new StartIndex(-3);
                break;
        }

        return retIndex;
    }


    private static bool HandleStlelemRef(MethodDefinition target, ExceptionHandler exceptionBlock,
        int index, Dictionary<int, string> arrayArguments, Instruction instruction, out StartIndex retIndex)
    {
        var instructions = target.Body.Instructions;

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () => $"{instruction}: has 3 param (array initialization)");
        var lastIndex = new StartIndex(index);
        LinkedList<string> subArguments = new LinkedList<string>();

        // Process the inserted parameter!
        lastIndex = FindStartInstruction(target, exceptionBlock, lastIndex.Index - 1, subArguments);

        // Process index!
        LinkedList<string> arrayIndex = new LinkedList<string>();
        lastIndex = FindStartInstruction(target, exceptionBlock, lastIndex.Index - 1, arrayIndex);

        if (arrayIndex.Count != 1 || !int.TryParse(arrayIndex.First.Value, out var arrayIndexValue))
        {
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Error,
                () => $"{instruction}: index not found! {arrayIndex.FormatArray()}");
            retIndex = default;
            return false;
        }

        arrayArguments[arrayIndexValue] = subArguments.AsChain();

        lastIndex = FindStartInstruction(target, exceptionBlock, lastIndex.Index - 1, subArguments);

        if (lastIndex.IsDup)
        {
            var nestedInstruction = instructions[lastIndex.Index - 1];
            if (nestedInstruction.OpCode == OpCodes.Newarr)
            {
                retIndex = FindStartInstruction(target, exceptionBlock, lastIndex.Index - 1, subArguments);
                return true;
            }

            if (nestedInstruction.OpCode != OpCodes.Stelem_Ref)
            {
                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Error,
                    () => $"{instruction}: expected Stelem_Ref or NewArr but found {nestedInstruction}");
                retIndex = default;
                return false;
            }

            return HandleStlelemRef(target, exceptionBlock, lastIndex.Index - 1, arrayArguments, nestedInstruction,
                out retIndex);
        }
        else
        {
            var dupInstruction = instructions[lastIndex.Index];
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Error,
                () => $"{instruction}: expected Dup but found {dupInstruction}");
            retIndex = default;
            return false;
        }
    }


    private static string FindParameterName(MethodDefinition method, Instruction instruction)
    {
        var debugInfo = method.DebugInformation;
        if ( debugInfo != null)
        {
            var index = instruction.OpCode.Code switch
            {
                Code.Ldloc or Code.Ldloc_S or Code.Ldloca or Code.Ldloca_S => ((VariableDefinition)instruction.Operand).Index,
                Code.Ldloc_0 => 0,
                Code.Ldloc_1 => 1,
                Code.Ldloc_2 => 2,
                Code.Ldloc_3 => 3,
                _ => -1
            };

            if (index != -1)
            {
                foreach (var scope in debugInfo.GetScopes())
                {
                    if (scope.Start.Offset < instruction.Offset || scope.End.Offset > instruction.Offset)
                        continue;

                    foreach (var variable in scope.Variables.Where(variable => variable.Index == index))
                    {
                        return variable.Name;
                    }
                }
            }
        }
        
        
        switch (instruction.OpCode.Code)
        {
            // Local variable loading
            case Code.Ldloc:
            case Code.Ldloc_S:
            case Code.Ldloca:
            case Code.Ldloca_S:
                return $"var_{((VariableDefinition)instruction.Operand).Index}";

            case Code.Ldloc_0:
                return "var_0";
            case Code.Ldloc_1:
                return "var_1";
            case Code.Ldloc_2:
                return "var_2";
            case Code.Ldloc_3:
                return "var_3";

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
                return "new " + instruction.Operand + "[]";

            // Null and primitives
            case Code.Ldnull:
                return "null";

            // Integer constants
            case Code.Ldc_I4:
            case Code.Ldc_I4_S:
                return ((int)instruction.Operand).ToString();
            case Code.Ldc_I4_0:
                return "0";
            case Code.Ldc_I4_1:
                return "1";
            case Code.Ldc_I4_2:
                return "2";
            case Code.Ldc_I4_3:
                return "3";
            case Code.Ldc_I4_4:
                return "4";
            case Code.Ldc_I4_5:
                return "5";
            case Code.Ldc_I4_6:
                return "6";
            case Code.Ldc_I4_7:
                return "7";
            case Code.Ldc_I4_8:
                return "8";
            case Code.Ldc_I4_M1:
                return "-1";

            // Long integer constants
            case Code.Ldc_I8:
                return ((long)instruction.Operand).ToString();

            // Floating-point constants
            case Code.Ldc_R4:
                return ((float)instruction.Operand).ToString("R");
            case Code.Ldc_R8:
                return ((double)instruction.Operand).ToString("R");

            // String constants
            case Code.Ldstr:
                return EscapeForCode((string)instruction.Operand);

            // Default case for any unhandled operand
            default:
                var operand = instruction.Operand;
                if (operand == null)
                    return null;

                if (operand.GetType().IsPrimitive)
                    return operand.ToString();

                if (operand is string s)
                    return EscapeForCode(s);

                break;
        }

        return null;
    }

    private static void ReplaceCallWithBepIn(MethodDefinition method, LogRedirects redirects,
        Collection<Instruction> instructions,
        ref int index, MethodReference methodRef)
    {
        var instruction = instructions[index];
        MethodReference replacementMethod = null;
        var parametersCount = methodRef.Parameters.Count;

        switch (methodRef.Name)
        {
            case "Log":
                replacementMethod = parametersCount > 1 ? redirects.LOGInfoWithContextMethod : redirects.LOGInfoMethod;
                break;
            case "LogError":
                replacementMethod =
                    parametersCount > 1 ? redirects.LOGErrorWithContextMethod : redirects.LOGErrorMethod;
                break;
            case "LogWarning":
                replacementMethod = parametersCount > 1
                    ? redirects.LOGWarningWithContextMethod
                    : redirects.LOGWarningMethod;
                break;
            case "LogFormat":
                replacementMethod = parametersCount > 2
                    ? redirects.LOGInfoFormatWithContextMethod
                    : redirects.LOGInfoFormatMethod;
                break;
            case "LogErrorFormat":
                replacementMethod = parametersCount > 2
                    ? redirects.LOGErrorFormatWithContextMethod
                    : redirects.LOGErrorFormatMethod;
                break;
            case "LogWarningFormat":
                replacementMethod = parametersCount > 2
                    ? redirects.LOGWarningFormatWithContextMethod
                    : redirects.LOGWarningFormatMethod;
                break;
        }

        if (replacementMethod != null)
        {
            // Replace the original Debug method call with the wrapper method
            var ilProcessor = method.Body.GetILProcessor();

            ilProcessor.Replace(instruction, Instruction.Create(OpCodes.Call, replacementMethod));
        }
    }

    private static void ReplaceCallWithThrothledBepIn(MethodDefinition method, LogRedirects redirects,
        Collection<Instruction> instructions,
        ref int index, ref int startIndex, MethodReference methodRef, long logkey)
    {
        var instruction = instructions[index];
        MethodReference replacementMethod = null;
        var parametersCount = methodRef.Parameters.Count;

        switch (methodRef.Name)
        {
            case "Log":
                replacementMethod = parametersCount > 1 ? redirects.LOGInfoWithContextMethod : redirects.LOGInfoMethod;
                break;
            case "LogError":
                replacementMethod =
                    parametersCount > 1 ? redirects.LOGErrorWithContextMethod : redirects.LOGErrorMethod;
                break;
            case "LogWarning":
                replacementMethod = parametersCount > 1
                    ? redirects.LOGWarningWithContextMethod
                    : redirects.LOGWarningMethod;
                break;
            case "LogFormat":
                replacementMethod = parametersCount > 2
                    ? redirects.LOGInfoFormatWithContextMethod
                    : redirects.LOGInfoFormatMethod;
                break;
            case "LogErrorFormat":
                replacementMethod = parametersCount > 2
                    ? redirects.LOGErrorFormatWithContextMethod
                    : redirects.LOGErrorFormatMethod;
                break;
            case "LogWarningFormat":
                replacementMethod = parametersCount > 2
                    ? redirects.LOGWarningFormatWithContextMethod
                    : redirects.LOGWarningFormatMethod;
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

    private static string EscapeForCode(string str)
    {
        var sb = new StringBuilder();
        foreach (var c in str)
        {
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
                    {
                        sb.AppendFormat("\\u{0:X4}", (int)c);
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return "\"" + sb + "\"";
    }

    private struct StartIndex(int index, bool isStub = false, bool isDup = false)
    {
        internal readonly int Index = index;
        internal readonly bool IsStub = isStub;
        internal readonly bool IsDup = isDup;
    }

    private static string FormatArguments(this IEnumerable<string> list)
    {
        return $"({list.AsParameters()})";
    }
    
    private static string FormatArray(this IEnumerable<string> list)
    {
        return $"[{list.AsParameters()}]";
    }
    
    private static string AsParameters(this IEnumerable<string> list)
    {
        return string.Join(", ", list);
    }
    
    private static string AsChain(this IEnumerable<string> list)
    {
        return string.Join(".", list);
    }
}
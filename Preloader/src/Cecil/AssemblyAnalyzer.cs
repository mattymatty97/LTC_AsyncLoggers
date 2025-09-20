using System;
using System.Linq;
using AsyncLoggers.Cecil.Decompiler;
using AsyncLoggers.Config;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

namespace AsyncLoggers.Cecil;

internal static class AssemblyAnalyzer
{
    internal static void ProcessAssembly(AssemblyDefinition assembly, out long callCount)
    {
        callCount = 0;
        foreach (var moduleDefinition in assembly.Modules) ProcessModule(assembly, moduleDefinition, ref callCount);
    }

    private static void ProcessModule(AssemblyDefinition assembly, ModuleDefinition module, ref long callCount)
    {
        var normalRedirects = new LogRedirects(module, typeof(LogWrapper));
        var throttledRedirects = new LogRedirects(module, typeof(ThrothledLogWrapper));
        foreach (var typeDefinition in module.Types)
            ProcessType(assembly, typeDefinition, normalRedirects, throttledRedirects, ref callCount);
    }

    private static void ProcessType(AssemblyDefinition assembly, TypeDefinition type, LogRedirects normalRedirects,
        LogRedirects throttledRedirects, ref long callCount)
    {
        foreach (var methodDefinition in type.Methods)
            ProcessMethod(assembly, type, methodDefinition, normalRedirects, throttledRedirects, ref callCount);
    }

    private static void ProcessMethod(AssemblyDefinition assembly, TypeDefinition type, MethodDefinition method,
        LogRedirects normalRedirects, LogRedirects throttledRedirects, ref long callCount)
    {
        if (!method.HasBody)
            return;

        if (method.Name.Any(char.IsControl))
            return;

        var instructions = method.Body.Instructions;

        for (var i = 0; i < instructions.Count; i++)
        {
            var instruction = instructions[i];

            if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                if (instruction.Operand is MethodReference methodReference &&
                    methodReference.DeclaringType.FullName == "UnityEngine.Debug" &&
                    methodReference.Name.StartsWith("Log"))
                {
                    callCount++;
                    HandleLogCall(assembly, type, method, normalRedirects, throttledRedirects, ref instructions, ref i);
                }
        }
    }

    private static void HandleLogCall(AssemblyDefinition assembly, TypeDefinition type, MethodDefinition method
        , LogRedirects normalRedirects, LogRedirects throttledRedirects,
        ref Collection<Instruction> instructions, ref int index)
    {
        var instruction = instructions[index];

        var typeName = type.FullName;

        var methodName = method.Name;

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Info, () =>
            $"Method {typeName}:{methodName} has Log call at IL_{instruction.Offset:x4}.");

        try
        {
            var line = ICodeLine.ParseInstruction(method, instruction);

            var startIndex = instructions.IndexOf(line.StartInstruction);

            var logline = line.ToString(true);

            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Info,
                () => $"Method {typeName}:{methodName} has Log named \"{logline}\"");

            var collection1 = instructions;
            var lenght1 = index - startIndex + 1;
            var index1 = startIndex;
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                () => $"Found span:\n{string.Join("\n", collection1.Skip(index1).Take(lenght1))}");

            var config = XmlConfig.LogCallInfo.GetOrAdd(assembly.Name.Name, type.FullName, method.Name, logline);

            switch (config.Status)
            {
                case XmlConfig.CallStatus.Suppressed:
                {
                    AsyncLoggers.VerboseLogWrappingLog(LogLevel.Warning, () =>
                        $"Suppressing \"{logline}\" from {config.ClassName}:{config.MethodName}");
                    var args = ((MethodReference)instruction.Operand).Parameters.Count;
                    instructions[index] = Instruction.Create(OpCodes.Pop);
                    for (var i = 1; i < args; i++) instructions.Insert(index, Instruction.Create(OpCodes.Pop));

                    break;
                }
                case XmlConfig.CallStatus.BepInEx:
                {
                    var callReference = instruction.Operand as MethodReference;
                    if (!line.IsIncomplete && config.Cooldown.HasValue)
                    {
                        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Warning, () =>
                            $"Throttling \"{logline}\" from {config.ClassName}:{config.MethodName}");
                        var logKey = ThrothledLogWrapper.NextID;
                        ThrothledLogWrapper.CooldownMemory[logKey] = TimeSpan.FromMilliseconds(config.Cooldown.Value);
                        ReplaceCallWithThrothledBepIn(method, throttledRedirects, instructions, ref index,
                            ref startIndex,
                            callReference, logKey);

                    }
                    else
                    {
                        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Warning, () =>
                            $"Wrapping \"{logline}\" from {config.ClassName}:{config.MethodName}");
                        ReplaceCallWithBepIn(method, normalRedirects, instructions, ref index, callReference);
                    }

                    break;
                }
                case XmlConfig.CallStatus.Unity:
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(config.Status));
            }

            var collection2 = instructions;
            var lenght2 = index - startIndex + 1;
            var index2 = startIndex;
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug, () =>
                startIndex > 0
                    ? $"Modified span:\n{string.Join("\n", collection2.Skip(index2).Take(lenght2))}"
                    : $"No span found: index is now {startIndex}");
        }
        catch (Exception ex)
        {
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Error, () => ex.ToString());
        }
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

    private class LogRedirects
    {
        private readonly ModuleDefinition _moduleDefinition;
        private readonly Type _redirectType;
        private MethodReference _logErrorFormatMethod;
        private MethodReference _logErrorFormatWithContextMethod;
        private MethodReference _logErrorMethod;
        private MethodReference _logErrorWithContextMethod;

        private MethodReference _logInfoFormatMethod;

        private MethodReference _logInfoFormatWithContextMethod;

        private MethodReference _logInfoMethod;

        private MethodReference _logInfoWithContextMethod;
        private MethodReference _logWarningFormatMethod;
        private MethodReference _logWarningFormatWithContextMethod;
        private MethodReference _logWarningMethod;
        private MethodReference _logWarningWithContextMethod;

        internal LogRedirects(ModuleDefinition moduleDefinition, Type redirectType)
        {
            _moduleDefinition = moduleDefinition;
            _redirectType = redirectType;
        }

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
    }
}

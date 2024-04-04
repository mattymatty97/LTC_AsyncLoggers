using System.Collections.Generic;
using System.Linq;
using AsyncLoggers.Wrappers;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace AsyncLoggers.Cecil
{
    internal static class UnityLogsCecilPatch
    {
        internal static void PatchUnityLogs(AssemblyDefinition assembly, TypeDefinition type)
        {
            if (PluginConfig.Unity.Enabled.Value)
            {
                foreach (MethodDefinition method in type.Methods)
                {
                    if (method.Name == ".cctor")
                    {
                        var loggerConstructors = assembly.MainModule.GetType("UnityEngine", "Logger").GetConstructors();
                        var logHandlerConstructors = assembly.MainModule.GetType("UnityEngine", "DebugLogHandler")
                            .GetConstructors();

                        var asyncLoggerConstructor =
                            typeof(ProxyClass).GetMethod(nameof(ProxyClass.WrapUnityLogger));
                        var asyncLogHandlerConstructor =
                            typeof(ProxyClass).GetMethod(nameof(ProxyClass.WrapUnityLogHandler));
                        MethodReference asyncConstructor;
                        IEnumerable<MethodReference> ogConstructors;

                        if (PluginConfig.Unity.Wrapper.Value == PluginConfig.UnityWrapperType.Logger)
                        {
                            asyncConstructor = assembly.MainModule.ImportReference(asyncLoggerConstructor);
                            ogConstructors = loggerConstructors;
                        }
                        else
                        {
                            asyncConstructor = assembly.MainModule.ImportReference(asyncLogHandlerConstructor);
                            ogConstructors = logHandlerConstructors;
                        }

                        ILProcessor processor = method.Body.GetILProcessor();

                        Instruction newAsyncLine = processor.Create(OpCodes.Call, asyncConstructor);

                        for (var i = 0; i < method.Body.Instructions.Count; i++)
                        {
                            var curr = method.Body.Instructions[i];

                            if (curr.OpCode == OpCodes.Newobj && ogConstructors.Contains(curr.Operand))
                            {
                                processor.InsertAfter(curr, newAsyncLine);
                                i++;
                                AsyncLoggers.Log.LogDebug("Forcing Unity LogHandler to Async!!");
                            }
                        }
                    }
                }
            }
        }
    }
}
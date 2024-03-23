using System.Linq;
using AsyncLoggers.Wrappers;
using AsyncLoggers.Wrappers.BepInEx;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace AsyncLoggers.Cecil
{
    internal static class BepInExChainloaderCecilPatch
    {
        internal static void PatchBepInExChainloader(AssemblyDefinition assembly, TypeDefinition type)
        {
            foreach (MethodDefinition method in type.Methods)
            {
                if (method.Name == "Initialize")
                {
                    var consoleConstructors = assembly.MainModule.GetType("BepInEx.Logging", "ConsoleLogListener").GetConstructors();
                    var diskConstructors = assembly.MainModule.GetType("BepInEx.Logging", "DiskLogListener").GetConstructors();
                    var unityConstructors = assembly.MainModule.GetType("BepInEx.Logging", "UnityLogListener").GetConstructors();

                    var asyncWrapperConstructor = typeof(WrapperWrapper).GetMethod(nameof(WrapperWrapper.WrapBepInExLogListener));
                    MethodReference asyncConstructor = assembly.MainModule.ImportReference(asyncWrapperConstructor);
                    
                    ILProcessor processor = method.Body.GetILProcessor();

                    Instruction newAsyncLine = processor.Create(OpCodes.Call, asyncConstructor);
                    
                    for (var i = 0; i < method.Body.Instructions.Count; i++)
                    {
                        var curr = method.Body.Instructions[i];
                        
                        if (PluginConfig.BepInEx.Console.Value && curr.OpCode == OpCodes.Newobj && consoleConstructors.Contains(curr.Operand))
                        {
                            processor.InsertAfter(curr, newAsyncLine);
                            i++;
                            AsyncLoggerPreloader.Log.LogDebug("Forcing BepInEx Console to Async!!");
                        }                        
                        
                        if (PluginConfig.BepInEx.Disk.Value && curr.OpCode == OpCodes.Newobj && diskConstructors.Contains(curr.Operand))
                        {
                            processor.InsertAfter(curr, newAsyncLine);
                            i++;
                            AsyncLoggerPreloader.Log.LogDebug("Forcing BepInEx Disk Writer to Async!!");
                        }                        
                        
                        if (PluginConfig.BepInEx.Unity.Value && curr.OpCode == OpCodes.Newobj && unityConstructors.Contains(curr.Operand))
                        {
                            processor.InsertAfter(curr, newAsyncLine);
                            i++;
                            AsyncLoggerPreloader.Log.LogDebug("Forcing BepInEx Unity Writer to Async!!");
                        }
                    }
                }
            }
        }
        
        internal static void PatchBepInExUnityLogSource(AssemblyDefinition assembly, TypeDefinition type)
        {
            foreach (MethodDefinition method in type.Methods)
            {
                if (method.Name == ".cctor")
                {

                    var original = "logMessageReceived";
                    var replacement = "logMessageReceivedThreaded";
                    
                    ILProcessor processor = method.Body.GetILProcessor();

                    Instruction newLdStr = processor.Create(OpCodes.Ldstr, replacement);
                    
                    for (var i = 0; i < method.Body.Instructions.Count; i++)
                    {
                        var curr = method.Body.Instructions[i];
                        
                        if (curr.OpCode == OpCodes.Ldstr && (string)curr.Operand == original)
                        {
                            processor.Replace(curr, newLdStr);
                            AsyncLoggerPreloader.Log.LogDebug("using logMessageReceivedThreaded instead of logMessageReceived!!");
                        }
                    }
                }
            }
        }
    }
}
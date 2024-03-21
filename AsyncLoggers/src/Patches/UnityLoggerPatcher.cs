using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using AsyncLoggers.Wrappers.Unity;
using HarmonyLib;
using UnityEngine;

namespace AsyncLoggers.Patches
{
    [HarmonyPatch]
    internal class UnityLoggerPatcher
    {
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(Debug), ".cctor", MethodType.StaticConstructor)]
        private static IEnumerable<CodeInstruction> UseAsyncLogListeners(IEnumerable<CodeInstruction> instructions)
        {
            if (!PluginConfig.Unity.Enabled.Value)
                return instructions;
            
            var codes = instructions.ToList();
            
            var loggerConstructors = typeof(Logger).GetConstructors();
            var logHandlerConstructors = typeof(DebugLogHandler).GetConstructors();
            
            var asyncLoggerConstructors = typeof(AsyncLoggerWrapper).GetConstructors();
            var asyncLogHandlerConstructors = typeof(AsyncLogHandlerWrapper).GetConstructors();
            
            
            for (var i = 0; i < codes.Count; i++)
            {
                var curr = codes[i];

                if (PluginConfig.Unity.Wrapper.Value == PluginConfig.UnityWrapperType.Logger && curr.opcode == OpCodes.Newobj && loggerConstructors.Contains(curr.operand))
                {
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Newobj, asyncLoggerConstructors[0])
                    {
                       blocks = curr.blocks,
                    });
                    i++;
                    AsyncLoggerPreloader.Log.LogDebug("Forcing Unity Logger to Async!!");
                }
                
                if (PluginConfig.Unity.Wrapper.Value == PluginConfig.UnityWrapperType.LogHandler && curr.opcode == OpCodes.Newobj && logHandlerConstructors.Contains(curr.operand))
                {
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Newobj, asyncLogHandlerConstructors[0])
                    {
                       blocks = curr.blocks,
                    });
                    i++;
                    AsyncLoggerPreloader.Log.LogDebug("Forcing Unity LogHandler to Async!!");
                }
            }
            return codes;
        }
    }
}
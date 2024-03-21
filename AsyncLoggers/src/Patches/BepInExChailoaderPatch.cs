using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using AsyncLoggers.Wrappers.BepInEx;
using AsyncLoggers.Wrappers.Unity;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace AsyncLoggers.Patches
{
    [HarmonyPatch]
    internal class BepInExChailoaderPatch
    {/*
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize))]
        private static IEnumerable<CodeInstruction> UseAsyncLogListeners(IEnumerable<CodeInstruction> instructions)
        {
            if (!PluginConfig.BepInEx.Enabled.Value)
                return instructions;

            var codes = instructions.ToList();

            var consoleConstructors = typeof(ConsoleLogListener).GetConstructors();
            var unityConstructors = typeof(UnityLogListener).GetConstructors();
            var diskConstructors = typeof(DiskLogListener).GetConstructors();


            var asyncConstructors = typeof(AsyncLogListenerWrapper).GetConstructors();

            for (var i = 0; i < codes.Count; i++)
            {
                var curr = codes[i];

                if (PluginConfig.BepInEx.Console.Value && curr.opcode == OpCodes.Newobj &&
                    consoleConstructors.Contains(curr.operand))
                {
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Newobj, asyncConstructors[0])
                    {
                        blocks = curr.blocks,
                    });
                    i++;
                    AsyncLoggerPreloader.Log.LogDebug("Converted ConsoleLogListener to Async!!");
                }

                if (PluginConfig.BepInEx.Unity.Value && curr.opcode == OpCodes.Newobj &&
                    unityConstructors.Contains(curr.operand))
                {
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Newobj, asyncConstructors[0])
                    {
                        blocks = curr.blocks,
                    });
                    i++;
                    AsyncLoggerPreloader.Log.LogDebug("Converted UnityLogListener to Async!!");
                }

                if (PluginConfig.BepInEx.Disk.Value && curr.opcode == OpCodes.Newobj &&
                    diskConstructors.Contains(curr.operand))
                {
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Newobj, asyncConstructors[0])
                    {
                        blocks = curr.blocks,
                    });
                    i++;
                    AsyncLoggerPreloader.Log.LogDebug("Converted DiskLogListener to Async!!");
                }
            }

            return codes;
        }*/


        [HarmonyPostfix]
        [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize))]
        private static void UseAsyncLogListeners()
        {
            AsyncLoggerPreloader.Log.LogInfo(
                "using logMessageReceivedThreaded instead of logMessageReceived for UnityLogSource!!");
            Application.LogCallback handler = UnityLogSource.OnUnityLogMessageReceived;
            EventInfo eventInfo =
                typeof(Application).GetEvent("logMessageReceived", BindingFlags.Static | BindingFlags.Public);
            if (eventInfo != null)
                eventInfo.RemoveEventHandler(null, handler);

            eventInfo = typeof(Application).GetEvent("logMessageReceivedThreaded",
                BindingFlags.Static | BindingFlags.Public);
            if (eventInfo != null)
                eventInfo.AddEventHandler(null, handler);
            
            if (PluginConfig.Unity.Enabled.Value)
            {
                AsyncLoggerPreloader.Log.LogWarning("Converting unity logger to async!!");
                switch (PluginConfig.Unity.Wrapper.Value)
                {
                    case PluginConfig.UnityWrapperType.LogHandler:
                        Debug.s_Logger.logHandler = new AsyncLogHandlerWrapper(Debug.s_Logger.logHandler);
                        Debug.s_DefaultLogger.logHandler = new AsyncLogHandlerWrapper(Debug.s_DefaultLogger.logHandler);
                        break;
                    case PluginConfig.UnityWrapperType.Logger:
                    {
                        Debug.s_Logger = new AsyncLoggerWrapper(Debug.s_Logger);
                        FieldInfo fieldInfo = typeof(Debug).GetField(nameof(Debug.s_DefaultLogger),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        fieldInfo?.SetValue(null, new AsyncLoggerWrapper(Debug.s_DefaultLogger));
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (PluginConfig.BepInEx.Enabled.Value)
            {
                AsyncLoggerPreloader.Log.LogWarning("Converting BepInEx loggers to async!!");
                var collection = BepInEx.Logging.Logger.Listeners;
                var list = collection.ToList();
                foreach (var logger in list)
                {
                    var isConsole = logger is ConsoleLogListener;
                    var isUnity = logger is UnityLogListener;
                    var isDisk = logger is DiskLogListener;

                    if (!isConsole && !isDisk && !isUnity)
                        continue;
                    if (isConsole && !PluginConfig.BepInEx.Console.Value)
                        continue;
                    if (isUnity && !PluginConfig.BepInEx.Unity.Value)
                        continue;
                    if (isDisk && !PluginConfig.BepInEx.Disk.Value)
                        continue;

                    AsyncLoggerPreloader.Log.LogWarning($"{logger.GetType().Name} Converted");
                    collection.Remove(logger);
                    collection.Add(new AsyncLogListenerWrapper(logger));
                }
            }
        }
    }
}
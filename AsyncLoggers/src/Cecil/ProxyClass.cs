using System.Runtime.CompilerServices;
using AsyncLoggers.Wrappers.Unity;
using BepInEx.Logging;
using UnityEngine;

namespace AsyncLoggers.Cecil
{
    internal class ProxyClass
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static object WrapUnityLogger(object _base)
        {
            return new AsyncLoggerWrapper((ILogger)_base);
        }
        
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static object WrapUnityLogHandler(object _base)
        {
            return new AsyncLogHandlerWrapper((ILogHandler)_base);
        }
        
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void AppendQuittingCallback()
        {
            Application.quitting += AsyncLoggerPreloader.OnApplicationQuit;
        }
        
    }
}
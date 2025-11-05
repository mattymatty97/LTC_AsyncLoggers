using System.Runtime.CompilerServices;
using UnityEngine;

namespace AsyncLoggers.Proxy;

internal static class ProxyClass
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void AppendQuittingCallback()
    {
        Application.quitting += AsyncLoggers.OnApplicationQuit;
    }
    
}
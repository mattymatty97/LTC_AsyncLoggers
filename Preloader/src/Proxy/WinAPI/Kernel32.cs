using System;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace AsyncLoggers.Proxy.WinAPI;

public static class Kernel32
{
    public const string DllName = "kernel32.dll";
    
    [DllImport(DllName, EntryPoint = "LoadLibrary", SetLastError = true)]
    private static extern IntPtr LoadLibrary(string dllToLoad);
    
    [DllImport(DllName, EntryPoint = "FreeLibrary", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);
    
    [DllImport(DllName, EntryPoint = "GetProcAddress", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetFunctionAddress(IntPtr hModule, string funcName);

    
    public class NativeLibrary : IDisposable
    {
        public IntPtr Address { get; private set; }
        public string Name { get; }
        public bool IsLoaded => Address != IntPtr.Zero;
        public int LastError { get; private set; }
        
        
        public NativeLibrary(string dllToLoad)
        {
            Name = dllToLoad;
            Address = LoadLibrary(dllToLoad);
            if (Address == IntPtr.Zero)
                throw new Win32Exception($"Could not load library {dllToLoad}");
        }

        public bool TryGetExportedFunctionOffset(string funcName, out ulong offset)
        {
            var ret = TryGetExportedFunctionAddress(funcName, out var address);
            offset = ret ? address - (ulong)Address : 0;
            return ret;
        }
        
        public bool TryGetExportedFunctionAddress(string funcName, out ulong address)
        {
            if (!IsLoaded)
                throw new DllNotFoundException($"{Name} is not loaded");
            
            address = (ulong)GetFunctionAddress(Address, funcName);
            if (address == 0)
            {
                LastError = Marshal.GetLastWin32Error();
                return false;
            }
            
            return true;
        }
        
        public void Dispose()
        {
            if (Address != IntPtr.Zero)
            {
                FreeLibrary(Address);
                Address = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }

        ~NativeLibrary() => Dispose();
    }
    
    public struct NativeFunction<T> where T : class
    {
        public NativeLibrary Library { get; }
        
        public string Name { get; }
        
        public IntPtr Address { get; }
        
        
        public ulong Offset => (ulong)Address.ToInt64() - (ulong)Library.Address.ToInt64();
        
        
        private T _delegate = null;
        
        public T Delegate
        {
            get
            {
                if (_delegate == null)
                {
                    _delegate = Marshal.GetDelegateForFunctionPointer<T>(Address);
                }

                return _delegate;
            }
        }

        private T _safeDelegate = null;
        
        public T SafeDelegate
        {
            get
            {
                if (_safeDelegate == null)
                {
                    var lib = Expression.Constant(Library);
                    var isLoadedProp = Expression.Property(lib, nameof(NativeLibrary.IsLoaded));

                    var invoke = typeof(T).GetMethod("Invoke")!;
                    var parameters = invoke.GetParameters()
                        .Select(p => Expression.Parameter(p.ParameterType, p.Name))
                        .ToArray();
        
                    var args = parameters.Cast<Expression>();

                    var body = Expression.Block(
                        Expression.IfThen(
                            Expression.IsFalse(isLoadedProp),
                            Expression.Throw(Expression.New(
                                typeof(ObjectDisposedException).GetConstructor([typeof(string)])!,
                                Expression.Constant($"NativeLibrary({Name})")))
                        ),
                        Expression.Invoke(Expression.Constant(Delegate), args)
                    );
            
                    _safeDelegate = Expression.Lambda<T>(body, parameters).Compile();
                }

                return _safeDelegate;
            }
        }

        public NativeFunction(NativeLibrary library,string functionName,  IntPtr address)
        {
            Library = library;
            Name = functionName;
            Address = address;
            
        }
        
        public NativeFunction(NativeLibrary library, string functionName, ulong offset)
        {
            Library = library;
            Name = functionName;
            Address = (IntPtr)((ulong)library.Address.ToInt64() + offset);
        }
    }
}
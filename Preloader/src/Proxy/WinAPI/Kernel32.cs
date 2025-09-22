using System;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace AsyncLoggers.Proxy.WinAPI;

public static class Kernel32
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibrary(string dllToLoad);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);
    
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    
    public class NativeLibrary : IDisposable
    {
        private IntPtr _handle;
        public string Name { get; }
        public bool IsLoaded => _handle != IntPtr.Zero;
        public int LastError { get; private set; }
        
        public NativeLibrary(string dllToLoad)
        {
            Name = dllToLoad;
            _handle = LoadLibrary(dllToLoad);
            if (_handle == IntPtr.Zero)
                LastError = Marshal.GetLastWin32Error();
        }

        public T GetRawDelegate<T>(string procName) where T : Delegate
        {
            if (!IsLoaded)
                throw new DllNotFoundException($"{Name} is not loaded");
            
            var procAddress = GetProcAddress(_handle, procName);
            
            if (procAddress == IntPtr.Zero)
            {
                LastError = Marshal.GetLastWin32Error();
                return null;
            }
            
            return Marshal.GetDelegateForFunctionPointer<T>(procAddress);
        }
        
        public T GetDelegate<T>(string procName) where T : Delegate
        {
            var fn = GetRawDelegate<T>(procName);
            var lib = Expression.Constant(this);
            var isLoadedProp = Expression.Property(lib, nameof(IsLoaded));

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
                Expression.Invoke(Expression.Constant(fn), args)
            );

            return Expression.Lambda<T>(body, parameters).Compile();
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                FreeLibrary(_handle);
                _handle = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }

        ~NativeLibrary() => Dispose();
    }
}
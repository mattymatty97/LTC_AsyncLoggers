Async Loggers
============
[![GitHub Release](https://img.shields.io/github/v/release/mattymatty97/LTC_AsyncLoggers?display_name=release&logo=github&logoColor=white)](https://github.com/mattymatty97/LTC_AsyncLoggers/releases/latest)
[![GitHub Pre-Release](https://img.shields.io/github/v/release/mattymatty97/LTC_AsyncLoggers?include_prereleases&display_name=release&logo=github&logoColor=white&label=preview)](https://github.com/mattymatty97/LTC_AsyncLoggers/releases)  
[![Thunderstore Downloads](https://img.shields.io/thunderstore/dt/mattymatty/AsyncLoggers?style=flat&logo=thunderstore&logoColor=white&label=thunderstore)](https://thunderstore.io/c/lethal-company/p/mattymatty/AsyncLoggers/)

### YEET the logs to their own thread!

# For the users:

### Async Logs:
remove any log related lag by processing them separately from the game stuff.
the more logs your modpack generates the bigger the impact this mod has!

### Log Level filter:
Reduce logs from mods by specifying a LogLevel for each LogSource

### Unity Log wrapping:
Detect calls to `Unity.Debug` inside the game assemblies and allow users to tweak them  
Note: Has to be enabled in the config `LogWrapping.Enabled`

# For the Developers:
the class `AsyncLoggers.API.AsyncLoggersAPI` contains a method to register your own `ILogListener` into AsyncLoggers system:
```c#
public static void UpdateListenerFlags(ILogListener target, LogListenerFlags flags)
```
possible flags are:
- `SyncHandling`  
This listener will receive logs directly, without any buffering or delay.
- `IgnoreFilters`  
This listener will receive all logs, bypassing any filters that might be applied.
- `AddTimeStamp`  
This listener will have a timestamp prepended to the log messages.
- `None`

additionally the class exposes two properties:
- `APIVersion`  
The current version of the API, represented as a [Version](https://learn.microsoft.com/en-us/dotnet/api/system.version).
- `TraceableLevelsMask`  
a flag enum representing what [LogLevel](https://docs.bepinex.dev/api/BepInEx.Logging.LogLevel.html) will make AsyncLoggers block to collect stacktrace.
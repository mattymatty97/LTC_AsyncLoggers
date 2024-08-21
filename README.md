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
the main class `AsyncLoggers.AsyncLoggers` contains 4 methods to register your own `LogListener` into AsyncLoggers system:
- SyncListener (listeners will receive logs directly)
```c#
public static bool RegisterSyncListener(ILogListener listener)
public static bool UnRegisterSyncListener(ILogListener listener)
```
- UnfilteredListener (listeners will bypass the user defined filters and receive all logs)
```c#
public static bool RegisterUnfilteredListener(ILogListener listener)
public static bool UnRegisterUnfilteredListener(ILogListener listener)
```
- TimestampedListener (listeners will have the TimeStamp prepended to the LogEventArgs Data)
```c#
public static bool RegisterTimestampedListener(ILogListener listener)
public static bool UnRegisterTimestampedListener(ILogListener listener)
```

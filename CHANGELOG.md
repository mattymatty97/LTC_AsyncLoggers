# v2.0.3
- ensure assemblies in the LogWrapping pass are loaded in dependency order

# v2.0.2
- small optimizations to wrapping system

# v2.0.1
- fix wrong defaults

# v2.0.0
- remove DBAPI
- add some api to handle custom ILogListeners
- remove patches to unity loggers
- embed AsyncLoggers.Filter
- immediately defer logs with a global Dispatcher Thread
- add LogWrapping system to edit basegame Log calls

---

# v1.6.3
- generate LogEvent timestamp only once

# v1.6.2
- fix Database rolling failing after 1 cycle
- fix NRE during app closing if Database is disabled
- add more try/catches to prevent unwanted exceptions from propagating

# v1.6.1
- fix errors during shutdown

# v1.6.0
- added Mods table to SqliteDb
- added Events table to SqliteDb
- added ModData table to SqliteDb
- new API for mods to write custom events and or data to sqliteDb ( intended use only for debug not as storage )

# v1.5.1
- cleanup and minor bugs caused by forgotten debug lines

# v1.5.0
- add incremental counter as TimeStamp option
- add option to write a Sqlite DB for collecting logs
- improve performance of TimeStamps
- Disable StackTraces by default

# v1.4.0
- add Timestamps to BepInEx logs
- removed AsyncLogger from exception stackTraces (BepInEx only)

# v1.3.0
- make the mod a PrePatcher ( meaning will load before everybody else )
- allow for multiple IJobs ( if somebody wants to have BepInEx use Jobs too )
- add LobbyCompatibility softDependency

# v1.2.7
- use non-unsigned values to calculate the wrapPoint ( make the patch actually work )

# v1.2.6
- dispose of BepInEx loggers when wrapper is disposed
- use Application.quitting instead of Application.wantsToQuit

# v1.2.5
- apply circular buffer logic and overwrite older logs

# v1.2.4
- avoid a cast at startup

# v1.2.3
- change threads to not background

# v1.2.2
- small improvements
- add config to decide the Shutdown Action

# v1.2.1
- use a size-limited RingBuffer instead of indefinitely growing buffer
- add config options for buffer sizes

# v1.2.0
- added Scheduler type selection

# v1.1.0
- Improved performance
- Added Error handling of logs
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
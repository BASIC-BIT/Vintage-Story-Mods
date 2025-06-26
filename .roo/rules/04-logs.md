# Logs

## Client

Local client logs can be found at `C:\Users\steve\AppData\Roaming\VintagestoryData\Logs`.  Use these when requested or after telling the user to launch the game to test, in the next turn.

## Server

Server logs can be fetched from the remote server using the `fetch-logs.ps1` script in `mods-dll/thebasics/scripts/`.

### Usage
```powershell
# Fetch all logs from last 7 days (default)
.\mods-dll\thebasics\scripts\fetch-logs.ps1

# Fetch only server logs from last 3 days
.\mods-dll\thebasics\scripts\fetch-logs.ps1 -LogType server -Days 3

# Fetch crash reports only
.\mods-dll\thebasics\scripts\fetch-logs.ps1 -LogType crash

# Fetch debug logs to custom directory
.\mods-dll\thebasics\scripts\fetch-logs.ps1 -LogType debug -OutputDir "C:\temp\logs"
```

### Configuration
Uses the same `.env` file as the package script with SFTP credentials.

### Local Storage
Downloaded logs are stored in `logs/` directory with timestamped subdirectories for easy organization.

### Log Types
- `server` - Main server logs (server-main.log, server-event.log)
- `debug` - Debug logs (server-debug.log)
- `crash` - Crash reports from /data/CrashReports/
- `mod` - Mod-specific logs (including thebasics logs)
- `all` - All available logs (default)

### Remote Log Locations
- Server logs: `/data/Logs/server-main.log`, `/data/Logs/server-audit.log`, `/data/Logs/server-chat.log`, `/data/Logs/server-build.log`, `/data/Logs/server-worldgen.log`
- Debug logs: `/data/Logs/server-debug.log`
- Mod logs: `/data/Logs/*mod*.txt`, `/data/Logs/*thebasics*.txt`
- Crash reports: `/data/CrashReports/`
- Archive logs: `/data/Logs/Archive/` (historical logs with timestamps)

### Verified Working
- ✅ Successfully tested and downloads 6+ server log files
- ✅ SFTP connection to production server working
- ✅ Timestamped local storage in `logs/` directory
- ✅ Handles archived logs and missing directories gracefully



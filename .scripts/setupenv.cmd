@echo off
set HOMEDRIVE=X:
set HOMEPATH=\
set DOTNET6BASE=X:\OPT\DOTNET6.0
set PATH=%DOTNET6BASE%;%PATH%
cd /D X:\DEV
rem opt-out of telemetry by setting the DOTNET_CLI_TELEMETRY_OPTOUT environment variable to '1' or 'true'
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set DOTNET_ROOT=X:\opt\dotnet6.0
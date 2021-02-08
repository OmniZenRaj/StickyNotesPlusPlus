@echo off
set HOMEDRIVE=I:
set HOMEPATH=\
set DOTNET3BASE=X:\OPT\DOTNET3.1
set PATH=%DOTNET3BASE%;%PATH%
cd /D X:\DEV
rem opt-out of telemetry by setting the DOTNET_CLI_TELEMETRY_OPTOUT environment variable to '1' or 'true'
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set DOTNET_ROOT=X:\opt\dotnet3.1
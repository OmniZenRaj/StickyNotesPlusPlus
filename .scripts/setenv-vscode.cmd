@echo off
call %~dp0\setupenv.cmd

echo NODEJS Environment setup
set NODEJSBASE=X:\OPT\NODEJS

rem Ensure this Node.js and npm are first in the PATH
set PATH=%NODEJSBASE%;%PATH%
setlocal enabledelayedexpansion
pushd "%NODEJSBASE%"

rem Figure out the Node.js version.
set print_version=.\node.exe -p -e "process.versions.node + ' (' + process.arch + ')'"
for /F "usebackq delims=" %%v in (`%print_version%`) do set version=%%v

rem Print message.
if exist npm.cmd (
  echo Your environment has been set up for using Node.js !version! and npm.
) else (
  echo Your environment has been set up for using Node.js !version!.
)
popd
endlocal

rem Setup GIT in the PATH
set PATH=X:\OPT\GIT\BIN;%PATH%

rem Workaround for OmniSharp bug ! NEEDED to work with OmniSharp and DOTNET 3.1
set MSBuildSDKsPath=X:\OPT\dotnet3.1\sdk\3.1.100\Sdks
echo MSBuildSDKsPath workaround for OmniSharp SDKs set

dotnet --info
echo DOTNET3.1 Environment setup with BASE as %DOTNET3BASE%

echo --------------------------------------------------------------------------------------------------
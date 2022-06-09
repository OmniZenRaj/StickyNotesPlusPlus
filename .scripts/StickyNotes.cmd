@echo off
rem Update latest version of StickyNotes
set STICKYNOTESFOLDER="S:\Metal Products\PROGRAMS\StickyNotes"
IF EXIST %STICKYNOTESFOLDER% robocopy %STICKYNOTESFOLDER% "%~dp0\" /MIR /NJH /NJS /NFL /NDL
set DOTNET6BASE=%~dp0\runtimes\
set PATH=%DOTNET6BASE%;%PATH%
set DOTNET_ROOT=%DOTNET6BASE%
cd /D %~dp0\
start OmniZenNotes.exe
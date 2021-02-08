@echo off
title Clean and Rebuild Project Binaries
echo --------------------------------------------------------------------------------------------------
echo CLEANING  %DATE%:%TIME% Development Project

echo Setup the DOTNET VSCode environment by running:
call %~dp0\setenv-vscode.cmd

cd /D %~dp0\..\

dotnet clean
rd obj /s /q
rd bin /s /q
dotnet build
if %ERRORLEVEL% GEQ 1 goto PUBLISH_ERROR

echo SUCCESS %DATE%:%TIME% CLEAN and REBUILD completed OK
echo --------------------------------------------------------------------------------------------------
goto :EOF

:PUBLISH_ERROR 
echo --------------------------------------------------------------------------------------------------
echo ERROR %DATE%:%TIME% CLEAN and REBUILD Problems encounterred
echo Check for error messages in the previous steps and review run book documentation
echo --------------------------------------------------------------------------------------------------
pause

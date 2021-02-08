@echo off
call %~dp0\setenv-vscode.cmd

echo Starting VSCODE for DOTNET 3.1, NODEJS 10 and GIT
cmd /c "start X:\opt\vscode\code.exe"
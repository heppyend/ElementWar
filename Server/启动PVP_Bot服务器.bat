@echo off
cd /d "%~dp0"
dotnet run --project "ElementWarServer.csproj" -- --bots
pause

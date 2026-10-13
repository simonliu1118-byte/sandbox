@echo off
setlocal
cd /d "%~dp0"
title Timecard OCR Portable
where powershell.exe >nul 2>nul
if errorlevel 1 (
  echo Windows PowerShell was not found.
  pause
  exit /b 1
)
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Sta -File "%~dp0server.ps1"
if errorlevel 1 (
  echo.
  echo Failed to start. Please capture this screen and send it for troubleshooting.
  pause
)

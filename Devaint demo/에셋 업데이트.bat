@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\assets\start_assets.ps1" %*
set "asset_exit=%errorlevel%"
echo.
pause
exit /b %asset_exit%

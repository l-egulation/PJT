@echo off
rem Starts the Vite dev server for the web client on http://localhost:5173.
setlocal
cd /d "%~dp0..\.."
echo.
echo === hanjjak web ===
echo.
call pnpm install
if errorlevel 1 goto failed
call pnpm --filter @hanjjak/web dev
echo.
echo === web stopped (exit code %ERRORLEVEL%) ===
pause
exit /b 0

:failed
echo.
echo pnpm install failed. Check that Node 24+ and pnpm 10 are installed.
pause
exit /b 1

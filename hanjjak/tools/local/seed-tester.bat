@echo off
rem Raises an existing account to the beta test build. Sign up in the app first.
rem Usage: seed-tester.bat you@example.com    (or double-click and type the email)
setlocal
cd /d "%~dp0..\.."
set COMPOSE=infra\local\compose.yaml
set SEED=%~dp0seed-beta-tester.sql

set EMAIL=%~1
if "%EMAIL%"=="" set /p EMAIL=Account email you signed up with:
if "%EMAIL%"=="" (
    echo No email given.
    pause
    exit /b 1
)

rem psql on PATH wins; otherwise look in the standard installer locations.
set "PSQL="
where psql >nul 2>nul
if not errorlevel 1 set "PSQL=psql"
if not defined PSQL for /d %%d in ("C:\Program Files\PostgreSQL\*") do (
    if exist "%%d\bin\psql.exe" set "PSQL=%%d\bin\psql.exe"
)
if not defined PSQL for /d %%d in ("C:\Program Files (x86)\PostgreSQL\*") do (
    if exist "%%d\bin\psql.exe" set "PSQL=%%d\bin\psql.exe"
)

if defined PSQL goto runpsql

echo psql not found on PATH or under Program Files\PostgreSQL.
echo Falling back to the postgres container (needs Docker Desktop running).
docker compose -f "%COMPOSE%" exec -T postgres psql -U hanjjak -d hanjjak -v email=%EMAIL% < "%SEED%"
goto done

:runpsql
echo Using: %PSQL%
set PGPASSWORD=local-only
"%PSQL%" -h localhost -U hanjjak -d hanjjak -v email=%EMAIL% -f "%SEED%"

:done
echo.
echo If a summary row printed above, refresh the browser tab.
echo If it says "no account found", sign up in the app first.
pause

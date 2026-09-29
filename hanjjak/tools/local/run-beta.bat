@echo off
rem One-click local beta stack: PostgreSQL, game API on the beta profile, web client.
rem Double-click this file. Two extra windows open and stay open.
setlocal
cd /d "%~dp0..\.."
set COMPOSE=infra\local\compose.yaml

echo === 1/3 PostgreSQL ===
where docker >nul 2>nul
if errorlevel 1 (
    echo docker not found on PATH.
    echo Start PostgreSQL yourself on localhost:5432 with database/user/password hanjjak/hanjjak/local-only,
    echo then run run-api-beta.bat and run-web.bat directly.
    pause
    exit /b 1
)
docker compose -f "%COMPOSE%" up -d
if errorlevel 1 (
    echo Failed to start the postgres container. Is Docker Desktop running?
    pause
    exit /b 1
)

echo Waiting for PostgreSQL to accept connections...
set TRIES=0
:waitpg
docker compose -f "%COMPOSE%" exec -T postgres pg_isready -U hanjjak >nul 2>nul
if not errorlevel 1 goto pgready
set /a TRIES+=1
if %TRIES% GEQ 30 (
    echo PostgreSQL did not come up in 60 seconds.
    pause
    exit /b 1
)
timeout /t 2 /nobreak >nul
goto waitpg

:pgready
echo PostgreSQL is ready.
echo.

echo === 2/3 game API (beta profile) ===
start "hanjjak api (beta)" cmd /k "%~dp0run-api-beta.bat"
echo Launched in a separate window. First run downloads Gradle dependencies and takes a few minutes.
echo.

echo === 3/3 web client ===
start "hanjjak web" cmd /k "%~dp0run-web.bat"
echo Launched in a separate window.
echo.

echo Next:
echo   1. Wait for the api window to print "Started GameApiApplication".
echo   2. Open http://localhost:5173 and sign up.
echo   3. Run seed-tester.bat in this folder to raise that account to the test build.
echo.
pause

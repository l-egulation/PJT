@echo off
rem Starts the game API with the accelerated beta profile.
rem Kept as its own file so run-beta.bat can launch it in a window without nested quoting.
setlocal
cd /d "%~dp0..\.."
set SPRING_PROFILES_ACTIVE=beta

netstat -ano -p tcp | findstr LISTENING | findstr ":8080" >nul 2>nul
if not errorlevel 1 (
    echo.
    echo WARNING: something is already listening on port 8080.
    echo If that is an older game-api, close it first - this one will fail to bind.
    echo To use a different port instead, close this window and run:
    echo     set SERVER_PORT=8081
    echo     gradlew.bat :apps:game-api:bootRun
    echo   ...and set VITE_API_PROXY=http://127.0.0.1:8081 before starting the web client.
    echo.
    pause
)

echo.
echo === hanjjak game-api (beta profile) ===
echo Gradle compiles first, so a Kotlin error stops here before the server starts.
echo Look for: The following 1 profile is active: "beta"
echo.
call gradlew.bat :apps:game-api:bootRun
echo.
echo === game-api stopped (exit code %ERRORLEVEL%) ===
pause

@echo off
rem Writes an environment report to last-run.log in this folder, then shows it.
rem Double-click this, then tell Claude it is done - Claude can read the file from here.
setlocal enabledelayedexpansion
cd /d "%~dp0..\.."
set LOG=%~dp0last-run.log

echo hanjjak local environment report > "%LOG%"
echo generated %DATE% %TIME% >> "%LOG%"
echo. >> "%LOG%"

echo [repo root] >> "%LOG%"
cd >> "%LOG%" 2>&1
echo. >> "%LOG%"

echo [gradlew.bat present] >> "%LOG%"
if exist gradlew.bat (echo yes >> "%LOG%") else (echo NO >> "%LOG%")
echo [compose file present] >> "%LOG%"
if exist infra\local\compose.yaml (echo yes >> "%LOG%") else (echo NO >> "%LOG%")
echo. >> "%LOG%"

echo [docker --version] >> "%LOG%"
docker --version >> "%LOG%" 2>&1
echo. >> "%LOG%"

echo [docker ps] >> "%LOG%"
docker ps >> "%LOG%" 2>&1
echo. >> "%LOG%"

echo [java -version] >> "%LOG%"
java -version >> "%LOG%" 2>&1
echo. >> "%LOG%"

echo [node --version] >> "%LOG%"
node --version >> "%LOG%" 2>&1
echo. >> "%LOG%"

echo [npm --version] >> "%LOG%"
call npm --version >> "%LOG%" 2>&1
echo. >> "%LOG%"

echo [corepack --version] >> "%LOG%"
call corepack --version >> "%LOG%" 2>&1
echo. >> "%LOG%"

echo [pnpm --version] >> "%LOG%"
call pnpm --version >> "%LOG%" 2>&1
echo. >> "%LOG%"

echo [psql on PATH] >> "%LOG%"
where psql >> "%LOG%" 2>&1
echo. >> "%LOG%"

echo [psql in Program Files] >> "%LOG%"
for /d %%d in ("C:\Program Files\PostgreSQL\*") do (
    if exist "%%d\bin\psql.exe" echo %%d\bin\psql.exe >> "%LOG%"
)
for /d %%d in ("C:\Program Files (x86)\PostgreSQL\*") do (
    if exist "%%d\bin\psql.exe" echo %%d\bin\psql.exe >> "%LOG%"
)
echo. >> "%LOG%"

echo [what is holding 5432 and 8080] >> "%LOG%"
for /f "tokens=5" %%p in ('netstat -ano -p tcp ^| findstr LISTENING ^| findstr ":5432"') do (
    echo port 5432 pid %%p >> "%LOG%"
    tasklist /fi "pid eq %%p" /fo list >> "%LOG%" 2>&1
)
for /f "tokens=5" %%p in ('netstat -ano -p tcp ^| findstr LISTENING ^| findstr ":8080"') do (
    echo port 8080 pid %%p >> "%LOG%"
    tasklist /fi "pid eq %%p" /fo list >> "%LOG%" 2>&1
)
echo. >> "%LOG%"

echo [postgres services] >> "%LOG%"
sc query type= service state= all | findstr /i "postgres" >> "%LOG%" 2>&1
echo. >> "%LOG%"

type "%LOG%"
echo.
echo ---------------------------------------------
echo Report written to %LOG%
echo Tell Claude it is done; Claude will read the file.
echo ---------------------------------------------
pause

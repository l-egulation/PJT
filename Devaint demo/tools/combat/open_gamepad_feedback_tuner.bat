@echo off
setlocal
cd /d "%~dp0"
python gamepad_feedback_tuner.py %*
if errorlevel 1 pause

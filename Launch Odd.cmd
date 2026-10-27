@echo off
if exist "%~dp0BUILD-HOLD.txt" (
 echo Odd is on hold pending Avast detection review. See AVAST-REVIEW.md.
 pause
 exit /b 1
)
start "" "%~dp0build\Odd2U.exe"

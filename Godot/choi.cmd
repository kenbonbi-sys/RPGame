@echo off
rem Starts Rung Thi Tham 3D. Expects Godot 4.7.2 in %USERPROFILE%\Godot (see Docs/Godot.md).
rem   choi.cmd          play
rem   choi.cmd editor   open the project in the Godot editor
set GODOT=%USERPROFILE%\Godot\Godot_v4.7.2-stable_win64.exe
if not exist "%GODOT%" (
  echo Godot 4.7.2 not found at %GODOT%
  pause
  exit /b 1
)
if /i "%1"=="editor" (
  start "" "%GODOT%" --editor --path "%~dp0."
) else (
  start "" "%GODOT%" --path "%~dp0."
)

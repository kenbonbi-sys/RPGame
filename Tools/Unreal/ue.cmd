@echo off
rem The Unreal port (Unreal/) from the command line:
rem   ue build          compile the game and editor code (UnrealBuildTool)
rem   ue level          rebuild the test map /Game/Maps/ThuNghiem (make_level.py, editor Python)
rem   ue test           run the RungThiTham automation tests without a window
rem   ue shot [file]    play the map in a window, screenshot it once it has settled, quit
rem UE_ROOT picks the engine (default: the launcher's UE 5.8). Logs go to Unreal/Saved/Tools.
setlocal
set "HERE=%~dp0"
for %%I in ("%HERE%..\..") do set "ROOT=%%~fI"
set "PROJ=%ROOT%\Unreal\RungThiTham.uproject"
set "OUT=%ROOT%\Unreal\Saved\Tools"
if not defined UE_ROOT set "UE_ROOT=C:\Program Files\Epic Games\UE_5.8"
set "EDITOR=%UE_ROOT%\Engine\Binaries\Win64\UnrealEditor-Cmd.exe"
if not exist "%EDITOR%" (
  echo Unreal Engine is not in "%UE_ROOT%". Install it from the Epic Games Launcher or set UE_ROOT.
  exit /b 2
)
if not exist "%OUT%" mkdir "%OUT%"
set "COMMON=-unattended -nopause -nosplash -nosound -NoLogTimes"

if /i "%~1"=="build" goto build
if /i "%~1"=="level" goto level
if /i "%~1"=="test" goto test
if /i "%~1"=="shot" goto shot
echo usage: ue build ^| level ^| test ^| shot [file.png]
exit /b 2

:build
call "%UE_ROOT%\Engine\Build\BatchFiles\Build.bat" RungThiThamEditor Win64 Development -Project="%PROJ%" -WaitMutex
exit /b %errorlevel%

:level
rem the map and its material are built from scratch: the commandlet cannot replace them on disk
if exist "%ROOT%\Unreal\Content\Maps\ThuNghiem.umap" del "%ROOT%\Unreal\Content\Maps\ThuNghiem.umap"
if exist "%ROOT%\Unreal\Content\Materials\M_Grid.uasset" del "%ROOT%\Unreal\Content\Materials\M_Grid.uasset"
"%EDITOR%" "%PROJ%" -run=pythonscript -script="%HERE%make_level.py" %COMMON% -abslog="%OUT%\level.log" >nul
findstr /c:"RTT_LEVEL_OK" "%OUT%\level.log" >nul && (echo The map is built: /Game/Maps/ThuNghiem & exit /b 0)
findstr /i /c:"LogPython: Error" /c:"Error:" "%OUT%\level.log"
echo The map was not built; see %OUT%\level.log
exit /b 1

:test
"%EDITOR%" "%PROJ%" -ExecCmds="Automation RunTests RungThiTham;Quit" -testexit="Automation Test Queue Empty" -NullRHI %COMMON% -abslog="%OUT%\test.log" >nul
findstr /c:"Test Completed" "%OUT%\test.log"
findstr /c:"Result={Fail" "%OUT%\test.log" >nul && (echo Some tests failed; see %OUT%\test.log & exit /b 1)
findstr /c:"Result={Success" "%OUT%\test.log" >nul || (echo No test ran; see %OUT%\test.log & exit /b 1)
echo All tests passed.
exit /b 0

:shot
set "SHOT=%~2"
if "%SHOT%"=="" set "SHOT=%OUT%\shot.png"
if exist "%SHOT%" del "%SHOT%"
"%EDITOR%" "%PROJ%" /Game/Maps/ThuNghiem -game -windowed -ResX=1600 -ResY=900 -RttShot="%SHOT%" %COMMON% -abslog="%OUT%\shot.log" >nul
if exist "%SHOT%" (echo %SHOT% & exit /b 0)
echo No screenshot; see %OUT%\shot.log
exit /b 1

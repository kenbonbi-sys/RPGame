@echo off
rem The Unreal port (Unreal/) from the command line:
rem   ue build          compile the game and editor code (UnrealBuildTool)
rem   ue level          rebuild the maps ThuNghiem (test ground) and HangGau (Gau Ma den), make_level.py
rem   ue test           run the RungThiTham automation tests without a window
rem   ue shot [file] [run]  play the map in a window, screenshot it once it has settled, quit
rem                     (run: the hero runs on by itself, for a shot of the run)
rem   ue hero [Name]    name the Paragon hero added from Fab in Config/DefaultGame.ini
rem   ue assets         bring in the game's own art (the sculpted Gau Ma, materials): import_assets.py
rem   ue play           play the game in a window (Unreal/Choi.cmd does this on a double-click)
rem Keep this file ASCII: cmd.exe misreads multi-byte UTF-8 lines in a .cmd.
rem UE_ROOT picks the engine (default: the launcher's UE 5.8); RTT_MAP the map shot and play open
rem (default: /Game/Maps/HangGau). Logs go to Unreal/Saved/Tools.
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
if not defined RTT_MAP set "RTT_MAP=/Game/Maps/HangGau"

if /i "%~1"=="build" goto build
if /i "%~1"=="level" goto level
if /i "%~1"=="test" goto test
if /i "%~1"=="shot" goto shot
if /i "%~1"=="hero" goto hero
if /i "%~1"=="play" goto play
if /i "%~1"=="assets" goto assets
echo usage: ue build ^| level ^| test ^| shot [file.png] [run] ^| hero [Name]
exit /b 2

:assets
rem the materials are built anew (rebuilding one in place trips an editor assert)
for %%M in (M_VertexColour M_Glow M_Telegraph) do if exist "%ROOT%\Unreal\Content\Materials\%%M.uasset" del "%ROOT%\Unreal\Content\Materials\%%M.uasset"
"%EDITOR%" "%PROJ%" -run=pythonscript -script="%HERE%import_assets.py" %COMMON% -abslog="%OUT%\assets.log" >nul
findstr /c:"RTT_ASSETS" "%OUT%\assets.log" | findstr /v /c:"Running Python"
findstr /c:"RTT_ASSETS_OK" "%OUT%\assets.log" >nul && exit /b 0
findstr /i /c:"LogPython: Error" "%OUT%\assets.log"
echo The art was not brought in; see %OUT%\assets.log
exit /b 1

:play
start "" "%UE_ROOT%\Engine\Binaries\Win64\UnrealEditor.exe" "%PROJ%" %RTT_MAP% -game -windowed -ResX=1600 -ResY=900
exit /b 0

:hero
"%EDITOR%" "%PROJ%" -run=pythonscript -script="%HERE%setup_hero.py %~2" %COMMON% -abslog="%OUT%\hero.log" >nul
findstr /c:"RTT_HERO" "%OUT%\hero.log" | findstr /v /c:"Running Python"
findstr /c:"RTT_HERO_OK" "%OUT%\hero.log" >nul && exit /b 0
findstr /i /c:"LogPython: Error" "%OUT%\hero.log"
echo No Paragon hero was set up; see %OUT%\hero.log
exit /b 1

:build
call "%UE_ROOT%\Engine\Build\BatchFiles\Build.bat" RungThiThamEditor Win64 Development -Project="%PROJ%" -WaitMutex
exit /b %errorlevel%

:level
rem the map and its material are built from scratch: the commandlet cannot replace them on disk
if exist "%ROOT%\Unreal\Content\Maps\ThuNghiem.umap" del "%ROOT%\Unreal\Content\Maps\ThuNghiem.umap"
if exist "%ROOT%\Unreal\Content\Maps\HangGau.umap" del "%ROOT%\Unreal\Content\Maps\HangGau.umap"
if exist "%ROOT%\Unreal\Content\Materials\M_Grid.uasset" del "%ROOT%\Unreal\Content\Materials\M_Grid.uasset"
"%EDITOR%" "%PROJ%" -run=pythonscript -script="%HERE%make_level.py" %COMMON% -abslog="%OUT%\level.log" >nul
findstr /c:"RTT_LEVEL_OK /Game/Maps/HangGau" "%OUT%\level.log" >nul && (echo The maps are built: /Game/Maps/ThuNghiem, /Game/Maps/HangGau & exit /b 0)
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
set "RUN="
if /i "%~3"=="run" set "RUN=-RttDemoRun -RttShotDelay=4"
if not defined RTT_SHOT_DELAY set "RTT_SHOT_DELAY=12"
if /i "%~3"=="fight" set "RUN=-RttDemoFight -RttShotDelay=%RTT_SHOT_DELAY%"
if exist "%SHOT%" del "%SHOT%"
"%EDITOR%" "%PROJ%" %RTT_MAP% -game -windowed -ResX=1600 -ResY=900 -RttShot="%SHOT%" %RUN% %COMMON% -abslog="%OUT%\shot.log" >nul
if exist "%SHOT%" (echo %SHOT% & exit /b 0)
echo No screenshot; see %OUT%\shot.log
exit /b 1

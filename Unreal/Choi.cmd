@echo off
rem Double-click to play the Unreal port in a window: Hang Gau, the Gau Ma fight (RTT_MAP picks another map).
rem WASD run, mouse turns the camera, left button Chem Gio, hold the right button for Tram,
rem Shift tap to dash (Luot), hold to sprint, Space jumps. Alt+F4 quits.
rem The first start compiles shaders and may take a few minutes.
rem (Keep this file ASCII: cmd.exe misreads multi-byte UTF-8 lines in a .cmd.)
call "%~dp0..\Tools\Unreal\ue.cmd" play

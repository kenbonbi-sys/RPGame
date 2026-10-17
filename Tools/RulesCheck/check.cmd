@echo off
rem Builds the Unreal port's plain C++ rules (Unreal/Source/RungThiTham/Rules) with the tests in
rem Unreal/Tests/RulesTests.cpp and runs them against Unreal/Content/Data, without the engine.
rem Compiled the way UE builds the game: C++20, no exceptions, warnings as errors.
rem Exit code 0 = it builds and every check passes.
setlocal
set "HERE=%~dp0"
set "ROOT=%HERE%..\.."
set "OUT=%HERE%.build"

if not defined VCToolsInstallDir (
  set "VCVARS="
  for %%V in (18 2022) do (
    for %%E in (BuildTools Community Professional Enterprise) do (
      if not defined VCVARS if exist "C:\Program Files (x86)\Microsoft Visual Studio\%%V\%%E\VC\Auxiliary\Build\vcvars64.bat" set "VCVARS=C:\Program Files (x86)\Microsoft Visual Studio\%%V\%%E\VC\Auxiliary\Build\vcvars64.bat"
      if not defined VCVARS if exist "C:\Program Files\Microsoft Visual Studio\%%V\%%E\VC\Auxiliary\Build\vcvars64.bat" set "VCVARS=C:\Program Files\Microsoft Visual Studio\%%V\%%E\VC\Auxiliary\Build\vcvars64.bat"
    )
  )
)
if not defined VCToolsInstallDir (
  if not defined VCVARS (
    echo No Visual Studio C++ tools found ^(vcvars64.bat^).
    exit /b 2
  )
  call "%VCVARS%" >nul 2>nul
)

if not exist "%OUT%" mkdir "%OUT%"
cl /nologo /std:c++20 /W4 /WX /permissive- /utf-8 /EHs-c- /D_HAS_EXCEPTIONS=0 /O2 ^
  /I "%ROOT%\Unreal\Source\RungThiTham" /Fo"%OUT%\\" /Fe"%OUT%\RulesTests.exe" ^
  "%ROOT%\Unreal\Source\RungThiTham\Rules\*.cpp" "%ROOT%\Unreal\Tests\RulesTests.cpp" >"%OUT%\build.log"
if errorlevel 1 (
  findstr /v /r "^Rtt[A-Za-z]*\.cpp$ ^RulesTests\.cpp$" "%OUT%\build.log"
  echo The rules do not build.
  exit /b 1
)
"%OUT%\RulesTests.exe" "%ROOT%\Unreal\Content\Data"
exit /b %errorlevel%

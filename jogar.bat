@echo off
setlocal
set "GODOT_EXE=F:\GODOT\App\Godot.exe"

if not exist "%GODOT_EXE%" (
  echo Godot nao encontrado em %GODOT_EXE%
  echo Ajuste GODOT_EXE neste arquivo para o executavel Godot .NET.
  pause
  exit /b 1
)

pushd "%~dp0"
"%GODOT_EXE%" --path "%CD%"
set "EXIT_CODE=%ERRORLEVEL%"
popd

if not "%EXIT_CODE%"=="0" (
  echo O jogo encerrou com o codigo %EXIT_CODE%.
  pause
)
exit /b %EXIT_CODE%

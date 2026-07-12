@echo off
setlocal
set "PROJECT_DIR=%~dp0"
set "GODOT_EXE=F:\GODOT\App\Godot.exe"

if not exist "%GODOT_EXE%" (
  echo Godot .NET nao encontrado em %GODOT_EXE%
  pause
  exit /b 1
)

if not exist "%PROJECT_DIR%builds" mkdir "%PROJECT_DIR%builds"
"%GODOT_EXE%" --headless --path "%PROJECT_DIR%" --export-release "Windows Desktop" "%PROJECT_DIR%builds\VampiresPrey.exe"
if not exist "%PROJECT_DIR%builds\VampiresPrey.exe" (
  echo Falha ao exportar. Instale os templates de exportacao do Godot 4.7 pelo menu Editor.
  pause
  exit /b 1
)
echo Executavel criado em builds\VampiresPrey.exe
pause

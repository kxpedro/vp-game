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

where dotnet >nul 2>&1
if errorlevel 1 goto dotnet_missing

echo Compilando scripts C#...
dotnet build "VP0.1.csproj" --nologo
if errorlevel 1 goto build_failed

rem Importa imagens novas ou alteradas (ex.: depois de um merge); sem isso o jogo cai no desenho antigo.
echo Importando recursos...
"%GODOT_EXE%" --headless --path "%CD%" --import

"%GODOT_EXE%" --path "%CD%"
set "EXIT_CODE=%ERRORLEVEL%"
popd

if not "%EXIT_CODE%"=="0" (
  echo O jogo encerrou com o codigo %EXIT_CODE%.
  pause
)
exit /b %EXIT_CODE%

:dotnet_missing
echo O SDK do .NET nao foi encontrado no PATH.
echo Instale o SDK do .NET 8 e tente novamente.
popd
pause
exit /b 1

:build_failed
echo Nao foi possivel compilar os scripts C#.
popd
pause
exit /b 1

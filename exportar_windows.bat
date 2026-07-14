@echo off
setlocal
set "PROJECT_DIR=%~dp0"
set "OUTPUT_EXE=%PROJECT_DIR%builds\VampiresPrey.exe"
set "EXPORT_LOG=%TEMP%\VampiresPrey-export.log"

if not defined GODOT_EXE set "GODOT_EXE=%PROJECT_DIR%..\App\Godot.exe"
if not exist "%GODOT_EXE%" set "GODOT_EXE=F:\GODOT\App\Godot.exe"

if not exist "%GODOT_EXE%" (
  echo [ERRO] Godot .NET nao encontrado.
  echo Defina GODOT_EXE com o caminho do executavel antes de rodar este arquivo.
  if not defined CI pause
  exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERRO] SDK do .NET nao encontrado no PATH.
  if not defined CI pause
  exit /b 1
)

if not exist "%PROJECT_DIR%VP0.1.sln" (
  echo [ERRO] VP0.1.sln nao encontrado. O Godot Mono exige a solution para exportar scripts C#.
  if not defined CI pause
  exit /b 1
)

pushd "%PROJECT_DIR%"
echo [1/3] Restaurando dependencias de exportacao Windows x64...
dotnet restore "VP0.1.sln" --runtime win-x64 --nologo
if errorlevel 1 (
  echo [ERRO] Nao foi possivel restaurar os runtimes Windows x64 do .NET.
  echo Verifique a conexao com https://api.nuget.org e tente novamente.
  popd
  if not defined CI pause
  exit /b 1
)

echo [2/3] Compilando o projeto C# em Release...
dotnet build "VP0.1.sln" --configuration Release --nologo
if errorlevel 1 (
  echo [ERRO] A compilacao C# falhou. A exportacao foi cancelada.
  popd
  if not defined CI pause
  exit /b 1
)

if not exist "%PROJECT_DIR%builds" mkdir "%PROJECT_DIR%builds"
if exist "%OUTPUT_EXE%" del /q "%OUTPUT_EXE%"
if exist "%EXPORT_LOG%" del /q "%EXPORT_LOG%"
echo [3/3] Exportando Windows Desktop...
"%GODOT_EXE%" --headless --path "." --export-release "Windows Desktop" "%OUTPUT_EXE%" > "%EXPORT_LOG%" 2>&1
set "GODOT_EXIT=%ERRORLEVEL%"
type "%EXPORT_LOG%"
findstr /C:"ERROR:" "%EXPORT_LOG%" >nul
set "GODOT_LOG_HAS_ERROR=%ERRORLEVEL%"
if not "%GODOT_EXIT%"=="0" (
  echo [ERRO] O Godot nao conseguiu exportar o jogo.
  echo Verifique se os templates de exportacao da mesma versao do editor estao instalados.
  popd
  if not defined CI pause
  exit /b 1
)

if "%GODOT_LOG_HAS_ERROR%"=="0" (
  echo [ERRO] O Godot reportou um erro interno durante a exportacao. O executavel gerado foi descartado.
  if exist "%OUTPUT_EXE%" del /q "%OUTPUT_EXE%"
  popd
  if not defined CI pause
  exit /b 1
)

if not exist "%OUTPUT_EXE%" (
  echo [ERRO] A exportacao terminou sem gerar %OUTPUT_EXE%.
  popd
  if not defined CI pause
  exit /b 1
)

popd
echo [OK] Executavel criado em builds\VampiresPrey.exe
if not defined CI pause

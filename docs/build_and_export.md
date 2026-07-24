# Build e exportação Windows

## Requisitos

- Godot Engine `4.7.stable.mono`;
- templates de exportação da mesma versão Mono;
- SDK do .NET 8;
- acesso ao NuGet na primeira restauração dos runtimes Windows.

Os templates são instalados em **Editor → Gerenciar Modelos de Exportação**. O Godot espera os executáveis em:

```text
%APPDATA%\Godot\export_templates\4.7.stable.mono\
```

## Build C#

O exportador Mono exige uma solution ao lado do `project.godot`:

```text
VP0.1.sln
VP0.1.csproj
```

Validação local:

```bash
dotnet build VP0.1.sln --configuration Debug
dotnet build VP0.1.sln --configuration Release
py .codex/skills/vp-create-blood-blessing/scripts/validate_blessings.py
py .codex/skills/vp-create-artifact/scripts/validate_artifacts.py
```

Validar a estrutura das skills locais com o `quick_validate.py` da skill de sistema `skill-creator`. As skills versionadas atualmente são `vp-create-blood-blessing`, `vp-create-artifact`, `vp-create-player-skill`, `vp-create-enemy` e `vp-create-boss`.

## Export automatizado

No Windows:

```bat
exportar_windows.bat
```

O script executa três etapas:

1. `dotnet restore VP0.1.sln --runtime win-x64`;
2. build C# em `Release`;
3. export do preset `Windows Desktop`.

O console do Godot é salvo temporariamente e analisado. Como o Godot pode retornar código 0 mesmo depois de registrar um erro interno, qualquer linha `ERROR:` cancela a operação e remove o executável incompleto.

É possível definir outro executável do Godot antes de chamar o script:

```bat
set GODOT_EXE=C:\caminho\Godot_v4.7-stable_mono_win64.exe
exportar_windows.bat
```

## Arquivos para distribuição

O preset incorpora o PCK no executável, mas o runtime C# self-contained permanece em uma pasta separada:

```text
builds/
├── VampiresPrey.exe
└── data_VP0.1_windows_x86_64/
```

Os dois itens são obrigatórios. Para distribuir um teste, compacte ambos no mesmo `.zip`, mantendo o nome e a posição relativa da pasta.

`builds/` está no `.gitignore` e não deve ser commitado.

## Diagnóstico rápido

- **Templates ausentes:** instalar exatamente `4.7.stable.mono` pelo editor.
- **`VP0.1.sln` ausente:** o export não inclui corretamente os scripts C#.
- **Falha `NU1301`:** verificar acesso a `https://api.nuget.org`.
- **O `.exe` existe, mas houve `ERROR:`:** considerar o export inválido; o script atual já faz essa verificação.
- **Jogo funciona localmente, mas não em outra máquina:** confirmar que `data_VP0.1_windows_x86_64` foi enviado junto.

## Smoke test

Para abrir as cenas principais no projeto sem janela:

```powershell
godot --headless --path . --quit-after 180 res://scenes/game/TestArena.tscn
godot --headless --path . --quit-after 120 res://scenes/game/IntermissionChamber.tscn
godot --headless --path . --quit-after 120 res://scenes/game/CrowsNest.tscn
```

Depois do export, o executável pode ser iniciado por poucos frames sem abrir janela:

```powershell
.\builds\VampiresPrey.exe --headless --quit-after 3
```

Uma inicialização limpa confirma que o runtime e as assemblies C# foram empacotados.

<!-- orch:plano · id: 20261011-0815-revisao-graficos · status: em-execucao · criado: 2026-10-11 08:15 · commit-inicial: bd370e4 · versao-orch: 0.5.0 -->
# Revisão das configurações gráficas

**Demanda original:** Pedro (2026-10-11), texto colado: (1) analisar videos, imagens da gameplay do Hades e Sworn como referencia; verificar se o jogo e parecido mecanicamente, identificar e destacar as principais diferencas e se e possivel implementar; analisar toda e qualquer mecanica. (2) melhorar visualmente as animacoes de ataque do jogador e dos inimigos, deixar mais obvio quando um inimigo for atacar, usando Hades e Sworn como referencia. (3) desenvolver mecanica de dialogo, usando Hades como referencia. (4) focar um pouco nas configuracoes graficas do jogo, dar uma revisada.
**Grupo:** 20261011-0815-referencia-hades-sworn · **Depende dos planos:** 20261011-0815-sistema-dialogo

## Objetivo

Menu de opções gráficas completo, aplicado em tempo real e persistido: modo de janela, resolução, VSync, limite de FPS, escala de renderização, qualidade de sombras/MSAA/efeitos e brilho, com presets e padrões sensatos, nos 3 idiomas.

## Suposições e decisões

- T1 (auditoria) pode rodar já; T2 espera o plano de diálogo porque ambos editam `ui.json`, `HUD*.cs` e `UiComponents.cs`.
- Renderer atual: Forward Plus/d3d12 (`project.godot`). A auditoria só informa se vale oferecer Mobile/Compatibility; o renderer não é trocado neste plano.
- Opções em `GlobalSettings` (já persistidas); campos novos são opcionais com padrão, sem quebrar o arquivo atual.
- Janela e resolução são verificadas por inspeção de valores; a tela de opções vai pelo `--ui-batch`.

## Tarefas

| ID | Tarefa | Executor | Depende de | Arquivos (escrita) | Status |
|---|---|---|---|---|---|
| T1 | Auditar as configurações gráficas atuais (project.godot, GlobalSettings, OptionsPanel) | `orch:pesquisador` | — | — (leitura) | concluida |
| T2 | Implementar e aplicar as opções gráficas (menu, runtime, persistência) | `ui-hud` | T1 + plano diálogo | `scripts/systems/GlobalSettings.cs`, `scripts/ui/UiComponents.cs`, `scripts/ui/UiPreview.cs`, `data/localization/*/ui.json`, `project.godot` | concluida |
| T3 | Revisar (aplicação, persistência, compatibilidade com opções antigas) | `orch:revisor` | T2 | — (leitura) | concluida |

Status: `pendente` · `em-andamento` · `concluida` · `falhou` · `pulada`

## Ondas de execução

```
Onda 1: T1
Onda 2: T2
Onda 3: T3
```

Uma tarefa começa assim que **todas** as suas dependências terminam — não espera a onda inteira.

## Detalhe das tarefas

### T1 — Auditar as configurações gráficas atuais (project.godot, GlobalSettings, OptionsPanel)
- **Executor:** `orch:pesquisador`
- **Pronto quando:** Relatório: o que existe, o que é aplicado de fato, o que está solto; gargalos prováveis (sombras, glow, SSAO, MSAA) e lista recomendada de opções com padrões por perfil.
- **Resultado:** Auditoria: só existem idioma, 3 resoluções fixas, tela cheia e volumes; nenhum MSAA/vsync/FPS/escala/qualidade definido; glow, SSAO, fog e sombra sempre ligados por cena; ShakeIntensity e FlashIntensity sem UI; centralização errada em multi-monitor e flash de janela no boot; presets e métodos Godot propostos; nomes exatos de API do 4.7 a validar no compilador.

### T2 — Implementar e aplicar as opções gráficas (menu, runtime, persistência)
- **Executor:** `ui-hud`
- **Pronto quando:** Opções aplicam em tempo real e persistem; foco por controle; presets baixo/médio/alto; `dotnet build VP0.1.sln` com 0 erros/0 avisos; `py tools/validate_localization.py` sem erro; captura da tela de opções nos 3 idiomas.
- **Resultado:** Opções gráficas completas: modo de janela, resolução gerada pelo monitor, monitor, VSync, FPS, escala 3D com Bilinear/FSR1, FXAA/MSAA, sombras, SSAO, glow, brilho, presets, tremor/flash; aplicador GraphicsSettings por cena sem editar .tscn; painel em 3 abas com L1/R1; confirmação com reversão em 10 s; 36 chaves nos 3 idiomas; migração do arquivo antigo e gravação atômica com debounce. Build 0/0, validador e smoke ok; nada testado com janela/FSR/controle reais.

### T3 — Revisar (aplicação, persistência, compatibilidade com opções antigas)
- **Executor:** `orch:revisor`
- **Pronto quando:** Sem achados altos; arquivo de opções antigo carrega com os padrões.
- **Resultado:** Revisão sem achados altos; corrigidos M1 (modo não confirmado gravado), M2 (Reverter e monitor), M3 (VSync reaplicado), e baixos B4-B9 e B11. Build 0/0, validadores, smoke e autoteste ok, arquivo real do usuário intacto.

## Registro de execução

| Quando | Evento |
|---|---|
| 2026-10-11 08:15 | Plano criado |
| 2026-10-11 02:10 | Plano em-execucao |
| 2026-10-11 02:10 | T1 em-andamento |
| 2026-10-11 02:35 | T1 concluida |
| 2026-10-11 02:35 | T2 em-andamento |
| 2026-10-11 03:20 | T2 concluida |
| 2026-10-11 03:20 | T3 em-andamento |
| 2026-10-11 04:10 | T3 concluida |
| 2026-10-11 04:10 | Plano concluido |

## Resultado final

Opções gráficas entregues: modo de janela, resolução, monitor, VSync, FPS, escala 3D Bilinear/FSR1, FXAA/MSAA, sombras, SSAO, glow, brilho, presets, tremor/flash, painel em 3 abas com controle, confirmação com reversão em 10 s, 36 chaves nos 3 idiomas. Não testado jogando: janela/exclusivo no d3d12, FSR, VSync adaptativo, multi-monitor, controle, reversão M1/M2.

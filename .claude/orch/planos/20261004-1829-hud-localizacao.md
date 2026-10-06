<!-- orch:plano · id: 20261004-1829-hud-localizacao · status: planejado · criado: 2026-10-04 18:29 · commit-inicial: 0c387de · versao-orch: 0.3.0 -->
# HUD e menus — Fase 4: localização por idioma, revisão de textos e fechamento

**Demanda original:** revisar, ajustar e melhorar a HUD inteira do jogo, de todos os menus e etc..
**Grupo:** 20261004-1829-hud-auditoria · **Depende dos planos:** 20261004-1829-hud-ingame, 20261004-1829-hud-menus-sistema, 20261004-1829-hud-overlays-progressao

## Objetivo

Todo texto exibido ao jogador vem de um recurso de localização **por idioma** (pt-BR, en, es) — interface e conteúdo (bênçãos, artefatos, skills, bosses, mapas, armas, mensagens). No código ficam só chaves. Traduções e acentuação revisadas e corrigidas; interface verificada nos três idiomas; revisão final e documentação.

## Suposições e decisões

- **Decisão D5 do usuário (2026-10-04):** um recurso específico por idioma; em código só chaves; o que aparece na tela tem de estar correto; erros de tradução/acento encontrados são corrigidos agora.
- Estrutura: uma pasta por idioma, `data/localization/<idioma>/`, com arquivos por domínio (`ui.json`, `content.json`, `world.json`). Arquivos de domínios diferentes permitem que T2, T3 e T4 rodem em paralelo sem conflito de escrita. `Localization` carrega todos os `.json` da pasta do idioma; fallback para pt-BR e, por último, a própria chave (registrando aviso em debug).
- `data/localization/game_texts.json` é migrado para essa estrutura e removido ao fim de T1.
- Catálogos (`blood_blessings.json`, `artifacts.json`, `skills.json`) passam a guardar chaves de nome/descrição/flavor em vez do texto; IDs e números não mudam. A descrição de bênção deixa de depender de regex sobre o texto em português (risco apontado no inventário): números e palavras-chave entram por placeholders.
- Validadores dos catálogos continuam passando (ajustados se necessário) e um validador novo garante paridade de chaves entre idiomas.
- Mensagens só de debug (log de eventos, ferramentas DEV) podem continuar fixas.
- Feita depois das fases 3a–3c porque mexe nos mesmos arquivos de UI.

## Tarefas

| ID | Tarefa | Executor | Depende de | Arquivos (escrita) | Status |
|---|---|---|---|---|---|
| T1 | Reestruturar a localização em recursos por idioma | `ui-hud` | — | `scripts/systems/Localization.cs`, `data/localization/**`, `tools/validate_localization.py` (novo) | pendente |
| T2 | Extrair textos da interface para `ui.json` | `ui-hud` | T1 | `scripts/ui/*.cs`, `scripts/systems/IntermissionChamber.cs`, `scripts/systems/RunStartBlessingRoom.cs`, `scripts/systems/{ArtifactChest,BloodFountain,WeaponBench,ScenePortal}.cs`, `scripts/items/RoomArtifact.cs`, `data/localization/*/ui.json` | pendente |
| T3 | Localizar conteúdo: bênçãos, artefatos, skills e armas | `conteudo-run` | T1 | `data/blessings/blood_blessings.json`, `data/artifacts/artifacts.json`, `data/skills/skills.json`, `scripts/systems/{blessings,artifacts,skills,weapons}/*.cs`, `scripts/player/BloodBlessingRuntime.cs`, `data/localization/*/content.json`, `.codex/skills/*/scripts/*.py` | pendente |
| T4 | Localizar mundo: bosses, mapas, inimigos e mensagens de combate | `inimigos-e-bosses` | T1 | `scripts/bosses/*.cs`, `scripts/systems/{CampaignCatalog,RunManager,RoomGenerator}.cs`, `scripts/enemies/*.cs`, `scripts/player/PlayerCombat.cs`, `data/localization/*/world.json` | pendente |
| T5 | Revisar traduções e acentuação em todos os idiomas | `game-designer` | T2, T3, T4 | `data/localization/**` | pendente |
| T6 | Verificar a interface em 3 idiomas e 3 resoluções | `designer-ux-arte` | T5 | — (leitura) | pendente |
| T7 | Revisão final de código e validação | `orch:revisor` | T5 | — (leitura) | pendente |
| T8 | Atualizar documentação da interface e da localização | `orch:documentador` | T6, T7 | `docs/gameplay_systems.md`, `README.md`, `docs/design/ui-guia.md`, `.claude/orch/perfil.md` | pendente |

## Ondas de execução

```
Onda 1: T1
Onda 2: T2 ∥ T3 ∥ T4   (após T1; arquivos de localização por domínio, sem conflito)
Onda 3: T5             (após T2, T3, T4)
Onda 4: T6 ∥ T7        (após T5)
Onda 5: T8             (após T6, T7)
```

Caminho crítico: T1 → T3 → T5 → T6 → T8 (T3 é a maior: ~83 entradas de conteúdo com nome, descrição e flavor).

## Detalhe das tarefas

### T1 — Reestruturar a localização em recursos por idioma
- **Executor:** `ui-hud`
- **Faz:** cria `data/localization/pt-BR/`, `en/`, `es/` com `ui.json`, `content.json`, `world.json` (formato plano `"chave": "texto"`); migra as chaves de `game_texts.json` para `ui.json` dos três idiomas e remove o arquivo antigo; `Localization` carrega todos os arquivos da pasta do idioma, mantém `Text`/`TextFormat`/`LanguageChanged`, fallback pt-BR → chave, protege `string.Format` contra tradução malformada; adiciona `Localization.Has(key)`. Cria `tools/validate_localization.py` (paridade de chaves entre idiomas, placeholders `{0}` iguais, JSON válido, chaves usadas no código existentes).
- **Pronto quando:** menu e HUD mostram os mesmos textos de antes nos três idiomas; validador passa; build limpo.
- **Resultado:** —

### T2 — Extrair textos da interface para `ui.json`
- **Executor:** `ui-hud`
- **Faz:** troca todos os literais de interface visíveis ao jogador (HUD, overlays, menus, prompts 3D dos interativos, Câmara, sala inicial) por chaves com prefixos do Anexo C do guia; telas abertas reagem a `LanguageChanged` ou são reconstruídas ao abrir; liga o idioma também nas opções da pausa (`OptionsPanel` com `showLanguage: true`).
- **Inclui (achado da revisão de hud-ingame):** textos da HUD em jogo que voltaram a ser literais em PT — `HUD.Notifications.cs` ("Chefe", "Sala x/y · n/m", "Próxima onda", "inimigos", texto do banner), `HUD.StatusBars.cs` (título após limpar a sala), `HUD.BlessingBar.cs` ("Ficha", "Nível", "Mais N bênçãos"); chaves antigas `hud.enemies`/`hud.blood_coins` ficaram sem uso.
- **Pronto quando:** nenhum literal de interface visível fora de `ui.json` (exceções de debug listadas); validador passa; build limpo.
- **Resultado:** —

### T3 — Localizar conteúdo: bênçãos, artefatos, skills e armas
- **Executor:** `conteudo-run`
- **Faz:** move nome, descrição, flavor e rótulos de efeito/raridade/família dos catálogos para `content.json` (chaves derivadas dos IDs, ex.: `blessing.<id>.name`); catálogos resolvem o texto via `Localization`; reescreve a montagem da descrição de bênção para usar placeholders em vez de regex sobre texto em PT (destaque de palavras-chave por marcação na própria string); traduz para en/es; ajusta os validadores para o novo formato.
- **Pronto quando:** ofertas, ficha, Fonte e compêndio mostram o conteúdo nos três idiomas; validadores de bênçãos/artefatos e de localização passam; build limpo.
- **Resultado:** —

### T4 — Localizar mundo: bosses, mapas, inimigos e mensagens de combate
- **Executor:** `inimigos-e-bosses`
- **Faz:** nomes de bosses e golpes, nomes de mapas/salas ("Calabouços de Armand" etc.), rótulos de sala, nomes de arquétipos/raridades exibidos, mensagens de combate visíveis (ex.: avisos de skill, causa da morte) passam a chaves em `world.json`, com tradução en/es.
- **Pronto quando:** sem literal de mundo visível ao jogador fora de `world.json`; validador passa; build e smoke limpos.
- **Resultado:** —

### T5 — Revisar traduções e acentuação em todos os idiomas
- **Executor:** `game-designer`
- **Faz:** revisa `ui.json`, `content.json` e `world.json` nos três idiomas — **inclusive pt-BR** (acentos, concordância, erros), terminologia consistente (glossário: Bênção de Sangue, Fúria Bestial, Moedas de Sangue, linhagens Dracul/Bathory/Karnstein/Orlok, Stirps, raridades), tom dark fantasy e comprimento compatível com os componentes; corrige o que encontrar.
- **Pronto quando:** glossário no resultado; validador passa; lista das correções feitas.
- **Resultado:** —

### T6 — Verificar a interface em 3 idiomas e 3 resoluções
- **Executor:** `designer-ux-arte`
- **Faz:** capturas de todas as telas via `--ui-preview` com `--ui-lang` em pt-BR/en/es (resoluções 720p/1080p por janela real ou cálculo, já que o Movie Maker grava 1600×900); aponta textos que estouram, se sobrepõem ou aparecem como chave crua.
- **Pronto quando:** relatório com a matriz de capturas e problemas; severidade alta volta para T2–T5.
- **Resultado:** —

### T7 — Revisão final de código e validação
- **Executor:** `orch:revisor`
- **Faz:** revisão do conjunto das fases 2–4 (regressões, `CallGroup`, pausa, foco, debug-only, alocações por frame, saves antigos); build, validadores (bênçãos, artefatos, localização) e smoke headless das 5 cenas.
- **Pronto quando:** sem achados de severidade alta; comandos verdes.
- **Resultado:** —

### T8 — Atualizar documentação da interface e da localização
- **Executor:** `orch:documentador`
- **Inclui (de hud-ingame):** sincronizar no guia §8.3 (secundário "Sala limpa" → "Portal aberto" via `NotifyPortalOpened`), §3.9/§3.14 (contagem do alerta em TextPrimary, host do "+N", leque de números) e §4.2 (painel do jogador ~404 px).
- **Faz:** atualiza a seção de HUD/menus em `docs/gameplay_systems.md`, o README (telas, controles, como adicionar um idioma/texto), o guia com o estado final e o perfil do orch (estrutura de localização, `--ui-preview`, validador novo).
- **Pronto quando:** docs refletem o código final, sem informação inventada.
- **Resultado:** —

## Registro de execução

| Quando | Evento |
|---|---|
| 2026-10-04 18:29 | Plano criado |
| 2026-10-04 19:20 | Replanejado pela decisão D5 do usuário: recursos por idioma, conteúdo também localizado, revisão de textos em todos os idiomas (5 → 8 tarefas) |

## Resultado final

—

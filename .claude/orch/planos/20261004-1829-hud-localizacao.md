<!-- orch:plano · id: 20261004-1829-hud-localizacao · status: concluido · criado: 2026-10-04 18:29 · commit-inicial: 0c387de · versao-orch: 0.3.0 -->
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
| T1 | Reestruturar a localização em recursos por idioma | `ui-hud` | — | `scripts/systems/Localization.cs`, `data/localization/**`, `tools/validate_localization.py` (novo) | concluida |
| T2 | Extrair textos da interface para `ui.json` | `ui-hud` | T1 | `scripts/ui/*.cs`, `scripts/systems/IntermissionChamber.cs`, `scripts/systems/RunStartBlessingRoom.cs`, `scripts/systems/{ArtifactChest,BloodFountain,WeaponBench,ScenePortal}.cs`, `scripts/items/RoomArtifact.cs`, `data/localization/*/ui.json` | concluida |
| T3 | Localizar conteúdo: bênçãos, artefatos, skills e armas | `conteudo-run` | T1 | `data/blessings/blood_blessings.json`, `data/artifacts/artifacts.json`, `data/skills/skills.json`, `scripts/systems/{blessings,artifacts,skills,weapons}/*.cs`, `scripts/player/BloodBlessingRuntime.cs`, `data/localization/*/content.json`, `.codex/skills/*/scripts/*.py` | concluida |
| T4 | Localizar mundo: bosses, mapas, inimigos e mensagens de combate | `inimigos-e-bosses` | T1 | `scripts/bosses/*.cs`, `scripts/systems/{CampaignCatalog,RunManager,RoomGenerator}.cs`, `scripts/enemies/*.cs`, `scripts/player/PlayerCombat.cs`, `data/localization/*/world.json` | concluida |
| T5 | Revisar traduções e acentuação em todos os idiomas | `game-designer` | T2, T3, T4 | `data/localization/**` | concluida |
| T6 | Verificar a interface em 3 idiomas e 3 resoluções | `designer-ux-arte` | T5, T9 | — (leitura) | concluida |
| T7 | Revisão final de código e validação | `orch:revisor` | T5, T9 | — (leitura) | concluida |
| T9 | Exibir nomes localizados onde a UI ainda usa o nome gravado no save | `ui-hud` | T2, T3 | `scripts/ui/*.cs`, `scripts/systems/IntermissionChamber.cs` | concluida |
| T8 | Atualizar documentação da interface e da localização | `orch:documentador` | T6, T7 | `docs/gameplay_systems.md`, `README.md`, `docs/design/ui-guia.md`, `.claude/orch/perfil.md` | concluida |

## Ondas de execução

```
Onda 1: T1
Onda 2: T2 ∥ T3 ∥ T4   (após T1; arquivos de localização por domínio, sem conflito)
Onda 3: T5 ∥ T9        (após T2, T3, T4; T9 só mexe em código de UI, T5 só em data/localization)
Onda 4: T6 ∥ T7        (após T5, T9)
Onda 5: T8             (após T6, T7)
```

Caminho crítico: T1 → T3 → T5 → T6 → T8 (T9 em paralelo com T5) (T3 é a maior: ~83 entradas de conteúdo com nome, descrição e flavor).

## Detalhe das tarefas

### T1 — Reestruturar a localização em recursos por idioma
- **Executor:** `ui-hud`
- **Faz:** cria `data/localization/pt-BR/`, `en/`, `es/` com `ui.json`, `content.json`, `world.json` (formato plano `"chave": "texto"`); migra as chaves de `game_texts.json` para `ui.json` dos três idiomas e remove o arquivo antigo; `Localization` carrega todos os arquivos da pasta do idioma, mantém `Text`/`TextFormat`/`LanguageChanged`, fallback pt-BR → chave, protege `string.Format` contra tradução malformada; adiciona `Localization.Has(key)`. Cria `tools/validate_localization.py` (paridade de chaves entre idiomas, placeholders `{0}` iguais, JSON válido, chaves usadas no código existentes).
- **Pronto quando:** menu e HUD mostram os mesmos textos de antes nos três idiomas; validador passa; build limpo.
- **Resultado:** Localização em data/localization/{pt-BR,en,es}/{ui,content,world}.json (36 chaves migradas para ui.json; game_texts.json removido). Localization.cs carrega por pasta, fallback idioma → pt-BR → chave, TextFormat protegido, Has(key) novo. tools/validate_localization.py criado. Build 0 avisos/0 erros, validador 0 erros (16 avisos: chaves de TextOr ainda não criadas), smokes MainMenu/TestArena limpos, menu verificado nos 3 idiomas via --ui-preview/--ui-lang.

### T2 — Extrair textos da interface para `ui.json`
- **Executor:** `ui-hud`
- **Faz:** troca todos os literais de interface visíveis ao jogador (HUD, overlays, menus, prompts 3D dos interativos, Câmara, sala inicial) por chaves com prefixos do Anexo C do guia; telas abertas reagem a `LanguageChanged` ou são reconstruídas ao abrir; liga o idioma também nas opções da pausa (`OptionsPanel` com `showLanguage: true`).
- **Inclui (achado da revisão de hud-ingame):** textos da HUD em jogo que voltaram a ser literais em PT — `HUD.Notifications.cs` ("Chefe", "Sala x/y · n/m", "Próxima onda", "inimigos", texto do banner), `HUD.StatusBars.cs` (título após limpar a sala), `HUD.BlessingBar.cs` ("Ficha", "Nível", "Mais N bênçãos"); chaves antigas `hud.enemies`/`hud.blood_coins` ficaram sem uso.
- **Pronto quando:** nenhum literal de interface visível fora de `ui.json` (exceções de debug listadas); validador passa; build limpo.
- **Resultado:** ui.json com 374 chaves por idioma (343 novas, 5 antigas removidas); ~237 chamadas de UI trocadas por Localization.Text/TextFormat; HUD, overlays, menu, Câmara, sala inicial e prompts 3D reagem a LanguageChanged; idioma também nas opções da pausa; PromptText dos portais nos .tscn passa a guardar chave. Build 0/0, validador 0 erros (4 avisos de portal.* referenciados só em .tscn), smokes das 5 cenas limpos. Exceções de debug mantidas literais.

### T3 — Localizar conteúdo: bênçãos, artefatos, skills e armas
- **Executor:** `conteudo-run`
- **Faz:** move nome, descrição, flavor e rótulos de efeito/raridade/família dos catálogos para `content.json` (chaves derivadas dos IDs, ex.: `blessing.<id>.name`); catálogos resolvem o texto via `Localization`; reescreve a montagem da descrição de bênção para usar placeholders em vez de regex sobre texto em PT (destaque de palavras-chave por marcação na própria string); traduz para en/es; ajusta os validadores para o novo formato.
- **Pronto quando:** ofertas, ficha, Fonte e compêndio mostram o conteúdo nos três idiomas; validadores de bênçãos/artefatos e de localização passam; build limpo.
- **Resultado:** content.json com 467 chaves por idioma; catálogos guardam só IDs/números (artefatos ganham saveName para compatibilidade de save, com migração em CharacterSheet.WithResolvedId); descrição de bênção via BloodBlessingText com placeholders {n} e marcação [kw=...] (corrigiu 5 bênçãos que não mostravam valor escalado); validadores e SKILL.md atualizados. Verificação conjunta após T2+T3: validador de localização 904 chaves/0 erros/0 avisos, bênçãos 63/0/0, artefatos 20/0/0, build 0 erros, smoke TestArena limpo.

### T4 — Localizar mundo: bosses, mapas, inimigos e mensagens de combate
- **Executor:** `inimigos-e-bosses`
- **Faz:** nomes de bosses e golpes, nomes de mapas/salas ("Calabouços de Armand" etc.), rótulos de sala, nomes de arquétipos/raridades exibidos, mensagens de combate visíveis (ex.: avisos de skill, causa da morte) passam a chaves em `world.json`, com tradução en/es.
- **Pronto quando:** sem literal de mundo visível ao jogador fora de `world.json`; validador passa; build e smoke limpos.
- **Resultado:** 63 chaves de mundo em world.json (bosses e golpes, mapas e regras, raridades/arquétipos/lendários de inimigo, rótulos e marcadores de sala, portal da Câmara, avisos de Fúria, causas de morte em Health.cs). Nomes resolvidos na exibição (DisplayName/Name calculados); Label3D reagem a LanguageChanged; IDs inalterados. Build 0/0, smokes TestArena/CrowsNest/IntermissionChamber limpos, 0 erros do validador no domínio de mundo. Logs de debug mantidos literais.

### T5 — Revisar traduções e acentuação em todos os idiomas
- **Executor:** `game-designer`
- **Faz:** revisa `ui.json`, `content.json` e `world.json` nos três idiomas — **inclusive pt-BR** (acentos, concordância, erros), terminologia consistente (glossário: Bênção de Sangue, Fúria Bestial, Moedas de Sangue, linhagens Dracul/Bathory/Karnstein/Orlok, Stirps, raridades), tom dark fantasy e comprimento compatível com os componentes; corrige o que encontrar.
- **Inclui (de T3/T4):** revisar nomes en/es novos (armas, bênçãos como "Raining Blood", lendários/arquétipos como "Hoja Sombría"/"Orc Brute") e os hints de glossário alterados (Fascínio, Hospedeiro, Interrupção).
- **Pronto quando:** glossário no resultado; validador passa; lista das correções feitas.
- **Resultado:** 904 chaves revisadas nos 3 idiomas; 1 correção (ícone de skill duplicado em pt-BR); glossário de 15 termos em `docs/design/glossario-localizacao.json`. Verificação na retomada (10/10): 0 palavras pt-BR sem acento nas listas de suspeitos, nenhuma chave vazia; igualdades en/es = pt-BR são cognatos/nomes próprios.

### T6 — Verificar a interface em 3 idiomas e 3 resoluções
- **Executor:** `designer-ux-arte`
- **Faz:** capturas de todas as telas via `--ui-preview` com `--ui-lang` em pt-BR/en/es (resoluções 720p/1080p por janela real ou cálculo, já que o Movie Maker grava 1600×900); aponta textos que estouram, se sobrepõem ou aparecem como chave crua.
- **Pronto quando:** relatório com a matriz de capturas e problemas; severidade alta volta para T2–T5.
- **Resultado:** Capturas: 108 PNGs (36 telas x 3 idiomas) geradas em UM processo do Godot via novo modo `--ui-batch` do UiPreview, repetidas depois das correções (0 erro de script, 0 chave ausente). Análise visual PARCIAL: o agente viu 58 das 108 imagens (quase só en/es; limite de API interrompeu duas vezes) e eu conferi mais algumas (Fonte de Sangue pt-BR/en, Ficha pt-BR/en, confirmação da pausa). Sem chave crua, acento quebrado ou estouro nas telas conferidas. Problemas: textos 'Prévia:', DEV, Codex DEV e log de eventos em português (só debug/preview, exceção aceita no plano; acentos do log corrigidos); separador decimal em en usava vírgula (corrigido: cultura da sessão segue o idioma); nomes de nó da Fonte e slot do baú truncados com '...' (igual em pt-BR, pré-existente, nome completo no painel de detalhe); diálogo de confirmação da pausa deixa vazar de leve o texto do menu atrás (visual, pendente). Não vistas: capturas pt-BR em sua maioria, hud_combat/toast em en/es, e textos que só aparecem com interação.

### T7 — Revisão final de código e validação
- **Executor:** `orch:revisor`
- **Faz:** revisão do conjunto das fases 2–4 (regressões, `CallGroup`, pausa, foco, debug-only, alocações por frame, saves antigos); build, validadores (bênçãos, artefatos, localização) e smoke headless das 5 cenas.
- **Pronto quando:** sem achados de severidade alta; comandos verdes.
- **Resultado:** Sem achados de severidade alta. 1 média corrigida (rótulo 'Sala de treinamento' do Ninho fixo em português: chave scene.crows_nest.training_hall + LanguageChanged) e baixas corrigidas: separador decimal agora segue o idioma do jogo (Localization.Format), prompts de portal passam a chave (ScenePortal.ToPromptKey removido) sem o prefixo 'E - ', tooltip da Fúria só reescreve quando muda, GetTunedAttack preserva NameKey. Pendentes baixas (não visíveis ao jogador): fontes de dano literais em PlayerCombat:910 e SummonedRat:66, hint de zoom da Fonte recalculado por quadro de pan. Verificados sem problema: CallGroup/assinaturas, pares += / -= de LanguageChanged, pausa/foco/UiModalStack, debug-only, comparações por ID. Build 0/0, validadores 0/0.

### T9 — Exibir nomes localizados onde a UI ainda usa o nome gravado no save
- **Executor:** `ui-hud`
- **Origem:** pendências 4.1 da T3 (adicionada em 2026-10-06).
- **Faz:** `HUD.ArtifactOffer.cs:259` → `NotifyArtifactCollected(ArtifactCatalog.DisplayNameFor(artifact))`; `HUD.ArtifactChest.cs:153–160, 436, 504` e `HUD.CharacterSheet.cs:639` → `ArtifactCatalog.DisplayNameFor(...)` no texto exibido (comparações por `Name` continuam); `HUD.CharacterSheet.cs:836` → `ArtifactCatalog.TryGetByRecordName`; `HUD.BlessingOffer.cs:376` ("Substitui: {0}") → `replaced.DisplayName`; `UiComponents.cs:1823` (monograma) → nome localizado; remover `WeaponPlaceholderSuffix` de `HUD.WeaponBench.cs` (obsoleto: a T3 já tirou o sufixo das descrições) e o `using System.Text.RegularExpressions` sem uso em `HUD.BlessingOffer.cs`; opcional: prévia do altar em `IntermissionChamber` usar `BloodBlessingText.FormatEffectValue`. Não escreve em `data/localization/` (conflito com T5); se faltar chave, reporta.
- **Pronto quando:** nenhum nome de save/idioma de aquisição exibido; build limpo; validadores passam.
- **Resultado:** nomes de artefato/bênção/arma exibidos pelo catálogo (fallbacks em ArtifactChest/CharacterSheet, monograma em UiComponents); `WeaponPlaceholderSuffix` e usings sem uso removidos. Build 0/0, validadores 0 erros. Prévia do altar (opcional) não alterada.

### T8 — Atualizar documentação da interface e da localização
- **Executor:** `orch:documentador`
- **Inclui (de hud-ingame):** sincronizar no guia §8.3 (secundário "Sala limpa" → "Portal aberto" via `NotifyPortalOpened`), §3.9/§3.14 (contagem do alerta em TextPrimary, host do "+N", leque de números) e §4.2 (painel do jogador ~404 px).
- **Faz:** atualiza a seção de HUD/menus em `docs/gameplay_systems.md`, o README (telas, controles, como adicionar um idioma/texto), o guia com o estado final e o perfil do orch (estrutura de localização, `--ui-preview`, validador novo).
- **Pronto quando:** docs refletem o código final, sem informação inventada.
- **Resultado:** README, docs/gameplay_systems.md, docs/design/ui-guia.md e perfil.md atualizados (localização por idioma, validadores, --ui-preview/--ui-batch, itens da fase 3a, estado verificado); referências a game_texts.json removidas também de .claude/agents/*.md. Divergências do guia corrigidas (OptionsPanel(true) na pausa; previews gallery/confirm/prompt não existem). docs/design/ui-auditoria.md ainda cita game_texts.json (histórico, não alterado).

## Registro de execução

| Quando | Evento |
|---|---|
| 2026-10-04 18:29 | Plano criado |
| 2026-10-04 19:20 | Replanejado pela decisão D5 do usuário: recursos por idioma, conteúdo também localizado, revisão de textos em todos os idiomas (5 → 8 tarefas) |
| 2026-10-06 12:57 | Execução iniciada (orch 0.4.0) · T1 iniciada por `ui-hud` |
| 2026-10-06 13:02 | T1 concluída por ui-hud |
| 2026-10-06 13:02 | T2 iniciada |
| 2026-10-06 13:02 | T3 iniciada |
| 2026-10-06 13:02 | T4 iniciada |
| 2026-10-06 13:16 | T4 concluída por inimigos-e-bosses (1 reenvio para ajustes) |
| 2026-10-06 19:31 | T2 concluída por ui-hud (retomada após limite de uso da API) |
| 2026-10-06 19:32 | T3 concluída por conteudo-run (retomada após limite de uso da API) |
| 2026-10-06 19:32 | Replanejado: T9 adicionada (pendências de UI da T3); sufixo de arma e revisão de nomes en/es incluídos na T5. Execução pausada a pedido do usuário após T2 e T3 |
| 2026-10-06 19:33 | Ajuste: a T3 já removeu o sufixo " Moveset em desenvolvimento." das armas (verificado); item saiu da T5 e a T9 passa a remover a constante obsoleta |
| 2026-10-07 | Retomada (--paralelo 2): T5 mantida como concluída; T6, T7, T9 estavam marcadas concluídas sem evidência (resultado só "delegado", 15–40 s) e voltaram a pendente |
| 2026-10-07 | T9 concluída por ui-hud (_GLOSSARY.json movido para docs/design/) |
| 2026-10-07 | T6 e T7 iniciadas |
| 2026-10-10 22:23 | Retomada: T6/T7 tinham sido cortadas por limite de sessão da API (06/10) e interrompidas pelo usuário (07/10) sem resultado; relançadas. Índice git corrigido; `__pycache__` e `execucoes/` ignorados |
| 2026-10-10 22:39 | T7 concluída por orch:revisor; achados corrigidos pelo orquestrador. Modo em lote (`--ui-batch`) adicionado ao UiPreview: T6 passa a analisar capturas geradas por um único processo do Godot |
| 2026-10-11 05:09 | T6 e T8 concluídas; plano concluído |

## Resultado final

Fase 4 concluída. Localização por idioma (pt-BR/en/es, 905 chaves cada) em data/localization/<idioma>/{ui,content,world}.json; catálogos sem texto, saves antigos migrados; T7 sem achados altos (média e baixas corrigidas); T6 com cobertura visual parcial (ver T6); docs atualizados. Build 0/0; validadores 0 erros/0 avisos; smokes TestArena, RunStartBlessingRoom, CrowsNest, IntermissionChamber e MainMenu limpos. Pendências: diálogo de confirmação da pausa translúcido, fontes de dano literais em PlayerCombat:910/SummonedRat:66, hint de zoom da Fonte por quadro, revisão visual completa em pt-BR. Nada foi commitado.

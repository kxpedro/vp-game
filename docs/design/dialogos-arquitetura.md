# Sistema de diálogo: arquitetura e especificação (T2)

Status: **especificação para implementação**. Nada deste documento existe no código ainda. Origem: plano `20261011-0815-sistema-dialogo` (T2). Este arquivo é autossuficiente: T3 (núcleo), T4 (UI), T5 (conteúdo) e T6 (revisão) o leem sem ver a conversa que o gerou.

Referências lidas e conferidas no código atual (commit `09d0158`): `SaveGameManager.cs`, `CharacterSheet.cs`, `CharacterPlaceholder.cs`, `CrowsNestCrypt.cs`, `InteractPrompt.cs`, `BloodFountain.cs`, `ScenePortal.cs`, `RunManager.cs`, `RunState.cs`, `Localization.cs`, `HUD.cs`, `HUD.BloodFountain.cs`, `HUD.Shop.cs`, `HUD.Pause.cs`, `HUD.Notifications.cs`, `ControllerUiSupport.cs` (inclui `UiModalStack`), `UiComponents.cs`, `UiPreview.cs`, `CodexProgress.cs`, `CampaignCatalog.cs`, `BossCatalog.cs`, `tools/validate_localization.py`, `project.godot`. Desenho inspirado no Hades (`comparativo-hades-sworn.md` §3.9, §3.11, §6.2) e no elenco de `personagens-futuros.md`.

## 0. Decisões em uma página

| Tema | Decisão |
|---|---|
| Escopo | 1 NPC no hub (Odalric, o Cronista) + falas de run. Texto primeiro, sem voz, sem presentes. Escolhas opcionais (2 a 3 opções) com efeito só em flags. |
| Dados | `data/dialogue/*.json` (JSONC, como as bênçãos): só IDs, números e condições. Textos em `data/localization/<idioma>/dialogue.json` (domínio novo, carregado sem mudar C#). |
| Seleção | Filtro por requisitos, depois ordenação por classe (essencial > específica > evergreen), "não vista primeiro", prioridade numérica, mais antiga, sorteio. Falas repetíveis só voltam depois de esgotadas as não vistas e do cooldown. |
| Cadência | Uma conversa por NPC por visita ao hub (esconde o orçamento de falas, como no Hades). |
| Persistência | Um único campo opcional novo em `GameSaveData`: `Dialogue` (objeto). Versão do save continua 2. Saves antigos carregam com valor padrão. |
| Núcleo | Autoload `DialogueManager` + catálogo estático + progresso estático (padrão `CodexProgress`). |
| UI | Conversas de NPC e de boss: modal na `UiModalStack`, **pausa a árvore** (não a música). Barks de run: legenda não modal em `HUD.Dialogue.cs`. |
| Contrato HUD | `CallGroup("hud", "OpenDialogue", id)` e `CallGroup("hud", "ShowDialogueBark", ...)`. O HUD pega o roteiro pronto em `DialogueManager.Instance.CurrentPlayback`. |
| Localização | Chaves `dlg.*` em `dialogue.json` (3 idiomas). Novo `tools/validate_dialogue.py` + 1 linha em `validate_localization.py`. |

## 1. Escopo desta entrega

### Dentro
- Conversas roteirizadas com NPC no hub (Ninho do Corvo), disparadas por interação (`interact`) ou, nas essenciais marcadas, por proximidade.
- Conversas especiais ligadas a boss (antes e depois) e ao retorno ao hub (morte, vitória, desistência).
- Barks: falas curtas de 1 a 2 linhas durante a run, sem pausar o jogo.
- Condições sobre o estado do jogo (lista fechada, seção 3).
- Escolhas opcionais (até 3 por conversa) cujo único efeito são **flags** persistidas; cada escolha pode levar a uma resposta curta (ramo de 1 a 3 linhas, sem novas escolhas).
- Persistência de falas vistas, contadores e flags no save.
- Localização em pt-BR, en e es; navegação por teclado, mouse e controle.
- Retratos **sem assets novos**: placeholder gerado por cor, inicial e moldura, com campo para textura futura.

### Fora (decidido, não é pergunta aberta)
- Voz dublada (o GDD §717 prevê, fica para depois; exige assets e `CREDITS.md`).
- Presentes e relacionamento com pontuação (Hades "Keepsakes/Nectar"). O GDD §296 prevê "relacionamento"; nesta entrega só flags.
- Escolhas que mudam a árvore de conversa de forma complexa (aninhadas, com condição por opção, com recompensa). Diálogo que libera mecânica (aspecto oculto do Hades) fica para quando existirem aspectos/armas.
- NPCs dentro das salas da run (o GDD fala em "personagens ao longo das salas"; aqui só o hub).
- Os 3 personagens desbloqueáveis de `personagens-futuros.md` falando: eles ainda não existem. Odalric só os menciona de passagem, sem fixar identidade (ver seção 9).
- Opção de velocidade do texto e legendas configuráveis (risco registrado na seção 11).
- Câmera/animação de cena, retratos desenhados, SFX de fala.

## 2. Modelo de dados

### 2.1 Arquivos

```
data/dialogue/speakers.json        catálogo de falantes (T3 cria com odalric e virgil; T5 completa)
data/dialogue/odalric.json         conversas do NPC do hub (categorias hub, death, return)
data/dialogue/run.json             barks, boss_pre e boss_post
```

O carregador lê **todos** os `*.json` de `res://data/dialogue/`: `speakers.json` é o catálogo de falantes (raiz `{"speakers":[...]}`); qualquer outro arquivo é lista de conversas (raiz `{"conversations":[...]}`). Autores podem dividir como quiserem. Formato JSONC (comentários e vírgula final permitidos), lido com `JsonCommentHandling.Skip` e `AllowTrailingCommas = true`, como `BloodBlessingCatalog.EnsureLoaded`. `export_filter="all_resources"` já cobre `data/*.json` (mesmo caso de `artifacts.json`).

### 2.2 Falante (`speakers.json`)

```jsonc
{
  "speakers": [
    {
      "id": "odalric",                          // snake_case, estável, único
      "name_key": "dlg.speaker.odalric.name",   // obrigatório; = dlg.speaker.<id>.name
      "kind": "npc",                            // npc | player
      "name_color": "#D9B25A",                  // cor do nome na caixa (opcional; padrão = UiPalette.Gold)
      "portraits": {
        "default": {                            // obrigatório
          "texture": "",                        // futuro: "res://images/portraits/odalric.png". Vazio ou falha ao carregar = placeholder
          "initial": "O",                       // 1 a 2 caracteres latinos, desenhado no placeholder
          "color": "#4A3B5C",                   // fundo do placeholder
          "accent": "#D9B25A"                   // moldura/realce
        },
        "amused": { "accent": "#C9A24A" },      // variantes herdam do default o que não declararem
        "grim":   { "color": "#2E2438", "accent": "#8A2B2B" },
        "sad":    { "accent": "#7C8AA6" }
      }
    },
    {
      "id": "virgil",
      "name_key": "dlg.speaker.virgil.name",
      "kind": "player",
      "portraits": {
        "default": { "texture": "", "initial": "V", "color": "#3A1F24", "accent": "#B03030" },
        "grim":    { "accent": "#7A1F1F" }
      }
    }
  ]
}
```

Regras:
- **Retrato sem assets novos:** o HUD desenha um quadrado arredondado de `color`, com moldura de 3 px em `accent` e a `initial` centralizada em `UiFont.Title` (28), cor `UiPalette.TextPrimary`. Se `texture` não estiver vazio e `ResourceLoader.Load<Texture2D>` funcionar, a textura substitui o desenho (`TextureRect`, `StretchMode = KeepAspectCovered`, mesma moldura). Asset novo exige `SOURCE.md` e linha em `CREDITS.md` (regra do projeto).
- **Resolução da variante** de uma fala: `line.portrait` (nome de variante), senão `line.emotion`, senão `"default"`; se a variante não existir no falante, usa `"default"`. Campos ausentes na variante vêm de `default`.
- `emotion` é fechado: `neutral | grim | amused | angry | sad | afraid | awe`. `neutral` equivale a `default`. `portrait` é livre, mas precisa existir em `portraits` do falante (o validador confere).
- Cores em `#RRGGBB`, lidas com `Color.FromHtml`.

### 2.3 Conversa (`odalric.json`, `run.json`)

Campos (todos em snake_case no JSON):

| Campo | Tipo | Obrigatório | Significado |
|---|---|---|---|
| `id` | string | sim | snake_case, **único e estável** (vai para o save). Proibido `speaker` e `choice` (colidem com prefixos de chave). |
| `speaker` | string | sim | falante principal; vale para as linhas sem `speaker` próprio. Para categorias de NPC, é o NPC dono da conversa. |
| `category` | enum | sim | `hub`, `bark`, `boss_pre`, `boss_post`, `death`, `return` (seção 4.1). |
| `trigger` | enum | só em `bark` | `room_enter`, `room_cleared`, `low_health`, `boss_phase`. |
| `priority_class` | enum | sim | `essential`, `specific`, `evergreen`. |
| `priority` | int 0 a 99 | não (0) | desempate dentro da classe. |
| `repeatable` | bool | não (`false`) | `false` = toca uma vez na vida do slot (`play_once`). |
| `cooldown_visits` | int ≥ 0 | não (4; 6 em bark) | só para `repeatable`: quantas execuções do mesmo *pool* precisam passar desde a última vez (4.3). |
| `chance` | float 0 a 1 | não (1.0) | sorteio por chamada, para quebrar a monotonia (barks usam 0,3 a 0,6). |
| `auto_start` | bool | não (`false`) | só `essential` de NPC: abre sozinha ao chegar perto do NPC. |
| `requirements` | lista de condições | não (`[]`) | todas precisam ser verdadeiras (E). Seção 3. |
| `sets_flags` | lista de string | não | flags gravadas ao **iniciar** a conversa. |
| `lines` | lista de linha | sim (≥ 1; bark ≤ 2) | roteiro principal. |
| `choices` | lista de escolha | não (≤ 3) | oferecidas depois da última linha. |
| `branches` | objeto id → ramo | se houver `next` | respostas às escolhas. |

Linha: `{ "text_key": "...", "speaker": "virgil", "emotion": "grim", "portrait": "..." }`. Só `text_key` é obrigatório. Limites de texto (avisos do validador): linha ≤ 180 caracteres em pt-BR; linha de bark ≤ 90.

Escolha: `{ "id": "who", "text_key": "dlg.choice.<conversa>.<n>", "sets_flags": ["..."], "next": "who_reply" }`. `next` (opcional) é o id de um ramo; sem `next` a conversa termina. `id` único dentro da conversa.

Ramo: `{ "lines": [ ... ], "sets_flags": [ ... ] }` (1 a 3 linhas; sem escolhas dentro; termina a conversa).

Exemplo completo (conversa essencial com escolha e ramos):

```jsonc
{
  "conversations": [
    {
      "id": "first_hub_arrival",
      "speaker": "odalric",
      "category": "hub",
      "priority_class": "essential",
      "priority": 90,
      "repeatable": false,
      "auto_start": true,
      "requirements": [],
      "sets_flags": ["met_odalric"],
      "lines": [
        { "text_key": "dlg.first_hub_arrival.1", "emotion": "amused" },
        { "text_key": "dlg.first_hub_arrival.2", "speaker": "virgil", "emotion": "grim" },
        { "text_key": "dlg.first_hub_arrival.3" }
      ],
      "choices": [
        { "id": "who",  "text_key": "dlg.choice.first_hub_arrival.1", "sets_flags": ["asked_odalric_name"],   "next": "who_reply" },
        { "id": "curt", "text_key": "dlg.choice.first_hub_arrival.2", "sets_flags": ["was_curt_with_odalric"], "next": "curt_reply" }
      ],
      "branches": {
        "who_reply":  { "lines": [ { "text_key": "dlg.first_hub_arrival.who_reply.1" }, { "text_key": "dlg.first_hub_arrival.who_reply.2", "emotion": "grim" } ] },
        "curt_reply": { "lines": [ { "text_key": "dlg.first_hub_arrival.curt_reply.1", "emotion": "amused" } ] }
      }
    },
    {
      "id": "odalric_death_by_boss1",
      "speaker": "odalric",
      "category": "death",
      "priority_class": "specific",
      "priority": 70,
      "repeatable": false,
      "requirements": [
        { "type": "last_run_outcome", "value": "died" },
        { "type": "died_at_boss", "id": "invisible_woman" }
      ],
      "lines": [
        { "text_key": "dlg.odalric_death_by_boss1.1", "emotion": "grim" },
        { "text_key": "dlg.odalric_death_by_boss1.2" }
      ]
    },
    {
      "id": "odalric_bench_book",
      "speaker": "odalric",
      "category": "hub",
      "priority_class": "evergreen",
      "priority": 0,
      "repeatable": true,
      "cooldown_visits": 4,
      "requirements": [ { "type": "weapon", "id": "longsword" } ],
      "lines": [ { "text_key": "dlg.odalric_bench_book.1" }, { "text_key": "dlg.odalric_bench_book.2", "emotion": "amused" } ]
    },
    {
      "id": "bark_low_health_1",
      "speaker": "virgil",
      "category": "bark",
      "trigger": "low_health",
      "priority_class": "evergreen",
      "repeatable": true,
      "cooldown_visits": 6,
      "chance": 0.6,
      "requirements": [ { "type": "in_run", "value": true } ],
      "lines": [ { "text_key": "dlg.bark_low_health_1.1", "emotion": "grim" } ]
    },
    {
      "id": "boss_pre_invisible_woman",
      "speaker": "virgil",
      "category": "boss_pre",
      "priority_class": "specific",
      "priority": 50,
      "repeatable": false,
      "requirements": [ { "type": "boss_in_room", "id": "invisible_woman" } ],
      "lines": [ { "text_key": "dlg.boss_pre_invisible_woman.1", "emotion": "grim" }, { "text_key": "dlg.boss_pre_invisible_woman.2", "speaker": "odalric" } ]
    }
  ]
}
```

Convenção de chaves de texto (a mesma do validador, seção 8):
- linha principal: `dlg.<conversa>.<n>` (n a partir de 1);
- linha de ramo: `dlg.<conversa>.<ramo>.<n>`;
- opção: `dlg.choice.<conversa>.<n>` (n = posição 1-based em `choices`);
- nome de falante: `dlg.speaker.<id>.name`.

### 2.4 Regras de integridade (o carregador e o validador aplicam)
- `id` de conversa, falante, escolha e ramo: `^[a-z][a-z0-9_]*$`, únicos no escopo; conversa duplicada vence a primeira (as demais são ignoradas com `GD.PushError`, igual ao catálogo de bênçãos).
- `speaker` e `lines[].speaker` precisam existir em `speakers.json`.
- `category == bark` exige `trigger`; as demais categorias não podem ter `trigger`.
- `auto_start` só em `essential` e só nas categorias de NPC (`hub`, `death`, `return`).
- `choices` ≤ 3; todo `next` aponta para um ramo existente; ramo sem uso é aviso.
- Erro de uma conversa **desativa só aquela conversa** (`GD.PushError` com o id); o resto carrega.

## 3. Condições suportadas (lista fechada)

Sintaxe: `requirements` é uma lista; cada item é `{ "type": "<nome>", ...parâmetros }`. A lista inteira é E. Faixas numéricas usam `min` e/ou `max` (inclusivos; pelo menos um). Tipo desconhecido ou parâmetro inválido = conversa desativada com `GD.PushError`.

| `type` | Parâmetros | Verdadeira quando | Fonte no código | Novo? |
|---|---|---|---|---|
| `runs_started` | `min`/`max` | nº de runs iniciadas está na faixa | `DialogueProgress.RunsStarted` (incrementa em `RunState.StartRun`) | **sim, persistido** |
| `runs_completed` | `min`/`max` | campanhas vencidas | `DialogueProgress.RunsCompleted` (incrementa no ramo final de `RunState.CompleteBoss`) | **sim, persistido** |
| `deaths` | `min`/`max` | runs perdidas | `DialogueProgress.Deaths` (incrementa em `RunState.FailRun` com run ativa e sem desistência) | **sim, persistido** |
| `boss_defeated` | `id` | boss `id` já foi derrotado alguma vez | `DialogueProgress.IsBossDefeated(id)`, ids do `BossCatalog` (`invisible_woman`, `labyrinth_keeper`, `silver_dragonkin`, `armand`) | **sim, persistido** |
| `boss_met` | `id` | já entrou na sala do boss `id` | `CodexProgress.IsDiscoveredRaw(CodexCategory.Boss, id)` (marcado em `RunManager.ConfigureBossRoom`) | não |
| `boss_in_room` | `id` | run ativa, sala de boss e `BossCatalog.Get(RunState.CurrentMapIndex).Id == id` | `RunState.IsRunActive`, `RunState.IsBossRoom`, `RunState.CurrentMapIndex`, `BossCatalog.Get` | não |
| `map` | `id` | `CampaignCatalog.Get(RunState.CurrentMapIndex).Id == id` (`armands_dungeons`, `blood_garden`, `upper_castle`, `armands_throne`); no hub é sempre o mapa 0 | `RunState.CurrentMapIndex`, `CampaignCatalog` | não |
| `room` | `min`/`max` | `RunState.CurrentRoom` (1-based) na faixa | `RunState.CurrentRoom` | não |
| `boss_room` | `value` (bool) | `RunState.IsBossRoom == value` e run ativa | `RunState` | não |
| `in_run` | `value` (bool) | `RunState.IsRunActive == value` | `RunState.IsRunActive` | não |
| `seen` | `id` | a conversa `id` já foi vista | `DialogueProgress.IsSeen` | **sim, persistido** |
| `not_seen` | `id` | negação de `seen` | idem | idem |
| `flag` | `id` | flag gravada por `sets_flags` | `DialogueProgress.HasFlag` | **sim, persistido** |
| `no_flag` | `id` | negação de `flag` | idem | idem |
| `weapon` | `id` | `WeaponLoadout.EquippedWeaponId == id` (hoje só `longsword`) | `WeaponLoadout.EquippedWeaponId` | não |
| `health_below` | `ratio` (0 a 1) | vida atual / máxima do jogador `< ratio`; falsa se não houver jogador | `Health.CurrentHealth`/`MaxHealth` do nó do grupo `player` (contexto, 3.2) | não |
| `last_run_outcome` | `value`: `died`, `victory`, `abandoned` | a última run terminada **nesta sessão** teve esse desfecho | `DialogueProgress.LastRun` | **sim, volátil** |
| `died_at_boss` | `id` | última run terminou em morte na sala do boss `id` | `DialogueProgress.LastRun` (`MapIndex` + `BossRoom`) | **sim, volátil** |
| `last_run_map` | `id` | a última run terminou no mapa `id` | `DialogueProgress.LastRun.MapIndex` | **sim, volátil** |
| `skill` | `id`, `min_rank` (padrão 1) | `CharacterSheet.GetSkillRank(id) >= min_rank` | `CharacterSheet` | não |
| `cursed_blood` | `min`/`max` | `CharacterSheet.CursedBlood` na faixa | `CharacterSheet.CursedBlood` | não |
| `blessing_owned` | `id` | `RunState.GetBlessingStack(id) > 0` | `RunState` | não |
| `discovered` | `category`, `id` | `CodexProgress.IsDiscoveredRaw(category, id)` (ignora o atalho de debug) | `CodexProgress` | não |
| `not` | `condition` (objeto) | negação de uma condição | — | — |
| `any` | `conditions` (lista) | OU entre condições | — | — |

Idiomas de uso:
- **Primeira vez no hub:** não precisa de condição. Uma conversa `play_once` essencial sem requisitos já é "só na primeira vez". Para existir uma segunda conversa que só vale *depois* da primeira: `{ "type": "seen", "id": "first_hub_arrival" }`.
- **Depois de vencer o boss 1:** `{ "type": "boss_defeated", "id": "invisible_woman" }`.
- **Depois de morrer:** `deaths` `min: 1` **e** `last_run_outcome` `died` (a segunda amarra a fala ao retorno imediato).
- **Arma:** `weapon`. **Vida baixa em bark:** o gatilho `low_health` já garante; `health_below` serve para refinar.

### 3.1 Contadores novos e onde nascem
Todos em `DialogueProgress` (classe estática nova, seção 6.5), no estilo de `CodexProgress`:

| Dado | Persistido? | Muda em |
|---|---|---|
| `RunsStarted` | sim | `RunState.StartRun()` (1 linha no fim: `DialogueProgress.OnRunStarted();`) |
| `RunsCompleted` | sim | `RunState.CompleteBoss()`, ramo da campanha completa, antes dos resets: `DialogueProgress.OnRunCompleted();` |
| `Deaths` | sim | `RunState.FailRun()`, primeira linha: `DialogueProgress.OnRunEnded(IsRunActive, CurrentMapIndex, CurrentRoom, IsBossRoom);` (só conta se a run estava ativa; desistência não conta como morte) |
| `BossesDefeated` | sim | `RunManager.CompleteRoom()`, ramo `RunState.IsBossRoom`: `DialogueProgress.MarkBossDefeated(BossCatalog.Get(RunState.CurrentMapIndex).Id);` |
| `Seen`, `Plays`, `Flags` | sim | `DialogueManager` (iniciar conversa, escolher opção) |
| `LastRun` (desfecho, mapa, sala, sala de boss) | **não** (sessão) | `OnRunEnded`, `OnRunCompleted`; zerado em `OnRunStarted` e em `DialogueProgress.Reset/LoadSaveData` |
| `_abandonPending` | **não** | `DialogueProgress.FlagAbandon()`, chamado pelo HUD ao detectar desistência (6.7) |

Por que `Deaths` entra em `FailRun` e não no `Died` do `Health`: `FailRun` já é o ponto único de fim de run (chamado por `HUD.ContinueAfterGameOver`, o botão Renascer), roda uma vez e tem `CurrentMapIndex/CurrentRoom` antes do reset. `MainMenu.StartSlot` também chama `FailRun`, mas com `IsRunActive == false`, então é ignorado. Limitação aceita: se o jogador fechar o jogo na tela de game over sem confirmar, a morte não é contada.

### 3.2 Contexto de avaliação
`DialogueContext` (record, seção 6.4) leva só o que não é estático: `float PlayerHealthRatio` (−1 = sem jogador). O `DialogueManager` o monta com `GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<Health>("Health")`. O resto o avaliador lê direto de `RunState`, `CharacterSheet`, `CodexProgress`, `WeaponLoadout` e `DialogueProgress`.

### 3.3 Argumentos de formatação dos textos
Todo texto passa por `Localization.TextFormat(key, a0, a1, a2, a3)` com 4 argumentos fixos, que os textos podem usar (`{0}` a `{3}`) ou ignorar:

| Arg | Valor |
|---|---|
| `{0}` | `DialogueProgress.RunsStarted` |
| `{1}` | `DialogueProgress.Deaths` |
| `{2}` | nome do mapa de contexto: `CampaignCatalog.Get(ctxMap).Name` |
| `{3}` | nome do boss do mapa de contexto: `BossCatalog.Get(ctxMap).DisplayName` |

Mapa de contexto (`ctxMap`): com run ativa, `RunState.CurrentMapIndex`; no hub, `DialogueProgress.LastRun.MapIndex` se houver `LastRun`, senão 0. Chaves literais `{` e `}` no texto escrevem-se `{{` e `}}`. O validador exige os mesmos placeholders nos 3 idiomas.

## 4. Algoritmo de seleção e gatilhos

### 4.1 Categorias: onde e como cada uma toca
| Categoria | Entrega | Gatilho (ponto no código) | Pool de cooldown |
|---|---|---|---|
| `hub` | modal, pausa | `DialogueNpc` (interação ou proximidade) → `DialogueManager.TryTalkTo(speakerId)` | `npc` |
| `death` | modal, pausa | idem; só candidata se `LastRun.Outcome == Died` | `npc` |
| `return` | modal, pausa | idem; só candidata se `LastRun` existe e não é `Died` (vitória ou desistência) | `npc` |
| `boss_pre` | modal, pausa | `RunManager.ConfigureBossRoom` → evento `RoomEntered` → 0,6 s depois | `npc` |
| `boss_post` | modal, pausa | `RunManager.CompleteRoom` (ramo boss) → evento `BossDefeated` → 1,6 s depois | `npc` |
| `bark` | legenda, sem pausa | `RoomEntered` (1,2 s), `RoomCleared` (0,8 s), vida baixa (sondagem), `BossPhaseChanged` | `bark` |

Uma NPC só enxerga conversas de `hub`, `death` e `return` cujo `speaker` é o dela. `boss_pre`, `boss_post` e `bark` não dependem de NPC: `speaker` é só o falante principal (Virgil pensando, ou Odalric "ecoando" pelo sangue).

### 4.2 Pontuação e ordenação
Chave de ordenação (comparada nesta ordem, do maior para o menor mérito):

1. **Classe:** `essential` (3) > `specific` (2) > `evergreen` (1).
2. **Não vista primeiro:** nunca vista (`!Seen`) antes das já vistas. É o que garante "repetíveis só voltam depois de esgotadas as não vistas".
3. **`priority`** (maior primeiro).
4. **Mais antiga:** menor valor em `Seen[id]` primeiro (nunca vista = 0).
5. **Sorteio:** `RandomNumberGenerator.Randi()` como último desempate.

### 4.3 Passos exatos (`DialogueSelector.Select`)
Entrada: lista de candidatas (já filtradas por categoria e falante), `DialogueContext`, `RandomNumberGenerator`, `allowFallback`.

1. Para cada candidata `c`, calcular `pool = c.Category == Bark ? "bark" : "npc"`.
2. **Elegibilidade.** `c` é elegível se:
   - todos os `requirements` são verdadeiros (`DialogueConditions.Evaluate`), **e**
   - não foi vista, **ou** (`c.Repeatable` **e** `DialogueProgress.GetPlays(pool) - DialogueProgress.GetSeen(c.Id) >= c.CooldownVisits`).
3. **Chance.** Para cada elegível com `c.Chance < 1`, sortear `rng.Randf() <= c.Chance` uma vez nesta chamada; quem falhar sai do conjunto.
4. Se sobrou alguma, ordenar pela chave de 4.2 e devolver a primeira.
5. **Fallback** (só com `allowFallback`, usado pela NPC): se nada sobrou no passo 4 mas existe candidata com requisitos verdadeiros, `Repeatable` e já vista, devolver a de menor `Seen[id]` (ignora cooldown e chance). Evita NPC muda quando o conteúdo é pouco.
6. Se nem o fallback achou, devolver `null`.

Marcar como vista (`DialogueProgress.MarkSeen(c)`): `Plays[pool] += 1; Seen[c.Id] = Plays[pool];`. `cooldown_visits` é, portanto, "quantas conversas do mesmo pool precisam ter ocorrido depois desta". Com `cooldown_visits = 4`, a fala volta no mínimo na 5ª execução seguinte do pool (depois que as não vistas esgotarem).

### 4.4 Quando marcar como vista
Ao **iniciar** a conversa (em `DialogueManager.CreatePlayback` não-preview) e ao **exibir** o bark. Motivo: evita que uma essencial repita para sempre se a cena trocar no meio; o custo é que pular com Esc conta como vista (aceito e coerente com o Hades). Flags de `sets_flags` da conversa gravam no mesmo momento; flags de escolha e de ramo gravam em `ChooseOption`.

### 4.5 Uma conversa por visita (NPC)
`DialogueNpc` guarda `_talkedThisVisit` (volátil, vive no nó). Depois de iniciar uma conversa com sucesso, o prompt some e o título escurece até a cena do hub ser recarregada (próxima volta de uma run). Isso reproduz "uma conversa encerra o papo até o próximo encontro". `auto_start` também respeita o limite.

### 4.6 Seleção para a NPC (`PeekNpcConversation` / `TryTalkTo`)
```
cats  = { Hub }
if LastRun existe: cats += (LastRun.Outcome == Died) ? { Death } : { Return }
pool  = todas as conversas com Speaker == speakerId e Category em cats
Select(pool, ctx, rng, allowFallback: true)
```
Como `death` e `return` são `specific`, elas passam na frente das `evergreen` de `hub`, mas perdem para `essential` não vistas (história primeiro).

### 4.7 Seleção de bark (`TryBark(trigger)`)
Pré-condições (todas): nenhuma conversa aberta; `!UiModalStack.AnyOpen`; run ativa; jogador vivo; `now - _lastBarkMs >= 18000`; `_barksThisRoom < 2`. Candidatas: `Category == Bark && Trigger == trigger`. `Select(..., allowFallback: false)`. Se devolver algo: `MarkSeen`, `_lastBarkMs = now`, `_barksThisRoom++`, exibir linhas em sequência (seção 6.6).

### 4.8 Boss
- `RoomEntered` em sala de boss: ignora barks `room_enter` e agenda `boss_pre`: `Select(categoria BossPre, allowFallback: false)`. Se nenhuma elegível, segue o combate calado.
- `BossDefeated`: `DialogueProgress.MarkBossDefeated(id)` (já feito pelo `RunManager`) e agenda `boss_post` (mesma seleção, categoria `BossPost`). Se houver outra modal aberta (por exemplo oferta), repete a tentativa a cada 0,5 s por até 10 s e desiste.
- `BossPhaseChanged` (opcional, 1 linha em `BossController`): tenta bark `boss_phase`. Gancho do comparativo §3.11; nenhuma conversa mínima o usa.

### 4.9 Vida baixa
O `DialogueManager` sonda a cada 0,5 s (só com run ativa, árvore não pausada): `ratio = Health.CurrentHealth / Health.MaxHealth` do nó `player`. Dispara o bark `low_health` quando `ratio < 0.3` (mesmo limiar de `HUD.CriticalHealthRatio`) e rearma quando `ratio > 0.5`. Não edita `PlayerController` nem `Health`.

## 5. Persistência

### 5.1 `GameSaveData`: um campo novo opcional
Em `SaveGameManager.cs`, junto de `GameSaveData`:

```csharp
public sealed class GameSaveData
{
    // ...campos existentes inalterados...
    /// <summary>Progresso do dialogo. Ausente em saves anteriores (carrega com os valores padrao).</summary>
    public DialogueSaveData Dialogue { get; set; } = new();
}

public sealed class DialogueSaveData
{
    public int RunsStarted { get; set; }
    public int RunsCompleted { get; set; }
    public int Deaths { get; set; }
    /// <summary>Ids de boss derrotados (BossCatalog).</summary>
    public List<string> BossesDefeated { get; set; } = new();
    /// <summary>Conversa -> valor de Plays[pool] na ultima vez em que foi vista. Presenca = ja vista.</summary>
    public Dictionary<string, int> Seen { get; set; } = new();
    /// <summary>Pool ("npc", "bark") -> execucoes ate agora.</summary>
    public Dictionary<string, int> Plays { get; set; } = new();
    /// <summary>Flags gravadas por sets_flags.</summary>
    public List<string> Flags { get; set; } = new();
}
```

Compatibilidade:
- **Sem bump de versão** (`CurrentSaveVersion` continua 2) e **sem apagar saves**: `ReadSlot` só arquiva saves com `Version` 1 ou menor que a atual; um v2 sem `Dialogue` passa. `System.Text.Json` mantém o inicializador `= new()` quando a propriedade falta no JSON.
- `DialogueProgress.LoadSaveData(DialogueSaveData data)` trata `null` (JSON com `"Dialogue": null`) e coleções `null` como vazias. IDs desconhecidos em `Seen`/`Flags`/`BossesDefeated` são preservados (nunca poda), então remover uma conversa do conteúdo não perde nada.
- **IDs estáveis:** `id` de conversa e de flag vai para o save; renomear = a fala volta a ser "não vista". Regra do projeto para IDs persistidos.
- `SlotInfo`/`GetSlotInfo` não mudam.

### 5.2 Ligação com `CharacterSheet`
- `CreateSaveData()`: `data.Dialogue = DialogueProgress.ToSaveData();`
- `LoadSaveData(data)`: depois de `CodexProgress.LoadSaveData(data.Discoveries);` chamar `DialogueProgress.LoadSaveData(data.Dialogue);`
- `ResetPermanentProgress()`: `DialogueProgress.Reset();` (junto de `CodexProgress.Reset()`).

### 5.3 Quando pedir autosave
Todo mutador de `DialogueProgress` que muda dado persistido chama `SaveGameManager.RequestAutosave()` (já agrupa em 1,5 s e grava de forma atômica): `OnRunStarted`, `OnRunCompleted`, `OnRunEnded` (quando conta morte), `MarkBossDefeated` (se novo), `MarkSeen`, `SetFlag` (se nova). Sem slot ativo (`CurrentSlot <= 0`, por exemplo cena aberta direto) `SaveCurrentSlot` já retorna cedo; nada a tratar.

### 5.4 Volátil × permanente
| Permanente (save) | Volátil (sessão ou run) |
|---|---|
| contadores, `BossesDefeated`, `Seen`, `Plays`, `Flags` | `LastRun`, `_abandonPending`, `_talkedThisVisit` (nó da NPC), `_lastBarkMs`, `_barksThisRoom`, gatilhos agendados, estado do `DialoguePlayback`, RNG |

`RunState` **não** ganha campos de diálogo: a run continua volátil, e o que é histórico mora em `DialogueProgress`.

## 6. Arquitetura C#

Convenções do projeto: sem namespaces, sem `Nullable`, `_camelCase` em campos privados, dados em `sealed record`, catálogos em `static class`, indentação de 4 espaços (a dos arquivos de `scripts/systems/`), `GD.PushError` com try/catch no carregamento, singleton `public static X Instance` atribuído em `_Ready`.

### 6.1 Arquivos novos

| Arquivo | Conteúdo |
|---|---|
| `scripts/systems/dialogue/DialogueDefinition.cs` | enums `DialogueCategory`, `DialogueTrigger`, `DialoguePriorityClass`, `DialogueEvent`; records `DialogueDefinition`, `DialogueLine`, `DialogueChoice`, `DialogueBranch` |
| `scripts/systems/dialogue/SpeakerDefinition.cs` | records `SpeakerDefinition`, `PortraitSpec` |
| `scripts/systems/dialogue/DialogueCatalog.cs` | `static class DialogueCatalog` (carrega e consulta) |
| `scripts/systems/dialogue/DialogueCondition.cs` | `sealed class DialogueCondition` (condição já parseada) + `static class DialogueConditions` (parse e avaliação) + `sealed record DialogueContext` |
| `scripts/systems/dialogue/DialogueProgress.cs` | `static class DialogueProgress` (persistido + volátil) |
| `scripts/systems/dialogue/DialogueSelector.cs` | `static class DialogueSelector` (algoritmo 4.3) |
| `scripts/systems/dialogue/DialoguePlayback.cs` | `sealed class DialoguePlayback` + `sealed record DialogueFrame` |
| `scripts/systems/dialogue/DialogueManager.cs` | `partial class DialogueManager : Node` (autoload) |
| `scripts/systems/DialogueNpc.cs` | `partial class DialogueNpc : Node3D` (NPC interativo do hub) |
| `scripts/ui/HUD.Dialogue.cs` | `partial class HUD` (T4) |

Cada `.cs` precisa do `.cs.uid` ao lado (regra do projeto, `*.uid` é versionado). **Como gerar:** o Godot cria os `.uid` quando o projeto é aberto/importado. Depois de escrever os `.cs`, rodar uma vez `F:/GODOT/App/Godot.exe --headless --path . --import` (comando não verificado neste ambiente; se não gerar, abrir o projeto no editor e fechar) e conferir com `git status` que apareceram: `DialogueDefinition.cs.uid`, `SpeakerDefinition.cs.uid`, `DialogueCatalog.cs.uid`, `DialogueCondition.cs.uid`, `DialogueProgress.cs.uid`, `DialogueSelector.cs.uid`, `DialoguePlayback.cs.uid`, `DialogueManager.cs.uid`, `DialogueNpc.cs.uid` (T3) e `HUD.Dialogue.cs.uid` (T4). Versionar todos junto do código.

### 6.2 Modelos (`DialogueDefinition.cs`, `SpeakerDefinition.cs`)

```csharp
public enum DialogueCategory { Hub, Bark, BossPre, BossPost, Death, Return }
public enum DialogueTrigger { None, RoomEnter, RoomCleared, LowHealth, BossPhase }
public enum DialoguePriorityClass { Evergreen = 1, Specific = 2, Essential = 3 }
public enum DialogueEvent { RoomEntered, RoomCleared, BossDefeated, BossPhaseChanged }

public sealed record DialogueLine(string TextKey, string SpeakerId, string Emotion, string Portrait);
public sealed record DialogueBranch(string Id, DialogueLine[] Lines, string[] SetsFlags);
public sealed record DialogueChoice(string Id, string TextKey, string[] SetsFlags, string NextBranchId);

public sealed record DialogueDefinition(
    string Id, string SpeakerId, DialogueCategory Category, DialogueTrigger Trigger,
    DialoguePriorityClass PriorityClass, int Priority, bool Repeatable, int CooldownVisits, float Chance,
    bool AutoStart, DialogueCondition[] Requirements, string[] SetsFlags,
    DialogueLine[] Lines, DialogueChoice[] Choices, IReadOnlyDictionary<string, DialogueBranch> Branches)
{
    public string Pool => Category == DialogueCategory.Bark ? "bark" : "npc";
}

public sealed record PortraitSpec(string TexturePath, string Initial, Color Color, Color Accent);
public sealed record SpeakerDefinition(string Id, string NameKey, bool IsPlayer, Color NameColor,
    IReadOnlyDictionary<string, PortraitSpec> Portraits)
{
    public string DisplayName => Localization.Text(NameKey);
}
```
`SpeakerId`/`Emotion`/`Portrait` de uma linha vazios significam "herdar" (o falante da conversa e `default`). Records públicos: a T4 os instancia para o preview.

### 6.3 `DialogueCatalog`
```csharp
public static class DialogueCatalog
{
    public static IReadOnlyList<DialogueDefinition> All { get; }                 // chama EnsureLoaded
    public static bool TryGet(string conversationId, out DialogueDefinition definition);
    public static bool TryGetSpeaker(string speakerId, out SpeakerDefinition speaker);
    public static IEnumerable<DialogueDefinition> Where(DialogueCategory category, string speakerId = null, DialogueTrigger trigger = DialogueTrigger.None);
    public static void Reload();                                                 // so debug/selftest
}
```
`EnsureLoaded()` privado (flag `_loaded`, try/catch por arquivo, `GD.PushError` com caminho e mensagem; lista `DirAccess.GetFilesAt("res://data/dialogue")` e ordena como `Localization.LoadLanguage`). Pasta ausente: `GD.PushError` e catálogo vazio (o jogo roda sem diálogo).

### 6.4 `DialogueConditions` e `DialogueContext`
```csharp
public sealed record DialogueContext(float PlayerHealthRatio);   // -1 = sem jogador

public sealed class DialogueCondition { /* Type, Id, Category, Min, Max, Value, Ratio, MinRank, Child, Children */ }

public static class DialogueConditions
{
    public static DialogueCondition Parse(JsonElement element, out string error);   // error != null => conversa desativada
    public static bool Evaluate(DialogueCondition condition, DialogueContext context);
    public static bool EvaluateAll(IReadOnlyList<DialogueCondition> conditions, DialogueContext context);
}
```
O avaliador é um `switch` por `Type` lendo as fontes da tabela da seção 3. Faixas: `min`/`max` ausentes = sem limite.

### 6.5 `DialogueProgress` (estático, padrão `CodexProgress`)
```csharp
public enum RunOutcome { None, Died, Abandoned, Victory }
public sealed record RunEndInfo(RunOutcome Outcome, int MapIndex, int Room, bool BossRoom)
{
    public string BossId => BossRoom ? BossCatalog.Get(MapIndex).Id : "";
}

public static class DialogueProgress
{
    public static int RunsStarted { get; private set; }
    public static int RunsCompleted { get; private set; }
    public static int Deaths { get; private set; }
    public static RunEndInfo LastRun { get; private set; }          // null = nenhuma run terminou nesta sessao

    public static bool IsSeen(string conversationId);
    public static int GetSeen(string conversationId);               // 0 = nunca
    public static int GetPlays(string pool);
    public static void MarkSeen(DialogueDefinition definition);     // 4.3 / 4.4; RequestAutosave
    public static bool HasFlag(string flag);
    public static bool SetFlag(string flag);                        // true se nova; RequestAutosave
    public static bool IsBossDefeated(string bossId);
    public static bool MarkBossDefeated(string bossId);             // true se nova; RequestAutosave

    public static void OnRunStarted();                              // RunsStarted++, LastRun = null, _abandonPending = false
    public static void OnRunCompleted();                            // RunsCompleted++, LastRun = Victory
    public static void OnRunEnded(bool runWasActive, int mapIndex, int room, bool bossRoom);   // !runWasActive => ignora; abandono => Abandoned (sem Deaths++); senao Died, Deaths++
    public static void FlagAbandon();                               // chamado pelo HUD

    public static DialogueSaveData ToSaveData();
    public static void LoadSaveData(DialogueSaveData data);         // null seguro; zera LastRun
    public static void Reset();
}
```

### 6.6 `DialogueManager` (autoload)

**Autoload:** sim, porque guarda estado de sessão (cooldown de bark, gatilhos agendados) que precisa sobreviver às trocas de cena e é chamado de qualquer cena (`RunManager`, `DialogueNpc`). Linha exata para `project.godot`, no fim do bloco `[autoload]` (depois de `GameCursor`, para vir depois de `Localization` e `SaveGameManager`):

```
DialogueManager="*res://scripts/systems/dialogue/DialogueManager.cs"
```

```csharp
public partial class DialogueManager : Node
{
    [Signal] public delegate void ConversationStartedEventHandler(string conversationId);
    [Signal] public delegate void ConversationEndedEventHandler(string conversationId, string choiceId);
    [Signal] public delegate void BarkShownEventHandler(string conversationId);

    public static DialogueManager Instance { get; private set; }
    public DialoguePlayback CurrentPlayback { get; private set; }     // null = nenhuma conversa modal aberta
    public bool IsConversationOpen => CurrentPlayback != null;

    public override void _Ready();                 // Instance = this; ProcessMode = Always; DialogueCatalog.All (forca carga); --dlg-selftest em debug
    public override void _Process(double delta);   // gatilhos agendados, sondagem de vida baixa

    // NPC
    public DialogueDefinition PeekNpcConversation(string speakerId);        // sem efeitos colaterais; null = nada a dizer
    public bool HasPriorityConversation(string speakerId);                  // existe essential/specific nao vista elegivel (marcador "!")
    public bool TryTalkTo(string speakerId);                                // Select + CreatePlayback + CallGroup hud OpenDialogue; false se nao ha HUD/conversa/modal aberta

    // Gatilhos de gameplay (RunManager, BossController)
    public void Notify(DialogueEvent gameEvent, int argument = 0);

    // Execucao
    public bool TryStartConversation(string conversationId);                // forcado (ignora requisitos e cooldown); usado por boss_pre/boss_post apos a selecao
    public DialoguePlayback CreatePlayback(DialogueDefinition definition, bool preview);  // preview=true: nao marca visto, nao grava flags
    public void EndPlayback(DialoguePlayback playback);                     // o HUD chama ao fechar a janela: limpa CurrentPlayback, emite ConversationEnded

    // Textos e retratos (o HUD usa)
    public static string FormatText(string textKey);                        // 3.3
    public static PortraitSpec ResolvePortrait(string speakerId, string variant);   // 2.2
    public void SetRandomSeed(ulong seed);                                  // testes
}
```

Constantes (campos `private const` no `DialogueManager`; ajustáveis pelo game-designer depois):

| Constante | Valor |
|---|---|
| `BarkGlobalCooldownSeconds` | 18 |
| `MaxBarksPerRoom` | 2 |
| `BarkRoomEnterDelay` / `BarkRoomClearedDelay` | 1,2 s / 0,8 s |
| `BossPreDelay` / `BossPostDelay` | 0,6 s / 1,6 s |
| `LowHealthRatio` / `LowHealthRearmRatio` | 0,3 / 0,5 |
| `BarkLineSeconds` | `Clamp(1.8 + 0.045 × caracteres, 2.5, 6.0)` |

Regras de `TryTalkTo`/`TryStartConversation`: recusar se `CurrentPlayback != null`, se `UiModalStack.AnyOpen`, se não houver HUD no grupo `hud`, ou se `IsDead` o jogador. Não marcar nada nas recusas.

Barks em sequência: para cada linha, `GetTree().CallGroup("hud", "ShowDialogueBark", speakerId, speakerName, text, emotion, seconds)` e espera `GetTree().CreateTimer(seconds, false)` (pausa junto com a árvore). O `Notify` agenda com uma lista interna `(due, kind)` processada em `_Process` (não usa `async void`).

**`DialoguePlayback`** (o roteiro que o HUD percorre; a T4 não precisa conhecer JSON nem condições):

```csharp
public sealed record DialogueFrame(
    bool IsChoice, string SpeakerId, string SpeakerName, string Text, string Emotion, string PortraitVariant,
    IReadOnlyList<string> ChoiceTexts, int LineNumber, int LineCount);

public sealed class DialoguePlayback
{
    public DialogueDefinition Definition { get; }
    public bool IsPreview { get; }
    public bool IsFinished { get; }
    public string ChosenChoiceId { get; }                 // "" se nenhuma
    public DialogueFrame Frame { get; }                   // recalculado a cada leitura: acompanha a troca de idioma
    public void Advance();                                // linha seguinte; apos a ultima: quadro de escolha (se houver) ou fim
    public void Choose(int index);                        // so em quadro de escolha: grava flags, vai ao ramo ou termina
    public void Skip();                                   // avanca ate o proximo quadro de escolha ou o fim
}
```
`Frame.Text` já vem formatado (3.3). `LineNumber/LineCount` contam o bloco atual (principal ou ramo), para um indicador "2/4" opcional.

### 6.7 Integração com o jogo (linhas exatas a inserir)

T3 faz estas inserções mínimas (somente adicionar linhas, sem reordenar nem renomear):

| Arquivo | Onde | Linha |
|---|---|---|
| `RunState.cs` | fim de `StartRun()` | `DialogueProgress.OnRunStarted();` |
| `RunState.cs` | `CompleteBoss()`, ramo da campanha completa, antes de `TemporaryArtifacts.Clear();` | `DialogueProgress.OnRunCompleted();` |
| `RunState.cs` | primeira linha de `FailRun()` | `DialogueProgress.OnRunEnded(IsRunActive, CurrentMapIndex, CurrentRoom, IsBossRoom);` |
| `RunManager.cs` | fim de `ConfigureRoom()` | `DialogueManager.Instance?.Notify(DialogueEvent.RoomEntered);` (cobre os dois caminhos: `_Ready` e primeira wave) |
| `RunManager.cs` | `CompleteRoom()`, ramo `RunState.IsBossRoom`, logo depois de `GetTree().CallGroup("audio_manager", "PlayEffect", "boss_defeated");` | `DialogueProgress.MarkBossDefeated(BossCatalog.Get(RunState.CurrentMapIndex).Id); DialogueManager.Instance?.Notify(DialogueEvent.BossDefeated);` |
| `RunManager.cs` | `CompleteRoom()`, ramo não-boss, depois de `GetTree().CallGroup("hud", "NotifyRoomCleared");` | `DialogueManager.Instance?.Notify(DialogueEvent.RoomCleared);` |
| `BossController.cs` | opcional, na troca de fase (linha ~587, junto do `AppendGameLog` de fase) | `DialogueManager.Instance?.Notify(DialogueEvent.BossPhaseChanged, phase);` |
| `CharacterSheet.cs` | `CreateSaveData`, `LoadSaveData`, `ResetPermanentProgress` | seção 5.2 |
| `HUD.Pause.cs` (T4) | `OnPlayerDied`, depois de ler `source` | `if (source == AbandonRunDeathSource) DialogueProgress.FlagAbandon();` |

Observação: `RunManager`, `BossController` e `RunState` pertencem a outros especialistas e a outros planos (`combate-leitura-visual` mexe em combate). As inserções acima são de uma linha, sem mudar assinaturas; quem as aplica deve reler o arquivo antes de editar e preservar o trabalho dos outros planos. `FailRun` roda *depois* de `RunState.CurrentMapIndex` ainda valer, por isso a chamada é a primeira linha.

### 6.8 NPC interativo do hub: `DialogueNpc`

Base no padrão de `CharacterPlaceholder` (geometria provisória criada em `_Ready`, `Area3D` de proximidade) e de `BloodFountain` (prompt via `InteractPrompt`, `ConsumeInteract` no `_Process`).

```csharp
public partial class DialogueNpc : Node3D
{
    [Export] public string SpeakerId { get; set; } = "odalric";
    // Chave de data/localization/<idioma>/dialogue.json; recebe o nome do falante como {0}. Vazio = "dlg.npc.prompt".
    [Export] public string PromptText { get; set; } = "";

    public override void _Ready();       // geometria, ProximityArea, Title (nome), prompt, marcador "!", LanguageChanged
    public override void _ExitTree();    // desassina LanguageChanged
    public override void _Process(double delta);  // ConsumeInteract -> TryTalk; sonda o marcador a cada 0,5 s
}
```
Comportamento:
- **Visual provisório** (sem assets): corpo = `CylinderMesh` (raio base 0,45, topo 0,25, altura 1,5) violeta escuro; cabeça = `SphereMesh` (raio 0,22) em y 1,7; capuz = `CylinderMesh` cônico escuro; um bloco pequeno (corvo) no ombro. Colisão `StaticBody3D` com `CapsuleShape3D` (raio 0,5, altura 1,7) para o jogador não atravessar. Cores no tom do hub (`CharacterPlaceholder`).
- **Título:** `Label3D` com `SpeakerDefinition.DisplayName`, estilizado por `InteractPrompt.StyleTitle` (Gold 48, só aparece a ≤ 9 m). **Prompt:** `InteractPrompt.CreatePrompt(this, Localization.TextFormat(key, name), 2.2f)` e `InteractPrompt.StyleTitlesAndAnchor(this, prompt)`; `PromptText` guarda **chave**, resolvida na exibição e na troca de idioma (`LanguageChanged`), como `BloodFountain.ResolvePromptText`.
- **Área:** `ProximityArea` com `BoxShape3D` (3,2 × 2 × 3,2) centrada no NPC. `BodyEntered`/`BodyExited` do grupo `player` ligam `_isPlayerInside` e a visibilidade do prompt.
- **Interação:** `if (!_isPlayerInside || _talkedThisVisit || !InteractPrompt.ConsumeInteract(this)) return;` → `if (DialogueManager.Instance != null && DialogueManager.Instance.TryTalkTo(SpeakerId)) { _talkedThisVisit = true; esconder prompt; escurecer título; }`.
- **Auto-start:** em `BodyEntered`, se `!_talkedThisVisit` e `PeekNpcConversation(SpeakerId)?.AutoStart == true`, chamar `TryTalkTo` com `CallDeferred` (evita abrir modal dentro do callback de física).
- **Marcador "!":** `Label3D` Gold sobre a cabeça, visível quando `!_talkedThisVisit && HasPriorityConversation(SpeakerId)`; sondar a cada 0,5 s. É a pista de que há história nova.
- **Posição no CrowsNest (sem mexer nos 3 sarcófagos):** instanciado por `CrowsNestCrypt` (que já é um script da cena), em novo método `CreateDialogueNpc()` chamado em `_Ready` logo depois de `CreateCharacterPlaceholders()`:
  `AddChild(new DialogueNpc { Name = "NpcOdalric", SpeakerId = "odalric", Position = new Vector3(-12.6f, 0.0f, 3.2f), RotationDegrees = new Vector3(0.0f, -60.0f, 0.0f) });`
  Justificativa: sala oeste (x de −15 a −5), junto da Bancada de Armas (−10, 0, 0) e do `WeaponLoreBook`; os sarcófagos estão na sala leste (x = 12,9). A `ProximityArea` (raio efetivo 1,6 m) fica em z de 1,6 a 4,8, fora da `WeaponBenchArea` (caixa 2,8 × 2,4 em z de −1,2 a 1,2) e longe dos barris (−7,7, −2,8) e das tochas. A rotação de −60° em Y vira a frente para leste-nordeste, na direção do centro do hub. Nenhum `.tscn` precisa mudar.
- **Preview do hub** (`UiPreview` tela `hub_npc`, T4): posicionar o jogador em `(-10.0, y, 4.4)`, de frente para o NPC.

## 7. UI (para a T4)

### 7.1 Comportamento
- **Conversas modais** (`hub`, `death`, `return`, `boss_pre`, `boss_post`): `UiModalStack.Push(_dialogueOverlay, pauses: true, initialFocus: <botão de avançar>, onCancel: SkipDialogue, mandatory: false, pausesAudio: false)`. Pausa a árvore (o jogador não se move nem ataca, os inimigos congelam) e **mantém a música**. `IsGameplayInputBlocked()` já cobre via `UiModalStack.AnyOpen`; `InteractPrompt.ConsumeInteract` ignora o aperto que fechou a janela (guarda de 1 frame existente).
- **Barks** (`ShowDialogueBark`): **não modais**. Legenda no rodapé central, acima da `BlessingHudList` (que ocupa `OffsetTop = -72` a `-SafeMargin`): painel `HudPanel` de ~640 px de largura, retrato de 40 px à esquerda, nome em Gold, texto `Body`; ancorado em `AnchorBottom = 1`, `OffsetBottom = -(72 + SafeMargin)`. Entra em fade (`UiMotion.OverlayIn`), fica `seconds`, sai em fade. Uma por vez: nova linha substitui a atual. Vive dentro de `_combatHudLayer` (some junto com pausa/compêndio/game over). `MouseFilter = Ignore`. Não rouba foco.
- A janela de conversa **não** usa `UiComponents.Window` (que é janela central com scrim 0,85). Layout próprio, docado embaixo, para mostrar o cenário 3D atrás:
  - `ColorRect` de tela cheia `Name = "DialogueOverlay"`, `Color = new Color(UiPalette.Scrim, 0.35f)`, `MouseFilter = Stop` (desvio consciente do "scrim único" do guia; registrar no `ui-guia.md`);
  - caixa `PanelContainer` ancorada embaixo-centro, largura 1100, altura mínima 220, margem `SafeMargin` (24) da borda;
  - retrato 160 × 160 à esquerda, subindo ~40 px acima da caixa; nome do falante em `UiVariation.SectionGoldLabel` (22) com a cor `NameColor`; texto em ~22 px (`UiFont.Section`) com `AutowrapMode.WordSmart`;
  - indicador "avançar" (um `▼` ou glifo `UiAction.Accept`) piscando no canto inferior direito quando a linha termina de aparecer;
  - rodapé de dicas com `UiComponents.HintBar()`: `UiAction.Accept` "Continuar" e `UiAction.Cancel` "Pular" (os glifos já trocam por dispositivo via `UiInput.DeviceChanged`).

### 7.2 Texto progressivo
- `Label.VisibleCharacters` incrementado em `HUD._Process` (o HUD é `ProcessMode.Always`, então roda com a árvore pausada), **45 caracteres/s**, +0,15 s de pausa depois de `. ! ? …`. Não usar `Tween` com `Pausable`.
- Pressionar avançar **com o texto ainda aparecendo** completa a linha na hora; pressionar de novo vai à próxima. Linha completa: mostra o indicador.
- Ao receber `LanguageChanged` (o HUD já assina em `OnLanguageChanged` → `ApplyLanguage`): chamar um `RefreshDialogueLanguage()` que relê `playback.Frame` e mostra o texto **completo** (sem recomeçar a digitação). A janela de opções só abre pela pausa, então na prática isso só ocorre no preview/lote.

### 7.3 Entradas
| Ação | Entrada (já existentes) | Efeito |
|---|---|---|
| Avançar / completar | clique esquerdo na caixa; botão "avançar" focado com `ui_accept` (Enter, Espaço, A no controle); `interact` (E / A) via atalho de modal | `playback.Advance()` ou completa a linha |
| Pular | `ui_cancel` (Esc / B / Círculo) via `onCancel` da pilha | `playback.Skip()`: avança até o próximo quadro de escolha ou o fim, e então fecha. Num quadro de escolha, `ui_cancel` não faz nada (a escolha é obrigatória) |
| Escolher | `ui_up`/`ui_down` e `ui_accept`; clique | `playback.Choose(index)` |

Cuidados:
- Para o `ui_accept` funcionar sem `Control` de texto focado, manter um `Button` plano de tela cheia na caixa (`Name = "DialogueAdvanceButton"`, `FocusMode.All`, estilo transparente) como `initialFocus`; ele dispara `Advance` em `Pressed`. O atalho `interact` entra por `RegisterModalShortcuts(_dialogueOverlay, HandleDialogueShortcut)`; como o `Button` consome `ui_accept` antes do `_UnhandledInput`, **não há avanço duplo**. Conferir que `interact` e `ui_accept` não disparam as duas vezes no botão A do controle (os dois mapeiam o botão 0): o `Button` consome o `ui_accept` e o evento não chega ao atalho.
- No quadro de escolha, esconder o botão de avançar e dar foco ao primeiro botão de opção. Botões com `UiComponents.MenuButton(text, onPressed, ButtonVariant.Secondary, ButtonSize.Medium)`, empilhados num `VBoxContainer` à direita acima da caixa, e `ControllerUiSupport.SetVerticalCycle` para o d-pad.
- Esc/`pause` não abre a pausa enquanto a conversa está aberta: o `ui_cancel` é consumido pela pilha (`CancelTop`) em `HUD._Input`.

### 7.4 Contrato com o HUD (nomes EXATOS; chamados por string, **não renomear**)

```csharp
// Chamado via CallGroup("hud", "OpenDialogue", conversationId) por DialogueManager.TryTalkTo / TryStartConversation.
public void OpenDialogue(string conversationId)
// Chamado via CallGroup("hud", "ShowDialogueBark", speakerId, speakerName, text, emotion, seconds).
public void ShowDialogueBark(string speakerId, string speakerName, string text, string emotion, float seconds)
// Chamado via CallGroup("hud", "HideDialogueBark") ao abrir uma conversa modal e na troca de cena.
public void HideDialogueBark()
```

`OpenDialogue(id)`:
1. `var playback = DialogueManager.Instance?.CurrentPlayback;` Se for nulo ou `playback.Definition.Id != id`, ignorar com `GD.PushWarning`.
2. `HideDialogueBark()`; montar o primeiro quadro a partir de `playback.Frame`; `UiModalStack.Push(...)` (7.1); `FadeIn` curto.
3. A cada avanço, ler `playback.Frame` de novo. Quando `playback.IsFinished`: `UiModalStack.Pop(_dialogueOverlay)` e **`DialogueManager.Instance.EndPlayback(playback)`** (devolve o resultado: o `ChosenChoiceId` fica no playback e o gerenciador emite `ConversationEnded(id, choiceId)`).
4. Se a janela for fechada por outro caminho (troca de cena, game over: `UiModalStack.Clear()`), ligar em `TreeExiting`/`VisibilityChanged` para também chamar `EndPlayback` uma única vez. `EndPlayback` é idempotente.

Pontos a editar, todos na T4: `HUD.StatusBars.cs` (chamar `BuildDialogueOverlay(root)` depois de `BuildShopOverlay(root)`), `HUD.cs` (`RebuildOverlaysForLanguage` e `OpenPreviewScreen`), `HUD.Pause.cs` (`HideOverlaysForGameOver` esconde `_dialogueOverlay` e o bark; `OnPlayerDied` com `FlagAbandon`), `UiPreview.cs`, `ui.json` e o `ui-guia.md`.

### 7.5 Preview e lote
Telas novas (nome → cena → o que mostra), todas com dados fictícios e **nada gravado**:

| Tela | Cena | Conteúdo |
|---|---|---|
| `dialogue` | `CrowsNest` | conversa de 3 linhas do falante `odalric`, texto totalmente revelado (no preview não há digitação, para a captura do lote ser estável) |
| `dialogue_choice` | `CrowsNest` | último quadro de uma conversa com 2 opções, a primeira com foco |
| `dialogue_bark` | `TestArena` | bark de `virgil`; repete a cada 3,0 s como `toast` (`_repeatInterval`) |
| `hub_npc` | `CrowsNest` | só o cenário, com o jogador diante do NPC (6.8) |

Montagem: o preview constrói `DialogueDefinition` em memória (records públicos), com `TextKey` em `ui.dialogue.preview.*` (chaves em `ui.json`, texto de interface, **não** em `dialogue.json`, para o preview não depender do conteúdo da T5) e `DialogueManager.Instance.CreatePlayback(definition, preview: true)`. Acrescentar as 4 telas em `UiPreview.BatchScreens` (e no comentário do cabeçalho de `UiPreview.cs`), em `HUD.OpenPreviewScreen` e, para `dialogue_bark`, no `switch` de `_repeatInterval` em `UiPreview.Open`. Comando de verificação:
`F:/GODOT/App/Godot.exe --path . --resolution 1600x900 -- --ui-batch --ui-shots=C:/tmp/shots_dlg --ui-screens=dialogue,dialogue_choice,dialogue_bark,hub_npc`
(3 idiomas por padrão = 12 PNGs). Conferir que a caixa não corta texto em en/es (textos mais longos) e que o retrato placeholder aparece.

## 8. Localização

### 8.1 Como o jogo e o validador descobrem os arquivos
- `Localization.LoadLanguage` (`Localization.cs`) lista **todos** os `*.json` de `res://data/localization/<idioma>/` e os mescla (avisa em debug de chave duplicada). Portanto `dialogue.json` é carregado **sem nenhuma mudança em C#**.
- `tools/validate_localization.py` monta `tables[idioma][domínio]` com todo `*.json` da pasta e cobra paridade de chaves e de placeholders por domínio, e que o domínio exista nos 3 idiomas. Também funciona sem mudança para o arquivo novo.
- Única mudança no validador existente: o aviso "chave sem referência literal em scripts/" dispararia para todas as chaves `dlg.*` (são lidas dinamicamente por `text_key`). Acrescentar `"dlg."` a `DYNAMIC_PREFIXES` (linha 20), como `"blessing."`/`"portal."`.
- Chaves literais no C# (`Localization.Text("dlg.npc.prompt")` etc.) precisam existir em pt-BR, regra já aplicada.

### 8.2 `tools/validate_dialogue.py` (novo, T3)
Mesmo estilo de `validate_artifacts.py`: só biblioteca padrão, raiz em `Path(__file__).resolve().parents[1]`, sai com 1 se houver erro. Verifica:
- **Erros:** JSON/JSONC inválido (remover comentários e vírgulas finais antes de `json.loads`); id inválido ou duplicado; `speaker` inexistente; `category`/`trigger`/`priority_class`/`emotion` fora dos enums; `trigger` ausente em bark ou presente fora de bark; `type` de condição desconhecido ou parâmetros faltando; `id` de boss fora de `scripts/bosses/BossCatalog.cs` (regex `new\("([a-z_]+)",\s*\d`); `map` fora de `scripts/systems/CampaignCatalog.cs`; `choices` > 3; `next` sem ramo; `portrait` inexistente no falante; todo `text_key`/`name_key` ausente em `dialogue.json` de **algum** dos 3 idiomas.
- **Avisos:** linha > 180 caracteres (bark > 90) em pt-BR; chave fora da convenção da 2.3; chave em `dialogue.json` sem uso nos JSON de `data/dialogue/`; ramo sem uso; conversa `essential` sem `repeatable: false`; `auto_start` fora do permitido.

### 8.3 Convenção de chaves e conteúdo de `dialogue.json`
| Chave | Uso |
|---|---|
| `dlg.speaker.<id>.name` | nome do falante (`odalric` = "Odalric", `virgil` = "Virgil") |
| `dlg.<conversa>.<n>` | linha principal |
| `dlg.<conversa>.<ramo>.<n>` | linha de ramo |
| `dlg.choice.<conversa>.<n>` | opção de escolha |
| `dlg.npc.prompt` | prompt do NPC, `"Falar com {0}"` (T3 cria; usada em `DialogueNpc`) |

`dialogue.json` é JSON plano `"chave": "texto"` em `data/localization/{pt-BR,en,es}/dialogue.json`, ordenado por conversa. Placeholders: só `{0}` a `{3}` (3.3), iguais nos 3 idiomas. Não colocar referência musical em texto visível (regra do projeto).

## 9. Conteúdo mínimo (para a T5)

### 9.1 NPC do hub: Odalric, o Cronista (`odalric`)
**Proposta e justificativa.** Os três personagens desbloqueáveis de `personagens-futuros.md` (Selina, Magdalena, Dominicus) estão presos e só despertam depois dos bosses, então não podem ser o NPC desta entrega, e fixar a voz deles agora comprometeria um design que ainda tem 12 perguntas em aberto. O hub precisa de alguém presente desde a primeira visita, neutro entre os arcos futuros e que explique a lore das linhagens. Proposta: **Odalric, o Cronista**, antigo arquivista da corte de Armand, preso ao Ninho do Corvo por um pacto de tinta, que registra cada tentativa de Virgil num livro e que, por ofício, conhece as quatro linhagens. Ele fica junto da Bancada de Armas e do `WeaponLoreBook` já existente no hub.

Voz: seco, irônico, cansado, escreve para ter um último capítulo ("preciso ver como a história de Armand acaba"). Chama Virgil de "caçador" e, a partir do boss 1, de "Virgil". Fala em frases curtas. Pode aludir a "três leitos selados" no leste do hub sem dizer quem são (compatível com os placeholders e com o que `personagens-futuros.md` deixa em aberto).

Falantes: `odalric` (kind `npc`) e `virgil` (kind `player`).

Lore a respeitar: Virgil, caçador de recompensas recém-transformado em vampiro, foge da maldição de Armand; linhagens (Dracul = domínio/roubo de vida, Bathory = poças/cura/sacrifício, Karnstein = sedução/agilidade, Orlok = doença/controle/risco); bosses por mapa (Guardiã Invisível, Guardião do Labirinto, Dragonkin Prateado, depois Armand). Tom dark fantasy, flavor curto: 2 a 5 linhas por conversa.

### 9.2 Lista de conversas (ids estáveis, a T5 pode acrescentar mas não renomear)

| id | categoria | classe / prioridade | repete? | Requisitos | Intenção |
|---|---|---|---|---|---|
| `first_hub_arrival` | `hub` | essential / 90 | não, `auto_start` | — | Primeira vez no hub. Odalric apresenta o Ninho, a maldição de Armand e a Fonte; Virgil responde seco. Escolha: "Quem é você?" / "Não preciso de guia." (flags `asked_odalric_name`, `was_curt_with_odalric`; ramo de 1 a 2 linhas cada). `sets_flags`: `met_odalric` |
| `odalric_lineages_a` | `hub` | evergreen / 0 | sim, cooldown 4 | `seen first_hub_arrival` | Dracul e Bathory: domínio e roubo de vida; poças, cura, sacrifício |
| `odalric_lineages_b` | `hub` | evergreen / 0 | sim, cooldown 4 | `seen first_hub_arrival` | Karnstein e Orlok: sedução e agilidade; doença, controle e risco |
| `odalric_armand_rumor` | `hub` | evergreen / 0 | sim, cooldown 4 | `seen first_hub_arrival` | Boatos sobre Armand e a origem da maldição, sem revelar demais |
| `odalric_bench_book` | `hub` | evergreen / 0 | sim, cooldown 4 | `weapon longsword` | O livro de armas na bancada; a espada de Virgil |
| `odalric_after_first_boss` | `hub` | specific / 60 | não | `boss_defeated invisible_woman` | Comentário sobre a Guardiã Invisível. Pode sugerir parentesco e lua/gato sem confirmar nada (a ligação com Selina em `personagens-futuros.md` §3.1 ainda é hipótese) |
| `odalric_first_death` | `death` | specific / 50 | não | `deaths min 1`, `last_run_outcome died` | Primeira morte: a crônica registra, "o sangue não esquece" |
| `odalric_death_by_boss1` | `death` | specific / 70 | não | `last_run_outcome died`, `died_at_boss invisible_woman` | Caiu diante da Guardiã Invisível: dica de leitura do golpe, tom irônico |
| `odalric_return_victory` | `return` | specific / 40 | não | `last_run_outcome victory` | Opcional (campanha completa). Só texto curto; se a T5 não escrever, remover do JSON |
| `bark_room_enter_1` | `bark` / `room_enter` | evergreen | sim, cooldown 6, chance 0,35 | `in_run true` | Virgil, corredor de pedra, frio, cheiro de ferro |
| `bark_room_enter_garden` | `bark` / `room_enter` | evergreen | sim, cooldown 6, chance 0,5 | `map blood_garden` | Virgil, jardim, sangue nas rosas |
| `bark_room_cleared_1` | `bark` / `room_cleared` | evergreen | sim, cooldown 6, chance 0,3 | `in_run true` | Virgil, alívio curto, "mais um passo" |
| `bark_low_health_1` | `bark` / `low_health` | evergreen | sim, cooldown 6, chance 0,6 | `in_run true` | Virgil: "Ainda não. Não aqui." |
| `bark_low_health_2` | `bark` / `low_health` | evergreen | sim, cooldown 6, chance 0,6 | `in_run true` | Variante com a voz de Odalric ecoando pelo sangue |
| `boss_pre_invisible_woman` | `boss_pre` | specific / 50 | não | `boss_in_room invisible_woman` | 2 linhas (Virgil e Odalric) antes da luta |
| `boss_pre_generic` | `boss_pre` | evergreen / 0 | sim, cooldown 4 | `boss_room true` | 1 linha de Virgil, serve a qualquer boss sem fala própria |

Isso cobre o pedido: 1 essencial, 3 a 4 evergreen (aqui 4), 2 a 3 específicas (aqui 3, mais 1 opcional), 4 a 6 barks de run (aqui 5, mais 2 de boss).

### 9.3 Guia de escrita (T5 detalha em `docs/design/dialogos.md`)
- Uma ideia por linha; linhas de NPC ≤ 180 caracteres; barks ≤ 90.
- pt-BR primeiro; en e es depois, com paridade de chaves e de placeholders. Termos de lore seguem `docs/design/glossario-localizacao.json`.
- Sem referência musical visível; sem gíria datada; sem repetir a mesma piada em evergreens vizinhas.
- Cada evergreen deve funcionar fora de ordem (o jogador pode ouvi-las em qualquer sequência).

## 10. Divisão de arquivos e ordem de integração

| Tarefa | Executor | Escreve (exclusivo) |
|---|---|---|
| **T3** núcleo | `conteudo-run` | `scripts/systems/dialogue/**` (+ `.cs.uid`), `scripts/systems/DialogueNpc.cs` (+ `.cs.uid`), `scripts/systems/SaveGameManager.cs` (`DialogueSaveData`, `GameSaveData.Dialogue`), `scripts/systems/CharacterSheet.cs`, `scripts/systems/RunState.cs` e `scripts/systems/RunManager.cs` (só as linhas da 6.7), `scripts/bosses/BossController.cs` (1 linha opcional), `scripts/systems/CrowsNestCrypt.cs` (`CreateDialogueNpc`), `project.godot` (1 linha de autoload), `tools/validate_localization.py` (1 linha), `tools/validate_dialogue.py` (novo), `data/dialogue/speakers.json` e uma **semente** em `data/dialogue/odalric.json` (`first_hub_arrival` e `odalric_bench_book`, com o texto mínimo "provisório"), `data/localization/{pt-BR,en,es}/dialogue.json` (semente: `dlg.speaker.*`, `dlg.npc.prompt` e as chaves da semente) |
| **T4** UI | `ui-hud` | `scripts/ui/HUD.Dialogue.cs` (+ `.cs.uid`), `scripts/ui/HUD.cs`, `scripts/ui/HUD.StatusBars.cs`, `scripts/ui/HUD.Pause.cs`, `scripts/ui/UiPreview.cs`, `data/localization/*/ui.json` (`ui.dialogue.*`), `docs/design/ui-guia.md` (nota da tela de diálogo) |
| **T5** conteúdo | `game-designer` | `data/dialogue/odalric.json`, `data/dialogue/run.json`, `data/dialogue/speakers.json` (completar), `data/localization/*/dialogue.json`, `docs/design/dialogos.md`. Pode substituir o texto da semente da T3, **mantendo os ids** `first_hub_arrival` e `odalric_bench_book` |

Ajustes em relação ao plano original: (a) a T3 também cria o catálogo de falantes e uma semente mínima de conversas e textos, para o fluxo ponta a ponta funcionar antes de a T5 escrever; (b) `HUD.StatusBars.cs` e `HUD.Pause.cs` entram na T4 (o plano só citava `HUD.cs`); (c) a T3 ganha as poucas linhas em `RunState`, `RunManager` e `BossController` e `project.godot`; (d) `tools/validate_dialogue.py` é novo.

**Ordem de integração:** T2 → **T3** → (**T4** ∥ **T5**) → T6. T4 e T5 só começam depois da T3 publicar a API da seção 6. T4 e T5 não escrevem nos mesmos arquivos; T5 não toca `ui.json` e T4 não toca `dialogue.json`. Conflitos possíveis: nenhum entre T4 e T5. Entre T3 e outros planos (`combate-leitura-visual`, `revisao-graficos`) existe risco só em `RunManager.cs`/`BossController.cs`/`HUD*.cs`; ver o aviso da 6.7.

## 11. Plano de verificação e riscos

### 11.1 Verificação (por tarefa)
**T3 (depois de implementar):**
1. `dotnet build VP0.1.sln --no-restore` → 0 erros, 0 avisos.
2. `py tools/validate_dialogue.py` → 0 erros; `py tools/validate_localization.py` → 0 erros (a contagem de chaves por idioma sobe; o aviso de chave sem referência não pode listar `dlg.*`).
3. `py .codex/skills/vp-create-blood-blessing/scripts/validate_blessings.py` e `.../validate_artifacts.py` seguem sem erros (regressão).
4. Smoke headless: `F:/GODOT/App/Godot.exe --headless --path . --quit-after 180 res://scenes/game/TestArena.tscn` e `--quit-after 120 res://scenes/game/CrowsNest.tscn`: sem `ERROR`/`SCRIPT ERROR`, sem "autoload" quebrado.
5. **Autoteste do algoritmo** (debug): `DialogueManager._Ready` aceita o argumento de usuário `--dlg-selftest` e roda o caso abaixo com definições em memória (progresso resetado antes e depois; sem slot ativo nada é gravado), imprime `DLG-SELFTEST PASS/FAIL <passo>` e sai com `GetTree().Quit()`:
   `F:/GODOT/App/Godot.exe --headless --path . -- --dlg-selftest`
6. Save: gravar um slot com progresso, conferir `Dialogue` no `save_slot_N.json` e que um save v2 *sem* o campo carrega (editar o JSON à mão em `user://`, remover `"Dialogue"`, iniciar o slot: contadores 0, nenhuma exceção).

**T4:** build limpo; `py tools/validate_localization.py` sem erro; `--ui-batch` com `--ui-screens=dialogue,dialogue_choice,dialogue_bark,hub_npc` nos 3 idiomas (12 PNGs, saída 0); abrir o hub, falar com o NPC e verificar pausa, retorno do foco, `ui_cancel` pulando, glifos por dispositivo (`--ui-device=xbox`).

**T5:** `py tools/validate_dialogue.py` e `py tools/validate_localization.py` sem erro; paridade de chaves; amostra de leitura nos 3 idiomas.

**T6:** regressão de save antigo; conversa não trava o jogo nem o input ao fechar (nem por Esc, nem por troca de cena); morrer com o bark na tela; abrir conversa com oferta aberta (deve recusar sem travar); `jogar.bat` até o boss 1.

### 11.2 Caso de teste manual do algoritmo (base do autoteste)
Estado inicial: save novo (`Seen = {}`, `Plays = {}`, contadores 0). Conversas do 9.2 carregadas. RNG com semente fixa (`SetRandomSeed(1)`).

| # | Ação | Esperado |
|---|---|---|
| 1 | `PeekNpcConversation("odalric")` | `first_hub_arrival` (essencial, não vista) |
| 2 | `TryTalkTo("odalric")`, fechar | `Seen[first_hub_arrival] = 1`, `Plays[npc] = 1`, flag `met_odalric` gravada; escolher "curt" grava `was_curt_with_odalric` |
| 3 | Nova visita (nó novo): `PeekNpcConversation` | uma das evergreen elegíveis não vistas (`odalric_lineages_a`, `_b`, `armand_rumor`, `bench_book`); nunca `first_hub_arrival` |
| 4 | Jogar 4 visitas seguidas | as 4 evergreen aparecem uma vez cada, sem repetição (não vistas primeiro) |
| 5 | Visita 6 (cooldown 4) | só volta a ser candidata a que tem `Plays[npc] − Seen[id] ≥ 4`; a mais antiga vence o desempate |
| 6 | `RunState.StartRun(); RunState.FailRun();` (sem desistência) | `RunsStarted = 1`, `Deaths = 1`, `LastRun = Died` |
| 7 | Visita seguinte | `odalric_first_death` (specific) passa na frente de qualquer evergreen |
| 8 | Marcar `MarkBossDefeated("invisible_woman")`, nova visita após a 2ª morte no boss | `odalric_death_by_boss1` (prio 70) antes de `odalric_after_first_boss` (prio 60) e de `first_death` (já vista) |
| 9 | Recarregar o slot do disco | `Seen`, `Plays`, `Flags`, `BossesDefeated`, contadores iguais; `LastRun = null` |
| 10 | Bark `low_health` com `chance 0.6`, 20 sorteios com semente fixa | proporção ~60% (tolerância ±25%); respeita 18 s globais e máx. 2 por sala |
| 11 | Remover `"Dialogue"` do JSON do save e carregar | defaults, sem exceção |

### 11.3 Riscos e pontos de atenção
| Risco | Mitigação |
|---|---|
| `CallGroup` por string quebra em silêncio se um método for renomeado | nomes da 7.4 fixados e citados no código com comentário "não renomear" (padrão do `OpenShop`) |
| Modal abrindo sobre outra modal (oferta, ficha) | `TryTalkTo` recusa se `UiModalStack.AnyOpen`; boss_pre/post repetem a tentativa por até 10 s |
| `boss_pre` pausando com o boss já agindo | atraso de 0,6 s e a sala de boss só inicia o ataque depois do telegraph; se o boss tiver acertado o jogador antes da pausa, aumentar `BossPreDelay` ou usar invulnerabilidade curta (decisão do T6) |
| Conversa não marcada se o jogo fechar logo após iniciar | `RequestAutosave` agrupa em 1,5 s; `FlushPendingSave` já roda em `_ExitTree`/fechar janela |
| Texto em en/es mais longo estoura a caixa | limites do validador (180/90) e conferência no `--ui-batch` |
| `Deaths` não conta se o jogador fechar o jogo na tela de game over | aceito (3.1) |
| Desistência marcada só pelo HUD | se a T4 esquecer o `FlagAbandon`, a desistência vira "morte" (inofensivo, apenas perde a distinção) |
| Autoload com estado estático e cena aberta sem HUD (testes) | `TryTalkTo` devolve `false` sem HUD e não marca nada |
| `.cs.uid` ausente | seção 6.1: gerar com `--import`/editor e conferir `git status` |
| Saves antigos mostram `first_hub_arrival` mesmo com muito progresso | aceito (é uma apresentação de lore, vista uma vez); se incomodar, a T5 pode exigir `runs_started max 0` |
| Acessibilidade: sem opção de velocidade do texto | fora do escopo; ao completar a linha com avançar o jogador já controla o ritmo. Registrar para plano de acessibilidade |
| Fala de Odalric sobre sarcófagos/Guardiã pode contradizer o design futuro | T5 mantém as alusões vagas; `personagens-futuros.md` segue como fonte para o arco real |

## 12. Divergências e observações

- **GDD (§296, §717, §887):** prevê NPCs *ao longo das salas*, relacionamento e diálogos **dublados**. Esta entrega faz NPC só no hub, sem relacionamento e sem voz. Proposta: manter o GDD como visão e registrar a fase atual em `dialogos.md`; atualizar o GDD só se o escopo de presentes/voz for adotado.
- **`comparativo-hades-sworn.md` §6.2:** o plano cita "falas de run curtas na entrada de sala e na fase do boss"; atendido por `bark`/`boss_phase` (gancho opcional) e `boss_pre`.
- **`CodexProgress.MarkBoss` não significa "derrotado":** é marcado ao *entrar* na sala do boss (`RunManager.ConfigureBossRoom`). Por isso `boss_defeated` precisa de contador próprio (`DialogueProgress.BossesDefeated`) e `boss_met` fica como condição separada.
- **`RunState.FailRun` também é chamado fora de morte** (`MainMenu.StartSlot`, com run inativa): o filtro `runWasActive` em `DialogueProgress.OnRunEnded` resolve.
- **Texto de morte do HUD usa nome de ataque** (`Health.LastDamageSource` é string de exibição, por exemplo `attack.Name`): não serve para saber *qual boss* matou. A causa vem do contexto (`RunState.IsBossRoom` + `BossCatalog.Get(mapIndex).Id` no `FailRun`), não do nome do golpe.
- **Suposição não verificada:** `Godot.exe --headless --path . --import` gerar os `.cs.uid`. Se não gerar, abrir o editor uma vez.

# Diálogos: guia de escrita e conteúdo

Complementa `docs/design/dialogos-arquitetura.md` (formato, condições, algoritmo). Este arquivo é a referência de **autoria**: tom, vozes, limites, convenções e a tabela de tudo o que existe. Fonte de verdade do conteúdo: `data/dialogue/*.json` e `data/localization/<idioma>/dialogue.json`.

## 1. Tom e premissa

Dark fantasy sóbrio. Frases curtas, humor seco, nenhuma exposição longa. Virgil é um caçador transformado em vampiro que foge da maldição de Armand; o Ninho do Corvo é o hub (en: Crow's Nest, es: Nido del Cuervo). Fale do jogo com os nomes localizados existentes (Fonte de Sangue, Sangue Amaldiçoado, Moedas de Sangue, Câmara de Hemomancia, Bênção de Sangue, Linhagem, Guardiã Invisível etc.; ver `glossario-localizacao.json`). Nunca prometa mecânica que não existe. Sem referência musical visível.

Regras: uma ideia por linha; cada evergreen funciona fora de ordem; não repetir a mesma piada em evergreens vizinhas; nada de gíria datada.

## 2. Vozes

| Falante | Voz | Exemplos de recursos |
|---|---|---|
| **Odalric, o Cronista** (`odalric`) | Seco, irônico, cansado. Arquivista da corte de Armand, preso ao ninho por um pacto de tinta, quer ver "como a história termina". Metáforas de livro, tinta, capítulo, crônica. Chama Virgil de "caçador" antes de derrotar o boss 1 e de "Virgil" depois (na prática: `odalric_after_first_boss` abre com "Virgil."). Sempre cita os leitos selados do leste de forma vaga ("talvez seja só o vento"). | "Anotei como retirada." "É estatística." |
| **Lorde Armand** (`armand`) | Vilão final. Cortês, régio, cruel sem pressa; trata a maldição como extensão do próprio nome. Fala pouco e denso, nunca grita. Só aparece em `boss_pre_armand`, `boss_post_armand` e barks de fase do chefe. Cor sangue-antigo, inicial "A". | "Meus súditos adoram um espetáculo." |
| **Virgil** (`virgil`) | Poucas palavras, cansado, pragmático. Fala sozinho em barks. Sem heroísmo. | "Ainda não. Não aqui." |

Alusões proibidas por enquanto: nomear Selina, Magdalena ou Dominicus; dizer o que há nos leitos selados (`personagens-futuros.md` segue como fonte, com 12 perguntas em aberto). Odalric pode falar de uma "lua e um gato" ligados ao posto da Guardiã Invisível, sem confirmar parentesco.

## 3. Limites

| Item | Limite | Obs. |
|---|---|---|
| Linha de conversa | ≤ 180 caracteres (medido em pt-BR) | en/es costumam ser maiores; confira no `--ui-batch` |
| Linha de bark | ≤ 90 caracteres | legenda pequena |
| Conversa | 2 a 5 linhas | |
| Bark | 1 a 2 linhas | |
| Escolhas | até 3 por conversa; cada ramo de 1 a 3 linhas, sem novas escolhas | só flags como efeito |

Placeholders (4 args fixos, iguais nos 3 idiomas): `{0}` runs iniciadas, `{1}` mortes, `{2}` nome do mapa de contexto, `{3}` nome do boss do mapa de contexto. Chaves literais: `{{` e `}}`. Em en/es escreva de forma natural, não traduzida palavra a palavra; use aspas escapadas `\"` quando precisar citar.

## 4. Chaves, ids e flags

- `dlg.<conversa>.<n>` linha principal; `dlg.<conversa>.<ramo>.<n>` linha de ramo; `dlg.choice.<conversa>.<n>` opção (n = posição em `choices`); `dlg.speaker.<id>.name`; `dlg.npc.prompt` (`Falar com {0}`, não renomear).
- ids em snake_case, **estáveis** (vão para o save: renomear = a fala volta a "não vista"). Nunca `speaker` nem `choice` como id de conversa.
- Padrões de nome: NPC `odalric_<assunto>`; bark `bark_<gatilho>_<n ou mapa>`; boss `boss_pre_<boss_id>` e `boss_pre_generic`.
- Flags (gravadas por `sets_flags`, persistem no slot): `met_odalric`, `asked_odalric_name`, `was_curt_with_odalric`. Convenção: `<verbo no passado>_<quem/o quê>`. As flags de escolha são lidas por `odalric_name_close` (`asked_odalric_name`) e `odalric_name_curt` (`was_curt_with_odalric`).
- Arquivos: `data/dialogue/odalric.json` (hub, death, return do Odalric), `run.json` (barks, boss_pre, boss_post e barks de fase), `speakers.json` (falantes). O carregador lê todos os `*.json` da pasta: pode criar `data/dialogue/<npc>.json`.
- Ordem de `dialogue.json`: chaves em ordem alfabética, idêntica nos 3 idiomas.

## 5. Como adicionar

**Conversa de NPC:** (1) criar a entrada em `odalric.json` (ou arquivo novo) com `category` `hub`/`death`/`return`, `priority_class` e `requirements` da seção 3 da arquitetura; (2) escrever as chaves nos 3 `dialogue.json`; (3) `py tools/validate_dialogue.py` e `py tools/validate_localization.py`. Sem código.

**Bark:** `category: "bark"` + `trigger` (`room_enter`, `room_cleared`, `low_health`, `boss_phase`; o gancho `BossController.OnHealthChanged` notifica a cada troca de fase, sem informar qual fase), `repeatable: true`, `cooldown_visits` 6, `chance` 0,3 a 0,6, 1 linha. Limites globais do gerenciador: 1 bark a cada 18 s, no máximo 2 por sala.

**Boss:** `category: "boss_pre"` ou `boss_post`, `requirements: [{ "type": "boss_in_room", "id": "<boss_id>" }]`. Ids válidos (`BossCatalog`): `invisible_woman`, `labyrinth_keeper`, `silver_dragonkin`, `armand`. `boss_pre` abre no encontro: o boss já está na sala, dormente e invulnerável, e a conversa abre quando o jogador chega a `boss.encounter.radius` dele (`BossController.Encounter.cs` → `DialogueManager.TryStartBossEncounter`); ao fechar, o boss acorda. `boss_post` roda 1,6 s depois da vitória (a run ainda está ativa, então `boss_in_room` vale); não há `boss_post` genérico, de propósito: repetir a derrota do mesmo boss fica em silêncio.

**Falante novo:** entrada em `speakers.json` (`id`, `name_key`, `kind`, `name_color`, `portraits.default` com `initial`, `color`, `accent`; variantes herdam do default) e `dlg.speaker.<id>.name` nos 3 idiomas. Retrato sem textura = placeholder. Emoções fechadas: `neutral grim amused angry sad afraid awe` (sem variante correspondente no falante, cai em `default`; hoje Odalric tem `amused`, `grim`, `sad`; Virgil tem `grim`). Bosses que falam entram como `kind: "npc"` (ex.: `invisible_woman`).

**Escolha:** até 3, com `sets_flags` e `next` apontando para um ramo; só flags como efeito.

## 6. Tabela de conteúdo (54 conversas)

Seleção: classe (essencial > específica > evergreen), não vista primeiro, prioridade, mais antiga, sorteio. Uma conversa de NPC por visita ao hub. "Cd" = `cooldown_visits`. "Repete" = `repeatable`.

| id | Arquivo | Categoria / gatilho | Classe / prio | Repete | Condição |
|---|---|---|---|---|---|
| `first_hub_arrival` | odalric | hub (auto_start) | essential / 90 | não | nenhuma; 2 escolhas (`who`, `curt`) |
| `odalric_lineages_a` | odalric | hub | evergreen / 0 | sim, cd 4 | `seen first_hub_arrival`. Dracul e Bathory |
| `odalric_lineages_b` | odalric | hub | evergreen / 0 | sim, cd 4 | `seen first_hub_arrival`. Karnstein, Orlok, Stirps |
| `odalric_armand_rumor` | odalric | hub | evergreen / 0 | sim, cd 4 | `seen first_hub_arrival` |
| `odalric_bench_book` | odalric | hub | evergreen / 0 | sim, cd 4 | `weapon longsword` |
| `odalric_after_first_boss` | odalric | hub | specific / 60 | não | `boss_defeated invisible_woman`. Menciona lua, gato e os leitos selados |
| `odalric_many_runs` | odalric | hub | specific / 20 | não | `runs_started min 10` (usa `{0}`) |
| `odalric_first_death` | odalric | death | specific / 50 | não | `deaths min 1` e `last_run_outcome died` |
| `odalric_death_by_boss1` | odalric | death | specific / 70 | não | `last_run_outcome died` e `died_at_boss invisible_woman` |
| `odalric_many_deaths` | odalric | death | specific / 30 | não | `deaths min 5` e `last_run_outcome died` (usa `{1}`) |
| `odalric_death_again` | odalric | death | evergreen / 0 | sim, cd 3 | `last_run_outcome died` |
| `odalric_return_victory` | odalric | return | specific / 40 | não | `last_run_outcome victory` |
| `odalric_return_abandoned` | odalric | return | specific / 10 | não | `last_run_outcome abandoned` |
| `bark_room_enter_1` | run | bark `room_enter` | evergreen | sim, cd 6, 35% | `in_run true` |
| `bark_room_enter_garden` | run | bark `room_enter` | evergreen | sim, cd 6, 50% | `map blood_garden` |
| `bark_room_enter_castle` | run | bark `room_enter` | evergreen | sim, cd 6, 50% | `map upper_castle` |
| `bark_room_cleared_1` | run | bark `room_cleared` | evergreen | sim, cd 6, 30% | `in_run true` |
| `bark_low_health_1` | run | bark `low_health` (Virgil) | evergreen | sim, cd 6, 60% | `in_run true` |
| `bark_low_health_2` | run | bark `low_health` (Odalric, "ecoando pelo sangue") | evergreen | sim, cd 6, 50% | `in_run true` e `seen first_hub_arrival` |
| `boss_pre_invisible_woman` | run | boss_pre | specific / 50 | não | `boss_in_room invisible_woman`. Virgil + a Guardiã (6 linhas, primeiro encontro) |
| `boss_pre_invisible_woman_again_1`, `_2` | run | boss_pre | specific / 40 | sim, cd 0 | `boss_in_room invisible_woman` e `seen boss_pre_invisible_woman`. A Guardiã + Virgil, 2 linhas |
| `boss_pre_generic` | run | boss_pre | evergreen / 0 | sim, cd 4 | `boss_room true` (usa `{3}`) |
| `odalric_after_second_boss` | odalric | hub | specific / 62 | não | `boss_defeated labyrinth_keeper` e `seen odalric_after_first_boss` |
| `odalric_after_third_boss` | odalric | hub | specific / 64 | não | `boss_defeated silver_dragonkin` e `seen odalric_after_second_boss` |
| `odalric_death_by_armand` | odalric | death | specific / 70 | não | `last_run_outcome died`, `died_at_boss armand`, `deaths min 3` |
| `odalric_name_close` | odalric | hub | specific / 15 | não | `flag asked_odalric_name` e `runs_started min 3`. Tom íntimo |
| `odalric_name_curt` | odalric | hub | specific / 15 | não | `flag was_curt_with_odalric` e `runs_started min 3`. Tom seco |
| `odalric_after_victory` | odalric | hub | specific / 35 | não | `seen odalric_return_victory` e `runs_completed min 1` |
| `bark_room_enter_dungeons_1`, `_2` | run | bark `room_enter` (Virgil) | evergreen | sim, cd 4, 50% | `map armands_dungeons` |
| `bark_room_enter_garden_2`, `_3` | run | bark `room_enter` (Virgil) | evergreen | sim, cd 4, 50% | `map blood_garden` |
| `bark_room_enter_castle_2`, `_3` | run | bark `room_enter` (Virgil) | evergreen | sim, cd 4, 50% | `map upper_castle` |
| `bark_room_enter_odalric_dungeons` | run | bark `room_enter` (Odalric) | evergreen | sim, cd 4, 35% | `map armands_dungeons` e `seen first_hub_arrival` |
| `bark_room_enter_odalric_castle` | run | bark `room_enter` (Odalric) | evergreen | sim, cd 4, 35% | `map upper_castle` e `seen first_hub_arrival` |
| `bark_room_cleared_dungeons` / `_garden` / `_castle` | run | bark `room_cleared` (Virgil) | evergreen | sim, cd 4, 40% | `map` do nome |
| `bark_boss_phase_<boss>_1`, `_2` (`invisible_woman`, `labyrinth_keeper`, `silver_dragonkin`) | run | bark `boss_phase` (Virgil) | evergreen | sim, cd 3, 85% | `boss_in_room <boss>` |
| `bark_boss_phase_armand_1`, `_2` (Armand) e `_3` (Virgil) | run | bark `boss_phase` | evergreen | sim, cd 3, 85% | `boss_in_room armand` |
| `boss_pre_labyrinth_keeper` | run | boss_pre | specific / 50 | não | `boss_in_room labyrinth_keeper`. Virgil + Odalric |
| `boss_pre_silver_dragonkin` | run | boss_pre | specific / 50 | não | `boss_in_room silver_dragonkin`. Virgil + Odalric |
| `boss_pre_armand` | run | boss_pre | specific / 50 | não | `boss_in_room armand`. Armand + Virgil, 5 linhas |
| `boss_post_invisible_woman` / `_labyrinth_keeper` / `_silver_dragonkin` | run | boss_post | specific / 50 | não | `boss_in_room <boss>`. Virgil (+ Odalric nos bosses 2 e 3) |
| `boss_post_armand` | run | boss_post | specific / 50 | não | `boss_in_room armand`. Armand + Virgil |

Observações de projeto:
- `death` e `return` são `specific`, então passam à frente das evergreen de `hub`, mas perdem para uma essencial não vista.
- `odalric_death_again` existe para a volta após a morte nunca cair numa fala alheia ao momento; o fallback do seletor cobre o resto.
- `bark_room_enter_*` não tocam em sala de boss (o gerenciador ignora `room_enter` ali).
- O mapa `armands_throne` só tem sala de boss: `room_enter` não toca ali, então ele fala só por `boss_pre_armand`, `boss_post_armand` e barks `boss_phase`.
- Os barks de fase tocam a cada troca (fase 2 e fase 3) sem saber qual; por isso cada boss tem 2 barks neutros ao progresso, sorteados com a regra de "não vista primeiro". O limite global (18 s entre barks) pode silenciar o segundo.
- As falas de Odalric dentro da run são "ecos pelo sangue" (mesmo recurso de `bark_low_health_2`).
- Cadeia do segundo arco: `after_first_boss` -> `after_second_boss` -> `after_third_boss`, uma por visita ao hub. `odalric_death_by_armand` aproxima "muitas mortes no mesmo boss" com `deaths min 3`, já que não há contador por boss.

## 7. Próximos arcos (ideias, nada implementado)

- **Barks de bênção e arma:** reação a Linhagens (`blessing_owned`), a `discovered`, a skills Bestialis/Humanitas (`skill`) e a vida baixa refinada com `health_below`. Precisa dos ids reais de cada conteúdo.
- **Barks de fase por número da fase:** exige expor a fase (o `Notify` recebe `phase`, mas o gerenciador a descarta) e uma condição nova (`boss_phase min/max`).
- **Contador de mortes por boss:** para um "muitas mortes no mesmo boss" exato, falta `DialogueProgress` guardar mortes por `BossId`.
- **Variante pós-final de `odalric_return_victory`:** hoje `odalric_after_victory` cobre o hub; uma versão por flag (tom íntimo/seco) exigiria `no_flag`/`flag` na própria return.
- **Leitos selados:** Odalric ficou com alusões crescentes (som, ritmo, fio de luz). A revelação depende de fechar as 12 perguntas de `personagens-futuros.md`.
- **Novos NPCs no hub:** os três personagens desbloqueáveis, uma vez implementados, com arquivo próprio em `data/dialogue/`.
- **Voz, presentes e relacionamento:** fora de escopo atual (GDD §296, §717); ver seção 12 da arquitetura.

## 8. Divergências com o GDD

O GDD prevê NPCs ao longo das salas, relacionamento e dublagem; o escopo atual é um NPC no hub, só texto, flags simples. Manter o GDD como visão e este guia como fase atual.

<!-- orch:plano · id: 20261012-0131-remover-titulos-3d · status: concluido · criado: 2026-10-12 01:31 · commit-inicial: 0740bf5 · versao-orch: 0.5.0 -->
# Remover os títulos flutuantes de lugar nas salas

**Demanda original:** remover textos que ficam flutuando no mapa, exemplo na imagem ("Portão Final", "Calabouços de Armand - Chefe: Guardiã Invisível", "Arena do Chefe").
**Grupo:** 20261012-0131-remover-titulos-3d · **Depende dos planos:** —

## Objetivo

Nenhum título decorativo de lugar fica flutuando nas salas da run. A informação continua no HUD (faixa do topo: "Calabouços de Armand · Portal aberto"). Os prompts de interação e os textos de combate não mudam.

## Suposições e decisões

- Origem dos 3 textos da imagem: `RunManager.CreateRoomLabel` (`RoomTitle`, `RunManager.cs:514`) e os `Label3D` `{nodeId}Label` de `RoomGenerator.cs:758` (Portão Final, Arena do Chefe).
- Escopo: títulos **decorativos** de lugar (sem interação). **Ficam:** prompts "[E] ...", títulos de objetos interativos (Fonte, Bancada, Loja, NPC), rótulos de porta/recompensa e números de dano, avisos e stacks dos inimigos.
- Os títulos decorativos do hub (ex.: `CrowsNestCrypt.cs:215` "Salão de Treino", rótulo do caixão) entram só se T1 os classificar como decorativos. T1 devolve a lista para o usuário conferir.
- O marcador de piso (cilindro colorido) dos nós do `RoomGenerator` permanece.
- Chaves de localização ficam no JSON (sem remoção, para não quebrar paridade).
- Este plano vai antes do `dev-tools`: os dois mexem em `RunManager.cs` e `RoomGenerator.cs`.

## Tarefas

| ID | Tarefa | Executor | Depende de | Arquivos (escrita) | Status |
|---|---|---|---|---|---|
| T1 | Inventariar os `Label3D` e classificar (decorativo / interativo / combate) | `orch:pesquisador` | — | — (leitura) | concluida |
| T2 | Remover os títulos decorativos e o encanamento de idioma que ficar sem uso | `orch:desenvolvedor` | T1 | `scripts/systems/RunManager.cs`, `scripts/systems/RoomGenerator.cs`, `scripts/systems/CrowsNestCrypt.cs`, `scripts/ui/HUD.cs`, `scenes/game/CrowsNest.tscn`, `data/localization/*/{world,ui}.json` | concluida |
| T3 | Verificar build, localização e capturas das salas | `orch:testador` | T2 | — | concluida |

Status: `pendente` · `em-andamento` · `concluida` · `falhou` · `pulada`

## Ondas de execução

```
Onda 1: T1
Onda 2: T2   (após T1)
Onda 3: T3   (após T2)
```

Caminho crítico: T1 → T2 → T3.

## Detalhe das tarefas

### T1 — Inventariar os Label3D
- **Executor:** `orch:pesquisador`
- **Faz:** percorre os ~71 usos de `Label3D` (`scripts/**` e `scenes/game/*.tscn`) e classifica cada um: decorativo de lugar, título/prompt de interativo, rótulo de porta/recompensa, combate. Confirma a origem dos 3 textos da imagem.
- **Pronto quando:** tabela arquivo:linha → categoria → remover? entregue; os 3 textos da imagem têm origem confirmada.
- **Resultado:** Origem confirmada: RunManager.CreateRoomLabel (RoomTitle, 2 chamadas) e RoomGenerator.CreatePointOfInterest (5 POIs: Entrada, Portão Final, Saída, Câmara Central, Arena do Chefe). Decorativos do hub: TrainingHallLabel e CoffinLabel. Nada lê esses nós por nome (exceto HUD.cs:337 para o caixão); StyleTitlesAndAnchor não é afetado.

### T2 — Remover títulos decorativos
- **Executor:** `orch:desenvolvedor`
- **Faz:** remove `CreateRoomLabel` e suas chamadas, o `Label3D` de `RoomGenerator` (mantendo o marcador de piso) e os decorativos da tabela de T1. Apaga `_localizedLabels` e o handler de `LanguageChanged` quando ficarem sem uso. Mantém `InteractPrompt` intacto.
- **Pronto quando:** `dotnet build VP0.1.sln` com 0 erros e 0 avisos; nenhum `RoomTitle`, `*Label` de nó ou título decorativo na árvore em runtime; prompts de interação e números de dano continuam.
- **Resultado:** Removidos RoomTitle (RunManager), Label3D dos 5 POIs (RoomGenerator, marcador de piso mantido), TrainingHallLabel e CoffinLabel (CrowsNestCrypt, CrowsNest.tscn, HUD.cs) e 9 chaves órfãs nos 3 idiomas. Build 0 erros/0 avisos; validate_localization 0 erros (1054 chaves).

### T3 — Verificar
- **Executor:** `orch:testador`
- **Faz:** `dotnet build`, `py tools/validate_localization.py`, smoke headless da `TestArena` e da sala de boss. Capturas com `--ui-batch` se aplicável.
- **Pronto quando:** build e validadores sem erro; smoke sem `ERROR`.
- **Resultado:** Build 0 erros/0 avisos; validadores de localização (1054 chaves), bênçãos (63) e artefatos (20) sem erro; smoke headless de TestArena e CrowsNest sem ERROR; ui-batch hud_combat gerou captura. Só avisos de vazamento no encerramento do Godot (não relacionados).

## Registro de execução

| Quando | Evento |
|---|---|
| 2026-10-12 01:31 | Plano criado |
| 2026-10-12 01:40 | Plano iniciado; T1 iniciada (orch:pesquisador) |
| 2026-10-12 01:46 | T1 concluída por orch:pesquisador; T2 iniciada (orch:desenvolvedor) |
| 2026-10-12 01:55 | T2 concluída por orch:desenvolvedor; T3 iniciada (orch:testador) |
| 2026-10-12 02:03 | T3 concluída por orch:testador; plano concluído |

## Resultado final

Títulos decorativos removidos das salas da run (RoomTitle, 5 POIs) e do hub (Sala de Treinamento, Caixão); marcadores de piso, prompts e textos de combate intactos. Arquivos: RunManager.cs, RoomGenerator.cs, CrowsNestCrypt.cs, HUD.cs, CrowsNest.tscn, world.json e ui.json nos 3 idiomas. Pendência: README.md:18 e :100 citam a sala de treinamento (a sala continua existindo; só o rótulo saiu).

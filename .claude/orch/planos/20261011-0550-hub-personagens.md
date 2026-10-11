<!-- orch:plano · id: 20261011-0550-hub-personagens · status: planejado · criado: 2026-10-11 05:50 · commit-inicial: 3c2e257 · versao-orch: 0.5.0 -->
# Hub: pontos reservados para os personagens desbloqueáveis e atributos ocultos

**Demanda original:** Pedro (2026-10-11), texto colado: fazer 4 personagens jogaveis que vao sendo liberados com o tempo, talvez possam ser inicialmente bosses que depois de derrotados sao desbloqueados para o jogador; cada personagem vai ter 4 variantes de armas, elaborar depois. Power ups por sala, ja tem isso, mas tem que mostrar pro jogador qual vai ser antes dele entrar na proxima, mostrando o icone na porta de acesso (igual hades). Novas recompensas de sala: devil's fruit (fruta do diabo), uma fruta formada por sangue, adiciona 15 pontos de vida ao total do jogador; blood coins (moeda de sangue), ja implementado, hoje serve para comprar aprimoramentos no hub, mas vamos remover isso, agora so vai funcionar para comprar upgrades dentro de uma run; cursed blood (sangue amaldicoado), novo recurso para comprar aprimoramentos no hub, adquirido principalmente ao derrotar bosses e inimigos especiais. Adendos na conversa: atributos dos personagens invisiveis ao jogador mas computados; artefatos de boss passam a ser vendidos na loja e saem da recompensa dos bosses; os outros personagens desbloqueaveis NAO serao implementados agora, apenas um placeholder no hub.
**Grupo:** 20261011-0550-economia-recompensas · **Depende dos planos:** 20261011-0550-economia-recompensas

## Objetivo

O hub ganha 3 pontos reservados (placeholders) para os futuros personagens desbloqueáveis; a Ficha deixa de mostrar a aba Atributos em release (os atributos seguem calculados); as decisões sobre o elenco ficam documentadas para um plano futuro. Nenhum personagem novo é implementado agora.

## Suposições e decisões

- **D9 (Pedro, 2026-10-11) Elenco futuro:** Virgil + 3 desbloqueáveis (bosses derrotados). **Não serão implementados agora.**
- **D10 Identidade futura:** cada personagem terá 4 variantes de arma (a elaborar depois) e um poder único; atributos base por personagem invisíveis ao jogador e computados por baixo; progressão permanente compartilhada por slot.
- **D11 Agora:** só placeholders no hub e ocultação da aba Atributos (decisão: a aba inteira e os totais da run somem).
- **A12** Placeholder = marcador físico no hub (pedestal/sarcófago em geometria provisória) com rótulo "Bloqueado"/"???" e prompt "Em breve", sem interação funcional; usa espaços livres do hub (inclusive o do antigo Baú de Artefatos).
- **A13** A aba Atributos permanece visível só em build de debug (como as seções DEV); os cálculos de `CharacterAttribute` não mudam.
- **A14** Sem mudança de save: campos de personagem entram em um plano futuro, de forma aditiva.
- Convenções do projeto (perfil): `dotnet build VP0.1.sln` deve terminar 0 erros/0 avisos ao fim de CADA tarefa; textos novos em `data/localization/<idioma>/*.json` nos 3 idiomas (`py -I tools/validate_localization.py`); IDs persistidos não mudam; `.cs` novo precisa de `.cs.uid`; indentação do arquivo editado; NUNCA abrir o Godot uma vez por tela: telas só pelo modo `--ui-batch` do UiPreview, um único processo.

## Tarefas

| ID | Tarefa | Executor | Depende de | Arquivos (escrita) | Status |
|---|---|---|---|---|---|
| T1 | Placeholders dos personagens no hub | `designer-ux-arte` | — | `scripts/systems/CharacterPlaceholder.cs` (+ .cs.uid, novo), `scripts/systems/CrowsNestCrypt.cs`, `scripts/ui/HUD.cs` (SceneLabelTexts, se o rótulo seguir o padrão), `data/localization/*/ui.json` | pendente |
| T2 | Ocultar a aba Atributos da Ficha | `ui-hud` | — | `scripts/ui/HUD.CharacterSheet.cs`, `scripts/ui/UiPreview.cs` | pendente |
| T3 | Documentar o elenco futuro | `game-designer` | — | `docs/design/personagens-futuros.md` (novo) | pendente |

Status: `pendente` · `em-andamento` · `concluida` · `falhou` · `pulada`

## Ondas de execução

```
Onda 1: T1 ∥ T2 ∥ T3
```

Caminho crítico: T1. Uma tarefa começa assim que **todas** as suas dependências terminam — não espera a onda inteira.

## Detalhe das tarefas

### T1 — Placeholders dos personagens no hub
- **Executor:** `designer-ux-arte`
- **Faz:** Define o layout no hub (lê `CrowsNestCrypt` e a cena), cria 3 marcadores em código (sem editar .tscn à mão), rótulos e prompt "Em breve" localizados e reagindo a `LanguageChanged`, leitura visual coerente com o hub. Sem interação funcional.
- **Pronto quando:** Captura do hub com os 3 pontos nos 3 idiomas, sem erro; nenhuma lógica de personagem.
- **Resultado:** —

### T2 — Ocultar a aba Atributos da Ficha
- **Executor:** `ui-hud`
- **Faz:** Remove a aba Atributos e os "Totais da run" fora de debug (navegação Q/E, foco inicial e abas ajustados); mantém `CharacterSheet.GetAttribute` e os efeitos; ajusta o preview `--ui-tab=Attributes` (só debug) e a lista do `--ui-batch`; procura textos de release que ainda mencionem pontos de atributo.
- **Pronto quando:** Ficha sem a aba em release e com ela em debug (gate `OS.IsDebugBuild()`); cálculos intactos; build e validadores sem erro.
- **Resultado:** —

### T3 — Documentar o elenco futuro
- **Executor:** `game-designer`
- **Faz:** Registra D9–D11 e o que foi conversado como especificação: hipótese de quais 3 bosses, poderes únicos, atributos base ocultos, 4 armas por personagem (a elaborar), progressão compartilhada, lista de perguntas em aberto e esboço do plano futuro (modelo de personagem, desbloqueio, seleção, armas, visuais). Só documento, sem números de balanceamento inventados.
- **Pronto quando:** Documento claro e completo, sem código.
- **Resultado:** —

## Registro de execução

| Quando | Evento |
|---|---|
| 2026-10-11 05:50 | Plano criado |

## Resultado final

—

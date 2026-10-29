# Vampire's Prey

Vampire's Prey é um roguelite de ação 3D com câmera isométrica, ambientação dark fantasy e progressão entre partidas. O jogador controla Virgil, um caçador transformado em vampiro, atravessa salas procedurais, enfrenta criaturas e combina poderes das linhagens Dracul, Bathory, Orlok e Karnstein para escapar da maldição de Armand.

> Projeto em desenvolvimento. A versão atual prioriza legibilidade, responsividade do combate e validação dos sistemas; parte dos visuais ainda usa geometria provisória.

## Estado atual

- suporte a teclado/mouse e controles compatíveis com o mapeamento padrão do Godot, incluindo DualSense e controles Xbox;
- navegação completa de menus por direcional ou analógico, confirmação por `X`/`A` e retorno por `Círculo`/`B`;
- Espada Grande como arma funcional, exibida nas mãos durante os ataques, com combo básico de três golpes, ataque rápido, ataque especial e Fúria Bestial;
- controles de ataque definidos por golpe: buffer, cancelamentos, recuperação, mobilidade, assistência de mira e redirecionamento no impacto;
- atributos de combate definidos pela arma equipada: dano, tempos, alcance, hitbox, empurrão, stagger e recargas;
- 63 Bênçãos de Sangue ativas: 15 Dracul, 15 Bathory, 14 Orlok, 15 Karnstein e 4 Stirps;
- no mínimo quatro bênçãos diretas para cada tipo de ataque — básico, rápido e especial — com peso maior nas ofertas;
- quatro waves de 5 inimigos por sala, com intervalo reduzido, spawns espaçados e arquétipos melee, ranged e tank;
- campanha com 6, 8 e 10 salas comuns nos três primeiros mapas, além do confronto final;
- primeira sala bônus da run, Câmara de Hemomancia entre mapas e sala de treinamento no Ninho do Corvo;
- portas de saída com a recompensa visível (Bênção de Sangue, Fruta do Diabo ou Moedas de Sangue); a recompensa escolhida fica no chão da sala seguinte e as portas só abrem depois de pegá-la;
- economia em dois recursos: Moedas de Sangue (só na run) e Sangue Amaldiçoado (permanente); artefatos só na Loja da Câmara de Hemomancia, sem drop de boss;
- HUD minimalista sem minimapa, com molduras discretas, cursor temático, ficha, bênçãos, níveis de aprimoramento, passivas, pausa, compêndio e barreira azul integrada à vida;
- personagem, inimigos, bosses, armas e partes dos cenários usam assets 3D externos com origem e licença registradas;
- áudio de combate diferencia cortes, estocada, rodopio, batida no chão, impacto corporal, ataque pelas costas, parede e esquiva;
- três slots de save (formato v2), progressão permanente Bestialis/Humanitas e configurações globais;
- localização em português (`pt-BR`, idioma padrão e fallback), inglês (`en`) e espanhol (`es`), com textos separados do código em `data/localization/`;
- exportação Windows self-contained para Godot .NET.

As mudanças de combate, interface, inimigos, bênçãos e fluxo de campanha estão detalhadas em [`docs/gameplay_systems.md`](docs/gameplay_systems.md).

## Tecnologias

- Godot Engine 4.7 Stable Mono;
- .NET 8;
- C#;
- Jolt Physics;
- catálogos JSON para habilidades, bênçãos e artefatos (só IDs e números) e arquivos JSON de textos por idioma.

## Como executar

1. Instale o Godot 4.7 com suporte a .NET e o SDK do .NET 8.
2. Clone o repositório:

   ```bash
   git clone https://github.com/kxpedro/vp-game.git
   cd vp-game
   ```

3. Abra `project.godot` no editor e aguarde a importação dos assets.
4. Execute o projeto com `F6`/`F5`.

No Windows, `jogar.bat` inicia o projeto pelo Godot sem abrir o editor.

### Validação

```bash
dotnet build VP0.1.sln
py .codex/skills/vp-create-blood-blessing/scripts/validate_blessings.py
py .codex/skills/vp-create-artifact/scripts/validate_artifacts.py
py tools/validate_localization.py
godot --headless --path . --quit-after 180 res://scenes/game/TestArena.tscn
```

`validate_localization.py` confere paridade de chaves entre os três idiomas, placeholders, JSON válido e se as chaves literais usadas no C# existem; os validadores de bênçãos e artefatos também conferem as chaves de `content.json`. Erros devolvem código de saída 1; avisos não.

### Pré-visualização de telas (build de debug)

Em build de debug, `scripts/ui/UiPreview.cs` abre uma tela de UI com dados fictícios, sem gravar nada. Os argumentos de usuário vêm depois de `--`:

```bash
# uma tela, em um idioma e com glifos de Xbox
godot --path . res://scenes/game/CrowsNest.tscn -- --ui-preview=blood_fountain --ui-lang=en --ui-device=xbox

# lote: um único processo percorre telas x idiomas e grava um PNG por combinação
godot --path . --resolution 1600x900 -- --ui-batch --ui-shots=C:/tmp/shots --ui-langs=pt-BR,en,es --ui-screens=pause,compendium
```

Opções: `--ui-preview=<tela>`, `--ui-lang=pt-BR|en|es`, `--ui-device=kbm|xbox|ps`, `--ui-tab=<aba>` (compêndio e ficha), `--ui-codex-locked[=sample]` (Dicionário na visão de release) e, no lote, `--ui-batch`, `--ui-shots=<pasta>` (padrão `user://ui_shots`), `--ui-langs=` e `--ui-screens=`. O lote recarrega a cena entre uma captura e outra, em vez de abrir o jogo uma vez por tela. A lista de telas está no cabeçalho de `UiPreview.cs`. O lote padrão (`BatchScreens`) tem 37 telas (contando cada aba do compêndio e da ficha), ou seja, 111 PNGs em 3 idiomas. Inclui `reward_doors` (portas de recompensa, na Câmara) e `hub_cast` (os 3 sarcófagos de personagens no hub). Os itens no chão (`RewardPickup`, `DevilsFruit`) não têm preview.

## Controles

| Ação | Teclado e mouse | DualSense | Xbox |
| --- | --- | --- | --- |
| Movimentar e mirar | `W`, `A`, `S`, `D` e direção do cursor | Analógico esquerdo | Analógico esquerdo |
| Caminhar | `Caps Lock` | — | — |
| Esquivar / dash | `Espaço` | `X` | `A` |
| Ataque básico | Botão esquerdo | `Quadrado` | `X` |
| Ataque rápido | Dash + ataque básico | `X` + `Quadrado` | `A` + `X` |
| Ataque especial | `Q` | `Triângulo` | `Y` |
| Fúria Bestial | `F` | `R1` | `RB` |
| Interagir / confirmar | `E` / `Enter` | `X` | `A` |
| Voltar / fechar camada atual | `Esc` | `Círculo` | `B` |
| Ficha do personagem | `C` | `Criar` | `View` |
| Pausar | `Esc` | `Opções` | `Menu` |

O ataque no controle é orientado pelo analógico esquerdo. Ao abrir a ficha com `C`, o jogo pausa; `Esc`, `Círculo` ou `B` fecham primeiro a camada de interface que estiver no topo. O menu de pausa também permite desistir da run, aplicando o mesmo fluxo de morte e retorno ao Ninho do Corvo.

## Fluxo da campanha

1. Escolher idioma e um dos três slots no menu principal.
2. Preparar a run no Ninho do Corvo, onde ficam a Fonte de Sangue (compra de skills com Sangue Amaldiçoado), a bancada de armas, a sala de treinamento e três sarcófagos reservados a personagens futuros (placeholders, "Em breve", sem interação).
3. Na primeira run room, interagir com a fonte central e escolher a bênção inicial obrigatória. Essa sala aparece apenas antes do primeiro mapa.
4. Limpar a sala. O bônus de sala limpa (Moedas de Sangue) e a recompensa da porta escolhida na sala anterior aparecem no chão; pegue todos com `interact` para as portas de saída abrirem. Cada porta mostra o tipo de recompensa da próxima sala (ver [Portas com recompensa](#portas-com-recompensa)).
5. Derrotar o boss: ele paga Moedas e Sangue Amaldiçoado e solta uma Fruta do Diabo (exceto no mapa final). Não há mais Altar de Artefatos nem drop de artefato.
6. Visitar a Câmara de Hemomancia para recuperar até 35% da vida, opcionalmente reduzir a vida máxima total da run em 5% para elevar em um nível uma bênção, preservando sua raridade, e gastar Moedas de Sangue na Loja de Artefatos.
7. Avançar pelo segundo mapa com 8 salas e pelo terceiro com 10; o quarto mapa contém o confronto final. A ordem dos bosses é Guardiã Invisível (`invisible_woman`), Guardião do Labirinto (`labyrinth_keeper`), Dragonkin Prateado (`silver_dragonkin`) e Lorde Armand (`armand`).

A vida atual é preservada entre salas e mapas. Ao morrer ou desistir, a run temporária termina e a progressão permanente do slot é mantida.

## Combate e progressão

A Espada Grande é a arma jogável atual. Saves antigos que referenciam `greatsword` ou `dual_blades` são migrados para o identificador funcional `longsword`. Revolver Duplo e Chicote permanecem como entradas de expansão sem moveset definitivo.

O modelo da arma fica oculto durante idle e locomoção e aparece somente enquanto um ataque está em execução. A duração das animações é sincronizada com startup + recovery do golpe; a estocada final usa uma animação distinta.

O combo básico reinicia após 1 segundo sem continuação, e o terceiro golpe empurra 20% além do empurrão padrão do combo. Cada golpe possui perfil próprio de buffer, janela de cancelamento, recuperação, mobilidade e assistência de mira; a estocada final libera o jogador mais cedo. O ataque rápido aceita buffer da combinação dash + ataque. O ataque especial sai rapidamente, mas possui 1 segundo de recarga. Modificadores de velocidade de ataque afetam somente o ataque básico, sem acelerar dash, ataque rápido, especial ou Fúria Bestial.

Com 100 pontos acumulados, a Fúria Bestial consome 10 pontos por segundo e dura 10 segundos antes de causar uma explosão final de 15% do dano básico em área. Efeitos externos podem alterar essa duração. A barreira absorve dano antes da vida, aguarda 8 segundos após obtenção ou consumo, perde 0,25 ponto por segundo e é zerada ao trocar de sala.

As habilidades permanentes ficam em `data/skills/skills.json` e são compradas na Fonte de Sangue. As Bênçãos de Sangue ficam em `data/blessings/blood_blessings.json` e duram apenas durante a run. Artefatos em `data/artifacts/artifacts.json` são exclusivamente passivos e só são obtidos na Loja da Câmara de Hemomancia.

Raridade e nível de bênção são progressões independentes. A raridade sorteada determina a base; toda bênção começa no nível 1 e pode chegar ao nível 10. Cada nível adicional aumenta em 25% os valores escaláveis daquela raridade. Chances são limitadas a 100% tanto na lógica quanto na interface. No Altar do Sacrifício, até três bênçãos elegíveis são sorteadas e apresentadas no mesmo formato visual das ofertas de bênção.

## Economia: dois recursos

Valores numéricos vêm de `CompendiumTuning` (chaves `economy.*`); os citados abaixo são os padrões do código. Detalhes de design em [`docs/design/economia-v2.md`](docs/design/economia-v2.md).

| Recurso | Onde vive | Duração | Como se ganha | Para que serve |
| --- | --- | --- | --- | --- |
| Moedas de Sangue | `RunState.RunBloodCoins` | só na run; nunca vai para o save | inimigos, bônus de sala limpa, boss e portas de Moedas | comprar artefatos na Loja da Câmara |
| Sangue Amaldiçoado | `CharacterSheet.CursedBlood` | permanente (salvo no slot) | boss (40/60/80/120 por mapa) e inimigo Lendário (0/6/8/8 por mapa; chance `economy.cursed_blood.legendary_drop_chance`, padrão 1,0) | comprar skills Bestialis/Humanitas na Fonte de Sangue |

As Moedas de Sangue zeram em `RunState.StartRun`, em `RunState.FailRun` (morte ou desistência) e em `RunState.CompleteBoss` do mapa final. Elas **não** zeram entre mapas, porque a Loja fica na Câmara entre mapas. Já o Sangue Amaldiçoado sobrevive à morte da run. No HUD, o contador mostra as Moedas durante a run e o Sangue Amaldiçoado no hub (Ninho do Corvo).

### Fruta do Diabo

`scripts/items/DevilsFruit.cs`: item coletado com `interact` que soma vida máxima só durante a run (`RunState.AddDevilsFruit`) e cura o jogador. O ganho padrão é `economy.fruit.max_health` = 15 (e `economy.fruit.heal` = 15). Zera em `StartRun`, `FailRun` e no fim da campanha. Fontes: porta de Fruta (teto de `economy.fruit.run_cap_from_doors` = 3 por run), uma Fruta garantida por boss (`economy.fruit.boss_guaranteed` = 1; no mapa final, `economy.fruit.boss_final_map` = 0) e `DevilsFruit.Spawn`.

O bônus é exato: `RunState.DevilsFruitMaxHealthTotal` é somado em `PlayerController.ApplyRuntimeTuning` depois dos multiplicadores de vida máxima (não passa por `MaxHealthBonus`), então cada Fruta dá +15 de fato.

### Loja da Câmara de Hemomancia

Na Câmara (depois de cada boss não final) há um balcão interativo que abre `HUD.Shop.cs` (`CallGroup("hud", "OpenShop")`). O estoque é sorteado uma vez por visita (`ArtifactCatalog.RollShopStock`): 3 artefatos, com 50% de chance de um quarto, o primeiro slot sempre Common ou Uncommon e a raridade dos demais pesada pela visita. Preço-base por raridade em `economy.shop.price.*` (250/420/700/1.100/1.700/2.400), com desconto da skill `humanitas.collector_eye`. A compra é `RunState.TryBuyShopArtifact`, gasta Moedas de Sangue e adiciona o artefato à run (sem limite de quantidade; por padrão não repete artefato já possuído). O sorteio do antigo Altar de Artefatos (`ArtifactCatalog.RollChoices`) foi removido.

### Portas com recompensa

Cada saída de sala comum tem uma ou duas portas (probabilidade de duas por mapa e sala em `RoomRewardCatalog.TwoDoorChance`; base 0,30/0,35/0,40 e teto 0,55), sorteadas por `RunState.GetOrRollRoomRewardSet`. Toda saída tem uma Bênção de Sangue (garantida pelo padrão `reward.blessing.max_sets_without_blessing` = 0); a segunda porta, opcional, é Fruta do Diabo ou Moedas (150/130/110 por mapa). Cada porta tem um ícone provisório (`DoorRewardIcon`) e um prompt dizendo a recompensa. Ao atravessar, a escolha fica pendente (`RunState.PendingRoomReward`) e é paga na sala seguinte como item no chão (`RewardPickup` para Bênção e Moedas, `DevilsFruit` para Fruta). As portas nascem travadas (`ScenePortal` com `StartsEnabled` falso) e só abrem quando não resta item no chão (`RunManager.UnlockExitAfterReward`); a saída para o boss e o boss têm saída única. Detalhes em [`docs/design/portas-recompensas.md`](docs/design/portas-recompensas.md).

## Save (formato v2)

O slot é `user://save_slot_{1..3}.json`, com `GameSaveData.Version = 2` (`CurrentSaveVersion`). Na v2 as Moedas de Sangue deixaram de ser salvas e surgiu o campo `CursedBlood`. Ao ler um slot, `SaveGameManager.ReadSlot` só trata como v1 um JSON em que `Version` existe, é inteiro e está em `1 <= Version < 2`; nesse caso o arquivo é **renomeado** para `save_slot_N.v1.json` (`ArchiveLegacySlot`) e o slot começa vazio; não há migração. JSON válido sem `Version` é carregado normalmente. JSON inválido segue o fluxo de slot corrompido (backup).

## Estrutura do projeto

```text
.codex/      skills locais e validadores dos catálogos
assets/      pacotes externos com licença e origem registradas
audio/       músicas, ambientes e efeitos sonoros
data/        skills, bênçãos e artefatos (IDs e números) e localization/<idioma>/ (textos)
docs/        documentação técnica
images/      interface, ícones e efeitos
scenes/      cenas do Godot
scripts/     código C# de gameplay e sistemas
tools/       validador de localização
```

## Localização

Todo texto exibido vem de `data/localization/<idioma>/` (idiomas `pt-BR`, `en` e `es`), em arquivos JSON planos no formato `"chave": "texto"`:

| Arquivo | Conteúdo |
| --- | --- |
| `ui.json` | interface: menus, HUD, opções, prompts e portais (`portal.*`) |
| `content.json` | bênçãos, artefatos, skills, armas e nomes de efeitos (`blessing.<id>.name/.desc/.flavor`, `artifact.<id>...`, `skill.<id>...`, `weapon.<arma>...`) |
| `world.json` | bosses e golpes, mapas, inimigos e mensagens de combate |

`scripts/systems/Localization.cs` (autoload) carrega todos os `.json` da pasta de cada idioma. Uma chave é resolvida no idioma atual, depois em `pt-BR` e, por último, aparece como a própria chave (com aviso em build de debug, uma vez por chave). Uso no C#: `Localization.Text(chave)`, `Localization.TextFormat(chave, args)` (um `string.Format` com tradução malformada não derruba o jogo) e `Localization.Has(chave)`. O sinal `LanguageChanged` faz overlays e rótulos 3D se atualizarem na hora. A cultura da sessão acompanha o idioma: `en` usa ponto decimal; `pt-BR` e `es` usam vírgula.

Os catálogos `blood_blessings.json`, `artifacts.json` e `skills.json` guardam só IDs e números. Descrições de bênção usam `{n}` (valor do efeito n) e `[kw=<id>]termo[/kw]` para palavras-chave. Cada artefato tem um `saveName`, identificador legado gravado nos saves: nunca o altere.

Para adicionar um texto, crie a chave nos três idiomas (o validador cobra paridade) e rode `py tools/validate_localization.py`. Termos de referência: `docs/design/glossario-localizacao.json`. Para um idioma novo, é preciso mexer em `Localization.Languages` e `NormalizeLanguage`, no seletor de idioma de `UiComponents.OptionsPanel` (que hoje tem três opções fixas), e nas listas `LANGUAGES` dos três validadores Python; depois crie a pasta com `ui.json`, `content.json` e `world.json`. Textos só de debug (log de eventos, seção DEV, linhas "Prévia:" dos previews) podem ficar literais.

Cenas principais:

- `scenes/ui/MainMenu.tscn`;
- `scenes/game/CrowsNest.tscn`;
- `scenes/game/RunStartBlessingRoom.tscn`;
- `scenes/game/TestArena.tscn`;
- `scenes/game/IntermissionChamber.tscn`;
- `scenes/player/Player.tscn`.

## Exportação Windows

Instale pelo editor os templates correspondentes a `4.7.stable.mono` e execute:

```bat
exportar_windows.bat
```

A distribuição deve manter juntos:

```text
builds/VampiresPrey.exe
builds/data_VP0.1_windows_x86_64/
```

Mais detalhes em [`docs/build_and_export.md`](docs/build_and_export.md).

## Documentação

- [`docs/initialization.md`](docs/initialization.md): snapshot técnico para novas sessões;
- [`docs/gameplay_systems.md`](docs/gameplay_systems.md): controles, combate, HUD, inimigos, bênçãos e salas especiais;
- [`docs/design/ui-guia.md`](docs/design/ui-guia.md): guia de estilo e especificação das telas de UI, com os previews em lote;
- [`docs/design/economia-v2.md`](docs/design/economia-v2.md): Moedas de Sangue, Sangue Amaldiçoado, Fruta do Diabo e Loja;
- [`docs/design/portas-recompensas.md`](docs/design/portas-recompensas.md): portas com recompensa visível;
- [`docs/design/personagens-futuros.md`](docs/design/personagens-futuros.md): elenco jogável futuro (3 desbloqueáveis, ainda não implementados), decisões e perguntas em aberto;
- [`docs/design/narrativa.md`](docs/design/narrativa.md): fluxo de abertura do save novo (cutscenes, tutorial, luta no trono, hub) e esqueleto da narrativa profunda, com pontos em aberto;
- [`docs/room_generation.md`](docs/room_generation.md): campanha, geração procedural e waves;
- [`docs/character_asset.md`](docs/character_asset.md): estado do visual provisório e catálogo do asset arquivado;
- [`docs/build_and_export.md`](docs/build_and_export.md): build e exportação;
- [`CREDITS.md`](CREDITS.md): autoria e licenças dos assets externos;
- [`GDD.txt`](GDD.txt): documento de design original e adendo de implementação atual.

## Autoria

Projeto e conceito de Pedro Henrique Rech Sarmento.

# Monstros do modo 2D

Folhas do pacote **Monsters Creatures Fantasy** (versões 1.0, 1.2 e 1.3), copiadas de `F:\ASSETS\Monsters_Creatures_Fantasy`
e `F:\ASSETS\Monster_Creatures_Fantasy(Version 1.x)` no PC do Pedro. Usadas por `scripts/pixel/PixelEnemySprite.cs`.

- **Autor provável:** Luiz Melo (luizmelo.itch.io), pelo nome e pelo formato (quadros de 150x150, como os Evil Wizard).
- **Licença:** NÃO REGISTRADA. O pacote veio sem licença nem readme. Antes de distribuir o jogo, confirmar os termos
  na página do pacote e registrar aqui e em `CREDITS.md`.

Cada pasta tem faixas horizontais de quadros de 150x150, olhando para a direita (pés em y≈101), renomeadas assim:

| Pasta | Arquivo | Original |
|---|---|---|
| `skeleton/` | `idle`, `walk`, `attack`, `hit`, `death` | Idle, Walk, Attack, Take Hit, Death |
| `goblin/`, `mushroom/` | `idle`, `walk`, `attack`, `hit`, `death` | Idle, Run, Attack, Take Hit, Death |
| `flying_eye/` | `flight`, `attack`, `hit`, `death` | Flight, Attack, Take Hit, Death |

As outras animações do pacote (Attack2/Attack3, Shield, projéteis) ficaram de fora por enquanto.

## Paleta

As folhas daqui já estão remapeadas para a paleta **AAP-64**, a mesma do kit da cripta, do Virgil, do chão e da
poça de sangue (ver `assets/pixel_art/SOURCE.md`). Os originais do pacote ficam intocados em
`tools/pixel_art/monsters/source/<monstro>/` (fora do Godot, pelo `.gdignore`). Para regerar depois de trocar ou
adicionar uma folha nos originais:

    py tools/pixel_art/monsters/recolor_monsters.py [--preview comparacao.png]

Cada cor vira a cor da AAP-64 mais próxima (OKLab, com peso maior na luminosidade); os vermelhos saturados vão
para a rampa de sangue e os cinzas neutros para a rampa fria da pedra. As cores berrantes da paleta (neon, ciano,
rosa, verde-limão) ficam de fora.

## mage_guardian (Guardiã Invisível, boss do mapa 1)

`mage_guardian/idle.png` é o `mage_guardian-magenta.png` do pacote **Mage Guardian (free)**, copiado sem alteração de
`F:\ASSETS\mage_guardian_free_creativekind` no PC do Pedro: faixa horizontal de 14 quadros de 64x64, olhando para a
direita, pés em y≈58. Não foi remapeado para a AAP-64 de propósito: o magenta do núcleo é a cor da Guardiã e casa
com a rampa violeta dos efeitos dela (`BossRuneFx.Violet`). O pacote só tem a flutuação parada; as outras animações
do `PixelEnemySprite` reaproveitam a mesma folha.

- **Autor:** creativekind (creativekind.itch.io), pelo nome da pasta do pacote.
- **Licença:** NÃO REGISTRADA. O pacote veio sem licença nem readme. Antes de distribuir o jogo, confirmar os termos
  na página do pacote e registrar aqui e em `CREDITS.md`.

## plant_red, plant_bulb, plant_purple (plantas carnívoras do Jardim Sangrento, mapa 2)

Pacote **Free Predator Plant Mobs** (Craftpix), copiado de `F:\ASSETS` no PC do Pedro. Cada folha original tem 4
linhas (uma por direção); daqui saiu só a linha 3 (y 192–256, olhando para a direita), em quadros de 64x64 com os pés
em y≈51 (`PixelEnemySprite.FeetOffsets`). Remapeadas para a AAP-64 com o mesmo `recolor_monsters.py`; os originais
recortados ficam em `tools/pixel_art/monsters/source/plant_*/`.

| Pasta | Planta do pacote | Arquétipo (`EnemyVisualCatalog.GetPixelEnemy`, só no mapa 2) |
|---|---|---|
| `plant_red/` | Plant1 | corpo a corpo |
| `plant_bulb/` | Plant2 | à distância |
| `plant_purple/` | Plant3 | tanque |

O corpo a corpo do mapa 2 sorteia meio a meio entre `plant_red` e `ghoul` (abaixo).

Arquivos: `idle`, `walk`, `attack`, `hit`, `death` (Idle, Walk, Attack, Hurt, Death do pacote).

- **Autor:** Craftpix (craftpix.net).
- **Licença:** licença de arquivos da Craftpix (https://craftpix.net/file-licenses/), indicada no `License.txt` do
  pacote: uso em jogos, inclusive comerciais, sem exigir atribuição; não pode redistribuir os arquivos soltos.

## ghoul (zumbi do Jardim Sangrento, mapa 2)

`Ghoul Sprite Sheet.png`, enviada pelo Pedro como exemplo (folha de 256x160, 8 colunas x 5 linhas de quadros de
32x32, olhando para a direita). Cada linha virou uma faixa ampliada 2x com vizinho mais próximo (quadros de 64x64, pés
na última linha, `PixelEnemySprite.FeetOffsets` = 31) em `tools/pixel_art/monsters/source/ghoul/`, e depois foi
remapeada para a AAP-64 como os outros. Por ser ampliado, o pixel do zumbi sai 2x maior que o das outras folhas.

| Linha | Arquivo | Quadros |
|---|---|---|
| 1 | `idle` | 4 |
| 2 | `walk` | 8 |
| 3 | `attack` | 6 |
| 4 | `hit` | 4 |
| 5 | `death` | 6 |

- **Autor e licença:** NÃO REGISTRADOS. Antes de distribuir o jogo, confirmar a origem do pacote e registrar aqui e em
  `CREDITS.md`.

## Conferência da paleta

    py tools/pixel_art/check_palette.py

Confere se as folhas (cripta, jardim, chãos, efeitos e monstros, menos a Guardiã) usam só cores da AAP-64 e alfa
0 ou 255. O `recolor_monsters.py` já grava o alfa binário (as sombras translúcidas das plantas somem, como já
sumiam no corte de alfa do jogo).

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

`mage_guardian/idle.png` é o `mage_guardian-magenta.png` do pacote **Mage Guardian (free)**, copiado de
`F:\ASSETS\mage_guardian_free_creativekind` no PC do Pedro (original intocado em
`tools/pixel_art/monsters/source/mage_guardian/`): faixa horizontal de 14 quadros de 64x64, olhando para a direita,
pés em y≈58. Remapeado para a AAP-64 pelo `recolor_monsters.py`, que para ela libera também o rosa `bc4a9b` e os
azuis vivos: o magenta do núcleo continua sendo a cor da Guardiã e casa com a rampa violeta dos efeitos dela
(`BossRuneFx.Violet`). O pacote só tem a flutuação parada; as outras animações do `PixelEnemySprite` reaproveitam
a mesma folha.

- **Autor:** creativekind (creativekind.itch.io), pelo nome da pasta do pacote.
- **Licença:** NÃO REGISTRADA. O pacote veio sem licença nem readme. Antes de distribuir o jogo, confirmar os termos
  na página do pacote e registrar aqui e em `CREDITS.md`.

## skeleton_guardian (lacaios da Guardiã Invisível)

Gerado pelo `recolor_monsters.py` a partir do `skeleton` original: cada cor é tingida pela luminosidade na rampa
violeta da Guardiã (`060608 141013 242234 403353 793a80 bc4a9b e9b5a3 fad6b8`, todas da AAP-64). Mesma origem e
licença do `skeleton`. Usado pelos lacaios que ela invoca (`BossMinionSummoner.cs`, `EnemyVisualCatalog.GetBossMinion`).


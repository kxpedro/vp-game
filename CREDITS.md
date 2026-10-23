# Créditos de assets externos

Os arquivos de licença e origem permanecem junto de cada pacote em `assets/`.

## Personagem

- `Knight_Male` do [Ultimate Animated Character Pack](https://quaternius.com/packs/ultimatedanimatedcharacter.html), por Quaternius — CC0 1.0.
- Sprite de teste do Virgil (`assets/characters/lpc_virgil_test/`), montado com camadas do [Universal LPC Spritesheet Character Generator](https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator) — CC-BY-SA 3.0 / GPL 3.0 (algumas camadas OGA-BY 3.0). Autores por camada em `assets/characters/lpc_virgil_test/CREDITS.txt`, entre eles Stephen Challener (Redshrike), Johannes Sjölund (wulax), bluecarrot16, JaidynReiman, Eliza Wyatt (ElizaWy), Manuel Riecke (MrBeast) e Nila122.

## Inimigos e bosses

- `Ninja`, `Demon`, `Orc`, `Dragon_Evolved`, `Ghost_Skull`, `Orc_Skull` e `BlueDemon` do [Ultimate Monsters Pack](https://quaternius.com/packs/ultimatemonsters.html), por Quaternius — CC0 1.0.

## Cenários

- Detalhes de piso e portão do [Modular Dungeon Kit](https://kenney.nl/assets/modular-dungeon-kit), por Kenney — CC0 1.0.
- Barris, ossos, livro, candelabros, carpete, baú, entrada e tochas do [Modular Medieval Dungeon](https://quaternius.com/packs/medievaldungeon.html), por Quaternius — CC0 1.0.

## Pixel art própria

- `assets/pixel_art/`: arte 2D feita para o jogo (autoria própria), na paleta [AAP-64](https://lospec.com/palette-list/aap-64), de Adigun A. Polack. Detalhes em `assets/pixel_art/SOURCE.md`.
- `assets/pixel_art/characters/virgil/`: sprite do Virgil (idle, 8 quadros), autoria própria, gerado por `tools/pixel_art/virgil/`. Detalhes no `SOURCE.md` da pasta.

## Interface

- `simple_panel.png` e `simple_divider.png` do [UI Pack](https://kenney.nl/assets/ui-pack), por Kenney — CC0 1.0.
- `cursor_pointer.png` e `cursor_gauntlet.png` do [Cursor Pack](https://kenney.nl/assets/cursor-pack), por Kenney — CC0 1.0.

## Armas

- `Whip` by Poly by Google, downloaded from
  [Poly Pizza](https://poly.pizza/m/fWeD6UzvJ0x), licensed under
  [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/).
- `Sword_Big` from the
  [Quaternius Modular Weapons Pack](https://quaternius.com/packs/medievalweapons.html),
  licensed under CC0 1.0.
- `Revolver` by Quaternius, downloaded from
  [Poly Pizza](https://poly.pizza/m/q4h8j4nK2k), licensed under CC0 1.0.

## Efeitos sonoros de combate

- Swishes e cortes de espada: “Swishes Sound Pack”, por artisticdude, via [OpenGameArt](https://opengameart.org/content/swishes-sound-pack) — CC0 1.0.
- Impactos de espada: “20 Sword Sound Effects (Attacks and Clashes)”, por StarNinjas, via [OpenGameArt](https://opengameart.org/content/20-sword-sound-effects-attacks-and-clashes) — CC0 1.0.
- Impactos carnais: “Fleshy Fight Sounds”, por Will Leamon, via [OpenGameArt](https://opengameart.org/content/fleshy-fight-sounds) — OGA-BY 3.0.
- Tiros e recarga do Revólver Duplo (`Revolver *.wav`): sintetizados para o projeto por `tools/generate_revolver_sfx.py` — CC0 1.0.

Attribution: “Additional Sound FX by Will Leamon”.

Os nomes dos arquivos foram adaptados às funções de runtime. A relação detalhada está em `audio/effects/combat/CC0_SOURCES.md`.

## Exibição no jogo

As atribuições exigidas pelas licenças CC BY 3.0 (Whip) e OGA-BY 3.0 (Will Leamon) aparecem na tela **Créditos** do menu principal (`scripts/ui/MainMenu.cs`, `ThirdPartyCredits`). Ao adicionar um asset com licença que exija atribuição, atualize também essa lista.

## Origem ainda não registrada

Os arquivos abaixo estão no projeto sem `SOURCE.md` nem licença documentada. Antes de distribuir o jogo, registre autoria e licença de cada um (ou substitua por material próprio/CC0):

- Músicas: `audio/ambience/*.mp3` (Floor 1–3, Game Over, Hub, Main Menu Theme) e `audio/boss/*.mp3`.
- Efeitos: `audio/effects/Boss Defeated.mp3`, `Death.mp3`, `Treasure.mp3`.
- Monstros do modo 2D: `assets/pixel_art/monsters/*` (pacote “Monsters Creatures Fantasy”, provavelmente de Luiz Melo; ver o `SOURCE.md` da pasta).
- Imagens: `images/bloodBoons/*`, `images/effects/*`, `images/inventory/*`, `images/generic.png` e `icon.png`.

## Material arquivado

Os modelos de referência (`3d/`) e as capturas de outros jogos (`references/`) foram retirados do projeto e do controle de versão; ficam fora da pasta do jogo, em `../vp-0.1-arquivo/`. Eles incluíam modelos de jogos comerciais que não podem ser redistribuídos e não devem voltar ao repositório.

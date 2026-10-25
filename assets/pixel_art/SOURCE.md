# Pixel art própria

Arte 2D/pixel art feita para o Vampire's Prey (autoria própria, sem material de terceiros), exceto `hud/` e
`monsters/`, que são pacotes de terceiros remapeados para a paleta (origem no `SOURCE.md` de cada pasta).
Paleta: [AAP-64](https://lospec.com/palette-list/aap-64), de Adigun A. Polack (paletas não são protegidas;
nenhum pixel de terceiros é usado).

## tiles/dungeon_floor

Chão de masmorra isométrico, 128px, desenhado proceduralmente por
`tools/pixel_art/generate_dungeon_floor.py` (numpy + Pillow). Para regerar:

    py tools/pixel_art/generate_dungeon_floor.py assets/pixel_art/tiles/dungeon_floor/dungeon_floor_128

- `dungeon_floor_128_<variante>.png`: bloco 128x128 (topo em losango 128x64 + 24px de lateral em cantaria).
- `dungeon_floor_128_<variante>_flat.png`: só o losango 128x64, para TileMap isométrico (tile size 128x64).
- Variantes: `base`, `cracked`, `blood`, `missing_stone`, `wet`. Todas compartilham o mesmo desenho de borda e
  encaixam entre si sem emenda.

No Godot, importe com filtro **Nearest** para não borrar os pixels.

## crypt (kit modular da cripta)

Paredes, cantos, arcos e objetos da cripta, todos desenhados por código em
`tools/pixel_art/crypt_kit/` (mesma paleta e mesma pedra do chão). Para regerar:

    py tools/pixel_art/crypt_kit/export_kit.py

- `walls/`: paredes de 1 tile que encaixam sem emenda (`wall_<ne|nw>_plain_a/b/c`, `_niche`, `_arch`, `_low`;
  `wall_<se|sw>_low` e `_gap` para a frente cortada) e cantos (`corner_n/e/w/s`, `corner_e_low`, `corner_w_tall`).
- `props/`: caixões (`coffin_virgil`, `coffin_stone_closed/broken`, `coffin_wood_closed/open/broken`, cada um em
  `_sw` e `_se`), velas (`candles_small/medium/large_a/b`, `candle_tall`), arandelas (`sconce_ne/nw`),
  tapete (`carpet_sw/se`), `bones`, `rubble_a/b`.
- `props/sacrifice_altar` e `props/artifact_counter` (tela 256x224, vértice do tile em 120, 136): altar do sacrifício
  e bancada de artefatos da Câmara de Hemomancia (`rooms/camara_intervalo.txt`, planta em cruz).
- `props/portcullis_<ne|nw>_0..7`: grade de ferro do vão do arco (`wall_<lado>_arch`), da fechada (0) à aberta (7).
  Não entra em layout: o portão do início da run (`RunStartGate.cs`) a desenha por cima do hub e troca o quadro ao abrir.
- Toda peça é um PNG 224x208 com o vértice de cima do tile em (112, 116) e o centro do tile em (112, 148).
  No TileSet do Godot (tile_size 128x64): `texture_origin = (0, 44)`.
- `manifest.json`: tipo de cada peça e a posição das luzes (velas e arandelas) em pixels do PNG, para pôr
  `PointLight2D` no jogo. A luz das velas não vem "assada": as peças só têm a penumbra base (ambient -0.6).
- Escala: Virgil com ~49px de altura e parede de 76px (ele fica com ~65% da parede).

## garden (kit do Jardim Sangrento, mapa 2)

Kit no mesmo formato do da cripta (peças de 224x208 com o vértice de cima do tile em (112, 116), `heights/` e
`manifest.json`), desenhado por código em `tools/pixel_art/garden_kit/` (sem pixels de terceiros; os pacotes
NatureTiles, Flowers e PixelBushes do `F:\ASSETS` serviram só de referência visual). Para regerar:

    py tools/pixel_art/garden_kit/export_garden.py
    py tools/pixel_art/garden_kit/preview_rooms.py <pasta>   (prévia das duas salas)

- `walls/`: mesmos nomes do kit da cripta, com sebe alta no lugar da pedra (`_niche` = sebe com rosas; o `_arch`
  tem o mesmo vão do da cripta, então a grade do portão é a mesma).
- `props/`: `hedge_block_a/b/c` (bloco de sebe do labirinto, 1 tile), `parterre` (canteiro 2x2), `topiary_center`,
  `topiary_ball`, `rose_bush`, `lantern` (com luz) e `dead_tree_a/b`.
- O `manifest.json` traz o chão do kit (`floor`): `tiles/garden_floor/garden_floor_128_<base|dark|moss|blood>_flat.png`.

O `PixelRoomComposer` escolhe o kit pelo `PixelRoomLayout.Kit` (`crypt` por padrão; `garden` nas salas do mapa 2).

## characters/virgil

Sprite do Virgil (idle de 8 quadros, 3/4 sudoeste, com e sem arma), gerado por `tools/pixel_art/virgil/`.
Detalhes em `characters/virgil/SOURCE.md`.

## effects/blood_pool

Poça de sangue dos inimigos, desenhadas por código em
`tools/pixel_art/blood_pool/generate_blood_pool.py` (numpy + Pillow, paleta AAP-64). Para regerar:

    py tools/pixel_art/blood_pool/generate_blood_pool.py

- `blood_pool_sheet.png`: 3 variantes (linhas) x 12 quadros de 64x32, já na projeção 2:1 da tela. Quadros 0-7 = a
  poça escorrendo; 8-11 = brilho em loop. O jogo (`BloodPool.cs`) a deita no chão girada para a câmera.

## effects/ritual_circle

Círculo de ritual (pentagrama) que marca no chão onde um monstro vai surgir no modo 2D, desenhado por código em
`tools/pixel_art/ritual_circle/generate_ritual_circle.py` (numpy + Pillow, paleta AAP-64). Para regerar (o `--gif`
é opcional e grava uma prévia ampliada):

    py tools/pixel_art/ritual_circle/generate_ritual_circle.py [--gif previa.gif]

- `ritual_circle_sheet.png`: 1 linha x 24 quadros de 72x40, já na projeção 2:1 da tela. Quadros 0-9 = acender (o anel
  é traçado e o pentagrama desenhado ponta a ponta); 10-15 = pulsar em loop até a wave chegar; 16-23 = estouro quando o
  monstro surge. O jogo (`SpawnRitualCircle.cs`, chamado pelo `RoomGenerator`) o deita no chão girado para a câmera.

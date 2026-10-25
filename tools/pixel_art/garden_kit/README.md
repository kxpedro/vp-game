# Kit do Jardim Sangrento (mapa 2)

Peças do modo 2D para o Jardim Sangrento, desenhadas por código com o mesmo renderizador do kit da cripta
(`../crypt_kit/iso.py`) e a mesma paleta AAP-64. A família de cor `hedge` (verde da sebe) é registrada em `garden.py`
e repetida em `PixelRoomComposer.Families`.

- `garden.py`: as peças (sebes, canteiro, topiarias, roseira, lanterna, árvore seca, chão).
- `export_garden.py`: grava `assets/pixel_art/garden/` (peças, alturas e `manifest.json`, com o chão do kit) e
  `assets/pixel_art/tiles/garden_floor/`.
- `preview_rooms.py <pasta> [semente]`: prévia das duas salas (clareira e corredores) com a mesma regra do
  `RoomGenerator.Garden.cs`.

Para regerar: `py tools/pixel_art/garden_kit/export_garden.py`.

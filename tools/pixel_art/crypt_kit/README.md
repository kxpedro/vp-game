# Kit modular da cripta (pixel art isométrica, 128x64, paleta AAP-64)

Tudo aqui é gerado por código (numpy + Pillow). Os PNGs ficam em `assets/pixel_art/crypt/`
e o chão em `assets/pixel_art/tiles/dungeon_floor/`.

| Arquivo | O que faz |
|---|---|
| `iso.py` | mini renderizador isométrico (mundo: 1 tile = 64 unidades; tela: `sx = x - y`, `sy = (x + y)/2 - z`) com z-buffer e luz por rampa de cor da AAP-64 |
| `materials.py` | texturas e objetos herdados dos concepts (pedra, madeira, sarcófago, velas, sangue...) |
| `walls.py` | paredes modulares de 1 tile; a textura é periódica a cada 64 unidades, então peças vizinhas não mostram emenda. Também a grade do portão (`portcullis`), exportada em 8 quadros para a animação de abrir |
| `props.py` | caixões, velas, arandelas, tapete, ossos e entulho em coordenadas locais do tile |
| `export_kit.py` | renderiza todas as peças em `assets/pixel_art/crypt/` + `manifest.json` + `heights/` (altura de cada pixel, usada pelo jogo) |
| `build_room.py` | monta uma sala a partir de um layout em texto, usando só os PNGs exportados, e aplica a luz das velas (prévia; o jogo monta a mesma sala em `scripts/pixel/PixelRoomComposer.cs`) |
| `gen_hub_layout.py` | gera `layouts/hub_completo.txt` (hub inteiro da planta baixa) |
| `layouts/*.txt` | exemplos: `ala_caixao.txt` (ala do caixão do Virgil) e `ossuario_exemplo.txt` |

## Montar uma sala

    py tools/pixel_art/crypt_kit/build_room.py tools/pixel_art/crypt_kit/layouts/ala_caixao.txt sala.png

No layout, `[grid]` desenha o chão (`.` normal, `;` meia-luz, `,` escuro), `[edges]` troca uma parede
(`arch`, `niche`, `gap`, `low`, `plain`, `none`), `[props]` posiciona objetos (`nome gx gy [dx dy]`).
Paredes e cantos saem sozinhos; uma parede do fundo vira "cortada" quando taparia chão atrás dela.

## Mudar o visual

Edite `walls.py`/`props.py`/`materials.py` e rode `export_kit.py`: todas as salas montadas depois já usam as peças novas.

## No jogo (modo 2D)

O menu tem "Jogar 2D (pixel art)": as cenas de `scenes/pixel/` montam as salas em tempo de execução com as mesmas
peças e a mesma luz (`scripts/pixel/PixelRoomComposer.cs`, porte do `build_room.py`). As salas fixas são layouts
deste formato em `assets/pixel_art/crypt/rooms/` (`hub_completo.txt`, `sala_bencao.txt`, `camara_intervalo.txt`);
as salas da run saem do `RoomGenerator` (`scripts/pixel/PixelArenaLayout.cs`). Um layout novo no hub ou nas salas
fixas é só editar o `.txt`; para conferir antes, rode o `build_room.py` nele.

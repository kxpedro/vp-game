# HUD em pixel art (modo 2D)

Peças de terceiros recortadas e remapeadas para a paleta AAP-64 do jogo por
`tools/pixel_art/hud/build_hud_kit.py` (recortes originais em `tools/pixel_art/hud/source/`). Para regerar:

    py tools/pixel_art/hud/build_hud_kit.py

Usadas só no modo 2D (`UiPixelKit`, em `scripts/ui/UIThemeFactory.cs`); no 3D o HUD continua com os StyleBox desenhados. Exceção: os ícones `slot_*.png` aparecem nos dois modos.

| Arquivo | Uso | Origem | Licença |
|---|---|---|---|
| `panel_frame.png` (64x64, 9-slice com borda 12) | painéis do HUD (vida, Moedas, legenda, chefe, avisos) | Retro Inventory, por ElvGames (`Inventory.png`, moldura grande) | uso comercial permitido, com crédito à ElvGames; não pode revender o pacote |
| `window_frame.png` (96x96, 9-slice com borda 18) | janelas modais e caixa de diálogo | Retro Inventory, por ElvGames (mesma moldura grande, em 3x, miolo opaco) | idem |
| `frame.png` (44x44, 9-slice com borda 8, miolo transparente) | botões, abas, linhas, cards, nós de skill e painéis internos das telas; tons neutros, tingidos em jogo pelo `PixelFrameBox` (`scripts/ui/PixelFrameBox.cs`) com a cor do estado ou da raridade | Retro Inventory, por ElvGames (`Inventory.png`, slot arredondado; o fundo cinza da folha foi apagado no recorte) | idem |
| `bar_frame.png` (76x22, 9-slice com borda 6) | moldura das barras de vida e do chefe | Pixel UI pack 3 (`All.png`, moldura arredondada) | **origem e licença a confirmar** (o pacote baixado não traz licença) |
| `bar_track_thin.png` (64x10, 9-slice com borda 2) | trilho da barra de Fúria | Pixel UI pack 3 (`All.png`, barra vazia) | **origem e licença a confirmar** |
| `coin_blood.png` (32x32) | glifo das Moedas de Sangue | Free - Raven Fantasy Icons, por Clockwork Raven Studios (`32x32.png`, moeda vermelha) | pacote gratuito do autor; conferir os termos na página da loja |
| `cursed_blood.png` (32x32) | glifo do Ícor, um frasco de sangue corrompido (HUD, Fonte, menu) e emblema da porta e do item no chão | arte própria do projeto, desenhada por código em `tools/pixel_art/hud/draw_cursed_blood.py` (paleta AAP-64) | do projeto |
| `slot_basic_attack.png`, `slot_quick_attack.png`, `slot_special_attack.png`, `slot_dash.png`, `slot_bestial_fury.png` (32x32) | ícones dos slots de herança por tipo de ataque na borda esquerda do HUD (`HUD.BlessingBar.cs`); tons neutros, tingidos em jogo com a cor da família ou apagados quando vazios | Free - Raven Fantasy Icons, por Clockwork Raven Studios (`32x32.png`, ícones de habilidade vermelhos: linha 74 col. 4, 77/3, 75/10, 77/1 e 76/5, contando de 0) | idem |

As cores foram trocadas (AAP-64, de Adigun A. Polack); o desenho é dos autores acima.

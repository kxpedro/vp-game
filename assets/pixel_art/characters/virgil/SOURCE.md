# Virgil (pixel art própria)

Sprite do Virgil feito para o Vampire's Prey (autoria própria, sem material de terceiros), a partir da arte
conceitual do projeto. Gerado por `tools/pixel_art/virgil/` (modelo 3D por SDF renderizado em pixel art, numpy + Pillow).

- `virgil_sheet.png`: folha usada pelo jogo (`scripts/player/PlayerSpriteVisual.cs`), 1280x896, células de 80x112.
  Linhas = 8 direções (n, ne, e, se, s, sw, w, nw); colunas 0-7 = idle, 8-15 = corrida. Pés a 40 px abaixo do centro
  da célula. Câmera de 30°, igual à do modo 2D. Com espada longa.
- `virgil_combat.png`: golpes, 4960x1024, células de 160x128 (pés também 40 px abaixo do centro). Linhas = as mesmas 8
  direções; colunas = corte (6), corte de volta (6), estocada (6), giro do ataque rápido (8) e dash (5). Gerada por
  `combat.py` + `combat_sheet.py`. Só com espada.
- `virgil_sheet_no_weapon.png`: a mesma folha sem arma, para a arma virar camada separada no futuro.
- `virgil_idle_sw.png` (conceito, câmera de 35°): idle, 8 quadros de 64x104 lado a lado (512x104; pés em y≈100), virado para sudoeste (3/4, baixo-esquerda), com espada longa.
- `virgil_idle_sw_no_weapon.png`: o mesmo idle sem arma (mesmo quadro de 64x104, alinhado com o de cima), para a arma entrar como camada separada.
- `frames/`: os mesmos quadros soltos.
- Ritmo sugerido: 8 quadros a ~6 fps (os GIFs de prévia usam 180/130 ms).

Escala: ~91px de altura, pensada para tiles isométricos de 128x64. No Godot, importe com filtro **Nearest**.
Para regerar a folha: em `tools/pixel_art/virgil/`, rode `py sheet.py <direção> 1` (e `0` sem arma) para cada direção e monte as células.

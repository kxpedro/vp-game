# Gerador do sprite do Virgil

Modelo 3D do Virgil por SDF (numpy), renderizado com câmera alta (35°) e reduzido para pixel art
na paleta do projeto. Python 3 + numpy + Pillow.

- `virgil3d.py`: modelo (juntas da pose em `J`, materiais, `set_pose` para animar).
- `render.py`: raymarching ortográfico; `AZ` (variável de ambiente) gira a câmera (30 = sudoeste, padrão do idle).
- `shade.py`: luz, paleta por material, redução 4x, contorno.
- `details.py`: olhos, fivelas, cruz e sangue na lâmina pintados por projeção.
- `anim.py`: idle de 8 quadros. Rodar na pasta deste arquivo:

      py anim.py 1    # com espada  -> idle_1_<n>.png
      py anim.py 0    # sem espada  -> idle_0_<n>.png

`preview_scene.py` é só da prévia na cripta e depende dos geradores da sala (fora do jogo).

Folha do jogo (8 direções x idle + corrida), câmera de 30° do modo 2D:

      py sheet.py <n|ne|e|se|s|sw|w|nw> 1   # com espada -> out/1_<dir>_<idle|run>_<n>.png
      py sheet.py <dir> 0                   # sem espada

Cada célula tem 80x112; a folha junta as direções em linhas e idle (0-7) + corrida (8-15) em colunas.

Golpes (combo de 3, giro do ataque rápido e dash), câmera de 30°:

      py combat.py <dir> [slash slash_reverse thrust spin dash]   # -> out/combat_<dir>_<anim>_<n>.png
      py combat_sheet.py                                          # monta virgil_combat.png (precisa das 8 direções)

Células de 160x128 (a espada estendida passa dos 80 px), pés 40 px abaixo do centro como na outra folha.
Colunas: corte (6), corte de volta (6), estocada (6), giro (8), dash (5). `virgil3d.ANIM["yaw"]` gira o corpo inteiro.

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

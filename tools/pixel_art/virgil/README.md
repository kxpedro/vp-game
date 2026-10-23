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

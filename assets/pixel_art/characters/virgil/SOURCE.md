# Virgil (pixel art própria)

Sprite do Virgil feito para o Vampire's Prey (autoria própria, sem material de terceiros), a partir da arte
conceitual do projeto. Gerado por `tools/pixel_art/virgil/` (modelo 3D por SDF renderizado em pixel art, numpy + Pillow).

- `virgil_idle_sw.png`: idle, 8 quadros de 64x104 lado a lado (512x104; pés em y≈100), virado para sudoeste (3/4, baixo-esquerda), com espada longa.
- `virgil_idle_sw_no_weapon.png`: o mesmo idle sem arma (mesmo quadro de 64x104, alinhado com o de cima), para a arma entrar como camada separada.
- `frames/`: os mesmos quadros soltos.
- Ritmo sugerido: 8 quadros a ~6 fps (os GIFs de prévia usam 180/130 ms).

Escala: ~91px de altura, pensada para tiles isométricos de 128x64. No Godot, importe com filtro **Nearest**.
Ainda não está ligado ao jogo (conceito aprovado; integração pendente).

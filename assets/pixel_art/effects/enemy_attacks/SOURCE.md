# Efeitos dos ataques dos monstros (modo 2D)

Recortes dos pacotes **Effect and FX Pixel All Free** e **RPG Effect All Free**, copiados de `F:\ASSETS` no PC do
Pedro. Usados por `scripts/enemies/EnemyAttackFx.cs` (golpes corpo a corpo, pancadas no chão e projéteis dos
monstros; só no 2D).

- **Autor provável:** BDragon1727 (bdragon1727.itch.io), pelo nome e pelo formato (cada efeito num PNG de 9 linhas
  de quadros de 64x64, uma por cor).
- **Licença:** NÃO REGISTRADA. Os pacotes vieram sem licença nem readme. Antes de distribuir o jogo, confirmar os
  termos na página dos pacotes e registrar aqui e em `CREDITS.md`.

Cada folha é uma faixa horizontal de quadros de 64x64 (só a linha de cor escolhida, sem os quadros vazios),
remapeada para a paleta AAP-64. Os recortes originais ficam em `tools/pixel_art/enemy_fx/source/`. Para regerar:

    py tools/pixel_art/enemy_fx/import_enemy_fx.py [--packs <pasta com os pacotes descompactados>]

| Folha | Original (arquivo, linha de cor) | Uso |
|---|---|---|
| `eye_orb` | RPG Effect `1781`, carmim | orbe do olho voador (laço) |
| `eye_orb_burst` | Effect and FX `230`, carmim | estouro do orbe |
| `spore_orb` | RPG Effect `1781`, verde | esporo da planta-bulbo (laço) |
| `spore_burst` | Effect and FX `485`, verde | respingo do esporo |
| `slash_white` | Effect and FX `438`, branco | corte do goblin |
| `spore_cloud` | Effect and FX `476`, malva | nuvem de esporos do cogumelo |
| `bite_red` | Effect and FX `316`, carmim | mordida da planta vermelha |
| `claw_red` | Effect and FX `465`, carmim | garras do zumbi |
| `dust_slam` | Effect and FX `70`, branco | poeira da pancada do esqueleto (e dos lacaios da Guardiã) |
| `thorn_burst` | Effect and FX `527`, violeta (tingido na rampa violeta) | espinhos da planta roxa |

# Assets de personagem

## Modelo atual

Desde 2026-07-26, o jogador usa `res://assets/characters/quaternius_ultimate_animated_character/Knight_Male.gltf`, do Ultimate Animated Character Pack de Quaternius (CC0 1.0).

Animações conectadas em `Player.tscn`:

| Estado | Animação |
| --- | --- |
| Parado | `Idle` |
| Caminhada | `Walk` |
| Corrida | `Run` |
| Esquiva | `Roll` |
| Cortes da Espada Grande | `SwordSlash` |
| Estocada final | `Punch` |

`PlayerAnimator` remove root motion de posição para que `PlayerController` continue autoritativo sobre deslocamento e colisão.

`PlayerWeaponVisual` encontra o esqueleto importado, cria `BoneAttachment3D` em `Fist.R` e `Fist.L` e instancia o modelo correspondente à arma equipada. A Espada Grande preserva o pivô original do cabo; não se deve recentralizar os bounds do modelo, pois isso prende o meio da lâmina à mão. O visual fica oculto durante idle e locomoção e aparece apenas enquanto `PlayerCombat.IsAttacking` estiver ativo.

Para evitar dessincronização entre modelo, hit e janela de controle, `PlayerCombat` fornece a duração total de startup + recovery ao `PlayerAnimator`. O clipe escolhido ajusta sua velocidade para caber nessa duração; ataques em linha selecionam a animação de estocada.

## Asset legado arquivado

O modelo `character.glb` não é usado pelo jogador na build atual e foi movido para fora do projeto (`../vp-0.1-arquivo/3d/`). O catálogo abaixo é mantido como referência caso seu rig seja retomado.

Arquivo arquivado: `../vp-0.1-arquivo/3d/character.glb`

O arquivo contem 45 animacoes. A numeracao abaixo segue a lista enviada pelo usuario
e parece ser 1-based; entre parenteses esta o indice 0-based interno do GLB e o nome
real da animacao.

Observacao de import: no `AnimationPlayer` do Godot, algumas animacoes aparecem sem
o sufixo `_Loop`. Exemplo: `Rig|Walk_Loop` no GLB vira `Rig|Walk` no Godot.

| Usuario | Uso desejado | Indice GLB | Nome no GLB |
| --- | --- | --- | --- |
| Animacao 9 | Idle1 | 8 | `Rig|Idle_Loop` / Godot: `Rig|Idle` |
| Animacao 10 | Talk1 | 9 | `Rig|Idle_Talking_Loop` |
| Animacao 13 | Run1 | 12 | `Rig|Jog_Fwd_Loop` |
| Animacao 17 | TakeItem1 | 16 | `Rig|PickUp_Table` |
| Animacao 21 | AimGun1 | 20 | `Rig|Pistol_Idle_Loop` |
| Animacao 22 | ReloadGun1 | 21 | `Rig|Pistol_Reload` |
| Animacao 23 | ShootGun1 | 22 | `Rig|Pistol_Shoot` |
| Animacao 24 | Punch1 | 23 | `Rig|Punch_Cross` |
| Animacao 26 | Punch2 | 25 | `Rig|Punch_Jab` |
| Animacao 28 | RollDodgeDash1 | 27 | `Rig|Roll` |
| Animacao 29 | RollDodgeDash2 | 28 | `Rig|Roll_RM` |
| Animacao 30 | SitDown1 | 29 | `Rig|Sitting_Enter` |
| Animacao 31 | UpFromSit1 | 30 | `Rig|Sitting_Exit` |
| Animacao 32 | Seated1 | 31 | `Rig|Sitting_Idle_Loop` |
| Animacao 36 | Hand1 | 35 | `Rig|Spell_Simple_Idle_Loop` |
| Animacao 37 | ShootingFromHand1 | 36 | `Rig|Spell_Simple_Shoot` |
| Animacao 38 | Run2 | 37 | `Rig|Sprint_Loop` / Godot: `Rig|Sprint` |
| Animacao 41 | Punch3 | 40 | `Rig|Sword_Attack` |
| Animacao 42 | Punch4 | 41 | `Rig|Sword_Attack_RM` |
| Animacao 43 | Idle2 | 42 | `Rig|Sword_Idle` |
| Animacao 44 | Walk1 | 43 | `Rig|Walk_Formal_Loop` / Godot: `Rig|Walk_Formal` |
| Animacao 45 | Walk2 | 44 | `Rig|Walk_Loop` / Godot: `Rig|Walk` |

Observacao: na lista original havia duas entradas como "Animacao 28". Pelo conteudo do
GLB, elas correspondem provavelmente as animacoes 28 e 29 em numeracao 1-based:
`Rig|Roll` e `Rig|Roll_RM`.

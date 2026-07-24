# Sistema de inimigos comuns

## Arquivos centrais

- `scenes/enemies/EnemyBasic.tscn`: composição base, colisão, vida e visual.
- `scripts/enemies/EnemyBasic.cs`: máquina de comportamento, ataques, projéteis, status, feedback, stagger e empurrão.
- `scripts/enemies/EnemyProjectile.cs`: voo, colisão, dano, alcance e pausa de projéteis.
- `scripts/enemies/EnemyTuningCatalog.cs`: raridades Normal, Magic, Rare e Legendary.
- `scripts/enemies/EnemyVisualCatalog.cs`: assets reservados para bosses; inimigos comuns usam geometria.
- `scripts/systems/RoomGenerator.cs`: sorteio de arquétipo, posição e instanciação em waves.
- `scripts/systems/RunManager.cs`: vida, dano, recompensa e progressão por mapa/sala.
- `scripts/combat/Health.cs`: dano, morte, shields e sinais.
- `scripts/player/PlayerCombat.cs`: seleção espacial de alvos, raio físico, stagger e knockback.

## Contratos do runtime

- Todo inimigo comum entra no grupo `enemies`.
- `HealthPath` deve resolver para `Health`; `VisualPath`, para o visual principal.
- `ConfigureVariant` é chamado depois de adicionar a instância à árvore.
- `SetSpawnActive(false/true)` protege o período de materialização.
- `SpawnAttackDelay` impede ataque imediato.
- `Health.Damaged`, `Health.HealthChanged` e `Health.Died` alimentam feedback e progressão.
- Projéteis pertencentes ao inimigo devem ser liberados quando ele sai da árvore.
- `ContactDamage` representa a fração de `AttackDamage`; o padrão `0.05` causa 5% por contato a cada cooldown.

## Arquétipos atuais

| Arquétipo | Forma/cor | Função |
| --- | --- | --- |
| Melee | cilindro ciano | aproximação e pressão curta |
| Ranged | prisma rosa-choque | distância preferencial, fuga e flecha |
| Tank | cubo amarelo | vida alta, resistência e golpe amplo |

`RoomGenerator.RollEnemyArchetype` usa aproximadamente 26,67% ranged e divide o restante entre melee e tank. Waves possuem 5 inimigos e tentam manter 2,35 unidades entre spawns.

## Status e defesa

- Sangramento e Pestilência possuem acúmulos visíveis.
- Cada acúmulo aumenta o dano recebido em 5% pelo padrão de `DamageTakenPerStatusStack`.
- Sangramento também multiplica seu dano periódico pelo total de acúmulos.
- Cinco acúmulos de Pestilência podem formar um Hospedeiro.
- Labels de stack usam `FontSize = 64`, `PixelSize = 0.006`, cor do status, `NoDepthTest` e prioridade de render.
- Encanto dura 5 segundos base; `CharmDamageLink` cria uma linha emissiva rosa até o alvo vinculado.
- Bosses e Legendary são imunes a Encanto; com a bênção adequada recebem Fascínio.
- Bosses, shields ativos e tanks Rare/Legendary possuem armadura de stagger.

Críticos usam amarelo-ouro e fonte três vezes maior que o dano normal. O flash vermelho de impacto continua temporário e não substitui a cor do arquétipo.

## Checklist

- forma, emissão e halo corretos;
- raio visual coerente com `HitTargetRadius`;
- telegraph precede dano;
- hitbox acompanha a área desenhada;
- ataque não atravessa pausa;
- comportamento não trava em parede;
- stagger e knockback possuem reação;
- status e números de acúmulo aparecem;
- morte contabiliza wave, moedas e fúria uma vez;
- build e arena headless sem erros.

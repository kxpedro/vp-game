---
name: vp-create-enemy
description: Criar, implementar, revisar, balancear ou atualizar inimigos comuns do Vampire's Prey. Usar ao alterar arquétipos melee, ranged ou tank, IA, perseguição, fuga, ataques, telegraphs, projéteis, hitboxes, raridades, resistências, visuais geométricos, spawn ou integração com status e recompensas.
---

# Criar ou atualizar inimigo

Implementar inimigos comuns legíveis, compatíveis com as waves procedurais e com todos os sistemas de combate do jogador.

## Fluxo

1. Ler `references/enemy-system.md`.
2. Inspecionar `EnemyBasic`, os catálogos de tuning/visual, `RoomGenerator`, `RunManager` e a cena base antes de editar.
3. Definir arquétipo, função tática, alcance, movimento, ataque, windup, cooldown, hitbox, recompensa e resistências.
4. Preferir propriedades e estados de `EnemyBasic` para variações comuns. Criar outro script apenas quando o comportamento não couber de forma clara no controlador existente.
5. Manter a decisão tática separada da execução do golpe: perseguição, reposicionamento ou fuga escolhem a ação; windup e telegraph antecedem dano.
6. Ajustar `ConfigureVariant` quando a mudança for própria de melee, ranged ou tank. Ajustar `EnemyTuningCatalog` quando for própria de raridade.
7. Preservar os contratos de status, stagger, empurrão, parede, Fascínio, Hospedeiro, grupos e sinais de `Health`.
8. Garantir que ranged e projéteis respeitem pausa, limpeza ao morrer e leitura visual. Garantir que tank tenha preparação proporcional à área e ao dano.
9. Verificar dano de contato como fração do ataque, stacks visíveis e feedback crítico sem alterar o boneco de treinamento.
10. Verificar spawn em wave, distância das paredes, espaçamento, primeiro atraso de ataque e escalonamento por mapa.
11. Executar `dotnet build VP0.1.sln --no-restore` e abrir `TestArena.tscn` em modo headless. Testar visualmente colisão, telegraph, ataque, morte e recompensas quando o Godot interativo estiver disponível.

## Regras

- Não alterar bosses por meio desta skill; usar `vp-create-boss`.
- Não substituir cores de arquétipo por flash permanente. O vermelho de impacto deve ser temporário.
- Não causar dano antes do telegraph terminar.
- Não mover projéteis durante pausa.
- Não ignorar `HitTargetRadius`, `Health`, `HasStaggerArmor` ou `KnockbackResistance`.
- Manter contato corporal em 5% de `AttackDamage` por cooldown; `TrainingDummy` deve continuar com contato zero.
- Manter números de stack com o tamanho do ícone, cor do efeito e renderização acima da geometria.
- Não criar um inimigo apenas no gerador: confirmar cena, configuração, runtime, HUD e limpeza.
- Manter melee ciano/cilindro, ranged rosa-choque/prisma e tank amarelo/cubo enquanto os placeholders geométricos forem a direção vigente.

## Entrega

Informar arquétipo, parâmetros alterados, comportamento tático, telegraph/hitbox, integração com status e testes executados.

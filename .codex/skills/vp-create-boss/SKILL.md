---
name: vp-create-boss
description: Criar, implementar, revisar, balancear ou atualizar bosses do Vampire's Prey. Usar ao editar BossCatalog, BossController, fases, movesets, seleção de golpes, telegraphs, arenas, vida, dano, assets, animações, barra de boss, recompensa de artefato ou transição pós-chefe.
---

# Criar ou atualizar boss

Implementar bosses com identidade mecânica, fases legíveis e integração completa ao fluxo da campanha.

## Fluxo

1. Ler `references/boss-system.md`.
2. Inspecionar `BossCatalog`, `BossController`, os métodos públicos de boss em `EnemyBasic`, `EnemyVisualCatalog`, `RunManager`, `RoomGenerator` e a arena.
3. Definir fantasia, duração-alvo, DPS esperado, vida estimada, fases e função de cada golpe.
4. Cadastrar a definição em `BossCatalog` e alinhar seu índice ao mapa em `CampaignCatalog`.
5. Reutilizar executores existentes quando o formato do golpe coincidir. Para golpe novo, adicionar ID, executor, telegraph, dano e animação coerentes.
6. Fazer todo golpe perigoso comunicar posição, área e tempo antes do dano. Usar telegraph circular para áreas e linear para investidas.
7. Projetar teleporte e reposicionamento para uma célula navegável segura; investidas físicas devem colidir com paredes.
8. Garantir cancelamento seguro quando boss morre, sai da árvore ou troca de cena. Liberar telegraphs e desbloquear ações apenas em instâncias válidas.
9. Preservar imunidade a Encanto, Fascínio, armadura de stagger, resistência a empurrão e barra exclusiva do HUD.
10. Verificar morte, Altar de Artefatos interativo, portão e Câmara de Hemomancia ou encerramento final.
11. Executar `dotnet build VP0.1.sln --no-restore`, abrir `TestArena.tscn` em modo headless e rodar o smoke test de telegraph somente quando o script correspondente existir.

## Regras

- Não usar crescimento ou tingimento vermelho permanente como fase sem pedido explícito.
- Não inflar vida sem relacioná-la a `TargetDurationSeconds * ExpectedPlayerDps`.
- Não repetir o mesmo golpe consecutivamente quando houver outra opção válida.
- Não aplicar dano antes do fim do windup.
- Não enterrar telegraphs sob o piso procedural.
- Não escrever `GlobalPosition` de teleporte sem passar por `RoomGenerator.ClampToWalkableArea`; usar margem de duas células para bosses.
- Não deixar tarefas assíncronas atuarem sobre nodes inválidos.
- Não considerar o boss concluído sem altar de recompensa interativo, HUD e transição pós-morte.
- Não editar inimigos comuns fora do necessário para um contrato compartilhado; usar `vp-create-enemy` para esse trabalho.

## Entrega

Informar definição, vida estimada, fases, golpes por fase, janelas de reação, multiplicadores, integração visual/HUD/recompensa e testes executados.

# Sistema de artefatos

## Arquivos centrais

- `data/artifacts/artifacts.json`: catálogo e valores base.
- `scripts/systems/artifacts/ArtifactDefinition.cs`: modelo carregado do JSON.
- `scripts/systems/artifacts/ArtifactCatalog.cs`: parsing, filtro passivo, lookup, pesos e multiplicador global de drops de boss.
- `scripts/systems/artifacts/ArtifactEffectRegistry.cs`: nomes exibidos para os tipos de efeito; não implementa gameplay.
- `scripts/systems/RunState.cs`: aplica e agrega efeitos passivos da run.
- `scripts/player/PlayerCombat.cs`, `scripts/player/PlayerController.cs` e `scripts/combat/Health.cs`: consumidores comuns de modificadores passivos.
- `scripts/ui/HUD.cs`: ofertas, coleção da run, descobertas permanentes, formatação e compêndio.
- `scripts/systems/CompendiumTuning.cs`: overrides de valores e pesos.

## Forma da entrada

Campos esperados: `id`, `name`, `icon`, `rarity`, `slot`, `weight`, `tags`, `description`, `flavorText` e `effects`. Cada efeito possui `type`, `operation` e `value`.

Usar sempre `slot: Passive`. O catálogo filtra defensivamente entradas ativas legadas, e o validador deve acusá-las. Reutilizar operações existentes salvo quando uma nova semântica for realmente necessária.

## Contrato de design

Artefatos são exclusivamente passivos. Não existem slots ativos, botões separados, cooldown global ou controlador de ativação. `Q` pertence ao ataque especial e `F` à Fúria Bestial.

Conceitos antes ativos devem virar modificadores contínuos, reações automáticas a eventos ou gatilhos por contagem, chance, estado de vida e cooldown interno automático.

## Caminho de execução

1. `ArtifactCatalog` carrega a definição.
2. `RunState` registra a aquisição e agrega os efeitos.
3. Um sistema de gameplay consulta o total por uma chave compatível.
4. A HUD exibe a descoberta na run e a coleção permanente no Baú de Artefatos.

O catálogo usa `bossDropPowerMultiplier` sobre efeitos numéricos. Verificar se a multiplicação faz sentido para duração, contagem, chance e fatores inversos como `EnemyTimeScale`.

## Recompensa de boss

`RunManager.SpawnArtifact` instancia um `RoomArtifact` apresentado como Altar de Artefatos no ponto retornado por `RoomGenerator.GetArtifactSpawnPosition()`. A morte do boss não abre a oferta automaticamente:

1. o altar surge com área, prompt e relíquia visual;
2. o jogador entra na área e usa `interact`;
3. `HUD.OpenArtifactOffer` pausa o jogo e sorteia três passivos;
4. a escolha chama `RoomArtifact.Consume`;
5. somente então `UnlockExitAfterReward` abre o portão.

Preservar esse bloqueio ao alterar recompensas, reroll ou ausência de escolhas.

## Coleção e armadilhas

- `ArtifactEffectRegistry` fornece somente texto amigável.
- O baú combina os artefatos adquiridos na run atual com os descobertos em runs anteriores no slot.
- Não inferir ausência da coleção apenas pelo estado permanente enquanto uma run está ativa.
- `ArtifactCatalog.CreateRecord` resume apenas o primeiro efeito para `ArtifactRecord`; a definição completa continua disponível no catálogo.
- Raridades desconhecidas caem no multiplicador padrão de peso. Confirmar suporte antes de introduzir outra raridade.
- Alguns tipos já cadastrados podem ser conceituais ou parcialmente implementados. Procurar o nome do tipo em todos os `.cs` e seguir o valor até o resultado em gameplay.
- Todos os registros devem usar `slot: Passive`; não reintroduzir ativação por tecla, slot ou cooldown.

# Sistema de artefatos

## Arquivos centrais

- `data/artifacts/artifacts.json`: catálogo e valores base.
- `scripts/systems/artifacts/ArtifactDefinition.cs`: modelo carregado do JSON.
- `scripts/systems/artifacts/ArtifactCatalog.cs`: parsing, lookup, pesos e multiplicador global de drops de boss.
- `scripts/systems/artifacts/ArtifactEffectRegistry.cs`: nomes exibidos para os tipos de efeito; não implementa gameplay.
- `scripts/systems/RunState.cs`: aplica e agrega efeitos passivos da run.
- `scripts/player/PlayerArtifactController.cs`: equipa e executa artefatos ativos nos slots Q/R.
- `scripts/player/PlayerCombat.cs`, `scripts/player/PlayerController.cs` e `scripts/combat/Health.cs`: consumidores comuns de modificadores passivos.
- `scripts/ui/HUD.cs`: ofertas, inventário, slots ativos, formatação e compêndio.
- `scripts/systems/CompendiumTuning.cs`: overrides de valores e pesos.

## Forma da entrada

Campos esperados: `id`, `name`, `icon`, `rarity`, `slot`, `weight`, `tags`, `description`, `flavorText` e `effects`. Cada efeito possui `type`, `operation` e `value`.

Usar `slot` como `Passive` ou `Active`. Reutilizar operações presentes no catálogo, salvo quando a implementação exigir uma nova semântica claramente definida.

## Caminhos de execução

Artefato passivo:

1. `ArtifactCatalog` carrega a definição.
2. `RunState` percorre os efeitos e armazena totais.
3. Um sistema de gameplay consulta o total por uma chave compatível.

Artefato ativo:

1. A HUD/equipamento coloca o artefato em Q ou R.
2. `PlayerArtifactController` percorre seus efeitos.
3. `ApplyEffect` precisa retornar `true` para pelo menos um efeito implementado.

O catálogo usa `bossDropPowerMultiplier` sobre efeitos numéricos. Verificar se a multiplicação faz sentido para duração, contagem, chance e fatores inversos como `EnemyTimeScale`.

## Armadilhas atuais

- `ArtifactEffectRegistry` fornece somente texto amigável.
- O cooldown dos ativos é atualmente global em `PlayerArtifactController.DefaultCooldownSeconds`, não um campo do JSON.
- `ArtifactCatalog.CreateRecord` resume apenas o primeiro efeito para `ArtifactRecord`; a definição completa continua disponível no catálogo.
- Raridades desconhecidas caem no multiplicador padrão de peso. Confirmar suporte antes de introduzir outra raridade.
- Alguns tipos já cadastrados podem ser conceituais ou parcialmente implementados. Procurar o nome do tipo em todos os `.cs` e seguir o valor até o resultado em gameplay.


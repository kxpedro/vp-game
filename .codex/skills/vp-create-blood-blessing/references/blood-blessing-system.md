# Sistema de Bênçãos de Sangue

## Arquivos centrais

- `data/blessings/blood_blessings.json`: famílias, escala de raridade, definições e parâmetros.
- `scripts/systems/blessings/BloodBlessingDefinition.cs`: modelos de definição, família, raridade e efeitos.
- `scripts/systems/blessings/BloodBlessingCatalog.cs`: parsing, oferta por família e sorteio de raridade.
- `scripts/systems/RunState.cs`: aquisição, agregação, parâmetros e reset de efeitos da run.
- `scripts/player/BloodBlessingRuntime.cs`: comportamentos condicionais e reações a combate/movimento/cura.
- `scripts/player/PlayerCombat.cs`, `scripts/player/PlayerController.cs`, `scripts/combat/Health.cs` e `scripts/enemies/EnemyBasic.cs`: consumidores comuns.
- `scripts/systems/artifacts/ArtifactEffectRegistry.cs`: nomes amigáveis também usados na formatação das bênçãos.
- `scripts/ui/HUD.cs`: oferta, formatação e compêndio.

## Forma da entrada

Campos principais: `id`, `name`, `family`, `rarity`, `weight`, `maxStacks`, `description`, `flavorText`, `tags` e `effects`.

Cada efeito exige `type`, `operation` e `value`, podendo carregar parâmetros numéricos, textuais ou booleanos adicionais. O parser separa esses parâmetros em `Numbers` e `Texts`.

Os valores representam a base Common. `rarityScaling` define multiplicadores e pesos para Common, Uncommon, Rare, Epic e Legendary. Parâmetros como duração e threshold não devem escalar.

## Caminho de execução

1. `BloodBlessingCatalog` escolhe uma família e oferece definições ainda não escolhidas.
2. Cada oferta recebe uma raridade sorteada e seu multiplicador.
3. `RunState.ApplyBlessingEffect` armazena o valor escalado e os parâmetros.
4. Sistemas diretos ou `BloodBlessingRuntime` consultam as chaves e aplicam o comportamento.

Procurar o tipo de efeito em todos os `.cs`. Confirmar onde o valor é lido, qual sufixo (`.Flat` ou `.Multiplier`) é usado e em qual evento o efeito é acionado.

## Armadilhas atuais

- O loader constrói cada `BloodBlessingDefinition` com `MaxStacks = 1`, ignorando atualmente o `maxStacks` do JSON.
- A oferta também exclui uma definição quando seu stack já é diferente de zero. Não descrever progressão por vários níveis como funcional sem corrigir e testar esse fluxo.
- `rarity` na entrada não limita a raridade sorteada; todas usam a tabela global.
- Um efeito pode ser parseado e exibido sem possuir consumidor de gameplay.
- Booleanos ficam em `BooleanValue` quando são o campo `value`; outros booleanos adicionais são armazenados como texto.


# Sistema de Bênçãos de Sangue

## Arquivos centrais

- `data/blessings/blood_blessings.json`: famílias, escala de raridade, definições e parâmetros.
- `scripts/systems/blessings/BloodBlessingDefinition.cs`: modelos de definição, família, raridade e efeitos.
- `scripts/systems/blessings/BloodBlessingCatalog.cs`: parsing, oferta por família e sorteio de raridade.
- `scripts/systems/RunState.cs`: aquisição, agregação, parâmetros e reset de efeitos da run.
- `scripts/player/BloodBlessingRuntime.cs`: comportamentos condicionais e reações a combate/movimento/cura.
- `scripts/player/PlayerCombat.cs`: ataque básico, rápido, especial e Fúria Bestial.
- `scripts/player/PlayerController.cs`, `scripts/combat/Health.cs` e `scripts/enemies/EnemyBasic.cs`: consumidores adicionais.
- `scripts/systems/artifacts/ArtifactEffectRegistry.cs`: nomes amigáveis também usados na formatação das bênçãos.
- `scripts/ui/HUD.cs`: oferta, formatação e compêndio.

## Moveset obrigatório

- ataque básico: botão esquerdo;
- ataque rápido: esquiva + ataque básico;
- ataque especial: `Q`;
- Fúria Bestial: `F`, somente quando uma bênção aplicar `UnlockBestialFury`.

As bênçãos são a camada temporária que altera diretamente esses ataques. Manter opções com as tags `basic_attack`, `quick_attack`, `special_attack` e `bestial_fury`. A primeira oferta da run precisa incluir ao menos uma definição `basic_attack`; as opções restantes são sorteadas normalmente, sem duplicá-la.

## Forma da entrada

Campos principais: `id`, `name`, `family`, `rarity`, `weight`, `maxStacks`, `description`, `flavorText`, `tags` e `effects`.

Cada efeito exige `type`, `operation` e `value`, podendo carregar parâmetros numéricos, textuais ou booleanos adicionais. O parser separa esses parâmetros em `Numbers` e `Texts`.

Os valores representam a base Common. `rarityScaling` define multiplicadores e pesos para Common, Uncommon, Rare, Epic e Legendary. Duração, threshold, booleanos, raio estrutural e contagens discretas só escalam quando a mecânica declarar isso expressamente.

## Caminho de execução

1. `BloodBlessingCatalog` escolhe uma família e oferece definições ainda não escolhidas.
2. Cada oferta recebe uma raridade sorteada e seu multiplicador.
3. `RunState.ApplyBlessingEffect` armazena o valor escalado e os parâmetros.
4. Sistemas diretos ou `BloodBlessingRuntime` consultam as chaves e aplicam o comportamento.

Procurar o tipo de efeito em todos os `.cs`. Confirmar onde o valor é lido, qual sufixo (`.Flat` ou `.Multiplier`) é usado e em qual evento o efeito é acionado.

## Integrações especiais

- `UnlockBestialFury` habilita o input `F` durante a run; bônus de Fúria só têm efeito depois disso.
- Poças criadas pelo especial usam a origem e a dimensão da área radial real do golpe.
- Ataque rápido é o golpe durante a janela de esquiva, não um input separado.
- Toda bênção de ataque básico precisa continuar elegível para a garantia da primeira oferta.

## Armadilhas atuais

- As bênçãos são deliberadamente únicas: o loader fixa `MaxStacks = 1` e a oferta exclui definições já escolhidas. A qualidade sorteada é a progressão de poder; não descrever efeitos como ganho "por nível".
- Bênçãos com `exclusiveGroup` substituem a escolha anterior do mesmo grupo. A HUD precisa continuar avisando essa troca antes da confirmação.
- `rarity` na entrada não limita a raridade sorteada; todas usam a tabela global.
- Um efeito pode ser parseado e exibido sem possuir consumidor de gameplay.
- Booleanos ficam em `BooleanValue` quando são o campo `value`; outros booleanos adicionais são armazenados como texto.
- Um efeito parseado e exibido não está implementado até possuir consumidor de gameplay.
- Ao iniciar outra run, todos os efeitos e o desbloqueio da Fúria devem ser removidos.

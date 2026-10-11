# Sistema de artefatos

## Arquivos centrais

- `data/artifacts/artifacts.json`: catálogo e valores base.
- `scripts/systems/artifacts/ArtifactDefinition.cs`: modelo carregado do JSON.
- `scripts/systems/artifacts/ArtifactCatalog.cs`: parsing, filtro passivo, lookup, pesos, estoque e preços da loja (`RollShopStock`, `GetShopPrice`) e o multiplicador de poder dos artefatos comprados.
- `scripts/systems/artifacts/ArtifactEffectRegistry.cs`: tipos de efeito conhecidos e formatação de valor; não implementa gameplay. Os nomes exibidos ficam em `content.json` (`artifact.effect.<Tipo>`).
- `data/localization/<idioma>/content.json`: nome, descrição e flavor de cada artefato.
- `scripts/systems/RunState.cs`: aplica e agrega efeitos passivos da run.
- `scripts/player/PlayerCombat.cs`, `scripts/player/PlayerController.cs` e `scripts/combat/Health.cs`: consumidores comuns de modificadores passivos.
- `scripts/ui/HUD.cs`: loja da Câmara, lista de artefatos da run, formatação e compêndio.
- `scripts/systems/CompendiumTuning.cs`: overrides de valores e pesos.

## Forma da entrada

Campos esperados: `id`, `saveName`, `icon`, `rarity`, `slot`, `weight`, `tags` e `effects`. Cada efeito possui `type`, `operation` e `value`.

`saveName` é o identificador legado (`ArtifactRecord.Name` guarda esse texto): não é exibido, não pode mudar e deve ser único. Artefatos não são mais gravados no save (só valem na run), mas o ID continua estável por causa do diário de descobertas. Para artefato novo, usar o próprio ID. `name`, `description` e `flavorText` no catálogo são erro no validador.

## Textos (localização)

Ficam em `data/localization/<idioma>/content.json` (`pt-BR`, `en`, `es`, sempre os três):

- `artifact.<id>.name`, `artifact.<id>.desc`, `artifact.<id>.flavor`;
- `artifact.effect.<Tipo>`: nome amigável de cada tipo de efeito (o tipo também entra em `KnownTypes` do `ArtifactEffectRegistry`);
- `artifact.effect_value.active` / `.inactive`: valor de efeitos booleanos.

`ArtifactDefinition.DisplayName`/`Description`/`FlavorText` resolvem a chave no idioma atual a cada leitura. Para exibir um `ArtifactRecord` (save ou run), usar `ArtifactCatalog.DisplayNameFor(record)`; nunca exibir `record.Name`. `ArtifactEffectRegistry.FormatValue` usa o separador decimal do idioma (ponto em `en`, vírgula em `pt-BR`/`es`).

Usar sempre `slot: Passive`. O catálogo filtra defensivamente entradas ativas legadas, e o validador deve acusá-las. Reutilizar operações existentes salvo quando uma nova semântica for realmente necessária.

## Contrato de design

Artefatos são exclusivamente passivos e valem só na run. Não existem slots ativos, slots equipados, coleção permanente, botões separados, cooldown global ou controlador de ativação. `Q` pertence ao ataque especial e `F` à Fúria Bestial.

Conceitos antes ativos devem virar modificadores contínuos, reações automáticas a eventos ou gatilhos por contagem, chance, estado de vida e cooldown interno automático.

## Caminho de execução

1. `ArtifactCatalog` carrega a definição.
2. `RunState` registra a aquisição e agrega os efeitos.
3. Um sistema de gameplay consulta o total por uma chave compatível.
4. A HUD exibe os artefatos da run e a loja da Câmara.

O catálogo usa `bossDropPowerMultiplier` (exposto como `economy.shop.power_multiplier`, padrão 1,75) sobre efeitos numéricos. Verificar se a multiplicação faz sentido para duração, contagem, chance e fatores inversos como `EnemyTimeScale`.

## Loja da Câmara de Hemomancia

Artefatos são vendidos só na loja da Câmara, por Moedas de Sangue da run (`RunState.RunBloodCoins`, gasto por `RunState.TrySpendRunBloodCoins`). Valem só na run, sem limite de quantidade, e zeram em `StartRun`, `FailRun` e no fim da campanha.

- `ArtifactCatalog.RollShopStock(rng, ownedIds)`: 3 a 4 artefatos (50% de chance do 4º), sem repetição, sem os que a run já tem (`economy.shop.exclude_owned`) e com o primeiro slot sempre Common ou Uncommon. A raridade sai da tabela da visita (`economy.shop.rarity_weight.<raridade>.<visita>`) e, dentro da classe, vale o campo `weight`. A visita é `CurrentMapIndex - 1`, porque a Câmara carrega depois de `CompleteBoss` incrementar o mapa.
- Preço: `economy.shop.price.<raridade>` (250/420/700/1.100/1.700/2.400) com o desconto da skill `humanitas.collector_eye` (5% por rank) via `ArtifactCatalog.GetShopPrice`.
- Compra: `RunState.TryBuyShopArtifact(definition, price)` gasta as moedas e chama `AddTemporaryArtifact`.
- Todos os números e o racional estão em `docs/design/economia-v2.md` (seções 6 e 9.4). Artefato novo precisa de raridade com preço definido; não crie preço por artefato.

## Armadilhas

- `ArtifactEffectRegistry` fornece somente texto amigável.
- Não existe coleção permanente: `CharacterSheet` não guarda artefatos. O diário de descobertas (`CodexProgress`) registra o que já foi visto.
- `ArtifactCatalog.CreateRecord` resume apenas o primeiro efeito para `ArtifactRecord`; a definição completa continua disponível no catálogo.
- O sorteio da loja só conhece Common, Uncommon, Rare, Epic, Legendary e Mythic; outra raridade fica fora do estoque. Confirmar suporte antes de introduzir uma nova.
- Alguns tipos já cadastrados podem ser conceituais ou parcialmente implementados. Procurar o nome do tipo em todos os `.cs` e seguir o valor até o resultado em gameplay.
- Todos os registros devem usar `slot: Passive`; não reintroduzir ativação por tecla, slot ou cooldown.

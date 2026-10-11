# Sistema de Bênçãos de Sangue

## Arquivos centrais

- `data/blessings/blood_blessings.json`: famílias, escala de raridade, definições e parâmetros.
- `scripts/systems/blessings/BloodBlessingDefinition.cs`: modelos de definição, família, raridade e efeitos.
- `scripts/systems/blessings/BloodBlessingCatalog.cs`: parsing, oferta por família e sorteio de raridade.
- `scripts/systems/RunState.cs`: aquisição, agregação, parâmetros e reset de efeitos da run.
- `scripts/player/BloodBlessingRuntime.cs`: comportamentos condicionais e reações a combate/movimento/cura.
- `scripts/player/PlayerCombat.cs`: ataque básico, rápido, especial e Fúria Bestial.
- `scripts/player/PlayerController.cs`, `scripts/combat/Health.cs` e `scripts/enemies/EnemyBasic.cs`: consumidores adicionais.
- `scripts/systems/artifacts/ArtifactEffectRegistry.cs`: tipos conhecidos e formatação de valores; nomes amigáveis em `content.json` (`artifact.effect.<Tipo>`).
- `data/localization/<idioma>/content.json`: nome, descrição (modelo) e flavor de cada bênção.
- `scripts/ui/HUD.BlessingOffer.cs`, `HUD.BlessingBar.cs`, `HUD.CharacterSheet.cs`, `HUD.Compendium.cs`: oferta, barra, ficha e compêndio (só chamam `BloodBlessingText`).

## Moveset obrigatório

- ataque básico: botão esquerdo;
- ataque rápido: esquiva + ataque básico;
- ataque especial: `Q`;
- Fúria Bestial: `F`, somente quando uma bênção aplicar `UnlockBestialFury`.

As bênçãos são a camada temporária que altera diretamente esses ataques. Manter opções com as tags `basic_attack`, `quick_attack`, `special_attack` e `bestial_fury`. A primeira oferta da run precisa incluir ao menos uma definição `basic_attack`; as opções restantes são sorteadas normalmente, sem duplicá-la.

## Forma da entrada

Campos principais: `id`, `family`, `rarity`, `weight`, `maxStacks`, `tags` e `effects` (mais `exclusiveGroup`, `parentFamilies`, `requiresAnyEffects`/`requiresAllEffects` e `icon` quando aplicáveis). O catálogo **não** guarda texto: `name`, `description` e `flavorText` no JSON são erro no validador.

## Textos (localização)

Nome, descrição e flavor ficam em `data/localization/<idioma>/content.json` (`pt-BR`, `en`, `es`, sempre os três), com chaves derivadas do ID:

- `blessing.<id>.name`, `blessing.<id>.desc`, `blessing.<id>.flavor`;
- `artifact.effect.<Tipo>`: nome amigável de cada `type` de efeito (compartilhado com artefatos; o tipo também deve constar em `KnownTypes` do `ArtifactEffectRegistry`);
- `blessing.keyword.<kw>.term` / `.hint`: termo e explicação do glossário da oferta;
- `blessing.family.<familia>.theme`: tema da linhagem. Os nomes das linhagens (Dracul, Bathory, Karnstein, Orlok, Stirps) não se traduzem e continuam no campo `families[].name`.

A descrição é um **modelo**:

- `{n}` recebe o valor exibido do efeito de índice `n` (base × raridade × nível × tuning do compêndio; em `%` quando a operação contém `Multiplier`, o tipo contém `Chance` ou o tipo é fracionário em `ArtifactEffectRegistry.FlatPercentTypes`; o sinal fica no texto, ex.: "{0} menos dano"). Efeitos booleanos/`Set`/`SetBool` não têm valor: não usar `{n}` para eles. Os três idiomas precisam usar os mesmos índices.
- Parâmetros estruturais (duração, limiar, acúmulos) que não escalam ficam escritos no texto de cada idioma.
- Palavras-chave são marcadas na própria string: `[kw=<id>]texto[/kw]`, com `<id>` em `basic_attack`, `quick_attack`, `special_attack`, `fury`, `bleed`, `pestilence`, `barrier`, `fascination`, `host`, `interrupt`, `wound`, `charm`. A UI converte em BBCode colorido e monta o glossário (`BloodBlessingText.FormatKeywords`/`CollectKeywords`); tooltips recebem o texto sem marcação. Id novo exige cor em `BloodBlessingText.KeywordStyles`, chaves `.term`/`.hint` e entrada em `KEYWORD_IDS` do validador.

A montagem fica em `BloodBlessingText.FormatDescription` (`scripts/systems/blessings/BloodBlessingDefinition.cs`); `BloodBlessingDefinition.DisplayName`/`Description`/`FlavorText` resolvem a chave no idioma atual a cada leitura (a troca de idioma vale sem recarregar o catálogo). `BloodBlessingRecord.Name` é só o nome no momento da aquisição; para exibir use `DisplayName`.

O arquivo é JSONC: `BloodBlessingCatalog` ignora comentários e vírgulas finais, e o validador local remove `//` e `/* ... */` antes do parse. Para desativar sem perder conteúdo, comentar o objeto inteiro. O pool atual possui 63 entradas ativas: 15 Dracul, 15 Bathory, 14 Orlok, 15 Karnstein e 4 Stirps.

Cada efeito exige `type`, `operation` e `value`, podendo carregar parâmetros numéricos, textuais ou booleanos adicionais. O parser separa esses parâmetros em `Numbers` e `Texts`.

Os valores representam a base Common. `rarityScaling` define multiplicadores e pesos para Common, Uncommon, Rare, Epic e Legendary. Cada bênção adquirida começa no nível 1 e pode ser aprimorada até o nível 10 sem alterar sua raridade. Cada nível adicional soma 25% do valor já escalado pela raridade. Duração, threshold, booleanos, raio estrutural e contagens discretas só escalam quando a mecânica declarar isso expressamente.

Valores de efeitos cujo tipo contém `Chance` são limitados a 100% ao serem agregados no `RunState`. A HUD e a prévia do Altar aplicam o mesmo limite; uma raridade ou nível maior nunca deve exibir nem armazenar chance acima de 100%.

## Caminho de execução

1. `BloodBlessingCatalog` escolhe uma família e oferece definições ainda não escolhidas.
2. Cada oferta recebe uma raridade sorteada e seu multiplicador.
3. `RunState.ApplyBlessingEffect` armazena o valor escalado pela raridade e pelo nível.
4. Sistemas diretos ou `BloodBlessingRuntime` consultam as chaves e aplicam o comportamento.

Procurar o tipo de efeito em todos os `.cs`. Confirmar onde o valor é lido, qual sufixo (`.Flat` ou `.Multiplier`) é usado e em qual evento o efeito é acionado.

## Integrações especiais

- `UnlockBestialFury` habilita o input `F` durante a run; bônus de Fúria só têm efeito depois disso.
- A Fúria base acumula 100 pontos, consome 10 por segundo e explode por 15% do dano básico ao chegar a zero. `FuryDuration` e skills externas alteram a taxa efetiva.
- Poças criadas pelo especial usam a origem e a dimensão da área radial real do golpe.
- Ataque rápido é o golpe durante a janela de esquiva, não um input separado.
- Toda bênção de ataque básico precisa continuar elegível para a garantia da primeira oferta.

## Status e defesa

- Sangramento e Pestilência acumulam; cada stack aumenta em 5% o dano recebido, e Sangramento escala também seu dano periódico.
- Cinco stacks de Pestilência formam um Hospedeiro. Os números de stack devem permanecer legíveis, coloridos e acima da geometria.
- Encanto dura 5 segundos base e usa o multiplicador externo de efeitos temporários. `CharmDamageLink` desenha uma linha rosa entre os vinculados.
- Fascínio substitui Encanto em alvos imunes e adiciona 25% de dano recebido conforme a bênção.
- Barreira absorve antes da vida, reinicia uma espera de 8 segundos ao ser obtida ou parcialmente consumida, depois decai 0,25 ponto por segundo, zera ao trocar de sala e aparece em azul junto da barra de vida. `OverhealToBarrier` pode sobrescrever esses valores com `decayDelay` e `decayPerSecond`.
- Ferida Recuperável espera 2 segundos sem novo dano e cura durante 5 segundos. Com base 20% e a escala global atual, chega a 20%/40%/70%/110%/140%; revisar o teto antes de rebalancear.

## Armadilhas atuais

- As bênçãos são deliberadamente únicas: o loader fixa `MaxStacks = 1` e a oferta exclui definições já escolhidas. Raridade e nível são eixos separados: a raridade sorteada define a base e o Altar do Sacrifício aprimora apenas o nível, de 1 a 10.
- `BloodBlessingRecord.PowerMultiplier` representa somente a raridade. Consumidores de gameplay devem usar `EffectivePowerMultiplier`, que inclui o nível.
- Bênçãos com `exclusiveGroup` substituem a escolha anterior do mesmo grupo. A HUD precisa continuar avisando essa troca antes da confirmação.
- `rarity` na entrada não limita a raridade sorteada; todas usam a tabela global.
- Bênçãos diretamente vinculadas ao ataque básico, rápido, especial ou Fúria Bestial são mutuamente exclusivas dentro do próprio tipo. Usar, respectivamente, `basic_attack_status`, `quick_attack_status`, `special_attack_status` e `bestial_fury_mode`; adquirir uma nova remove a anterior antes de recalcular os efeitos.
- Um efeito pode ser parseado e exibido sem possuir consumidor de gameplay.
- Booleanos ficam em `BooleanValue` quando são o campo `value`; outros booleanos adicionais são armazenados como texto.
- Um efeito parseado e exibido não está implementado até possuir consumidor de gameplay.
- Ao iniciar outra run, todos os efeitos e o desbloqueio da Fúria devem ser removidos.

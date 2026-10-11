# Sistema de habilidades do jogador

## Arquivos centrais

- `scripts/systems/skills/SkillDefinition.cs`: árvores, metadados e efeitos da definição.
- `data/skills/skills.json`: fonte de verdade das definições das habilidades.
- `scripts/systems/skills/SkillCatalog.cs`: carregamento, validação e constantes dos IDs usados pelo runtime.
- `scripts/systems/CharacterSheet.cs`: compra, pré-requisitos e ranks possuídos.
- `scripts/player/PlayerSkillController.cs`: passivas automáticas conectadas ao jogador.
- `scripts/ui/HUD.cs`: árvore, compra, descrições e compêndio.
- `scripts/systems/SaveGameManager.cs`: persistência da ficha.
- `scripts/systems/RunState.cs`: consumo de efeitos limitados a uma run ou mapa.

## Forma da definição

O catálogo contém somente habilidades passivas e permanentes. Habilidades básicas chegam ao rank 5, medianas ao rank 3 e fortes ou capazes de alterar uma run possuem rank único. Preferir bônus simples, como vida máxima flat, dano percentual, mitigação, cura ao concluir sala, revive e reroll.

Bestialis deve formar uma cruz invertida e Humanitas uma cruz em pé. As posições vêm de `progression.position` e devem ser conferidas visualmente na Fonte de Sangue.

Cada entrada separa:

- `presentation`: referência musical (`metalReference`) e tags. Nome, descrição, flavor e monograma **não** ficam no JSON: vão em `data/localization/<idioma>/content.json` (`pt-BR`, `en`, `es`, sempre os três) nas chaves `skill.<id>.name`, `skill.<id>.desc`, `skill.<id>.flavor` e `skill.<id>.icon` (2 letras do nó na Fonte de Sangue). `SkillDefinition.DisplayName`/`Description`/`FlavorText`/`IconText` resolvem no idioma atual. Não há validador de skills: conferir as 4 chaves nos 3 idiomas com `py tools/validate_localization.py` (paridade) e por busca manual da chave;
- `progression`: tier, custo, ranks, posição, pré-requisitos e grupo exclusivo;
- `runtime`: estado de implementação e efeitos parametrizados.

O `SkillCatalog` converte essas entradas em instâncias de `SkillDefinition`, valida IDs, posições, ranks, pré-requisitos e ciclos. Habilidades `Planned` não podem ser compradas até seus efeitos existirem e o estado mudar para `Implemented`.

Usar IDs como `bestialis.nome_da_skill` ou `humanitas.nome_da_skill`. Manter uma constante pública em `SkillCatalog` apenas quando o runtime referencia a habilidade. Não duplicar a definição completa em C#.

`metalReference` guarda a inspiração de banda ou música como easter egg, mas a HUD nunca deve concatená-la à descrição, ao flavor text ou ao compêndio.

## Ranks e custos

- Um único item em `runtime.effects[].values` representa o valor por rank e é multiplicado pelo rank atual.
- Uma lista com um valor por rank seleciona o valor correspondente ao rank atual.
- Efeitos booleanos ou de uso único usam `maxRank: 1`.
- A Fonte de Sangue mostra rank atual/máximo e aumenta o preço em 50% do custo base por rank já comprado.
- Ao carregar um save, limitar o rank persistido ao `maxRank` atual.

## Caminhos de execução

Execução passiva:

1. `CharacterSheet.GetSkillRank(id)` informa o rank adquirido.
2. `PlayerSkillController` ou o sistema dono do evento resolve o efeito e a escala.
3. O efeito altera um cálculo ou reage a um evento concreto.
4. Efeitos limitados à run ou ao mapa registram seu consumo em `RunState`.

## Limites e armadilhas

- A Fúria Bestial é um ataque em `F` e permanece indisponível até uma bênção conceder `UnlockBestialFury`. Uma skill permanente pode melhorá-la, mas nunca desbloqueá-la.
- A Fonte de Sangue bloqueia compra de habilidades com `runtime.status: Planned`.
- `exclusiveGroup` impede adquirir duas escolhas do mesmo grupo; ele não funciona como loadout alternável.
- Cadastro e compra não significam gameplay implementado; todo efeito precisa de consumidor real.
- Usar apenas `MaxRank` 5, 3 ou 1 e verificar compra, custo, limite e persistência.
- A HUD enumera o catálogo automaticamente, mas layouts e conexões de pré-requisito devem ser verificados visualmente.
- Habilidades não possuem slots, inputs ou cooldowns de ativação; não reintroduzir esses conceitos.
- Artefatos também são somente passivos e não ocupam inputs do moveset.

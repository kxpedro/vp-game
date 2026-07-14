# Sistema de habilidades do jogador

## Arquivos centrais

- `scripts/systems/skills/SkillDefinition.cs`: árvores, modos e campos da definição.
- `scripts/systems/skills/SkillCatalog.cs`: IDs e catálogo em C#.
- `scripts/systems/CharacterSheet.cs`: compra, pré-requisitos, skills possuídas e slots ativos.
- `scripts/player/PlayerSkillController.cs`: input, cooldowns, passivas conectadas ao jogador e despacho das ativas.
- `scripts/ui/HUD.cs`: árvore, compra, equipamento, slots, cooldown e compêndio.
- `scripts/systems/SaveGameManager.cs`: persistência da ficha.
- `scripts/systems/CompendiumTuning.cs`: override de cooldown pela chave formada pelo ID da skill e `.cooldown`.
- `project.godot`: ações `skill_slot_1` a `skill_slot_4`.

## Forma da definição

`SkillDefinition` contém ID, nome, árvore, modo, custo, rank máximo, cooldown, descrição, texto curto do ícone e pré-requisitos.

Usar IDs como `bestialis.nome_da_skill` ou `humanitas.nome_da_skill`. Manter uma constante pública em `SkillCatalog` quando o runtime referencia a habilidade.

## Caminhos de execução

Passiva:

1. `CharacterSheet.HasSkill(id)` confirma aquisição.
2. O sistema dono do evento consulta a habilidade.
3. O efeito altera um cálculo ou reage a um sinal/evento concreto.

Ativa:

1. `CharacterSheet` equipa o ID em um dos quatro slots.
2. `PlayerSkillController` lê `skill_slot_N` e valida definição/cooldown.
3. `ApplyActiveSkill` executa o comportamento e sinaliza sucesso.
4. A HUD recebe sinais de uso/falha e exibe cooldown.

## Armadilhas atuais

- Apenas `BestialTransformationId` possui despacho ativo explícito em `ApplyActiveSkill`.
- Algumas descrições declaram que o efeito será refinado; cadastro e compra não significam gameplay implementado.
- `MaxRank` existe na definição, mas confirmar o suporte real a múltiplos ranks antes de usar valor maior que 1.
- A HUD enumera o catálogo automaticamente, mas layouts e conexões de pré-requisito devem ser verificados visualmente.
- Cooldown pode ser alterado pelo compêndio, então usar a definição como base e preservar a chave de tuning.


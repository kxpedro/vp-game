---
name: vp-create-blood-blessing
description: Criar, implementar, revisar ou balancear Bênçãos de Sangue das famílias Dracul, Bathory, Karnstein e Orlok no Vampire's Prey. Usar ao editar data/blessings/blood_blessings.json, integrar bênçãos ao ataque básico, ataque rápido, especial ou Fúria Bestial, criar efeitos no BloodBlessingRuntime ou corrigir ofertas que não alteram o gameplay.
---

# Criar Bênção de Sangue

Criar uma bênção temporária de run, coerente com a família e integrada diretamente ao moveset do jogador.

## Fluxo

1. Ler `references/blood-blessing-system.md` antes de editar.
2. Inspecionar a família escolhida, seus temas e efeitos existentes. Evitar uma bênção mecanicamente duplicada.
3. Escolher qual parte do moveset a bênção modifica: ataque básico, ataque rápido, ataque especial ou Fúria Bestial.
4. Definir ID, família, peso, tags e efeitos Common base no JSON. Nome, descrição (modelo com `{n}` = valor do efeito `n` e marcação `[kw=<id>]termo[/kw]`) e flavor vão em `data/localization/{pt-BR,en,es}/content.json` como `blessing.<id>.name/.desc/.flavor`, nos três idiomas. Bênçãos são únicas durante a run.
5. Calcular os resultados nas cinco raridades usando `rarityScaling` e nos níveis 1–10 usando `BloodBlessingProgression`. Não escalar booleanos, thresholds, durações ou outros parâmetros estruturais.
6. Preferir tipos existentes. Para efeito novo, implementar o consumo no sistema correto e expor parâmetros via `BloodBlessingRuntime` ou `RunState`.
7. Usar `UnlockBestialFury` quando a bênção habilitar o ataque em `F`. Sem esse efeito, a Fúria não pode ser usada.
8. Para efeitos espaciais derivados de um golpe, reutilizar a área real do ataque. Uma poça do especial deve ter o mesmo tamanho do hitbox radial do especial.
9. Adicionar a entrada preservando JSONC válido, UTF-8, ID único em `snake_case` e família exatamente compatível. Quando o pedido for retirar sem apagar, comentar o objeto inteiro.
10. Confirmar que a primeira oferta da run sempre contém ao menos uma bênção com a tag `basic_attack`.
11. Verificar oferta, exclusão de bênçãos já escolhidas, aplicação, feedback e reset ao iniciar outra run.
12. Garantir exclusividade por tipo de ataque: bênçãos de ataque básico, rápido, especial e Fúria Bestial devem usar o `exclusiveGroup` reservado ao seu tipo e substituir a equipada anteriormente.
13. Executar `py .codex/skills/vp-create-blood-blessing/scripts/validate_blessings.py` (confere também as chaves de texto nos três idiomas, os `{n}` e a marcação `[kw]`), `py tools/validate_localization.py` e `dotnet build VP0.1.csproj`.
14. Informar valores Common/escalados, ataque afetado, integração e testes.

## Regras de conclusão

- Não escrever texto exibido no catálogo nem no C#: só chaves em `content.json`. Tipo de efeito novo exige `artifact.effect.<Tipo>` nos três idiomas e entrada em `KnownTypes` do `ArtifactEffectRegistry`.

- Não considerar uma entrada no JSON como efeito implementado.
- Toda bênção deve interagir com ao menos um ataque do moveset ou uma consequência direta dele.
- Manter cobertura para ataque básico, rápido, especial e Fúria Bestial.
- Nunca permitir duas bênçãos equipadas para o mesmo tipo direto de ataque. Usar `basic_attack_status`, `quick_attack_status`, `special_attack_status` e `bestial_fury_mode`; a nova escolha substitui a anterior.
- A primeira oferta deve continuar encontrando uma opção `basic_attack`.
- Somente uma bênção pode desbloquear a Fúria por meio de `UnlockBestialFury`; nenhuma skill permanente substitui essa condição.
- Não alterar `rarityScaling` global ou outras famílias sem pedido explícito.
- Não confundir stack, raridade e nível: bênçãos são únicas, a raridade é sorteada uma vez e o nível 1–10 é aprimorado separadamente.
- Ao alterar Barreira, Encanto, Sangramento, Pestilência, Hospedeiro ou Ferida Recuperável, verificar feedback na HUD, duração/reset e escala por raridade descritos na referência.
- Tratar warnings de efeito sem referência C# como investigação obrigatória, salvo consumo genérico comprovado.
- Manter o tema: Dracul enfatiza domínio/sangue; Bathory, cura/sangue derramado; Karnstein, sedução/agilidade; Orlok, doença/controle/risco.

## Exemplos de acionamento

- "Crie uma bênção que fortaleça o ataque rápido depois da esquiva."
- "Faça o especial da espada longa gerar uma poça com o mesmo hitbox."
- "Crie uma bênção que desbloqueie e modifique a Fúria Bestial em F."

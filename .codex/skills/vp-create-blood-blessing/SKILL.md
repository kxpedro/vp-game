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
4. Definir ID, nome, família, peso, descrição, flavor text, tags e efeitos Common base. Bênçãos são únicas durante a run.
5. Calcular os resultados nas cinco raridades usando `rarityScaling`. Não escalar booleanos, thresholds, durações ou outros parâmetros estruturais.
6. Preferir tipos existentes. Para efeito novo, implementar o consumo no sistema correto e expor parâmetros via `BloodBlessingRuntime` ou `RunState`.
7. Usar `UnlockBestialFury` quando a bênção habilitar o ataque em `F`. Sem esse efeito, a Fúria não pode ser usada.
8. Para efeitos espaciais derivados de um golpe, reutilizar a área real do ataque. Uma poça do especial deve ter o mesmo tamanho do hitbox radial do especial.
9. Adicionar a entrada preservando JSON válido, UTF-8, ID único em `snake_case` e família exatamente compatível.
10. Confirmar que a primeira oferta da run sempre contém ao menos uma bênção com a tag `basic_attack`.
11. Verificar oferta, exclusão de bênçãos já escolhidas, aplicação, feedback e reset ao iniciar outra run.
12. Executar `py .codex/skills/vp-create-blood-blessing/scripts/validate_blessings.py` e `dotnet build VP0.1.csproj`.
13. Informar valores Common/escalados, ataque afetado, integração e testes.

## Regras de conclusão

- Não considerar uma entrada no JSON como efeito implementado.
- Toda bênção deve interagir com ao menos um ataque do moveset ou uma consequência direta dele.
- Manter cobertura para ataque básico, rápido, especial e Fúria Bestial.
- A primeira oferta deve continuar encontrando uma opção `basic_attack`.
- Somente uma bênção pode desbloquear a Fúria por meio de `UnlockBestialFury`; nenhuma skill permanente substitui essa condição.
- Não alterar `rarityScaling` global ou outras famílias sem pedido explícito.
- Não prometer stacks múltiplos sem verificar a limitação atual do loader.
- Tratar warnings de efeito sem referência C# como investigação obrigatória, salvo consumo genérico comprovado.
- Manter o tema: Dracul enfatiza domínio/sangue; Bathory, cura/sangue derramado; Karnstein, sedução/agilidade; Orlok, doença/controle/risco.

## Exemplos de acionamento

- "Crie uma bênção que fortaleça o ataque rápido depois da esquiva."
- "Faça o especial da espada longa gerar uma poça com o mesmo hitbox."
- "Crie uma bênção que desbloqueie e modifique a Fúria Bestial em F."

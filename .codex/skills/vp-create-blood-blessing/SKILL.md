---
name: vp-create-blood-blessing
description: Criar, implementar, revisar ou balancear Bênçãos de Sangue das famílias Dracul, Bathory, Karnstein e Orlok no Vampire's Prey. Usar ao adicionar entradas em data/blessings/blood_blessings.json, criar efeitos ou parâmetros de bênção, integrar o comportamento em BloodBlessingRuntime e sistemas consumidores, ajustar pesos/raridade/stacking/textos, ou diagnosticar bênçãos oferecidas na HUD que não alteram o gameplay.
---

# Criar Bênção de Sangue

Criar uma bênção coerente com a família, funcional em todas as raridades e integrada ao runtime.

## Fluxo

1. Ler `references/blood-blessing-system.md` antes de editar.
2. Inspecionar a família escolhida, seus temas e efeitos existentes. Evitar uma bênção mecanicamente duplicada.
3. Definir ID, nome, família, peso, limite pretendido de stacks, descrição, flavor text, tags e efeitos Common base.
4. Calcular os resultados nas cinco raridades usando `rarityScaling`. Não escalar booleanos, thresholds, durações ou outros parâmetros estruturais, conforme a regra do catálogo.
5. Preferir tipos existentes. Para efeito novo, implementar o consumo no sistema correto e expor parâmetros via `BloodBlessingRuntime` ou `RunState` conforme o padrão mais próximo.
6. Adicionar a entrada preservando JSON válido, UTF-8, ID único em `snake_case` e nome de família exatamente compatível.
7. Registrar um nome amigável quando necessário para a HUD/compêndio.
8. Verificar oferta, exclusão de bênçãos já escolhidas, aplicação na run e reset ao iniciar outra run.
9. Executar `py .codex/skills/vp-create-blood-blessing/scripts/validate_blessings.py` e `dotnet build VP0.1.csproj`.
10. Informar os valores Common e escalados, a integração do efeito e os testes executados.

## Regras de conclusão

- Não considerar uma entrada no JSON como efeito implementado.
- Não alterar `rarityScaling` global ou outras famílias sem pedido explícito.
- Não prometer stacks múltiplos sem verificar a limitação atual do loader.
- Tratar warnings de efeito sem referência C# como investigação obrigatória, salvo consumo genérico comprovado.
- Manter o tema: Dracul enfatiza domínio/sangue; Bathory, cura/sangue derramado; Karnstein, sedução/agilidade; Orlok, doença/controle/risco.

## Exemplos de acionamento

- "Crie uma bênção Orlok que espalha doença ao executar um inimigo."
- "Adicione uma bênção Bathory baseada em poças de sangue."
- "Implemente a bênção que aparece na oferta mas não causa seu efeito."

---
name: vp-create-player-skill
description: Criar, implementar, revisar ou ampliar habilidades passivas e ativas das árvores Bestialis e Humanitas do Vampire's Prey. Usar ao modificar SkillCatalog ou SkillDefinition, adicionar pré-requisitos/custos/ranks/cooldowns, implementar efeitos em PlayerSkillController ou outros sistemas, integrar slots ativos e HUD, ou corrigir habilidades desbloqueáveis que ainda não afetam o gameplay.
---

# Criar habilidade do jogador

Entregar uma habilidade desbloqueável, equipável quando ativa, executável e visível nos fluxos existentes do jogo.

## Fluxo

1. Ler `references/player-skill-system.md` antes de editar.
2. Inspecionar as duas árvores e procurar habilidades ou efeitos semelhantes.
3. Definir árvore, modo de ativação, custo, rank máximo, cooldown, descrição, texto curto do ícone e pré-requisitos.
4. Adicionar uma constante de ID e uma `SkillDefinition` em `SkillCatalog`. Usar o prefixo `bestialis.` ou `humanitas.` e `snake_case` depois do ponto.
5. Confirmar que todos os pré-requisitos existem, pertencem à progressão pretendida e não formam ciclos.
6. Implementar o efeito. Para passivas, conectar ao evento ou cálculo de gameplay correto. Para ativas, adicionar despacho explícito em `PlayerSkillController.ApplyActiveSkill` e retornar sucesso somente quando a ativação ocorrer.
7. Preservar o modelo de compra, persistência, equipamento e cooldown existente. Alterar HUD ou cenas apenas se a definição não for automaticamente descoberta ou se o novo comportamento exigir feedback próprio.
8. Adicionar feedback visual/sonoro e mensagens de falha proporcionais à habilidade.
9. Procurar o ID e o nome da habilidade com `rg` para verificar todas as integrações.
10. Executar `dotnet build VP0.1.csproj`. Quando possível, executar o jogo e testar compra, pré-requisito, equipamento, ativação, cooldown, morte e recarregamento do save.
11. Informar cadastro, implementação, integrações e testes realizados.

## Regras de conclusão

- Não considerar a descrição ou a entrada no catálogo como implementação do efeito.
- Não criar uma ativa que possa ser equipada mas sempre retorne `false` em `ApplyActiveSkill`.
- Não introduzir pré-requisito inexistente, ciclo ou ID sem o prefixo da árvore.
- Não quebrar saves existentes; manter IDs estáveis depois de publicados.
- Preferir comportamento no sistema proprietário do evento, evitando concentrar toda lógica no controlador de skills.

## Exemplos de acionamento

- "Adicione uma passiva Bestialis que cura ao executar inimigos."
- "Crie uma ativa Humanitas que desacelera inimigos próximos."
- "Faça Relógio de Bolso realmente reduzir os cooldowns."

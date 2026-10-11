---
name: vp-create-player-skill
description: Criar, implementar, revisar ou ampliar habilidades passivas e permanentes das árvores Bestialis e Humanitas do Vampire's Prey. Usar ao modificar data/skills/skills.json, SkillCatalog, ranks 5/3/1, pré-requisitos, custos, PlayerSkillController, persistência, layout da Fonte de Sangue ou habilidades compráveis que ainda não afetam o gameplay.
---

# Criar habilidade do jogador

Entregar uma habilidade passiva, permanente, comprável na Fonte de Sangue e funcional de ponta a ponta.

## Fluxo

1. Ler `references/player-skill-system.md` antes de editar.
2. Inspecionar as árvores, os efeitos existentes e a progressão salva. Preservar IDs publicados.
3. Classificar a habilidade como básica, mediana ou forte e usar respectivamente `maxRank` 5, 3 ou 1.
4. Preferir efeitos fundamentais e legíveis, como vida máxima flat, dano percentual, redução de dano, cura entre salas ou um revive por run.
5. Definir árvore, custo base, apresentação, posição, tier, pré-requisitos e efeitos parametrizados. Bestialis deve formar uma cruz invertida; Humanitas, uma cruz em pé.
6. Adicionar a definição em `data/skills/skills.json` (sem textos: `presentation` guarda só `metalReference` e `tags`) e os textos em `data/localization/{pt-BR,en,es}/content.json`: `skill.<id>.name`, `.desc`, `.flavor` e `.icon` (monograma de 2 letras), nos três idiomas. Manter uma constante de ID em `SkillCatalog` somente quando o runtime referenciar a habilidade. Usar o prefixo `bestialis.` ou `humanitas.` e `snake_case` depois do ponto.
7. Confirmar que todos os pré-requisitos existem, pertencem à progressão pretendida e não formam ciclos.
8. Implementar cada efeito declarado em `runtime.effects` e usar `runtime.status: Implemented` somente quando o comportamento existir.
9. Para múltiplos ranks, confirmar escala, compra repetida, custo crescente, limite, persistência e aplicação após carregar o save.
10. Manter referências musicais somente em metadados de easter egg. Nunca exibir a música ou a banda na descrição.
11. Procurar o ID e os tipos de efeito com `rg` para verificar todas as integrações.
12. Executar `py tools/validate_localization.py` e `dotnet build VP0.1.csproj`. Quando possível, validar também em modo headless e testar compra, upgrade, pré-requisito, efeito, morte e recarregamento.
13. Informar cadastro, ranks, integração, persistência e testes realizados.

## Regras de conclusão

- Não considerar a descrição ou a entrada no catálogo como implementação do efeito.
- Não reintroduzir habilidades ativáveis, slots de skills ou cooldowns de ativação.
- Não usar uma habilidade permanente para desbloquear a Fúria Bestial; o ataque em `F` depende de uma Bênção de Sangue da run.
- Usar somente `maxRank` 5, 3 ou 1 conforme a força da habilidade.
- Não introduzir pré-requisito inexistente, ciclo ou ID sem o prefixo da árvore.
- Não quebrar saves existentes; manter IDs estáveis e limitar ranks carregados ao máximo atual.
- Não exibir referências de bandas ou músicas na descrição. Elas são easter eggs de autoria.
- Preferir comportamento no sistema proprietário do evento, evitando concentrar toda lógica no controlador de skills.

## Exemplos de acionamento

- "Adicione uma Bestialis básica que concede 10% de dano por rank até o nível 5."
- "Crie uma Humanitas mediana que reduz o dano recebido em três níveis."
- "Implemente uma habilidade forte que concede um revive por run."

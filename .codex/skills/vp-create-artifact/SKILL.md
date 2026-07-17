---
name: vp-create-artifact
description: Criar, implementar, revisar ou ampliar artefatos exclusivamente passivos do Vampire's Prey. Usar ao editar data/artifacts/artifacts.json, criar efeitos passivos, conectar consumidores ao runtime, ajustar raridade, peso, textos, ícones, baú/coleção ou corrigir um artefato que aparece no jogo mas não produz efeito.
---

# Criar artefato de Vampire's Prey

Entregar um artefato passivo funcional de ponta a ponta. Artefatos não possuem ativação, slots de input ou botões dedicados.

## Fluxo

1. Ler `references/artifact-system.md` antes de editar.
2. Inspecionar `data/artifacts/artifacts.json` e procurar artefatos ou efeitos semelhantes com `rg`.
3. Definir identidade, raridade, peso, tags, descrição, flavor text e efeitos com `slot: Passive`.
4. Se a ideia exigir ativação manual, redesenhá-la como gatilho passivo, modificador contínuo ou reação automática a um evento.
5. Preferir tipos de efeito já implementados. Para um tipo novo, implementar comportamento, feedback e persistência no sistema proprietário.
6. Adicionar ou editar em UTF-8, com IDs únicos em `snake_case` e caminhos Godot `res://`.
7. Confirmar aplicação em `RunState` e consumo por pelo menos um sistema real de gameplay.
8. Registrar um nome legível em `ArtifactEffectRegistry` quando o efeito aparecer na HUD ou no compêndio.
9. Confirmar que o Baú de Artefatos lista itens encontrados na run e descobertos permanentemente no save.
10. Usar ícone existente somente quando placeholder for permitido. Para ícone novo, usar a skill de geração de imagens disponível.
11. Executar `py .codex/skills/vp-create-artifact/scripts/validate_artifacts.py` e `dotnet build VP0.1.csproj`.
12. Informar cadastro, gatilho passivo, consumidor de runtime e testes.

## Regras de conclusão

- Não descrever como funcional um efeito meramente presente no JSON ou no registro de nomes.
- Aceitar somente `slot: Passive`; o catálogo e a validação devem rejeitar ou ignorar conteúdo ativo legado.
- Não criar `PlayerArtifactController`, slots ativos, cooldowns ou inputs de artefato.
- Não ocupar `Q` ou `F`: essas teclas pertencem ao ataque especial e à Fúria Bestial.
- Não alterar multiplicadores globais, distribuição de raridades ou outros artefatos sem necessidade explícita.
- Não sobrescrever arte ou alterações não relacionadas já existentes no worktree.
- Tratar warnings de efeito sem referência no runtime como itens a investigar; justificar apenas quando o consumo for comprovadamente genérico.
- Manter valores ajustáveis pelo compêndio quando o sistema já oferecer uma chave de tuning.

## Exemplos de acionamento

- "Crie um artefato passivo épico que explode automaticamente depois de três críticos."
- "Implemente o efeito do Espelho do Último Convidado."
- "Faça o baú exibir os artefatos encontrados durante a run."

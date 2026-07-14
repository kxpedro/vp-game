---
name: vp-create-artifact
description: Criar, implementar, revisar ou ampliar artefatos passivos e ativos do Vampire's Prey. Usar ao adicionar entradas em data/artifacts/artifacts.json, criar tipos de efeito de artefato, conectar efeitos ao runtime, ajustar raridade/peso/textos/ícones ou diagnosticar um artefato que aparece no jogo mas não produz o comportamento esperado.
---

# Criar artefato de Vampire's Prey

Criar conteúdo funcional de ponta a ponta. Não considerar uma entrada no catálogo concluída até confirmar que todos os efeitos são consumidos pelo runtime.

## Fluxo

1. Ler `references/artifact-system.md` antes de editar.
2. Inspecionar `data/artifacts/artifacts.json` e procurar artefatos ou efeitos semelhantes com `rg`.
3. Definir identidade, slot, raridade, peso, tags, descrição, flavor text e efeitos. Manter o tom dark fantasy do GDD e evitar duplicar mecânicas sem uma diferença clara.
4. Preferir tipos de efeito já implementados. Para um tipo novo, localizar todos os consumidores necessários e implementar o comportamento, feedback e persistência adequados.
5. Adicionar ou editar o artefato preservando JSON válido, UTF-8, IDs únicos em `snake_case` e caminhos Godot iniciados por `res://`.
6. Para artefatos ativos, confirmar ativação em `PlayerArtifactController`, duração, alcance, alvo, cooldown e feedback. Para passivos, confirmar aplicação em `RunState` e consumo pelo sistema de gameplay relevante.
7. Registrar um nome legível em `ArtifactEffectRegistry` quando o efeito aparecer na HUD ou no compêndio.
8. Usar um ícone existente somente quando o pedido permitir placeholder. Se o usuário pedir um ícone novo, criar o bitmap com a skill de geração de imagens disponível e salvar/importar o asset no projeto.
9. Executar `py .codex/skills/vp-create-artifact/scripts/validate_artifacts.py` e `dotnet build VP0.1.csproj`.
10. Informar o que foi cadastrado, o que foi implementado no runtime e quais testes foram executados.

## Regras de conclusão

- Não descrever como funcional um efeito meramente presente no JSON ou no registro de nomes.
- Não alterar multiplicadores globais, distribuição de raridades ou outros artefatos sem necessidade explícita.
- Não sobrescrever arte ou alterações não relacionadas já existentes no worktree.
- Tratar warnings de efeito sem referência no runtime como itens a investigar; justificar apenas quando o consumo for comprovadamente genérico.
- Manter valores ajustáveis pelo compêndio quando o sistema já oferecer uma chave de tuning.

## Exemplos de acionamento

- "Crie um artefato épico que explode depois de três críticos."
- "Implemente o efeito do Espelho do Último Convidado."
- "Adicione um artefato ativo de controle de área com ícone próprio."

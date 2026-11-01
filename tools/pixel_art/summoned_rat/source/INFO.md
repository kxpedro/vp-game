# Pacote de ratos (F:\ASSETS\Rats)

Pacote fornecido pelo Pedro. Autor e licença: **não constam** (a pasta original só tem `Rats.png`, sem README nem licença). Confirmar a origem antes de usar no jogo final e registrar em `CREDITS.md`/`SOURCE.md`.

## Rats.png
- 576 x 384 px, RGBA, só a metade de cima (192 px) tem conteúdo.
- Quadros de **48 x 48 px**, grade de 12 colunas x 4 linhas utilizadas.
- **4 variações de cor**, 3 colunas cada (3 quadros de caminhada/corrida por variação):
  - colunas 0-2: marrom claro; 3-5: marrom escuro avermelhado; 6-8: preto/cinza escuro; 9-11: branco (albino).
- **4 linhas = 4 direções** (rato com cauda rosada):
  - linha 0 (y 0-47): de frente, correndo na direção da câmera (para baixo);
  - linha 1 (y 48-95): de perfil, encarando a **esquerda** (cauda à direita);
  - linha 2 (y 96-143): de perfil, encarando a **direita** (cauda à esquerda);
  - linha 3 (y 144-191): de costas, afastando-se (para cima).
- Animações: só ciclo de movimento de 3 quadros por direção/cor. Não há idle, ataque nem morte (idle pode ser o quadro do meio; ataque, um deslocamento/flash no código).
- Tamanho do rato no quadro: perfil ocupa cerca de 47 x 17 px; frente/costas cerca de 24 x 21 px.

# Roteiro de teste: títulos 3D, painel de dev (F1) e Bigorna

Cobre os 3 planos de 2026-10-12 (`remover-titulos-3d`, `dev-tools`, `bigorna`). Marque `[x]` ao passar e anote o que falhar (cena, passos, o que viu). Use build de debug (`F:\GODOT\App\Godot.exe --path .`); o painel F1 e as ações só existem em debug.

**Tempo estimado:** 40 a 60 min. **Antes:** abra o save de um slot de teste; faça uma cópia de `%APPDATA%\Godot\app_userdata\VP0.1\` se quiser poder voltar atrás (`dev_tuning.json` guarda seus overrides; hoje tem `economy.coins.boss.0 = 4390`).

---

## A. Títulos flutuantes removidos

Esperado: nenhum título decorativo de lugar flutuando. Continuam: prompts "[E] ...", nomes de objetos interativos (Fonte, Bancada, Loja, NPC), rótulos das portas de recompensa e números de dano/avisos dos inimigos.

- [ ] **A1 Hub (Ninho do Corvo):** não aparecem "Sala de Treinamento" nem "Caixão de Virgil". Aparecem os títulos de Fonte, Bancada, portal e NPC; o prompt "[E]" surge ao chegar perto.
- [ ] **A2 Sala inicial de bênção** (depois do portal do hub): sem título de sala; a Fonte mantém título e prompt.
- [ ] **A3 Sala comum da run:** sem "Entrada", "Saída", "Câmara Central" nem "Calabouços de Armand - Sala x/y" flutuando. Os marcadores coloridos no piso continuam. A faixa do topo do HUD ("Calabouços de Armand · ...") continua.
- [ ] **A4 Sala do boss:** sem "Portão Final", "Arena do Chefe" nem "Calabouços de Armand - Chefe: ...". O marcador de piso continua.
- [ ] **A5 Combate:** números de dano, aviso de ataque, stacks de status e marcador "!" dos inimigos continuam aparecendo.
- [ ] **A6 Troca de idioma** (Opções): nada quebra nas salas (não há mais rótulos para atualizar).

## B. Painel de dev (F1)

### B1 Abrir e fechar
- [ ] F1 abre o painel no hub, numa sala e no menu de pausa; F1 e Esc fecham. Também abre pelo botão "Painel de dev (F1)" na seção DEV da pausa.
- [ ] Com o painel aberto o jogo não recebe input (personagem parado) e fecha sem travar o input depois.
- [ ] Com outro overlay aberto (ficha, Compêndio), F1 não abre por cima.

### B2 Parâmetros
- [ ] Lista de grupos à esquerda (Economia, Recompensas / Portas, Combate, Inimigos, Armas, Skills, Artefatos, Bênçãos, Jogador, Esquiva, Câmera, Bosses, Salas e waves, Campanha, Bigorna, Ajustes rápidos…, Ações). A lista rola.
- [ ] Busca "moedas" acha os parâmetros de economia; busca sem acento ("esquiva", "alcance") funciona.
- [ ] Editar `economy.coins.enemy.base_min`/`base_max` (ex.: 50 e 50): a próxima morte de inimigo dá 50 Moedas (conta com Fúria/mapa: o fator `map_factor` do mapa 1 é 1,0).
- [ ] A linha editada fica dourada com "●" e mostra "padrão: X"; "↺" volta ao padrão; "Só alterados" filtra.
- [ ] "Resetar grupo" e "Resetar tudo" (com confirmação) restauram os padrões.
- [ ] **Persistência:** altere um valor, feche o jogo, abra de novo: o valor continua (e `%APPDATA%\Godot\app_userdata\VP0.1\dev_tuning.json` mostra a chave).
- [ ] Aviso em parâmetros que não valem na hora: "vale só para objetos novos / próxima sala / após reaplicar".

### B3 Efeito real (um de cada domínio, volte ao padrão depois)
- [ ] **Jogador:** `player.walk_speed` 3,6 → 8: corre bem mais rápido. `player.max_health` 100 → 300: vida máxima muda.
- [ ] **Esquiva:** `player.dodge.cooldown` 0,18 → 1,5: esquiva demora a voltar.
- [ ] **Combate:** `weapon.longsword.basic_1.damage` 32 → 300: primeiro golpe mata de um hit. `combat.crit.base_multiplier`.
- [ ] **Câmera:** `camera.zoom_distance` 15 → 25: a câmera afasta (movimento continua alinhado; `camera.yaw` desalinha o movimento, é esperado).
- [ ] **Inimigos** (vale em inimigos novos, ou próxima sala): `enemy.basic.health` 110 → 1000: inimigo aguenta muito mais. `enemy.tank.speed_multiplier` 0,62 → 1,24: tank anda ~2x mais rápido **uma vez só** (não 4x).
- [ ] **Salas/waves:** `room.waves.size` 5 → 1 e `room.waves.count` 4 → 1: próxima sala tem só 1 inimigo.
- [ ] **Boss** (próxima sala de boss): `boss.speed_mult.default` ou um `boss.<golpe>.*` mudam o boss visivelmente.
- [ ] **Economia:** `economy.coins.room_clear_bonus.0`; `economy.shop.price.common` muda o preço na Loja de Artefatos (Câmara entre mapas).
- [ ] **Ajustes rápidos (GameTuning):** mexer num deles (ex.: `game.walk`) reflete na hora e mantém ao reabrir o jogo.
- [ ] `campaign.map.0.rooms` tem mínimo 1 (e a nota "vale melhor entre runs").

### B4 Aba "Ações"
- [ ] **Moedas de Sangue:** "Dar" (+popup), "Definir para", "Zerar"; o contador do HUD atualiza.
- [ ] **Sangue Amaldiçoado:** dar/definir; aparece na ficha (C) e persiste no save.
- [ ] **Vida:** "Curar tudo"; "Definir vida atual"; **Modo deus** (levar dano não tira vida; desligue depois); "Stamina infinita" (sem efeito visível, esperado); "Fúria cheia" (só com a skill da Fúria; senão avisa "Fúria bloqueada").
- [ ] **Sala:** "Matar todos os inimigos"; "Completar sala" (itens no chão e portas aparecem; se a wave estiver surgindo, pede para tentar de novo); "Pular para mapa/sala"; "Ir para o boss" (a cena troca, a run segue).
- [ ] **Recompensas:** dar uma Bênção (várias raridades) e um Artefato por id: aparecem na barra/ficha e funcionam; "Dar Fruta do Diabo" (+15 de vida máxima); "Abrir oferta de Bênção".
- [ ] **Tempo:** slider 0,1x a 4x muda a velocidade; aviso "TEMPO x,x×" aparece; "Normal (1×)" volta; ao trocar de sala ou ir ao menu principal a escala volta a 1.
- [ ] "Aplicar tuning agora", "Abrir pasta de dados" (abre `user://`), "Exportar overrides" (mostra o caminho de `dev_tuning.json`).

### B5 Compêndio (compatibilidade)
- [ ] Abrir o Compêndio (debug): os editores por entrada ainda funcionam e a mudança aparece também no painel F1.
- [ ] "Restaurar valores originais" no Compêndio avisa que também apaga os valores do F1 (e apaga).

## C. Bigorna

### C0 Preparação para forçar a Bigorna (via F1)
1. Comece uma run (portal do hub) e entre na primeira sala da arena. A arma equipada precisa ser a Espada Grande (única implementada). Iniciar uma run zera o override abaixo, então ligue-o **depois** de entrar na run.
2. F1 > aba **Ações** > **Portas de recompensa**: clique **Bênção + Bigorna** (liga "Forçar portas" com duas portas, esquerda Bênção e direita Bigorna). Vale em toda saída ainda não sorteada; para a sala atual use **Rerrolar portas desta sala** (recria as portas se a sala já estiver limpa). A Sala Inicial e a Câmara já montam suas portas ao carregar: o override vale a partir da sala seguinte. O override ignora o teto de Bigornas e não o conta; sem precisar de porta, **Bigorna agora** abre a oferta direto.
   - Alternativa (sem o botão): grupo **Recompensas / Portas**, `reward.doors.two_chance.base.0`, `.1`, `.2` = 1 e `reward.doors.two_chance.cap` = 1; `reward.door_weight.coins.0`/`fruit.0` (e dos mapas seguintes) = 0 e `reward.door_weight.anvil.0` = 100. Vale só para salas **não sorteadas**: comece uma run nova depois de ajustar.
3. Comece uma run (portal do hub). A arma equipada precisa ser a Espada Grande (única implementada).

### C1 Porta e item
- [ ] Ao limpar a sala, aparecem **duas portas**: Bênção e Bigorna. A porta da Bigorna tem medalhão de bigorna (diferente da esfera/discos/losango), rótulo "Bigorna" e dica; legível também em escala de cinza.
- [ ] Nunca aparece Bigorna como porta única, nem duas portas do mesmo tipo (com "Forçar portas" desligado; o override livre permite combinações fora do normal e o painel avisa).
- [ ] Atravesse a porta da Bigorna; na **sala seguinte**, ao limpá-la, um item da Bigorna cai no chão (junto do bônus de Moedas) com prompt "[E]". As portas ficam **travadas** enquanto houver item.
- [ ] Pegar o item abre a **oferta**; Moedas no chão também precisam ser pegas para destravar.

### C2 Tela de oferta
- [ ] Mostra até 3 cartas (nome, descrição com o número, flavor, nome da arma). Sem botão de pular; Esc avisa "escolha uma" e não fecha.
- [ ] Escolha por mouse, teclado (setas + Enter, botão "Forjar") e controle, se tiver.
- [ ] Ao escolher: toast/log de confirmação, a oferta fecha e as portas **destravam** (uma vez só).
- [ ] Troque o idioma antes/depois: textos em pt-BR, en e es corretos (Bigorna / Anvil / Yunque); nada cortado.
- [ ] Ficha (C) → aba Bênçãos: seção "Modificações da arma" lista a modificação escolhida com o efeito.

### C3 Efeitos no combate (use "Dar bênção"/"Modo deus" e o boneco/inimigos; compare antes e depois)
Cada modificação só pode ser pega 1 vez por run. Para ver as 5, repita a Bigorna ou ajuste o teto `economy.anvil.run_cap` (e `weapon.mod.offer_choices`).
- [ ] **Lâmina Alongada** (+15% alcance): o arco acerta inimigos mais longe (compare a distância; no painel `weapon.longsword.basic_1.range` mostra a base).
- [ ] **Fio Afiado** (+15% dano básico/rápido): inimigo morre em menos hits; some com o golpe especial (não é afetado).
- [ ] **Cabo Equilibrado** (−12% preparo e recuperação): combo visivelmente mais rápido; recarga do especial não muda por ele.
- [ ] **Contrapeso de Aço** (+30% empurrão, +40% atordoamento): inimigos voam mais e ficam atordoados mais tempo.
- [ ] **Impacto Ampliado** (+20% raio, −20% recarga do especial): especial maior e disponível mais cedo.
- [ ] Sem nenhuma modificação, o combate é idêntico ao de antes (referência: dano 32/38/52, quick 68, especial 56).

### C4 Estado da run
- [ ] As modificações **persistem entre salas e entre mapas**, mas **zeram** ao morrer/desistir e ao começar outra run. Nada aparece depois de sair para o hub.
- [ ] Salvar e sair no meio da run: as modificações não vão ao save (run não é salva).
- [ ] Teto: depois de 3 Bigornas escolhidas na run, a porta de Bigorna para de aparecer (ajuste `run_cap` para 1 para testar mais rápido; com "Forçar portas" ligado o teto é ignorado, então desligue-o para este teste).
- [ ] **Pool vazio:** pegue as 5 modificações (ajuste `run_cap`); depois disso a Bigorna **para de ser sorteada**. Se forçar uma pendente com o pool vazio, o item paga **75 Moedas de Sangue** (mensagem "O ferro virou 75 Moedas de Sangue") e destrava as portas sem abrir tela.
- [ ] Bênção continua garantida em toda saída; Fruta e Moedas ainda aparecem quando você devolver os pesos ao padrão (Moedas 22/24/26, Fruta 6/8/10).
- [ ] Dica: teste a frequência com pesos padrão (Bigorna 8/10/12): ~1 em 4 segundas portas.

## D. Regressões rápidas (nada disso deve ter mudado com o painel em padrão)
- [ ] Combate de uma run normal sem tocar em nada: dano, esquiva, câmera, velocidade como antes.
- [ ] Bênção/Fruta/Moedas nas portas, bônus de sala limpa, boss (4 mapas se possível), Loja da Câmara, Fonte de Sangue (custo e rank) e altar.
- [ ] Opções, Compêndio, ficha, pausa, diálogo do hub (NPC), troca de idioma.
- [ ] Reiniciar o jogo: sem erros no log do Godot (painel "Saída"/"Depurador"): procure `ERROR`/`SCRIPT ERROR`.
- [ ] Ao final, **resetar tudo** no painel F1 (Resetar tudo) para voltar aos padrões.

## E. Fora do escopo / limites conhecidos
- Navegação por controle no painel F1 não foi feita (use mouse e teclado).
- O medalhão da Bigorna e o item no chão são geometria provisória.
- `PlayerCombat.GetTunedAttack` descarta o perfil de controle dos golpes (buffer e mira assistida do `WeaponCatalog` não têm efeito): não é novo.
- Armas Revólveres Duplos e Chicote são placeholders: a Bigorna não aparece com elas.
- `camera.yaw` gira a câmera mas o movimento continua em 45°.
- Mudar `campaign.map.{m}.rooms` no meio da run pode levar direto ao boss; use entre runs.

## Registro de falhas

| # | Item | O que aconteceu | Cena/passos |
|---|---|---|---|
| | | | |

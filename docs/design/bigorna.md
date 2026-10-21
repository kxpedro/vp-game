# Bigorna: modificações de arma na run

Documento de design da tarefa T1 do plano `20261012-0131-bigorna`. Fonte única de regras, números e textos para T2 (dados, catálogo, `RunState`, `PlayerCombat`), T3 (portas e item no chão) e T4 (tela de oferta, ficha, localização). Escrito em 2026-10-14.

**Status dos números: proposta a validar jogando.** As contas de oferta/escolha são analíticas (não houve simulação nem playtest) e estão marcadas com **[VALIDAR]**.

Referência de design: o Martelo de Dédalo do Hades (power-up raro, escolha de 1 entre 3 modificações da arma equipada, valem até o fim da run). Aqui a Bigorna é uma porta de recompensa opcional, como a Fruta do Diabo, e o efeito é sempre determinístico (sem raridade).

Arquivos lidos para os números: `RoomRewardCatalog.cs`, `WeaponCatalog.cs`, `WeaponDefinition.cs`, `PlayerCombat.cs` (`GetTunedAttack` L1219, `BeginAttack` L217, `BeginQuickAttack` L321, `DealDamage`/`DealCircularDamage`, `GetCurrentBasicAttackDamage`), `docs/design/portas-recompensas.md`, `blood_blessings.json`, `DevParameters.*.cs`.

---

## 0. Resumo das decisões

| Tema | Decisão |
|---|---|
| O que é | Recompensa de porta "Bigorna": ao pegar o item no chão, o jogador escolhe 1 de até 3 modificações da arma equipada. Vale até o fim da run. |
| Onde aparece | Só como segunda porta (a Bênção continua garantida em toda saída), junto de Fruta e Moedas. Duas portas nunca do mesmo tipo. |
| Peso por mapa | `anvil` 8 / 10 / 12 (mapas 1 a 3). Moedas **permanece 22/24/26** (decisão do orquestrador: não reduzir a economia de Moedas de porta); a Bigorna toma fatia das três outras segundas portas, e a Fruta cai de ~21% para ~17% no mapa 1 (ver nota em 4.4). |
| Teto | 3 Bigornas escolhidas por run (`economy.anvil.run_cap`). |
| Pool | 5 modificações por arma; cada uma é pega no máximo 1 vez por run. Pool vazio (ou arma sem modificações) = Bigorna sai do sorteio. |
| Composição | Cada modificação é um fator multiplicativo próprio, aplicado depois dos multiplicadores aditivos das Bênçãos/artefatos. Não entra em `GetEffectTotal`. |
| Persistência | Só na run (`RunState`). Zera em `StartRun`, `FailRun` e fim da campanha. Nada vai ao save. |
| Espada Grande | 5 modificações (seção 3). Dois valores mudam em relação ao plano, e o nome da quinta também (seção 3.3). |

---

## 1. Regras

### 1.1 Fantasia e objetivo

O sangue de Virgil não basta para vencer o castelo: a lâmina também precisa ser refeita. A Bigorna é a oferta de "especializar o moveset" que as Bênçãos não dão (elas modificam ataques por efeito; a Bigorna mexe na geometria e no ritmo da arma). Objetivo de design: uma decisão de build adicional, rara (cerca de 1 a 2 por run) e legível.

### 1.2 Quando a porta aparece

1. Entra no sorteio de `RoomRewardCatalog.Roll` como terceiro tipo da segunda porta (ao lado de Fruta e Moedas). `RoomRewardType.Anvil`.
2. A primeira porta continua sendo a Bênção (pity 0, opção B atual). A Bigorna **nunca** é a única porta: se `types` não contém Bênção e o conjunto teria uma só porta, a Bigorna não pode ser sorteada. Na prática: peso da Bigorna = 0 enquanto `chosen` estiver vazio.
3. Peso da Bigorna = 0 (fora do sorteio) quando qualquer uma for verdadeira:
   - a arma equipada não tem modificações disponíveis (`IsImplemented == false`, ou todas as modificações dela já foram pegas, descontando uma se já houver Bigorna pendente de pagamento ou escolhida e não paga);
   - `AnvilsChosen >= economy.anvil.run_cap` (contagem de portas de Bigorna **escolhidas**, como a Fruta);
   - o tipo já está em `chosen` (duas portas nunca do mesmo tipo).
4. Mapa 4 e salas de boss: sem conjunto de portas, portanto sem Bigorna.
5. A elegibilidade é avaliada no momento do sorteio do conjunto (guardado em `RunState`). Recarregar cena não re-sorteia.

### 1.3 Recompensa: item no chão e porta

- Igual às demais (docs de portas, memória do projeto): a porta mostra o tipo (ícone + `reward.door.anvil.short/.hint`), entrar nela grava `PendingRoomReward = Anvil`, e a recompensa é **paga ao limpar a sala seguinte**, caindo como item no chão (`RewardPickup`, medalhão provisório de geometria, como Bênção/Moedas).
- Coletar o item abre a oferta (`GetTree().CallGroup("hud", "OpenAnvilOffer")`, nome fixado no plano). A oferta é obrigatória (não há cancelar), como a de Bênção; as portas continuam travadas até a escolha (`UnlockExitAfterReward`).
- Cor/forma sugerida do ícone (T3/designer-ux-arte decide o final, mas precisa ser distinta de losango dourado, esfera vermelha e pilha de discos): trapézio escuro de aço (`#6F7C8C`, luminância média-baixa) com um topo claro, silhueta de bigorna (base larga, topo plano com chifre).

### 1.4 Oferta de modificações

- Pool = modificações da arma equipada (`WeaponLoadout.EquippedWeaponId`) ainda não pegas nesta run.
- Sorteio uniforme sem repetição de até 3 (`min(3, pool)`). Se restarem 2 ou 1, oferece essas (a tela deve aceitar 1 a 3 cartas).
- Chave de tuning `weapon.mod.offer_choices` (padrão 3, 1 a 5).
- Cada modificação é pega **no máximo uma vez** por run (sem níveis, sem acúmulo). Isso mantém o pool de 5 finito e o teto de 3 Bigornas coerente: com 5 modificações e teto 3, a terceira oferta sempre tem pelo menos 2 cartas.
- O sorteio da oferta é feito ao abrir (não ao sortear a porta), com o RNG da run. Reabrir (recarga de cena com oferta pendente) mantém as mesmas cartas: `RunState` guarda a lista oferecida até a escolha.
- Sem reroll (o Espelho do Destino não vale para a Bigorna).

### 1.5 Duração, reset e troca de arma

- Estado: `RunState` guarda o conjunto de ids pegos (lista ordenada) e o contador `AnvilsChosen`. Zera junto de `_devilsFruitMaxHealth`: `StartRun`, `FailRun` e fim da campanha. **Não** zera entre mapas, nem ao trocar de sala, nem em recarga de cena. Nada em `GameSaveData`.
- Cada modificação tem `weaponId`. `RunState.GetWeaponModTotal(effect)` soma/multiplica só as modificações cujo `weaponId` é o da arma equipada **naquele instante**. Se o jogador trocar de arma com modificações ativas (só possível no Banco de Armas do hub, ou seja, praticamente fora de run), as modificações da outra arma ficam guardadas e inativas; voltando à arma original, voltam a valer. Elas continuam contando como "já pegas" para a exclusão do pool daquela arma.
- Recompensa paga com pool vazio no momento do pagamento (caso raro: troca de arma entre o sorteio e a coleta): paga `economy.coins.blessing_fallback` (75) com a mensagem `anvil.offer.none`, igual à Bênção sem oferta.

### 1.6 Interação com Fúria, Bênçãos e artefatos

Decisão: **multiplicativa, em fator próprio, aplicada depois do conjunto aditivo.**

- Bênçãos e artefatos somam dentro de `(1 + currencyDamage + roomDamage + ContextDamageBonus)` e dentro de `AttackSpeed.Multiplier`. A modificação **não** entra nessas somas.
- Dano (Fio Afiado): `attackDamage = attack.Damage * damageMultiplier * edgeFactor * (Fúria ? furyDamageMultiplier : 1)`, com `edgeFactor = 1 + valor` só para ataques Basic e Quick. Fúria e Bênçãos escalam por cima (multiplicativo com a Fúria, que já é multiplicativa em `damageMultiplier`).
- Por que multiplicativo: uma modificação fixa, garantida e visível não pode ser diluída pelo pool aditivo das Bênçãos (que cresce até ×7 por raridade). Aditiva faria o Fio Afiado valer menos quanto mais o jogador evolui, e o jogador não perceberia o ganho. Multiplicativa torna o valor lido na descrição o valor real contra o dano atual.
- Risco de degenerescência controlado: o maior fator possível é pequeno (+15%), o teto é 3 modificações por run e só 1 (Fio Afiado) mexe em dano. O produto máximo de dano é ×1,15.
- A Fúria multiplica o dano e as Moedas, não o Sangue Amaldiçoado (inalterado). A explosão final da Fúria (15% do dano básico) usa `GetCurrentBasicAttackDamage`, que deve incluir o Fio Afiado.
- Ordem de aplicação para valores de tuning (vale para alcance, empurrão, atordoamento, tempo e recarga): valor do catálogo → chave de dev `weapon.longsword.<golpe>.<campo>` (e `Tuned*` de `WeaponDefinition`) → `GameTuning` → **modificação** (último multiplicador). Assim o painel F1 continua editando a base e a modificação sempre escala sobre o valor tunado vigente.

### 1.7 Onde cada efeito é consumido (resumo para T2)

Todos os efeitos de golpe passam por `PlayerCombat.GetTunedAttack`, que hoje recebe `(attack, keyPrefix)`. T2 deve:

1. Mudar a assinatura para `GetTunedAttack(attack, keyPrefix, PlayerAttackType type)` e atualizar as 5 chamadas (`BeginBasicAttack` L145, `BeginSpecialAttack` L173, `BeginQuickAttack` L335, `GetCurrentBasicAttackDamage` L595, `GetCurrentBasicAttackInterval` L614).
2. **Atenção ao bug existente**: `GetTunedAttack` constrói um `WeaponAttackDefinition` novo sem o parâmetro `Control`, que vira `null`. `BeginAttack` e `BeginQuickAttack` fazem `attack.Control ?? AttackControlProfile.Default`, então o perfil de controle por golpe (buffer, cancelamentos, aim assist) do `WeaponCatalog` **nunca é usado** em jogo. Isso é anterior à Bigorna. Não consertar dentro desta tarefa sem decisão (consertar muda a sensação do combate); apenas passar `attack.Control` adiante se o dono do combate decidir. Registrado como divergência (seção 9).
3. Os valores vêm de `WeaponModCatalog`/`RunState.GetWeaponModTotal`, que lê `CompendiumTuning.GetFloat("weapon.mod.<id>.value", padrão)`.

---

## 2. Referência de balanceamento

Ordem de grandeza das Bênçãos (`blood_blessings.json`, `rarityScaling`):

| Bênção (Common, ×1) | Valor | Rare ×3,5 | Legendary ×7 |
|---|---|---|---|
| `predatory_thirst` (dano, todos os ataques) | +12% | +42% | +84% |
| `quickened_pulse` (velocidade de ataque) | +10% | +35% | +70% |
| `crimson_stride` (velocidade de movimento) | +10% | +35% | +70% |
| `hunters_eye` (chance de crítico) | +10 pontos | +35 pontos | +70 pontos |

Média ponderada de uma oferta: ×2,62 (pesos 35/28/20/12/5). Uma Bênção sorteada vale em média cerca de +31% de dano ou +26% de velocidade.

Régua para a Bigorna: uma modificação é **determinística** (sem sorte de raridade) e **rara** (cerca de 1,5 por run), então deve valer algo entre um Common e um Uncommon da mesma categoria (×1 a ×2 da Bênção base), nunca perto da média de uma Bênção. Efeitos que atuam em só um subconjunto de golpes valem mais por ponto que um efeito global.

---

## 3. As 5 modificações da Espada Grande (`longsword`)

### 3.1 Tabela final

Valores padrão: `value` (e `value2` quando houver) lidos como `weapon.mod.<id>.value`/`.value2`. Percentuais gravados como fração (0,15 = 15%).

| # | `id` | Nome pt-BR | Efeito | Chave de efeito (JSON) | Padrão |
|---|---|---|---|---|---|
| 1 | `longsword_reach` | Lâmina Alongada | +15% de alcance de todos os ataques | `AttackRange.Multiplier` | `value` 0,15 |
| 2 | `longsword_edge` | Fio Afiado | +15% de dano do combo básico e do ataque rápido | `BasicQuickDamage.Multiplier` | `value` 0,15 |
| 3 | `longsword_tempo` | Cabo Equilibrado | −12% do tempo de preparo e de recuperação de todos os ataques | `AttackTiming.Reduction` | `value` 0,12 |
| 4 | `longsword_weight` | Contrapeso de Aço | +30% de empurrão e +40% de duração do atordoamento | `Knockback.Multiplier` e `Stagger.Multiplier` | `value` 0,30 e `value2` 0,40 |
| 5 | `longsword_whirl` | Impacto Ampliado | Ataque especial: +20% de raio e −20% de recarga | `SpecialRadius.Multiplier` e `SpecialCooldown.Reduction` | `value` 0,20 e `value2` 0,20 |

(O `id` `longsword_whirl` é mantido por compatibilidade com o plano e é estável; só o nome exibido muda, seção 3.3.)

### 3.2 Como cada uma se aplica no código

Nomes de campos de `WeaponAttackDefinition`: `Damage`, `HitDelay`, `Recovery`, `Knockback`, `Range`, `Width`, `StaggerDuration`. Definição de `mod(e)` = soma do efeito `e` entre as modificações ativas da arma equipada (cada modificação é única, então a soma é o próprio valor).

| # | Onde | Fórmula exata | Ordem e observações |
|---|---|---|---|
| 1 Alcance | `GetTunedAttack`, na linha do `Range` | `Range = tunado(.range) * EquippedWeapon.TunedRangeMultiplier * (1 + mod(AttackRange))` | Vale para Basic, Quick e Special (no Special é o raio do círculo, `DealCircularDamage` usa `attack.Range`; no Quick o `TunedQuickAttackHitboxScale` multiplica depois, então o ganho é proporcional). `Width` não muda. O teste de acerto soma `edgeForgiveness` (+1,15 fixo) ao alcance, então o alcance **efetivo** sobe menos que 15% (cerca de +9% no `basic_1`: 1,55+1,15=2,70 vira 1,78+1,15=2,93; cerca de +13% no `special`). O indicador visual (`visualRange`) usa `attack.Range` e acompanha. O valor mostrado ao jogador é o do campo `Range` (15%), não o efetivo. |
| 2 Dano | `BeginAttack` (`attackDamage`) e `BeginQuickAttack` (`quickDamage`); também `GetCurrentBasicAttackDamage` | `attackDamage = attack.Damage * damageMultiplier * (1 + mod(BasicQuickDamage)) * fúria`, só se `attackType` for Basic ou Quick; Special não recebe | Fator multiplicativo próprio (seção 1.6). Em `BeginQuickAttack` entra junto de `quickDamageMultiplier`. `GetCurrentBasicAttackDamage` (usado pela HUD e pela explosão da Fúria) deve multiplicar também. |
| 3 Tempo | `GetTunedAttack`, nas linhas de `HitDelay` e `Recovery` | `HitDelay = tunado(.hit_delay) * (1 - mod(AttackTiming))` e `Recovery = tunado(.recovery) * (1 - mod(AttackTiming))` | Vale para Basic, Quick e Special. Acontece **antes** de `GetAttackDelay` (que ainda divide pela velocidade de ataque das Bênçãos e multiplica por `EquippedWeapon.TunedAttackDelayMultiplier`), logo é multiplicativo com `quickened_pulse`. Intervalo do `basic_1`: 0,24 s → 0,211 s, equivalente a +13,6% de velocidade de ataque. Piso: `Mathf.Max(..., 0.01f)` já existe em `GetAttackDelay`, e `BeginQuickAttack` já protege com `Max(hitDelay, 0.04f)`. Limite de segurança: `value` no painel de dev vai até 0,5. |
| 4 Peso | `GetTunedAttack`, nas linhas de `Knockback` e `StaggerDuration` | `Knockback = tunado(.knockback) * (1 + mod(Knockback))`; `StaggerDuration = tunado(.stagger) * (1 + mod(Stagger))` | `DealDamage` usa `attack.Knockback`/`attack.StaggerDuration`; `DealCircularDamage` idem, então vale para todos os golpes. Os multiplicadores de `WeaponDefinition` (`TunedBasicAttackKnockbackMultiplier` etc.) e o corte de chefe (`GetAttackKnockback` devolve `knockback` puro para chefes) são aplicados depois e não mudam. O dano de impacto na parede (`TunedWallImpactDamageMultiplier * finalDamage`) não depende do empurrão, só a chance de o inimigo chegar à parede aumenta. Atordoamento: `basic_1` 0,20 s → 0,28 s. |
| 5 Especial | `GetTunedAttack` (raio, só `type == Special`) e `BeginSpecialAttack` (recarga) | `Range = ... * (1 + mod(SpecialRadius))` (somado ao fator da modificação 1 por multiplicação); `_specialAttackCooldownTimer = Max(EquippedWeapon.TunedSpecialAttackCooldown * (1 - mod(SpecialCooldown)), 0.05f)` | Raio `special`: 3,15 → 3,78 (4,35 com a Lâmina Alongada). Recarga: 1,0 s → 0,8 s. `OnSpecialAttack(.., attack.Range, ..)` já recebe o raio final, então as Bênçãos que usam o raio do especial acompanham. A recarga reduz também o tempo entre repetições da animação, nada além disso. |

### 3.3 Mudanças em relação ao plano (e por quê)

| Modificação | Plano | Final | Justificativa (1 linha) |
|---|---|---|---|
| `longsword_edge` | +12% | **+15%** | O alvo é só Basic e Quick (sem o Special) e é fixo, então precisa de um pouco mais que o Common global (+12%) para valer uma porta. |
| `longsword_tempo` | −12% só da recuperação | **−12% de preparo e recuperação** | A recuperação dos golpes é 0,11 a 0,16 s: −12% disso é menos de 20 ms, imperceptível; incluir o `HitDelay` entrega ≈ +14% de velocidade, na faixa do `quickened_pulse` Common–Uncommon. |
| `longsword_weight` | +25% empurrão, +20% atordoamento | **+30% e +40%** | O atordoamento base é 0,20 a 0,34 s: +20% são 40 a 70 ms, abaixo da percepção; é um efeito de utilidade, não de dano. |
| `longsword_whirl` | nome "Giro Ampliado" | **"Impacto Ampliado"** (`id` igual) | O especial da Espada Grande é a "Queda Carmesim", uma batida no chão, não um giro (o giro é o Corte em Rodopio, o ataque rápido). |
| `longsword_reach` | +15% | +15% | Sem mudança: o alcance efetivo sobe só 9% a 13% (seção 3.2), então não precisa subir o nominal. |

Comparação com a régua (seção 2): Edge +15% em 2 de 3 tipos de golpe ≈ Common–Uncommon de dano; Tempo ≈ Common–Uncommon de velocidade; Alcance e Peso são de controle de espaço, sem equivalente direto nas Bênçãos; Impacto Ampliado (+20% raio e −20% recarga) é forte no especial, mas o especial é 1 em 3 golpes e tem recarga de 1 s; aceitável como a modificação mais "temática" de build de especial.

### 3.4 Textos das modificações (nome, descrição, flavor)

`content.json`, chaves `weaponmod.<id>.name/.desc/.flavor`. Nas descrições, `{0}` e `{1}` são o percentual inteiro **com o sinal %** já incluído (formatado a partir do valor tunado, ex.: `15%`), para o painel de dev refletir na tela.

| `id` | pt-BR | en | es |
|---|---|---|---|
| `longsword_reach` | **Lâmina Alongada**<br>"Aumenta o alcance de todos os ataques em {0}."<br>_"Mais aço entre você e o perigo."_ | **Elongated Blade**<br>"Increases the range of all attacks by {0}."<br>_"More steel between you and the danger."_ | **Hoja Alargada**<br>"Aumenta el alcance de todos los ataques en {0}."<br>_"Más acero entre tú y el peligro."_ |
| `longsword_edge` | **Fio Afiado**<br>"Aumenta o dano do combo básico e do ataque rápido em {0}."<br>_"A pedra de amolar nunca dorme."_ | **Honed Edge**<br>"Increases the damage of the basic combo and the quick attack by {0}."<br>_"The whetstone never sleeps."_ | **Filo Afilado**<br>"Aumenta el daño del combo básico y del ataque rápido en {0}."<br>_"La piedra de afilar nunca duerme."_ |
| `longsword_tempo` | **Cabo Equilibrado**<br>"Reduz em {0} o preparo e a recuperação dos ataques."<br>_"O peso certo na mão certa."_ | **Balanced Hilt**<br>"Reduces the windup and recovery of attacks by {0}."<br>_"The right weight in the right hand."_ | **Empuñadura Equilibrada**<br>"Reduce en {0} la preparación y la recuperación de los ataques."<br>_"El peso justo en la mano justa."_ |
| `longsword_weight` | **Contrapeso de Aço**<br>"Aumenta o empurrão em {0} e a duração do atordoamento em {1}."<br>_"O que a lâmina não corta, o peso derruba."_ | **Steel Counterweight**<br>"Increases knockback by {0} and stagger duration by {1}."<br>_"What the blade can't cut, the weight topples."_ | **Contrapeso de Acero**<br>"Aumenta el empuje en {0} y la duración del aturdimiento en {1}."<br>_"Lo que la hoja no corta, el peso lo derriba."_ |
| `longsword_whirl` | **Impacto Ampliado**<br>"O ataque especial ganha {0} de raio e recarrega {1} mais rápido."<br>_"O chão carmesim se alarga a cada golpe."_ | **Wider Slam**<br>"The special attack gains {0} radius and recharges {1} faster."<br>_"The crimson ground widens with every blow."_ | **Impacto Ampliado**<br>"El ataque especial gana {0} de radio y se recarga {1} más rápido."<br>_"El suelo carmesí se ensancha con cada golpe."_ |

Para o ícone/letras de carta da oferta, T4 usa o nome. Referências musicais: nenhuma.

---

## 4. Pesos de porta e teto

### 4.1 Valores padrão

| Chave | Mapa 1 | Mapa 2 | Mapa 3 | Observação |
|---|---|---|---|---|
| `reward.door_weight.anvil.{m}` | 8 | 10 | 12 | Cresce com o mapa como a Fruta (build mais formado, mais valor em especializar). |
| `reward.door_weight.coins.{m}` (alterado) | 18 | 18 | 18 | Era 22 / 24 / 26. Ver 4.2. |
| `reward.door_weight.fruit.{m}` | 6 | 8 | 10 | Sem mudança. |
| `reward.door_weight.blessing.{m}` | 72 | 68 | 64 | Sem mudança. |
| `economy.anvil.run_cap` | 3 | | | Teto de Bigornas **escolhidas** por run. |
| `reward.door_weight.ramp_shift` | 2 | | | Sem mudança: sai de Moedas e vai para Fruta. A Bigorna não participa da rampa. |

### 4.2 Efeito sobre a soma dos pesos e o sorteio

- Como a Bênção ocupa a primeira porta (pity 0, `DefaultMaxSetsWithoutBlessing = {0,0,0}`), o peso `blessing` não participa do sorteio da segunda porta. A segunda porta sorteia entre `fruit`, `coins` e `anvil`.
- Hoje (sem Bigorna), a soma da segunda porta é 28 / 32 / 36 (6+22, 8+24, 10+26). Para manter a mesma soma e a Fruta na mesma fatia, a Bigorna toma peso de Moedas:

| Mapa | Fruta (antes → depois) | Moedas (antes → depois) | Bigorna | Soma |
|---|---|---|---|---|
| 1 | 21% → 19% | 79% → 56% | 25% | 32 (era 28) |
| 2 | 25% → 22% | 75% → 50% | 28% | 36 (era 32) |
| 3 | 28% → 25% | 72% → 45% | 30% | 40 (era 36) |

(Percentuais fora da rampa. Na rampa, Fruta +2 e Moedas −2: mapa 1 fica 8/16/8.)

- **Consequências, com os dois ajustes assumidos:**
  - Fruta: cai cerca de 10% em frequência de oferta (2,1 → cerca de 1,9 por run neutra, **[VALIDAR]**). Para recuperar, subir `reward.door_weight.fruit.{m}` em +1 por mapa; não aplicado aqui para não mexer na economia de vida.
  - Moedas: a oferta de porta cai cerca de 30% a 40%. O doc de portas (seção 11.3) avisa que as Moedas de porta davam cerca de +20% de renda acima do que a Loja previa; a queda traz a renda neutra de porta de cerca de 734 para cerca de 490 por run, ainda acima do alvo de 600 da economia-v2 se o jogador pegar a Moeda quando aparece. **[VALIDAR]**.
  - Bigorna: ≈ 28% das segundas portas. Com cerca de 10,4 segundas portas por run, são cerca de 2,9 ofertas por run; o jogador neutro (50% de escolha) pega cerca de 1,5 e o que evita Bênção chega ao teto 3. **[VALIDAR]**.

### 4.3 Efeito sobre o pity da Bênção

Nenhum. A Bênção continua sendo garantida em toda saída (`types[0] = Blessing` quando `setsWithoutBlessing >= MaxSetsWithoutBlessing`). A Bigorna só pode ser a segunda porta (regra 1.2.2), e `RoomRewardSet.HasBlessing` continua sendo o que alimenta o contador do pity. Se a opção A for adotada no futuro (pity 0, 1, 1), a regra "Bigorna nunca é porta única" já cobre o caso.

### 4.4 Alavancas

| Se acontecer | Mexa em |
|---|---|
| Bigorna rara demais (< 1 por run) | `reward.door_weight.anvil.{m}` +2 |
| Bigorna sempre no teto | `economy.anvil.run_cap` 3 → 2, ou peso −2 |
| Fruta caiu demais | `reward.door_weight.fruit.{m}` +1 |
| Moedas escassas para a Loja | `reward.door_weight.coins.{m}` +2 (e Bigorna −2) |

---

## 5. Textos de interface prontos

Padrão conferido em `data/localization/pt-BR/`: os tipos de recompensa ficam em `content.json` (`reward.type.<tipo>.name/.desc`); os rótulos e dicas de porta em `world.json` (`reward.door.<tipo>.short/.hint`); a tela de oferta em `ui.json`. Glossário: "Bigorna/Anvil/Yunque" é termo novo (adicionar a `docs/design/glossario-localizacao.json` com contexto "Recompensa de porta que modifica a arma equipada"); "Arma/Weapon/Arma", "Ataque especial/Special Attack/Ataque especial", "Moedas de Sangue/Blood Coins/Monedas de Sangre", "Sala/Room/Sala" mantidos.

### 5.1 Recompensa e porta (`content.json` e `world.json`)

| Chave | Arquivo | pt-BR | en | es |
|---|---|---|---|---|
| `reward.type.anvil.name` | content | Bigorna | Anvil | Yunque |
| `reward.type.anvil.desc` | content | Forja uma modificação para a arma equipada: escolha uma de três. Vale até o fim da run. | Forges a modification for your equipped weapon: choose one of three. Lasts until the end of the run. | Forja una modificación para el arma equipada: elige una de tres. Dura hasta el final de la run. |
| `reward.door.anvil.short` | world | Bigorna | Anvil | Yunque |
| `reward.door.anvil.hint` | world | Bigorna: escolha uma de três modificações para a arma. | Anvil: choose one of three modifications for your weapon. | Yunque: elige una de tres modificaciones para el arma. |

(`reward.door.fruit.locked` já existe e serve para travar as portas até a Bigorna ser resolvida: "Pegue a recompensa no chão para destravar as portas".)

### 5.2 Tela de oferta (`ui.json`)

| Chave | pt-BR | en | es |
|---|---|---|---|
| `anvil.offer.title` | Ferrum Sanguinis | Ferrum Sanguinis | Ferrum Sanguinis |
| `anvil.offer.status` | A bigorna aguarda. Escolha uma modificação para {0}. | The anvil awaits. Choose a modification for {0}. | El yunque espera. Elige una modificación para {0}. |
| `anvil.offer.choose` | Forjar | Forge | Forjar |
| `anvil.offer.must_choose` | Escolha uma modificação para liberar a saída. | Choose a modification to unlock the exit. | Elige una modificación para desbloquear la salida. |
| `anvil.offer.none` | Nenhuma modificação disponível para esta arma. O ferro virou {0} Moedas. | No modifications available for this weapon. The iron became {0} Coins. | No hay modificaciones disponibles para esta arma. El hierro se convirtió en {0} Monedas. |
| `anvil.offer.applied` | Modificação forjada: {0} | Modification forged: {0} | Modificación forjada: {0} |
| `anvil.sheet.title` | Modificações da arma | Weapon modifications | Modificaciones del arma |
| `anvil.sheet.none` | Nenhuma modificação forjada. | No modifications forged. | Ninguna modificación forjada. |

`{0}` em `anvil.offer.status` é `WeaponDefinition.DisplayName`; em `anvil.offer.none` é o número de Moedas; em `anvil.offer.applied` é o nome da modificação. O título em latim segue o padrão de `offer.blessing.title` ("Hereditas Sanguinis"); confirmar se o en/es dele também é igual nos três idiomas antes de fechar.

---

## 6. Listas para implementação

### 6.1 Chaves de localização a criar

Mesmos nomes nos três idiomas (`py tools/validate_localization.py` cobra paridade e placeholders).

| Arquivo | Chaves |
|---|---|
| `content.json` | `reward.type.anvil.name`, `reward.type.anvil.desc` |
| `content.json` | `weaponmod.longsword_reach.name/.desc/.flavor`, `weaponmod.longsword_edge.name/.desc/.flavor`, `weaponmod.longsword_tempo.name/.desc/.flavor`, `weaponmod.longsword_weight.name/.desc/.flavor`, `weaponmod.longsword_whirl.name/.desc/.flavor` (15 chaves) |
| `world.json` | `reward.door.anvil.short`, `reward.door.anvil.hint` |
| `ui.json` | `anvil.offer.title`, `anvil.offer.status`, `anvil.offer.choose`, `anvil.offer.must_choose`, `anvil.offer.none`, `anvil.offer.applied`, `anvil.sheet.title`, `anvil.sheet.none` |

Total: 27 chaves por idioma. Placeholders: `{0}` em `anvil.offer.status/none/applied`, `weaponmod.*.desc` (`{0}`, e `{1}` em `weight` e `whirl`).

### 6.2 Chaves de tuning a registrar (`DevParameters.*.cs`)

Grupo sugerido: `GRewards` ("Recompensas / Portas") para as de porta e `GWeapons` (novo grupo "Bigorna") para as de modificação. Todas lidas por `CompendiumTuning.GetFloat/GetInt(chave, padrão)`.

| Chave | Descrição | Padrão | Min | Max | Passo | Inteiro | Aplicação |
|---|---|---|---|---|---|---|---|
| `reward.door_weight.anvil.0` | Peso da Bigorna (mapa 1) | 8 | 0 | 100 | 1 | sim | NextRoom |
| `reward.door_weight.anvil.1` | Peso da Bigorna (mapa 2) | 10 | 0 | 100 | 1 | sim | NextRoom |
| `reward.door_weight.anvil.2` | Peso da Bigorna (mapa 3) | 12 | 0 | 100 | 1 | sim | NextRoom |
| `economy.anvil.run_cap` | Teto de Bigornas escolhidas por run | 3 | 0 | 10 | 1 | sim | NextRoom |
| `weapon.mod.offer_choices` | Cartas por oferta de Bigorna | 3 | 1 | 5 | 1 | sim | Live |
| `weapon.mod.longsword_reach.value` | Lâmina Alongada: alcance | 0,15 | 0 | 1 | 0,01 | não | Live |
| `weapon.mod.longsword_edge.value` | Fio Afiado: dano básico/rápido | 0,15 | 0 | 2 | 0,01 | não | Live |
| `weapon.mod.longsword_tempo.value` | Cabo Equilibrado: redução de tempo | 0,12 | 0 | 0,5 | 0,01 | não | Live |
| `weapon.mod.longsword_weight.value` | Contrapeso de Aço: empurrão | 0,30 | 0 | 3 | 0,05 | não | Live |
| `weapon.mod.longsword_weight.value2` | Contrapeso de Aço: atordoamento | 0,40 | 0 | 3 | 0,05 | não | Live |
| `weapon.mod.longsword_whirl.value` | Impacto Ampliado: raio do especial | 0,20 | 0 | 2 | 0,01 | não | Live |
| `weapon.mod.longsword_whirl.value2` | Impacto Ampliado: redução de recarga | 0,20 | 0 | 0,9 | 0,01 | não | Live |

**Decisão final: NÃO mudar** `reward.door_weight.coins.{m}` (continua 22/24/26). A proposta original de 18/18/18 foi descartada; as contas das seções 4.2 a 4.4 usam o cenário 18/18/18 e valem só como referência da alavanca. Registre `reward.door_weight.anvil.{m}` pelo mesmo helper `RegisterPerMap`. Rode `py tools/validate_dev_tuning.py` (chaves lidas x registradas).

Dados de catálogo (`data/weapons/weapon_mods.json`, só IDs e números): por modificação `id`, `weaponId`, `effects[]` com `type` (coluna "Chave de efeito" da seção 3.1) e `value`/`value2` padrão. Os valores do JSON são os **padrões**; o jogo lê a chave de tuning com o valor do JSON como fallback.

---

## 7. Armas futuras (só ideias, sem implementação)

### Revólveres Duplos (`dual_revolvers`)

| # | Nome | Efeito |
|---|---|---|
| 1 | Tambor Extra | +1 projétil na rajada básica. |
| 2 | Gatilho Leve | +15% de cadência de tiro. |
| 3 | Recarga Veloz | −25% de recarga do ataque especial. |
| 4 | Bala Perfurante | Os projéteis atravessam +1 inimigo. |
| 5 | Queima-roupa | +30% de dano em alvos a menos de 3 m. |

### Chicote (`whip`)

| # | Nome | Efeito |
|---|---|---|
| 1 | Tira Longa | +20% de alcance. |
| 2 | Laço de Espinhos | A ponta enrosca e puxa o primeiro inimigo atingido em 1,5 m. |
| 3 | Estalo | O golpe de ponta causa +35% de dano. |
| 4 | Corrente de Sangue | O golpe atinge +1 inimigo em cadeia. |
| 5 | Farpas Sangrentas | +40% de dano e duração do Sangramento causado pelo chicote. |

---

## 8. Casos de borda e testes de aceitação

### Casos de borda

| Caso | Regra |
|---|---|
| Arma sem moveset (`IsImplemented == false`) equipada | Bigorna fora do sorteio; se a recompensa for paga assim mesmo, fallback de Moedas. |
| Todas as 5 modificações pegas | Bigorna fora do sorteio (impossível com teto 3, mas o guard existe por causa do painel de dev). |
| Restam 1 ou 2 modificações | Oferta com 1 ou 2 cartas. |
| Teto (3) atingido | Bigorna fora do sorteio de `Roll` (como o teto da Fruta). |
| Bigorna escolhida na porta e morte antes de pagar | Perde-se com a run (`FailRun` zera `PendingRoomReward`, as modificações e `AnvilsChosen`). |
| Conjunto sorteado já tinha Bigorna, mas o pool esvaziou antes de entrar na porta | Impossível com 5 modificações e teto 3; se ocorrer pelo painel de dev, a recompensa paga Moedas de fallback. |
| Bigorna pendente no momento de sortear a próxima porta | A elegibilidade desconta a Bigorna pendente (conta como uma modificação já reservada). |
| Troca de arma (Banco do hub) com modificações ativas | Modificações de outra arma ficam inativas e voltam ao reequipar. |
| Recarga de cena com oferta aberta | Mantém as mesmas cartas (lista guardada em `RunState`). |
| Dano contra chefe | O empurrão de chefe continua ignorando multiplicadores de combo (`GetAttackKnockback`); o Contrapeso só aumenta o atordoamento. |
| Idioma | Descrições são formatadas com o valor tunado; trocar de idioma reformata (sinal `LanguageChanged`). |
| Save | `GameSaveData` não muda; nenhuma modificação é persistida. |

### Testes de aceitação

1. Sem modificações o combate é idêntico ao atual (alcance, dano, tempos e empurrão iguais).
2. Com `longsword_reach`, o `Range` do `basic_1` passa de 1,55 para 1,78 em log, e o do `special` de 3,15 para 3,62.
3. Com `longsword_edge`, `GetCurrentBasicAttackDamage` sobe 15% e o golpe especial não muda.
4. Com `longsword_tempo`, o intervalo do `basic_1` passa de 0,24 s para ≈ 0,211 s.
5. Com `longsword_weight`, `Knockback` do `basic_1` passa de 4,2 para 5,46 e `StaggerDuration` de 0,20 para 0,28.
6. Com `longsword_whirl`, o raio do especial passa de 3,15 para 3,78 e a recarga de 1,0 s para 0,8 s.
7. Duas modificações juntas se multiplicam sobre o valor tunado (ex.: Alcance + Impacto: 3,15 × 1,15 × 1,20 = 4,35).
8. Mudar `weapon.longsword.special.range` no painel F1 continua alterando a base; a modificação escala sobre o novo valor.
9. A oferta nunca mostra uma modificação já pega; com 2 pegas mostra 3 e, com 4 pegas (via painel de dev), mostra 1.
10. Pool vazio: a porta de Bigorna não aparece; um pagamento forçado dá 75 Moedas e a mensagem `anvil.offer.none`.
11. Teto: após 3 Bigornas escolhidas na run, nenhuma conjunto novo contém Bigorna.
12. Toda saída de sala comum continua tendo uma Bênção; a Bigorna nunca é a única porta.
13. `FailRun` e `StartRun` zeram as modificações, o contador e a recompensa pendente; sair para o menu e voltar não restaura nada.
14. `py tools/validate_localization.py`, `py tools/validate_dev_tuning.py` e `dotnet build` sem erro; 27 chaves novas por idioma.

---

## 9. Divergências e riscos

### Divergências

| # | Fonte | Observação |
|---|---|---|
| 1 | Plano T1 | Valores de `longsword_edge`, `longsword_tempo` e `longsword_weight` ajustados, e o nome de `longsword_whirl` (seção 3.3). |
| 2 | `PlayerCombat.GetTunedAttack` | Descarta `AttackControlProfile` (`Control`) dos golpes: `attack.Control ?? Default` é sempre o padrão em jogo, então os valores de `Control` do `WeaponCatalog` não têm efeito. Fora do escopo; decidir com o dono do combate. |
| 3 | `docs/design/portas-recompensas.md` | A tabela de pesos passa a ter 4 tipos (Bênção, Fruta, Moedas, Bigorna) e as Moedas permanecem 22/24/26 (a proposta de 18 foi descartada). O doc de portas deve ganhar um adendo quando T3 fechar. |
| 4 | `GDD.txt` | Não prevê modificações de arma; sem conflito. |

### Riscos

1. Cerca de 30% a 40% menos Moedas de porta, mais Bigorna em vez de economia: pode apertar a Loja da Câmara. Alavanca: seção 4.4.
2. Alcance e Impacto Ampliado podem tornar a Espada Grande mais segura contra inimigos de longa distância (menos risco no trade-off de alcance). Observar nos bosses 2 e 3.
3. Tempo e Fio Afiado somados à Fúria e a `quickened_pulse` empilham: o pico é ×1,15 de dano e ≈ +14% de velocidade, mas multiplicativo com um pool de Bênçãos já grande; vigiar o TTK do boss 1 (`target_seconds` do `BossCatalog`).
4. O ícone da Bigorna precisa ser desenhado como forma distinta (daltonismo/escala de cinza) pelo designer-ux-arte.
5. A tela de oferta precisa suportar 1 a 3 cartas e mostrar o valor tunado, não o padrão.
6. Todos os números de frequência desta proposta são analíticos, não simulados.

# Famílias de herança de sangue: decisões oficiais

Status: **decidido em design, ainda não implementado** (decisão do Pedro em 2026-10-30). Este documento oficializa o conjunto de 8 famílias; a implementação fica para depois.

## Decisões

- O jogo terá **8 famílias**: as 4 atuais (Dracul, Báthory, Orlok, Karnstein) e 4 novas (**Strigoi, Vârcolac, Liaozhai, Nachtalb**).
- **Stirps não é família**: é o conceito de herança híbrida de duas famílias.
- **Descartada:** Tepes (o Dracul já é a referência ao Vlad).
- **Liaozhai** substitui a ideia inicial "Jiangshi" (nome menos óbvio: vem da obra *Liaozhai Zhiyi*).
- **Saint-Germain** e **Camazotz** ficam como **NPCs de mitologia do mundo**, não como famílias.
- Reservas não escolhidas (podem virar 9ª/10ª família): Estriga (projéteis/relâmpago), Ruthven (economia/apostas), Varney (maldição/retaliação).
- Cores novas: usar a paleta AAP-64 (hex abaixo são aproximações a conferir).
- Cada família terá personalidade própria no diálogo de herança (`narrativa.md`, seção 9).

| # | Família | Referência | Mecânica | Cor |
|---|---|---|---|---|
| 1 | Dracul | Vlad Drácula | roubo de vida, poder permanente | existente |
| 2 | Báthory | Elizabeth Báthory | sangramento, poças | existente |
| 3 | Orlok | Nosferatu | pestilência, ratos | existente |
| 4 | Karnstein | Carmilla | encanto, aliados | existente |
| 5 | Strigoi | folclore romeno | sombra, esquiva ofensiva | cinza-névoa |
| 6 | Vârcolac | vampiro-lobo dos Bálcãs | Forma de Lobo | marrom-terra |
| 7 | Liaozhai | *Liaozhai Zhiyi* | Selo, paralisia | jade escuro |
| 8 | Nachtalb | folclore alemão | sono e pesadelo | índigo / azul-noite |

## Pendências

- Valores numéricos, textos en/es e implementação (nada disso foi feito).
- Decidir se o Instinto do Vârcolac é separado da Fúria Bestial do Dracul (recomendação: separado, com uma Stirps Dracul + Vârcolac que os una).
- Definir quais pares de Stirps existirão (28 pares possíveis com 8 famílias).

---


## 1. Strigoi: sombra e esquiva

**Referência:** strigoi e moroi do folclore romeno, mortos que saem à noite.
**Voz:** inquieta e sussurrante, paranoica. Fala de portas, de ser seguida, de quem espia no escuro. Trata Virgil como "um dos nossos que ainda não aprendeu a sumir".
**Estado de inimigo:** `shadow_mark` (**Marca da Sombra**). Inimigo marcado revela a posição, toma dano extra e é alvo dos efeitos de sombra.
**Identidade:** dash e esquiva como arma, ataque pelas costas.

| # | Nome | Raridade | Efeito | Flavor |
|---|---|---|---|---|
| 1 | Passo Sem Eco | Common | +velocidade de movimento [reuso `MoveSpeed`] | Nenhum chão guarda a sua pegada. |
| 2 | Sombra Afiada | Common | +dano em ataques pelas costas do inimigo [novo] | A lâmina chega antes do aviso. |
| 3 | Véu da Noite | Common | +chance crítica logo após o dash [reuso `CriticalChanceAfterDash`] | Quem some, volta mais letal. |
| 4 | Mordida das Trevas | Common | ataque rápido tem chance de aplicar Marca da Sombra [novo, padrão `QuickAttack*Chance`] | Um beijo no escuro. |
| 5 | Coração Inquieto | Common | dash dura mais com invulnerabilidade [novo] | Quem não para, não é pego. |
| 6 | Olhos de Coruja | Common | +dano contra inimigos marcados [novo] | A noite mostra o que o dia esconde. |
| 7 | Eco do Dash | Uncommon | o dash deixa um clone de sombra que explode após 1s [novo] | A sombra demora a perceber que ficou para trás. |
| 8 | Esquiva Perfeita | Uncommon | esquivar na janela de um golpe inimigo dá crítico garantido no próximo ataque [novo] | Errar de propósito é uma arte. |
| 9 | Presa Furtiva | Uncommon | ataque especial tem chance de aplicar Marca [novo, padrão `SpecialAttack*Chance`] | O segundo beijo é sempre mais fundo. |
| 10 | Névoa Rasteira | Uncommon | atravessar inimigos no dash causa dano e aplica Marca [novo] | Passar por dentro é a maior ofensa. |
| 11 | Passo Entre Mundos | Rare | o dash teleporta para trás do inimigo marcado mais próximo [novo] | A porta certa fica sempre atrás de você. |
| 12 | Sede de Sombra | Rare | matar um inimigo marcado reduz a recarga do dash e cura um pouco [reuso parcial `HealOn*Kill`] | Cada sumiço alimenta o próximo. |
| 13 | Forma de Névoa | Rare | após uma esquiva perfeita, 2s de intangibilidade total (uma vez por sala) [novo] | Nem o sangue prende o que não tem corpo. |
| 14 | Matilha de Sombras | Epic | 2 clones persistentes imitam seus golpes por 8s ao matar um marcado [novo] | Uma só sombra nunca bastou. |
| 15 | Noite Eterna | Epic | as marcas não expiram na sala atual e marcados tomam dano extra de todas as fontes [novo] | Uma noite que não acaba. |

**Observação de balanceamento:** Strigoi depende de a esquiva ter janela precisa; sem isso o clone/teleporte vira bônus passivo. Precisa de teste de sensação.

---

## 2. Vârcolac: fera e transformação

**Referência:** vârcolac/vrykolakas, o vampiro-lobo dos Bálcãs.
**Voz:** instintiva e rosnante, frases curtas, fala de caça, matilha e lua. Não entende a etiqueta de Dracul e debocha dela.
**Mecânica central:** barra de **Instinto**. Acertar golpes enche; cheia, Virgil entra na **Forma de Lobo** por alguns segundos: mais velocidade e dano, ataque rápido mais veloz, menos defesa. **[novo]**
**Estado de inimigo:** `mauled` (**Dilaceração**). Acumula com golpes rápidos; cada acúmulo aumenta o dano recebido; no máximo, o inimigo fica atordoado por pouco tempo.
**Em aberto:** a Fúria Bestial do Dracul (`UnlockBestialFury`) e o Instinto do Vârcolac devem ser sistemas separados, ou o Instinto alimenta a Fúria? Recomendo separados, com uma Stirps Dracul + Vârcolac que os una.

| # | Nome | Raridade | Efeito | Flavor |
|---|---|---|---|---|
| 1 | Instinto de Matilha | Common | acertos geram mais Instinto [novo] | A matilha lembra quem lidera. |
| 2 | Pelagem Grossa | Common | +vida máxima e redução de dano pequena [reuso `MaxHealth`] | O frio nunca me feriu. |
| 3 | Faro | Common | +velocidade de movimento [reuso `MoveSpeed`] | O medo tem cheiro. |
| 4 | Garra Selvagem | Common | +dano do ataque rápido [reuso `QuickAttackDamage`] | Rasgar é mais honesto que cortar. |
| 5 | Fôlego da Lua | Common | +duração da Forma de Lobo [novo] | Enquanto houver lua, há caça. |
| 6 | Ossos de Lobo | Common | +dano do ataque especial [reuso `SpecialAttackDamage`] | Cada osso conta uma caçada. |
| 7 | Cio da Lua | Uncommon | Forma de Lobo concede mais dano [novo] | A lua cheia exige tributo. |
| 8 | Rasgar | Uncommon | ataque rápido aplica Dilaceração [novo, padrão `QuickAttack*Chance`] | Feridas que ninguém fecha. |
| 9 | Uivo | Uncommon | ao entrar na forma, grito empurra e atordoa inimigos próximos [novo] | A noite inteira ouviu. |
| 10 | Caçada Noturna | Uncommon | +velocidade por inimigo dilacerado em tela [novo, padrão `MoveSpeedPer*`] | Presa ferida corre mal. |
| 11 | Salto da Fera | Rare | dash na forma causa dano em área [novo] | O salto vale pela queda. |
| 12 | Fome do Lobo | Rare | matar na forma cura [reuso `HealOn*Kill`] | Carne quente conserta o resto. |
| 13 | Lua de Sangue | Rare | matar na forma estende sua duração [novo] | A lua só sacia com sangue. |
| 14 | Fera Primal | Epic | ao entrar na forma, 2s de invulnerabilidade e explosão de dano [novo] | Todo lobo foi, um dia, um homem com fome. |
| 15 | Eclipse | Epic | forma ativa em ciclos curtos enquanto estiver acima de certa vida; a lua nunca termina de se pôr [novo] | A lua cobre a própria sombra. |

**Observação:** a forma "menos defesa" precisa de um custo real e legível, com troca visual (sprite de lobo ou brilho) via `BloodlineVisuals`.

---

## 3. Liaozhai: selo e paralisia

**Referência:** *Liaozhai Zhiyi* (Pu Songling, séc. XVII), a coletânea clássica de contos de cadáveres e espíritos, e a figura do sacerdote taoísta que sela os mortos com talismãs.
**Voz:** ritualística e calada, fala em fórmulas, proibições e avisos ("não pise nas linhas"). Trata o sangue como algo que se contém, não que se bebe.
**Estado de inimigo:** `sealed` (**Selo**). Inimigo selado fica rígido (lento e sem esquiva) e toma dano extra. Selos acumulam.
**Mecânica central:** **talismãs** no chão: zonas fixas, com duração, que selam quem entra. **[novo]**

| # | Nome | Raridade | Efeito | Flavor |
|---|---|---|---|---|
| 1 | Papel Amarelo | Common | +duração do Selo [novo] | A tinta seca devagar. |
| 2 | Tinta de Cinábrio | Common | +dano contra selados [novo] | Vermelho que prende. |
| 3 | Jade no Peito | Common | +vida máxima [reuso `MaxHealth`] | A pedra fria guarda o calor. |
| 4 | Rigidez Mortal | Common | ataque rápido tem chance de aplicar Selo [novo, padrão `QuickAttack*Chance`] | Quem para, já morreu. |
| 5 | Passos Rituais | Common | +velocidade de movimento dentro de talismãs [novo] | Só os que sabem as linhas andam livres. |
| 6 | Sopro Contido | Common | +redução de dano recebido por selados atacando você [novo] | Respirar baixo é uma disciplina. |
| 7 | Talismã de Contenção | Uncommon | o ataque especial deixa um talismã por alguns segundos que sela quem entra [novo] | Escrito às pressas, obedecido com fé. |
| 8 | Sino de Bronze | Uncommon | ao acumular 3 Selos, o inimigo fica atordoado [novo] | Um som antigo manda calar. |
| 9 | Arroz Glutinoso | Uncommon | selados não se curam nem regeneram [novo] | O velho remédio contra o que não deveria andar. |
| 10 | Ofício Taoísta | Uncommon | ao matar um selado, o Selo se espalha para os mais próximos [novo, padrão `PestilenceSpreadTargets`] | O ofício passa de mestre a discípulo. |
| 11 | Quadrado Bagua | Rare | 3 talismãs próximos formam um círculo que pulsa dano [novo] | Oito direções, nenhuma saída. |
| 12 | Espelho Sem Reflexo | Rare | selados não atacam à distância e projéteis inimigos se desviam [novo] | O que não tem reflexo não tem alma. |
| 13 | Selo Mestre | Rare | elites e chefes selados duram mais e tomam mais dano [reuso parcial `DamageVsElite`/`DamageVsBoss`] | O traço final é do mestre. |
| 14 | Prisão do Mandarim | Epic | ao completar o 5º Selo, cria uma zona de prisão que impede movimento por 3s [novo] | Os juízes também condenam os mortos. |
| 15 | Decreto Celeste | Epic | ao entrar numa sala, todos os inimigos começam com Selo leve por 2s [novo] | O céu já havia escrito o veredito. |

**Observação:** Selo vs encanto (Karnstein): Selo prende sem efeito colateral; Encanto inverte alianças. Manter distinto na leitura visual (aura jade e glifo no chão).

---

## 4. Nachtalb: sono e pesadelo

**Referência:** Nachtalb (Nacht + Alb), espírito do pesadelo do folclore alemão. Parente do Alp, do Mara e do Nachzehrer.
**Voz:** arrastada e sonolenta, fala como quem sonha e mistura o que Virgil diz com o que não disse. Fala de peso no peito, de noites sem fim, de acordar sem lembrar.
**Estado de inimigo:** `nightmare` (**Pesadelo**). Acertos acumulam **Sonolência**; cheia, o inimigo adormece. Dormente não acorda com dano de Virgil e toma dano extra. Ao acordar, entra em **Pânico**: foge e ataca os próprios aliados. **[novo]**

| # | Nome | Raridade | Efeito | Flavor |
|---|---|---|---|---|
| 1 | Cantiga Sombria | Common | acertos geram mais Sonolência [novo] | A cantiga que ninguém lembra de ter ouvido. |
| 2 | Peso nas Pálpebras | Common | +duração do sono [novo] | Dormir é a parte fácil. |
| 3 | Dormente Vulnerável | Common | +dano contra inimigos dormindo [novo] | Quem dorme não vê o golpe. |
| 4 | Olhos Pesados | Common | ataque rápido tem chance de aplicar Sonolência [novo, padrão `QuickAttack*Chance`] | O cansaço é um golpe lento. |
| 5 | Noite Longa | Common | +vida máxima [reuso `MaxHealth`] | A noite me cobre inteiro. |
| 6 | Travesseiro de Penas | Common | +redução de dano recebido de inimigos sonolentos [novo] | Até o golpe chega amortecido. |
| 7 | Pesadelo Recorrente | Uncommon | ao acordar, o inimigo sofre dano de Pânico [novo] | O mesmo sonho, sempre pior. |
| 8 | Sussurro do Alp | Uncommon | inimigos em Pânico atacam aliados por 4s [novo] | Eles acordam sem saber quem é quem. |
| 9 | Peso no Peito | Uncommon | o dash causa dano em dormentes por onde passa [novo] | O Alp senta onde quer. |
| 10 | Ronco do Alp | Uncommon | aura que gera Sonolência em inimigos próximos [novo] | Um ronco que cresce na sala. |
| 11 | Paralisia do Sono | Rare | dormentes são imunes a empurrão e levam crítico garantido [novo] | Presos entre o sonho e o corpo. |
| 12 | Colheita de Sonhos | Rare | matar dormentes cura e dá Moedas [reuso parcial `HealOn*Kill`] | Os sonhos também têm preço. |
| 13 | Terror Noturno | Rare | o Pânico se espalha em cadeia para inimigos próximos [novo, padrão `PestilenceSpreadTargets`] | O grito acorda todo o quarto. |
| 14 | Hora das Bruxas | Epic | com 3+ inimigos em Pânico, a sala inteira dorme 3s (recarga longa) [novo] | Meia-noite pesa em todos. |
| 15 | Soberano do Pesadelo | Epic | dormentes e em pânico tomam dano extra de todas as fontes; matar um estende o sono dos outros [novo] | O Alp reina onde a vigília falha. |

**Observação:** Nachtalb vs Encanto: ambos "tiram" o inimigo do combate. Diferença: Encanto vira aliado, Pesadelo o neutraliza e depois o devolve em pânico. Manter essa clareza na descrição.

---

## Stirps possíveis (para os pares novos)
Com 8 famílias, são 28 pares. Sugestão: começar com poucos por família nova, escolhendo os que cruzam mecânicas:
- **Strigoi + Karnstein:** sedução na penumbra; marcado fica encantado.
- **Vârcolac + Dracul:** Fúria Bestial + Forma de Lobo.
- **Liaozhai + Orlok:** selado e pestilento: o contágio fica preso.
- **Nachtalb + Báthory:** dormir sangrando; poças com sonolência.

## Pendente
- Valores numéricos e curvas de raridade.
- Conferir os hex na paleta AAP-64.
- Textos em en/es e entradas de `blessing.<id>.name/.desc/.flavor`.
- `blessing.family.<id>.theme` das 4 novas e estados de inimigo em `EnemyStatusVisuals`.
- Voz de cada família no diálogo da herança (narrativa §9) e fala de conclusão.
- Decidir se o Instinto do Vârcolac e a Fúria do Dracul são separados.

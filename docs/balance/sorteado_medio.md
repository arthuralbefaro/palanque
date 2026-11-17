# Sorteado do porte médio: diagnóstico e alavancas

Medido em 2026-11-17, no branch `sorteado-medio` (criado da `main` em `3e68f2d`). Só relatório: nenhum número do jogo mudou. Os testes foram mudanças temporárias numa cópia de `data/`, e a instrumentação do BalanceSim foi revertida depois.

Pedido do autor (D-057): na linha de base de `docs/balance/regua_d026.md`, só o partido sorteado do porte médio fica fora da faixa (39%, IC 30–49; faixa de 5% a 25%). Por que a chapa fraca faz cadeira tanto nesse porte, e qual parâmetro mexe só nisso.

Como foi medido: `tools/Planalto.BalanceSim`, semente 1, 10 cidades por porte, as mesmas da régua. A linha de base foi rodada de novo com 200 campanhas por porte e repete a régua exatamente (73/19, 84/39, 46/11 e 49/12). Uma instrumentação temporária anotou, em cada campanha, a apuração de vereador: refaz `ElectionRules.Proportional` com as listas finais (os votos de cada candidato e os 8 anônimos de cada coletivo) e confere que dá os mesmos eleitos que o jogo (em todas as campanhas, menos 1 de 200 no pequeno: uma campanha no partido forte em que o jogador se elege nas duas contas). Intervalo de confiança de 95% pelo método de Wilson, entre parênteses.

## Resumo

- **Metade do excesso é a amostra.** O BalanceSim joga a campanha `i` na cidade `i % 10` e no partido mais forte quando `i` é par (`Program.cs`). Com 10 cidades, as cidades de índice par só jogam o partido mais forte e as de índice ímpar só o sorteado. No médio, o sorteado caiu nas 5 cidades menores da amostra (14 a 25 mil eleitores, todas com 11 vagas). Trocando as metades, o sorteado faz 25 de 100 nas outras 5 cidades. Nas 10 cidades juntas: **64 de 200 = 32% (26–39)**, 7 pontos acima da faixa em vez de 14.
- **Mesmo nas 10 cidades, o médio fica acima da faixa por dois motivos que o pequeno não tem:**
  1. **mais vagas para o mesmo voto da chapa fraca.** A chapa sorteada faz cerca de 9% dos votos válidos nos dois portes (9,2% no pequeno, 9,4% no médio). O quociente é 1/vagas: 11,1% com 9 vagas e 9,1% com 11. Na linha de base, a mesma chapa chega a 0,83 do quociente no pequeno e a 1,03 no médio, e passa da cláusula de 80% em 24% contra 50% das campanhas;
  2. **mais partidos com voto real na cidade.** As cidades médias têm de 7 a 20 partidos com votação de vereador em 2024 (`forca_vereador` do cenário); as pequenas, de 3 a 8. No médio, 22% dos partidos sorteados têm força real mas ficam fora dos 6 da disputa, e entram no lugar do 6º (`RaceSetup.Create`). Esses se elegem em 48% das vezes. No pequeno esse caso quase não existe (2 de 200).
- **Quando a chapa sorteada faz cadeira, a cadeira é do jogador** (78% no médio, 79% no pequeno): ele é o 1º da chapa, à frente dos anônimos, porque o coletivo de um partido fraco quase não tem rivais nomeados e o jogador do médio ainda supera o 1º anônimo. No grande isso já não vale: a chapa sorteada faz cadeira em 61 de 100 campanhas, mas o jogador fica com ela em 11 (18%), porque o coletivo representa 15 candidatos e o 1º anônimo tem 0,37 do quociente, contra 0,14 do jogador.
- **Nenhum parâmetro mexe só no sorteado do médio.** Os parâmetros por porte (`npc_effort_by_porte`, `porte_recognition_multiplier`) mexem só no médio, mas no forte e no sorteado juntos. O par **`npc_effort_by_porte.medio` 1,4 → 1,7 com `porte_recognition_multiplier.medio` 1,0 → 0,8** põe os dois dentro da faixa: forte 75%, sorteado 24%, sem campanha 0% (200 campanhas), e os outros portes não mudam (por construção; conferido).

## 1. Diagnóstico por porte

Linha de base, 100 campanhas no partido sorteado por porte (as cidades de índice ímpar da amostra). "Força" é a fatia do partido em `forca_vereador` do cenário `brasil_2026` na cidade (`CampaignContext.PartyStrength`).

| Partido sorteado | Pequeno | Médio | Grande | Metrópole |
|---|---|---|---|---|
| Eleitos | 19 de 100 = 19% (13–28) | 39 de 100 = 39% (30–49) | 11 de 100 = 11% (6–19) | 12 de 100 = 12% (7–20) |
| Vagas (média; mín.–máx.) | 9,0 (9–9) | 11,0 (11–11) | 15,6 (10–21) | 37,4 (27–43) |
| Eleitores (média) | 5.351 | 18.987 | 94.581 | 1.411.281 |
| Quociente eleitoral (votos; % dos válidos) | 392; 11,1% | 1.146; 9,1% | 4.233; 6,8% | 24.681; 2,8% |
| Partidos na disputa | 6 | 6 | 6 | 6 |
| Partidos com voto na cidade (`forca_vereador` > 0) | 4,8 | 10,2 | 17,6 | 25,6 |
| Sorteado entre os 6 mais fortes | 21 de 100 = 21% (14–30) | 23 de 100 = 23% (16–32) | 21 de 100 = 21% (14–30) | 27 de 100 = 27% (19–36) |
| Sorteado com voto, fora dos 6 | 0 de 100 = 0% (0–4) | 16 de 100 = 16% (10–24) | 39 de 100 = 39% (30–49) | 65 de 100 = 65% (55–74) |
| Sorteado sem voto na cidade | 79 de 100 = 79% (70–86) | 61 de 100 = 61% (51–70) | 40 de 100 = 40% (31–50) | 8 de 100 = 8% (4–15) |
| Rivais nomeados na chapa (sem o jogador) | 0,48 | 0,45 | 0,59 | 0,70 |
| Candidatos que o coletivo representa (`Multiplicity`) | 8,5 | 10,6 | 15,0 | 36,7 |
| Anônimos na apuração | 8 | 8 | 8 | 8 |
| Votos da chapa / quociente (média; mediana) | 0,83; 0,58 | 1,03; 0,80 | 1,36; 1,16 | 4,15; 3,54 |
| Chapa com 80% do quociente (cláusula da 2ª etapa) | 24 de 100 = 24% (17–33) | 50 de 100 = 50% (40–60) | 61 de 100 = 61% (51–70) | 99 de 100 = 99% (95–100) |
| Votos do jogador / quociente | 0,23 | 0,28 | 0,14 | 0,14 |
| Jogador com 20% do quociente | 39 de 100 = 39% (30–49) | 55 de 100 = 55% (45–64) | 21 de 100 = 21% (14–30) | 22 de 100 = 22% (15–31) |
| 1º anônimo da chapa / quociente | 0,15 | 0,21 | 0,37 | 1,32 |
| Jogador em 1º na chapa | 45 de 100 = 45% (36–55) | 63 de 100 = 63% (53–72) | 6 de 100 = 6% (3–12) | 0 de 100 = 0% (0–4) |
| Reconhecimento final do jogador | 72,3% | 62,4% | 53,0% | 38,5% |
| Chapa sorteada com ao menos 1 cadeira | 24 de 100 = 24% (17–33) | 50 de 100 = 50% (40–60) | 61 de 100 = 61% (51–70) | 99 de 100 = 99% (95–100) |
| Com cadeira, ela é do jogador | 19 de 24 = 79% (60–91); cadeiras: jogador 0,79, anônimos 0,25, nomeados 1,08 | 39 de 50 = 78% (65–87); cadeiras: jogador 0,78, anônimos 0,52, nomeados 0,24 | 11 de 61 = 18% (10–29); cadeiras: jogador 0,18, anônimos 1,52, nomeados 0,10 | 12 de 99 = 12% (7–20); cadeiras: jogador 0,12, anônimos 3,99, nomeados 0,15 |
| Cadeiras da chapa por etapa (quociente / 2ª / 3ª) | 32 / 19 / 0 | 51 / 26 / 0 | 85 / 25 / 0 | 357 / 65 / 0 |

O médio é o meio-termo entre dois limiares. A chapa sorteada passa da cláusula de 80% cada vez mais com o porte (24%, 50%, 61% e 99%), porque o quociente cai de 11,1% para 2,8% dos válidos e a lista do coletivo cresce de 8,5 para 36,7 candidatos. O jogador fica com a cadeira da chapa cada vez menos (79%, 78%, 18% e 12%), porque o 1º anônimo da chapa sobe de 0,15 para 1,32 do quociente e o jogador cai de 0,23 para 0,14. Só no médio as duas coisas acontecem juntas com frequência: a chapa faz cadeira em metade das campanhas e o jogador fica com ela em quase 4 de 5.

Como cada linha sai do código:

- **Vagas:** as reais do TSE, projetadas para o ano (`CampaignContext.SeatsIn`, `PopulationProjection.Seats`). O porte vem da população (`data/portes.json`: médio até 100 mil habitantes), as vagas vêm da Câmara real. Por isso Arcos (44 mil habitantes) e Bom Despacho (55 mil) têm 9 vagas, e as médias pequenas da amostra têm 11.
- **Quociente eleitoral:** votos válidos ÷ vagas, arredondado pelo art. 106 (`ElectionRules.ElectoralQuotient`). Os válidos são os nominais mais a legenda (`legend_vote_share` 0,08 da chapa, em `SlateLists`).
- **Partidos na disputa:** sempre 6 (`parties_in_race`), os 6 de maior força na cidade; o do jogador entra no lugar do 6º se estiver fora (`RaceSetup.Create`). Os votos dos partidos que ficam fora da disputa não existem: o softmax só reparte entre os 6.
- **Partido sorteado:** sorteado entre todos os partidos do mundo, sem olhar a cidade (`rng.Pick(world.Parties)`, `SimContent.NewCareer`). Um partido sem `forca_vereador` na cidade tem afinidade zero em todos os segmentos (`ElectorateBuilder.FitAffinity`): o termo do partido (`weights.party` 2,0) vale zero para o jogador e para o coletivo dele.
- **Tamanho das listas:** os 10 rivais nomeados são repartidos pela força dos 6 partidos (`RaceSetup.Apportion`); o partido sorteado quase nunca recebe nenhum. O coletivo de cada partido representa vagas + 1 − nomeados candidatos (`Multiplicity`, art. 10 da Lei 9.504) no modelo de voto (`VoteModel.Project`: a fatia é multiplicada por esse número) e vira 8 anônimos com votos 1, 1/2, 1/3… na apuração (`bulk_candidates_per_party`, `CampaignEngine.Split`). A chapa fraca é, então, o jogador mais um coletivo de lista cheia.
- **Sobras e média:** 1ª etapa, quociente partidário, com candidato de ao menos 10% do quociente; 2ª etapa, maiores médias entre partidos com 80% do quociente e candidatos com 20%; 3ª etapa, maiores médias entre todos (`ProportionalRulesConfig`, padrão da lei; `ElectionRules.Proportional`).
- **Cláusula de desempenho:** é a de 80% e 20% da 2ª etapa. Na prática a 3ª etapa (aberta a todos, sem piso) quase nunca é usada: no médio, nenhuma vaga da cidade saiu da 3ª etapa em 400 campanhas; no pequeno, houve vaga da 3ª etapa em 19 de 400 campanhas, e 2 delas foram da chapa sorteada. No médio, toda cadeira da chapa sorteada sai do quociente partidário ou da 2ª etapa, e chapa abaixo de 80% do quociente não faz cadeira.

## 2. A amostra: cada cidade joga só um dos dois partidos

`Program.cs` joga a campanha `i` em `chosen[i % chosen.Count]` com `strongestParty: i % 2 == 0`. Com 10 cidades (número par), a cidade de índice par só recebe campanhas no partido mais forte e a de índice ímpar só no sorteado. O mesmo vale para as paradas. Rodando de novo com as metades trocadas (instrumentação temporária, `strongestParty: i % 2 == 1`; 200 campanhas no médio e no pequeno):

| Porte | Forte, cidades pares (base) | Forte, cidades ímpares (trocado) | Forte, 10 cidades | Sorteado, cidades ímpares (base) | Sorteado, cidades pares (trocado) | Sorteado, 10 cidades |
|---|---|---|---|---|---|---|
| pequeno | 73 de 100 = 73% (64–81) | 63 de 100 = 63% (53–72) | 136 de 200 = **68%** (61–74) | 19 de 100 = 19% (13–28) | 19 de 100 = 19% (13–28) | 38 de 200 = **19%** (14–25) |
| médio | 84 de 100 = 84% (76–90) | 65 de 100 = 65% (55–74) | 149 de 200 = **74,5%** (68–80) | 39 de 100 = 39% (30–49) | 25 de 100 = 25% (18–34) | 64 de 200 = **32%** (26–39) |

O sorteado do médio, cidade por cidade (20 campanhas em cada; as 5 primeiras são as da linha de base):

| Cidade | Eleitores | Vagas | Partidos com força | Sorteado eleito | Chapa sorteada com cadeira | Chapa / quociente | Jogador / quociente |
|---|---|---|---|---|---|---|---|
| Urucurituba/AM | 14.301 | 11 | 11 | 9 | 10 | 0,99 | 0,28 |
| Nova Xavantina/MT | 16.277 | 11 | 7 | 5 | 7 | 0,92 | 0,26 |
| Aldeias Altas/MA | 18.492 | 11 | 7 | 6 | 8 | 0,94 | 0,27 |
| Sobradinho/BA | 20.428 | 11 | 11 | 10 | 15 | 1,13 | 0,27 |
| Ouro Fino/MG | 25.436 | 11 | 15 | 9 | 10 | 1,19 | 0,35 |
| Tupaciguara/MG | 20.892 | 11 | 11 | 6 | 8 | 0,86 | 0,28 |
| Arcos/MG | 33.487 | 9 | 12 | 2 | 3 | 0,56 | 0,19 |
| Bom Despacho/MG | 39.050 | 9 | 15 | 4 | 7 | 0,73 | 0,19 |
| Porto Ferreira/SP | 39.939 | 11 | 20 | 8 | 10 | 1,05 | 0,28 |
| Ipirá/BA | 48.012 | 15 | 10 | 5 | 13 | 1,06 | 0,21 |

Leituras:
- Pelo número de vagas: 9 vagas, 6 de 40 = 15% (7–29); 11 vagas, 53 de 140 = 38% (30–46); 15 vagas, 5 de 20 = 25% (11–47).
- As 5 cidades do sorteado na linha de base são todas de 11 vagas, e as duas de 9 vagas caíram no forte.
- O forte também depende da metade: 84% nas cidades maiores, 65% nas 5 menores. No pequeno, 73% e 63%. Nas 10 cidades, os dois portes ficam dentro da faixa (68% e 74,5%).
- Em Ipirá (15 vagas) a chapa sorteada faz cadeira em 13 de 20, mas o jogador fica com ela em só 5: com 15 vagas o coletivo representa 15 candidatos e o 1º anônimo passa à frente do jogador.

## 3. Por que a chapa fraca faz cadeira no médio

O partido sorteado, nas 200 campanhas do médio (as 10 cidades), em três casos:

| Caso | Pequeno (200) | Médio (200) |
|---|---|---|
| entre os 6 mais fortes, com força | 39: chapa / quociente 1,96; com cadeira 33; eleitos 25 = 64% (48–77) | 41: chapa / quociente 1,87; com cadeira 40; eleitos 25 = 61% (46–74) |
| com força na cidade, mas fora dos 6 (entra no lugar do 6º) | 2: eleitos 2 | **44**: chapa / quociente 0,95; com cadeira 26; eleitos 21 = **48%** (34–62) |
| sem força na cidade | 159: chapa / quociente 0,48; com cadeira 12; eleitos 11 = 7% (4–12) | 115: chapa / quociente 0,61; com cadeira 25; eleitos 18 = **16%** (10–23) |
| total | 38 = 19% (14–25) | 64 = 32% (26–39) |

1. **O voto da chapa fraca não cai com o porte; o quociente cai.** Sem força, a chapa sorteada é o jogador mais um coletivo de lista cheia (vagas + 1 candidatos, `Multiplicity`), com o termo do partido em zero. Na linha de base, o coletivo (com a legenda) faz 6,7% dos válidos no pequeno e 6,8% no médio, e o jogador, 2,5% e 2,6%. O coletivo cresce com as vagas, porque a lista cresce; o quociente encolhe com elas. De 9 para 11 vagas, a chapa sem força sobe de 0,48 para 0,61 do quociente (10 cidades de cada porte).
2. **Num município com muitos partidos, o sorteado tem voto próprio e menos concorrentes.** Com 6 partidos na disputa, o partido que é o 8º ao 15º da cidade entra no lugar do 6º. Fica com a própria força e com uma parte dos votos dos partidos que ficaram fora da disputa, porque o modelo reparte o voto só entre os 6. No médio isso acontece em 22% das campanhas no sorteado; no pequeno, em 1%, porque as cidades pequenas têm poucos partidos com voto.
3. **O jogador fica com a cadeira da chapa.** No médio o jogador termina com 62% de reconhecimento e 0,28 do quociente; o 1º anônimo da própria chapa, com 0,21; o 1º nomeado, com 0,07. O jogador é o 1º da chapa em 63% das campanhas e ganha 78% das cadeiras que a chapa faz. No grande, com o empenho dos rivais de 3,5 e o reconhecimento × 0,6, os anônimos passam à frente: a chapa sorteada faz cadeira em 61 de 100 campanhas, mas o jogador fica com ela em 11 (18%), porque o coletivo representa 15 candidatos e o 1º anônimo tem 0,37 do quociente, contra 0,14 do jogador.
4. **A cláusula é o limiar.** No médio, nenhuma cadeira da chapa sorteada vem da 3ª etapa. A chapa faz cadeira quando passa de 80% do quociente (no médio, as 91 chapas sorteadas com cadeira nas 10 cidades estavam todas acima disso) e o jogador se elege quando, além disso, tem 20% do quociente e fica à frente dos anônimos.
5. **Campanha "suja" pesa mais no sorteado:** na linha de base, 27 de 50 eleitos nas campanhas "sujas" contra 12 de 50 nas limpas (régua da D-026).

## 4. Alavancas testadas

Todas com a mesma semente e as mesmas cidades da linha de base. Cada teste mudou só uma cópia de `data/campaign/config.json`. Os dois parâmetros são lidos pelo porte do município em foco (`CampaignEngine.RecognitionScale`, `NpcTurn`, `RivalEngine` linha 252, `EffectApplier`, `RaceSetup.Create`, `DebateEngine`), e cada campanha é independente: os outros portes saem idênticos à linha de base. Conferido: 10 campanhas do pequeno com os dados do teste C deram exatamente o mesmo resultado, campanha a campanha. Nas tabelas, os outros portes repetem a linha de base de 200 (o "sem campanha" deles é o da régua, 0 de 200, porque esta rodada não mediu paradas).

Controle do médio com 100 campanhas (as primeiras 100 da linha de base): forte 43 de 50 = 86% (74–93), sorteado 17 de 50 = 34% (22–48), sem campanha 0 de 100 (0–4); tipo 26,3%, par 22,2%, canal 29,1% (Material gráfico), decisões 2,75.

### Alavanca A: empenho dos rivais no médio

- **O que muda:** `npc_effort_by_porte.medio` de 1,4 para **2,0** em data/campaign/config.json. Os rivais nomeados e o coletivo de cada partido fazem cerca de 43% mais saídas por semana no médio (`CampaignEngine.NpcTurn`, `RivalEngine.ScheduleActions`).
- 100 campanhas no médio (50 e 50) e 100 paradas.

| Porte | Mais forte | Sorteado | Sem campanha | Tipo | Par | Canal | Decisões |
|---|---|---|---|---|---|---|---|
| pequeno | 73 de 100 = **73%** (64–81) | 19 de 100 = **19%** (13–28) | 0 de 200 (0–2) | 27,8% | **30,2%** | 33,1% (Rua) | 2,72 |
| médio | 29 de 50 = **58%** (44–71) | 8 de 50 = **16%** (8–29) | 0 de 100 (0–4) | 26,9% | 22,3% | 31,0% (Rua) | 2,75 |
| grande | 46 de 100 = **46%** (37–56) | 11 de 100 = **11%** (6–19) | 0 de 200 (0–2) | 27,5% | 19,0% | 35,4% (Redes) | 2,81 |
| metrópole | 49 de 100 = **49%** (39–59) | 12 de 100 = **12%** (7–20) | 0 de 200 (0–2) | 37,2% | 17,7% | 29,9% (Redes) | 2,91 |

Contas no médio: limpas 0,00%; "por fora" acima de 30%, 30,3%. Contra o controle de 100: a chapa sorteada faz cadeira do mesmo jeito (26 de 50 contra 25 de 50), mas o jogador fica com ela em 8 de 26 (31%) contra 17 de 25 (68%). O jogador cai de 0,25 para 0,16 do quociente; o 1º anônimo da chapa sobe de 0,22 para 0,24. No forte, o jogador cai de 0,83 para 0,52 do quociente e de 1º da chapa em 62% para 38%: o forte cai abaixo de 65%.

### Alavanca B: escala do reconhecimento no médio

- **O que muda:** `porte_recognition_multiplier.medio` de 1,0 para **0,8**. Escala o reconhecimento inicial dos rivais nomeados e do coletivo (`RaceSetup.Create`) e todos os ganhos de reconhecimento, inclusive os do jogador (`EffectApplier`, `CampaignEngine.RecognitionScale`).
- 100 campanhas no médio e 100 paradas.

| Porte | Mais forte | Sorteado | Sem campanha | Tipo | Par | Canal | Decisões |
|---|---|---|---|---|---|---|---|
| pequeno | igual à base | | | | | | |
| médio | 47 de 50 = **94%** (84–98) | 15 de 50 = **30%** (19–44) | 0 de 100 (0–4) | 25,8% | 22,5% | 38,9% (Rua) | 2,73 |
| grande e metrópole | iguais à base | | | | | | |

Contas no médio: limpas 0,00%; "por fora" acima de 30%, 34,9%. Contra o controle de 100: o reconhecimento de todos cai, mas o dos rivais e do coletivo (que partem de 18 × 0,8) cai mais em proporção do que o do jogador que faz campanha. No forte, o jogador sobe de 0,83 para 0,92 do quociente, o 1º anônimo da chapa cai de 0,60 para 0,56, e o forte passa de 90%. No sorteado, chapa com cadeira em 24 de 50 (controle 25) e jogador com 0,27 do quociente (controle 0,25): quase nada muda.

### Alavanca C: as duas juntas, mais brandas no empenho

- **O que muda:** `npc_effort_by_porte.medio` de 1,4 para **1,7** e `porte_recognition_multiplier.medio` de 1,0 para **0,8**. A ideia: o empenho tira votos do jogador nos dois partidos; o reconhecimento menor devolve ao forte (alavanca B) e não ao sorteado.
- **200 campanhas no médio (100 e 100) e 200 paradas**, para firmar o número.

| Porte | Mais forte | Sorteado | Sem campanha | Tipo | Par | Canal | Decisões |
|---|---|---|---|---|---|---|---|
| pequeno | 73 de 100 = **73%** (64–81) | 19 de 100 = **19%** (13–28) | 0 de 200 (0–2) | 27,8% | **30,2%** | 33,1% (Rua) | 2,72 |
| médio | 75 de 100 = **75%** (66–82) | 24 de 100 = **24%** (17–33) | 0 de 200 = **0%** (0–2) | 26,0% | 22,0% | 32,2% (Redes) | 2,72 |
| grande | 46 de 100 = **46%** (37–56) | 11 de 100 = **11%** (6–19) | 0 de 200 (0–2) | 27,5% | 19,0% | 35,4% (Redes) | 2,81 |
| metrópole | 49 de 100 = **49%** (39–59) | 12 de 100 = **12%** (7–20) | 0 de 200 (0–2) | 37,2% | 17,7% | 29,9% (Redes) | 2,91 |

Contas no médio: limpas 0,00% (102 campanhas); "por fora" acima de 30%, 32,7% (67). Campanhas limpas no sorteado: 7 de 50; "sujas": 17 de 50.

Com as metades trocadas (as outras 5 cidades, 200 campanhas): forte 62 de 100 = 62% (52–71), sorteado 24 de 100 = 24% (17–33). Nas 10 cidades: **forte 137 de 200 = 68% (62–75), sorteado 48 de 200 = 24% (19–30)**. O que muda é o caso 2 da seção 3 (sorteado com voto, fora dos 6: de 48% para 27%) e o caso 3 (sem voto: de 16% para 8%); o sorteado entre os 6 mais fortes continua em 66%. Por vagas: 9 vagas, 9 de 40; 11 vagas, 35 de 140; 15 vagas, 4 de 20.

### Resumo das alavancas (médio; os outros portes não mudam)

Eleitos no partido mais forte / no sorteado / sem campanha, em %.

| Medição | Faixa (D-057) | Base | A: empenho 2,0 | B: reconhecimento × 0,8 | C: empenho 1,7 e reconhecimento × 0,8 |
|---|---|---|---|---|---|
| amostra do BalanceSim (5 cidades por partido) | 65–90 / 5–25 / 0–1 | 84 / **39** / 0 (200) | **58** / 16 / 0 (100) | **94** / **30** / 0 (100) | 75 / 24 / 0 (200) |
| 10 cidades nos dois partidos | 65–90 / 5–25 / 0–1 | 74,5 / **32** / – | – | – | 68 / 24 / – |
| critérios fora | — | nenhum no médio | nenhum | nenhum | nenhum |

## Leitura

- **Nenhum parâmetro mexe só no sorteado do médio.** O que é próprio do sorteado do médio (11 vagas e muitos partidos com voto) vem de dados reais (vagas do TSE e `forca_vereador` de 2024) e de regras globais (`parties_in_race`, `Multiplicity` do coletivo). Os parâmetros que mexem só no médio mexem no forte e no sorteado juntos.
- **A (empenho 2,0)** derruba os dois: o sorteado vai para 16%, o forte para 58%, fora da faixa. A chapa sorteada continua fazendo cadeira (26 de 50); quem fica com ela passa a ser o anônimo da chapa.
- **B (reconhecimento × 0,8)** sobe o forte (94%, acima de 90%) e quase não mexe no sorteado (30%, controle 34%). Sozinha, não serve; serve para compensar o forte quando o empenho sobe.
- **C (empenho 1,7 e reconhecimento × 0,8)** combina as duas direções e põe o médio inteiro na faixa: forte 75% (66–82), sorteado 24% (17–33), sem campanha 0% (0–2), com tipo, par, canal, decisões e contas dentro. O limite de cima do intervalo do sorteado (33%) ainda passa de 25%. Nas 10 cidades fica em 68% e 24% (19–30), mas o forte das 5 cidades menores cai para 62% (52–71). As duas mudanças valem para todo jogador no médio: a campanha fica mais difícil nesse porte (rivais com mais saídas, ganhos de reconhecimento 20% menores), no forte também.
- **Recomendação:** a decisão é do autor, em duas partes.
  1. **Antes de mexer em número, corrigir a amostra do BalanceSim** (ferramenta, não jogo): cada cidade jogar os dois partidos, por exemplo `strongestParty: (i / cidades) % 2 == 0` em vez de `i % 2 == 0`. Com isso o sorteado do médio passa a 32% (26–39), o forte do médio a 74,5% e o do pequeno a 68%. O médio continua fora, mas por 7 pontos, não por 14.
  2. **Se o autor quiser o médio na faixa já, o candidato é a alavanca C** (`npc_effort_by_porte.medio` 1,7 e `porte_recognition_multiplier.medio` 0,8): é a única das três que põe forte, sorteado e sem campanha do médio dentro da faixa, nas duas amostras, sem tocar nos outros portes. O preço é o forte do médio cair de 84% para 75% (68% nas 10 cidades), perto do piso de 65%. Como a D-057 manda medir de novo depois do cansaço por veículo (`cansaco-veiculo`), que sobe os eleitos, o número de C deve ser conferido nessa medição antes de entrar.
- **O que não foi testado:** um número de partidos na disputa que dependa de quantos partidos têm voto na cidade (hoje `parties_in_race` = 6 em todo porte) atacaria o caso 2 da seção 3, que é o mais próprio do médio. Mexe em todos os portes onde há mais de 6 partidos com voto (médio, grande e metrópole) e pede código novo. Fica como próximo teste, se o autor quiser.

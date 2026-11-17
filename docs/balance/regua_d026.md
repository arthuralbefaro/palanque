# Régua da D-026: medição e alavancas

Medido em 2026-11-16 e 17, no código da `main` em `fd873bc` (depois da divisão rural por distritos, D-049). Só relatório: nenhum número do jogo mudou. Os experimentos foram mudanças temporárias em `data/` e no código, revertidas depois.

Régua (D-026, D-039 "Dificuldade", D-048 adendo 3): partido mais forte eleito na grande maioria das cidades pequenas e em cerca de metade das metrópoles (a medição original deu 96%, 96%, 67% e 58%); partido sorteado raramente (13%, 13%, 8% e 0%); sem campanha, nunca.

Como foi medido: `tools/Planalto.BalanceSim`, semente 1, 10 cidades por porte (as mesmas em todas as rodadas), metade das campanhas no partido mais forte da cidade e metade num partido sorteado; "sem campanha" são as mesmas cidades e sementes com o robô parado (`--paradas`). Intervalo de confiança de 95% pelo método de Wilson, entre parênteses. A linha de base tem 200 campanhas por porte (100 no partido mais forte, 100 no sorteado) e 200 paradas; as alavancas têm 100 por porte (50 e 50) e 100 paradas, porque a máquina estava dividida com a integração da R4 e cada rodada de 200 levou 1h45. Com 50 campanhas o intervalo é de cerca de ±12 pontos perto de 50% e de ±8 perto de 10%: diferenças menores que isso entre uma alavanca e a base são ruído.

## 1. Linha de base (200 campanhas por porte)

| Porte | Partido mais forte | Partido sorteado | Sem campanha (forte + sorteado) |
|---|---|---|---|
| pequeno | 73 de 100 = **73%** (64–81) | 19 de 100 = **19%** (13–28) | 0 de 200 = **0%** (0–2) |
| médio | 84 de 100 = **84%** (76–90) | 39 de 100 = **39%** (30–49) | 0 de 200 = **0%** (0–2) |
| grande | 46 de 100 = **46%** (37–56) | 11 de 100 = **11%** (6–19) | 0 de 200 = **0%** (0–2) |
| metrópole | 49 de 100 = **49%** (39–59) | 12 de 100 = **12%** (7–20) | 0 de 200 = **0%** (0–2) |

Critérios que o BalanceSim já reporta, na mesma rodada:

| Porte | Tipo (até 40%) | Par tipo e zona (até 25%) | Canal (até 40%) | Decisões com troca por semana (1 ou mais) |
|---|---|---|---|---|
| pequeno | 27,8% | **30,2%** | 33,1% (Rua) | 2,72 |
| médio | 26,8% | 22,0% | 31,3% (Rua) | 2,75 |
| grande | 27,5% | 19,0% | 35,4% (Redes) | 2,81 |
| metrópole | 37,2% | 17,7% | 29,9% (Redes) | 2,91 |

Contas: limpas perdem o mandato em 0,00% (406 campanhas); "por fora" acima de 30% do limite, em 35,6% (304; critério de 25% a 40%).

Leituras da base que explicam o resto:
- O sorteado do médio (39%) está acima da medição da `fase-r3` (30%), mas dentro do intervalo dela; os outros portes repetem a `fase-r3` dentro do ruído.
- Com o **mesmo reconhecimento** no fim da campanha (pequeno: 72,5% no mais forte, 72,3% no sorteado), o jogador do partido mais forte faz 74% do quociente eleitoral e o do sorteado, 23%. A diferença inteira vem do partido.
- Campanhas "sujas" se elegem mais no sorteado em todos os portes (pequeno 13 contra 6 de 50; médio 27 contra 12; grande 8 contra 3; metrópole 12 contra 0). Nas campanhas limpas o sorteado fica em 12%, 24%, 6% e 0%.
- Parado, o jogador termina com 7% a 12% de reconhecimento e de 1% a 2% do quociente.

## 2. O que move cada taxa (conferido no código)

**Partido mais forte e partido sorteado (campanha ativa).** As duas taxas dependem dos mesmos números, porque dependem dos votos do jogador em relação aos dos rivais:

| Parâmetro | Arquivo | Onde entra no código |
|---|---|---|
| `npc_effort_by_porte` | data/campaign/config.json | saídas por semana do candidato coletivo (`CampaignEngine.NpcTurn`) e dos rivais nomeados (`CampaignEngine.ScheduleActions`, RivalEngine.cs) |
| `npc_action_efficiency` | data/campaign/config.json | presença por saída: `NpcTurn` (`basePresence`) e `RunRivalAction` (`amount`) |
| `npc_actions_per_week`, `npc_drive` | data/campaign/config.json | número de saídas e empenho sorteado de cada rival nomeado (`RaceSetup.Create`, `ScheduleActions`) |
| `bulk_drive` | data/campaign/config.json | empenho do candidato coletivo de cada partido (`RaceSetup.Create`, `NpcTurn`) |
| `npc_recognition`, `incumbent_recognition_bonus` | data/campaign/config.json | reconhecimento inicial dos rivais nomeados; o bônus vale para os vereadores em exercício, que vêm da Câmara do cenário (`ctx.Local.Camara`) e se concentram nos partidos fortes |
| `bulk_recognition` | data/campaign/config.json | reconhecimento inicial do coletivo e piso do decaimento dele (`RaceSetup.Create`; `Territory.Decay` em `CampaignEngine.AdvanceWeek`) |
| `porte_recognition_multiplier` | data/campaign/config.json | escala o reconhecimento inicial dos rivais e os ganhos de todos, inclusive do jogador (`EffectApplier`) |
| `named_rivals`, `parties_in_race` | data/campaign/config.json | quantos rivais nomeados há (repartidos pela força dos partidos, `RaceSetup.Apportion`) e quantos partidos disputam |
| `bulk_candidates_per_party` (8), `legend_vote_share` (0,08) | data/campaign/config.json | o coletivo vira 8 candidatos anônimos com votos 1, 1/2, 1/3… (`CampaignEngine.Split`, chamado em `SlateLists`, RivalEngine.cs); a legenda sai da fatia do coletivo |
| `weights.party` (2,0), `weights.recognition` (2,5), `temperature` (1,3) | data/electorate/vote_model.json | utilidade de cada candidato (`VoteModel.Utility`) e softmax (`VoteModel.Project`) |
| `awareness` (expoente 1, piso 0,02) | data/electorate/vote_model.json | fatia multiplicada pelo reconhecimento (`VoteModel.Project`) |
| força do partido na cidade | data/parties/scenarios/brasil_2026 (`ForcaVereador`) | `ElectorateBuilder.Strength` e `FitAffinity` (Electorate.cs): a afinidade de cada segmento com cada partido; define também quais 6 partidos disputam |
| `decaimento_semanal` (0,6), `zona_natal_fator`, `reduto_fator` | data/territory/config.json, bloco `simulacao` | perda semanal de presença onde o candidato não agiu (`TerritoryState.Decay`) e a presença de partida por zona (`RaceSetup.BuildTerritory`) |
| regras do quociente e das sobras | `ProportionalRulesConfig` (lei: 10%, 80% e 20%, última fase aberta a todos) | `ElectionRules.Proportional`; não é parâmetro de balanceamento |

**Sem campanha.** O jogador parado só tem o reconhecimento de origem (`recognition` de 2 a 10 em data/social_origins.json, gravado por `PersonGenerator`; não depende do partido nem do porte), que perde 0,6 ponto por semana em 38 semanas. O que o mantém fora: o conjunto de consideração (`awareness`), o peso do reconhecimento (`weights.recognition`) e os candidatos anônimos do próprio partido, que ficam acima dele. Tudo o que dá reconhecimento ao jogador sem ação (herdado, piso) ou que espalha os votos do coletivo em mais nomes mexe nesta taxa.

## 3. Por que mexer num move o outro

O jogador perde por caminhos diferentes no partido mais forte e no sorteado (diagnóstico acrescentado ao BalanceSim só no experimento: quem ocupou as cadeiras do partido do jogador derrotado; intervalos sobre as rodadas das alavancas A, B e C, que repetem o mesmo padrão):

| Porte | Partido mais forte, derrotado | Partido sorteado, derrotado |
|---|---|---|
| pequeno | o partido sempre tem cadeira (2,4 a 3,0); perde para nomeados da própria chapa (1,6 a 2,3) e para anônimos (0,8 a 1,0) | o partido não faz cadeira em 39 a 43 de 41 a 45 derrotas |
| médio | partido com 4,2 a 4,6 cadeiras; perde para nomeados (2,7 a 2,8) e anônimos (1,5 a 1,8) | sem cadeira em 25 a 35 de 32 a 42 derrotas |
| grande | partido com 4,0 a 4,4 cadeiras, **3,4 a 3,8 delas de anônimos** | sem cadeira em 17 a 29 de 45 a 48; com cadeira, quase todas de anônimos |
| metrópole | partido com 5,2 a 6,4 cadeiras, **4,7 a 5,7 delas de anônimos** | o partido sempre tem cadeira (3,3 a 3,8), quase todas de anônimos |

Disso sai o mecanismo:

1. **A força do partido cancela dentro da chapa.** O termo do partido (`weights.party` × afinidade do segmento / maior afinidade, em `VoteModel.Utility`) é o mesmo para o jogador, os rivais nomeados e o coletivo do mesmo partido. No partido mais forte, ele dá mais cadeiras à chapa (`ElectionRules.Proportional` sobre a soma da chapa em `SlateLists`), mas não ajuda o jogador a ficar à frente dos colegas. Os colegas do partido forte são, além disso, os mais duros: o partido forte tem mais rivais nomeados (`Apportion` pela força), os vereadores em exercício com +20 de reconhecimento, e o coletivo dividido em só 8 anônimos, cujo primeiro leva 37% da fatia (1/H₈). Nas cidades grandes e nas metrópoles o coletivo representa de cerca de 13 a mais de 40 candidatos (`Multiplicity` = vagas + 1 − nomeados) mas é repartido em 8: cada anônimo tem votos de vários candidatos juntos, e é a eles que o jogador perde.
2. **No partido sorteado, a força entra pelo voto do próprio jogador e pelo limiar.** O partido sorteado costuma estar fora dos 6 mais fortes e entra no lugar do último (`RaceSetup.Create`); com pouca afinidade, o jogador faz de um terço a metade dos votos que faria no forte, com o mesmo reconhecimento. Ele se elege quando os votos dele levam o partido até as sobras (80% do quociente para o partido e 20% para o candidato) ou até a última fase. É um limiar absoluto sobre os votos dele.
3. **O empenho dos rivais derruba os dois porque mexe nos votos do jogador, não na estrutura.** `npc_effort_by_porte`, `npc_action_efficiency`, `bulk_drive` e `npc_drive` aumentam o reconhecimento de todos os rivais, de todos os partidos (`NpcTurn`, `ScheduleActions` e `RunRivalAction`), e no softmax com o fator de consideração (`VoteModel.Project`) isso tira do jogador a mesma fração dos votos nos dois casos. No forte, ele cai na ordem da chapa; no sorteado, cai abaixo do limiar. As rodadas de calibração da R3 (D-048, P-26) mexeram em empenho, eficiência, `bulk_drive` e número de anônimos, e as duas taxas andaram juntas em todas.
4. **O que separa precisa mudar só um dos dois caminhos:** o número de cadeiras da chapa forte ou a disputa dentro dela (só o forte), ou o voto do jogador num partido fraco (só o sorteado). O reconhecimento herdado da força do partido entra pelo voto do jogador, mas proporcional à força, então pesa quase só no forte.

## 4. Alavancas testadas

Todas medidas com a mesma semente e as mesmas cidades da base, 100 campanhas por porte (50 no forte, 50 no sorteado) e 100 paradas (50 e 50). O código temporário de cada uma ficou desligado por padrão, de modo que só o dado ligava a alavanca.

### Alavanca A: peso do partido no modelo de voto

- **O que muda:** `weights.party` de 2,0 para **3,0** em data/electorate/vote_model.json. A diferença de utilidade entre o partido mais forte e um fraco cresce 50%: a chapa forte ganha cadeiras, o jogador num partido fraco faz menos votos.
- **Efeito colateral fora do BalanceSim:** mexe na calibração da D-015 (temperatura 1,3, peso partidário 2,0 contra a votação real de prefeito de 2024), que não foi medida de novo.

| Porte | Mais forte | Sorteado | Sem campanha | Tipo | Par | Canal | Decisões |
|---|---|---|---|---|---|---|---|
| pequeno | 41 de 50 = **82%** (69–90) | 5 de 50 = **10%** (4–21) | 2 de 100 = **2%** (1–7), as 2 no forte | 28,0% | **29,9%** | 38,8% | 2,73 |
| médio | 46 de 50 = **92%** (81–97) | 8 de 50 = **16%** (8–29) | 0 de 100 (0–4) | 27,1% | 22,4% | 31,2% | 2,73 |
| grande | 23 de 50 = **46%** (33–60) | 2 de 50 = **4%** (1–13) | 0 de 100 (0–4) | 27,9% | 18,6% | **40,6%** | 2,78 |
| metrópole | 21 de 50 = **42%** (29–56) | 5 de 50 = **10%** (4–21) | 0 de 100 (0–4) | 36,0% | 17,7% | 31,6% | 2,88 |

Contas: "por fora" acima de 30%, 36,4%; limpas, 0,00%.

### Alavanca B: reconhecimento inicial herdado da força do partido

- **O que muda:** parâmetro novo (só no experimento) `party_recognition_inherited` = **20** em data/campaign/config.json. Em `RaceSetup.Create`, o reconhecimento inicial do jogador ganha 20 × escala do porte × (força do partido dele / força do partido mais forte da cidade): no partido mais forte, +32 pontos no pequeno, +20 no médio, +12 no grande e +7 na metrópole; num partido sorteado, uma fração disso. Depois decai como o resto (0,6 por semana onde ele não age).

| Porte | Mais forte | Sorteado | Sem campanha | Tipo | Par | Canal | Decisões |
|---|---|---|---|---|---|---|---|
| pequeno | 46 de 50 = **92%** (81–97) | 9 de 50 = **18%** (10–31) | **7 de 100 = 7%** (3–14), as 7 no forte (14% dele) | 30,4% | **28,2%** | 35,0% | 2,78 |
| médio | 45 de 50 = **90%** (79–96) | 18 de 50 = **36%** (24–50) | 0 de 100 (0–4) | 25,7% | 21,7% | 32,5% | 2,78 |
| grande | 27 de 50 = **54%** (40–67) | 4 de 50 = **8%** (3–19) | 0 de 100 (0–4) | 27,2% | 18,3% | 37,2% | 2,89 |
| metrópole | 26 de 50 = **52%** (39–65) | 8 de 50 = **16%** (8–29) | 0 de 100 (0–4) | 35,0% | 17,6% | 32,1% | 2,93 |

Contas: "por fora" acima de 30%, 37,5%; limpas, 0,00%. O jogador parado no partido forte do pequeno termina com 25% de reconhecimento (12% na base) e 10% do quociente, e os anônimos do partido ficam abaixo dele.

### Alavanca C: candidatos anônimos repartidos pelo tamanho da lista

- **O que muda:** parâmetro novo (só no experimento) `bulk_split_by_multiplicity` = **true** em data/campaign/config.json. Em `SlateLists` (RivalEngine.cs), o coletivo é repartido em tantos anônimos quantos ele representa (`Multiplicity`, vagas + 1 − nomeados, no mínimo os 8 de hoje), em vez de 8 sempre. O total do partido não muda; só a votação de cada anônimo. Quase não mexe no pequeno (lista de 10) e cresce com o porte.

| Porte | Mais forte | Sorteado | Sem campanha | Tipo | Par | Canal | Decisões |
|---|---|---|---|---|---|---|---|
| pequeno | 38 de 50 = **76%** (63–86) | 9 de 50 = **18%** (10–31) | 0 de 100 (0–4) | 27,9% | **29,5%** | 35,0% | 2,72 |
| médio | 44 de 50 = **88%** (76–94) | 18 de 50 = **36%** (24–50) | 0 de 100 (0–4) | 26,3% | 22,2% | 29,1% | 2,75 |
| grande | 30 de 50 = **60%** (46–72) | 5 de 50 = **10%** (4–21) | 0 de 100 (0–4) | 27,5% | 18,7% | 36,7% | 2,85 |
| metrópole | 29 de 50 = **58%** (44–71) | 10 de 50 = **20%** (11–33) | 0 de 100 (0–4) | 36,3% | 17,7% | 30,0% | 2,93 |

Contas: "por fora" acima de 30%, 37,9%; limpas, 0,00%. No pequeno e no médio é praticamente a base (serve de controle do ruído entre rodadas de 100 e de 200).

### Alavanca D: reconhecimento herdado contra empenho dos rivais (o par sugerido pelo autor)

- **O que muda:** a alavanca B (`party_recognition_inherited` = 20) e, junto, `npc_effort_by_porte` × 1,2 em todos os portes: de 0,95 / 1,4 / 3,5 / 4,4 para **1,14 / 1,68 / 4,2 / 5,28**. A ideia: o empenho derruba os dois, e o reconhecimento herdado devolve só ao partido forte.

| Porte | Mais forte | Sorteado | Sem campanha | Tipo | Par | Canal | Decisões |
|---|---|---|---|---|---|---|---|
| pequeno | 45 de 50 = **90%** (79–96) | 8 de 50 = **16%** (8–29) | **3 de 100 = 3%** (1–8), as 3 no forte | 30,2% | **28,1%** | 37,2% | 2,76 |
| médio | 45 de 50 = **90%** (79–96) | 13 de 50 = **26%** (16–40) | 0 de 100 (0–4) | 24,8% | 21,2% | 32,5% | 2,75 |
| grande | 13 de 50 = **26%** (16–40) | 0 de 50 = **0%** (0–7) | 0 de 100 (0–4) | 27,6% | 18,4% | 37,7% | 2,81 |
| metrópole | 10 de 50 = **20%** (11–33) | 2 de 50 = **4%** (1–13) | 0 de 100 (0–4) | 36,6% | 17,2% | 35,2% | 2,85 |

Contas: "por fora" acima de 30%, 34,6%; limpas, 0,00%. No grande e na metrópole, os derrotados do partido forte perdem para anônimos da própria chapa (3,8 de 4,0 e 5,1 de 5,5 cadeiras): o empenho a mais fortalece o coletivo, e o reconhecimento herdado (+12 e +7 pontos nesses portes) não compensa.

### Resumo das alavancas

Eleitos no partido mais forte / no sorteado / sem campanha, em %. Base com 200 por porte; alavancas com 100.

| Porte | Régua (D-026) | Base | A: partido 3,0 | B: herdado 20 | C: anônimos pela lista | D: herdado 20 + empenho × 1,2 |
|---|---|---|---|---|---|---|
| pequeno | 96 / 13 / 0 | 73 / 19 / 0 | 82 / 10 / 2 | 92 / 18 / **7** | 76 / 18 / 0 | 90 / 16 / **3** |
| médio | 96 / 13 / 0 | 84 / 39 / 0 | 92 / 16 / 0 | 90 / 36 / 0 | 88 / 36 / 0 | 90 / 26 / 0 |
| grande | 67 / 8 / 0 | 46 / 11 / 0 | 46 / 4 / 0 | 54 / 8 / 0 | 60 / 10 / 0 | **26** / 0 / 0 |
| metrópole | 58 / 0 / 0 | 49 / 12 / 0 | 42 / 10 / 0 | 52 / 16 / 0 | 58 / 20 / 0 | **20** / 4 / 0 |
| critérios fora | — | par no pequeno | par no pequeno; canal no grande (40,6%) | par no pequeno | par no pequeno | par no pequeno |

O par (tipo, zona) do pequeno fica fora em todas (28% a 30%): nenhuma destas alavancas mexe nele, como esperado (é a entrevista no veículo de maior alcance, P-26). Tipo, canal (com a exceção de A no grande), decisões com troca e contas continuam dentro em todas.

## Leitura

- **A (peso do partido)** é a única que afasta forte e sorteado no pequeno e no médio sem tocar na campanha parada (a não ser 2 de 100 no pequeno); no grande e na metrópole não sobe o forte, porque as cadeiras a mais da chapa vão para os anônimos dela, e tira a calibração da D-015 do lugar e empurra o canal do grande para 40,6%.
- **B (reconhecimento herdado)** sobe o forte em todos os portes e quase não mexe no sorteado, mas o jogador parado do partido forte no pequeno passa a se eleger em 14% das vezes (7 de 100 no total): quebra o "sem campanha, nunca".
- **C (anônimos pelo tamanho da lista)** só age onde o coletivo representa mais de 8 candidatos: sobe o forte no grande e na metrópole (para 60% e 58%) sem mexer na campanha parada, mas sobe junto o sorteado da metrópole (20%), que também perde para anônimos; no pequeno e no médio não faz nada.
- **D (herdado contra empenho)** separa bem no pequeno e no médio (forte 90%, sorteado 16% e 26%) e reduz o efeito de B na campanha parada (3 de 100), mas o mesmo × 1,2 no empenho derruba o forte do grande e da metrópole para 26% e 20%: o empenho pesa muito mais nos portes grandes.
- Nenhuma, sozinha, alcança a régua nos quatro portes; o que trava o forte no grande e na metrópole é a disputa com os anônimos da própria chapa (o caminho de C), e o que trava o sorteado no médio é o voto do jogador num partido fraco (o caminho de A).

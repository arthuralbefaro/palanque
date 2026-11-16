# Auditoria da fatia vertical (Fases 3 a 5)

> Data: 2026-11-15. Base: código em `core/Planalto.Core` e `src/UI` no commit `62440c5`, e os arquivos de `data/`.
> Motivo: o autor jogou a fatia vertical e reprovou: "pouca coisa pra fazer, confuso, feio, muito raso, pouca variedade de ações e de gestão", "extremamente chato e saturável".
> Os princípios citados (a–i) são os da seção 17.1 do GDD.

## 0. Diagnóstico de partida: o que o código confirma e o que corrige

| Afirmação | Veredito | Evidência |
|---|---|---|
| O ciclo é clicar nos mesmos 6 botões, ver um número subir e avançar a semana | **Confirmado** | `data/campaign/actions.json` tem 6 ações; `HubScreen.BuildAgenda` cria um botão por ação; `CampaignEngine.Execute` soma efeitos fixos |
| A melhor ação não depende da situação | **Confirmado, e pior do que parecia** | Os efeitos de cada ação são constantes do JSON, multiplicados só pelo porte. Não entram bairro, público, notícia, rival nem semana. Nada impede repetir a mesma ação: `ActionsThisWeek` é gravado e nunca lido. Redes sociais rende 3 de reconhecimento por 15 de energia (0,20 por ponto de energia), contra 0,15 do comício e da entrevista, 0,10 do corpo a corpo e 0,075 da visita. Seis cliques em "Redes sociais" gastam 90 de energia e são a melhor semana possível em qualquer cidade e em qualquer momento |
| Os resultados dizem só "feito" | **Corrigido em parte** | Desde `62440c5`, `EffectSummary` mostra o efeito medido depois do clique ("+3 de reconhecimento, −R$ 300"). Continua faltando a previsão antes do clique, o "onde" e o "para quem", e a explicação da causa |
| Os rivais não reagem ao jogador | **Confirmado** | `CampaignEngine.NpcTurn` sorteia ações com `Rng.Pick(actions)` para cada rival. O rival não lê pesquisa, não lê o que o jogador fez, não tem alvo nem plano |
| As consequências não ficam | **Confirmado, com uma ressalva** | Reconhecimento, imagem e apoio decaem toda semana (`recognition_decay_per_week`, `image_decay_per_week`, `support_decay_per_week`), então o efeito de qualquer ação some sozinho. Os "esqueletos no armário" são gravados (`EffectApplier`) e nunca lidos: nenhum evento usa `needs_skeleton` e não há sorteio de exposição. Ressalva: existem cadeias de evento agendadas (ex.: doação por fora → perguntas do jornal 5 semanas depois) e o jogador leva reconhecimento, imagem e apoios para o mandato |
| O mapa está escondido numa tela separada | **Confirmado** | Botão "Mapa" troca de cena (`Game.Scenes.Map`). O mapa mostra os municípios do estado, não a cidade em disputa; numa campanha municipal ele não informa nenhuma decisão |
| O mundo não se mexe sozinho | **Confirmado** | Entre dois cliques em "Avançar semana" só acontecem: sorteio de ações dos rivais (invisível), decaimento, uma pesquisa pública a cada 3 semanas e 0,6 evento por semana em média. Não há economia, clima, obra, boato nem notícia que não seja sobre o jogador |

## 1. Sistema por sistema

### 1.1 As 6 ações de campanha
**O que o jogador faz em cada clique:** escolhe uma linha entre seis ("Comício · 40 energia · R$ 6.000"). O clique desconta energia e dinheiro, soma reconhecimento na cidade inteira e, em três ações, apoio num grupo de eleitores. "Entrevista" sorteia sucesso pela Oratória; "Redes" e "Comício" sorteiam um risco. Uma linha de texto entra em "Resultados".

**Princípios feridos:**
- (a) A situação manda: o efeito é igual em qualquer semana, lugar e cenário. Existe uma ordem fixa de eficiência (redes > comício = entrevista > corpo a corpo > visita).
- (g) Repetir cansa: o único freio é o retorno decrescente do reconhecimento geral (`ganho × (1 − atual/100)`), que vale igual para todas as ações e por isso não muda a ordem entre elas. O apoio por grupo só para no teto de 1.
- (f) O mundo explica: não há previsão antes do clique. A descrição diz "grande ganho de reconhecimento", sem número, sem incerteza, sem dizer quem é atingido.
- (e) Troca real: a única troca é energia e dinheiro contra reconhecimento. Nenhuma ação tira algo de alguém ou fecha uma porta.

**Aproveita:** `Effects` e `EffectApplier` (modelo de efeito, testes de atributo, riscos sorteados), o custo por porte, o teste de atributo com chance entre 5% e 95%, o `VoteModel`, que já aceita apoio por grupo e posição por tema.
**Descarta:** a lista plana de 6 ações de efeito fixo, a energia semanal de 100 pontos como único orçamento, o reconhecimento como um número só para a cidade inteira.

### 1.2 Agenda e energia
**O que o jogador faz:** nada além de gastar 100 pontos. Não existe agenda: as ações acontecem no instante do clique, sem dia, sem lugar, sem ordem. A energia volta a 100 toda semana e não acumula.

**Princípios feridos:** (a) não há por que guardar ou antecipar nada; (e) não há conflito de horário, de deslocamento nem de convite; (i) a palavra "agenda" promete um calendário que a tela não tem.

**Aproveita:** o conceito de orçamento de tempo do candidato, o Estresse (hoje só sobe 2 por semana de campanha e não tem efeito).
**Descarta:** os 100 pontos de energia; entram turnos do dia (seção 17.6).

### 1.3 Pesquisas
**O que o jogador faz:** clica em "Encomendar: Pesquisa interna (R$ X)" e recebe uma tabela com até 8 nomes e percentuais. A cada 3 semanas de campanha sai uma pública sozinha.

**Princípios feridos:** (f) a pesquisa não diz onde nem com quem o jogador vai bem ou mal: o Core calcula a intenção por segmento (`VoteProjection.Segments`) e a tela mostra só o total; (a) como nenhuma ação tem alvo territorial e só uma tem alvo de público, a pesquisa não muda a decisão seguinte; (h) nenhum objetivo sai dela.

**Aproveita:** tudo de `Polling` (amostra multinomial, margem de erro, viés de casa, indecisos, pesquisa interna) e os institutos fictícios.
**Muda:** recorte por zona e por grupo, série histórica, e a pesquisa passa a gerar metas e reações dos rivais.

### 1.4 Eventos
**O que o jogador faz:** lê uma janela com título e um parágrafo e clica em uma de 2 a 4 escolhas. Lê uma frase de resultado.

**Princípios feridos:**
- (b) e (c): os 32 eventos (bastidores 12, mídia 9, rua 11) são sorteados por peso e raridade; nenhum nasce de algo que o jogador ou um rival fez. O rival citado é sorteado entre os 4 mais conhecidos.
- (d): só há uma cadeia longa; as escolhas mexem em números que decaem. Os esqueletos nunca voltam.
- (g): com 0,6 evento por semana e 40 semanas de campanha, o jogador vê cerca de 24 sorteios de um conjunto de 32; numa segunda campanha, quase tudo se repete.
- (f): a escolha não mostra o que está em jogo antes do clique.

**Aproveita:** todo o `EventEngine` (condições, raridade, cooldown, únicos, cadeias, variáveis, requisitos visíveis, teste de atributo), o `EventLint` e os 32 textos.
**Muda:** os eventos passam a ser disparados também por fatos da simulação (GDD 15.4, ainda não feito) e a citar pessoas e zonas reais do save.

### 1.5 Rivais
**O que o jogador faz:** nada. A coluna "Principais rivais" lista 6 retratos com nome, partido e reconhecimento. Não há clique possível sobre um rival.

**Princípios feridos:** (c) têm nome e retrato, mas não têm interesse, plano, reduto, temperamento nem memória; (b) agem, mas a ação é um sorteio invisível; (h) não dá para escolher um rival a superar porque não se sabe onde ele é forte.

**Aproveita:** `RaceSetup` (6 partidos, 10 rivais nomeados e persistidos, candidato coletivo com multiplicidade, incumbentes), o `Drive`, o empenho por porte, o gerador de pessoas e retratos.
**Descarta:** `NpcTurn` como sorteio de ações.

### 1.6 Hub
**O que o jogador vê:** uma barra de texto com 7 números, três colunas de texto (agenda à esquerda, manchetes e pesquisas no meio, rivais à direita) e dois botões embaixo. Não há imagem do lugar em que a campanha acontece.

**Princípios feridos:** (i) tudo tem o mesmo peso visual; o que pede atenção (evento, debate, falta de dinheiro) aparece como mais uma linha; (b) e (f) as "manchetes" são o histórico dos eventos do próprio jogador; (h) não há painel de objetivos.

**Aproveita:** `Game.Execute`, o registro de resultados com efeito medido, o `EventDialog`, o `PortraitView` com cache, save e autosave.
**Descarta:** o layout de três colunas; entra a tela central (seção 17.3).

### 1.7 Mandato de vereador
**O que o jogador faz:** por semana, marca Sim, Não ou Abstenção em 1 a 3 projetos da pauta; clica em até 7 ações de gabinete (tribuna, gabinete itinerante, articular, fiscalizar, protocolar projeto, pedir urgência, indicar emenda) com a mesma energia de 100; clica em "Avançar semana". São 208 semanas.

**Princípios feridos:**
- (g): 36 modelos de projeto para 4 anos; a pauta repete o mesmo texto várias vezes no mandato.
- (d): a emenda dá +0,06 de apoio num grupo e some pelo decaimento; não existe obra, lugar nem beneficiário. "Fiscalizar" dá +2 de reconhecimento e −6 de relação com o prefeito, sem objeto fiscalizado.
- (c): os vereadores têm nome, partido, posição e relação, mas não pedem nada e não lembram de nada além do número de relação, que volta sozinho a zero.
- (b): não há eventos de mandato (registrado como pendência na D-027).
- (e): a troca real existe em um ponto, e ele funciona: votar contra a orientação custa fidelidade e pode levar à expulsão.

**Aproveita:** `ChamberSetup`, `ChamberVote` (logito por afinidade, orientação, disciplina, governo e oposição, relação), quóruns, fidelidade com advertência, sanção e expulsão, comissões, tramitação, os 36 projetos, `CareerFlow`, `PartyAffiliation`.
**Descarta:** as 7 ações de efeito fixo; emenda sem lugar.

### 1.8 Prefeitura
**O que o jogador faz:** move 8 controles de orçamento que somam 100% e clica em "Aplicar"; clica em "Decretar" ou "Enviar à Câmara" em até 17 políticas; clica em "+" e "−" para dar secretarias a partidos; usa 4 ações de gabinete; responde a uma crise a cada 33 semanas em média (3% por semana), com 3 escolhas.

**Princípios feridos:**
- (c): secretaria é um contador por partido, sem secretário. Ninguém executa, erra, pede ou trai.
- (a) e (e): existe uma configuração de orçamento que maximiza a aprovação e, achada, não precisa mais ser tocada. Os indicadores caminham 3,5% por semana até o alvo e param.
- (d): "Inaugurar obra" rende +2,5 de reconhecimento sem existir obra. Política revogada devolve metade da reação. Crise resolvida vira uma linha de notícia.
- (b): nada acontece na cidade além das 7 crises.
- (i): 7 indicadores, 8 áreas, 17 políticas e 8 secretarias na mesma página de texto.

**Aproveita:** `ExecutiveEngine` quase inteiro: orçamento por área com mínimos legais e Tribunal de Contas, indicadores com atraso, aprovação por grupo ponderada pelo eleitorado, políticas por decreto ou lei com votação pela Câmara, coalizão, crises sazonais com choque.
**Muda:** secretarias viram pessoas; obras, licitações, servidores e convênios entram como objetos com lugar e prazo (seção 17.15).

### 1.9 Debate
**O que o jogador faz:** em 3 rodadas, escolhe 1 de 3 cartas; vê o placar. Dois debates por campanha de prefeito, só em cidade média, grande ou metrópole.

**Princípios feridos:** (a) em parte cumprido: Proposta e Dado rendem mais em tema importante e Defesa só vale se houver ataque; (d) o placar vira imagem (× 0,012), que decai 0,02 por semana, então o debate de 5 semanas antes já sumiu no dia da eleição; (c) os rivais têm estilo, mas não lembram do ataque; (f) a carta mostra o atributo testado, não a chance.

**Aproveita:** `DebateEngine` inteiro. É o sistema atual mais próximo do desenho novo.
**Muda:** o que foi dito no debate entra na memória dos rivais e na plataforma (posição assumida ao vivo conta como compromisso), e vira pauta da mídia local.

## 2. As 5 causas principais do tédio, em ordem de gravidade

1. **Não há decisão, só gasto.** O efeito de cada ação é uma constante que não olha para lugar, público, momento nem adversário, e nada limita a repetição. Existe uma semana ótima (seis vezes "Redes sociais") que vale do primeiro ao último dia, em qualquer cidade. Depois de descoberta, o jogo vira repetição mecânica. Fere (a), (e) e (g). *Correção: 17.5 Território, 17.6 Agenda, 17.8 Verba por canal, 17.9 Plataforma.*
2. **O mundo é uma planilha parada.** Entre dois cliques do jogador só há sorteios invisíveis e decaimento. Rivais não têm plano, a cidade não tem lugares, não há economia, clima, obra nem boato. O jogador nunca é surpreendido por algo que tenha causa. Fere (b). *Correção: 17.4 Tempo, 17.5 Território, 17.11 Rivais, 17.13 Mídia local.*
3. **Ninguém tem rosto, interesse ou memória.** Os rivais são um retrato e um número. Eleitores são 240 linhas que o jogador nunca vê. Vereadores e secretarias são contadores. Não há quem peça, cobre, ajude ou se vingue, então não há história para contar depois da partida. Fere (c). *Correção: 17.7 Equipe, 17.10 Lideranças, 17.11 Rivais, 17.14 e 17.15 (assessores, secretários, vereadores com pedido).*
4. **Nada fica.** Todo ganho decai por semana; promessas não existem; esqueletos são gravados e nunca lidos; emenda e obra não têm lugar; o debate evapora em duas semanas. A semana 20 é a semana 2 com números um pouco maiores. Fere (d) e (h). *Correção: Livro de compromissos (17.2), 17.9 Plataforma, 17.12 Doadores e contas, 17.14 e 17.15 (obras no mapa, base que cobra).*
5. **A tela não mostra o jogo.** Três colunas de texto do mesmo tamanho, sem mapa do lugar em disputa, sem hierarquia, sem previsão antes do clique e sem dizer onde e para quem o efeito aconteceu. O que o Core já calcula bem (intenção por segmento, margem de erro, voto da Câmara) não chega ao jogador. Fere (f) e (i). *Correção: 17.3 Tela central, 17.4 Tempo e a seção "Interface" do ART_GUIDE.*

## 3. O que o Core já tem de bom e fica

- Regras eleitorais (`ElectionRules`), modelo de voto por segmento (`VoteModel`), pesquisas (`Polling`), montagem da disputa (`RaceSetup`), apuração com parciais.
- Motor de eventos com cadeias e `EventLint`.
- Câmara (`ChamberVote`, fidelidade, quóruns, tramitação) e prefeitura (orçamento, indicadores, aprovação por grupo, políticas, crises).
- Debate.
- Carreira (`CareerFlow`, `PartyAffiliation`, fundação de partido), save, retratos, dados de todos os municípios.

O problema da fatia vertical não está nas regras, que são sólidas e testadas. Está na camada de decisão entre o jogador e essas regras, que hoje é uma lista de botões de efeito fixo.

# Progresso

## Relatório para o autor (atualizado a cada parte)

**Estado em 2026-11-16, depois da R2a.** Este bloco é reescrito ao fim de cada parte.

- **Tags prontas:** `fase-r1`, `fase-rd` e `fase-r2a`, as três com pendências.
- **Por qual tag começar a testar:** `fase-r2a`, com `docs/ROTEIRO_TESTE_R2A.md` (20 minutos). É a primeira em que o jogo muda de verdade: mapa em "Minha força", agenda por dia e turno, lideranças com pedido e o Livro. Os roteiros da R1 e da RD continuam valendo para o que é delas (tela, tempo, tooltips; conferência de números).
- **Em andamento:** R2b (equipe, verba por canal, plataforma, objetivos).
- **Pendências que precisam de você** (docs/PENDENCIAS.md):
  - **P-11:** eleitores por zona, onde se vota ou onde se mora (escolhi onde se mora).
  - **P-12:** parâmetros de jogo novos da RD (mortalidade, faixas plausíveis, 300 nascidos vivos, homicídios só por agressão).
  - **P-13:** termos de uso das fontes que não são do IBGE.
  - **P-15:** o princípio a não passa em cidade grande (41,4%) e metrópole (59,8%): a caminhada ganha por margem mínima quase toda semana. Decidir se o critério vale como "opção de maior ganho da semana" (como medi) e se o retorno de abrir zona nova deve cair.
  - **P-19:** no mapa "Minha força", faixas por quintil (ART_GUIDE) ou por intervalo de valor (o relato 1 pede o mapa "quase todo cinza" no começo).
  - **P-10 e P-14:** o que ficou de fora da RD (votação de 2024 por zona; transferências e CAGED).
- **Decisões conservadoras tomadas sozinho, a revisar** (DECISOES D-038 e D-039):
  1. Reunião com apoiadores só onde há liderança conhecida daquele público na zona; apoio de conversa com teto.
  2. Evento da cidade divide o cansaço com a caminhada; o bônus de 50% vale só para o público do evento.
  3. Alcance de um turno numa zona limitado a 10 vezes a base (um turno a pé não cobre uma zona de metrópole como cobre a de cidade pequena).
  4. Pedido de dinheiro de liderança, se aceito, é gasto irregular e vira esqueleto; o jogo avisa e não julga.
  5. Dificuldade: eficiência dos rivais em 0,5 e empenho 5,0 na metrópole, para manter os dois extremos da D-026.
  6. Energia dos eventos antigos convertida em estresse (4 para 1).
  7. As da RD: declarações implausíveis descartadas; Brasília pelo ente estadual; RAIS de estabelecimentos; marca "(artigo a conferir)" em dois verbetes a mais.

## R2a — Território, agenda, lideranças e Livro de compromissos (2026-11-16, com pendências)

**O que entrou**
- **Território na simulação:** presença de cada candidato por zona (0 a 100), com decaimento de 0,6 por semana nas zonas sem compromisso e 15% de transbordo para as vizinhas; os 240 segmentos repartidos pelas zonas (idade, renda, religião e escolaridade de cada uma, fechando com o município segmento a segmento); voto calculado zona a zona, e o da cidade é a soma; demanda da zona em unidade real; reconhecimento = média das presenças.
- **Agenda com lugar e dia:** 9 tipos de compromisso em grade de 7 dias por 3 turnos, com previsão antes de marcar (faixa, fatores e cansaço), resultado explicado depois, chuva prevista, eventos da cidade que só existem no dia (os religiosos na proporção do Censo do município) e convites com dia e turno fixos. Saem a energia semanal e as 6 ações.
- **Pessoa com memória e Livro:** um modelo de pessoa com interesses, temperamento e até 20 lembranças datadas (as de peso 3 não se apagam); Livro com promessas, dívidas, posições e esqueletos, que atravessa a eleição e o save.
- **Lideranças:** de 6 a 30 por cidade, com zona, público, influência, interesse e pedido; visitar revela o pedido; aceitar, negociar, ouvir sem se comprometer ou recusar gravam lembrança; apoio por zona; rivalidades; deriva sem contato; rivais que as procuram e aparecem no feed.
- **BalanceSim:** robô que marca o compromisso de maior ganho previsto de votos, com relatório por porte (`docs/balance/r2a.md`).
- **Interface:** área Agenda, modo "Minha força", marcadores no mapa, Ficha da zona, área Pessoas com Ficha da pessoa, cartão do pedido de liderança com previsão e "quem vai lembrar", resultado explicado no feed, Livro na Enciclopédia, 12 verbetes novos, pausas novas e dicas de primeira vez.

**Números dos testes**
- 581 testes no Core (eram 496 na RD); 76 no importador; DataValidator limpo; build do Godot limpo.
- 15 alvos de fumaça com janela, todos passando: os 12 anteriores mais `agenda`, `zona` e `lideranca`.
- Capturas em 1366×768 e 1920×1080 em `docs/referencias/r2a/` (24 arquivos), examinadas em duas rodadas de correção.
- Princípio g (cansaço): o segundo compromisso igual rende 62% e o terceiro, 45%; a previsão mostra o fator.
- Princípio f: em 1.000 execuções, 100% dos resultados dentro da faixa prevista.
- Campanha inteira jogada sozinha em 3 sementes por porte, com save e load no meio, terminando igual à sem salvar.

**BalanceSim** (48 campanhas por porte em 8 cidades; critério: 40% por tipo, 25% por par)

| Porte | Tipo com a opção de maior ganho da semana | Par (tipo, zona) mais repetido | Eleito no partido mais forte | Eleito em partido sorteado |
|---|---|---|---|---|
| Pequeno | 30,2% | 22,6% | 20 de 24 | 5 de 24 |
| Médio | 34,1% | 15,5% | 24 de 24 | 5 de 24 |
| Grande | **41,4%** (fora) | 12,6% | 24 de 24 | 10 de 24 |
| Metrópole | **59,8%** (fora) | 11,5% | 16 de 24 | 2 de 24 |

O critério passa em pequeno e médio e não passa em grande e metrópole (P-15). Não rodei as 200 campanhas por porte do critério: foram 48, por tempo de máquina.

**As 5 coisas de que menos gostei no meu próprio trabalho**
1. **O princípio a não fecha nas cidades grandes.** O cansaço empata os tipos, mas a caminhada em zona nova ganha por um fio quase toda semana. Na prática o robô ainda caminha em 49% a 62% dos turnos nessas cidades.
2. **Dinheiro mal resolvido.** Nas cidades médias e maiores o robô passa de um terço a metade dos turnos arrecadando (P-18).
3. **O mundo ainda quase não se mexe sozinho.** Os rivais só "fizeram campanha em X"; não há mídia, tema em alta nem rival com plano (R3).
4. **Uma liderança só faz um pedido,** e as promessas de obra não têm como ser cumpridas antes do mandato; "posições assumidas" no Livro fica vazio até a R2b.
5. **Marcar a semana dá trabalho:** 3 ou mais cliques por compromisso, sem repetir semana; e o estresse chega a 100 sem consequência visível.

**Autocrítica de imersão (GDD 17.21)**
- **Relato 1, já acontece:** o mapa abre em "Minha força" com a zona natal acima das outras; a feira de sábado existe como evento da cidade; a visita traz o pedido, e "Veio, ouviu e não se comprometeu" (+1) fica na ficha; rivais aparecem no feed e no mapa sem o jogador agir.
- **Relato 1, falta:** os três objetivos, o coordenador a contratar e o primo preterido (R2b); a rádio, o tema em alta e o anúncio do prefeito (R3); o rival no "culto de domingo", com ficha; o mapa "quase todo cinza".
- **Relato 4, já acontece:** presença desigual por zona; o aviso de cansaço antes de confirmar, com ordinal e percentual; aliada com lembranças; liderança que muda de lado, com pausa; promessa no Livro com prazo.
- **Relato 4, falta:** segundo pedido de quem já é aliado; posição pública com custo (R2b); briga de equipe e financiamento coletivo (R2b, R3); o vereador que desiste e abre a base (R4); objetivos que trocam.
- Das seis coisas que deveriam mudar a melhor jogada entre a semana 2 e a semana 20, a R2a entrega o mapa, o cansaço e as pessoas (só lideranças), parte do Livro e parte do mundo.

## RD — Dados reais de partida (2026-11-16, com pendências)

**O que entrou**
- **Indicadores reais** de todos os municípios em `data/indicadores_reais/` (6,3 MB): PIB e valor adicionado por setor (IBGE, 2023), emprego formal (RAIS 2025), finanças (SICONFI: receita corrente, despesa por função, RCL e despesa com pessoal), IDEB dos anos iniciais (INEP 2025, com a série), mortalidade infantil, homicídios e leitos (SIM, SINASC e CNES), saneamento (Censo 2022). Contexto das UFs em `_ufs.json` e economia nacional em `data/economy/start.json`.
- **Lacunas registradas:** 2.681, cada uma com a regra aplicada (2.323 de mortalidade infantil pela região imediata; 251 de IDEB; 70 de finanças; 35 de despesa com pessoal; 1 de PIB e 1 de saneamento, do município criado depois do Censo).
- **Receita real na prefeitura:** `ExecutiveEngine.AnnualRevenue` usa a receita corrente declarada; sem dado, vale a estimativa da D-031.
- **Mortalidade e população:** tábuas por UF e Brasil em `data/demography/` (a probabilidade recomposta de cada faixa é igual à publicada) e projeção municipal reescalada pela projeção de cada UF.
- **Zonas:** eleitores pelos locais de votação do TSE em 5.361 municípios e religião e escolaridade pelas áreas de ponderação em 16.592 zonas; teto de 20 MB em `data/zonas/` (16,8 MB).
- **Importador:** leitor de .xlsx, leitor de .dbc do DATASUS (descompressão "implode" portada do zlib/contrib/blast), download por FTP, baixador retomável do SICONFI, tudo sem pacote novo. Brutos só no cache (cerca de 9 GB).
- **Na tela:** F1 → Este mundo mostra os números oficiais de partida do município em foco, com unidade, fonte, ano e a marca de substituição.

**Números dos testes**
- 496 testes no Core (eram 455), 76 no importador (eram 28); DataValidator limpo (108 arquivos); build do Godot limpo.
- 12 alvos de fumaça com janela, todos passando. A RD não acrescenta alvo.
- Finanças: declaradas em 5.504 de 5.571 municípios; 56 pelo exercício anterior; 11 estimadas pela mediana do porte na UF.
- Totais por UF: a soma do PIB dos municípios confere com o total da UF (diferença só de arredondamento) e a dos domicílios é igual, sem tolerância.
- Conferência de leitura: tábua do Brasil aos 40, 60 e 80 anos igual aos seis valores do GDD; Selic 13,75%, câmbio R$ 5,6518 e dívida de 82,95% e 70,00% do PIB, como no GDD.

**BalanceSim:** não se aplica à RD (só dados).

**As 5 coisas de que menos gostei no meu próprio trabalho**
1. **Os números ainda não mudam o jogo.** Só a receita da prefeitura usa o dado real; os indicadores continuam de 0 a 100 até a R4.
2. **Eleitores por zona saíram do jeito aprovado e não servem ao modelo** (P-11): local de votação não é moradia. Tive de escolher sozinho o que entra no voto.
3. **Três itens ficaram de fora:** votação de 2024 por zona (P-10), transferências e CAGED (P-14).
4. **Mortalidade infantil pela região imediata em 2.323 municípios** (42%): a regra dos 300 nascidos vivos é minha e esconde a taxa própria de muita cidade pequena.
5. **Não há captura de tela desta parte** e o cartão da Enciclopédia é uma lista corrida de números, sem a ficha de município que o roteiro antigo previa.

**Autocrítica de imersão (GDD 17.21):** a RD não acrescenta nada aos relatos; ela dá a base do relato 3 (folha em % da receita corrente líquida, receita real, obras) que a R4 vai usar.

## Relatório da manhã (corujão da Fase R, 2026-11-16) — R1

**Partes concluídas:** só a **R1**, tag local `fase-r1`, **com pendências** (docs/PENDENCIAS.md). RD, R2a, R2b, R3 e R4 não começaram. Parei de propósito: preferi entregar a R1 testada, olhada em captura e corrigida em duas rodadas, a abrir a RD pela metade.

**O que jogar, nesta ordem:**
1. `docs/ROTEIRO_TESTE_R1.md` (20 minutos). Comece pela criação com 17 anos e o material de campanha novo; depois a tela central numa cidade média que você conheça e numa cidade pequena.
2. Se sobrar tempo, jogue uma campanha inteira em 4x e anote onde o jogo parou sozinho e se parou demais.

**As 5 coisas de que menos gostei no meu próprio trabalho:**
1. **O jogo ainda é raso.** A R1 troca a tela, mas as 6 ações continuam com efeito igual em qualquer zona; "Redes sociais" segue sendo a melhor ação quase sempre. O mapa informa, mas ainda não muda nenhuma decisão. Isso só muda na R2a.
2. **Mandato e prefeitura ficaram nas telas antigas** (P-01). Quem chegar à posse vai sentir o salto de qualidade para trás.
3. **Mapa de cidade pequena** (P-09): as zonas urbanas são pequenas perto da rural, os rótulos se sobrepõem e, quando um partido lidera tudo, o mapa fica de uma cor só.
4. **A intenção por zona é uma estimativa** (P-05): a da cidade repesada por idade e situação urbana ou rural. É honesta na tela ("estimativa por zona"), mas não é o voto por zona que a R2a vai calcular.
5. **Tooltips em três partes só na tela central e na Enciclopédia** (P-02), e não há tela de Opções para as pausas (P-03).

**Pendências que precisam do autor:** P-04 (artigos de lei citados de memória nos tooltips do material de campanha), P-05 (a estimativa por zona basta até a R2a?), P-06 (termos de uso do IBGE), P-08 (15 MB de atributos de zonas no repositório).

**Testes e números finais:**
- 455 testes no Core (eram 433) e 28 no importador; DataValidator limpo; build do Godot limpo.
- 12 alvos de fumaça com janela, todos passando: `newworld`, `creator`, `creator17` (novo), `picker`, `hub`, `campaign`, `map`, `mandate`, `prefeitura`, `eventos`, `layout`, `explicacao` (novo).
- Zonas: 28.135 em 5.570 municípios, 20,5 MB de contornos (teto de 30 MB) e 15 MB de atributos. A população das zonas bate com o Censo sem tolerância em 5.569 municípios; Boa Esperança do Norte (criado depois do Censo) tem uma zona só. Pela regra de sigilo, 1.423 blocos de perfil (idade ou renda) usam o do município.
- Previsão: em 1.000 execuções das ações, 100% dos resultados caíram na faixa prevista (critério: 80%).
- Tempo: sete dias produzem exatamente o mesmo estado que uma semana do motor antigo.

**Defeito de teste encontrado e corrigido:** o clique simulado dos testes de fumaça usava coordenadas da janela. Com a janela em 1965 px de largura, ele errava o botão "Mapa" da tela nova. Agora usa as coordenadas do viewport, e o teste `eventos` confere o clique num hub novo antes de tudo.

## R1 — Tela central, mapa da cidade e tempo corrente (2026-11-16, com pendências)
- **Idade inicial (D-036):** branch `wip/idade-inicial` trazido com os testes que faltavam: 17 a 45 anos, 18 aferidos em 15/8, profissões com idade mínima, Estudante, família coerente. Corrigi duas falhas do branch: validação que ainda exigia 18 anos e chave "uniao_estavel" que não carregava.
- **Zonas:** modo `zonas` do importador, com Malha de Setores e agregados do Censo 2022 (básico, idade, renda do responsável). Setores agrupados sem misturar urbano e rural; nomes de bairro ou distrito do IBGE, ou nomes do jogo. Contornos com tolerância proporcional ao tamanho de cada zona.
- **Core:** `Clock` (dia, semana no domingo, pausas por `data/time.json`), `Forecaster` (faixa de cada efeito e chance dos desfechos), `ZoneProjection` (intenção por zona, estimativa) e Enciclopédia em `data/encyclopedia/glossary.json`.
- **Tela central:**
  - barra com data, pausa, 1x/2x/4x, fase e quatro números com legenda;
  - trilho com 7 áreas e painel da área;
  - mapa 2D das zonas, com 4 modos, zoom, arraste e ficha da zona;
  - notícias e intenção de voto;
  - cartão de decisão sobre o mapa, com previsão por escolha e "O que mudou";
  - dicas de primeira vez, F1 em qualquer tela, tooltips em três partes;
  - fontes Inter, Oswald e Source Serif 4, e ícones próprios.
- **Criação:** outdoor (proibido) trocado por adesivo, bandeira e peça de redes.

**Autocrítica de imersão (portão 4):** joguei pelo teste de fumaça `campaign` (38 semanas, sem eleição) e pelos testes de autoplay do Core, e reli os quatro relatos do GDD 17.21. Contra o relato 1, faltam:
- o coordenador a contratar e o vínculo com o rival;
- o Seu Anselmo e o pedido dele;
- a Rádio Vale e o tema em alta;
- o rival aparecendo numa zona.

O que a R1 já dá é o palco: a cidade real em zonas, o dia passando, a previsão antes do clique e o motivo de cada bloqueio. A semana 20 ainda é igual à semana 2, porque nada no mapa muda a melhor ação e nenhuma pessoa lembra de nada. O feed só mostra eventos do próprio jogador e pesquisas, então o mundo ainda não "se mexe sozinho" na tela. É exatamente o que R2a (território, agenda, lideranças, Livro) e R3 (rivais e mídia) atacam.


## 1C — Mundo e partidos (2026-11-15)
Famílias, temas, lista de bloqueio (95 itens, não confirmados nas páginas do TSE, nomes contidos também bloqueados), gerador de partidos, WorldSetup com predefinições, travas, validações e cenários salvos, números de candidato.
282 → 292 testes; variedade de nomes 4.105 → 4.064 distintos em 10.000 gerações após a regra de nomes contidos.

## 1D — Cenário Brasil 2026 (2026-11-15)
`dotnet run --project tools/Planalto.DataImporter -- cenario` gera nacional.json e 27 UFs (2,35 MB) do TSE: 30 partidos fictícios (família pela pesquisa de Bolognesi et al. 2025, regra diagonal), Câmara 513, Senado 81, Assembleias 1.059, 5.569 municípios com prefeito, Câmara, força local e alianças de 2024.
Lacunas registradas: proporcionais do AP (usa 2022), 14 prefeituras pendentes, 21 Câmaras de 2020, Placas/PA sem fonte. Fundo e TV calculados no Core pela Lei 9.504; predefinição Brasil 2026 disponível no WorldSetup.

## 1E — Eleitorado e pesquisas (2026-11-15)
240 segmentos por município (zona × idade × renda × religião × escolaridade) do perfil do Censo, com afinidade partidária ajustada por IPF à força local de 2024 (Brasil 2026) ou gerada por tamanho e proximidade; modelo de voto da 5.3 com softmax, brancos/nulos e comparecimento com voto facultativo.
Calibração contra prefeitos de 2024: erro médio 11 pontos, vencedor certo em 72% (D-015). Pesquisas com amostra multinomial, viés de casa simétrico, margem de erro e pesquisa interna; 322 testes.

## Fase 2 — Criação de personagem (2026-11-15)
Appearance no Core (163 peças, 24 tons, pesos regionais do Censo, envelhecimento), arte placeholder SVG gerada por código (tools/Planalto.PlaceholderArt), retrato em camadas tingidas por shader com expressões e cache, NPCs com prenomes do IBGE por UF e década (193 masc., 230 fem.).
Telas Novo Mundo, Criação (5 abas, santinho, outdoor, logotipo, presets, desfazer) e seletor de município; teste de fumaça da UI (src/UI/Debug/UiShot.tscn) passando nos 4 fluxos; 348 testes.

## Fase 3 — Fatia vertical: vereador (2026-11-15)
Campanha completa no Core (RaceSetup com 6 partidos, 10 rivais NPC persistidos e candidatos coletivos; CampaignEngine com agenda, pesquisas, rivais, decaimento e apuração proporcional; EventEngine com 32 eventos e EventLint) e na interface (hub semanal, janela de eventos, urna, noite da apuração, save/load com autosave, véspera da eleição e ironman). Protótipo do mapa 2.5D: modo `geo` do importador gera data/geo/ (6,3 MB, 27 UFs) e a tela extruda os municípios (MG: 853 malhas em cerca de 300 ms, 60 FPS).
Balanceamento D-026 (conjunto de consideração no voto, rivais e coletivos ativos, empenho por porte): partido mais forte eleito em 96% das cidades pequenas e 58% das metrópoles; sem campanha, nunca. A aba Partido mostra a força local de cada partido. Roteiro em docs/ROTEIRO_TESTE_FASE3.md; 389 testes no Core, 28 no importador, smoke da UI em 6 fluxos.

## Fase 4 — Mandato legislativo (2026-11-15)
Mandato de vereador no Core (ChamberSetup e LegislatureEngine: Câmara com NPCs, base do prefeito, lideranças, comissões, pauta semanal, voto NPC, quóruns, fidelidade com advertência, sanção e expulsão, relações, capital político, emendas e projetos próprios), transição da carreira (CareerFlow: posse, reeleição com incumbentes do mandato, entressafra; PartyAffiliation: prazo de filiação e janela partidária) e fundação de partido (PartyFoundingEngine). Telas novas: mandato, entressafra e diálogo de fundação; a apuração leva à posse.
36 projetos municipais e 7 ações de gabinete em data/. 413 testes no Core, incluindo carreiras inteiras jogadas sozinhas (mandato de 4 anos e reeleição, 3 sementes); smoke da UI em 7 fluxos. Pendente para depois: eventos de mandato, eleição da Mesa e CPIs (D-027).

## Fase 5 — Prefeito (2026-11-15)
Candidatura a prefeito na pré-campanha (idade mínima na posse, vaga do próprio partido, prefeito incumbente e renúncia ao disputar outro cargo), mandato de prefeito no Core (ExecutiveEngine: orçamento por área com mínimos de saúde e educação e Tribunal de Contas, indicadores com atraso, 17 políticas por decreto ou lei votada na Câmara, coalizão por secretarias, aprovação por grupo, 7 crises) e debate de TV (DebateEngine: três rodadas, seis cartas, IA por estilo, cortes virais). Telas novas: prefeitura (com overlay de crise) e debate; o hub troca a candidatura e chama o debate; a apuração mostra o prefeito.
425 testes no Core (prefeitura de 4 anos e reeleição jogadas sozinhas, campanha de prefeito com debates); smoke da UI em 8 fluxos.

## Fase 6 — itens avulsos (2026-11-15)
Contornos simplificados da Malha Municipal (feitos na Fase 3, data/geo/) marcados no ROADMAP. População projetada e vagas de vereador por eleição (D-033): taxa anual entre o Censo 2022 e a estimativa do IBGE, teto do art. 29, IV com a população do ano anterior à eleição. 428 testes no Core.

## Correções eleitorais (2026-11-15)
Janela partidária só no ano da eleição em que o mandato acaba (vereador: municipal; deputados: geral). Idade mínima de 18 anos aferida no prazo do registro (15/8). Meta de assinaturas do Brasil 2026 com os votos válidos reais para a Câmara (566.265). CONTENT_GUIDE atualizado: só as fases pre_campanha e campanha; eventos de mandato ainda não existem. Adendos em D-028 a D-030; 433 testes no Core.

## Resumo da noite (modo corujão, 2026-11-15)
- **Concluído:** Fase 3 (UI da campanha, urna, apuração, save/load, mapa 2.5D, balanceamento D-026, roteiro em docs/ROTEIRO_TESTE_FASE3.md), Fase 4 (mandato de vereador, transição, filiação, fundação de partido), Fase 5 (candidatura e mandato de prefeito, debate) e 2 dos 11 itens da Fase 6 (contornos; população e vagas por eleição). Decisões D-021 a D-033.
- **Testes:** 428 no Core, 28 no importador; teste de fumaça da UI em 8 fluxos (newworld, creator, hub, picker, campaign, map, mandate, prefeitura), sem janela e com janela.
- **Pendências:** nenhuma em docs/PENDENCIAS.md (nada bloqueou). Uma exceção "Handle is not initialized", vista uma vez numa rodada com janela, não se repetiu em 5 rodadas; o timer da urna ganhou proteção contra tela já liberada.
- **Adiados de propósito (registrados nas decisões):** eventos de mandato (vereador e prefeito), eleição da Mesa, CPIs, vetos e cassação do prefeito.
- **Por onde retomar:** jogar o roteiro da Fase 3 (dá para seguir até a posse, o mandato e, na pré-campanha seguinte, disputar a prefeitura); depois, o resto da Fase 6 (estados, deputados, senador e governador, simulação regional e nacional, mídia e justiça, Espelho, evolução dos partidos, urna completa, mapa completo).

## Correções de interface (2026-11-16)
Travamento depois de evento: a janela fechava, mas o fundo escuro (que bloqueia o mouse) ficava no hub; agora sai junto. Resultado de cada ação mostra o efeito medido (reconhecimento, dinheiro, imagem, apoio, relações), nunca só "feito". Novo Mundo com ícones de sortear e travar (tooltip) e tabela que cabe na tela; aba Partido da criação em duas linhas. Testes de fumaça novos, com janela: `eventos` (as 80 opções dos 32 eventos clicadas com o mouse, conferindo que o hub volta a aceitar cliques; sem "Handle is not initialized") e `layout` (1920x1080 e 1366x768, falha se algum controle passar da janela). No modo sem janela o Godot não entrega cliques de mouse à interface, por isso esses dois rodam com janela.

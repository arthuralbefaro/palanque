# Decisões

Registro das decisões tomadas durante o desenvolvimento (CLAUDE.md, seção Autonomia). Cada entrada: data, fase, pergunta, decisão, alternativas e como reverter.

## D-001 — Fonte da classificação ideológica dos partidos reais (cenário Brasil 2026)
- **Data:** 2026-11-15 · **Fase:** 1D
- **Pergunta:** que pesquisa acadêmica com especialistas sustenta a família ideológica de cada partido real espelhado (GDD 16.4)?
- **Decisão:** Bolognesi, B.; Ribeiro, E.; Codato, A.; Silva, B. F. "O desaparecimento do centro ideológico no sistema partidário brasileiro: a classificação mais atualizada dos experts". *Opinião Pública*, v. 31, e31120, 2025. DOI 10.1590/1807-0191202531120. Acesso aberto (SciELO: https://www.scielo.br/j/op/a/hv8GBg9hfCCZLwcWktfYhtC/). Questionário a 978 cientistas políticos da ABCP, retorno de 48,8% em 2022; escala esquerda–direita de 0 a 10; 32 partidos registrados no TSE em julho de 2022 (Tabela 1, médias de 2022 conferidas no PDF). Faixas categóricas dos autores: extrema esquerda 0–1,5; esquerda 1,51–3; centro-esquerda 3,01–4,49; centro 4,5–5,5; centro-direita 5,51–7; direita 7,01–8,5; extrema direita 8,51–10.
- **Alternativas:** Bolognesi, Ribeiro e Codato (2023, *Dados*), só com a rodada de 2018; Zucco e Power (Brazilian Legislative Survey), que ouve parlamentares, não especialistas; rankings estatísticos (sem especialistas).
- **Como reverter:** trocar a tabela de escores no pipeline de tools/ e regenerar data/parties/scenarios/brasil_2026/.

## D-002 — Partidos criados, fundidos ou renomeados depois de julho de 2022
- **Data:** 2026-11-15 · **Fase:** 1D
- **Pergunta:** como posicionar partidos registrados hoje que não constam da Tabela 1?
- **Decisão:** aplicar a mesma regra que os autores usam no artigo: partido fundido recebe a média aritmética das siglas de origem; mudança de nome mantém o escore; partido incorporado deixa de ser classificado e o incorporador mantém o próprio escore. Assim: PRD = média de PTB (7,72) e Patriota (8,60) = 8,16; Podemos mantém 7,44 (incorporou o PSC); Solidariedade mantém 6,01 (incorporou o PROS); Mobiliza mantém o escore do PMN (6,74).
- **Alternativas:** deixar esses partidos sem classificação.
- **Como reverter:** remover as derivações da tabela do pipeline.
- **Pendente:** o Missão (registrado em 2025) não consta de nenhuma fonte publicada encontrada; a decisão foi levada ao autor (parada prevista no CLAUDE.md, item 2).

## D-003 — Missão sem classificação publicada
- **Data:** 2026-11-15 · **Fase:** 1D · **Decidido pelo autor.**
- **Pergunta:** como tratar o partido fictício que espelha o Missão, ausente de qualquer fonte publicada?
- **Decisão:** entra com as cadeiras e os votos reais, mas família e posição são sorteadas como num partido gerado; o arquivo marca `classified: false`.
- **Alternativas:** excluir do cenário; esperar fonte.
- **Como reverter:** incluir o escore no pipeline quando houver fonte e regenerar o cenário.

## D-004 — Regra de família dos partidos espelhados
- **Data:** 2026-11-15 · **Fase:** 1D · **Decidido pelo autor.**
- **Decisão:** eixo econômico = (escore − 5) × 20, exatamente o publicado (arredondado ao inteiro; o escore original fica no arquivo). Eixo de costumes = o mesmo valor mais uma variação sorteada pelo Rng de até ±N, com N em data/parties/generator.json, marcada no arquivo como "variação sorteada, não derivada da fonte". A família é a que contém o ponto final, entre as famílias gerais (esquerda socialista, esquerda, centro-esquerda, centro, centro-direita, direita liberal, direita conservadora, liberal-conservador); se nenhuma contiver, a de retângulo mais próximo; empate, o centro mais próximo. Famílias específicas (verde, trabalhista, cristã, agrária, pragmática, libertária, nacional-populista, regionalista) nunca são atribuídas a partidos reais.
- **Alternativas:** só o eixo econômico; tabela fixa por categoria.
- **Como reverter:** mudar a regra em tools/ e regenerar.

## D-005 — Amapá sem resultado oficial das proporcionais de 2026
- **Data:** 2026-11-15 · **Fase:** 1D
- **Pergunta:** no consulta_cand_2026 do TSE todas as candidaturas a deputado federal e estadual do Amapá estão com situação "#NULO" (8 + 24 vagas sem eleitos). Como compor Câmara e Assembleia?
- **Decisão:** usar, só para o Amapá, a composição eleita em 2022 (consulta_cand_2022, TSE), com os partidos mapeados pela mesma linhagem; o arquivo do cenário marca `fonte_ano: 2022` e a substituição fica registrada nas fontes.
- **Alternativas:** deixar as vagas sem dono; recalcular pelo quociente com os votos parciais (só 11.730 votos válidos registrados).
- **Como reverter:** quando o TSE publicar o resultado, rodar o importador de novo (a regra só se aplica a UF sem eleitos).

## D-006 — Base governista e oposição no início
- **Data:** 2026-11-15 · **Fase:** 1D
- **Decisão:** base = partidos da coligação ou federação do presidente eleito em 2026; oposição = partidos da coligação do 2º colocado; demais, independentes. Fonte: consulta_coligacao_2026 e consulta_cand_2026 (TSE).
- **Alternativas:** votações nominais na Câmara (não há legislatura nova ainda).
- **Como reverter:** trocar a regra em tools/ e regenerar.

## D-007 — Prefeito reeleito em 2024
- **Data:** 2026-11-15 · **Fase:** 1D
- **Decisão:** o layout atual do consulta_cand não tem ST_REELEICAO; o prefeito eleito em 2024 é considerado reeleito (inelegível para prefeito em 2028) se o mesmo título de eleitor foi eleito prefeito no mesmo município em 2020 (ordinária ou suplementar), pelo consulta_cand_2020.
- **Alternativas:** ignorar a regra.
- **Como reverter:** trocar o critério no pipeline.

## D-008 — Números dos partidos espelhados
- **Data:** 2026-11-15 · **Fase:** 1D
- **Decisão:** os números fictícios são sorteados entre 10 e 99 evitando todos os números reais em uso em 2026, para nenhum partido fictício carregar o número de um real.
- **Como reverter:** remover o filtro no pipeline.

## D-009 — Edição do cenário Brasil 2026 na tela Novo Mundo
- **Data:** 2026-11-15 · **Fase:** 1D
- **Decisão:** o cenário pode ser editado campo a campo (nome, sigla, cores, tamanho etc.), mas não aceita adicionar, remover ou duplicar partidos, porque os arquivos estaduais e municipais referenciam os ids. Restaurar recarrega do arquivo.
- **Como reverter:** permitir e converter o mundo em Personalizado sem dados locais.

## D-010 — Fundo eleitoral e tempo de TV
- **Data:** 2026-11-15 · **Fase:** 1D
- **Decisão:** o cenário guarda só as entradas (cadeiras e votos para a Câmara); a divisão é calculada no Core por objeto de configuração com o padrão da lei (Lei 9.504/1997, art. 16-D: 2% igual, 35% votos para a Câmara entre partidos com deputado, 48% cadeiras na Câmara, 15% cadeiras no Senado; art. 47, §2º: 90% proporcional à bancada na Câmara, 10% igual).
- **Como reverter:** mudar os padrões no objeto de configuração.

## D-011 — Municípios sem resultado de 2024 na fonte
- **Data:** 2026-11-15 · **Fase:** 1D
- **Pergunta:** no consulta_cand_2024, 14 municípios têm todas as candidaturas a prefeito com situação "#NULO" (eleição anulada, sem suplementar com resultado) e 21 têm todas as de vereador assim. Como compor?
- **Decisão:** prefeito e vice ficam `null` (pendentes) e a lacuna é registrada em `gaps` do arquivo nacional; o jogo trata como prefeitura interina até a próxima eleição. Câmara sem eleitos em 2024 usa a eleita em 2020 (consulta_cand_2020) quando o número de cadeiras é igual às vagas atuais, com `camara_fonte_ano: 2020`. Onde nem isso fecha (Placas/PA: 11 vagas, 13 eleitos em 2020), a Câmara fica vazia, registrada como `camara_sem_fonte`; nenhum número é inventado.
- **Alternativas:** descartar os municípios do cenário; ratear as cadeiras pelos votos válidos parciais.
- **Como reverter:** quando o TSE publicar os resultados, rodar `dotnet run --project tools/Planalto.DataImporter -- cenario` de novo.
- **Adendo (força para prefeito):** usa os votos válidos da eleição que definiu o prefeito, 1º turno; se a fonte não tiver votos válidos do 1º turno (Aracaju/SE: só o 2º turno publicado), o 2º turno; se o prefeito veio de suplementar, os votos dela.

## D-012 — Segmentos de eleitores
- **Data:** 2026-11-15 · **Fase:** 1E
- **Pergunta:** como montar os segmentos a partir do perfil do Censo, que só traz cada bloco separado (sem tabela conjunta)?
- **Decisão:** segmentos = cruzamento de 5 dimensões agrupadas em data/electorate/segments.json: zona (urbana, rural), idade (16–17, 18–34, 35–59, 60–69, 70+; as faixas 16–17 e 70+ marcadas como voto facultativo; 0–15 excluída), renda (baixa até 1 SM, média 1–3 SM, alta acima de 3 SM), religião (católica, evangélica, outras, sem religião; "não sabe" e "sem declaração" fora do denominador) e escolaridade (até fundamental, médio ou mais) = 240 segmentos por município. A fatia de cada grupo é a soma das categorias dividida pela soma das categorias do bloco (nunca o total publicado). Sem tabela conjunta, o peso do segmento é o produto das fatias (independência entre dimensões) vezes o eleitorado do TSE.
- **Ideologia, temas e engajamento por grupo:** parâmetros de jogo em segments.json (deslocamentos moderados nos dois eixos, pesos de temas, ajuste de comparecimento), sem fonte empírica; ajustáveis no balanceamento. Nenhum parâmetro altera atributos de personagens nem é usado em texto de evento.
- **Alternativas:** cruzar todas as categorias do Censo (milhares de segmentos); importar tabelas conjuntas do SIDRA (não existem para todas as combinações por município).
- **Como reverter:** editar as dimensões em segments.json; o código não fixa nenhuma.

## D-013 — Afinidade partidária inicial dos segmentos
- **Data:** 2026-11-15 · **Fase:** 1E
- **Decisão:** no cenário Brasil 2026, a força local de 2024 para vereador vira a afinidade dos segmentos por ajuste proporcional iterativo (IPF): parte de força × proximidade ideológica segmento–partido e ajusta até a média ponderada pelos eleitores reproduzir a força local e cada segmento somar 1. Nos demais cenários, a força municipal é gerada pelo tamanho do partido (pesos de generator.json) × proximidade ao centro ideológico do município × ruído pelo Rng, e passa pelo mesmo IPF. O centro do município é a média das posições dos partidos ponderada pela força (Brasil 2026) ou um sorteio em torno de 0 (demais).
- **Como reverter:** parâmetros em data/electorate/vote_model.json.

## D-014 — Modelo de voto e pesquisas
- **Data:** 2026-11-15 · **Fase:** 1E
- **Decisão:** utilidade da fórmula do GDD 5.3 com pesos em vote_model.json; intenção = softmax com temperatura; brancos e nulos crescem quando a melhor utilidade do segmento é baixa; comparecimento = base + engajamento do segmento, multiplicado pelo fator de voto facultativo em 16–17 e 70+. Pesquisas: amostra multinomial simulada pelo Rng entre os prováveis eleitores, viés de casa do instituto inclinando o eixo econômico (vieses simétricos: soma zero entre os institutos), margem de erro = 1,96 × √(0,25/n) × √(efeito de desenho), "não sabe" decrescendo até a eleição; pesquisa interna com amostra maior, sem viés e mais cara. Institutos fictícios em data/electorate/pollsters.json.
- **Como reverter:** parâmetros nos arquivos de data/electorate/.

## D-015 — Calibração inicial do modelo de voto
- **Data:** 2026-11-15 · **Fase:** 1E
- **Pergunta:** que temperatura e pesos usar no softmax?
- **Decisão:** calibrado contra a votação real de prefeito de 2024 (força local para prefeito do cenário) em 250 municípios sorteados, com um candidato por partido e reconhecimento igual: temperatura 0,35 dava erro médio de 19,7 pontos por candidato; temperatura 1,3 com peso ideológico 1,5 e partidário 2,0 dá 11,1 pontos e acerta o vencedor em 72% dos municípios (a qualidade individual dos candidatos reais, não modelada, explica o resto). Temperaturas maiores quase não melhoram e achatam a disputa.
- **Como reverter:** vote_model.json; o balanceamento fino fica para o BalanceSim (Fase 10).

## D-016 — Pesos regionais de tom de pele
- **Data:** 2026-11-15 · **Fase:** 2
- **Pergunta:** de onde vêm os "pesos realistas por região do Brasil" da aparência dos NPCs (GDD 3.8)?
- **Decisão:** composição por cor ou raça de cada UF do Censo 2022 (SIDRA 9605, importada por `dotnet run --project tools/Planalto.DataImporter -- aparencia` para data/appearance/cor_raca_uf.json). Cada categoria declarada tem uma faixa de pesos sobre os 24 tons da paleta (palettes.json, `skin_by_cor_raca`); o tom sorteado mistura as faixas pela composição da UF. A correspondência categoria → faixa de tom é parâmetro de jogo (cor ou raça é autodeclaração, não tom de pele), e aparência nunca altera votos nem atributos. Peças e acessórios usam pesos por gênero, sem proibir nenhuma combinação.
- **Alternativas:** pesos uniformes.
- **Como reverter:** editar `skin_by_cor_raca` ou passar `uf: null` ao gerador (usa o total do Brasil).

## D-017 — Nomes de NPCs
- **Data:** 2026-11-15 · **Fase:** 2 · **Atualizada a pedido do autor.**
- **Decisão:** prenomes da API de nomes do IBGE (Censo 2010), importados por `dotnet run --project tools/Planalto.DataImporter -- nomes`: (1) ranking dos 20 mais frequentes por sexo, UF e década de nascimento (todas as 9 décadas da API: antes de 1930 até 2000–2009; a chave D cobre quem nasceu em [D − 10, D)), mais o ranking geral por UF e do Brasil; (2) como o ranking só traz 20 nomes, prenomes complementares listados em data/names/names.json têm a frequência nacional por década consultada nome a nome na mesma API e entram se passarem de `complementary_min_frequency`. O gerador sorteia pela década de nascimento do personagem na UF, cai para o ranking geral da UF (e depois do Brasil) se faltar dado, e soma os complementares com a frequência nacional da década escalada pelo peso da UF. Resultado: 193 prenomes masculinos e 230 femininos distintos (meta: 150 por sexo). Acentos restaurados por mapa em names.json (a API devolve sem acento). Sobrenomes: lista de sobrenomes frequentes com pesos (conteúdo do jogo; o IBGE não publica ranking de sobrenomes). Pessoas não binárias sorteiam entre as duas listas. Nome de urna por padrões de names.json, com título de profissão por gênero.
- **Como reverter:** editar names.json ou rodar o importador de novo.

## D-018 — Construção das telas e textos
- **Data:** 2026-11-15 · **Fase:** 2
- **Decisão:** as telas são montadas em C# a partir de cenas mínimas (um nó raiz com script), usando o tema base; os textos ao jogador passam por `Tr()` com o próprio texto em PT-BR como chave, para a tradução futura entrar por CSV sem tocar no código (GDD 8.5). Textos que dependem do gênero do personagem vêm de GenderedText no Core. Os dados de data/ são lidos pelo Core a partir do caminho global de res://data (vale no editor e nas execuções de desenvolvimento; o empacotamento para exportação fica para a Fase 10).
- **Como reverter:** converter telas para .tscn editáveis quando a arte final chegar.

## D-019 — Início do jogo
- **Data:** 2026-11-15 · **Fase:** 2
- **Decisão:** a carreira começa na semana ISO 1 do ano inicial escolhido (padrão 2028, ano de eleição municipal), com o personagem já filiado ao partido escolhido na criação, antes do prazo de filiação de abril.
- **Como reverter:** mudar a data em NewGame.

## D-020 — Partido criado pelo jogador
- **Data:** 2026-11-15 · **Fase:** 2
- **Decisão:** na aba "Partido" (GDD 14.2 A) o jogador cria o partido já registrado; ele entra no mundo como nanico, sem bancadas nem força local, inclusive no cenário Brasil 2026 (exceção à D-009, porque não há dados locais a manter). Nome e sigla passam pela mesma validação da lista de bloqueio.
- **Como reverter:** bloquear a criação nos cenários espelhados.

## D-021 — Montagem da disputa de vereador
- **Data:** 2026-11-15 · **Fase:** 3
- **Decisão:** quando o município entra em foco, a disputa reúne os 6 partidos mais fortes nele (o do jogador sempre entra, no lugar do sexto), 10 rivais nomeados (gerados como NPCs e gravados no save) e, por partido, um candidato coletivo que representa os demais candidatos anônimos. O coletivo pesa no modelo de voto como `vagas + 1 − nomeados` candidatos (agregação log-soma-exp), para que a fatia de cada nome não seja inflada. Na apuração, o coletivo vira candidatos anônimos com votos decrescentes e 8% dos votos do partido vão para a legenda. Prefeito: 2 a 4 candidatos. Todos os números em data/campaign/config.json.
- **Como reverter:** ajustar config.json; a regra do coletivo está em RaceSetup e VoteModel.

## D-022 — Ritmo da campanha e ganhos decrescentes
- **Data:** 2026-11-15 · **Fase:** 3
- **Decisão:** a campanha avança por semana com 100 de energia; ações de rua e mídia só a partir de 16 de agosto (pré-campanha antes disso). Custos em dinheiro multiplicados pelo porte (0,4 no pequeno a 6 na metrópole) e reconhecimento escalado no sentido contrário (1,6 a 0,35). O ganho de reconhecimento é decrescente (`ganho × (1 − atual/100)`) e há decaimento semanal de reconhecimento, imagem e apoio, para que repetir a mesma ação não sature o jogo. NPCs fazem de 1 a 3 ações por semana com eficiência 0,6. Pesquisa pública a cada 3 semanas; ruído do resultado final com desvio 0,12.
- **Como reverter:** config.json; a curva de ganho está em EffectApplier.Gain.

## D-023 — Sorteio de eventos
- **Data:** 2026-11-15 · **Fase:** 3
- **Decisão:** o número de eventos da semana segue uma Poisson com média pela frequência de eventos das opções do mundo (0,3 a 1,6), limitada a 3; cada evento elegível é sorteado com peso × raridade (comum 1, incomum 0,5, raro 0,2, lendário 0,05), respeitando condições, cooldown, eventos únicos e eventos só de cadeia. A semana não avança com evento pendente. O EventLint também roda como teste do Core, então um evento quebrado reprova a verificação obrigatória.
- **Como reverter:** data/events/config.json.

## D-024 — Urna, apuração e saves
- **Data:** 2026-11-15 · **Fase:** 3
- **Decisão:** a urna segue a ordem real das municipais (vereador, depois prefeito), aceita voto nominal, de legenda (2 dígitos, só vereador), branco e nulo, pelo mouse ou pelo teclado; o voto do jogador entra na apuração. A noite da apuração mostra 12 parciais. Saves: autosave a cada semana, um save extra na véspera da urna (para rejogar a eleição) e slots manuais; no modo ironman só há um slot.
- **Como reverter:** constantes em SaveManager e count_snapshots em config.json.

## D-025 — Protótipo do mapa 2.5D
- **Data:** 2026-11-15 · **Fase:** 3
- **Decisão:** contornos de data/geo/ gerados pelo modo `geo` do importador a partir da Malha Municipal 2025 já usada para as coordenadas: simplificação por cobertura da UF (vizinhos mantêm a mesma divisa), tolerância de 0,01° (≈ 1,1 km, cerca de 1 pixel na visão de MG), coordenadas inteiras em 1/10.000 de grau e face de cima já triangulada no importador (com furos). Total de 6,3 MB para as 27 UFs. As áreas operacionais das lagoas dos Patos e Mirim, que a malha traz com código próprio, ficam de fora. No jogo, uma malha por município; altura pela força do partido escolhido (o do jogador, por padrão) com o topo da escala no percentil 95, porque a força costuma se concentrar em poucos municípios; cor pelo partido mais forte, mais viva quanto maior a margem sobre o segundo. Medido: 853 municípios de MG montados em cerca de 300 ms e 60 FPS (limite do vsync) na janela.
- **Como reverter:** tolerância e escala em GeoImport; parâmetros visuais em MapScreen.

## D-026 — Balanceamento da campanha de vereador
- **Data:** 2026-11-15 · **Fase:** 3
- **Pergunta:** a fatia vertical estava fácil demais: com o partido mais forte da cidade e a energia gasta toda semana, o jogador saía o mais votado em qualquer porte, inclusive com 23 vagas numa metrópole, e sem fazer campanha alguma se elegia em cerca de 1/3 das cidades grandes.
- **Decisão:** (1) **conjunto de consideração** no modelo de voto: a fatia de cada candidato é multiplicada por max(reconhecimento, 0,02) (`awareness` em vote_model.json; expoente 0 desliga). O eleitor só vota em quem conhece. Com reconhecimento igual para todos o fator se cancela, então a calibração D-015 continua valendo. (2) O candidato coletivo de cada partido também faz campanha no turno dos rivais (`bulk_drive` 0,8), porque representa candidatos anônimos que pedem voto. (3) Rivais mais ativos: 2 a 4 ações por semana, eficiência 0,8, empenho de 0,5 a 1,2 (antes fixo no código, de 0,3 a 0,9), com arredondamento estocástico. (4) Empenho dos rivais por porte (`npc_effort_by_porte`: pequeno 1,05, médio 1,1, grande 1,8, metrópole 2,2): nas cidades maiores as campanhas têm equipe, e a energia do jogador é a de uma pessoa.
- **Medição** (8 cidades por porte × 3 sementes, ações sorteadas gastando toda a energia): partido mais forte da cidade eleito em 23/24 (pequeno, posição mediana 4), 23/24 (médio, 2), 16/24 (grande, 4) e 14/24 (metrópole, 6); partido sorteado em 3, 3, 2 e 0 de 24; sem campanha, 0 de 24 em todos os portes. Com 8 cidades por porte o ruído da amostra é da ordem do ajuste, então os multiplicadores não foram refinados além disso. CampaignBalanceTests guarda os dois extremos.
- **Como reverter:** data/campaign/config.json e data/electorate/vote_model.json; o ajuste fino definitivo fica para o BalanceSim (Fase 10).

## D-027 — Mandato de vereador
- **Data:** 2026-11-15 · **Fase:** 4
- **Decisão:** na posse (1º de janeiro), a Câmara reúne os eleitos da apuração; os anônimos do candidato coletivo viram NPCs. A base do prefeito é o partido dele mais os partidos ideologicamente próximos (distância até 90), do mais perto para o mais longe, até a maioria mais uma cadeira; sem isso, governo minoritário. Líder de bancada é o mais votado de cada partido; o jogador líder orienta a própria bancada e não sofre perda de fidelidade. Pauta semanal (1 a 3 projetos, recesso em janeiro e julho) com projetos do Executivo (0,35 por semana) e dos colegas (0,45), que passam de 3 a 8 semanas na comissão; projetos de vereador são arquivados com 25% de chance (o do jogador ganha com boas relações e quando o tema é da comissão dele). Voto NPC = logito de afinidade ideológica (indiferença a 70 pontos), orientação × disciplina do partido, governo/oposição nos projetos do Executivo e relação com o jogador nos projetos dele, mais ruído; perto de zero, abstenção. Quórum simples, absoluto ou de dois terços conforme o projeto. O voto do jogador mexe no apoio de cada grupo citado no projeto e na imagem pela reação líquida da cidade (peso de cada grupo no eleitorado), na fidelidade (advertência abaixo de 55, perda de comissão e liderança abaixo de 35, expulsão abaixo de 12, com mandato mantido), na relação com o prefeito e com o autor, e no capital político (votar com o governo estando na base; projeto próprio aprovado). Falta conta como ausência. 36 projetos municipais em data/bills/municipal.json, cobrindo os dois lados do espectro; números em data/legislature/config.json.
- **Ainda não:** eventos de mandato (a categoria existe no EventLint, mas o motor de eventos ainda é da campanha), eleição da Mesa e CPIs.
- **Como reverter:** data/legislature/ e data/bills/.

## D-028 — Transição da carreira e filiação partidária
- **Data:** 2026-11-15 · **Fase:** 4
- **Decisão:** depois da apuração, a política local (Câmara, votos, prefeito, imagem e apoios do jogador) fica no estado e substitui os dados do cenário na disputa seguinte: vereadores em exercício e o prefeito (se não estiver no 2º mandato) concorrem de novo; o jogador leva reconhecimento, imagem e apoios do mandato. Eleito, toma posse em 1º de janeiro; derrotado, vai à entressafra. A campanha das eleições seguintes abre no prazo de filiação (6 meses antes, Lei 9.504, art. 9º), não em janeiro, para que a troca de partido na janela (30 dias antes do prazo, Lei 9.096, art. 22-A, III) entre na disputa. Só disputa quem está filiado até o prazo, a um partido registrado até lá (art. 4º). Com mandato, trocar de partido fora da janela custa o mandato (pede confirmação na tela); expulso ou fora de mandato, a troca é livre. Na campanha de reeleição, o mandato vota sozinho seguindo a orientação.
- **Como reverter:** CareerFlow e PartyAffiliation.
- **Adendo (2026-11-15, a pedido do autor):** a janela partidária (Lei 9.096, art. 22-A, III) só vale ao término do mandato vigente: vereador só a usa no ano da eleição municipal em que o mandato acaba; deputado estadual e federal, no ano da eleição geral em que o mandato acaba. Um vereador eleito em 2028 não tem janela em 2030 (eleição geral) e perde o mandato se trocar de partido ali; em 2032, troca sem perder. Cargos majoritários continuam livres. **Motivo:** a versão anterior abria a janela em todo ano de eleição municipal, inclusive no meio de um mandato que ainda não acabava. **Como reverter:** PartyAffiliation.InWindow.

## D-029 — Fundação de partido durante a carreira
- **Data:** 2026-11-15 · **Fase:** 4
- **Decisão:** processo da Lei 9.096 simplificado (GDD 14.2 B): reunir 101 fundadores custa 15 de capital político e R$ 20 mil; depois, apoio de eleitores igual a 0,5% dos votos válidos para a Câmara (aproximados como 72% do eleitorado de data/municipios/: 566.792 assinaturas), com mínimo de 0,1% em ao menos 9 estados, em até 104 semanas. Coleta semanal em até 3 estados, dividida pelo tamanho do eleitorado, em três intensidades (voluntários, mutirão, coletores pagos; estes acumulam risco de impugnação). O pedido ao TSE leva de 8 a 20 semanas; impugnado, perde 25% das assinaturas e volta à coleta. Registrado, o partido entra no mundo como nanico. Nome, sigla e número passam pela lista de bloqueio e pela unicidade.
- **Como reverter:** data/parties/founding.json.
- **Adendo (2026-11-15, a pedido do autor):** no cenário Brasil 2026, a meta nacional de apoio usa os votos válidos reais para deputado federal em 2026 (nominais + legenda, todos os partidos: 113.252.815, gravados em nacional.json como `camara_valid_votes` pelo importador): 0,5% são 566.265 assinaturas. Os 72% do eleitorado ficam para os outros cenários e para o mínimo por estado, que não tem dado real de comparecimento por UF no cache. **Motivo:** o cenário espelhado tem o número oficial; a aproximação só se justifica onde ele não existe. **Como reverter:** não passar `camaraValidVotes` ao PartyFoundingEngine.

## D-030 — Candidatura a prefeito
- **Data:** 2026-11-15 · **Fase:** 5
- **Decisão:** na pré-campanha, o jogador escolhe entre vereador e prefeito (até 16 de agosto). Disputando a prefeitura, ele ocupa a vaga de candidato do próprio partido, e o NPC que estava nela passa a disputar a Câmara. A idade mínima é verificada na data da posse, num objeto de configuração com os valores da CF, art. 14, § 3º, VI (18 para vereador, 21 para prefeito e deputados, 30 para governador, 35 para senador e presidente). Prefeito em exercício no 1º mandato começa candidato à reeleição como incumbente; se trocar para outro cargo, renuncia (desincompatibilização, art. 14, § 6º, com aviso na tela). As pesquisas e os rivais dos eventos acompanham a disputa do jogador. O 2º turno já existia (mais de 200 mil eleitores). Prefeito que troca de partido não perde o mandato (cargo majoritário).
- **Como reverter:** CandidacyRulesConfig e RaceSetup.SwitchPlayerOffice.
- **Adendo (2026-11-15, a pedido do autor):** quando a idade mínima é 18 anos (vereador), ela é aferida na data-limite do pedido de registro, 15 de agosto do ano da eleição (Lei 9.504, art. 11, § 2º e caput); nos demais cargos, continua na posse. Os parâmetros ficam no CandidacyRulesConfig, com os valores da lei por padrão. **Motivo:** a versão anterior aferia tudo na posse e deixaria disputar quem só faz 18 anos entre agosto e dezembro. **Como reverter:** AgeCheckedAtRegistration em CandidacyRulesConfig.

## D-031 — Mandato de prefeito
- **Data:** 2026-11-15 · **Fase:** 5
- **Decisão:** o orçamento é um conjunto de parcelas por área (saúde, educação, segurança, obras, assistência, administração e dívida, e uma reserva para programas) que somam 100%. A escala em reais vem de uma receita por habitante por porte, marcada no arquivo como estimativa de jogo e não dado oficial. Cada indicador (saúde, educação, segurança, infraestrutura, emprego, proteção social e contas públicas) caminha 3,5% por semana rumo a um alvo dado pelo gasto da área relativo à referência (45 pontos por 100% acima ou abaixo), mais as políticas em vigor e a reserva não usada. Mínimos de 15% em saúde e 25% em educação (aplicados ao orçamento todo, simplificação dos mínimos sobre a receita de impostos): descumprir no ano leva o Tribunal de Contas a rejeitar as contas, com perda de imagem. São 17 políticas: as que custam saem da reserva; as que arrecadam ou economizam liberam; as que precisam de lei vão à Câmara pelo mesmo modelo de voto do mandato de vereador (o partido do prefeito orienta SIM; a base vota como governo). A coalizão é feita por 8 secretarias: quem tem secretaria está na base; dar melhora a relação da bancada, tirar piora. A aprovação é a média, pesada pelo eleitorado, da aprovação de cada grupo, que pesa os indicadores do seu jeito (renda baixa olha saúde, assistência e emprego; renda alta, segurança e contas), mais imagem, apoio e lua de mel nos primeiros 6 meses. Há 7 crises (enchente e dengue sazonais, greves, escândalo, queda de arrecadação, onda de assaltos), com 3% de chance por semana, chance dobrada quando o indicador ligado está ruim, e três respostas cada. A inflação ficou de fora: é nacional, fora do alcance do prefeito.
- **Ainda não:** vetos, impeachment do prefeito (cassação pela Câmara) e eventos de mandato executivo.
- **Como reverter:** data/executive/.

## D-032 — Debate
- **Data:** 2026-11-15 · **Fase:** 5
- **Decisão:** debates de prefeito na TV 5 e 2 semanas antes do 1º turno, em cidades médias, grandes e metrópoles (as pequenas raramente têm debate na TV). São três rodadas com temas sorteados pelo peso que têm no eleitorado da cidade. O jogador escolhe uma de três cartas da mão: Proposta e Dado testam Gestão e rendem mais em tema importante; Ataque testa Carisma e mira um adversário; Defesa testa Articulação e reduz os ataques à metade; Emoção testa Carisma; Evasiva testa Oratória. Rivais escolhem pelo estilo (agressivo, técnico, emotivo, equilibrado; quem tem o traço esquentado é agressivo e bate mais forte). O placar vira imagem (diferença para a média × 0,012), todos ganham reconhecimento pela exposição, e há 15% de chance de um corte viral para quem atacou ou se emocionou. Recusar custa 0,04 de imagem. A semana não avança com debate marcado.
- **Como reverter:** data/debate/debate.json.

## D-033 — População projetada e vagas por eleição
- **Data:** 2026-11-15 · **Fase:** 6
- **Decisão:** a população de cada município cresce pela taxa anual geométrica entre o Censo 2022 e a estimativa do IBGE gravada em data/municipios/ (2026), limitada entre -3% e +5% ao ano (data/population_projection.json), projetada a partir do ano da estimativa. Em cada eleição municipal, as vagas de vereador são o mínimo entre as vagas atuais e o teto da CF, art. 29, IV, calculado com a população do ano anterior à eleição (a estimativa que o TSE usa). Assim as vagas nunca sobem sozinhas (aumentar é decisão da Câmara) e caem quando a população sai da faixa.
- **Como reverter:** sem Projection no CampaignContext, valem as vagas de data/municipios/.
- **Adendo (2026-11-16, fase RD; decisão 16 do GDD 17.20, registrada na D-035):** a taxa municipal passa a valer só como distribuição relativa. A cada ano, a população de cada município pela taxa (estimativa × (1 + taxa)^anos) é multiplicada por um fator único da UF, de modo que a soma dos municípios da UF seja a população total da UF nas Projeções da População do IBGE, revisão 2024 (`data/demography/projecao_uf.json`, de 2022 a 2070, importado da coluna POP_T dos indicadores implícitos; fonte em `data/demography/_fontes.json`). A soma difere do alvo só pelo arredondamento de cada município para inteiro (até meia pessoa por município). Depois de 2070, o alvo da UF segue a taxa do último ano publicado (2069 para 2070); antes de 2022, vale o de 2022. No ano da estimativa (2026), a soma dos municípios já é igual à projeção em 25 UFs e a reescala não muda nada; em PR e SC a soma municipal difere da projeção em 68 habitantes (a estimativa municipal segue a divisa atualizada), e a reescala reparte essa diferença (menos de 1 habitante a cada 100 mil). As vagas de vereador de cada eleição continuam pela mesma regra, agora com a população reescalada. Código: `PopulationProjection.From(dados, config, projecaoUf)` e `PopulationProjection.Load(pastaDeDados, dados)`, que lê o arquivo se ele existir. A migração e os choques da simulação entram depois, como desvio sobre isso (GDD 17.17).
- **Como reverter o adendo:** montar a projeção sem a projeção por UF (`PopulationProjection.From(dados, config)`) ou remover `data/demography/projecao_uf.json`: sem o arquivo, `Load` volta à projeção só pela taxa do município.

## D-034 — Reformulação depois do teste da fatia vertical
- **Data:** 2026-11-16 · **Fase:** R · **Decidido pelo autor.**
- **Pergunta:** o autor jogou a fatia vertical (Fases 3 a 5) e a reprovou: rasa, repetitiva, confusa e feia. Como seguir?
- **Decisão:** antes da Fase 6 entra a Fase R (R1, R2a, R2b, R3, RD, R4), descrita no GDD, seção 17, a partir do diagnóstico de docs/AUDITORIA_FASE3.md. O laço semanal com 6 ações de efeito fixo dá lugar a uma tela central com o mapa da cidade, tempo corrente por dia, território em zonas, pessoas com memória, compromissos com lugar e dia e consequências que ficam; as regras eleitorais, o modelo de voto, a Câmara, a prefeitura e o debate do Core são mantidos. O modo autônomo fica desligado na Fase R: cada parte termina, para e espera o autor jogar. Os princípios a–j do GDD 17.1 têm critério verificável e valem para todo sistema novo.
- **Pendências do autor:** as 22 decisões do GDD 17.20, tomadas pelo autor na D-035.
- **Alternativas:** seguir para a Fase 6 e polir depois; refazer só a interface.
- **Como reverter:** a Fase R é aditiva no ROADMAP; os motores atuais continuam no Core e as telas antigas ficam no histórico do Git.

## D-035 — Decisões do autor sobre o GDD 17.20
- **Data:** 2026-11-16 · **Fase:** R · **Decidido pelo autor.**
- **Aprovadas como recomendado:** 1 (mapa da cidade 2D plano), 2 (zonas por setores do Censo 2022 em `data/`, com teto de 30 MB para `data/geo/zonas/`; o importador falha se passar), 3 (nome de distrito ou nome gerado onde não há bairro oficial), 4 (religião e escolaridade por área de ponderação onde houver duas ou mais), 5 (faixas de renda por zona como estimativa de jogo), 6 (eleitores e votação de 2024 por zona pelos locais de votação do TSE, na RD), 8 (ritmo do tempo), 10 (limite de gastos pela tabela do TSE de 2024), 11 (lista de bloqueio de nomes de veículos), 14 (renovação das Câmaras Municipais só informativa), 16 (população reescalada pelas Projeções por UF), 17 (homicídios pelo SIM, soma de 3 anos), 18 (microdados da RAIS, brutos só no cache), 19 (proteção social como índice do jogo), 20 (aprovação pela variação e pela comparação), 21 (Inter, Oswald e Source Serif 4), 22 (curva depois dos 89 anos e fator pessoal como parâmetros de jogo).
- **7 — Termos de uso:** o autor confere os termos antes da RD. A R1 pode usar a Malha de Setores e os agregados do Censo 2022 citando a fonte.
- **9 — Regras de campanha, com correções:** propaganda paga em rádio e TV é proibida (Lei 9.504, art. 44): o canal "rádio" sai da verba e vira presença conquistada (entrevistas e horário gratuito). Outdoor é proibido (art. 39, § 8º): o outdoor da criação de personagem dá lugar a adesivo, bandeira, material impresso e peça de redes (GDD 3.4). Impulsionamento pago nas redes é permitido com regras (art. 57-C) e entra como canal. Cabos eleitorais têm limite de contratação pelo eleitorado (art. 100-A), em configuração. O resto é conferido item por item na R3, com os artigos citados; o GDD 17.20 marca o que foi citado de memória.
- **12 — Vacância no Executivo, com correção:** morte ou renúncia seguem o padrão do art. 81 da CF, configurável; vacância por decisão da Justiça Eleitoral segue o Código Eleitoral, art. 224, §§ 3º e 4º (nova eleição, indireta só a menos de 6 meses do fim do mandato). Dois caminhos, com teste de cada um.
- **13 — Alterada:** tábua de mortalidade por UF (IBGE, Projeções da População 2024), com a tábua completa do Brasil como reserva.
- **15 — Não aprovado:** o pacote de nomes fica suspenso. As vagas individuais fictícias do cenário Brasil 2026 seguem no plano.
- **Onde ficou registrado:** GDD 3.4, 17.5, 17.8, 17.16, 17.17, 17.18 e 17.20; ROADMAP (R1, R2b, R3, RD, R4 e Fase 6); CLAUDE.md.
- **Como reverter:** cada item é uma linha da tabela do GDD 17.20; mudar a linha e a subseção que ela cita.

## D-036 — Idade inicial de 17 anos, profissões com idade mínima e família coerente
- **Data:** 2026-11-16 · **Fase:** R1 · **Trazido do branch `wip/idade-inicial` do autor.**
- **Decisão:** o jogador começa com 17 a 45 anos; NPCs continuam entre 25 e 45 (`npc_age` em social_origins.json). A idade de 18 anos para vereador é aferida no prazo do registro, 15 de agosto (D-030); quem não a tiver na primeira eleição começa fora da disputa, e a carreira pula para a primeira eleição em que é elegível. **Alterado pelo autor em 16/11 (teste da `fase-r1`, P-24, item 8):** quem não pode disputar a primeira eleição começa direto na pré-campanha da primeira que pode disputar, com a idade correspondente, e a criação avisa antes de confirmar. Cada profissão tem idade mínima (`min_age` em professions.json, de 17 a 25), e entra a profissão Estudante (17). Estado civil, filhos e cônjuge são gerados coerentes com a idade pelas regras de data/family.json. A chave dos estados civis em family.json é o id em snake_case ("uniao_estavel").
- **Testes:** idade aferida em 15/8 (aniversário em 15/8 e em 16/8) e na posse para prefeito; NPCs entre 25 e 45; profissão nunca abaixo da idade mínima e Estudante aos 17; família sem incoerência em 10.000 sorteios; configuração inválida reportada; jogador de 17 anos espera a eleição de 2032 e a disputa até a apuração; teste de fumaça `creator17`.
- **Como reverter:** `starting_age` em social_origins.json e `min_age` em professions.json.

## D-037 — Respostas do autor às pendências da R1 e continuação do corujão
- **Data:** 2026-11-16 · **Fase:** R · **Decidido pelo autor.**
- **P-06, termos de uso do IBGE:** aceitos para uso com citação da fonte "IBGE" (em `_fontes.json` e em `assets/CREDITS.md`, como já é feito). **Conferir o ofício original antes do lançamento.** Vale para a Malha de Setores, os agregados do Censo 2022 e as demais fontes do IBGE usadas na RD.
- **P-05, intenção por zona:** a estimativa da R1 (`ZoneProjection`: a intenção da cidade repesada por idade e situação) basta até a R2a, que passa a calcular o voto zona a zona.
- **P-04, artigos citados de memória no material de campanha:** a conferência fica para a R3, junto dos demais artigos de campanha. Até lá, todo verbete e tooltip que cita artigo não conferido mostra a marca "(artigo a conferir)": campo `a_conferir` em `data/encyclopedia/glossary.json` (verbetes "Pré-campanha" e "Horário eleitoral gratuito" e artigo "O que a lei permite na propaganda") e os três tooltips de `CampaignMaterials`. Na R3, o que for conferido perde a marca.
- **P-08, tamanho de `data/zonas/`:** os 15 MB estão aceitos. Teto de 20 MB para a pasta (atributos e `_fontes.json`): o importador mede a saída e falha, sem gravar, se passar (`ZonasPaths.MaxZonasBytes`); um teste do Core confere a pasta.
- **Corujão:** o autor não testa por enquanto e autoriza seguir a Fase R inteira (RD → R2a → R2b → R3 → R4, depois o resto das pendências da R1 com a tag `fase-r-final`), sem esperar pelo teste dele. Dúvida vira escolha conservadora em PENDENCIAS; download bloqueado vira lacuna registrada; falta de janela vira parte "com pendências". Parar ao fim das pendências da R1; não começar a Fase 6.
- **Como reverter:** o teto é uma constante; a marca é um campo por verbete.


## D-038 — Escolhas da RD (dados reais de partida)
- **Data:** 2026-11-16 · **Fase:** RD · Tomadas no corujão; as marcadas "a confirmar" estão em docs/PENDENCIAS.md.
- **Finanças do Distrito Federal:** o DF não tem declaração municipal no SICONFI; Brasília usa a do ente estadual (código 53). Fica dito em `data/indicadores_reais/_fontes.json`.
- **Finanças, o que é lido:** receita corrente = receitas correntes brutas menos as deduções (DCA, Anexo I-C); despesa por função = despesa empenhada (Anexo I-E); receita corrente líquida e despesa total com pessoal em % da RCL ajustada = como publicadas no RGF, Anexo 01, do Executivo (quadrimestral ou, nos municípios que entregam o simplificado, semestral). Exercício mais recente entre 2025, 2024 e 2023. Sem nenhuma DCA: mediana por habitante dos municípios do mesmo porte na UF, marcada como estimativa (GDD 17.17).
- **Limite de chamadas do SICONFI:** a API recusa (HTTP 429) acima de cerca de 1 chamada por segundo; o baixador respeita o ritmo e espera quando o limite é atingido. O download completo leva algumas horas e é retomável.
- **Emprego formal:** soma de "Qtd Vínculos Ativos" dos microdados de **estabelecimentos** da RAIS 2025 (144 MB), em vez dos microdados de vínculos (4 GB): dá o mesmo estoque em 31/12 por município. O .7z é aberto pelo `tar` do sistema (bsdtar), sem pacote novo; os brutos ficam só no cache.
- **Homicídios:** óbitos por agressão do SIM, CID-10 de X85 a Y09, por município de residência, soma de 2022 a 2024 (definitivos). Intervenção legal (Y35 e Y36) fica de fora, a confirmar.
- **Mortalidade infantil:** óbitos de menores de 1 ano ÷ nascidos vivos, soma de 2022 a 2024; com menos de 300 nascidos vivos no período (`min_nascidos_mortalidade_infantil`), vale a taxa da região geográfica imediata.
- **Leitos:** todos os leitos existentes do CNES (SUS e não SUS, inclusive complementares) na competência de setembro de 2026.
- **PIB por habitante:** o arquivo guarda o PIB de 2023; a divisão pela população é feita no Core (não há estimativa municipal de população para 2023).
- **Faixas plausíveis:** `data/indicators/definitions.json` declara, por indicador, a faixa fora da qual o importador e os testes reprovam o valor. São limites de conferência, largos, não dados.
- **Eleitores por zona:** o arquivo guarda os eleitores pelos locais de votação do TSE (onde se vota), reescalados para o eleitorado atual. No modelo de voto, porém, os eleitores de cada zona são repartidos pela população de 16 anos ou mais (onde se mora): 4.344 zonas habitadas não têm local de votação e ficariam sem eleitor, e as zonas centrais ficariam com vários múltiplos dos moradores. **A confirmar pelo autor** (PENDENCIAS).
- **Religião e escolaridade por zona:** gravadas como listas na ordem das categorias do perfil, para `data/zonas/` caber no teto de 20 MB (ficou em 16,8 MB).
- **Votação de 2024 por zona:** os arquivos por seção do TSE estão no cache (26 UFs, cerca de 3 GB), mas a ligação com os partidos fictícios do cenário não foi feita nesta parte (PENDENCIAS).
- **Como reverter:** cada escolha é uma regra do importador (`tools/Planalto.DataImporter/Rd/`) ou um parâmetro de `data/indicators/definitions.json`; rodar `rd` de novo regenera os arquivos.
- **Adendo (declarações implausíveis):** DCA cuja receita corrente por habitante cai fora da faixa plausível de `data/indicators/definitions.json`, e RGF cuja despesa com pessoal em % da RCL cai fora dela, são tratados como erro evidente de preenchimento: a declaração é descartada, vale a regra de substituição do indicador e o valor declarado fica na lacuna. Foram 26 casos. Nenhum número é corrigido.
- **Adendo (IDEB do Distrito Federal):** o DF não tem rede municipal; usa-se a nota da rede estadual (distrital) do próprio município, marcada como `rede_estadual`.

## D-039 — Escolhas da R2a (território, agenda, lideranças, Livro)
- **Data:** 2026-11-16 · **Fase:** R2a · Tomadas no corujão.
- **Saem a energia semanal e as 6 ações** (`actions.json`); entram os compromissos de `data/campaign/commitments.json`. Nos eventos antigos, cada 4 pontos de energia viram 1 de estresse.
- **Presença de partida:** o jogador é 4 vezes mais conhecido na zona natal (a zona urbana mais populosa); cada rival nomeado a vereador tem 1 ou 2 redutos (2,5 vezes); candidatos a prefeito e o candidato coletivo começam iguais em toda a cidade. O reconhecimento é a média das presenças pela população.
- **Alcance:** as bases de presença são "pontos na cidade inteira"; um turno numa zona rende isso vezes (eleitores da cidade ÷ eleitores da zona), limitado a 10. Zona pequena rende mais presença por turno e vale menos votos.
- **Reunião com apoiadores** só onde há liderança conhecida e não hostil daquele público na zona; o apoio de conversa tem teto e retorno decrescente.
- **Evento da cidade:** divide o cansaço com a caminhada na mesma zona; o bônus de 50% vale para o público do evento (presença, se o público é geral; apoio do grupo, se é de um grupo).
- **Cansaço:** por trio (tipo, zona, público); na visita, por pessoa; no jantar, reduz o arrecadado.
- **Lideranças:** uma por pedido; aceitar grava promessa no Livro (prazo: a eleição, ou a posse mais o prazo do pedido); pedido de dinheiro aceito é gasto irregular e vira esqueleto que a liderança conhece; a promessa de comparecer se cumpre indo à zona com a liderança; convite recusado ou vencido não conta como contato; um convite pendente por vez.
- **Demanda da zona:** a regra que mais se destaca entre renda baixa, idosos, jovens e zona rural, com o valor medido e o da cidade; pesa 0,15 no tema dos eleitores dali.
- **Eleitores por zona no modelo de voto:** pela população de 16 anos ou mais (ver D-038 e P-11).
- **Dificuldade:** eficiência dos rivais em 0,5 e empenho por porte de 1,05, 1,1, 2,0 e 5,0, para manter os dois extremos da D-026 (parado nunca se elege; ativo no partido mais forte se elege na maior parte das vezes).
- **Critério do princípio a no BalanceSim:** "melhor escolha da semana" = a opção de maior ganho previsto por turno vista na semana; o par (tipo, zona) é medido turno a turno no conjunto das campanhas, porque as zonas são de cada cidade. O relatório mostra também o que o robô de fato marcou.
- **Como reverter:** todos os números estão em `data/campaign/commitments.json`, `leaders.json`, `config.json`, `data/territory/config.json` e `data/world/city_events.json`.

## D-040 — Escolhas da R2b (equipe, verba, plataforma, objetivos)
- **Interface (adendo):** a pausa por caixa abaixo de duas semanas de folha reusa o id `caixa_baixo` de `data/time.json` (desliga junto); o verbete "bandeira" atende por "bandeira(s) da campanha", para não confundir com a bandeira física da propaganda; a matriz "Plataforma × públicos" mostra um público por vez (o painel tem 400 px), e a leitura inversa fica na tela do tema; a previsão de mudança de posição mostra a variação dos seus votos em cada grupo e zona, com os pontos dos votos válidos no tooltip (para vereador eles dão ±0,01); o vazamento aparece na previsão de demitir e no feed, não como decisão; só o coordenador dá conselho.
- **Data:** 2026-11-16 · **Fase:** R2b · Tomadas no corujão, onde o GDD era vago. Cada uma se reverte no arquivo ou método citado.
- **Equipe:** sem marqueteiro ou social media o rendimento é 1,0 (qualidade 1 ou 2 fica abaixo de "ninguém"); sem advogado ou tesoureiro vale o pior padrão (`sem` em `staff.json`). O salário pedido segue a faixa mostrada, não a qualidade real. A rede de cabos ocupa uma vaga da equipe quando há verba de cabos. Erro contábil só em semana com gasto e vira esqueleto `erro_contabil`. Salários pela metade do exemplo do GDD (R$ 900 vira cerca de R$ 425 no porte médio), porque o caixa não comporta antes dos doadores da R3.
- **Delegação:** compromisso delegado não ocupa turno nem dá estresse, rende 50% e quem recebe lembra; delegáveis: caminhada, reunião, visita e evento (`delegavel` em `commitments.json`).
- **Conselho da semana:** "seguido" é ter 2 compromissos na zona aconselhada na semana (`conselho_compromissos_minimos`).
- **Vazamento:** o demitido com lealdade baixa põe o rival na lista de quem sabe do esqueleto e a imagem cai; a exposição pública é da R3.
- **Verba:** o aumento sai na hora e a redução vale na segunda; tudo é cobrado toda segunda e rende no fim da semana. Os canais abrem em 16 de agosto (a data, não a semana). Na pré-campanha há teto por canal e alvo, com rendimento de 60%. **Saturação** da verba repetida no mesmo canal e alvo: regra nova, fora do GDD 17.8 (`saturacao_fator`). O custo fixo do comício saiu: ele passa a exigir verba de Rua na zona.
- **Plataforma:** a posição própria só nasce quando o jogador a define; falar do tema aumenta a posição conhecida e registra no Livro. A incoerência conta as mudanças de todos os temas em 52 semanas. A credibilidade conta todas as promessas do Livro. O pedido "defender em público a pauta" se cumpre ao falar do tema.
- **Objetivos:** 7 tipos; proposta não fixada sai em 3 semanas; nunca dois do mesmo tipo.
- **Gravação para as redes** tem risco de viral negativo mesmo sem social media (`risco_viral`).
- **Lint de decisões:** as naturezas de ganho e custo são declaradas nos dados e conferidas contra os efeitos.
- **Lei:** `ChannelLawConfig` (cabos, art. 100-A; impulsionamento, art. 57-C; vedados: rádio e TV, outdoor, brinde) nasceu com os números de memória e a marca de não conferido; a conferência no texto oficial está em `docs/referencias/LEIS_CONFERIDAS.md` e a marca sai na R3.

## D-041 — Respostas do autor em 16/11 (segunda rodada): integração, tags e retratos 3D
- **Data:** 2026-11-16 · **Fase:** R · Decisão do autor.
- **Tags:** a `fase-r2a` não é movida; o que o autor pediu para a R2a depois da tag (CONTENT_GUIDE e EventLint) entra na main antes da `fase-r3`. O branch `wip/idade-inicial` fica com o autor.
- **Ordem de integração:** a R4 só entra na main depois da R3 integrada e com a tag `fase-r3`. Ao integrar a R4, roda-se tudo de novo: verificação obrigatória, os alvos de fumaça com janela e o BalanceSim com os critérios da R2a, da R2b e da R3; critério de parte anterior quebrado pela integração é corrigido antes da `fase-r4`. O Relatório para o autor lista todas as tags e o BalanceSim de cada uma.
- **Teste do autor na `fase-r1`:** dez itens registrados na PENDENCIAS P-24, corrigidos antes da `fase-r-final`, um commit por item.
- **Retratos e personagens em 3D** (o autor chamou esta decisão de D-038; esse número já estava em uso pelas escolhas da RD, então ela fica aqui): os retratos e personagens passam a ser bonecos 3D estilizados, os mesmos das posses (GDD 13.2), no lugar das camadas 2D. **Estilo corrigido pelo autor em 16/11:** não é low-poly facetado; ver o adendo "estilo dos bonecos" abaixo, que substitui o desenho descrito neste item.
  - **Não se faz durante a Fase R.** Ao terminar a `fase-r-final`, antes de parar, entrega-se só: esta decisão; o GDD atualizado (3.1, 3.6, 3.7, 7.4, 13.2 e 13.3); uma parte nova no ROADMAP entre a Fase R e a Fase 6, com itens e testes; e um protótipo com 3 personagens sorteados da mesma Appearance, em 3 expressões, com capturas.
  - **Desenho:** cabeça low-poly com o rosto em textura (olhos, sobrancelhas e boca trocáveis, o que dá as expressões); tom de pele, cabelo, grisalho e roupas por material tingível; 8 a 12 cabelos e 4 a 6 barbas em 3D, mapeados dos estilos atuais; retrato renderizado em SubViewport e guardado pelo PortraitCache.
  - **Base:** só pacotes CC0 ou CC BY (ex.: Kenney, Quaternius), com licença conferida e crédito em CREDITS.md; nenhum pacote de código novo.
  - A aparência continua sem efeito mecânico e com a diversidade do Censo (D-016).
  - Até lá, só se corrigem os encaixes errados de brinco e cabelo da arte 2D atual.

## D-042 — Escolhas da R3 (rivais, mídia, doadores, contas) e conferência dos artigos de campanha
- **Data:** 2026-11-16 · **Fase:** R3 · Tomadas no corujão.
- **Conferência de lei (item do ROADMAP):** os artigos de campanha foram lidos no texto compilado do Planalto em 16/11/2026, em três leituras; o resultado, artigo por artigo, está em `docs/referencias/LEIS_CONFERIDAS.md`. Só depois nasceram os objetos `CampaignFinanceLawConfig` (arts. 11, 18, 18-C, 22-A § 3º, 23 §§ 1º e 2º-A, 28 § 4º, 29 III e IV, 30, 30-A; vedação de doação de empresa) e `PropagandaLawConfig` (arts. 36, 39 §§ 3º, 4º e 9º, 43, 44, 47, 48, 58 § 1º, 77), e o `ChannelLawConfig` passou a "conferido" (art. 100-A com o arredondamento do § 2º; art. 57-C). Fecha a P-04.
- **Fica marcado como não conferido, para o autor:** a tabela de limites de gastos do TSE por município (site bloqueou; o limite do jogo é parâmetro por porte em `data/campaign/finance.json`, campo `lacuna`); a ADI 4650 e a Resolução TSE 23.607/2019; a fase 2 das sobras (80% e 20%) no acórdão do STF; a ADI 5.525 sobre o art. 224 do Código Eleitoral.
- **Sobras em três fases** (como registrado na conferência): quociente partidário; maiores médias entre partidos com 80% do quociente e candidatos com 20%; vagas restantes entre todos.
- **Ataque:** o dano é uma penalidade separada da imagem, que se apaga em linha reta (`AttackPenalty`). Responder não pausa o jogo: vencido o prazo, vale o silêncio. O prazo de 48 horas é o do GDD; os prazos do art. 58 (24, 48 e 72 horas) estão no objeto de lei, ainda sem diferenciar por tipo de veículo.
- **Tema em alta:** só contam notícias de fatos sem autor (cidade, economia), com 2 fatos diferentes; o bônus fica travado ao marcar o compromisso.
- **Notícia** mexe só na imagem, e pouco (`imagem_por_noticia`).
- **Verba dos rivais** só disputa o canal com o jogador; não rende presença a eles.
- **Recursos próprios** são o gasto além do que entrou; acima de 10% do limite viram irregularidade.
- **Entrevista sem veículo** continua aceita até a interface adotar o veículo.
- **Data da diplomação (18/12)** é parâmetro de jogo.
- **Eventos:** as 85 escolhas dos 32 eventos gravam lembrança ou entrada no Livro, ou levam a marca `sem_rastro` com motivo (46 delas); ganho e custo de naturezas diferentes são cobrados nas escolhas marcadas como relevantes (5 hoje), e tudo o que vai ao Livro ou abre cadeia é obrigado a ser relevante.
- **Como reverter:** `data/campaign/rival_profiles.json`, `attacks.json`, `finance.json`, `data/media/*.json` e os objetos de lei citados.
- **Adendo (terceira rodada, 16/11), antes da `fase-r3`:** (1) o BalanceSim da R3 roda com 200 campanhas por porte; (2) o canal de verba só é ajustado depois de diagnosticado (qual canal vence em cada porte e por quê), medido junto com a dificuldade; (3) a dificuldade segue a régua da D-026 (partido mais forte eleito na grande maioria das cidades pequenas e em cerca de metade das metrópoles; partido sorteado raramente; sem campanha, nunca), com a tabela antes e depois registrada; (4) o par (tipo, zona) do porte pequeno é medido de novo depois da correção do mapa de cidade pequena, antes de mexer em parâmetros. Os testes lentos ganham categoria: a cada commit rodam só os rápidos; antes de cada tag, a suíte inteira (registrado no CLAUDE.md).
- **Adendo (quarta rodada, 16/11): redefinição visual.** O autor decidiu duas coisas, que ele chama de D-038 (retratos e personagens em bonecos 3D estilizados) e D-039 (mapa da cidade 2D detalhado e fiel ao lugar real, com a interface em janelas flutuantes sobre o mapa; referência: Geopolitical Simulator). Os dois números estão hoje ocupados pelas escolhas da RD e da R2a; **por decisão do autor (quinta rodada, 16/11), nenhuma decisão já registrada é renumerada: a D-038 e a D-039 continuam sendo as escolhas da RD e da R2a, e as duas decisões de visual entram, na entrega do fim da `fase-r-final`, como D-043 (bonecos 3D estilizados, formas suaves) e D-044 (mapa 2D detalhado e fiel, interface em janelas flutuantes)**, registradas logo abaixo desta entrada, na entrega de 17/11. Onde os pedidos do autor dizem "D-038" e "D-039" sobre visual, leia-se D-043 e D-044.
  - **Nada disso se faz durante a Fase R.** Na `fase-r-final`, as telas antigas recebem só os tooltips (P-02), sem trocar o visual.
  - **Mapa (D-039 do autor):** ruas com nome pelas faces de logradouro do Censo 2022; rios, corpos d'água, estradas e nomes de localidades e povoados pela base cartográfica oficial (pesquisar a BC250 do IBGE); relevo sombreado de fonte pública de elevação; mancha urbana e área rural com aparências distintas; zoom da cidade inteira até a rua, com rótulos por nível e desvio de colisão; zonas e modos do mapa como cor translúcida sobre o mapa de base, com as fronteiras por cima; acontecimentos marcados no lugar; localidades da BC250 podem nomear zonas onde não há bairro do IBGE. O mapa continua 2D (GDD 17.20, item 1).
  - **Interface:** o mundo ocupa a tela inteira; janelas semitransparentes flutuam sobre ele; a interface fica nas bordas (barra de ícones embaixo, retrato e aprovação num canto, minimapa com data e velocidade no outro, poucos números no topo); mantêm-se as regras de clareza do ART_GUIDE (texto mínimo de 18 px, um destaque por vez, todo número explicado).
  - **Entrega ao terminar a `fase-r-final`, antes de parar, e só isto:** (1) as duas decisões no DECISOES; (2) GDD atualizado (3.1, 3.6, 3.7, 7.4, 13, 17.3 e 17.5) e ART_GUIDE reescrito para o estilo novo, como proposta para o autor aprovar; (3) pesquisa de fontes e tamanhos (faces de logradouro, BC250 e elevação: cobertura, licença, tamanho estimado; se não couber no repositório, gerar o mapa de base de cada cidade a partir de dados compactos quando ela entra em foco), registrada em PENDENCIAS como decisão do autor; (4) parte nova no ROADMAP, "RV — Redefinição visual", entre a Fase R e a Fase 6, com itens, testes e metas de desempenho (60 FPS com zoom e arraste; mapa de base de uma metrópole pronto em menos de 3 segundos); (5) protótipo no Godot com Mantena (MG) e uma metrópole: mapa de base detalhado com os modos do jogo por cima, uma janela flutuante e 3 bonecos em 3 expressões, com capturas em 1366×768 e 1920×1080 nos níveis de zoom da cidade inteira e da rua. Esta lista substitui a entrega dos retratos descrita acima.
  - **Base de arte:** só pacotes CC0 ou CC BY, com licença conferida e crédito em CREDITS.md; nenhum pacote de código novo.
  - **Referência da tela central:** se o autor salvar `docs/referencias/rv_tela_central.html`, esse arquivo é o alvo da tela central na parte RV, junto com a referência do Geopolitical Simulator.
- **Adendo (quinta rodada, 16/11): ids de zona.** Se a divisão do rural por distritos mudar ids de zona, os saves antigos não são migrados durante a Fase R (o autor começa carreiras novas a cada tag): o save antigo falha ao carregar com uma mensagem clara, sem travar o jogo, e a mudança fica registrada em PENDENCIAS.
- **Adendo (sexta rodada, 16/11): estilo dos bonecos (D-043), corrigido pelo autor.** Não é low-poly facetado: é 3D estilizado com formas suaves e proporções de desenho animado (referências de estilo: Two Point Hospital, Overcooked, Planet Coaster). Substitui o "Desenho" do item dos retratos acima.
  - **Personagens:** corpos e cabeças base com variações de formato; cabelo, barba e roupas como peças trocáveis; tom de pele, cabelo e roupas por material; expressões por animação do rosto (blendshapes ou ossos). Retrato renderizado em SubViewport, com cache (PortraitCache). Aparência sem efeito mecânico, com a diversidade do Censo (D-016).
  - **Cenários das telas fora do jogo, no mesmo estilo, como proposta na parte RV:** menu principal como um lugar que muda com a carreira (comitê de vereador, gabinete de prefeito, palácio), com o menu sobre a cena; seção eleitoral na urna; estúdio de TV na apuração; estúdio de foto do santinho na criação de personagem.
  - **Pesquisa pedida:** bases 3D estilizadas CC0 ou CC BY para personagens e cenários, com licença, qualidade e o que cada uma cobre, e onde a qualidade exigiria um artista 3D; registrada em PENDENCIAS como decisão do autor.
  - **Protótipo do fim da `fase-r-final`:** 3 personagens nesse estilo, em 3 expressões, e o menu como cena (além do mapa e da janela flutuante já pedidos).
  - **Referências de composição:** `docs/referencias/rv_tela_central.html` (salvo pelo autor) e as telas desenhadas pelo autor com o consultor, se estiverem na pasta (hoje há também `docs/referencias/tela_central.png`).

## D-043 — Retratos e personagens em bonecos 3D estilizados de formas suaves
- **Data:** decidida pelo autor em 2026-11-16 (quarta e sexta rodadas); registrada em 2026-11-17, na entrega do fim da `fase-r-final` · **Fase:** RV · Decisão do autor. O autor a chamou de D-038, número que ficou com as escolhas da RD. Origem: D-041 e os adendos da quarta e da sexta rodada, registrados logo acima, depois da D-042.
- **Pergunta:** como devem ser os retratos e os personagens do jogo, hoje em camadas 2D?
- **Decisão:** retratos e personagens passam a ser **bonecos 3D estilizados, de formas suaves e proporções de desenho animado**, no estilo de Two Point Hospital, Overcooked e Planet Coaster. **Não é low-poly facetado** (correção do autor na sexta rodada, que substitui o "Desenho" do item dos retratos da D-041).
  - **Personagens:** corpos e cabeças base com variações de formato; cabelo, barba e roupas como peças trocáveis; tom de pele, cabelo e roupas por material; expressões por animação do rosto (shape keys ou ossos) ou, pelo adendo da D-045, por troca de peças do rosto, com o mesmo nome de expressão.
  - **Retrato:** renderizado do boneco em SubViewport e guardado pelo PortraitCache (512, 256 e 96 px).
  - **Os mesmos bonecos** servem aos retratos, às posses (GDD 13.2), ao menu como cena e às cenas da RV-B (D-055).
  - **Cenários das telas fora do jogo, no mesmo estilo:** menu principal como um lugar que muda com a carreira (comitê de vereador, gabinete de prefeito, palácio), com o menu sobre a cena; seção eleitoral na urna; estúdio de TV na apuração; estúdio de foto do santinho na criação.
  - **Aparência sem efeito mecânico** e com a diversidade do Censo (D-016), como antes.
  - **Bases e artista:** pela D-045 (CC0: KayKit e Human Base Meshes do Blender; MPFB só como referência; Quaternius fora; artista só para cabeça, expressões e roupas, depois de um teste pago, e só se o autor reprovar as duas cabeças do teste A/B). Toda cabeça segue o contrato da cabeça substituível (ART_GUIDE).
- **O que o protótipo mostrou** (branch `rv-prototipo`, fora da main até a RL; prancha e capturas em `docs/referencias/rv/` nesse branch):
  - Duas cabeças pelo mesmo contrato. **A:** a cabeça estilizada CC0 dos Human Base Meshes, adaptada no Blender por script, expressões por shape keys, 7.094 triângulos. **B:** gerada por código, expressões por troca de peças do rosto, 4.786 triângulos visíveis no pior caso (8.322 no arquivo, contando as variantes das peças).
  - As duas passam na cena de teste de cabeça: nomes, pontos de troca, anel do pescoço (16 pontos, 5,5 cm), materiais, as 8 expressões e o piscar, orçamento. A B tem um material fora da lista (`interior_boca`), aceito no protótipo com aviso.
  - Os mesmos 3 personagens para cada cabeça, em neutra, sorriso e bravo, com cabelo e roupa trocáveis e cores por material; retratos com as 8 expressões e o piscar em 6 tons de pele, a 96 e 512 px.
  - O cache de retratos acertou 54 de 54 pedidos repetidos. Trocar A por B mexeu só no nó da cabeça: os 19 nós de fora ficaram iguais.
  - Menu como cena: comitê à noite com 6 personagens, "PALANQUE" e o subtítulo (D-046). Na Radeon integrada da máquina de desenvolvimento, com a máquina dividida com outros programas, o menu ficou entre **31 e 36 FPS em qualidade cheia** e entre 50 e 58 em qualidade leve (sem sombra nem brilho, 3D a 75% com FSR). **Não chegou a 60 FPS em qualidade cheia.**
  - Autocrítica do protótipo, resumida: a A tem o rosto mais acabado, mas parece criança e realista demais; a B chega mais perto das referências e lê melhor o bravo, mas tem rosto simples e pescoço fino e comprido. Nas duas, as três expressões só se distinguem com esforço a 96 px; o cabelo em casca parece capacete; os corpos e as roupas parecem sacos e as mãos, luvas. Só um artista resolveria rosto, cabelos, roupas e corpo. **Nenhuma das duas foi declarada boa o bastante.**
- **Fica para o autor decidir** (PENDENCIAS P-40): julgar as cabeças A e B pela prancha (aprovando uma, o briefing do artista fica arquivado; reprovando as duas, vale o teste pago da D-045); aprovar as duas convenções que o protótipo adotou no contrato (sufixo numérico do Blender ignorado; material `interior_boca` aceito); se a qualidade leve vira opção; a placa de vídeo-alvo dos 60 FPS; em que parte entram os retratos 3D no lugar dos 2D e as cenas da urna, da apuração e do estúdio de foto (proposta: RV-B, ROADMAP).
- **Alternativas:** low-poly facetado (o primeiro desenho, corrigido pelo autor); manter os retratos 2D em camadas; personagens realistas.
- **Como reverter:** os retratos 2D continuam na main até a troca; o protótipo está só no branch. Voltar atrás é não integrar o branch e manter o `PortraitRenderer` 2D.

## D-044 — Mapa da cidade 2D detalhado e fiel ao lugar real, com a interface em janelas flutuantes
- **Data:** decidida pelo autor em 2026-11-16 (quarta rodada); registrada em 2026-11-17, na entrega do fim da `fase-r-final` · **Fase:** RV · Decisão do autor. O autor a chamou de D-039, número que ficou com as escolhas da R2a. Origem: o adendo da quarta rodada, registrado logo acima, depois da D-042.
- **Pergunta:** o mapa de zonas sobre fundo liso e a tela de painéis fixos não dão a sensação de estar na cidade. Como deve ser o mapa e onde fica a interface?
- **Decisão, mapa:** o mapa da cidade continua **2D** (GDD 17.20, item 1), mas passa a ser **detalhado e fiel ao lugar real**, como referência o Geopolitical Simulator:
  - ruas com nome, pelas Faces de Logradouros do Censo 2022 (IBGE);
  - rios, corpos d'água, estradas e nomes de localidades e povoados, pela Base Cartográfica Contínua 1:250.000 (BC250) do IBGE;
  - relevo sombreado, de fonte pública de elevação;
  - mancha urbana e área rural com aparências distintas; praças e quarteirões (D-055);
  - zoom da cidade inteira até a rua, com rótulos por nível e desvio de colisão;
  - zonas e modos do mapa como cor translúcida sobre o mapa de base, com as fronteiras por cima; acontecimentos marcados no lugar;
  - localidades da BC250 podem nomear zonas onde não há bairro do IBGE;
  - luz de dia e de noite pela hora do relógio (D-055, RV-A).
- **Decisão, interface:** o mundo ocupa a tela inteira; janelas semitransparentes flutuam sobre ele; a interface fica nas bordas (barra de ícones embaixo, retrato e aprovação num canto, minimapa com data e velocidade no outro, poucos números no topo). Continuam as regras de clareza do ART_GUIDE: texto mínimo de 18 px, um destaque por vez, todo número explicado.
- **Restrições:** dados geográficos só de fontes oficiais ou abertas, com crédito; nada de OpenStreetMap sem o autor liberar (licença ODbL; os nomes de bairro foram liberados pela D-058); nenhum pacote de código novo.
- **Quando (D-055):** a pilha de janelas com Esc entra na RL; o mapa de base, a câmera suave travada no município, a luz de dia e de noite e o estilo final da interface entram na RV-A; os marcadores vivos e o mundo se mexendo, na RV-C.
- **O que o protótipo mostrou** (branch `rv-prototipo`; capturas em `docs/referencias/rv/` nesse branch):
  - O importador ganhou o modo `rv` (`rv baixar` e `rv mapa`), que baixa as faces de logradouro de MG e a BC250 e monta o mapa de base de cada município num arquivo compacto no cache (`cache/rv/mapa_base/{código}.rvmapa`), fora do Git.
  - Mantena (MG) e Belo Horizonte com o contorno, a mancha urbana, as zonas, as quadras remontadas das faces, as ruas com nome, os rios, as massas d'água, as rodovias, as ferrovias, a área edificada e as localidades da BC250. **O relevo não foi feito:** nenhuma fonte de elevação teve a licença confirmada.
  - As quadras fecham em cerca de 80% dos códigos (84% em Mantena, 80% em BH). Os nomes de rua vêm da fonte em caixa alta e sem acento.
  - Zonas translúcidas na cor de cinco modos (Minha força, Partido líder, Renda, Idosos, Urbana/rural); rótulos por zoom com desvio de colisão; uma janela flutuante semitransparente e arrastável com a ficha da zona; a interface nas bordas, na composição de `rv_tela_central.html`.
  - **Medidas**, três rodadas, com a máquina dividida com outros programas (outro Godot com janela e compilações de outros trabalhos), sincronia vertical desligada, Radeon integrada (Ryzen 5 7520U): mapa de BH pronto em 0,48 a 0,78 s; Mantena em 0,08 a 0,38 s (1,9 s na primeira rodada, que montava o mundo junto); zoom e arraste a 218–233 FPS em Mantena (1% pior de 55 a 131) e a 160–168 FPS em BH (1% pior de 29 a 96). As duas metas do autor (60 FPS com zoom e arraste; metrópole em menos de 3 s) passaram nessa máquina.
  - Tamanho: BH 17,8 MB (7,1 MB com gzip) no formato do protótipo; Mantena 0,54 MB.
- **Fica para o autor decidir** (PENDENCIAS P-39): a licença das faces de logradouro (não confirmada), os termos da BC250 e a fonte de elevação; como guardar ou gerar o mapa de base de cada cidade (baixado por município, só cidades pequenas no repositório ou gerado pelo importador no computador do jogador); remedir com a máquina livre e definir a placa de vídeo-alvo; o ART_GUIDE da RV, que é proposta.
- **Alternativas:** manter as zonas sobre fundo liso (GDD 17.3 até a RL); OpenStreetMap (fora pela licença, sem o autor liberar); mapa 2.5D da cidade.
- **Como reverter:** o mapa de base é uma camada abaixo das zonas e vem de arquivo gerado fora do Git; sem ele, o mapa de zonas atual continua funcionando. As janelas flutuantes trocam só a disposição dos painéis, não o conteúdo.

## D-045 — Bases 3D e artista para a parte RV (resposta do autor à P-28)
- **Data:** 2026-11-16 · **Fase:** RV (decidida durante a Fase R; nada de visual muda no jogo antes da RV) · Decisão do autor.
- **Bases livres CC0** para cenários, animações, corpos e multidão: KayKit (cenários e animações), a cabeça e o corpo estilizados dos Human Base Meshes do Blender (ponto de partida) e o MPFB só como referência de expressões e de diversidade (o programa e o addon não entram no repositório).
- **Artista 3D só para cabeça, expressões e roupas,** contratado depois de o autor aprovar o protótipo da RV. Antes do contrato, um **teste pago**: 1 cabeça, 3 expressões, 1 tom de pele e 1 cabelo, pelo briefing em `docs/BRIEFING_ARTISTA_3D.md`.
- **Cabeça substituível:** o protótipo usa uma cabeça provisória montada de base livre, no mesmo formato que o artista entrega (materiais, pontos de troca e nomes de expressão no ART_GUIDE, seção RV), para a troca não exigir mudança de código.
- **Quaternius fora** até haver confirmação da licença por escrito (a página de licença do site diverge da CC0 das páginas dos pacotes); o autor vai pedir. Nenhum arquivo da Quaternius entra no repositório até lá.
- **Aprovação em duas etapas** (ROADMAP, parte RV): o protótipo é aprovado por composição, câmera, cor, menu como cena e desempenho; o rosto só é julgado depois do teste do artista.
- **Escolhas tomadas ao escrever o briefing, a revisar:** as 3 expressões do teste são neutra, sorriso e preocupado; o orçamento é de até 8.000 triângulos para a cabeça e 6.000 para o cabelo; a cessão de direitos é total, por escrito, com crédito ao artista e sem uso de IA nem de assets de terceiros fora de CC0. O texto da cessão deve ser revisto por advogado antes de assinar.
- **Fecha a P-28.**
- **Adendo (16/11, ajuste do autor ao briefing):** as 3 expressões do teste do artista e do protótipo passam a ser neutra, sorriso e **bravo** (`expr_bravo`), no lugar de preocupado. O briefing ganhou uma cláusula de origem (trabalho original; sem assets de terceiros com licença incompatível com a cessão; CC0 listado, qualquer outra licença só com aceite por escrito; uso de ferramentas generativas "a definir pelo autor"), e o contrato da cabeça (ART_GUIDE, seção RV) passou a fixar a posição, a altura e a borda do encaixe do pescoço e a regra da multidão (figurantes com bases CC0 e os mesmos materiais, até 3.000 triângulos; cabeça do contrato só para pessoas com nome, com LOD automático em plano médio).
- **Adendo (16/11, teste A/B antes do artista):** o autor quer saber se dá para dispensar o artista. No protótipo da RV (fim da `fase-r-final`, sem interromper o que está rodando) são feitas duas cabeças pelo mesmo contrato: **A**, a cabeça estilizada CC0 do Blender adaptada; **B**, gerada por código, de formas suaves, com expressões por troca de peças do rosto. Cada uma ganha os mesmos 3 personagens em neutra, sorriso e bravo, com prancha comparativa e autocrítica (ROADMAP, parte RV). **O teste A/B vem antes de qualquer contato com artista. Se o autor aprovar uma das cabeças, o briefing fica arquivado; se reprovar as duas, o briefing é usado.** Para a B caber no contrato sem retrabalho futuro, o contrato da cabeça (ART_GUIDE) aceita dois modos de expressão pelo mesmo nome: shape keys ou troca de peças do rosto.

## D-046 — Nome do jogo para o jogador: "Palanque"
- **Data:** 2026-11-16 · **Fase:** RV (decidida durante a Fase R; nada muda antes da RV) · Decisão do autor.
- **Decisão:** o jogo passa a se chamar, para o jogador, **"Palanque"**, com o subtítulo **"Simulador de Carreira Política"**.
- **Motivo:** "Planalto" já é o nome de um simulador político brasileiro ativo (planalto.fun).
- **Alcance:** só o que o jogador vê: título da janela, menu, créditos, textos do jogo e página de loja. O código, a solução, os namespaces, os projetos e as pastas continuam como Planalto.
- **Quando:** na parte RV, junto com o menu como cena (ROADMAP, parte RV), com teste: nenhum texto visível ao jogador contém "Planalto" como nome do jogo; "Palácio do Planalto" como lugar continua permitido.

## D-047 — Autoria com colaboradores convidados
- **Data:** 2026-11-16 · Decisão do autor, ao abrir um repositório privado no GitHub para dividir o projeto com amigos.
- **Decisão:** os commits do autor e os feitos pelo assistente continuam só em nome de Arthur Albefaro, sem menção a IA. Colaboradores convidados pelo autor assinam os próprios commits com o próprio nome. O hook `.githooks/commit-msg` continua apagando linhas de atribuição a IA de qualquer commit; quem clona ativa o hook com `git config core.hooksPath .githooks` (nesta máquina ele está configurado com caminho absoluto para a cópia principal).
- **Não muda:** o assistente não é autor de nada; nunca altera `user.name`/`user.email` nem usa `--author`; nunca faz push sem pedido. Os créditos do jogo continuam como estão (CLAUDE.md); incluir colaboradores neles é decisão à parte.
- **Onde:** CLAUDE.md, seção Autoria.

## D-048 — Balanceamento da R3 com 200 campanhas por porte
- **Data:** 2026-11-16 · **Fase:** R3 · Tomadas no corujão, a partir do diagnóstico pedido pelo autor.
- **Diagnóstico do canal** (antes): o retorno por real era igual nos quatro canais; Cabos venciam porque davam a mesma presença da Rua e mais o apoio da zona inteira, mesmo rendendo a metade sem material; Redes e Material chegavam saturados à campanha oficial porque o robô gastava neles de 74% a 78% da verba na pré-campanha; o limite de cabos do art. 100-A vale para o robô e nunca o prendeu.
- **Ajuste:** Rua `k_presenca` 1,0 → 0,9; Redes `k_presenca` 4,5 → 6,75, `k_apoio` 0,036 → 0,054, fator de pré-campanha 0,6 → 0,4; Material `k_presenca` 0,9 → 1,08, fator de pré-campanha 0,6 → 0,5 (a pré-campanha rende o mesmo que antes); Cabos `k_presenca` 1,0 → 0,72, `k_apoio` 0,12 → 0,084; rivais: `npc_action_efficiency` 0,5 → 0,65, `bulk_drive` 0,8 → 1,2, `npc_effort_by_porte` de 1,05/1,1/2,0/5,0 para 0,95/1,4/3,5/4,4. Nenhuma regra de lei nem dado de município mudou.
- **Também:** a projeção do modelo de voto passou a guardar os temas em vetores (mesmas contas, mesma ordem, relatório idêntico), o que deixou o robô cerca de 5 vezes mais rápido; testes lentos com a categoria `Lento` (569 rápidos em cerca de 10 s; 203 lentos).
- **Resultado e o que não fechou:** ver PENDENCIAS P-26.
- **Como reverter:** `data/campaign/channels.json` e `data/campaign/config.json`.

## D-049 — Zona rural por distritos e o mapa de cidade pequena (P-24, item 10)
- **Data:** 2026-11-16 · **Fase:** R (teste do autor na `fase-r1`) · Escopo reduzido pelo autor: só simulação e leitura básica; contorno, estilo e rótulos por zoom ficam para a RV.
- **Rural por distritos:** a parte rural de cada distrito do IBGE (código e nome do agregado básico por setores do Censo 2022, já no cache) vira uma zona com o nome oficial; a da sede fica "<município> (rural)". Distrito com menos de **200** moradores rurais vai para o vizinho de maior divisa; o município pode passar do máximo de zonas do porte em até **4**; quando há mais grupos que o permitido, os menores se juntam e a zona misturada volta ao nome gerado; grupo com mais de uma zona ganha "(rural norte)", "(rural sul)". Parâmetros em `data/territory/config.json`, bloco `rural_por_distrito`.
- **Resultado:** 1.518 municípios ganharam zonas (28.135 → 31.026); `data/zonas` 19,5 MB (teto 20) e `data/geo/zonas` 22,6 MB (teto 30). Regenerado de novo na main, pela importação, com resultado idêntico ao do branch (só a data de geração muda).
- **Ids de zona mudaram em 2.024 municípios.** Versão da divisão gravada nos dados e no save (`ZonasPaths.Versao = 2`, `GameState.ZonasVersao`); save sem a versão, com outra versão ou com id de zona inexistente é recusado com a mensagem combinada e o jogo fica no menu. Sem migração (decisão do autor).
- **Mapa:** cidade pequena abre no núcleo urbano (mancha urbana em volta da zona urbana mais populosa), com o botão "Cidade inteira" / "Núcleo urbano"; nomes de zona com desvio de colisão, no máximo 12, abreviados com o nome inteiro no tooltip.
- **Como reverter:** `data/territory/config.json` e a importação `zonas` com o bloco desligado.

## D-050 — Escolhas da R4 (mandatos na tela central, indicadores reais, renovação) e da integração
- **Data:** 2026-11-16 · **Fase:** R4 · Tomadas no corujão, onde o GDD era vago. Cada uma se reverte no arquivo ou método citado.
- **Tela central (fecha a P-01):** mandato de vereador, prefeitura e entressafra saem das telas antigas (`MandateScreen`, `ExecutiveScreen` e `OutOfOfficeScreen`, removidas) e usam a moldura da campanha. As previsões da prefeitura (política, orçamento, crise, atos, repasse, convênio, entrega, assessor e voto) são montadas no Core e conferidas em 1.000 execuções (acerto de 80% ou mais).
- **Indicadores (17.17):** 11 indicadores guardados em unidade real: os 10 importados e a proteção social, que é índice do jogo. Valor de partida = dado importado. A lacuna é preenchida pela regra de `data/indicators/model.json` e marcada como estimativa. A conversão para a escala de jogo é linear, ou logarítmica para valores em reais. **Piso e teto de cada indicador = percentis 1 e 99 dos valores importados**, arredondados; parâmetros a confirmar. A comparação é com a mediana do mesmo porte na UF; os outros municípios não são simulados. A tendência do IDEB vem das 3 últimas edições do município e zera em 12 anos. A nota que o eleitor julga é 50 + variação no mandato + 0,25 × diferença para os semelhantes.
- **Renovação (17.16):**
  - Sorteio mensal de morte pela tábua da UF, com gerador próprio, para o mundo não mudar a sequência da campanha. O jogador nunca é sorteado.
  - Parâmetros de jogo, sem base em lei: posse do suplente em 7 dias, eleição da cadeira em 90 dias, luto oficial de 3 dias sem sessão da Câmara.
  - Eleição suplementar resolvida sozinha. Na indireta, os candidatos são os mais votados das duas maiores bancadas.
  - O vice que sai não tem substituto. Mandato que acaba antes do prazo da nova eleição fica com o presidente da Câmara.
  - Quem já está no mandato numa Câmara sem eleição jogada tem idade calibrada para cerca de 48 anos.
  - Pesos de origem dos que entram (liderança, família, equipe, comunicador) iguais para todos os partidos.
- **Critério de idade média que não passa inteiro:** em 40 anos e 8 municípios, a idade média muda +1,0 ano no conjunto e menos de 5 anos em toda Câmara de 15 cadeiras ou mais. Em 3 das 4 Câmaras de 9 a 11 cadeiras muda de 5,9 a 8,3 anos: com 9 cadeiras, a média de uma legislatura oscila uns 3 anos só pelo acaso. O teste aceita até 10 anos nas menores. A decidir pelo autor (P-34).
- **Sucessão:** regras em `SuccessionRulesConfig` (CF, arts. 56, 79, 80 e 81, por simetria; Código Eleitoral, art. 224, §§ 3º e 4º), conferidas no texto oficial (`docs/referencias/LEIS_CONFERIDAS.md`). A Lei Orgânica de cada município pode diferir; o jogo usa o padrão da CF.
- **Contas de campanha no mandato:** julgamento e representação andam só fora de campanha (na reeleição, a faixa de contas da campanha não aparece). A cassação segue a sucessão real. O cartão das contas fica no fim do resumo de cada modo e sobe ao topo só na semana com novidade. O salto da entressafra até a pré-campanha para na novidade das contas.
- **Entressafra:** 3 turnos por semana, com caminhada, visita, entrevista e posição. Não há verba nem equipe paga. Parâmetros em `data/career/offseason.json`.
- **BalanceSim de mandato:** o robô soma a previsão em pontos de presença na cidade, um juízo do robô e não uma regra do jogo. A campanha que leva ao mandato mantém presença mínima de 60 nas zonas; depois de seis preparações sem eleição, o piso sobe para 75 e depois 90, porque os rivais da R3 são mais duros. A opção `--mundo` roda indicadores e renovação junto com a campanha; a medição de referência roda sem ela, para comparar com a `fase-r3`.
- **Como reverter:** `data/indicators/`, `data/demography/renewal.json` e `entries.json`, `data/career/offseason.json`, `data/legislature/`, `data/executive/`, `data/works/`, `data/mandate/`, e os motores `IndicatorEvolution`, `RenewalEngine` e `WorldEngine`.

## D-051 — Critério de idade média dos vereadores nas Câmaras pequenas (resposta do autor à P-34)
- **Data:** 2026-11-16 · **Fase:** R4 · Decisão do autor.
- **Critério:** a variação menor que 5 anos na idade média entre a 1ª e a 10ª legislatura vale no conjunto e em cada Câmara de 13 cadeiras ou mais, como estava (o teste usava 15; passou a 13). Nas Câmaras de 9 a 11 cadeiras vale como **média de 20 sementes**; uma semente isolada pode passar de 5 anos. O que se mede nelas é deriva, não acaso.
- **Resultado** (40 anos, sementes 1000 a 1019): Manoel Urbano, 9 cadeiras, +1,1; Marcação, 9 cadeiras, +0,5; Mucajaí, 11 cadeiras, +0,8; Icapuí, 11 cadeiras, +0,6. Por semente, de −12,3 a +10,9. No conjunto, +1,0.
- **Teste:** `Idade_media_nas_Camaras_de_9_a_11_cadeiras_deriva_menos_de_5_anos_na_media_de_20_sementes` (categoria `Lento`). Saiu a tolerância de 10 anos por semente nas Câmaras pequenas.
- **Fecha a P-34 nesse ponto.**

## D-052 — reservada para o cansaço por veículo (branch `cansaco-veiculo`, registrada quando ele entrar na main)

## D-053 — Pacote de nomes privado (GDD 17.18; revê a D-035, item 15, só para uso privado)
- **Data:** 2026-11-16 · **Fase:** depois da `fase-r-final` · Decisão do autor.
- **Decisão:** o leitor do pacote de nomes (GDD 17.18) é construído no jogo. O pacote é de uso privado do autor e **nunca entra no repositório**, nem em `data/` nem nos builds exportados. O gerador do pacote também fica fora do repositório.
- **A suspensão da D-035, item 15, continua valendo para tudo o que está no repositório:** nenhum nome real em `data/`, `docs/`, código, testes ou commits; a neutralidade vale como antes.
- **O que o leitor faz:**
  - lê `user://mods/nomes/`;
  - troca nome, sigla, número e cor dos partidos pelo id fictício;
  - troca nome, nome de urna, nascimento e gênero das pessoas do município por id estável: `mun.<ibge>.pref`, `mun.<ibge>.vice` e `mun.<ibge>.ver.<ordem>` (ordem dos eleitos de 2024);
  - mostra uma marca discreta "pacote privado";
  - o campo `protecao_eventos` (padrão `false`) decide se eventos de escândalo, vida pessoal, doença e morte podem sortear quem tem nome trocado;
  - o save registra que foi criado com pacote.
- **Começa depois da tag `fase-r-final`** (PENDENCIAS P-35).

## D-054 — Parte RL (leitura do jogo) antes do estilo da RV · **escopo reorganizado pela D-055**
- **Data:** 2026-11-17 · **Fase:** depois da `fase-r-final` · Decisão do autor, depois de jogar a `fase-r4` em Mantena.
- **Pergunta:** o autor não conseguiu jogar a campanha ("entediante e bem confuso"; sem relógio, sem controle organizado das regiões, tudo em texto, sem saber a energia). Os sistemas da Fase R existem, mas a tela não os mostra (causa 5 da AUDITORIA_FASE3, ainda aberta).
- **Decisão:**
  - cria-se a parte **RL — Leitura do jogo** no ROADMAP, entre a `fase-r-final` e o estilo da RV;
  - a RL vem **antes de qualquer trabalho de estilo**;
  - o protótipo da RV continua no branch próprio, mas **não entra na main antes da RL**.
- **O que a RL faz:** relógio e linha do tempo do dia; painel "Você"; painel de zonas sincronizado com o mapa; mapa vivo; gráfico da intenção; retratos nas notícias e cartões; primeira campanha guiada por objetivos; "Fazer agora" como ação principal; e as 5 propostas das skills de jogo aprovadas pelo autor (pilha de janelas com Esc, painéis por sinal do Core, efeito proporcional à importância, "Reduzir piscadas", câmera suave e travada no município). Tudo na tela central atual, sem trocar fontes nem paleta, com a lógica no Core. As skills orientam; o GDD, o ART_GUIDE e o CLAUDE.md prevalecem.
- **Rumo cedo:** capturas ao autor quando relógio, painel Você e painel de zonas estiverem prontos, antes do mapa vivo.
- **Também nesta mensagem:** `.claude/settings.json` passa a ser versionado (não tem caminho local nem segredo); as skills `card-game` e `visual-novel` foram removidas da cópia local.
- **Muda o critério de pronto:** além de os sistemas funcionarem, o teste de fumaça confere que o jogador os vê (relógio andando, painel Você, painel de zonas sincronizado com o mapa, "Fazer agora" sem abrir a agenda, números flutuantes depois de uma ação, pilha de janelas com Esc), em 1366×768, 1920×1080 e 2560×1080.

## D-055 — Leitura e imersão em quatro partes: RL, RV-A, RV-C e RV-B
- **Data:** 2026-11-17 · **Fase:** depois da `fase-r-final` · Decisão do autor.
- **Pergunta:** o autor achou a `fase-r4` entediante, confusa e "só texto", e uma referência de tela com painéis e polígonos "pouco imersiva". Ele quer (A) a cidade real desenhada no mapa, (B) gente na tela em cenas e (C) o mundo se mexendo à volta.
- **Decisão:** entre a `fase-r-final` e a Fase 6, quatro partes, cada uma com tag própria, nesta ordem:
  1. **RL, leitura mínima** (`fase-rl`): relógio e linha do dia, painel "Você", painel de zonas sincronizado com o mapa, "Fazer agora" como ação principal, número flutuante depois da ação, retrato em notícia e cartão. Sem trocar estilo.
  2. **RV-A, cidade real** (`fase-rva`): o mapa de base do protótipo da RV vira o mapa do jogo (ruas com nome, rios, estradas, localidades, relevo sombreado, mancha urbana, praças e quarteirões; zoom até a rua; zonas translúcidas por cima; luz de dia e de noite pelo relógio). O estilo final (fontes, paleta, menu como cena, nome "Palanque") acompanha a RV-A. Metas: 60 FPS com zoom e arraste; metrópole em menos de 3 s.
  3. **RV-C, mundo vivo** (`fase-rvc`): carros e pedestres pela densidade, carro de som e cabos do rival em campo, multidão do tamanho do público, chuva por zona quando muda a previsão, personagens andando até o compromisso, áudio ambiente com volume nas Opções. Tudo pela hora e pelo estado do Core; nada sorteado só para enfeite que contradiga o Core.
  4. **RV-B, cenas** (`fase-rvb`): cenas curtas e puláveis em 3D estilizado, primeiro caminhada e comício, depois reunião, entrevista e notícia; resultado no fim da cena; opção para desligar; com a cabeça escolhida no teste A/B.
- **Antes de tudo:** a correção bloqueante dos traços na criação (feita em 17/11) e a `fase-r-final`.
- **Rumo cedo:** capturas ao autor no meio de cada parte; na RV-A e na RV-C, também um vídeo de 20 a 30 s em Mantena, de manhã e à noite.
- **O que muda na D-054:** a RL fica só com a leitura mínima. As propostas das skills foram repartidas: pilha de janelas com Esc e painéis por sinal do Core ficam na RL; câmera suave e travada no município vai para a RV-A; pulso, hachura de disputa, marcadores, efeito proporcional à importância e "Reduzir piscadas" vão para a RV-C. O gráfico da intenção, a primeira campanha guiada, o feed com fatores e os objetivos com origem ficam para o autor encaixar (PENDENCIAS P-37). O protótipo da RV continua no branch próprio e não entra na main antes da RL.
- **Restrições:** dados geográficos só de fontes oficiais ou abertas, com crédito; nomes de bairro do OpenStreetMap só com o autor liberando (liberados pela D-058); assets 3D e de som só CC0 ou CC BY, com crédito, nada da Quaternius.

## D-056 — Escolhas da fase-r-final (tooltips, Opções, mapa de cidade pequena e testes de fumaça)
- **Data:** 2026-11-17 · **Fase:** R (`fase-r-final`) · Tomadas no corujão. Cada uma se reverte no arquivo ou método citado.
- **Pausas novas desligáveis:** achado da fiscalização, mesa de negociação, cobrança do Livro, luto oficial e obra pronta, todas ligadas por padrão (`data/time.json`, `Clock.PauseIds`). Crise e decisão imediata continuam fixas. Com a cobrança desligada, o cartão não abre sozinho e a promessa fica em "Pede atenção" e no Livro. "Religar as dicas" também traz de volta as dicas fechadas com "Entendi".
- **Opções sem efeito na simulação:** Dificuldade, Realismo jurídico, Cúpula e o estatuto do partido próprio (disciplina, democracia interna, postura) ainda não mudam nada; o tooltip diz isso em vez de prometer efeito. Cabe ao autor decidir se ganham efeito ou saem (P-38).
- **Links do glossário fora da tela central:** o texto vira um rótulo com link do mesmo estilo (`Glossary.LinkTerms`), sem tocar no tema. "Realismo jurídico" fica sem link, porque "jurídico" no glossário remete ao advogado de campanha.
- **Núcleo urbano da cidade pequena:** passa a ser a mancha com mais zonas urbanas a partir de qualquer pedaço da zona urbana mais populosa (Mesquita/MG abria na vila afastada). Reverter: só o maior pedaço em `ZoneMapView.FrameNucleus`.
- **Cliques dos testes de fumaça:** o clique espera o controle pronto (até 20 s em tempo real) e confere que chegou; clique que não chega é falha com diagnóstico, nunca repetido. Toda carreira de teste usa a semente fixa 2028 (o Novo Mundo sorteava pelo relógio). A conferência do `explicacao` ignora o tooltip aberto pelo ponteiro real do mouse.

## D-057 — Régua da D-026 em faixas (resposta do autor ao relatório `docs/balance/regua_d026.md`)
- **Data:** 2026-11-17 · **Fase:** depois da `fase-r-final` · Decisão do autor.
- **Pergunta:** a régua da D-026 guardava os números da medição de 2026-11-15 (96%, 96%, 67% e 58% no partido mais forte; 13%, 13%, 8% e 0% no sorteado), feita com um robô que sorteava ações. Com o robô de hoje nenhuma alavanca testada fechava esses números sozinha.
- **Decisão:** a régua passa a ser em faixas, pelo texto original da D-026 ("grande maioria", "cerca de metade", "raramente", "nunca"):
  - **partido mais forte eleito:** pequeno e médio de 65% a 90%; grande e metrópole de 40% a 60%;
  - **partido sorteado eleito:** de 5% a 25% em todos os portes;
  - **sem campanha:** de 0% a 1%.
- **Nenhuma das alavancas A, B, C ou D do relatório é aplicada.** A B (reconhecimento herdado da força do partido) fica descartada: o jogador parado no partido forte da cidade pequena se elegia em 7 de 50 campanhas, o que viola "sem campanha, nunca".
- **Como fica a linha de base** (200 campanhas por porte, `main` em `fd873bc`): mais forte 73%, 84%, 46% e 49% (dentro); sorteado 19%, **39%**, 11% e 12% (o médio fora); sem campanha 0% nos quatro (dentro).
- **O sorteado do porte médio** é investigado antes de qualquer mudança: por que a chapa fraca faz cadeira tanto nesse porte (vagas, quociente, tamanho das listas, número de partidos) e qual parâmetro mexe só nisso. O resultado vai ao autor antes de mudar número.
- **Quando medir de novo:** só depois de o cansaço por veículo (branch `cansaco-veiculo`, D-052 reservada) entrar na main, porque lá o robô passou a pesar a imagem e os eleitos subiram.
- **Como reverter:** o critério é do BalanceSim e dos documentos; nenhum número do jogo mudou.

## D-058 — Nomes de bairro do OpenStreetMap liberados para as zonas e o mapa (atualiza a D-044)
- **Data:** 2026-11-17 · **Fase:** depois da `fase-r-final` · Decisão do autor. Atualiza a D-044 (e as restrições da D-055 e da P-39), que deixavam o OpenStreetMap fora sem o autor liberar.
- **Decisão:**
  - os nomes de bairro do OpenStreetMap entram **só onde o IBGE não tem bairro oficial**; onde o IBGE tem, vale o IBGE;
  - crédito "© colaboradores do OpenStreetMap" no mapa e em `assets/CREDITS.md`;
  - a base derivada segue a licença ODbL: `data/zonas/_fontes.json` registra o que veio do OSM, com a data da extração;
  - os nomes de zona do OSM entram com `nome_fonte` `"bairro_osm"`, e a Ficha da zona diz a origem do nome;
  - os brutos do OSM ficam no cache, fora do Git; se o download for bloqueado, o trabalho para e o autor é informado.
- **O que continua fora:** o resto do OpenStreetMap (ruas, quadras, edificações) segue fora; o mapa de base da RV continua pelas fontes do IBGE (D-044, P-39).
- **Como reverter:** o importador volta a dar o nome gerado às zonas sem bairro do IBGE e o crédito sai; os nomes vêm todos do arquivo de zonas.
- **Implementação (17/11, branch `osm-bairros`):**
  - **Extração:** Overpass API, uma consulta por UF (27), com pausa de 20 s, espera crescente quando o servidor está cheio (HTTP 502 algumas vezes) e identificação do importador no User-Agent. Pega nós, contornos e relações com `place=suburb|quarter|neighbourhood` e nome e relações `boundary=administrative` com `admin_level=10` e nome; de cada um, só nome, tipo e ponto (o do nó ou o centro do contorno). Brutos em `cache/osm/bairros_{UF}.json` (fora do Git), com a data no `.meta.json`. Modo `zonas-osm` só baixa; o modo `zonas` baixa o que faltar.
  - **Quem recebe:** só a zona **urbana** que ficaria com nome gerado. Bairro do IBGE e distrito do IBGE ficam como estão (leitura conservadora, a confirmar pelo autor: `distrito_ibge` é nome oficial). A zona rural com nome gerado continua com ele (escolha provisória, PENDENCIAS P-42).
  - **Atribuição:** ponto no polígono, pelo setor da malha do IBGE sem simplificar que contém o ponto (na divisa, o de menor código).
  - **Vários na zona:** vale o tipo de maior prioridade (limite de bairro e `suburb`, depois `quarter`, depois `neighbourhood`); no mesmo tipo, o que tem mais moradores perto (cada setor da zona conta para o lugar mais próximo do seu ponto interior); no empate, o mais perto do centro populacional da zona e a ordem alfabética.
  - **Sem repetir nome:** o nome do OSM não pode repetir bairro ou distrito do IBGE do município, o próprio município, o nome de outra zona com fonte oficial nem o nome gerado de outra zona; a zona mais populosa escolhe primeiro e a seguinte passa ao próximo candidato; sem candidato, fica o nome gerado.
  - **Filtros:** nome vazio, sem letra, com mais de 60 caracteres, com dois-pontos ou parênteses (anotação), igual ao município, igual a um nome excluído ("Zona Rural", "Bairro", "Loteamento") ou começando por rua, avenida, quadra, lote, bloco, edifício e semelhantes. Nome todo em maiúsculas passa para a caixa normal; a palavra "Bairro" no começo sai. Listas em `data/territory/config.json`, bloco `osm_bairros`.
  - **Jogo:** `nome_fonte` `bairro_osm` no Core e no DataValidator (que confere o registro ODbL em `_fontes.json`); a Ficha da zona diz a origem do nome pelo Core (`ZoneNameOrigin`); o mapa da cidade mostra "© colaboradores do OpenStreetMap" no canto inferior direito **só quando o município tem zona com nome do OSM** (crédito onde o dado aparece, sem dizer que há OSM onde não há).
  - **Os ids das zonas não mudam** (só os nomes): a divisão continua a 2 e os saves continuam abrindo. `data/geo/zonas/` sai idêntico.

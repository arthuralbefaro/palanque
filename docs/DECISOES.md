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
- **Decisão:** o jogador começa com 17 a 45 anos; NPCs continuam entre 25 e 45 (`npc_age` em social_origins.json). A idade de 18 anos para vereador é aferida no prazo do registro, 15 de agosto (D-030); quem não a tiver na primeira eleição começa fora da disputa, e a carreira pula para a primeira eleição em que é elegível. Cada profissão tem idade mínima (`min_age` em professions.json, de 17 a 25), e entra a profissão Estudante (17). Estado civil, filhos e cônjuge são gerados coerentes com a idade pelas regras de data/family.json. A chave dos estados civis em family.json é o id em snake_case ("uniao_estavel").
- **Testes:** idade aferida em 15/8 (aniversário em 15/8 e em 16/8) e na posse para prefeito; NPCs entre 25 e 45; profissão nunca abaixo da idade mínima e Estudante aos 17; família sem incoerência em 10.000 sorteios; configuração inválida reportada; jogador de 17 anos espera a eleição de 2032 e a disputa até a apuração; teste de fumaça `creator17`.
- **Como reverter:** `starting_age` em social_origins.json e `min_age` em professions.json.


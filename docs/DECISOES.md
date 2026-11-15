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
- **Data:** 2026-11-15 · **Fase:** 2
- **Decisão:** prenomes = os 20 mais frequentes por sexo em cada UF e no Brasil, pela API de nomes do IBGE (Censo 2010), importados por `dotnet run --project tools/Planalto.DataImporter -- nomes`, sorteados pela frequência; acentos restaurados por mapa em data/names/names.json (a API devolve sem acento). Sobrenomes: lista de sobrenomes frequentes com pesos (conteúdo do jogo; o IBGE não publica ranking de sobrenomes). Pessoas não binárias sorteiam entre as duas listas. Nome de urna por padrões de names.json, com título de profissão por gênero.
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

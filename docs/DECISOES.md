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

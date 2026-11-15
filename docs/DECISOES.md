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

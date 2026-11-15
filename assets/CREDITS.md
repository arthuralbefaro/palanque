# Créditos

## Planalto
Criação, design e desenvolvimento: Arthur Albefaro.

## Dados
Os dados dos municípios em `data/municipios/` vêm de fontes oficiais e públicas, cruzadas pelo código IBGE:

- **IBGE — Instituto Brasileiro de Geografia e Estatística**
  - API de Localidades: lista de municípios, UF, região, microrregião e região geográfica imediata.
  - Estimativas da População (SIDRA, tabela 6579).
  - Censo Demográfico 2022 (SIDRA, tabelas 10211, 9514, 10296, 10198 e 10061): situação do domicílio, idade, rendimento, religião e nível de instrução.
  - Malha Municipal 2025: coordenadas (centroide ou ponto interior) e área dos municípios.
- **TSE — Tribunal Superior Eleitoral**, Portal de Dados Abertos
  - Perfil do eleitorado (atual).
  - Códigos oficiais de UF e municípios segundo o TSE e o IBGE.
  - Consulta de vagas das Eleições Municipais 2024.

Ano, endereço e data de download de cada fonte estão em `data/municipios/_fontes.json`.

### Cenário "Brasil 2026", aparência e nomes
- **TSE — Tribunal Superior Eleitoral**, Portal de Dados Abertos: candidaturas e eleitos de 2020, 2022, 2024 (com eleições suplementares) e 2026, votação por partido de 2024 e 2026, coligações de 2024 e 2026 e vagas de 2026. Usados só para os fatos estruturais do cenário (cadeiras, partidos de prefeitos e governadores, força local); todos os nomes de partidos e políticos do jogo são fictícios.
- **IBGE**, Censo Demográfico 2022, SIDRA tabela 9605 (população por cor ou raça e UF): pesos regionais de tom de pele dos personagens.
- **IBGE**, Censo Demográfico 2010, API de nomes: prenomes mais frequentes por sexo, UF e década de nascimento.
- Classificação ideológica dos partidos reais (só no pipeline de dados): Bolognesi, B.; Ribeiro, E.; Codato, A.; Silva, B. F. *O desaparecimento do centro ideológico no sistema partidário brasileiro: a classificação mais atualizada dos experts*. Opinião Pública, v. 31, e31120, 2025.

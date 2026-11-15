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

# Créditos

## Planalto
Criação, design e desenvolvimento: Arthur Albefaro.

## Dados
Os dados dos municípios em `data/municipios/` vêm de fontes oficiais e públicas, cruzadas pelo código IBGE:

- **IBGE — Instituto Brasileiro de Geografia e Estatística**
  - API de Localidades: lista de municípios, UF, região, microrregião e região geográfica imediata.
  - Estimativas da População (SIDRA, tabela 6579).
  - Censo Demográfico 2022 (SIDRA, tabelas 10211, 9514, 10296, 10198 e 10061): situação do domicílio, idade, rendimento, religião e nível de instrução.
  - Malha Municipal 2025: coordenadas (centroide ou ponto interior) e área dos municípios; contornos simplificados do mapa 2.5D (data/geo/).
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

### Zonas das cidades
- **IBGE**, Censo Demográfico 2022: Malha de Setores Censitários (versão definitiva) e Agregados por Setores Censitários (básico, demografia e rendimento do responsável). Os setores são agrupados em zonas pelo DataImporter (`data/zonas/` e `data/geo/zonas/`); fonte, método e lacunas em `data/zonas/_fontes.json`.
- **IBGE**, Censo Demográfico 2022, resultados da amostra por áreas de ponderação (tabelas de religião e de nível de instrução) e composição das áreas de ponderação por setor: religião e escolaridade por zona nos municípios com duas ou mais áreas.
- **TSE**, Portal de Dados Abertos, eleitorado por local de votação de 2024: eleitores que votam em cada zona.

### Indicadores reais de partida
- **Ministério da Saúde, DATASUS**: Sistema de Informações sobre Mortalidade (SIM, declarações de óbito por município de residência, 2022 a 2024), Sistema de Informações sobre Nascidos Vivos (SINASC, por município de residência, 2022 a 2024) e Cadastro Nacional de Estabelecimentos de Saúde (CNES, leitos, competência de setembro de 2026). Microdados do FTP do DATASUS, agregados por município pelo DataImporter.
- **IBGE**, Produto Interno Bruto dos Municípios (SIDRA 5938, ano de 2023): PIB e valor adicionado por setor.
- **IBGE**, Censo Demográfico 2022 (SIDRA 6803 e 6805): domicílios ligados à rede geral de água e à rede de esgoto.
- **IBGE**, PNAD Contínua trimestral (SIDRA 4099): taxa de desocupação do Brasil e das UFs. **IBGE**, Sistema de Contas Nacionais (SIDRA 6784): PIB do Brasil.
- **Tesouro Nacional**, SICONFI (API do Data Lake): Declaração de Contas Anuais (receitas e despesas por função) e Relatório de Gestão Fiscal (receita corrente líquida e despesa com pessoal) dos municípios e dos estados. Dados sob a licença ODbL declarada pelo Tesouro.
- **INEP**, IDEB dos anos iniciais do ensino fundamental por município e rede (edição de 2025, com a série desde 2005).
- **Ministério do Trabalho e Emprego**, RAIS 2025, microdados de estabelecimentos: vínculos formais ativos em 31/12 por município.
- **Banco Central do Brasil**, Sistema Gerenciador de Séries Temporais: IPCA (série 433), meta da taxa Selic (432), dólar comercial (1), dívida bruta do governo geral (13762) e dívida líquida do setor público (4513).

Endereço, ano, data de download, regras de substituição e lacunas de cada indicador em `data/indicadores_reais/_fontes.json`. Os números são o ponto de partida do jogo; a partir do primeiro dia, tudo evolui pela simulação.

### Mortalidade e população projetada
- **IBGE**, Projeções da População do Brasil e das Unidades da Federação, revisão 2024: tábuas de mortalidade por sexo e idade quinquenal (ano de 2024, Brasil e 27 UFs) e população total projetada por UF, de 2022 a 2070.
- **IBGE**, Tábuas Completas de Mortalidade para o Brasil, 2024 (homens, mulheres e ambos os sexos): tábua do Brasil e perfil por idade simples das tábuas das UFs.

Endereço, data de download e método em `data/demography/_fontes.json`.

## Código de terceiros
- **blast** (descompressor do formato "implode" da PKWare DCL), Copyright (C) 2003, 2012, 2013 Mark Adler, de `zlib/contrib/blast`, sob a licença zlib. Portado para C# e alterado em `tools/Planalto.DataImporter/Rd/Blast.cs` (o aviso da licença está no arquivo); usado só pelo importador, para ler os arquivos .dbc do DATASUS.

## Fontes tipográficas
Todas sob a SIL Open Font License 1.1; a licença de cada uma está em `assets/fonts/`.
- **Inter**, Copyright 2020 The Inter Project Authors (https://github.com/rsms/inter).
- **Oswald**, Copyright 2016 The Oswald Project Authors (https://github.com/googlefonts/OswaldFont).
- **Source Serif 4**, Copyright 2014 The Source Serif 4 Project Authors (https://github.com/adobe-fonts/source-serif).

## Ícones
Ícones da interface em `assets/icons/ui/`: desenho próprio do projeto.

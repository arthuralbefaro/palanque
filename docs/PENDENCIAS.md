# Pendências

Bloqueios e decisões novas surgidos durante o corujão da Fase R (autorização de 2026-11-16). Cada item: parte, o que ficou faltando, o que foi feito provisoriamente, o que o autor precisa fazer e como reverter.

## R1 — com pendências

### P-01 · Mandato de vereador e prefeitura ainda nas telas antigas
- **Falta:** o item "Painéis com o conteúdo atual" da R1 pede que mandato e prefeitura virem áreas da tela central e que as telas antigas saiam. Só a campanha foi para a tela central.
- **Provisório:** fora da campanha, o jogo usa as telas de mandato, prefeitura e entressafra como estavam. Na campanha de reeleição, o vereador já usa a tela central.
- **Para fechar:** levar pauta, votos e ações de gabinete (mandato) e orçamento, políticas, secretarias e crise (prefeitura) para áreas do trilho, com tooltips em três partes; o tempo corrente do mandato (1 s por dia em 1x) já está em `data/time.json`.
- **Do autor:** nada; é trabalho pendente.

### P-02 · Tooltips em três partes só na tela central e na Enciclopédia
- **Falta:** o teste `explicacao` percorre a tela central (7 áreas, 4 modos do mapa, ficha de zona) e a Enciclopédia. Criação de personagem, Novo Mundo, urna, apuração, debate, mandato e prefeitura ainda têm tooltips antigos ou nenhum.
- **Para fechar:** estender o teste a essas telas e preencher os tooltips.

### P-03 · Opções → Tempo sem tela
- **Falta:** as pausas automáticas são configuráveis no código (`Settings.SetPause`, padrões em `data/time.json`), mas não há tela de Opções para o jogador ligar ou desligar cada uma, nem para religar as dicas.
- **Provisório:** valem os padrões de `data/time.json`.

### P-04 · Artigos de lei citados de memória nos tooltips do material de campanha · **adiada para a R3 (D-037)**
- **O quê:** os tooltips de adesivo (Lei 9.504/1997, art. 38), bandeira (art. 37, § 6º) e peça de redes (art. 57-C) e o verbete "O que a lei permite na propaganda" citam artigos de memória.
- **Decisão do autor (D-037):** a conferência fica para a R3. Até lá, esses textos e os verbetes "Pré-campanha" e "Horário eleitoral gratuito" mostram a marca "(artigo a conferir)" (`a_conferir` em `data/encyclopedia/glossary.json`).
- **Para fechar na R3:** conferir no texto oficial, corrigir o que estiver errado e tirar a marca.

### P-05 · Intenção por zona é estimativa · **fechada (D-037)**
- **O quê:** na R1, a intenção de voto de cada zona é a da cidade repesada pelo perfil de idade e pela situação (urbana ou rural) da zona (`ZoneProjection`). A tela diz "estimativa por zona". O voto calculado zona a zona, com presença por zona, é a R2a.
- **Mapeamento de idade (escolha de jogo):** a faixa de 15 a 19 anos do Censo é repartida igualmente: 2/5 para 16 e 17 anos e 2/5 para 18 a 34; a de 30 a 39, metade para 18 a 34 e metade para 35 a 59.
- **Decisão do autor (D-037):** a aproximação basta até a R2a.

### P-06 · Termos de uso das fontes do IBGE · **fechada (D-037), com lembrete de lançamento**
- **O quê:** a R1 usa a Malha de Setores e os agregados por setor do Censo 2022 citando a fonte (`data/zonas/_fontes.json` e `assets/CREDITS.md`), como autorizado (GDD 17.20, item 7).
- **Decisão do autor (D-037):** termos aceitos para uso com citação da fonte "IBGE".
- **Antes do lançamento:** conferir o ofício original.

### P-07 · Boa Esperança do Norte (MT) sem setores
- **O quê:** o município foi criado depois do Censo 2022 e não tem setores no agregado. Ficou com uma zona só, com o contorno municipal e sem perfil por zona; a lacuna está em `data/zonas/_fontes.json`. É a mesma lacuna já registrada para o perfil municipal (D-011 e 1B).

### P-08 · Tamanho de data/zonas/ · **fechada (D-037)**
- **O quê:** os atributos das zonas (`data/zonas/`, cerca de 15 MB) não tinham teto; o teto de 30 MB do autor vale para os contornos (`data/geo/zonas/`, 20,5 MB).
- **Decisão do autor (D-037):** 15 MB aceitos; teto de 20 MB para `data/zonas/`. O importador mede a saída e falha, sem gravar, se passar (`ZonasPaths.MaxZonasBytes`); o teste `Toda_zona_tem_contorno_e_os_contornos_cabem_no_teto` confere a pasta.

### P-09 · Mapa em cidade pequena (autocrítica visual, segunda rodada)
- **O quê:** em cidade pequena, as zonas urbanas são minúsculas perto da zona rural; o enquadramento inicial aproxima o núcleo urbano, mas os rótulos de zonas vizinhas se sobrepõem ("Centro" e "Zona Nordeste" em Mesquita/MG) e, quando um partido lidera em todas as zonas, o modo Partido líder fica de uma cor só. Captura: `docs/referencias/r1/central_cidade_pequena_1920.png`.
- **Para fechar:** rótulos com desvio de colisão; o modo "Minha força" da R2a (presença do jogador por zona) dá contraste mesmo quando o líder é o mesmo; considerar subdividir a zona rural em cidades pequenas com rural dominante.

## RD — pendências

### P-10 · Votação de 2024 por zona (GDD 17.20, item 6) não foi ligada ao cenário
- **Falta:** levar às zonas a votação real de 2024 no cenário Brasil 2026. Os arquivos por seção do TSE estão no cache (`rd/tse_votacao_secao_2024_{UF}.zip`, 26 UFs, cerca de 3 GB) e cada local de votação já cai numa zona (`ZonasRdImport`), mas a soma por partido fictício exige passar pela correspondência partido real → fictício, que só existe dentro do pipeline do cenário.
- **Provisório:** a força dos partidos em cada zona vem do modelo (afinidade dos segmentos repesada pelo perfil da zona), não da votação real de 2024 por zona.
- **Para fechar:** somar a votação de vereador por seção em cada zona, converter pelo mesmo mapa do `ScenarioImport` e gravar em `data/parties/scenarios/brasil_2026/` (só ids fictícios).

### P-11 · Eleitores por zona: onde se vota ou onde se mora · **a confirmar pelo autor**
- **O quê:** o autor aprovou repartir os eleitores pelos locais de votação do TSE. Feito, o resultado mostra onde as pessoas votam, não onde moram: 4.344 zonas habitadas (8,6 milhões de moradores) não têm local de votação e ficam com zero eleitor, e zonas centrais ficam com várias vezes os seus moradores.
- **Escolha conservadora:** o arquivo guarda o dado do TSE (`eleitores`, com a fonte), mas o modelo de voto reparte o eleitorado do município pela população de 16 anos ou mais de cada zona. A Ficha da zona pode mostrar os dois números.
- **Como reverter:** `ZoneElectorateBuilder.ZoneVoters` (uma linha): voltar a usar `Zona.Eleitores`.

### P-12 · Parâmetros de jogo novos que só o autor pode confirmar
- `data/demography/config.json`: pesos de saúde e estresse no fator pessoal de mortalidade, curva depois dos 89 anos, 12 sorteios por ano (só o intervalo de 0,6 a 2,5 veio do GDD).
- `data/indicators/definitions.json`: faixas plausíveis de cada indicador e mínimo de 300 nascidos vivos em 3 anos para a taxa de mortalidade infantil valer por município.
- Homicídios: só agressão (X85 a Y09); intervenção legal (Y35 e Y36) fora.
- População depois de 2070: segue a taxa de 2069 para 2070 da projeção da UF.

### P-13 · Termos de uso das fontes não IBGE
- **O quê:** a RD usa dados do Tesouro Nacional (licença ODbL declarada), TSE (CC BY declarada), INEP, DATASUS, MTE e Banco Central (dados abertos sem licença explícita localizada), sempre citando a fonte em `_fontes.json` e em `assets/CREDITS.md`, como o corujão autoriza. O autor confere os termos de cada uma antes do lançamento.

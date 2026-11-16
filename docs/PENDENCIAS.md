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

### P-14 · Transferências (FPM e FPE) e saldo do CAGED não importados
- **Falta:** as transferências constitucionais (Tesouro Transparente, CSV mensal) e o saldo de emprego formal do Novo CAGED, que o GDD 17.17 lista como fonte da inclinação inicial.
- **Provisório:** a receita corrente do SICONFI já inclui as transferências recebidas; o emprego formal tem o estoque da RAIS 2025, sem a inclinação.
- **Para fechar:** dois leitores novos no modo `rd` (as colunas do CSV do Tesouro precisam ser conferidas) e os campos correspondentes em `data/indicadores_reais/`. Entra junto com o modelo de evolução da R4, que é quem usa a inclinação.

### P-13 · Termos de uso das fontes não IBGE
- **O quê:** a RD usa dados do Tesouro Nacional (licença ODbL declarada), TSE (CC BY declarada), INEP, DATASUS, MTE e Banco Central (dados abertos sem licença explícita localizada), sempre citando a fonte em `_fontes.json` e em `assets/CREDITS.md`, como o corujão autoriza. O autor confere os termos de cada uma antes do lançamento.

## R2a — com pendências

### P-15 · Princípio a não passa em cidade grande e metrópole
- **Critério:** nenhum tipo de compromisso como melhor escolha em mais de 40% das semanas; nenhum par (tipo, zona) em mais de 25%.
- **Medido** (`docs/balance/r2a.md`, 48 campanhas por porte, 8 cidades): tipo com a opção de maior ganho da semana em 30,2% (pequeno), 34,1% (médio), **41,4% (grande)** e **59,8% (metrópole)**; par mais repetido em 22,6%, 15,5%, 12,6% e 11,5%.
- **Por quê:** o cansaço deixa os tipos quase empatados toda semana (numa metrópole: caminhada 19,2, reunião 18,7, gravação 17,6, entrevista 17,3 votos por turno), mas a caminhada tem 12 a 20 zonas para escolher e ganha por margem mínima; abrir zona nova sempre rende mais que reforçar zona já trabalhada.
- **O que foi ajustado** (sete rodadas): reunião só com liderança do público na zona; teto e retorno decrescente no apoio de conversa; cansaço da visita por pessoa; evento da cidade dividindo cansaço com a caminhada e com bônus só para o público dele; alcance pela fatia da cidade (limitado a 10 vezes); entrevista e gravação com alcance maior; menos eventos por semana.
- **Para fechar:** os sistemas da R2b (verba por canal, equipe, plataforma) e da R3 (tema em alta, rivais que respondem à entrada no reduto) acrescentam razões para não caminhar; o BalanceSim é rodado de novo em cada uma. Se ainda não passar na metrópole, a regra a rever é o retorno de abrir zona nova.
- **Também não foi feito:** a rodada de 200 campanhas por porte do critério (foram 48, por tempo de máquina: cerca de 40 minutos por rodada completa). Comando: `dotnet run --project tools/Planalto.BalanceSim -- --campanhas 200 --cidades 10 --saida docs/balance/r2a.md`.

### P-16 · Lint de rastro e eventos antigos que não conversam com a agenda
- **Falta o lint** "toda escolha relevante grava uma entrada no Livro ou uma lembrança em alguém" (princípio d). As escolhas novas (pedido de liderança, convite) gravam; os 32 eventos antigos, não.
- **Eventos antigos:** 12 escolhas ainda mexem em "energia" (hoje convertida em estresse, com sinal estranho em duas delas); `campanha_comicio_chuva` dispara sem comício marcado; `campanha_carreata` fala de ações que não existem mais; um evento trata de promessa a liderança fora do Livro.
- **Para fechar:** é o item da R3 "eventos atuais ligados a pessoas, zonas e fatos do save".

### P-17 · Números de jogo ainda no código
- `CityBuilder` (chance de segunda zona, idade, afinidade, preço e influência das lideranças, favor inicial), `AnswerLeader` (custo do pedido de dinheiro), `LeadersWeek` (limiar de convite, queda de afinidade por semana), `RaceSetup.BuildTerritory` (chance de dois redutos) e o id `"caminhada"` usado pelo motor.
- **Para fechar:** levar para `data/campaign/leaders.json` e `data/territory/config.json`.

### P-18 · Caixa aperta demais nas cidades médias e maiores
- **Medido:** o robô gasta 32% (médio), 38% (grande) e 49% (metrópole) dos turnos em escolhas forçadas, quase todas jantar de arrecadação, porque os custos crescem com o porte e a arrecadação por jantar, menos.
- **Provisório:** fica como está; a R2b troca o gasto por clique pela verba por canal e a R3 traz os doadores.

### P-19 · Sobras da interface da R2a
- Célula da agenda corta "Caminhada" (o nome inteiro está no tooltip); faixas por quintil exageram diferenças pequenas em cidade de 3 a 5 zonas, e o mapa não fica "quase todo cinza" como no relato 1 (decisão do autor: quintil ou intervalo de valor); a legenda ocupa cerca de 300 × 250 px do mapa; pausa e faixa de decisão em cor de atenção ao mesmo tempo; fichas com mais de 3 blocos.
- Marcar uma semana cheia custa 3 ou mais cliques por compromisso, sem atalho de repetir; o estresse chega a 100 sem a tela mostrar consequência.
- As capturas de `docs/referencias/r2a/` e parte do roteiro foram feitas antes da última calibração: os números de presença e a regra da reunião (agora só com liderança do público na zona) podem diferir do texto.
- Sem ficha de rival, sem seta nas dicas, sem as animações do ART_GUIDE, seção 8.

## R2b — pendências (Core; a interface está em andamento)

### P-20 · BalanceSim da R2b fora do critério em parte dos portes
- **Medido** (`docs/balance/r2b.md`, 48 campanhas por porte): tipo de compromisso em 27,0% (pequeno), 35,9% (médio), 36,3% (grande) e **60,6% (metrópole)**; par (tipo, zona) em **29,8% (pequeno)**, 18,1%, 14,2% e 12,8%; canal de verba mais repetido em **45,0% (pequeno, Cabos)**, **49,0% (médio, Rua)**, **49,5% (grande, Rua)** e 39,2% (metrópole); decisões com troca por semana entre 1,8 e 2,4 (critério: 1 ou mais, passa).
- **O que foi ajustado** (3 rodadas): rendimento por real igualado entre canais; cabos dão presença; salários e escala da verba pela metade; saturação da verba repetida.
- **Por quê:** os canais rendem quase o mesmo por real e o robô gasta quase tudo na pré-campanha, em Redes e Material; sobra pouco para Rua e Cabos na campanha oficial.
- **Para fechar:** rodar de novo com os doadores da R3 (o caixa muda tudo) e dar ao robô a regra de guardar caixa para a campanha oficial.

### P-21 · O caixa não comporta equipe e verba antes dos doadores
- **Medido:** com folha e verba, o robô passa de 15% (pequeno) a 61% (metrópole) dos turnos arrecadando e se elege menos na metrópole (9 de 24 contra 16 de 24 na R2a), com os preços já pela metade.
- **Para fechar:** os doadores, o fundo do partido e o financiamento coletivo da R3; depois, voltar os salários ao valor do GDD.

### P-22 · O que ficou para a R3 por depender dela
- Opinião do marqueteiro, do tesoureiro e do advogado; agenda dos rivais vista na véspera; ação do advogado; "posição fora da faixa do partido esfria a direção"; rivais que se posicionam e atacam contradições; tema em alta.
- A plataforma não atravessa eleições (o Livro guarda as posições assumidas, mas o estado da plataforma nasce de novo a cada campanha).

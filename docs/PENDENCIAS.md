# Pendências

Bloqueios e decisões novas surgidos durante o corujão da Fase R (autorização de 2026-11-16). Cada item: parte, o que ficou faltando, o que foi feito provisoriamente, o que o autor precisa fazer e como reverter.

## R1 — com pendências

### P-01 · Mandato de vereador e prefeitura ainda nas telas antigas — **fechada na R4 (tag fase-r4)**
- **Fechamento:** mandato de vereador, prefeitura e entressafra estão na tela central, com as áreas do trilho, previsões montadas no Core e tooltips em três partes; as telas antigas foram removidas (D-050).
- **Falta:** o item "Painéis com o conteúdo atual" da R1 pede que mandato e prefeitura virem áreas da tela central e que as telas antigas saiam. Só a campanha foi para a tela central.
- **Provisório:** fora da campanha, o jogo usa as telas de mandato, prefeitura e entressafra como estavam. Na campanha de reeleição, o vereador já usa a tela central.
- **Para fechar:** levar pauta, votos e ações de gabinete (mandato) e orçamento, políticas, secretarias e crise (prefeitura) para áreas do trilho, com tooltips em três partes; o tempo corrente do mandato (1 s por dia em 1x) já está em `data/time.json`.
- **Do autor:** nada; é trabalho pendente.

### P-02 · Tooltips em três partes só na tela central e na Enciclopédia — **fechada na fase-r-final**
- **Fechamento:** menu, Opções, Novo Mundo, criação (as cinco abas e o partido próprio), seletor de cidade, mapa do estado, debate, urna, apuração e fundação de partido ganharam tooltip em três partes, motivo nos bloqueados e link nos termos do glossário (sem mudar o visual); o `explicacao` percorre essas telas e falha se faltar um.
- **Falta:** o teste `explicacao` percorre a tela central (7 áreas, 4 modos do mapa, ficha de zona) e a Enciclopédia. Criação de personagem, Novo Mundo, urna, apuração, debate, mandato e prefeitura ainda têm tooltips antigos ou nenhum.
- **Para fechar:** estender o teste a essas telas e preencher os tooltips.

### P-03 · Opções → Tempo sem tela — **fechada na fase-r-final**
- **Fechamento:** tela de Opções (menu e barra da tela central) com a aba Tempo: cada pausa de `data/time.json`, inclusive as cinco novas do mandato e da prefeitura, liga e desliga com tooltip em três partes, e o botão Religar as dicas; grava por `Settings`; alvo `opcoes`.
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

### P-09 · Mapa em cidade pequena (autocrítica visual, segunda rodada) — **fechada na fase-r-final**
- **Fechamento:** conferido com capturas de Mesquita (MG) e Mantena (MG) em 1366×768 e 1920×1080 (`docs/referencias/r-final/`): nomes sem sobreposição, abertura em Minha força com as zonas em faixas, e Partido líder com o tom pela vantagem (Mesquita hoje tem 3 líderes; Mantena, 2). Corrigido o enquadramento: em Mesquita o mapa abria numa vila afastada (a Zona Sudoeste junta sede e vila) e agora abre na sede; o `mapa_pequeno` inclui Mesquita e confere o Centro no núcleo e o contraste dos dois modos.
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

## R2b — com pendências

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

### P-23 · Sobras da interface da R2b
- **Resolvido da P-19:** a célula da agenda mostra "Caminhada" inteiro; há "Repetir a semana anterior"; o estresse acima do limite ganhou cartão de aviso. O resto da P-19 continua (quintis, legenda grande, pausa e faixa em cor de atenção ao mesmo tempo, fichas com mais de três blocos, sem ficha de rival, sem seta nas dicas, sem animações).
- **Fora do ART_GUIDE:** a área Campanha rola na primeira visita (com a dica aberta); o bloco de Objetivos ocupa cerca de 410 px (o guia pede 176) e corta a linha de origem (inteira no tooltip); as opções do cartão de decisão têm 3 linhas (o guia pede 2); a previsão de mudança de posição pede bastante rolagem no painel de 400 px.
- **Capturas** por porte são de pré-campanha, com Rua e Cabos fechados; os dois canais abertos só aparecem em `verba_resultado_1920.png` e `comicio_sem_rua_1920.png`.
- **Textos do Core a acertar:** a previsão das opções da equipe usa hífen em "-10" e cita o salário mesmo quando ele não muda; a linha "Na cidade" da previsão de posição dá "de −0,01 a −0,01" para vereador.
- **Saturação:** a mesma verba no mesmo alvo por 33 semanas chegou a −94%; conferir no BalanceSim da R3 se a curva não está dura demais.
- **A interface foi num commit só** (as telas se cruzam em `HubScreen.cs`), e não um por funcionalidade.
- **Lei dos cabos (art. 100-A):** o arredondamento do texto (despreza abaixo de 0,5 e arredonda para 1 a partir daí) e as frações de deputado ainda não estão conferidos no objeto de configuração; entra na conferência da R3.

## Teste do autor na tag fase-r1 (2026-11-16)

### P-24 · Dez itens do teste do autor, a corrigir antes da fase-r-final
Um commit por item. Antes de corrigir, conferir se a `fase-r2b` já resolveu. Situação de cada um é atualizada aqui.
1. **Criação, gênero:** mudar o gênero sorteia de novo a aparência com os pesos do gênero novo; se a aparência já foi editada à mão, perguntar antes. — aberto
2. **Número de urna:** campo de texto com 3 dígitos (zeros à esquerda e dígitos repetidos permitidos, como 000 e 111), validado contra os números ocupados no partido. — aberto
3. **Nenhum texto ao jogador cita o GDD ou outro documento interno** (ex.: "(GDD 16.2)"); checagem no DataValidator ou no alvo `explicacao`. — aberto
4. **Botões da base da criação encostam na barra de tarefas do Windows;** o alvo `layout` deve considerar a janela real, não só o viewport. — aberto
5. **Partido da criação × tela seguinte:** o santinho mostrou FRB e a tela seguinte, ACNB. Conferir, corrigir se for erro e testar. — aberto
6. **"Como te veem 0,00":** mostrar a imagem numa escala legível, seguindo o ART_GUIDE. — aberto
7. **Cores do logotipo aparecem como quadrados vazios.** — aberto
8. **Personagem que não pode disputar a primeira eleição** começa direto na pré-campanha da primeira que pode disputar, com a idade correspondente; a criação avisa; ajustar o alvo `creator17`. — aberto
9. **Entressafra na tela central,** com a agenda fora de campanha (visitar lideranças, entrevistas, posições, presença nas zonas). — **feito na R4 (tag `fase-r4`)**, alvo de fumaça `entressafra`. **Dono:** parte R4, no trabalho da interface da R4 (branch `r4-ui`), junto da P-01; entra na main com a `fase-r4`. Se não ficar pronto, é o primeiro item da `fase-r-final`.
10. **Mapa de cidade pequena (P-09):** enquadramento inicial no núcleo urbano; zona rural dividida por localidades ou distritos, quando houver; todos os nomes de zona legíveis, com desvio de colisão; contorno rural menos simplificado. Teste com Mantena (MG) e capturas antes e depois. — **feito no escopo reduzido (D-049), integrado depois da `fase-r3`.** Sobrou: na vista da cidade inteira os nomes urbanos se amontoam em volta da sede com linhas-guia; um nome deslocado pode ficar sobre outra zona; marcadores de zonas diferentes ainda podem se cobrir; uma zona urbana pode juntar a sede e vilas de distrito afastadas (em Mantena, "Zona Noroeste" junta bairros da sede e vilas a 25 km), defeito antigo da importação, para a RV ou para a divisão urbana por distrito. **Dono original:** trabalho próprio no branch `mapa-pequeno` (importador de zonas e desenho do mapa), começado antes da `fase-r3` porque muda as zonas das cidades pequenas e o BalanceSim depende delas; entra na main depois da `fase-r3` e antes da `fase-r4`, com o par (tipo, zona) do porte pequeno medido de novo antes de qualquer ajuste de parâmetro. Se não ficar pronto, é o segundo item da `fase-r-final`.
   - **Escopo reduzido pelo autor (16/11):** como o mapa de base será refeito na parte RV, faz-se agora só a divisão do rural por distritos, o enquadramento inicial no núcleo urbano e os nomes de zona sem sobreposição; contorno, estilo e rótulos por zoom ficam para a RV. Na integração: regenerar `data/zonas` e `data/geo/zonas`, rodar `zonas-rd` de novo, conferir os tetos (20 e 30 MB) e os testes do importador, conferir que lideranças, presença e o Livro apontam para zonas que existem, e só então medir o par (tipo, zona) do porte pequeno com 200 campanhas. A `fase-r3` sai com o par do porte pequeno como pendência (autorizado).
- **Também:** corrigir os encaixes errados de brinco e cabelo da arte 2D atual (os retratos 3D ficam para depois da Fase R). — aberto
- **Pesquisa pedida, sem implementar:** se o IBGE publica as faces de logradouro ou as quadras do Censo 2022 para todos os municípios e quanto ocupariam simplificadas só no núcleo urbano; o resultado entra aqui como decisão do autor.

### P-25 · Ruas ou quadras do IBGE no mapa das cidades (decisão do autor; nada foi implementado)
- **O que existe (lido nas listagens oficiais do geoftp e na ficha do produto):** a "Base de Faces de Logradouros do Brasil, versão 2022 (Censo Demográfico), resultados preliminares": linhas de rua das áreas urbanizadas, expansões urbanas e aglomerados rurais, um arquivo por município (5.570), 13,85 milhões de faces, em shapefile e JSON, um zip por UF, de 2024-06-07 (`geoftp.ibge.gov.br/recortes_para_fins_estatisticos/malha_de_setores_censitarios/censo_2022/base_de_faces_de_logradouros_versao_2022_censo_demografico/`). Total nacional: cerca de 1,0 GB em zip (RR 1,7 MB; PR 38 MB; SP 377 MB).
- **Quadras em polígono:** nenhum produto encontrado nas pastas percorridas; a quadra só aparece como atributo da face (`CD_QUADRA`) e teria de ser reconstruída. Em Roraima, 9,7% das faces não têm código de quadra.
- **Medido em Roraima** (15 municípios, 37.917 faces): simplificado a 3 m e gravado em JSON compacto sem atributos, dá cerca de 22 bytes por face; um município pequeno fica entre 6 e 70 KB.
- **Estimativa** (não é número do IBGE): o país inteiro daria de 305 a 415 MB nesse JSON; só os municípios com até 1.000 faces (2.853 deles), de 32 a 44 MB; só os com até 500 faces (1.441), de 10 a 13 MB. Com 17 MB de zonas já no repositório e o teto de 20 MB (D-037), **não cabe**. Caberia apenas um recorte dos menores municípios com compactação mais agressiva (não medida), ou os dados fora do repositório, baixados por município.
- **Licença:** não confirmada. As listagens dizem "Todos os arquivos aqui disponíveis são públicos"; a ficha do produto só traz ressalvas de qualidade e isenção de responsabilidade; as páginas do ibge.gov.br recusaram a leitura automática. O autor confere os termos antes de qualquer uso.
- **Não confirmado:** se há versão definitiva mais nova; a fração de faces fora da sede; a taxa de simplificação fora de Roraima.
- **Atualização (16/11, quarta rodada):** o autor decidiu usar as ruas no mapa novo (redefinição visual, DECISOES D-041, adendo). A pesquisa completa de fontes e tamanhos (faces, BC250, elevação) e a forma de guardar ou gerar o mapa de base são entregues ao fim da `fase-r-final`; a licença continua a conferir.
- **A decidir pelo autor:** (a) não usar e manter as zonas; (b) usar só nas cidades pequenas, com teto próprio de tamanho; (c) baixar por município, fora do Git. Nada muda até a decisão.

## R3 — pendências

### P-04 · Artigos de campanha citados de memória — **fechada (D-042)**
- Conferidos no texto oficial em 16/11/2026 (`docs/referencias/LEIS_CONFERIDAS.md`); as marcas "a conferir" saíram dos verbetes. O que não pôde ser aberto está na P-27.

### P-16 · Lint de rastro e eventos antigos — **fechada na R3**
- O EventLint cobra rastro, troca e texto de morte; os 32 eventos foram reescritos; nenhuma escolha usa mais "energia".

### P-26 · BalanceSim da R3: canal de verba fora nos quatro portes; par fora no pequeno; rivais fáceis demais
- **Medido** (`docs/balance/r3.md`, 48 campanhas por porte): tipo de compromisso em 25,4%, 27,5%, 27,0% e 37,4% (**passa nos quatro**, o que fecha a parte de tipo da P-15 e da P-20); par (tipo, zona) em **30,2% (pequeno)**, 23,1%, 18,9% e 17,4%; canal de verba em **51,6%**, **46,6%**, **40,1%** e **46,2%**; turnos forçados entre 16% e 34% (eram até 61%: a P-18 e a P-21 melhoram com os doadores).
- **Contas:** campanha limpa perde o mandato em 0,00% das carreiras; com mais de 30% do limite "por fora", em 36,2% (critério: 25% a 40%; passa). Na metrópole isolada, 44,4%.
- **Dificuldade:** em partido sorteado o robô agora se elege em 6, 10, 17 e 15 de 24 campanhas (eram 1, 6, 8 e 1). Os dois extremos da D-026 continuam passando nos testes, mas os rivais precisam ser recalibrados (`npc_effort_by_porte`, `npc_action_efficiency`).
- **O par do porte pequeno** é a entrevista no veículo de maior alcance.
- **Ressalva:** a medição é anterior a dois ajustes pequenos (lembranças nas escolhas de evento; data da prestação parcial). De novo 48 campanhas por porte, não 200.
- **Para fechar:** recalibrar canais e rivais e rodar de novo na integração da R4 (D-041).

### P-27 · O que a R3 deixou marcado para o autor
- **Tabela de limites de gastos do TSE por município** não importada (site bloqueou o acesso automático): o limite do jogo é parâmetro por porte. Para fechar: baixar a tabela à mão, pôr no cache e importar.
- **Não conferidos no acórdão:** ADI 4650 e Resolução TSE 23.607/2019; fase 2 das sobras (80% e 20%); ADI 5.525 (art. 224 do Código Eleitoral).
- **Vaga de vereador sem suplente:** o jogo usa "mais de 15 meses" (Constituição, art. 56, § 2º, por simetria); o Código Eleitoral, art. 113, fala em 9 meses. A confirmar.
- **Lista de bloqueio de nomes de veículos** (`data/media/blocklist.json`, 84 nomes) foi escrita de memória e está marcada assim; o autor precisa revisar.
- **Fora da R3:** rivais sem doadores nem contas próprias; pacto de divisão de zonas; "saída de outro rival" como gatilho de plano; a suíte de testes passou de 2 para 6 minutos.

### P-28 · Bases 3D estilizadas para personagens e cenários — **fechada (D-045)**
- **Decidido pelo autor em 16/11 (DECISOES D-045):** bases livres CC0 para cenários, animações, corpos e multidão; artista 3D só para cabeça, expressões e roupas, depois do protótipo aprovado e de um teste pago (`docs/BRIEFING_ARTISTA_3D.md`); Quaternius fora até a confirmação da licença por escrito, que o autor vai pedir. A pesquisa abaixo fica como registro.
Pesquisa de 16/11 para a parte RV. "Lido" = na página oficial; o resto é avaliação.
- **Nenhuma base livre entrega o pedido inteiro.** Não há personagem CC0 ou CC BY no estilo de Two Point Hospital com expressões no rosto, idades de 17 a 90 anos, corpos variados, traços da população brasileira e roupa institucional.
- **O que mais se aproxima:**
  - **Quaternius, Universal Base Characters e Universal Animation Library:** corpos de sombreado liso, 20 cabelos, rig humanoide, mais de 250 animações; sem expressões no rosto, sem idoso, sem roupa social. **Risco de licença:** as páginas dos pacotes dizem CC0, mas `quaternius.com/license.html` (lido) traz uma "Quaternius Asset License v1.0", de 28/08/2026, que proíbe redistribuir os arquivos, de graça ou não; isso impede guardá-los num repositório público até haver confirmação por escrito ou a prova de que a cópia baixada é CC0.
  - **KayKit (CC0, lido):** 133 animações (inclui acenar, comemorar, sentar) e mobília de formas arredondadas; entre os grátis, é o mais parecido com Overcooked nos cenários. Personagens de fantasia, sem rosto animável.
  - **Kenney (CC0, lido):** simples e blocado; serve de preenchimento e protótipo.
  - **Blender, Human Base Meshes (CC0; página oficial bloqueou a leitura, confirmado por trecho oficial e notícia):** cabeça e corpo estilizados de topologia limpa, sem rig, roupa, cabelo nem expressões: bom ponto de partida para um artista.
  - **MPFB 2 / MakeHuman (assets CC0, lido; o programa é AGPL e o addon GPLv3, e não entram no repositório):** 54 formas de rosto no padrão ARKit, idade, peso e diversidade; estilo realista. Serve de referência de expressões e de diversidade, não de malha final. Alguns pacotes são CC BY (Hair 02 e 03, Suits 03).
  - **Sketchfab, Canino3d (CC BY):** três personagens de estilo "Pixar" com mais de 100 formas de rosto; pesados (57 a 105 mil faces) e sem roupas trocáveis.
  - **Texturas e luz:** Poly Haven e ambientCG (CC0, lido).
- **Excluídos:** Mixamo (não é CC; proíbe repassar os arquivos brutos), Ready Player Me (encerrado, segundo fontes secundárias), VRoid (condições por modelo). Poly Pizza não garante a licença que informa: conferir na origem.
- **Onde é preciso um artista 3D** (estimativa grosseira, em dias de um artista pleno, adaptando base livre / do zero): cabeça estilizada com topologia de rosto, 2–3 / 4–6; expressões e visemas, 3–5 / 5–8; variações de corpo e rosto (idade, peso, traços), 4–6 / 6–10; 8 a 12 cabelos, 5–8 / 8–12; 4 a 6 barbas, 2–3 / 3–4; roupas (social, terno, camiseta, campanha, uniforme), 10–15 / 15–20; rig de jogo, retarget e animação de discursar, 3–5 / 6–8; materiais de tingimento, 1–2; 25 a 40 objetos brasileiros e institucionais (urna, púlpito, bandeiras genéricas, câmeras, estúdio de foto, gabinete, plateia), 10–15 / 15–25; montagem e luz dos 6 cenários, 6–10 / 8–12. **Total: cerca de 45 a 70 dias adaptando, 70 a 105 do zero.** Sem artista, o protótipo do fim da `fase-r-final` sai com bases livres montadas e fica abaixo do padrão das referências, principalmente no rosto.
- **Cuidados:** crédito item a item em CREDITS.md para todo CC BY; guardar a prova da licença de cada asset; não usar estúdios que copiam emissoras reais nem urnas com brasão ou logo da Justiça Eleitoral; conferir as regras de uso dos símbolos nacionais (Lei 5.700/1971, não lida); não foi possível confirmar que modelos do Sketchfab não vêm de IA.
- **A decidir pelo autor:** (a) aceitar bases livres montadas, com rosto simples; (b) contratar um artista para a cabeça, as expressões e as roupas, e montar o resto com bases livres; (c) encomendar tudo a um artista. E, no caso da Quaternius, se vale pedir a confirmação da licença.

### P-29 · Blender para converter a cabeça A do teste A/B — **fechada em 16/11**
- **Fechamento:** o autor instalou o Blender 5.3.0 e apontou o caminho em Editor Settings → FileSystem → Import → Blender → Blender Path (`C:/Program Files/Blender Foundation/Blender 5.3/blender.exe`, nas configurações do editor 4.7). **Conferido com uma importação de teste** num projeto Godot à parte, fora do repositório: um `.blend` gerado pelo próprio Blender (esfera `cabeca_teste`, material `pele`, forma `expr_sorriso`) importou com `godot --headless --import` sem erro e virou cena; ao carregar a cena, a malha, o material e a forma vieram com os nomes certos. Com isso, o protótipo da RV monta as duas cabeças do teste A/B. O Blender continua fora do repositório.
- **Registro original:** a cabeça A parte dos Human Base Meshes do Blender, distribuídos em `.blend`. Converter para glTF exige o Blender: o Godot só importa `.blend` chamando o executável do Blender. **Conferido em 16/11: o Blender não está instalado nesta máquina.** Nada será instalado sem o autor.
- **O que o autor precisa fazer, se quiser a cabeça A:** instalar o Blender (versão estável atual, em blender.org; programa livre, GPL, que não entra no repositório) e, no Godot, apontar o caminho do executável em Editor → Configurações do Editor → Sistema de arquivos → Importação → Blender. Alternativa: abrir o `.blend` no Blender e exportar para `.glb` à mão.
- **Sem isso:** o protótipo segue só com a cabeça B, e a prancha mostra a coluna A vazia com o motivo.

## R4 — pendências (tag fase-r4)

### P-30 · Agenda por dia no mandato
- O mandato de vereador e a prefeitura usam turnos por semana: inauguração e fiscalização acontecem no clique, sem dia nem lugar na agenda (GDD 17.6 e 17.14 pedem compromissos com dia, turno e zona também no mandato). Decidido pelo autor em 16/11: fica como pendência da R4.

### P-31 · Objetivos do mandato além das promessas do Livro
- No mandato, os objetivos são só as promessas mais urgentes do Livro, sem o gerador da campanha (GDD 17.2, peça 6), sem fixar nem dispensar. Decidido pelo autor em 16/11: fica como pendência da R4.

### P-34 · Idade média dos vereadores nas Câmaras pequenas — **fechada (D-051)**
- O critério vale no conjunto e nas Câmaras de 13 cadeiras ou mais; nas de 9 a 11, como média de 20 sementes (deriva de +0,5 a +1,1 ano).

### P-36 · O que a R4 deixou de fora ou a revisar (D-050; separado da P-34 quando ela fechou)
- **Caminhos raros de sucessão:** em 40 anos simulados, toda vaga foi preenchida por suplente na mesma semana. A cadeira sem suplente e a eleição indireta só ocorrem nos testes dedicados.
- **Parâmetros a confirmar:** piso e teto dos indicadores (percentis 1 e 99), posse do suplente em 7 dias, eleição da cadeira em 90 dias.
- **Fora da R4:** inclinação dos indicadores pelo CAGED e pelas transferências (P-14); velório como compromisso; herdeiro que herda dívidas; licença por doença de mais de 120 dias; candidatura do jogador numa eleição suplementar (hoje ela se resolve sozinha); inelegibilidade pela Ficha Limpa.
- **Contas na reeleição:** como as contas só andam fora de campanha, a faixa de contas da campanha não aparece durante uma campanha de reeleição.
- **Imagem do prefeito:** o tooltip não diz que ela vai para a reeleição, porque o Core só leva a do vereador.
- **Interface, anteriores à integração** (capturas em `docs/referencias/r4/`): na metrópole o mapa encosta nos botões de modo; a legenda cobre parte do mapa e um marcador; o cartão das contas é longo no painel de 400 px.

## R3 — fechamento (tag fase-r3)

### P-26 · Atualização com 200 campanhas por porte (docs/balance/r3.md)
- **Passa agora:** tipo de compromisso nos quatro portes (27,9%, 27,2%, 27,6%, 36,9%), o que fecha a parte de tipo da P-15 e da P-20; **canal de verba nos quatro portes** (Rua 33,6%, Redes 30,2%, Redes 37,0%, Redes 29,1%), o que fecha a parte de canal da P-20; decisões com troca (2,73 a 2,93 por semana); contas (limpas 0,00%; "por fora" acima de 30% do limite, 36,2%, dentro de 25% a 40%); sem campanha, nunca eleito (0 em 400).
- **Não passa:**
  - **Par (tipo, zona) no porte pequeno: 30,5%** (critério 25%). Não é zona: é a entrevista no veículo de maior alcance (rádio comunitária 13,9 pontos, página de notícias 6,3, entrevista sem veículo 5,4). A mudança das zonas (branch `mapa-pequeno`) sozinha não deve movê-lo; o que o move é o número de veículos por cidade, o alcance deles ou um cansaço da entrevista por veículo, que hoje não existe. Medir de novo depois da integração do mapa, como o autor pediu, antes de mexer em parâmetro. A `fase-r3` sai com esta pendência (autorizado pelo autor).
  - **Régua da D-026:** partido mais forte eleito em 76%, 84%, 52% e 53% (régua: 96%, 96%, 67%, 58%); partido sorteado em 19%, 30%, 11% e 15% (régua: 13%, 13%, 8%, 0%). Os dois andam juntos: o que derruba o sorteado derruba quase o mesmo o mais forte. Só nas campanhas limpas o sorteado fica em 12%, 20%, 8% e 0%. A régua foi medida em 2026-11-15 com um robô que sorteava ações; o de hoje escolhe a melhor opção e usa equipe, verba e doadores.
- **A decidir pelo autor:** se a régua da D-026 continua valendo para o robô atual; e, se sim, qual alavanca usar: empenho do candidato coletivo por porte, peso do partido no modelo de voto (mexe na D-015) ou número de partidos na disputa.
- **Também:** o robô simples do `CampaignBalanceTests` ficou no limite do teste (3 de 7 cidades); rivais mais duros o quebram.

### P-32 · Sobras da interface da R3
- Mídia e Contas pedem bastante rolagem no painel de 400 px; o cartão das contas destoa das telas antigas do mandato (que somem na R4).
- Em cidade pequena a chapa é quase toda "outro nome da lista"; no modo Disputa a etiqueta usa a primeira palavra do nome ("Sargento", "Dra."); legenda larga; tema em alta na cidade inteira põe um ícone em cada zona.
- Textos do Core com gênero fixo nas previsões ("ele não ataca você", "o jornalista achou").
- Duas escalas de imagem (barra de −1 a +1, previsões de ataque em pontos); uma faixa de decisão só para ataque, oferta e aviso; rivais não propõem pacto nem dobradinha; o contra-ataque não nomeia a prova; o tesoureiro não avisa do limite; o prazo do ataque conta em dias (o Core devolve 72 horas no dia do ataque para um prazo de 48).
- Sem tela de Opções: as pausas novas (`ataque`, `decisao_com_prazo`) só se configuram em `data/time.json` (vai com a P-03, na `fase-r-final`).

### P-33 · Ambiente compartilhado dos testes de fumaça (corrigido em parte)
- Até o commit `c898e4e`, rodadas de fumaça de vários agentes gravavam no mesmo `user://`; agora cada rodada usa `user://smoke/<perfil>/`. **As rodadas da R4 anteriores à correção gravaram na pasta real do jogo** (`%APPDATA%\Godot\app_userdata\Planalto\saves`: `autosave.json` de 16/11 15h37 e cinco `smoke*.json`); se o autor tinha um autosave ali, ele pode ter sido trocado. Nada foi apagado.
- Alvos com falha intermitente sob carga (`agenda`, `lideranca`, `verba`, `eventos`), todos depois de um clique simulado; com a máquina livre, `eventos` passou 10 de 10 seguidas. Endurecer o clique do UiShot depois da integração da R4.
- **Cliques: fechado na fase-r-final.** O clique espera o controle visível, habilitado, dentro da janela e parado, confere que nada o cobre, entra num só quadro e confirma que chegou (senão falha com diagnóstico, sem repetir); a semente dos testes não depende mais do relógio do Novo Mundo.

### P-26 · Par do porte pequeno medido de novo depois do mapa (pedido do autor)
- Com a divisão rural por distritos, 200 campanhas no porte pequeno: par (tipo, zona) **30,2%** (antes 30,5%); tipo 27,8%; canal 33,1%; partido mais forte eleito em 73 de 100, sorteado em 19 de 100. Como previsto, a mudança das zonas não move o par: ele é a entrevista no veículo de maior alcance. Nenhum parâmetro foi alterado por causa dele. **A decidir pelo autor:** cansaço da entrevista por veículo, mais veículos na cidade pequena, ou aceitar.
- **Medido de novo na integração da R4** (200 campanhas por porte; `docs/balance/r4.md`): a R4 não muda a campanha, e o porte grande deu números idênticos numa cópia da main sem ela. Par do pequeno 30,2%. A régua da D-026 ficou pior que na `fase-r3` no partido mais forte do pequeno (76 → 73), do grande (52 → 46) e da metrópole (53 → 49), e no sorteado do médio (30 → 39). A causa é o mapa de cidade pequena, não a R4.
- **Cansaço por veículo (pedido do autor, branch `cansaco-veiculo`, só depois da `fase-r4`):** feito e testado. Na medição preliminar, com 50 campanhas no porte pequeno, o par cai de 30,2% para 19,0%, mas o tipo sobe de 27,9% para **82,5%**: com cada veículo cansando sozinho e vagas a cada 2 a 4 dias, a entrevista chega descansada e vira a melhor opção de quase toda semana. Não integrado. Caminhos levados ao autor: entrevista mais fraca ou alcance absoluto, intervalos de semanas, ou medir o tipo por turno para opções com vaga (por turno, a entrevista fica em 29,8%).
- **Achado na integração:** com as zonas novas, o robô da R3 encontrou uma previsão errada: a perda de imagem prevista para quem ataca ignorava o limite de −1; perto do piso a perda real era menor que a prevista. Corrigido no Core (`fix(campanha)`).

### P-24 · Itens 1 a 8 e encaixes dos retratos — **feitos** (branch `r1-teste`, integrado depois da `fase-r3`)
- **1, gênero:** sorteia de novo com os pesos do gênero novo; se a aparência foi editada à mão, pergunta antes.
- **2, número de urna:** campo de 3 algarismos validado no Core (`CandidateNumbers.TryParseSuffix`). Na criação a lista de ocupados é vazia de fato (o Core reserva primeiro o número do jogador e depois sorteia os dos colegas); a checagem existe e está testada.
- **3, documento interno:** havia um caso ("(GDD 16.2)"); o DataValidator ganhou o lint "textos sem citar documento interno" e o alvo `explicacao` confere as telas.
- **4, barra de tarefas:** causa reproduzida (janela de 1920×1080 com moldura num monitor com área útil de 1920×1020); o jogo abre maximizado e o alvo `layout` confere a janela real contra a área útil.
- **5, partido:** não havia troca de partido; o mais provável é que a tela central da `fase-r1` abrisse no modo "Partido líder" com a sigla do líder da zona sem título. Agora o partido aparece na linha da candidatura e "(seu partido)" na legenda.
- **6, imagem:** faixa nomeada com régua de 5 casas; o número só no tooltip. Faixas em `data/campaign/config.json` (`image_scale`: −1, −0,25, −0,05, +0,05, +0,25), escolhidas pelo tamanho dos efeitos de evento; **a revisar pelo autor**.
- **7, cores do logotipo:** a amostra do seletor não tinha espaço (margens do tema); afetava todos os seletores da criação. Corrigido; a bandeira da pré-visualização também mostrava o nome na cor do fundo.
- **8, 17 anos:** começa na pré-campanha da primeira eleição que pode disputar (D-036 alterada). O calendário só salta: a eleição anterior não é simulada.
- **Retratos 2D:** encaixes de brinco, orelha e cabelo corrigidos sem redesenhar a arte (`fit` e `lobe_y` em `parts.json`).
- **Sobrou:** Mandato e Prefeitura nas telas antigas ainda mostravam "Imagem +0.00" (somem na R4); as cores do logotipo não acompanham a troca de partido depois de sorteadas (só pelo botão "Cores do partido").

## Depois da tag fase-r4

### P-35 · Pacote de nomes privado (GDD 17.18, D-053), a fazer depois da tag fase-r-final (nada foi implementado)
- O autor revê a suspensão da D-035 (item 15) só para uso privado:
  - leitor do pacote em `user://mods/nomes/`, trocando partidos (nome, sigla, número, cor) e pessoas do município por id estável (`mun.<ibge>.pref`, `.vice`, `.ver.<ordem>`);
  - marca "pacote privado" na tela; o save registra o pacote;
  - campo `protecao_eventos`, padrão `false`;
  - o pacote e o gerador ficam fora do repositório e dos builds.
- O prompt completo está com o autor.

### P-37 · Itens do primeiro pedido da RL fora da leitura mínima (D-055)
- **O quê:** gráfico da intenção de voto por semana (eu, os dois acima e os dois abaixo); primeira campanha guiada por objetivos (agir numa zona, visitar uma liderança, ler a previsão; dispensável, fora do save); feed com fatores; objetivos com origem (os dois últimos vêm da referência `docs/referencias/rl_tela_central.html`).
- **A decidir pelo autor:** em que parte cada um entra (RL, RV-A, RV-C ou depois).

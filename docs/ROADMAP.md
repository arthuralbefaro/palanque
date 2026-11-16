# PLANALTO — Roadmap

> Fonte única do planejamento de fases, derivada de todo o docs/GDD.md (o conteúdo de cada item está descrito lá). Um item por vez; marque o checkbox ao concluir.

## Fase 0 — Fundação
- [x] Git + .gitignore Godot/.NET
- [x] Projeto Godot .NET configurado (1920x1080, canvas_items/expand)
- [x] Solução com Planalto (Godot), Planalto.Core, Planalto.Core.Tests
- [x] Autoloads vazios, tema base, menu principal
- [x] CLAUDE.md
- [x] Build e testes passando

## Fase 1 — Núcleo de simulação

### 1A — Lógica pura
- [x] Rng, Calendar, GameState
- [x] Character, atributos, traços, recursos
- [x] ElectionRules (proporcional e majoritário) com testes, incluindo sobras obtidas e não preenchidas no divisor e ProportionalRulesConfig
- [x] Carregamento e validação de JSON + DataValidator

### 1B — Municípios
- [x] Planalto.DataImporter: IBGE (Localidades, estimativa de população 2026, Censo 2022/SIDRA) e TSE (perfil do eleitorado ATUAL, de-para TSE↔IBGE, vagas 2024), cruzados pelo código IBGE, gerando data/municipios/{UF}.json (27 arquivos) e data/municipios/_fontes.json, com brutos em cache fora do Git e créditos em assets/CREDITS.md
- [x] Coordenadas e área de todos os municípios pela Malha Municipal 2025 do IBGE (centroide, ou ponto interior quando o centroide cai fora do polígono), com NetTopologySuite só no DataImporter
- [x] Divisão regional: microrregiao (pode ser null) e regiao_imediata
- [x] Perfil do Censo em contagens inteiras, gravado como publicado; Boa Esperança do Norte com perfil do MT (perfil_fonte: "uf")
- [x] Vagas de vereador reais do TSE 2024 (vagas_vereador) + teto constitucional (vagas_vereador_max), com a lista dos municípios acima do teto em _fontes.json
- [x] Porte por data/portes.json, lido por função do Core, sobre a estimativa de 2026
- [x] Distrito Federal e Fernando de Noronha sem eleições municipais (eleicoes_municipais: false)
- [x] Testes do importador: contagem igual à lista oficial do IBGE, nenhum código duplicado, soma de população por UF igual ao total oficial, eleitorado > 0 (exceto casos especiais), soma de vagas igual ao total do TSE 2024, todos os municípios com lat/lon dentro do próprio polígono
- [x] DataValidator validando data/municipios

### 1C — Mundo e partidos
- [x] Famílias ideológicas em data/parties/families.json (seção 14.1)
- [x] Gerador de partidos fictícios (nomes, siglas, números, cores) com lista de bloqueio em data/parties/blocklist.json (itens não confirmados também bloqueiam; conferência nas páginas do TSE possível depois, se salvas no cache)
- [x] WorldSetup no Core: predefinições, sorteio por campo, travas e validações da tela "Novo Mundo" (seção 16.1)
- [x] Números de candidato por cargo (seção 16.2) com testes

### 1D — Cenário "Brasil 2026"
- [x] Cenário "Brasil 2026" (seção 16.4), em três partes: nacional (partidos, federações, Congresso, fundo eleitoral), estadual (governadores, senadores, Assembleias de 2026) e municipal (prefeitos, vices, inelegibilidade por reeleição, Câmaras, força local e alianças de 2024 para todos os municípios)

### 1E — Eleitorado e pesquisas
- [x] Segmentos de eleitores gerados do perfil real do município (idade, renda, religião, escolaridade, urbano/rural), com categorias normalizadas pela soma delas; afinidade partidária inicial pela força local da 1D no cenário "Brasil 2026" e, nos demais cenários, por distribuição gerada a partir das famílias e dos tamanhos dos partidos
- [x] Modelo de voto (utilidade por segmento, softmax, brancos/nulos/abstenção, comparecimento considerando voto facultativo de 16–17 e 70+)
- [x] Pesquisas com margem de erro, institutos fictícios com viés e pesquisa interna

## Fase 2 — Criação de personagem
- [x] Modelo Appearance no Core + randomização com pesos + serialização
- [x] Arte placeholder gerada por código para todas as categorias
- [x] PortraitRenderer com shader de tingimento, camadas trás/frente, expressões e cache
- [x] Tela de criação com 4 abas, aleatório, desfazer, presets
- [x] Santinho, outdoor e compositor de logotipo
- [x] Geração de NPCs usando o mesmo sistema
- [x] Seleção de município (qualquer município do Brasil como cidade natal e palco da primeira campanha): busca com autocompletar, filtro por UF/região, mini-mapa e ficha (população, eleitorado, vagas, porte, dificuldade sugerida)
- [x] Tela "Novo Mundo" (seção 16.1): predefinições, lista de partidos editável, sorteio, travas, cenários salvos
- [x] Aba "Partido" na criação de personagem (seção 14.2 A): filiar-se a um partido existente ou criar o seu

## Fase 3 — Fatia vertical: Vereador
- [x] 1 cidade, 6 partidos, ~10 rivais
- [x] Hub semanal, agenda de campanha, 6 ações
- [x] Painel de pesquisas, noite da apuração
- [x] 30 eventos
- [x] Save/Load
- [x] Simulação detalhada do município em foco: todos os segmentos, candidatos NPC individuais, mídia local e eventos locais
- [x] Candidatos e políticos NPC do município gerados só quando ele entra em foco e persistidos no save
- [x] Protótipo do mapa 2.5D com os municípios de um estado (validar desempenho e visual antes da Fase 6)
- [x] Motor de eventos (seção 15.3): condições, peso, raridade, cooldown, cadeias e variáveis de texto
- [x] tools/Planalto.EventLint (seção 15.5): referências, variáveis inexistentes, cadeias quebradas, eventos inalcançáveis, distribuição por categoria
- [x] Urna (seção 16.3) para vereador e prefeito
- **Marco:** campanha de vereador jogável do início ao fim e divertida
- **Resultado do teste do autor (2026-11-15):** jogável do início ao fim; **reprovada em diversão** (rasa, repetitiva, confusa). Diagnóstico em docs/AUDITORIA_FASE3.md; correção na Fase R.

## Fase 4 — Mandato legislativo
- [x] Votações, projetos, emendas, fidelidade, relações, capital político
- [x] Transição para a próxima eleição
- [x] Fundação de partido durante a carreira (seção 14.2 B): fundadores, apoio de eleitores, registro no TSE, prazo

## Fase 5 — Prefeito
- [x] Orçamento, indicadores, políticas, coalizão municipal
- [x] Minigame de debate

## Fase R — Reformulação: imersão e profundidade

> Origem: o autor jogou a fatia vertical e a reprovou (docs/AUDITORIA_FASE3.md). O desenho está no GDD, seção 17. **Esta fase não roda em modo autônomo:** cada parte termina com a verificação obrigatória, os testes da parte e o roteiro de teste entregue; depois para e espera o autor jogar e aprovar. Nenhuma parte começa antes de o autor aprovar a anterior e decidir as pendências que ela usa (GDD 17.20).
> Uma parte só está pronta quando: (1) todos os itens estão marcados; (2) os testes automáticos listados passam junto com a verificação obrigatória; (3) o roteiro de teste da parte está atualizado com os nomes reais dos controles; (4) o autor jogou o roteiro e aprovou.

### R1 — Tela central, mapa da cidade e tempo corrente
Conteúdo de jogo igual ao atual (6 ações, pesquisas, eventos, rivais como estão), na tela nova.
- [ ] Importador, modo `zonas`: contornos e população das zonas de todos os municípios pela Malha de Setores e pelos agregados do Censo 2022 (GDD 17.5), com os testes do importador; tamanho de `data/geo/zonas/` medido e informado ao autor
- [ ] Tela central (GDD 17.3 e ART_GUIDE, Interface): barra superior, trilho, painel da área, mapa 2D das zonas com os modos Partido líder e Perfil, coluna direita (Notícias, Objetivos, Intenção), faixa e cartão de decisão
- [ ] Tempo corrente (GDD 17.4): dia como unidade no Core, pausa e 1x/2x/4x, simulação semanal na segunda-feira, pausa automática configurável por tipo, `data/time.json`
- [ ] Painéis com o conteúdo atual: campanha, mandato de vereador e prefeitura passam a ser áreas da tela central; as telas antigas saem
- [ ] Previsão e explicação (GDD 17.2, peça 6) para as ações, pesquisas e escolhas de evento atuais: faixa antes do clique, "O que mudou" depois
- [ ] Tooltips em três partes em todos os controles, nomes fictícios com o que são, termos do glossário com link (princípio j)
- [ ] Enciclopédia (F1): Glossário, Como funciona, Este mundo, Sua carreira; `data/encyclopedia/`
- [ ] Explicação de primeira vez, dispensável, guardada fora do save
- [ ] Fontes OFL em `assets/fonts/` e ícones do trilho, com crédito
- **Princípios que passa a cumprir:** f (para o conteúdo atual), i, j. Parcialmente b (o tempo corre e os rivais agem em dias visíveis no feed).
- **Testes automáticos:**
  - Core: `AdvanceDay` sete vezes produz o mesmo estado que o `AdvanceWeek` atual com a mesma semente (campanha, mandato e prefeitura); uma semana sem decisão não gera pausa; cada tipo de pausa respeita a configuração.
  - Core: toda ação, pesquisa e escolha de evento devolve previsão com faixa e explicação não vazia; em 1.000 execuções o resultado cai na faixa em 80% ou mais.
  - Importador: zonas dentro do polígono do município, soma da população igual à do Censo sem tolerância, nenhum setor sem zona, urbano e rural separados.
  - Interface (teste de fumaça ampliado): nenhuma sobreposição ou texto cortado em 1366×768, 1920×1080, 1280×800 e 2560×1080, em 100% e 130% (ART_GUIDE, Interface, seção 9); barra superior com até 5 números; todo acontecimento que pausa tem faixa de decisão.
  - Interface: todo controle interativo tem tooltip nas três partes; todo controle desativado diz o motivo e o que falta; todo termo de `glossary.json` na tela tem link para o verbete; F1 abre a Enciclopédia em todas as telas.
  - DataValidator: limites de tamanho de texto do ART_GUIDE; todo verbete citado existe.
- **Roteiro do autor:** docs/ROTEIRO_TESTE_R1.md

### R2 — Território, agenda, equipe, verba, plataforma e lideranças
- [ ] Território na simulação (GDD 17.5): presença por zona, segmentos por zona, voto por zona, decaimento e transbordo, demanda da zona, Ficha da zona, modo Minha força
- [ ] Agenda com lugar e dia (17.6): compromissos, turnos, cansaço, ocasião, deslocamento, delegação, convites, eventos da cidade; sai a energia semanal
- [ ] Pessoa com memória e Livro de compromissos (17.2)
- [ ] Equipe (17.7)
- [ ] Verba por canal (17.8)
- [ ] Plataforma (17.9): posições, bandeiras, posição conhecida, promessas, custo de mudar
- [ ] Rede de lideranças (17.10)
- [ ] Gerador de objetivos (17.2, peça 6)
- [ ] `tools/Planalto.BalanceSim`: robô de campanha que escolhe o compromisso de maior ganho previsto, com relatório por porte
- **Princípios que passa a cumprir:** a, d (campanha), e, g, h; c para equipe e lideranças.
- **Testes automáticos:**
  - BalanceSim (200 campanhas por porte): nenhum tipo de compromisso é a melhor escolha em mais de 40% das semanas, nenhum par (tipo, zona) em mais de 25%; média de 1 ou mais decisões com troca por semana.
  - Core: terceiro compromisso igual em 2 semanas rende no máximo 50% do primeiro, e a previsão informa o fator.
  - Core: soma dos eleitores das zonas igual ao eleitorado do município; projeção por zona somada igual à do município.
  - Core: toda pessoa de equipe e liderança tem nome, aparência, interesse e lembranças; visita, promessa, recusa e demissão gravam lembrança.
  - Lint de conteúdo: toda escolha relevante grava rastro; toda decisão tem ganho e custo de naturezas diferentes; pesos de interesse por tipo de liderança dentro de 2 para 1.
  - Core: em qualquer dia há de 1 a 3 objetivos ativos com condição, prazo e origem; 4 ou mais tipos numa campanha.
  - Campanha inteira jogada sozinha em 3 sementes por porte, do prazo de filiação à apuração, sem erro.
- **Roteiro do autor:** docs/ROTEIRO_TESTE_R2.md

### R3 — Rivais com estratégia, mídia local e doadores
- [ ] Rivais (17.11): perfil, plano semanal, gatilhos, resposta a ataque, alianças, pactos, painel da chapa
- [ ] Mídia local (17.13): veículos, linha editorial, feed a partir de fatos, tema em alta, entrevista, pauta, resposta a ataque com prazo, boato
- [ ] Doadores e prestação de contas (17.12): fontes, limite, painel Contas, risco, julgamento, representação; esqueletos com "quem sabe" e chance de vir à tona
- [ ] Eventos atuais (32) ligados a pessoas, zonas e fatos do save; eventos dinâmicos disparados por fatos (GDD 15.4)
- **Princípios que passa a cumprir:** b, c (rivais, doadores, jornalistas), d (contas e esqueletos).
- **Testes automáticos:**
  - Campanha de 40 semanas sem comando do jogador: 3 ou mais notícias por semana que não citam o jogador; liderança muda em 1 ou mais zonas; presença de rival muda em mais da metade das zonas.
  - Depois de um ataque, 80% ou mais dos rivais não calculistas respondem em até 14 dias; rival ultrapassado no reduto troca de plano em até 2 semanas.
  - Nenhuma notícia sem fato de origem; tom de cada veículo coerente com a linha em 95% ou mais dos casos; boato sempre marcado.
  - BalanceSim: campanha com mais de 30% do limite "por fora" perde o mandato em 25% a 40% das carreiras em 2 anos; limpa, em menos de 1%.
  - Regras de lei em objetos de configuração com o padrão da lei; teste de cada limite.
  - Nenhum nome de veículo coincide com a lista de bloqueio.
- **Roteiro do autor:** docs/ROTEIRO_TESTE_R3.md

### RD — Dados reais de partida
Entra antes da R4, que usa os indicadores. Só importação e validação; nada muda na tela além dos números.
- [ ] Importador: economia municipal, finanças municipais e estaduais, indicadores sociais e séries nacionais das fontes da tabela do GDD 17.17, com ano, endereço e data em `_fontes.json` e crédito em `assets/CREDITS.md`
- [ ] Lacunas registradas com a regra de substituição de cada indicador (GDD 17.17)
- [ ] Receita real por município no lugar da receita por habitante estimada da D-031
- [ ] Tábua de mortalidade do IBGE em `data/demography/` (GDD 17.16)
- [ ] Religião e escolaridade por área de ponderação e eleitorado por local de votação, se o autor aprovar (GDD 17.20)
- **Princípios que passa a cumprir:** dá base a b e f na R4.
- **Testes automáticos:** todo município tem valor ou lacuna registrada para cada indicador; totais por UF iguais aos oficiais onde a fonte publica total; nenhum valor fora da faixa plausível declarada em `data/`; nenhum número escrito à mão (todo campo com fonte em `_fontes.json`); DataValidator cobrindo os arquivos novos.
- **Roteiro do autor:** docs/ROTEIRO_TESTE_RD.md

### R4 — Mandatos na nova tela, com gestão nova
- [ ] Mandato de vereador (17.14): gabinete, requerimento, indicação, fiscalização, emenda com obra no mapa, negociação de voto, contagem nominal, instrução permanente de voto, base que cobra
- [ ] Prefeitura (17.15): secretários, obras com licitação e prazo no mapa, servidores e limite fiscal, convênios, Câmara que negocia, crises com causa
- [ ] Indicadores ancorados em números reais e mostrados em unidade real; modelo de evolução (17.17)
- [ ] Renovação do mundo político no município (17.16): envelhecimento, mortalidade, saídas e entradas de vereadores, prefeito, lideranças e equipe; luto, suplente e vice pela regra real
- [ ] Eventos de mandato (legislativo e executivo), Mesa, CPI e cassação (pendências das D-027 e D-031)
- [ ] Cobrança de promessas e dívidas do Livro de compromissos
- **Princípios que passa a cumprir:** todos, também no mandato.
- **Testes automáticos:**
  - Carreira (campanha + mandato) em 3 sementes: 70% ou mais das promessas geram cobrança; nenhuma inauguração sem obra entregue; nenhuma greve sem negociação anterior.
  - Mandato de 4 anos sem comando: obras do prefeito NPC andam, pelo menos 1 crise com causa, notícias semanais.
  - Limites de lei (folha, mínimos, inauguração no período vedado) em objetos de configuração, com teste.
  - Indicadores: valor inicial igual ao dado importado; conversão de ida e volta entre unidade real e escala de jogo sem perda; evolução dentro dos limites plausíveis em 40 anos simulados.
  - Renovação municipal em 40 anos: a Câmara nunca fica com cadeira vaga por mais tempo do que a regra permite; idade média dos vereadores estável (variação menor que 5 anos entre a 1ª e a 10ª legislatura); toda morte de titular resulta em posse de suplente ou vice, ou em eleição, conforme a regra configurada.
  - BalanceSim de mandato: nenhum instrumento é a melhor escolha em mais de 40% das semanas.
- **Roteiro do autor:** docs/ROTEIRO_TESTE_R4.md

## Fase 6 — Esfera estadual e federal
> Só começa depois da Fase R aprovada. As esferas estadual e federal usam a tela central, o tempo corrente e os sistemas da seção 17 do GDD.
- [ ] 27 estados
- [ ] Deputados, senador, governador
- [ ] Mídia, fake news, escândalos e justiça
- [ ] Envelhecimento e Espelho
- [ ] Evolução dos partidos (seção 14.1): fusões, federações, cisões, mudança de nome, crescimento e declínio
- [ ] Urna com todos os cargos, na ordem real (seção 16.3)
- [ ] Simulação por nível de detalhe: regional (demais municípios do estado, agregados por região geográfica imediata) e nacional (outros estados, agregados por UF)
- [x] Evolução da população dos municípios e regra de vagas a cada eleição municipal: mínimo entre as vagas atuais e o teto calculado com a população do ano
- [x] Contornos simplificados a partir da Malha Municipal 2025 (mesma fonte das coordenadas)
- [ ] Mapa 2.5D (seção 13.1): extrusão, níveis de detalhe (Brasil → UF → região imediata/município), câmera inclinada, modo apuração, tooltips e alternativa 2D plana
- [ ] Desempenho: avançar uma semana em menos de 200 ms, mesmo em eleição presidencial; mapa a 60 FPS na visão de um estado
- [ ] Renovação do mundo político nas esferas estadual e federal (GDD 17.16): deputados, senadores, governadores; suplentes e vices pela regra real
- [ ] Vagas individuais no cenário Brasil 2026 (GDD 17.18): cada cadeira de deputado federal e estadual, senador, governador e presidente como NPC fictício com id estável
- [ ] Leitura do pacote opcional de nomes em user://mods/ (GDD 17.18) — **só se o autor aprovar; decisão pendente**

## Fase 7 — Presidência
- [ ] Economia nacional, ministérios, Congresso, MPs, vetos
- [ ] Modelo de economia nacional (GDD 17.17): PIB, inflação, juros, câmbio, desemprego e dívida partindo dos números oficiais e evoluindo por ciclo, política econômica e choques; efeito nas receitas de estados e municípios
- [ ] Crises nacionais, impeachment
- [ ] Tela de fim de carreira com linha do tempo de retratos

## Fase 8 — Conteúdo
- [ ] 600+ eventos, 100+ projetos de lei, 60+ políticas
- [ ] Conteúdo local (eventos e personalidades) para municípios de todas as regiões
- [ ] Ampliar catálogo de aparência (meta: 30+ cabelos, 12+ barbas, 40+ roupas)
- [ ] Conquistas
- [ ] Eventos com dados do município: variáveis {municipio.nome}, {municipio.uf}, {municipio.porte} e filtros por região e porte
- [ ] Eventos nas 12 categorias da seção 15.2, nas metas de cada uma
- [ ] Eventos dinâmicos gerados pela simulação (seção 15.4)

## Fase 9 — Polimento
- [ ] Arte final de retratos (ilustrador ou pipeline próprio)
- [ ] Bonecos 3D parametrizados a partir de Appearance (seção 13.2): materiais tingíveis, formatos 3D de cabelo e barba mapeados dos estilos 2D
- [ ] Cerimônias de posse em 3D (seção 13.2), começando pela presidencial e pela de vereador, depois os demais cargos; sempre puláveis
- [ ] Cena da seção eleitoral (ilustração 2D com parallax), sons originais da urna e acessibilidade da urna (áudio de confirmação, fonte grande)
- [ ] Animações, áudio, jingles, música
- [ ] Tutorial na primeira campanha
- [ ] Opções e acessibilidade
- [ ] Converter as telas principais para cenas .tscn editáveis no editor, mantendo a lógica em C#

## Fase 10 — Balanceamento e lançamento
- [ ] Relatórios do BalanceSim e ajustes
- [ ] BalanceSim de 40 anos: renovação da Câmara dos Deputados e do Senado dentro das faixas do GDD 17.16; indicadores dentro dos limites plausíveis do GDD 17.17
- [ ] Playtests externos
- [ ] Export Windows, Linux, macOS
- [ ] Página na Steam e/ou itch.io

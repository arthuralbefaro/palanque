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

> Origem: o autor jogou a fatia vertical e a reprovou (docs/AUDITORIA_FASE3.md). O desenho está no GDD, seção 17. **Fora da execução autorizada abaixo, esta fase não roda em modo autônomo** (na execução de 2026-11-16 nenhuma parte para nem espera o autor: valem a autorização e a extensão dos parágrafos seguintes)**:** cada parte termina com a verificação obrigatória, os testes da parte e o roteiro de teste entregue; depois para e espera o autor jogar e aprovar. Nenhuma parte começa antes de o autor aprovar a anterior e decidir as pendências que ela usa (GDD 17.20).
> **Autorização do autor (2026-11-16), só para a execução noturna dessa data:** modo corujão em toda a Fase R, na ordem R1 → RD → R2a → R2b → R3 → R4. As paradas para o autor viram **revisão posterior por tag** (fase-r1, fase-rd, fase-r2a, fase-r2b, fase-r3, fase-r4): o item (5) abaixo passa a ser cumprido de manhã, jogando as partes na ordem das tags. Bloqueios vão para docs/PENDENCIAS.md. O portão completo desta execução está no CLAUDE.md (Fase R).
> **Extensão (D-037, 2026-11-16):** o autor autoriza seguir a Fase R inteira sem esperar pelo teste dele (RD → R2a → R2b → R3 → R4), e depois fechar o resto das pendências da R1 (P-02, P-03, P-09) com a tag `fase-r-final`. Parar ali; a Fase 6 não começa.
> **Precedência nesta execução:** a autorização do corujão prevalece sobre toda instrução de "pare", "pergunte" ou "espere o autor" do CLAUDE.md e do ROADMAP, inclusive dentro de cada parte. Nos casos em que antes o trabalho pararia:
>   - **decisão nova ou dúvida de lei:** escolher a leitura mais conservadora e coerente com o GDD, citar o artigo ou a fonte, registrar em docs/PENDENCIAS.md como "a confirmar pelo autor" com como reverter, e seguir;
>   - **download bloqueado ou manual:** registrar em PENDENCIAS.md (URL, arquivo, onde pôr no cache), usar a substituição prevista marcada como lacuna, e seguir;
>   - **termos de uso das fontes da RD:** o autor confere depois; nesta execução a RD roda citando as fontes;
>   - **sem janela para os testes de fumaça:** registrar em PENDENCIAS.md, marcar a parte "com pendências", seguir só com o que não depende de tela (Core, dados, testes sem janela) e tentar os testes com janela de novo ao fim de cada parte.
> Continuam valendo sempre: nada de push, reset --hard, rebase, --no-verify ou apagar dados; autoria e neutralidade.
> Partes, na ordem fora desta execução: R1, R2a, R2b, R3, RD, R4.
> Uma parte só está pronta quando: (1) todos os itens estão marcados; (2) os testes automáticos listados passam junto com a verificação obrigatória; (3) **os testes de fumaça que exigem janela foram rodados e passaram** (abaixo); (4) o roteiro de teste da parte está atualizado com os nomes reais dos controles; (5) o autor jogou o roteiro e aprovou.
> **Testes de fumaça com janela.** No modo sem janela o Godot não entrega cliques de mouse à interface, então estes só valem com janela. Comando, na raiz do repositório, depois de `godot --headless --build-solutions --quit`: `godot --path . res://src/UI/Debug/UiShot.tscn -- --target=<alvo>` (sem `--headless`; código de saída 0 = passou). Alvos que exigem janela hoje: `eventos` (clica todas as opções de todos os eventos e confere que a tela volta a aceitar cliques) e `layout` (1920x1080 e 1366x768; falha se algum controle passar da janela). Os demais alvos (`newworld`, `creator`, `picker`, `hub`, `campaign`, `map`, `mandate`, `prefeitura`) rodam com e sem janela e também entram. Cada parte da Fase R acrescenta os seus alvos a esta lista (R1 acrescentou `creator17`, personagem de 17 anos que começa fora da disputa, e `explicacao`, tooltips em três partes, termos do glossário com link e F1, que exige janela; R2a acrescentou três alvos que exigem janela: `agenda`, marcar compromissos clicando nos turnos, previsão com o cansaço no terceiro igual, bloqueio com motivo, desmarcar e cartões de resultado no feed; `zona`, clique na zona do mapa, ficha, "Marcar compromisso aqui", marcador e feed que centraliza o mapa; e `lideranca`, visita marcada pela ficha da pessoa, pedido respondido, lembrança e promessa no Livro pela Enciclopédia; e pôs no `layout` e no `explicacao` a tela central em cidade pequena, média e metrópole com as áreas, fichas, marcação e cartão do pedido; R2b acrescentou quatro alvos que exigem janela: `equipe`, contratar um coordenador clicando com a previsão antes, delegar um compromisso, pausa de caixa abaixo de duas semanas de folha, conselho da semana, decisão da equipe forçada no cartão, repetir a semana anterior, aviso de estresse e demitir com confirmação; `verba`, bloqueio antes de 16 de agosto com o motivo, verba em dois canais com as duas contas na previsão, resultado explicado, comício sem verba de Rua, modo Verba do mapa e limite legal de cabos; `plataforma`, mudar uma posição com a previsão por grupo e por zona, posição assumida no Livro, bandeiras, promessa a uma zona e tema da entrevista; e `objetivos`, de 1 a 3 objetivos na coluna direita, fixar, dispensar e o cartão de objetivo cumprido no feed; e pôs no `layout` e no `explicacao` as telas novas: Campanha com os três blocos, Equipe, ficha de membro, decisão da equipe, marcação com representante e com tema, Verba e cada canal, Plataforma, tema, prometer e Objetivos, com a conferência de que o painel da área mantém 400 px; R3 acrescentou três alvos que exigem janela: `rivais`, ficha do rival aberta pela área Pessoas, pelo marcador do mapa e pelo feed, ataque com a previsão antes e bloqueio do segundo, investigar, pacto com previsão e aviso de quebra na marcação, ataque com prazo respondido pela faixa sem travar o tempo, prazo vencido e painel da chapa; `midia`, veículos com linha e alcance, horário gratuito, anúncio bloqueado em emissora com o art. 44, entrevista com veículo escolhido e tema em alta na previsão, modo Disputa do mapa, notícia com fato de origem, boato marcado, pauta, anúncio escrito com o limite do art. 43 e cartão de evento com pessoas e veículo; e `contas`, barra do limite, risco em faixa, oferta de doação com recibo e "por fora" com previsão, esqueleto com quem sabe, fontes, prazos e o julgamento das contas depois da eleição; e pôs no `layout` e no `explicacao` as telas novas: Pessoas com a chapa, painel da chapa, ficha do rival, atacar, Mídia, ficha de veículo, entrevista com veículo, ataque com prazo, Finanças com o bloco Contas, painel Contas, oferta de doação, modo Disputa e cartão de evento com pessoas; a P-24 acrescentou `mapa_pequeno`, que exige janela: Mantena, duas cidades pequenas com distritos e uma metrópole em 1366x768 e 1920x1080, nomes de zona sem sobreposição e dentro do mapa, núcleo urbano no enquadramento inicial, "Cidade inteira" e volta clicados, e save da divisão de zonas anterior recusado com a mensagem; R4 acrescentou cinco alvos que exigem janela: `gabinete` (nomeia um assessor clicando, com a previsão e quem vai lembrar antes, faz uma indicação com previsão e resultado, destina uma emenda escolhendo a zona no mapa, confere a obra no modo Obras, abre a contagem nominal e fecha um acordo de voto), `obras` (inicia uma obra de prefeito escolhendo a zona no mapa, escolhe a modalidade de licitação com o bloqueio explicado e o artigo, acompanha no mapa, inaugura a obra pronta e, na campanha de reeleição, confere o bloqueio do período vedado e a entrega sem cerimônia), `fiscal` (folha na faixa prudencial com as três marcas na barra e o que cada faixa bloqueia, mesa de negociação com o reajuste vedado pelo art. 22 e a revisão geral possível, mínimos de saúde e educação e repasse à Câmara), `indicadores` (painel em unidade real com fonte no tooltip, ficha com valor, unidade, fonte, variação no mandato, comparação com o estado e os fatores de "por que mudou", marca de estimativa, composição da Câmara e a Ficha da cidade fora de mandato) e `entressafra` (caminhada com a zona escolhida no mapa, previsão, resultado no feed e cansaço na terceira igual, turnos que acabam com o motivo e voltam na segunda-feira, visita a liderança, posição no Livro, entrevista com tema e ida à pré-campanha levando a presença); reescreveu `mandate` e `prefeitura` para a tela central, com cliques (passam a exigir janela); e pôs no `layout` e no `explicacao` as telas novas de vereador, prefeito e entressafra, em cidade pequena e metrópole); a `fase-r-final` acrescentou `opcoes`, que exige janela (Opções pelo menu e pela tela central, cada pausa de `data/time.json`, pausa da semana sem compromisso desligada com clique e sem parar o tempo no domingo, gravada no perfil, e dica que volta com Religar as dicas), pôs no `layout` e no `explicacao` as telas fora da tela central (menu, Opções, Novo Mundo, criação, seletor de cidade, mapa do estado, debate, urna, apuração e fundação de partido) e Mesquita (MG) no `mapa_pequeno`, com o contraste dos modos Minha força e Partido líder e os registra aqui e no CLAUDE.md; a RL acrescentou `rl`, que exige janela (Mantena em 1366×768, 1920×1080 e 2560×1080: a tela abre no painel de zonas; o relógio anda e bate com a hora do Core, com a linha do dia, os três turnos e as marcas de 6h a 22h; a barra de cima fica só com data, tempo, fase e contagem; o painel Você mostra os números do Core com tooltip em três partes e fica à vista com qualquer área aberta; o painel de zonas fica sincronizado com o mapa nos dois sentidos e reordena; "Fazer agora" conclui pela ficha sem abrir a agenda, com o estresse do terceiro turno na previsão e "Fazer amanhã de manhã" sem turno hoje; o número flutuante sobe da zona; a pilha de janelas só deixa clicar na de cima, prende o foco na modal e fecha com Esc; os painéis se refazem pelo evento do Core; a faixa da virada do dia some na primeira hora; e o feed tem retratos); com `--semanas=pasta`, o mesmo alvo grava as capturas das semanas 1, 10 e 20. Se a máquina não puder abrir janela, a parte não é dada como pronta: pare e informe.

### R1 — Tela central, mapa da cidade e tempo corrente · **com pendências (tag fase-r1)**
Conteúdo de jogo igual ao atual (6 ações, pesquisas, eventos, rivais como estão), na tela nova.
- [x] Trazer o branch `wip/idade-inicial` (commit `bf4fa1b`, sem testes) para a main: idade inicial de 17 a 45 anos; 18 anos completos na data-limite do registro (15/8) para disputar vereador, com a criação avisando quando o personagem só poderá disputar a eleição seguinte; profissões com idade mínima (`min_age`) e a profissão Estudante; família coerente com a idade (`data/family.json`: teto de filhos por idade, idade mínima para ter filho, estado civil por idade, idade do cônjuge). **Com os testes que faltam:** `MeetsAge` nos limites (aniversário em 15/8 e em 16/8; cada cargo); profissão indisponível abaixo da idade mínima e Estudante disponível aos 17; gerador de família sem filho fora da regra, sem cônjuge menor de 18 e sem estado civil abaixo da idade mínima, em 10.000 sorteios; NPCs continuam entre 25 e 45; personagem de 17 anos atravessa a carreira até a primeira eleição em que é elegível; DataValidator cobrindo `family.json` e `min_age`; teste de fumaça `creator` com idade 17. Atualizar o GDD 3.3 (faixa de idade, Estudante) e registrar a decisão no DECISOES
- [x] Importador, modo `zonas`: contornos e população das zonas de todos os municípios pela Malha de Setores e pelos agregados do Censo 2022 (GDD 17.5), com os testes do importador. **Teto de 30 MB para `data/geo/zonas/` no repositório:** o importador mede a saída e falha, sem gravar, se passar; para caber, aumenta a simplificação dos contornos e, se ainda não bastar, agrupa mais setores por zona (menos zonas por porte, nunca abaixo do mínimo de cada porte). O tamanho final e a tolerância usada vão para `_fontes.json`
- [x] Tela central (GDD 17.3 e ART_GUIDE, Interface): barra superior, trilho, painel da área, mapa 2D das zonas com os modos Partido líder e Perfil, coluna direita (Notícias, Objetivos, Intenção), faixa e cartão de decisão
- [x] Tempo corrente (GDD 17.4): dia como unidade no Core, pausa e 1x/2x/4x, simulação semanal na segunda-feira, pausa automática configurável por tipo, `data/time.json` — **Opções → Tempo na `fase-r-final` (P-03 fechada)**
- [ ] Painéis com o conteúdo atual: campanha, mandato de vereador e prefeitura passam a ser áreas da tela central; as telas antigas saem — **só a campanha; mandato e prefeitura continuam nas telas antigas (PENDENCIAS P-01)**
- [x] Previsão e explicação (GDD 17.2, peça 6) para as ações, pesquisas e escolhas de evento atuais: faixa antes do clique, "O que mudou" depois
- [x] Tooltips em três partes em todos os controles, nomes fictícios com o que são, termos do glossário com link (princípio j) — **tela central e Enciclopédia na R1; as demais telas na `fase-r-final` (P-02 fechada)**
- [x] Enciclopédia (F1): Glossário, Como funciona, Este mundo, Sua carreira; `data/encyclopedia/`
- [x] Explicação de primeira vez, dispensável, guardada fora do save
- [x] Fontes OFL em `assets/fonts/` e ícones do trilho, com crédito
- [x] Criação de personagem: trocar o outdoor (proibido, Lei 9.504, art. 39, § 8º) por adesivo, bandeira e peça para redes na pré-visualização e em `CampaignMaterials` (GDD 3.4)
- [x] Crédito da Malha de Setores e dos agregados do Censo 2022 em `_fontes.json` e `assets/CREDITS.md` (os termos de uso são conferidos pelo autor antes da RD)
- **Princípios que passa a cumprir:** f (para o conteúdo atual), i, j. Parcialmente b (o tempo corre e os rivais agem em dias visíveis no feed).
- **Testes automáticos:**
  - Core: `AdvanceDay` sete vezes produz o mesmo estado que o `AdvanceWeek` atual com a mesma semente (campanha, mandato e prefeitura); uma semana sem decisão não gera pausa; cada tipo de pausa respeita a configuração.
  - Core: toda ação, pesquisa e escolha de evento devolve previsão com faixa e explicação não vazia; em 1.000 execuções o resultado cai na faixa em 80% ou mais.
  - Importador: zonas dentro do polígono do município; nenhum setor sem zona; urbano e rural separados; `data/geo/zonas/` com 30 MB ou menos.
  - Importador, população: a soma da população total das zonas (agregado básico, que não tem supressão) é igual à população do município no Censo, sem tolerância; o importador falha se encontrar célula suprimida no básico ou município com diferença, listando os casos. Para os blocos com supressão por sigilo (idade, renda, saneamento, alfabetização), o teste é outro: a soma das categorias publicadas nunca passa da população da zona, a cobertura de cada bloco fica gravada por zona, e zona com cobertura abaixo de 80% usa a distribuição do município, com a lacuna em `_fontes.json` (GDD 17.5).
  - Interface (teste de fumaça ampliado): nenhuma sobreposição ou texto cortado em 1366×768, 1920×1080, 1280×800 e 2560×1080, em 100% e 130% (ART_GUIDE, Interface, seção 9); barra superior com até 5 números; todo acontecimento que pausa tem faixa de decisão.
  - Interface: todo controle interativo tem tooltip nas três partes; todo controle desativado diz o motivo e o que falta; todo termo de `glossary.json` na tela tem link para o verbete; F1 abre a Enciclopédia em todas as telas.
  - DataValidator: limites de tamanho de texto do ART_GUIDE; todo verbete citado existe.
- **Roteiro do autor:** docs/ROTEIRO_TESTE_R1.md

### R2a — Território, agenda, lideranças e Livro de compromissos · **com pendências (tag fase-r2a)**
Primeira metade da antiga R2. Termina, para e espera o autor (regra para execuções futuras; na execução de 2026-11-16, pela D-037, terminou com a tag e a R2b começou em seguida).
- [x] Território na simulação (GDD 17.5): presença por zona, segmentos por zona, voto por zona, decaimento e transbordo, demanda da zona, Ficha da zona, modo Minha força
- [x] Agenda com lugar e dia (17.6): compromissos, turnos, cansaço, ocasião, deslocamento, convites, eventos da cidade; sai a energia semanal. Ainda sem delegação (depende da equipe, R2b); o comício usa o custo fixo atual até a verba por canal (R2b)
- [x] Pessoa com memória e Livro de compromissos (17.2): promessas, dívidas, posições assumidas e esqueletos gravados e visíveis na Enciclopédia (Sua carreira)
- [x] Rede de lideranças (17.10): descobrir, visitar, pedido, apoio por zona, rivalidades, deriva sem contato; a promessa aceita entra no Livro
- [x] `tools/Planalto.BalanceSim`: robô de campanha que escolhe o compromisso de maior ganho previsto, com relatório por porte (`docs/balance/r2a.md`) — **o critério do princípio a passa nos portes pequeno e médio e não passa no grande (41,4%) e na metrópole (59,8%): PENDENCIAS P-15**
- **Princípios que passa a cumprir:** a, g; c para lideranças; d para promessas a lideranças; e (conflito de convites, pedido de liderança).
- **Testes automáticos:**
  - BalanceSim (200 campanhas por porte): nenhum tipo de compromisso é a melhor escolha em mais de 40% das semanas, nenhum par (tipo, zona) em mais de 25%.
  - Core: terceiro compromisso igual em 2 semanas rende no máximo 50% do primeiro, e a previsão informa o fator.
  - Core: soma dos eleitores das zonas igual ao eleitorado do município; projeção por zona somada igual à do município.
  - Core: toda liderança tem nome, aparência, interesse e lembranças; visita, promessa, negociação e recusa gravam lembrança; liderança sem contato por 8 semanas perde relação.
  - Core: toda promessa aceita está no Livro com destinatário, objeto e prazo, e sobrevive ao save e à passagem para o mandato.
  - Lint de conteúdo: toda escolha relevante grava rastro; pesos de interesse por tipo de liderança dentro de 2 para 1; eventos religiosos da cidade proporcionais à composição do Censo.
  - Campanha inteira jogada sozinha em 3 sementes por porte, do prazo de filiação à apuração, sem erro.
- **Roteiro do autor:** docs/ROTEIRO_TESTE_R2A.md

### R2b — Equipe, verba, plataforma e objetivos · **com pendências (tag fase-r2b)**
Segunda metade da antiga R2. Só começa com a R2a aprovada. Termina, para e espera o autor (regra para execuções futuras; na execução de 2026-11-16, pela D-037, começou sem esperar a aprovação da R2a, terminou com a tag `fase-r2b` e a R3 começou em seguida).
- [x] Equipe (17.7): funções, candidatos com vínculos, qualidade em faixa, conselho da semana, eventos próprios; delegação de compromissos na agenda
- [x] Verba por canal (17.8): quatro canais pagos (Rua, Redes com impulsionamento, Material gráfico, Cabos) com alvo, retorno decrescente e disputa de canal por zona; o comício passa a depender do canal Rua. Rádio e TV não são canal de verba: presença conquistada por entrevista e horário gratuito
- [x] Plataforma (17.9): posições, bandeiras, posição conhecida, promessas locais, custo de mudar, credibilidade
- [x] Gerador de objetivos (17.2, peça 6) e painel de Objetivos
- [x] BalanceSim ampliado: o robô passa a repartir verba, contratar e posicionar (`docs/balance/r2b.md`) — **fora do critério no canal de verba em três portes, no par (tipo, zona) do porte pequeno e no tipo de compromisso da metrópole: PENDENCIAS P-20**
- **Princípios que passa a cumprir:** h; c para a equipe; d para posições e promessas de plataforma; completa a, e e f na campanha.
- **Testes automáticos:**
  - BalanceSim rodado de novo com os sistemas desta parte: os limites de 40% e 25% do princípio a continuam valendo; nenhum canal de verba é o melhor destino em mais de 40% das semanas; média de 1 ou mais decisões com troca por semana.
  - Core: toda pessoa da equipe tem nome, aparência, interesse e lembranças; contratação, conselho ignorado, briga e demissão gravam lembrança; demitido com lealdade baixa que conhece esqueleto pode vazar.
  - Core: dobrar a verba de um canal rende entre 35% e 45% a mais; verba de rival no mesmo canal e zona reduz o efeito.
  - Core: mudar e desmudar uma posição em 2 semanas custa mais que mudar uma vez; posição desconhecida vale a do partido; toda promessa e toda mudança de posição entram no Livro.
  - Core: em qualquer dia há de 1 a 3 objetivos ativos com condição, prazo e origem; 4 ou mais tipos numa campanha.
  - Lint: toda decisão tem ganho e custo de naturezas diferentes.
  - Regras de lei dos canais em objeto de configuração, com teste: limite de contratação de cabos pelo eleitorado (art. 100-A), regras do impulsionamento (art. 57-C), nenhum gasto possível com rádio, TV, outdoor ou brinde.
  - Campanha inteira jogada sozinha em 3 sementes por porte, sem erro.
- **Roteiro do autor:** docs/ROTEIRO_TESTE_R2B.md

### R3 — Rivais com estratégia, mídia local e doadores · **com pendências (tag fase-r3)**
- [x] Rivais (17.11): perfil, plano semanal, gatilhos, resposta a ataque, alianças, pactos, painel da chapa
- [x] Mídia local (17.13): veículos, linha editorial, feed a partir de fatos, tema em alta, entrevista, pauta, resposta a ataque com prazo, boato
- [x] Doadores e prestação de contas (17.12): fontes, limite, painel Contas, risco, julgamento, representação; esqueletos com "quem sabe" e chance de vir à tona
- [x] Eventos atuais (32) ligados a pessoas, zonas e fatos do save; eventos dinâmicos disparados por fatos (GDD 15.4)
- [x] Conferir, item por item e no texto oficial, os artigos de campanha citados de memória no GDD 17.20 (Lei 9.504, arts. 18-C, 22-A, 23, 30-A, 39, 43, 44, 57-C, 77 e 100-A; doação de empresas), registrar o resultado no DECISOES com o artigo e a data da consulta, e só então criar os objetos de configuração. O que não puder ser conferido fica marcado e vai ao autor
- [x] Rádio e TV como presença conquistada: horário eleitoral gratuito com o tempo do partido (D-010) e o rendimento do marqueteiro
- **Princípios que passa a cumprir:** b, c (rivais, doadores, jornalistas), d (contas e esqueletos).
- **Testes automáticos:**
  - Campanha de 40 semanas sem comando do jogador: 3 ou mais notícias por semana que não citam o jogador; liderança muda em 1 ou mais zonas; presença de rival muda em mais da metade das zonas.
  - Depois de um ataque, 80% ou mais dos rivais não calculistas respondem em até 14 dias; rival ultrapassado no reduto troca de plano em até 2 semanas.
  - Nenhuma notícia sem fato de origem; tom de cada veículo coerente com a linha em 95% ou mais dos casos; boato sempre marcado.
  - BalanceSim: campanha com mais de 30% do limite "por fora" perde o mandato em 25% a 40% das carreiras em 2 anos; limpa, em menos de 1%.
  - Regras de lei em objetos de configuração com o padrão da lei; teste de cada limite.
  - Nenhum nome de veículo coincide com a lista de bloqueio.
- **Roteiro do autor:** docs/ROTEIRO_TESTE_R3.md

### RD — Dados reais de partida · **com pendências (tag fase-rd)**
Entra antes da R4, que usa os indicadores. Só importação e validação; nada muda na tela além dos números.
- [x] Importador: economia municipal, finanças municipais e estaduais, indicadores sociais e séries nacionais das fontes da tabela do GDD 17.17, com ano, endereço e data em `_fontes.json` e crédito em `assets/CREDITS.md` — **feito para PIB, emprego formal (RAIS), finanças (SICONFI), IDEB, mortalidade infantil, leitos, homicídios, saneamento, desocupação e séries nacionais; faltam as transferências (FPM e FPE) e o saldo do CAGED (PENDENCIAS P-14)**
- [x] Lacunas registradas com a regra de substituição de cada indicador (GDD 17.17)
- [x] Receita real por município no lugar da receita por habitante estimada da D-031
- [x] Tábuas de mortalidade do IBGE em `data/demography/`: por UF (Projeções da População 2024), com a tábua completa do Brasil de 2024 como reserva e como perfil para a idade simples (GDD 17.16); teste: a probabilidade recomposta de cada faixa etária é igual à publicada
- [x] Religião e escolaridade por área de ponderação (municípios com duas ou mais) e eleitorado e votação de 2024 por zona pelos locais de votação do TSE (aprovados, GDD 17.20) — **religião, escolaridade e eleitorado feitos; a votação de 2024 por zona não (PENDENCIAS P-10); o uso do eleitorado por local de votação no modelo de voto está a confirmar (P-11)**
- [x] População: municípios reescalados para acompanhar as Projeções da População 2024 por UF
- [x] Antes de começar: o autor confere os termos de uso das fontes desta parte (GDD 17.20, item 7) — **cumprido pela D-037:** IBGE aceito com citação (D-037); as demais fontes rodaram citadas, como o corujão autoriza, e ficam para conferência (PENDENCIAS P-13)
- **Princípios que passa a cumprir:** dá base a b e f na R4.
- **Testes automáticos:** todo município tem valor ou lacuna registrada para cada indicador; totais por UF iguais aos oficiais onde a fonte publica total; nenhum valor fora da faixa plausível declarada em `data/`; nenhum número escrito à mão (todo campo com fonte em `_fontes.json`); DataValidator cobrindo os arquivos novos.
- **Roteiro do autor:** docs/ROTEIRO_TESTE_RD.md

### R4 — Mandatos na nova tela, com gestão nova · **com pendências (tag fase-r4)**
- [x] Mandato de vereador (17.14): gabinete, requerimento, indicação, fiscalização, emenda com obra no mapa, negociação de voto, contagem nominal, instrução permanente de voto, base que cobra
- [x] Prefeitura (17.15): secretários, obras com licitação e prazo no mapa, servidores e limite fiscal, convênios, Câmara que negocia, crises com causa
- [x] Indicadores ancorados em números reais e mostrados em unidade real; modelo de evolução (17.17)
- [x] Renovação do mundo político no município (17.16): envelhecimento, mortalidade, saídas e entradas de vereadores, prefeito, lideranças e equipe; luto, suplente e vice pela regra real
- [x] Eventos de mandato (legislativo e executivo), Mesa, CPI e cassação (pendências das D-027 e D-031)
- [x] Cobrança de promessas e dívidas do Livro de compromissos
- Pendências: agenda por dia no mandato (P-30), objetivos do mandato (P-31), o que ficou fora (P-36; a idade média nas Câmaras pequenas foi resolvida pela D-051); par do porte pequeno e régua da D-026 continuam na P-26.
- **Princípios que passa a cumprir:** todos, também no mandato.
- **Testes automáticos:**
  - Carreira (campanha + mandato) em 3 sementes: 70% ou mais das promessas geram cobrança; nenhuma inauguração sem obra entregue; nenhuma greve sem negociação anterior.
  - Mandato de 4 anos sem comando: obras do prefeito NPC andam, pelo menos 1 crise com causa, notícias semanais.
  - Limites de lei (folha, mínimos, inauguração no período vedado) em objetos de configuração, com teste.
  - Indicadores: valor inicial igual ao dado importado; conversão de ida e volta entre unidade real e escala de jogo sem perda; evolução dentro dos limites plausíveis em 40 anos simulados.
  - Renovação municipal em 40 anos: a Câmara nunca fica com cadeira vaga por mais tempo do que a regra permite; idade média dos vereadores estável (variação menor que 5 anos entre a 1ª e a 10ª legislatura); toda morte de titular resulta em posse de suplente ou vice, ou em eleição, conforme a regra configurada.
  - Vacância no Executivo, um teste para cada caminho (GDD 17.20, item 12): morte ou renúncia de prefeito e vice na primeira metade do mandato leva a eleição direta e, na segunda, a indireta; vacância por decisão da Justiça Eleitoral leva a nova eleição, indireta só a menos de 6 meses do fim do mandato.
  - BalanceSim de mandato: nenhum instrumento é a melhor escolha em mais de 40% das semanas.
- **Roteiro do autor:** docs/ROTEIRO_TESTE_R4.md

## RV — Redefinição visual (protótipo e estilo; base das partes RV-A, RV-C e RV-B)

> Decidida pelo autor em 16/11: **D-043** (retratos e personagens em bonecos 3D estilizados de formas suaves) e **D-044** (mapa da cidade 2D detalhado e fiel ao lugar real, com a interface em janelas flutuantes); origem na D-041 e nos adendos. O protótipo foi entregue ao fim da `fase-r-final`, no branch `rv-prototipo`, que **não entra na main antes da RL** (D-054). Esta parte não tem tag própria: seus itens entram nas partes da D-055, na ordem **RL → RV-A → RV-C → RV-B**, e cada item abaixo diz em qual. Guia: `docs/ART_GUIDE.md`, Parte 1 (proposta a aprovar pelo autor). Pendências: P-39 (fontes e tamanhos do mapa de base) e P-40 (o que o protótipo pede ao autor).

**Referências de composição e de conteúdo** (não de estilo; o código delas não entra no jogo):
- `docs/referencias/rv_tela_central.html`: a tela central sobre o mapa da cidade, com o mundo ocupando a tela inteira, janelas semitransparentes flutuando sobre ele e a interface nas bordas.
- `docs/referencias/rv_cenas.html`: quatro cenas das telas fora do jogo, trocadas pelos botões do topo ou pelas teclas 1 a 4:
  1. **Menu:** o comitê de campanha à noite, com o menu sobre a cena.
  2. **Criação de personagem:** um estúdio de foto, com o santinho impresso ao lado.
  3. **Urna:** a seção eleitoral, com mesários e fila.
  4. **Apuração:** uma transmissão de TV, com a Câmara se enchendo e o comitê comemorando no canto.
- Os dois arquivos foram desenhados pelo autor com um consultor. Valem como referência de **composição e de conteúdo** de cada tela, **não de estilo**: o estilo final é 3D estilizado de formas suaves. O código deles não entra no jogo.
- Referência de interface: Geopolitical Simulator. Referências de estilo dos personagens e cenários: Two Point Hospital, Overcooked, Planet Coaster.

**Bases e artista (DECISOES D-045):**
- Bases livres CC0 para cenários, animações, corpos e multidão: KayKit e os Human Base Meshes do Blender; o MPFB só como referência.
- Quaternius fora até a confirmação da licença por escrito.
- Artista 3D só para cabeça, expressões e roupas, depois do protótipo aprovado e de um teste pago (`docs/BRIEFING_ARTISTA_3D.md`).

**Protótipo · entregue ao fim da `fase-r-final` (branch `rv-prototipo`; capturas, prancha e `medidas.md` em `docs/referencias/rv/` nesse branch):**
- [x] Mantena (MG) e Belo Horizonte, com o mapa de base detalhado e os modos do jogo por cima (Minha força, Partido líder, Renda, Idosos, Urbana/rural). Relevo não feito (nenhuma fonte de elevação licenciada, P-39).
- [x] Uma janela flutuante e o menu como cena (comitê à noite, 6 personagens, "PALANQUE").
- [x] Teste A/B de cabeça, prancha e autocrítica (abaixo). A prancha e as cabeças A e B **esperam o julgamento do autor; nenhuma foi declarada boa o bastante.**

O que foi pedido para o protótipo (registro):
- Mantena (MG) e uma metrópole, com o mapa de base detalhado e os modos do jogo por cima; uma janela flutuante e o menu como cena.
- **Teste A/B de cabeça** (D-045, adendos), as duas pelo contrato técnico do ART_GUIDE (Parte 1, seção 9, "Cabeça substituível"), com a cena de teste de cabeça:
  - **Cabeça A, base CC0:** a cabeça estilizada dos Human Base Meshes do Blender, adaptada ao contrato. Se a conversão exigir o Blender instalado, nada é instalado: fica a PENDENCIAS P-29 e o protótipo segue com a B.
  - **Cabeça B, gerada por código:** formas suaves (cápsula, esferas, curvas) montadas no Godot ou por um gerador em `tools/`, sem pacote novo. Expressões por troca de peças ou textura do rosto, não por deformação. Estilo de boneco de desenho animado, não low-poly facetado.
  - **Personagens:** para cada cabeça, os mesmos 3 personagens (pele clara, média e escura; gêneros e idades diferentes, um jovem e um idoso com grisalho e rugas pela Appearance), nas expressões neutra, sorriso e bravo, com cabelo e roupa trocáveis, cores por material e a cor do partido.
  - **Prancha** em `docs/referencias/rv/`: A e B lado a lado, com os mesmos personagens e expressões, em retrato (512, 256 e 96 px, os tamanhos do cache) e em corpo inteiro na cena do menu, em 1366×768 e 1920×1080.
  - **Autocrítica** de até 10 linhas no relatório da RV: o que ficou bom e ruim em cada uma, qual chega mais perto das referências com retoque, e onde só um artista resolveria. Nenhuma das duas é declarada boa o bastante: quem decide é o autor.
  - **Verificação:** as duas cabeças carregam pelo mesmo código de personagem; trocar A por B não muda nada fora do nó da cabeça; o cache de retrato funciona nos três tamanhos; 60 FPS na cena do menu com os 6 personagens.

**Resultado do protótipo:**
- **Verificação** (alvo de fumaça `rv_prototipo`):
  - as duas cabeças passam no contrato (A: 7.094 triângulos, shape keys; B: 4.786 visíveis no pior caso, troca de peças do rosto, com o aviso do material `interior_boca`);
  - cache de retratos: 54 de 54 acertos na segunda volta;
  - troca A → B: os 19 nós fora da cabeça ficaram iguais;
  - **60 FPS no menu: não passou.** Em qualidade cheia, 31 a 36 FPS; em qualidade leve (sem sombra nem brilho, 3D a 75% com FSR), 50 a 58.
- **Medidas do protótipo**, três rodadas, **com a máquina dividida com outros programas** (outro Godot com janela e compilações de outros trabalhos), sincronia vertical desligada, Radeon integrada (Ryzen 5 7520U):

| O que | Rodada 1 | Rodada 2 | Rodada 3 |
|---|---|---|---|
| Mapa de Belo Horizonte pronto | 777 ms (com a campanha) | 479 ms | 565 ms |
| Mapa de Mantena pronto | 1.900 ms (com o mundo) | 383 ms | 81 ms |
| Zoom e arraste em Mantena, FPS médio (1% pior) | 218 (55) | 233 (131) | 230 (124) |
| Zoom e arraste em BH, FPS médio (1% pior) | 160 (29) | 168 (96) | 167 (96) |
| Menu, cabeça A, cheia | 32 (27) | 32 (26) | 31 (20) |
| Menu, cabeça A, leve | 56 (36) | 55 (31) | 56 (41) |
| Menu, cabeça B, cheia | 34 (26) | 36 (24) | 36 (20) |
| Menu, cabeça B, leve | 50 (8) | 58 (46) | 56 (45) |

- **Arquivos e verificação do branch:** mapa de base de BH com 17,8 MB (7,1 MB com gzip), Mantena 0,54 MB. Verificação do branch: build sem avisos, 798 testes rápidos, 88 do importador, DataValidator, import e build do Godot, `rv_prototipo`, `layout` e `newworld`.
- **Autocrítica do protótipo** (resumo): a A tem o rosto mais acabado, mas parece criança e realista demais, com sorriso e bravo fracos a 96 px e o piscar incompleto; a B chega mais perto de Two Point Hospital e Overcooked e lê melhor o bravo, mas tem rosto oval simples, nariz e orelhas colados e pescoço fino e comprido. Nas duas, as três expressões só se distinguem com esforço a 96 px; o cabelo em casca parece capacete e o preto sai acinzentado; corpos e roupas parecem sacos e as mãos, luvas. No mapa, as quadras fecham em cerca de 80% dos códigos e os nomes de rua vêm sem acento. No menu, a composição funciona, mas a luz deixa os rostos escuros e o cartaz fica atrás do painel. Com retoque, a B chega mais perto das referências; só um artista resolveria rosto, cabelos, roupas e corpo.

**Itens da RV, por parte** (cada um se marca na parte em que entra; o desenho está no GDD 3.1, 3.6, 3.7, 7.4, 13, 17.3 e 17.5 e no ART_GUIDE, Parte 1):
- [ ] **RL:** base das janelas flutuantes: ficha da zona em pilha, só a de cima recebe clique, Esc volta, foco preso no cartão de decisão; painéis atualizados por sinal do Core (é o item da RL "Base para as janelas").
- [ ] **RV-A:** mapa de base de todo município em foco no importador: faces de logradouro, quadras remontadas, BC250 e, se licenciado, relevo, em formato compacto indexado (vértices indexados em 16 bits), recortado no município, guardado ou gerado como o autor decidir (P-39).
- [ ] **RV-A:** rótulos por nível de zoom com desvio de colisão (zonas e localidades de longe; ruas, rios e rodovias de perto); localidades da BC250 dando nome às zonas rurais sem bairro do IBGE.
- [ ] **RV-A:** interface em janelas flutuantes sobre o mapa, com a interface nas bordas (GDD 17.3, "Na parte RV"; ART_GUIDE, Parte 1, seção 5).
- [ ] **RV-A:** menu como cena que muda com a carreira (comitê de vereador, gabinete de prefeito, palácio), com o menu sobre a cena; com a cabeça aprovada ou, sem julgamento até lá, com a provisória do protótipo.
- [ ] **RV-A:** nome "Palanque" com o subtítulo "Simulador de Carreira Política" no título da janela, no menu, nos créditos e nos textos do jogo (D-046).
- [ ] **RV-A:** qualidade gráfica nas Opções (cheia, ou leve: sem sombra nem brilho e 3D a 75% com FSR), se o autor aprovar (P-40).
- [ ] **RV-A:** estilo final da interface (paleta do mapa de base, janelas, luz de dia e de noite) pelo ART_GUIDE, Parte 1, depois de aprovado.
- [ ] **RV-C:** mundo vivo pelo ART_GUIDE, Parte 1, seção 10 (os itens estão na RV-C, abaixo).
- [ ] **RV-B:** bonecos da cabeça aprovada: retratos 3D no lugar dos 2D, pelo `PortraitCache` nos três tamanhos (GDD 7.4). **Proposta**, a confirmar pelo autor (P-40).
- [ ] **RV-B:** cenas das telas fora do jogo: estúdio de foto na criação, seção eleitoral na urna, estúdio de TV na apuração (`rv_cenas.html`). **Proposta**, a confirmar pelo autor (P-40).

**Testes da RV** (crescem a cada parte):
- **RV-A:** o alvo `rv_prototipo` vira `rv`, com janela: carga do mapa de base, FPS com zoom e arraste, contrato de cabeça, cache de retratos, troca de cabeça só no nó, rótulos sem colisão.
- **RV-A:** nenhum texto visível ao jogador com "Planalto" como nome do jogo (D-046).
- **RV-A:** importador: toda quadra dentro do polígono do município; nomes de zona iguais aos de `data/zonas/`; teto de tamanho por município.
- **RV-A:** medição de FPS e de tempo de carga três vezes, **com a máquina livre**, em Mantena e numa metrópole, na placa-alvo definida pelo autor.
- **RV-C e RV-B:** os testes de cada parte, abaixo; o alvo `rv` passa a cobrir os elementos do mundo vivo e as cenas.

**Metas de desempenho:**
- **Do autor:** 60 FPS com zoom e arraste. No protótipo: de 160 a 233 FPS de média, 1% pior de 29 a 131.
- **Do autor:** mapa de base de uma metrópole pronto em menos de 3 s. No protótipo: Belo Horizonte de 0,48 a 0,78 s.
- **Proposta:** 60 FPS no menu com 6 personagens na placa-alvo a definir (P-40). No protótipo, na Radeon integrada, **o menu não chegou a 60 FPS em qualidade cheia** (31 a 36); só a leve chega perto (50 a 58).
- **Da RV-C e da RV-B:** 60 FPS com o mundo vivo ligado numa metrópole e na cena com a multidão.
- Todas as medidas do protótipo foram feitas com a máquina dividida com outros programas; remedir com a máquina livre antes de fixar a placa-alvo.

**Nome do jogo para o jogador (D-046), na RV-A:** junto com o menu como cena, o nome visível passa a ser "Palanque", com o subtítulo "Simulador de Carreira Política": título da janela, menu, créditos, textos do jogo e página de loja. Código, solução, namespaces e pastas continuam como Planalto.
- **Teste:** nenhum texto visível ao jogador (dados de `data/`, textos de tela, título da janela, créditos) contém "Planalto" como nome do jogo; "Palácio do Planalto" como lugar continua permitido.

**Critério de aprovação do protótipo** (o autor julga sem olhar o rosto; **situação: a julgar**):
- composição das telas conforme as referências;
- câmera: enquadramentos e zoom da cidade inteira até a rua;
- cor;
- menu como cena;
- 60 FPS com zoom e arraste;
- mapa de base de uma metrópole pronto em menos de 3 segundos.

**Critério do rosto** (não entra na aprovação do protótipo; vale primeiro para as cabeças A e B do teste A/B e, se o autor reprovar as duas, para o teste pago do artista; **situação: A e B esperam o julgamento do autor**):
- a cabeça passa na cena de teste de cabeça (nomes, materiais, expressões, orçamento);
- as 3 expressões se distinguem nos retratos de 96 px e de 512 px;
- a tintura funciona em todos os tons de pele do catálogo;
- o estilo bate com as referências.
Se o autor aprovar a cabeça A ou a B, o briefing do artista fica arquivado. Se reprovar as duas, o briefing é usado; aprovado o teste do artista, o contrato cobre as demais cabeças, as 8 expressões, os cabelos, as barbas e as roupas.

## Depois da Fase R: leitura e imersão (D-054, reorganizada pela D-055)

> Decidido pelo autor em 17/11, depois de jogar a `fase-r4` em Mantena ("entediante e bem confuso", "só texto", "mais fácil só realizar a ação"; e uma referência de tela com painéis e polígonos achada "pouco imersiva"). Ele quer três coisas: **(A) a cidade real desenhada no mapa; (B) gente na tela, em cenas; (C) o mundo se mexendo à volta.** Diagnóstico: os sistemas existem; falta a tela mostrar o jogo e dar a sensação de estar na cidade.
> **Ordem:** a correção bloqueante dos traços na criação e a `fase-r-final` vêm antes de tudo; depois **RL → RV-A → RV-C → RV-B**, cada uma com tag própria. O estilo final (fontes, paleta, menu como cena, nome "Palanque") acompanha a RV-A. A Fase 6 só começa depois.
> **Em todas:** lógica e números só no Core, a tela exibe; capturas ao autor **no meio** do trabalho, não só no fim (o autor quer corrigir o rumo cedo); dados geográficos só de fontes oficiais ou abertas, com crédito, e nomes de bairro do OpenStreetMap só onde o IBGE não tem bairro, com crédito (liberados pela D-058); assets 3D e de som só CC0 ou CC BY, com crédito, nada da Quaternius. Skills de apoio (orientam; GDD, ART_GUIDE e CLAUDE.md prevalecem): game-ui-ux, game-feel, godot-ui-control, godot-csharp, camera-systems, performance-optimization e audio-design.

## RL — Leitura mínima · tag `fase-rl`

> Na tela central atual, **sem trocar estilo**. Referência de disposição e comportamento: `docs/referencias/rl_tela_central.html` (composição e conteúdo, não estilo; o código dela não entra no jogo).

- [x] **Relógio** andando na barra superior (6h às 22h, no ritmo do tempo corrente) e **linha do dia** com os três turnos, o compromisso em andamento e os próximos; a virada do dia é visível.
- [x] **Painel "Você"**, sempre visível: turnos livres hoje e na semana, estresse com o limite que derruba os testes, dinheiro e semanas até a eleição; tooltip dizendo o que cada um limita.
- [x] **Painel de zonas** ordenável e sincronizado com o mapa (clicar na lista destaca no mapa, e o contrário): presença, líder da intenção, minha posição, lideranças conhecidas, demanda principal, última visita, cansaço por público.
- [x] **"Fazer agora"** como ação principal na ficha da zona e no painel de zonas: executa no próximo turno livre, avança o tempo até lá e mostra o resultado no mapa. Agendar vira opção secundária (planejar, convites com data). Limite de turnos, estresse e previsão antes do clique continuam valendo; bloqueado, diz o motivo.
- [x] **Número flutuante** na zona depois da ação ("+2,1 presença"), com transbordo para as vizinhas.
- [x] **Retrato** da pessoa em toda notícia e todo cartão que fala de alguém.
- [x] Base para as janelas: ficha da zona flutuante em pilha (só a de cima recebe clique, Esc volta, foco preso no modal); painéis atualizados por sinal do Core, sem ler o estado a cada quadro.
- **Testes:** fumaça com janela em 1366×768, 1920×1080 e 2560×1080: relógio andando, painel Você, painel de zonas sincronizado com o mapa, "Fazer agora" concluindo sem abrir a agenda, número flutuante depois da ação, pilha de janelas com Esc. Capturas da tela central nas semanas 1, 10 e 20 de uma campanha em Mantena.
- **Autocrítica de leitura:** para cada captura, três linhas como jogador novo (princípio i: o que acontece, o que pede atenção, o que posso fazer).
- **No meio:** capturas ao autor quando relógio, painel Você e painel de zonas estiverem prontos, lado a lado com a referência.
- **Fora da leitura mínima** (do pedido anterior da RL; o autor encaixa, PENDENCIAS P-37): gráfico da intenção por semana, primeira campanha guiada por objetivos, feed com fatores e objetivos com origem.
- **Entrega:** `docs/ROTEIRO_TESTE_RL.md` e capturas em `docs/referencias/rl/`.
- **Feito no branch `rl`** (sem tag; o coordenador integra): alvo de fumaça `rl`; escolhas a confirmar na PENDENCIAS P-41.

## RV-A — Cidade real · tag `fase-rva`

> O mapa de base do protótipo da RV vira o mapa do jogo. O estilo final (fontes, paleta, menu como cena, nome "Palanque") entra junto.

- [ ] Ruas com nome, rios, estradas, localidades, relevo sombreado, mancha urbana, praças e quarteirões, de fonte oficial ou aberta, com crédito (pesquisa de fontes e tamanhos na PENDENCIAS P-39, decisão do autor; itens da parte RV acima).
- [ ] Zoom da cidade inteira até a rua; câmera com zoom e arraste suaves e independentes do FPS, travada nos limites do município, com corte seco quando o feed centraliza.
- [ ] Zonas como cor translúcida por cima do mapa de base, com os modos do jogo.
- [ ] Luz de dia e de noite pela hora do relógio.
- [ ] Estilo final da interface (fontes, paleta) e o menu como cena, pela parte RV acima.
- **Metas:** 60 FPS com zoom e arraste; mapa de base de uma metrópole pronto em menos de 3 s.
- **Testes:** medição de FPS e de tempo de carga em Mantena e numa metrópole (três vezes, máquina livre); fumaça com janela do zoom até a rua e do modo de cada zona por cima; capturas em 1366×768, 1920×1080 e 2560×1080.
- **No meio:** capturas e um **vídeo de 20 a 30 s** (Mantena, de manhã e à noite) ao autor.

## RV-C — Mundo vivo · tag `fase-rvc`

> Tudo pela hora e pelo estado do Core; nada sorteado só para enfeite que contradiga o Core.

- [ ] Carros nas ruas e pedestres pela densidade de população da zona.
- [ ] Carro de som e cabos do rival no reduto dele, quando ele está em campo.
- [ ] Multidão no lugar do evento e do comício, do tamanho do público.
- [ ] Chuva por zona quando ela muda a previsão.
- [ ] Meu personagem e os rivais com ícone andando pelas ruas até o compromisso.
- [ ] Marcadores dos meus compromissos, dos rivais em campo e das lideranças pela cor do alinhamento; pulso onde acontece um fato (no máximo 3 por segundo); zona em disputa hachurada.
- [ ] Efeito proporcional à importância (quase nada no clique comum, grande na apuração; sem tremer a tela) e a opção "Reduzir piscadas".
- [ ] Áudio ambiente (rua, multidão, rádio) com volume nas Opções; assets CC0 ou CC BY com crédito.
- **Metas:** 60 FPS com o mundo vivo ligado numa metrópole.
- **Testes:** cada elemento confere com o estado do Core (carro de som só com o rival em campo naquela zona, multidão do tamanho do público, chuva só onde muda a previsão); fumaça com janela; capturas.
- **No meio:** capturas e um **vídeo de 20 a 30 s** (Mantena, de manhã e à noite) ao autor.

## RV-B — Cenas · tag `fase-rvb`

> Cenas curtas e puláveis em 3D estilizado de formas suaves, com a cabeça escolhida no teste A/B (parte RV).

- [ ] Primeiro: **caminhada** (personagem na rua, eleitores reagindo) e **comício** (palco, multidão do tamanho do público, bandeiras do partido).
- [ ] Depois: reunião com liderança; entrevista no estúdio de rádio ou TV; notícia como telejornal ou capa de jornal.
- [ ] O resultado da ação aparece no fim da cena.
- [ ] Opção para desligar as cenas; toda cena se pula com um clique ou Esc.
- **Testes:** cada cena abre, pula e mostra o mesmo resultado que o Core aplicou; desligadas, o jogo segue igual; 60 FPS na cena com a multidão.
- **No meio:** capturas ao autor.

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
- Pacote de nomes (GDD 17.18): **não aprovado pelo autor; suspenso.** Não é item deste roadmap

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
- [ ] Arte final de retratos (ilustrador ou pipeline próprio) — com a D-043, os retratos passam a ser renderizados dos bonecos 3D (parte RV, item da RV-B)
- [ ] Bonecos 3D parametrizados a partir de Appearance (seção 13.2): materiais tingíveis, formatos 3D de cabelo e barba mapeados dos estilos 2D — adiantado para a parte RV (D-043), com a cabeça aprovada no teste A/B ou a do artista
- [ ] Cerimônias de posse em 3D (seção 13.2), começando pela presidencial e pela de vereador, depois os demais cargos; sempre puláveis
- [ ] Cena da seção eleitoral (ilustração 2D com parallax; pela D-043 e `rv_cenas.html`, proposta como cena 3D estilizada na RV-B), sons originais da urna e acessibilidade da urna (áudio de confirmação, fonte grande)
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

# Progresso

## Relatório da manhã (corujão da Fase R, 2026-11-16)

**Partes concluídas:** só a **R1**, tag local `fase-r1`, **com pendências** (docs/PENDENCIAS.md). RD, R2a, R2b, R3 e R4 não começaram. Parei de propósito: preferi entregar a R1 testada, olhada em captura e corrigida em duas rodadas, a abrir a RD pela metade.

**O que jogar, nesta ordem:**
1. `docs/ROTEIRO_TESTE_R1.md` (20 minutos). Comece pela criação com 17 anos e o material de campanha novo; depois a tela central numa cidade média que você conheça e numa cidade pequena.
2. Se sobrar tempo, jogue uma campanha inteira em 4x e anote onde o jogo parou sozinho e se parou demais.

**As 5 coisas de que menos gostei no meu próprio trabalho:**
1. **O jogo ainda é raso.** A R1 troca a tela, mas as 6 ações continuam com efeito igual em qualquer zona; "Redes sociais" segue sendo a melhor ação quase sempre. O mapa informa, mas ainda não muda nenhuma decisão. Isso só muda na R2a.
2. **Mandato e prefeitura ficaram nas telas antigas** (P-01). Quem chegar à posse vai sentir o salto de qualidade para trás.
3. **Mapa de cidade pequena** (P-09): as zonas urbanas são pequenas perto da rural, os rótulos se sobrepõem e, quando um partido lidera tudo, o mapa fica de uma cor só.
4. **A intenção por zona é uma estimativa** (P-05): a da cidade repesada por idade e situação urbana ou rural. É honesta na tela ("estimativa por zona"), mas não é o voto por zona que a R2a vai calcular.
5. **Tooltips em três partes só na tela central e na Enciclopédia** (P-02), e não há tela de Opções para as pausas (P-03).

**Pendências que precisam do autor:** P-04 (artigos de lei citados de memória nos tooltips do material de campanha), P-05 (a estimativa por zona basta até a R2a?), P-06 (termos de uso do IBGE), P-08 (15 MB de atributos de zonas no repositório).

**Testes e números finais:**
- 455 testes no Core (eram 433) e 28 no importador; DataValidator limpo; build do Godot limpo.
- 12 alvos de fumaça com janela, todos passando: `newworld`, `creator`, `creator17` (novo), `picker`, `hub`, `campaign`, `map`, `mandate`, `prefeitura`, `eventos`, `layout`, `explicacao` (novo).
- Zonas: 28.135 em 5.570 municípios, 20,5 MB de contornos (teto de 30 MB) e 15 MB de atributos. A população das zonas bate com o Censo sem tolerância em 5.569 municípios; Boa Esperança do Norte (criado depois do Censo) tem uma zona só. Pela regra de sigilo, 1.423 blocos de perfil (idade ou renda) usam o do município.
- Previsão: em 1.000 execuções das ações, 100% dos resultados caíram na faixa prevista (critério: 80%).
- Tempo: sete dias produzem exatamente o mesmo estado que uma semana do motor antigo.

**Defeito de teste encontrado e corrigido:** o clique simulado dos testes de fumaça usava coordenadas da janela. Com a janela em 1965 px de largura, ele errava o botão "Mapa" da tela nova. Agora usa as coordenadas do viewport, e o teste `eventos` confere o clique num hub novo antes de tudo.

## R1 — Tela central, mapa da cidade e tempo corrente (2026-11-16, com pendências)
- **Idade inicial (D-036):** branch `wip/idade-inicial` trazido com os testes que faltavam: 17 a 45 anos, 18 aferidos em 15/8, profissões com idade mínima, Estudante, família coerente. Corrigi duas falhas do branch: validação que ainda exigia 18 anos e chave "uniao_estavel" que não carregava.
- **Zonas:** modo `zonas` do importador, com Malha de Setores e agregados do Censo 2022 (básico, idade, renda do responsável). Setores agrupados sem misturar urbano e rural; nomes de bairro ou distrito do IBGE, ou nomes do jogo. Contornos com tolerância proporcional ao tamanho de cada zona.
- **Core:** `Clock` (dia, semana no domingo, pausas por `data/time.json`), `Forecaster` (faixa de cada efeito e chance dos desfechos), `ZoneProjection` (intenção por zona, estimativa) e Enciclopédia em `data/encyclopedia/glossary.json`.
- **Tela central:**
  - barra com data, pausa, 1x/2x/4x, fase e quatro números com legenda;
  - trilho com 7 áreas e painel da área;
  - mapa 2D das zonas, com 4 modos, zoom, arraste e ficha da zona;
  - notícias e intenção de voto;
  - cartão de decisão sobre o mapa, com previsão por escolha e "O que mudou";
  - dicas de primeira vez, F1 em qualquer tela, tooltips em três partes;
  - fontes Inter, Oswald e Source Serif 4, e ícones próprios.
- **Criação:** outdoor (proibido) trocado por adesivo, bandeira e peça de redes.

**Autocrítica de imersão (portão 4):** joguei pelo teste de fumaça `campaign` (38 semanas, sem eleição) e pelos testes de autoplay do Core, e reli os quatro relatos do GDD 17.21. Contra o relato 1, faltam:
- o coordenador a contratar e o vínculo com o rival;
- o Seu Anselmo e o pedido dele;
- a Rádio Vale e o tema em alta;
- o rival aparecendo numa zona.

O que a R1 já dá é o palco: a cidade real em zonas, o dia passando, a previsão antes do clique e o motivo de cada bloqueio. A semana 20 ainda é igual à semana 2, porque nada no mapa muda a melhor ação e nenhuma pessoa lembra de nada. O feed só mostra eventos do próprio jogador e pesquisas, então o mundo ainda não "se mexe sozinho" na tela. É exatamente o que R2a (território, agenda, lideranças, Livro) e R3 (rivais e mídia) atacam.


## 1C — Mundo e partidos (2026-11-15)
Famílias, temas, lista de bloqueio (95 itens, não confirmados nas páginas do TSE, nomes contidos também bloqueados), gerador de partidos, WorldSetup com predefinições, travas, validações e cenários salvos, números de candidato.
282 → 292 testes; variedade de nomes 4.105 → 4.064 distintos em 10.000 gerações após a regra de nomes contidos.

## 1D — Cenário Brasil 2026 (2026-11-15)
`dotnet run --project tools/Planalto.DataImporter -- cenario` gera nacional.json e 27 UFs (2,35 MB) do TSE: 30 partidos fictícios (família pela pesquisa de Bolognesi et al. 2025, regra diagonal), Câmara 513, Senado 81, Assembleias 1.059, 5.569 municípios com prefeito, Câmara, força local e alianças de 2024.
Lacunas registradas: proporcionais do AP (usa 2022), 14 prefeituras pendentes, 21 Câmaras de 2020, Placas/PA sem fonte. Fundo e TV calculados no Core pela Lei 9.504; predefinição Brasil 2026 disponível no WorldSetup.

## 1E — Eleitorado e pesquisas (2026-11-15)
240 segmentos por município (zona × idade × renda × religião × escolaridade) do perfil do Censo, com afinidade partidária ajustada por IPF à força local de 2024 (Brasil 2026) ou gerada por tamanho e proximidade; modelo de voto da 5.3 com softmax, brancos/nulos e comparecimento com voto facultativo.
Calibração contra prefeitos de 2024: erro médio 11 pontos, vencedor certo em 72% (D-015). Pesquisas com amostra multinomial, viés de casa simétrico, margem de erro e pesquisa interna; 322 testes.

## Fase 2 — Criação de personagem (2026-11-15)
Appearance no Core (163 peças, 24 tons, pesos regionais do Censo, envelhecimento), arte placeholder SVG gerada por código (tools/Planalto.PlaceholderArt), retrato em camadas tingidas por shader com expressões e cache, NPCs com prenomes do IBGE por UF e década (193 masc., 230 fem.).
Telas Novo Mundo, Criação (5 abas, santinho, outdoor, logotipo, presets, desfazer) e seletor de município; teste de fumaça da UI (src/UI/Debug/UiShot.tscn) passando nos 4 fluxos; 348 testes.

## Fase 3 — Fatia vertical: vereador (2026-11-15)
Campanha completa no Core (RaceSetup com 6 partidos, 10 rivais NPC persistidos e candidatos coletivos; CampaignEngine com agenda, pesquisas, rivais, decaimento e apuração proporcional; EventEngine com 32 eventos e EventLint) e na interface (hub semanal, janela de eventos, urna, noite da apuração, save/load com autosave, véspera da eleição e ironman). Protótipo do mapa 2.5D: modo `geo` do importador gera data/geo/ (6,3 MB, 27 UFs) e a tela extruda os municípios (MG: 853 malhas em cerca de 300 ms, 60 FPS).
Balanceamento D-026 (conjunto de consideração no voto, rivais e coletivos ativos, empenho por porte): partido mais forte eleito em 96% das cidades pequenas e 58% das metrópoles; sem campanha, nunca. A aba Partido mostra a força local de cada partido. Roteiro em docs/ROTEIRO_TESTE_FASE3.md; 389 testes no Core, 28 no importador, smoke da UI em 6 fluxos.

## Fase 4 — Mandato legislativo (2026-11-15)
Mandato de vereador no Core (ChamberSetup e LegislatureEngine: Câmara com NPCs, base do prefeito, lideranças, comissões, pauta semanal, voto NPC, quóruns, fidelidade com advertência, sanção e expulsão, relações, capital político, emendas e projetos próprios), transição da carreira (CareerFlow: posse, reeleição com incumbentes do mandato, entressafra; PartyAffiliation: prazo de filiação e janela partidária) e fundação de partido (PartyFoundingEngine). Telas novas: mandato, entressafra e diálogo de fundação; a apuração leva à posse.
36 projetos municipais e 7 ações de gabinete em data/. 413 testes no Core, incluindo carreiras inteiras jogadas sozinhas (mandato de 4 anos e reeleição, 3 sementes); smoke da UI em 7 fluxos. Pendente para depois: eventos de mandato, eleição da Mesa e CPIs (D-027).

## Fase 5 — Prefeito (2026-11-15)
Candidatura a prefeito na pré-campanha (idade mínima na posse, vaga do próprio partido, prefeito incumbente e renúncia ao disputar outro cargo), mandato de prefeito no Core (ExecutiveEngine: orçamento por área com mínimos de saúde e educação e Tribunal de Contas, indicadores com atraso, 17 políticas por decreto ou lei votada na Câmara, coalizão por secretarias, aprovação por grupo, 7 crises) e debate de TV (DebateEngine: três rodadas, seis cartas, IA por estilo, cortes virais). Telas novas: prefeitura (com overlay de crise) e debate; o hub troca a candidatura e chama o debate; a apuração mostra o prefeito.
425 testes no Core (prefeitura de 4 anos e reeleição jogadas sozinhas, campanha de prefeito com debates); smoke da UI em 8 fluxos.

## Fase 6 — itens avulsos (2026-11-15)
Contornos simplificados da Malha Municipal (feitos na Fase 3, data/geo/) marcados no ROADMAP. População projetada e vagas de vereador por eleição (D-033): taxa anual entre o Censo 2022 e a estimativa do IBGE, teto do art. 29, IV com a população do ano anterior à eleição. 428 testes no Core.

## Correções eleitorais (2026-11-15)
Janela partidária só no ano da eleição em que o mandato acaba (vereador: municipal; deputados: geral). Idade mínima de 18 anos aferida no prazo do registro (15/8). Meta de assinaturas do Brasil 2026 com os votos válidos reais para a Câmara (566.265). CONTENT_GUIDE atualizado: só as fases pre_campanha e campanha; eventos de mandato ainda não existem. Adendos em D-028 a D-030; 433 testes no Core.

## Resumo da noite (modo corujão, 2026-11-15)
- **Concluído:** Fase 3 (UI da campanha, urna, apuração, save/load, mapa 2.5D, balanceamento D-026, roteiro em docs/ROTEIRO_TESTE_FASE3.md), Fase 4 (mandato de vereador, transição, filiação, fundação de partido), Fase 5 (candidatura e mandato de prefeito, debate) e 2 dos 11 itens da Fase 6 (contornos; população e vagas por eleição). Decisões D-021 a D-033.
- **Testes:** 428 no Core, 28 no importador; teste de fumaça da UI em 8 fluxos (newworld, creator, hub, picker, campaign, map, mandate, prefeitura), sem janela e com janela.
- **Pendências:** nenhuma em docs/PENDENCIAS.md (nada bloqueou). Uma exceção "Handle is not initialized", vista uma vez numa rodada com janela, não se repetiu em 5 rodadas; o timer da urna ganhou proteção contra tela já liberada.
- **Adiados de propósito (registrados nas decisões):** eventos de mandato (vereador e prefeito), eleição da Mesa, CPIs, vetos e cassação do prefeito.
- **Por onde retomar:** jogar o roteiro da Fase 3 (dá para seguir até a posse, o mandato e, na pré-campanha seguinte, disputar a prefeitura); depois, o resto da Fase 6 (estados, deputados, senador e governador, simulação regional e nacional, mídia e justiça, Espelho, evolução dos partidos, urna completa, mapa completo).

## Correções de interface (2026-11-16)
Travamento depois de evento: a janela fechava, mas o fundo escuro (que bloqueia o mouse) ficava no hub; agora sai junto. Resultado de cada ação mostra o efeito medido (reconhecimento, dinheiro, imagem, apoio, relações), nunca só "feito". Novo Mundo com ícones de sortear e travar (tooltip) e tabela que cabe na tela; aba Partido da criação em duas linhas. Testes de fumaça novos, com janela: `eventos` (as 80 opções dos 32 eventos clicadas com o mouse, conferindo que o hub volta a aceitar cliques; sem "Handle is not initialized") e `layout` (1920x1080 e 1366x768, falha se algum controle passar da janela). No modo sem janela o Godot não entrega cliques de mouse à interface, por isso esses dois rodam com janela.

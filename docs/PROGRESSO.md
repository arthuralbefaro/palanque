# Progresso

Resumo de cada etapa concluída (CLAUDE.md, seção Autonomia).

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

## Resumo da noite (modo corujão, 2026-11-15)
- **Concluído:** Fase 3 (UI da campanha, urna, apuração, save/load, mapa 2.5D, balanceamento D-026, roteiro em docs/ROTEIRO_TESTE_FASE3.md), Fase 4 (mandato de vereador, transição, filiação, fundação de partido), Fase 5 (candidatura e mandato de prefeito, debate) e 2 dos 11 itens da Fase 6 (contornos; população e vagas por eleição). Decisões D-021 a D-033.
- **Testes:** 428 no Core, 28 no importador; teste de fumaça da UI em 8 fluxos (newworld, creator, hub, picker, campaign, map, mandate, prefeitura), sem janela e com janela.
- **Pendências:** nenhuma em docs/PENDENCIAS.md (nada bloqueou). Uma exceção "Handle is not initialized", vista uma vez numa rodada com janela, não se repetiu em 5 rodadas; o timer da urna ganhou proteção contra tela já liberada.
- **Adiados de propósito (registrados nas decisões):** eventos de mandato (vereador e prefeito), eleição da Mesa, CPIs, vetos e cassação do prefeito.
- **Por onde retomar:** jogar o roteiro da Fase 3 (dá para seguir até a posse, o mandato e, na pré-campanha seguinte, disputar a prefeitura); depois, o resto da Fase 6 (estados, deputados, senador e governador, simulação regional e nacional, mídia e justiça, Espelho, evolução dos partidos, urna completa, mapa completo).

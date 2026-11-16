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

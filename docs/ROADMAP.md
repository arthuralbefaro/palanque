# PLANALTO — Roadmap

> Derivado de docs/GDD.md (seções 9 e 12). Um item por vez; marque o checkbox ao concluir.

## Fase 0 — Fundação
- [x] Git + .gitignore Godot/.NET
- [x] Projeto Godot .NET configurado (1920x1080, canvas_items/expand)
- [x] Solução com Planalto (Godot), Planalto.Core, Planalto.Core.Tests
- [x] Autoloads vazios, tema base, menu principal
- [x] CLAUDE.md
- [x] Build e testes passando

## Fase 1 — Núcleo de simulação
- [x] Rng, Calendar, GameState
- [x] Character, atributos, traços, recursos
- [ ] Eleitorado, modelo de voto, pesquisas
- [x] ElectionRules (proporcional e majoritário) com testes
- [x] Carregamento e validação de JSON + DataValidator
- [ ] Planalto.DataImporter: baixa IBGE (Localidades, Censo 2022/SIDRA, coordenadas) e TSE (perfil do eleitorado), cruza pelo código IBGE e gera data/municipios/{UF}.json (27 arquivos), com brutos em cache fora do Git e créditos das fontes em assets/CREDITS.md
- [ ] Testes do importador: contagem total de municípios igual à lista oficial, nenhum código duplicado, soma de população por UF igual ao total oficial, todo município com eleitorado > 0
- [ ] Regras derivadas por município: vagas de vereador (CF art. 29, IV, de 9 a 55), 2º turno para prefeito acima de 200 mil eleitores, porte (pequeno, médio, grande, metrópole) e regras próprias do Distrito Federal *(vagas de vereador e 2º turno feitos na 1A; faltam porte e Distrito Federal)*
- [ ] Segmentos de eleitores gerados do perfil real do município (idade, renda, religião, escolaridade, urbano/rural)

## Fase 2 — Criação de personagem
- [ ] Modelo Appearance no Core + randomização com pesos + serialização
- [ ] Arte placeholder gerada por código para todas as categorias
- [ ] PortraitRenderer com shader de tingimento, camadas trás/frente, expressões e cache
- [ ] Tela de criação com 4 abas, aleatório, desfazer, presets
- [ ] Santinho, outdoor e compositor de logotipo
- [ ] Geração de NPCs usando o mesmo sistema
- [ ] Seleção de município (qualquer município do Brasil como cidade natal e palco da primeira campanha): busca com autocompletar, filtro por UF/região, mini-mapa e ficha (população, eleitorado, vagas, porte, dificuldade sugerida)

## Fase 3 — Fatia vertical: Vereador
- [ ] 1 cidade, 6 partidos, ~10 rivais
- [ ] Hub semanal, agenda de campanha, 6 ações
- [ ] Painel de pesquisas, noite da apuração
- [ ] 30 eventos
- [ ] Save/Load
- [ ] Simulação detalhada do município em foco: todos os segmentos, candidatos NPC individuais, mídia local e eventos locais
- [ ] Candidatos e políticos NPC do município gerados só quando ele entra em foco e persistidos no save
- **Marco:** campanha de vereador jogável do início ao fim e divertida

## Fase 4 — Mandato legislativo
- [ ] Votações, projetos, emendas, fidelidade, relações, capital político
- [ ] Transição para a próxima eleição

## Fase 5 — Prefeito
- [ ] Orçamento, indicadores, políticas, coalizão municipal
- [ ] Minigame de debate

## Fase 6 — Esfera estadual e federal
- [ ] 27 estados, mapa do Brasil
- [ ] Deputados, senador, governador
- [ ] Mídia, fake news, escândalos e justiça
- [ ] Envelhecimento e Espelho
- [ ] Simulação por nível de detalhe: regional (demais municípios do estado, agregados por microrregião) e nacional (outros estados, agregados por UF)
- [ ] Mapa: Brasil por UF → UF por microrregião/município (malhas simplificadas do IBGE), colorido por intenção de voto ou resultado
- [ ] Desempenho: avançar uma semana em menos de 200 ms, mesmo em eleição presidencial

## Fase 7 — Presidência
- [ ] Economia nacional, ministérios, Congresso, MPs, vetos
- [ ] Crises nacionais, impeachment
- [ ] Tela de fim de carreira com linha do tempo de retratos

## Fase 8 — Conteúdo
- [ ] 300+ eventos, 100+ projetos de lei, 60+ políticas
- [ ] Conteúdo local (eventos e personalidades) para municípios de todas as regiões
- [ ] Ampliar catálogo de aparência (meta: 30+ cabelos, 12+ barbas, 40+ roupas)
- [ ] Conquistas
- [ ] Eventos com dados do município: variáveis {municipio.nome}, {municipio.uf}, {municipio.porte} e filtros por região e porte

## Fase 9 — Polimento
- [ ] Arte final de retratos (ilustrador ou pipeline próprio)
- [ ] Animações, áudio, jingles, música
- [ ] Tutorial na primeira campanha
- [ ] Opções e acessibilidade

## Fase 10 — Balanceamento e lançamento
- [ ] Relatórios do BalanceSim e ajustes
- [ ] Playtests externos
- [ ] Export Windows, Linux, macOS
- [ ] Página na Steam e/ou itch.io

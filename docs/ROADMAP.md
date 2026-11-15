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
- [x] Gerador de partidos fictícios (nomes, siglas, números, cores) com lista de bloqueio em data/parties/blocklist.json (pendente: conferir a lista nas páginas oficiais do TSE)
- [x] WorldSetup no Core: predefinições, sorteio por campo, travas e validações da tela "Novo Mundo" (seção 16.1)
- [x] Números de candidato por cargo (seção 16.2) com testes

### 1D — Cenário "Brasil 2026"
- [ ] Cenário "Brasil 2026" (seção 16.4), em três partes: nacional (partidos, federações, Congresso, fundo eleitoral), estadual (governadores, senadores, Assembleias de 2026) e municipal (prefeitos, vices, inelegibilidade por reeleição, Câmaras, força local e alianças de 2024 para todos os municípios)

### 1E — Eleitorado e pesquisas
- [ ] Segmentos de eleitores gerados do perfil real do município (idade, renda, religião, escolaridade, urbano/rural), com categorias normalizadas pela soma delas; afinidade partidária inicial pela força local da 1D no cenário "Brasil 2026" e, nos demais cenários, por distribuição gerada a partir das famílias e dos tamanhos dos partidos
- [ ] Modelo de voto (utilidade por segmento, softmax, brancos/nulos/abstenção, comparecimento considerando voto facultativo de 16–17 e 70+)
- [ ] Pesquisas com margem de erro, institutos fictícios com viés e pesquisa interna

## Fase 2 — Criação de personagem
- [ ] Modelo Appearance no Core + randomização com pesos + serialização
- [ ] Arte placeholder gerada por código para todas as categorias
- [ ] PortraitRenderer com shader de tingimento, camadas trás/frente, expressões e cache
- [ ] Tela de criação com 4 abas, aleatório, desfazer, presets
- [ ] Santinho, outdoor e compositor de logotipo
- [ ] Geração de NPCs usando o mesmo sistema
- [ ] Seleção de município (qualquer município do Brasil como cidade natal e palco da primeira campanha): busca com autocompletar, filtro por UF/região, mini-mapa e ficha (população, eleitorado, vagas, porte, dificuldade sugerida)
- [ ] Tela "Novo Mundo" (seção 16.1): predefinições, lista de partidos editável, sorteio, travas, cenários salvos
- [ ] Aba "Partido" na criação de personagem (seção 14.2 A): filiar-se a um partido existente ou criar o seu

## Fase 3 — Fatia vertical: Vereador
- [ ] 1 cidade, 6 partidos, ~10 rivais
- [ ] Hub semanal, agenda de campanha, 6 ações
- [ ] Painel de pesquisas, noite da apuração
- [ ] 30 eventos
- [ ] Save/Load
- [ ] Simulação detalhada do município em foco: todos os segmentos, candidatos NPC individuais, mídia local e eventos locais
- [ ] Candidatos e políticos NPC do município gerados só quando ele entra em foco e persistidos no save
- [ ] Protótipo do mapa 2.5D com os municípios de um estado (validar desempenho e visual antes da Fase 6)
- [ ] Motor de eventos (seção 15.3): condições, peso, raridade, cooldown, cadeias e variáveis de texto
- [ ] tools/Planalto.EventLint (seção 15.5): referências, variáveis inexistentes, cadeias quebradas, eventos inalcançáveis, distribuição por categoria
- [ ] Urna (seção 16.3) para vereador e prefeito
- **Marco:** campanha de vereador jogável do início ao fim e divertida

## Fase 4 — Mandato legislativo
- [ ] Votações, projetos, emendas, fidelidade, relações, capital político
- [ ] Transição para a próxima eleição
- [ ] Fundação de partido durante a carreira (seção 14.2 B): fundadores, apoio de eleitores, registro no TSE, prazo

## Fase 5 — Prefeito
- [ ] Orçamento, indicadores, políticas, coalizão municipal
- [ ] Minigame de debate

## Fase 6 — Esfera estadual e federal
- [ ] 27 estados
- [ ] Deputados, senador, governador
- [ ] Mídia, fake news, escândalos e justiça
- [ ] Envelhecimento e Espelho
- [ ] Evolução dos partidos (seção 14.1): fusões, federações, cisões, mudança de nome, crescimento e declínio
- [ ] Urna com todos os cargos, na ordem real (seção 16.3)
- [ ] Simulação por nível de detalhe: regional (demais municípios do estado, agregados por região geográfica imediata) e nacional (outros estados, agregados por UF)
- [ ] Evolução da população dos municípios e regra de vagas a cada eleição municipal: mínimo entre as vagas atuais e o teto calculado com a população do ano
- [ ] Contornos simplificados a partir da Malha Municipal 2025 (mesma fonte das coordenadas)
- [ ] Mapa 2.5D (seção 13.1): extrusão, níveis de detalhe (Brasil → UF → região imediata/município), câmera inclinada, modo apuração, tooltips e alternativa 2D plana
- [ ] Desempenho: avançar uma semana em menos de 200 ms, mesmo em eleição presidencial; mapa a 60 FPS na visão de um estado

## Fase 7 — Presidência
- [ ] Economia nacional, ministérios, Congresso, MPs, vetos
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

## Fase 10 — Balanceamento e lançamento
- [ ] Relatórios do BalanceSim e ajustes
- [ ] Playtests externos
- [ ] Export Windows, Linux, macOS
- [ ] Página na Steam e/ou itch.io

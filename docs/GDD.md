# PLANALTO — Simulador de Carreira Política no Brasil
> Game Design Document + Plano de Desenvolvimento
> Engine: Godot 4.7.2 .NET (C#) · Plataforma: PC (Windows, Linux, macOS)
> Versão 2.0 — novembro de 2026

---

## 1. VISÃO GERAL

**Conceito:** O jogador cria seu personagem, começa como cidadão comum numa cidade brasileira, se filia a um partido e disputa a primeira eleição para vereador. A cada ciclo eleitoral decide se busca reeleição, se sobe de cargo ou se troca de partido, até chegar à Presidência da República e tentar sobreviver a ela.

**Referências:** Democracy 4 (simulação de políticas), Suzerain (narrativa), Reigns (decisões rápidas), Football Manager (gestão semanal), The Sims / Crusader Kings 3 (criação e envelhecimento de personagem).

**Pilares de design:**
1. **Autêntico ao Brasil:** regras eleitorais reais (voto proporcional, quociente eleitoral, segundo turno, horário eleitoral gratuito, fundo eleitoral, Ficha Limpa, federações, janela partidária).
2. **Toda escolha cobra um preço:** alianças dão votos e criam dívidas; dinheiro fácil gera risco de escândalo; coerência ideológica fideliza a base e limita o crescimento.
3. **Ficção neutra:** partidos, políticos, empresas, institutos e mídia fictícios. Ideologia modelada em eixos simétricos, sem lado "certo". Estados e cidades reais podem ser usados.
4. **Seu político, sua história:** personagem totalmente customizável, que envelhece, muda de visual e carrega a trajetória na aparência e na biografia.
5. **Cada carreira é única:** eventos procedurais, eleitorado simulado, rivais persistentes com carreira própria.

**Público:** jogadores de estratégia/gestão e interessados em política, 16+.
**Duração de uma carreira completa:** 6 a 12 horas.

---

## 2. ENGINE E STACK

| Item | Escolha |
|---|---|
| Engine | Godot 4.7.2 – .NET |
| Linguagem | C# (.NET 8+), nullable habilitado, TreatWarningsAsErrors |
| Núcleo de simulação | Biblioteca .NET pura `Planalto.Core`, sem referência ao Godot |
| Testes do núcleo | xUnit em `Planalto.Core.Tests`, via `dotnet test` |
| Testes de integração/UI | GdUnit4Net (opcional, a partir da Fase 3) |
| Conteúdo | JSON em `data/`, desserializado com System.Text.Json e validado na carga |
| Renderer | Forward+ (2D) |
| Resolução base | 1920x1080, stretch `canvas_items`, aspect `expand` |
| Plataformas | Windows, Linux, macOS (sem versão Web) |
| Versionamento | Git, um commit por funcionalidade |

**Por que essa arquitetura:** toda a simulação (eleitorado, apuração, economia, IA, eventos) roda em .NET puro, testável em segundos sem abrir a engine. O Godot cuida apenas de apresentação: UI, retratos, animação e áudio.

---

## 3. CRIAÇÃO DE PERSONAGEM

A criação acontece em 4 abas com pré-visualização ao vivo do retrato e do "santinho" à direita. Botões globais: **Aleatório**, **Aleatório só nesta aba**, **Desfazer**, **Salvar preset**, **Carregar preset**.

### 3.1 Aba "Aparência"

Retrato em busto (cabeça e ombros), estilo vetorial ilustrado, composto por camadas.

| Categoria | Opções |
|---|---|
| Corpo | Tipo de corpo (4), largura de ombros, altura relativa (afeta só enquadramento) |
| Tom de pele | Paleta contínua com 24 tons + seletor fino; subtom (quente, neutro, frio) |
| Rosto | Formato (oval, redondo, quadrado, longo, coração, triangular), maxilar, queixo, maçãs do rosto |
| Olhos | Formato (8), tamanho, inclinação, cor (paleta + seletor) |
| Sobrancelhas | Estilo (10), espessura, cor |
| Nariz | Formato (10), largura |
| Boca | Formato (8), espessura dos lábios |
| Orelhas | Formato (4) |
| Cabelo | 30+ estilos (curto, raspado, black power, tranças, dreads, cacheado, liso, coque, careca, calvície parcial etc.), cor (natural + tingido), grisalho (0–100%) |
| Pelos faciais | Barba (12 estilos), bigode (8), cavanhaque, nenhum; cor e grisalho independentes |
| Marcas | Sardas, pintas, cicatriz, rugas (intensidade), olheiras, covinhas |
| Maquiagem | Batom, sombra, delineado (opcional, cor e intensidade) |
| Acessórios | Óculos (10 armações + escuros), brincos, piercing, colar, broche/bottom do partido |

### 3.2 Aba "Vestuário"

O personagem tem um **guarda-roupa por contexto**, usado automaticamente conforme a situação (ou trocado manualmente):

| Contexto | Quando aparece | Exemplos de peças |
|---|---|---|
| Institucional | Plenário, gabinete, posse | Terno, tailleur, camisa social, gravata, vestido formal |
| Campanha de rua | Comícios, corpo a corpo | Camiseta com número de urna, colete, boné |
| Debate/TV | Debates, entrevistas | Formal sem gravata, blazer |
| Casual | Eventos pessoais | Camisa polo, camiseta, jaqueta |
| Regional | Eventos em certas regiões | Chapéu de vaqueiro, camisa de time fictício, chapéu de palha |

Cada peça tem cor primária e secundária; a cor do partido pode ser aplicada com um clique.

**Regra de design:** roupas e aparência **não alteram atributos nem votos**. A única exceção são eventos narrativos opcionais (ex.: "a imprensa comentou seu terno de grife na visita à comunidade") em que o efeito vem do contexto, nunca do corpo do personagem.

### 3.3 Aba "Identidade"

| Campo | Detalhe |
|---|---|
| Nome civil | Livre |
| Nome de urna | Livre (ex.: "Zé da Saúde", "Dra. Ana") |
| Gênero e pronomes | Masculino, feminino, não binário; pronomes usados em todos os textos |
| Data de nascimento | Define idade inicial (25 a 45 anos) |
| Estado e cidade natal | Lista de cidades jogáveis; dá bônus de reconhecimento local |
| Origem social | Periferia, classe média, família rica, zona rural, família de políticos (cada uma muda recursos iniciais e eventos) |
| Profissão anterior | Médico(a), advogado(a), professor(a), pastor(a)/líder religioso(a), sindicalista, empresário(a), policial, jornalista, influenciador(a), atleta, agricultor(a), servidor(a) público(a) — cada uma com bônus de atributo, contatos e eventos exclusivos |
| Religião | Opcional; libera/afeta eventos |
| Estado civil e família | Solteiro(a), casado(a), filhos (0–4); cônjuge e filhos têm retratos gerados e podem participar da história |
| Traços de personalidade | Escolher 2 de 3 entre ~20 (ver 3.5) |
| Biografia | Gerada automaticamente a partir das escolhas, editável |

### 3.4 Aba "Campanha" (identidade visual)

| Elemento | Opções |
|---|---|
| Número de urna | Definido pelo partido (prefixo) + escolha dos dígitos finais |
| Slogan | Livre ou sugestões geradas |
| Logotipo | Ícone (estrela, mão, casa, árvore, coração, engrenagem, livro etc.), fonte (6), cores |
| Jingle | Escolha de estilo musical (forró, sertanejo, pagode, funk, marchinha, rock) — muda a faixa tocada em comícios e no horário eleitoral |
| Foto oficial | Pose (3), expressão, fundo (bandeira do estado, gabinete, cidade, cor sólida) |

A pré-visualização mostra o **santinho** (frente e verso) e um **outdoor**. Esses materiais aparecem depois no jogo (em telas de campanha, na apuração e na mídia).

### 3.5 Traços de personalidade (exemplos)

| Traço | Efeito |
|---|---|
| Bom de papo | +Oratória, risco de "falar demais" em entrevistas |
| Workaholic | +Energia semanal, eventos de desgaste familiar |
| Esquentado | Debates com ataques mais fortes, risco de vídeo viral negativo |
| Técnico | +Gestão, −Carisma em comícios |
| Populista | +Carisma com renda baixa, desconfiança do mercado |
| Articulador nato | +Articulação, eventos de bastidores |
| Idealista | +Integridade, dificuldade em fechar alianças pragmáticas |
| Pragmático | Alianças mais baratas, penalidade de "incoerência" menor |
| Midiático | +Ganho nas redes, maior exposição a polêmicas |
| Discreto | Escândalos demoram mais a aparecer, menos reconhecimento |
| Religioso | Afinidade com segmentos religiosos, eventos exclusivos |
| Esportista | +Saúde (envelhece melhor), eventos esportivos |

Traços podem ser **ganhos ou perdidos** ao longo da carreira por escolhas repetidas (ex.: ceder a pressões várias vezes adiciona "Maleável").

### 3.6 Expressões
Todo retrato (jogador e NPCs) suporta expressões: neutra, sorriso, sorriso largo, preocupado, bravo, triste, surpreso, cansado. As expressões mudam sobrancelhas, olhos e boca e são usadas automaticamente em eventos, debates, apuração e manchetes.

### 3.7 Envelhecimento e mudança de visual
- O personagem envelhece com o calendário: rugas, grisalho, entradas e olheiras evoluem com a idade e com o **estresse** acumulado (mandatos executivos e crises envelhecem mais rápido).
- A qualquer momento fora de campanha, o jogador pode visitar o **"Espelho"** para mudar cabelo, barba, maquiagem, óculos e roupas. Mudanças de rosto/corpo não são permitidas depois da criação, exceto por eventos (ex.: cirurgia plástica, com reação da mídia).
- Mudanças grandes de visual podem gerar uma manchete leve (sem efeito em votos, só reconhecimento).
- A tela final de carreira mostra uma **linha do tempo de retratos** (posse em cada cargo).

### 3.8 NPCs
O mesmo sistema gera todos os NPCs (rivais, aliados, família, jornalistas, caciques) por sorteio com pesos realistas por região do Brasil. Rivais persistentes envelhecem junto com o jogador.

---

## 4. PROGRESSÃO DE CARREIRA

### 4.1 Calendário
- 1 turno = 1 semana.
- Eleições municipais e gerais alternam a cada 2 anos.
- 1º turno no primeiro domingo de outubro; 2º turno no último domingo de outubro (CF, arts. 29, II, e 77).
- Campanha oficial a partir de 16 de agosto (Lei 9.504/1997, art. 36), cerca de 7 semanas antes do 1º turno.
- Filiação partidária exigida 6 meses antes da eleição.
- Desincompatibilização: ocupantes de cargos executivos precisam renunciar 6 meses antes para disputar outro cargo.
- Janela partidária: período em que parlamentares trocam de partido sem perder o mandato.

### 4.2 Escada de cargos

| Nível | Cargo | Esfera | Sistema | Mandato |
|---|---|---|---|---|
| 1 | Vereador | Municipal | Proporcional | 4 anos |
| 2 | Prefeito | Municipal | Majoritário (2º turno se > 200 mil eleitores) | 4 anos, 1 reeleição |
| 2 | Deputado Estadual | Estadual | Proporcional | 4 anos |
| 3 | Deputado Federal | Federal | Proporcional | 4 anos |
| 4 | Senador | Federal | Majoritário simples | 8 anos |
| 4 | Governador | Estadual | Majoritário, 2 turnos | 4 anos, 1 reeleição |
| 5 | Presidente | Federal | Majoritário, 2 turnos | 4 anos, 1 reeleição |

Rotas livres: o jogador pode pular etapas, mas o risco cresce sem base, dinheiro e reconhecimento. Cargos por indicação (secretário municipal/estadual, ministro) dão visibilidade e contatos.

### 4.3 Fim de jogo
- **Vitória principal:** concluir um mandato presidencial com aprovação acima de 40%.
- **Conquistas:** reeleição presidencial, invicto nas urnas, carreira limpa, recorde de velocidade, "voltou por cima" (presidente após uma derrota grave) etc.
- **Derrotas:** inelegibilidade, prisão, cassação, impeachment, aposentadoria após derrotas seguidas, idade/saúde.

---

## 5. SISTEMAS PRINCIPAIS

### 5.1 Atributos e recursos
**Atributos (0–100):** Carisma, Oratória, Articulação, Gestão, Integridade (parcialmente oculto).
**Recursos:** Dinheiro de campanha, Capital político, Reconhecimento (por região), Energia (pontos de ação semanais), Saúde, Estresse.

### 5.2 Ideologia
Dois eixos de -100 a +100: **Econômico** (estatista ↔ liberal) e **Costumes** (progressista ↔ conservador). Posições em temas (-2 a +2): segurança, saúde, educação, meio ambiente, agronegócio, impostos, programas sociais, armas, privatizações, transparência. Mudanças de posição geram penalidade de incoerência proporcional à frequência e à exposição.

### 5.3 Eleitorado simulado
Segmentos por: renda, faixa etária, religião, zona (urbana/rural), região. Cada segmento tem peso, ideologia média, temas prioritários, engajamento e afinidade partidária.

```
utilidade(candidato) =
    a * proximidade_ideologica
  + b * afinidade_temas
  + c * reconhecimento
  + d * imagem (carisma, escândalos, mídia)
  + e * forca_partido_no_segmento
  + f * apoios (líderes, igrejas, sindicatos)
  + ruido
```
Intenção de voto = softmax das utilidades + brancos/nulos/abstenção (dependem de engajamento e rejeição).

**Pesquisas:** institutos fictícios com margem de erro, amostragem simulada e vieses. Pesquisa interna contratável (mais cara, mais precisa).

### 5.4 Regras de apuração
- **Proporcional:** quociente eleitoral, quociente partidário, sobras por maiores médias, votação mínima individual de 10% do quociente.
- **Majoritário:** maioria absoluta de válidos ou 2º turno.
- **Senado:** maioria simples, alternância de 1 e 2 vagas.
Toda regra com testes unitários usando casos calculados à mão.

### 5.5 Partidos
10 a 14 partidos fictícios cobrindo o espectro, com ideologia, tamanho, fundo eleitoral, caciques (NPCs), disciplina interna, reputação, cor e logotipo. Mecânicas: filiação, convenção, janela partidária, federações, cargos na direção, apoio em eleições majoritárias.

### 5.6 Campanha
Agenda semanal com Energia e Dinheiro:

| Ação | Efeito principal |
|---|---|
| Comício | Grande ganho local; custo alto; toca o jingle escolhido |
| Corpo a corpo | Alcance pequeno, forte afinidade em renda baixa |
| Visita a igreja/sindicato/associação | Apoio de segmento |
| Redes sociais | Alcance jovem; risco de viral negativo |
| Entrevista (rádio/TV/podcast) | Teste de Oratória |
| Debate | Minigame (5.7) |
| Horário eleitoral | Tempo por bancada; tom (propositivo, ataque, emocional) |
| Arrecadação | Dinheiro; doações grandes criam dívidas |
| Apoio de líder local | Custo em capital político |
| Contratar equipe | Marqueteiro, coordenador, tesoureiro, advogado eleitoral, social media |

Prestação de contas: estouro de limite ou caixa dois aumentam risco de cassação.

### 5.7 Debates (minigame de cartas)
Temas sorteados; cartas **Proposta, Dado, Ataque, Defesa, Emoção, Evasiva**, cada uma testando um atributo contra os segmentos que assistem. Rivais com IA por personalidade. Retratos com expressões reagem em tempo real. Resultado em pesquisa pós-debate e cortes virais.

### 5.8 Mandato Legislativo
Pauta semanal de votações, proposição de projetos (comissões e plenário), emendas parlamentares, cargos na casa (liderança, comissões, presidência), CPIs, fidelidade partidária.

### 5.9 Mandato Executivo
Orçamento por área, indicadores com atraso (emprego, inflação, segurança, saúde, educação, infraestrutura, dívida), cartas de política, coalizão no Legislativo, secretarias/ministérios como moeda, MPs e vetos (federal), aprovação ponderada, crises (enchente, epidemia, greve, crise externa, escândalo de aliado), risco de impeachment.

### 5.10 Mídia
Veículos fictícios com linha editorial, manchetes com o retrato do jogador (expressão conforme a notícia), ciclo de notícias, fake news (desmentir, processar, ignorar).

### 5.11 Escândalos e justiça
Atalhos antiéticos criam "esqueletos no armário" com chance semanal de vir à tona (maior com inimigos, cargo alto ou investigação). Escala: matéria → investigação → denúncia → julgamento → condenação. Defesas: advogados, foro privilegiado, aliados. Escolhas íntegras também têm custo, sem moralismo.

### 5.12 NPCs e relações
Políticos procedurais com carreira própria, relação com o jogador (-100 a 100) e memória de favores e traições. Mentor inicial. Família com eventos (apoio, cobranças, escândalos, filhos que entram na política).

### 5.13 Eventos narrativos
JSON com condições, texto com variáveis (`{cidade}`, `{rival.nome}`, `{jogador.pronome}`), escolhas e efeitos. Meta: 300+ eventos no lançamento.

---

## 6. LOOP DE JOGO

### 6.1 Semana
1. Manchetes da semana
2. Eventos (0 a 3 decisões)
3. Agenda (alocar Energia)
4. Painéis (pesquisas, finanças, relações, indicadores)
5. Avançar semana → simulação → resultados

### 6.2 Ciclo de 2 anos
Mandato/preparação → pré-campanha (filiação, convenção, alianças, Espelho/visual) → campanha (de 16 de agosto ao 1º turno, no primeiro domingo de outubro) → apuração (2º turno, se houver, no último domingo de outubro) → posse (retrato oficial salvo na linha do tempo) ou derrota com opções.

### 6.3 Ritmo
Botão "avançar até o próximo evento"; foco mensal em mandato, semanal em campanha.

---

## 7. ARQUITETURA TÉCNICA

### 7.1 Princípios
1. Simulação em `Planalto.Core` (.NET puro). A UI só lê estado e envia comandos.
2. Data-driven: cargos, partidos, cidades, eventos, ações, políticas e **peças de aparência** em JSON.
3. Determinismo: RNG único com semente.
4. Tudo testável sem a engine.
5. Classes Godot que herdam de Node são `partial`.

### 7.2 Estrutura de pastas
```
planalto/
├── CLAUDE.md
├── project.godot
├── Planalto.sln
├── Planalto.csproj                  # projeto Godot (UI, autoloads, render)
├── docs/
│   ├── GDD.md
│   ├── ROADMAP.md
│   ├── CONTENT_GUIDE.md             # como escrever eventos
│   └── ART_GUIDE.md                 # especificação das camadas de retrato
├── core/
│   ├── Planalto.Core/
│   │   ├── State/                   # GameState, Calendar, Rng
│   │   ├── Characters/              # Character, Attributes, Traits, Appearance, AppearanceAging, Family
│   │   ├── Electorate/              # Segment, Electorate, VoteModel, Polling
│   │   ├── Elections/               # ElectionRules, ElectionSim, Results
│   │   ├── Campaign/
│   │   ├── Legislature/
│   │   ├── Executive/               # Budget, Indicators, Policies, Economy
│   │   ├── Media/
│   │   ├── Scandals/
│   │   ├── Npcs/                    # NpcGenerator, NpcAi, Relations
│   │   ├── Events/                  # EventEngine, ConditionParser, EffectApplier
│   │   ├── Data/                    # carregamento e validação de JSON
│   │   └── Save/                    # serialização com versionamento
│   └── Planalto.Core.Tests/
├── tools/
│   ├── Planalto.BalanceSim/         # console: simula N carreiras
│   ├── Planalto.DataImporter/       # console: baixa IBGE/TSE e gera data/municipios/ (brutos em cache/, fora do Git)
│   └── Planalto.DataValidator/      # console: valida todos os JSON
├── src/
│   ├── Autoload/                    # Game.cs, EventBus.cs, SaveManager.cs, AudioManager.cs, Settings.cs
│   └── UI/
│       ├── Screens/                 # MainMenu, CharacterCreator, Mirror, Hub, Campaign, Debate, ElectionNight, Legislature, Executive, CareerEnd
│       ├── Panels/                  # Polls, Finances, Relations, Map, News
│       ├── Widgets/                 # Cards, Charts, Bars, Tooltips
│       ├── Portrait/                # PortraitView.cs, PortraitRenderer.cs, PortraitCache.cs
│       ├── CampaignMaterials/       # Santinho, Outdoor, LogoComposer
│       └── Theme/
├── data/
│   ├── appearance/                  # parts.json (catálogo de peças por camada), palettes.json, presets/
│   ├── offices.json  parties.json  states.json  segments.json
│   ├── municipios/                  # {UF}.json, 27 arquivos gerados pelo DataImporter (ver 12.3)
│   ├── events/  bills/  policies.json  actions.json
│   ├── traits.json  professions.json  media_outlets.json
│   └── names/
└── assets/
    ├── portrait/                    # camadas organizadas por categoria (ver 7.4)
    ├── fonts/  icons/  maps/  sfx/  music/  jingles/
    └── CREDITS.md
```

### 7.3 Fluxo de dados
```
UI → Game.Execute(comando)
   → Core aplica e atualiza GameState
   → EventBus emite sinal (StateChanged, WeekAdvanced, EventTriggered)
   → UI redesenha
```

### 7.4 Sistema de retratos
**Modelo (Core):** `Appearance` é um record serializável com IDs de peça por camada, cores (hex) e parâmetros numéricos (grisalho, rugas, intensidade de maquiagem). `AppearanceAging` calcula a evolução por idade e estresse. Nada de Godot no Core.

**Renderização (Godot):** `PortraitRenderer` empilha camadas na ordem:
```
fundo → corpo/roupa (trás) → pescoço → cabeça (formato + pele) → orelhas
→ marcas (sardas, pintas, rugas) → olhos → sobrancelhas → nariz → boca
→ maquiagem → pelos faciais → cabelo (frente) → óculos → acessórios
→ roupa (frente: gola, gravata) → broche do partido
```
- Camadas em PNG 1024x1024 em tons de cinza; a cor é aplicada por **shader de tingimento** (multiplicação + máscara de sombra/luz), assim uma peça serve para qualquer cor.
- Cabelo tem camada "trás" e "frente" para ficar atrás das orelhas/ombros corretamente.
- Expressões trocam apenas olhos, sobrancelhas e boca (variações por peça).
- `PortraitCache` gera texturas em `SubViewport` e guarda em cache (tamanhos 512, 256, 96) para listas, manchetes e mapas sem custo.
- Retrato oficial a cada posse é salvo como imagem no save.

**Arte de placeholder:** na Fase 2 o Claude Code gera camadas vetoriais simples (SVG → PNG) por código para todas as categorias, para o sistema funcionar antes da arte final. A arte final segue `docs/ART_GUIDE.md` (dimensões, pontos de ancoragem, nomenclatura `categoria_id_variante.png`) e pode ser feita por um ilustrador.

**Catálogo (`data/appearance/parts.json`):**
```json
{
  "id": "hair_cacheado_medio",
  "category": "hair",
  "layers": { "back": "hair/cacheado_medio_back.png", "front": "hair/cacheado_medio_front.png" },
  "tintable": true,
  "tags": ["cacheado", "medio"],
  "random_weight": { "default": 1.0 },
  "balding_compatible": false
}
```

### 7.5 Formato de evento (exemplo)
```json
{
  "id": "campanha_doacao_empreiteira",
  "category": "escandalo_potencial",
  "phase": ["campanha"],
  "conditions": { "office_level_min": 2, "money_lt": 50000, "chance": 0.15 },
  "portrait": { "npc": "empresa.dono", "expression": "sorriso" },
  "title": "Uma oferta generosa",
  "text": "O dono da Construtora {empresa.nome} oferece R$ 300 mil 'por fora' para sua campanha em {cidade}.",
  "choices": [
    { "label": "Aceitar",
      "effects": { "money": 300000, "add_skeleton": { "type": "caixa_dois", "severity": 3 }, "relation": { "empresa": 30 } } },
    { "label": "Pedir que doe oficialmente",
      "effects": { "money": 20000, "relation": { "empresa": -10 } } },
    { "label": "Recusar e denunciar à imprensa",
      "effects": { "integrity": 5, "recognition": 3, "relation": { "empresa": -60 }, "add_enemy": "empresa" } }
  ]
}
```

### 7.6 Save/Load
GameState inteiro em JSON com versão de formato e migrações; 3 slots + autosave semanal + autosave antes de eleição; modo Ironman opcional; presets de personagem salvos separadamente em `user://presets/`.

### 7.7 Balanceamento
`Planalto.BalanceSim` simula 1.000+ carreiras com estratégias variadas e gera relatório (taxa de vitória por cargo, tempo até presidente, causas de derrota, distribuição de votos).

---

## 8. ARTE, UI E ÁUDIO

### 8.1 Direção de arte
2D vetorial ilustrado, inspirado em material de campanha brasileiro e infográficos de jornal. Interface sóbria (azul-escuro, branco), acentos verde e amarelo neutros. Fontes OFL: sans para UI (Inter/Montserrat) e condensada para manchetes (Oswald/Bebas Neue). Retratos com diversidade real de tons de pele, cabelos e traços do Brasil.

### 8.2 Telas
1. Menu principal
2. **Criação de personagem** (4 abas + pré-visualização de retrato, santinho e outdoor)
3. **Espelho** (mudança de visual durante a carreira)
4. Hub semanal
5. Pesquisas · Finanças · Relações · Mapa eleitoral
6. Debate
7. Noite da apuração
8. Plenário
9. Gabinete executivo
10. Fim de carreira (linha do tempo com retratos de cada posse, estatísticas, conquistas)

### 8.3 Acabamento
Transições com Tween, números que contam, barras animadas, manchetes com efeito de digitação, retratos que reagem com expressões, apuração com tensão (parciais por região, viradas), tooltips explicando a origem de cada número, sons em todo clique.

### 8.4 Áudio
Trilhas (menu, mandato, campanha, apuração, vitória/derrota), 6 jingles por estilo musical, SFX de UI, carimbo, multidão, plantão de notícias. Fontes livres (Kenney CC0, Freesound com licença verificada) ou composição própria; créditos em `assets/CREDITS.md`.

### 8.5 Opções e acessibilidade
Escala de UI, modo daltônico (partidos com ícone além da cor), velocidade de texto, volumes separados, tela cheia/janela, localização preparada (PT-BR primeiro, `Tr()` + CSV).

---

## 9. ROADMAP

### Fase 0 — Fundação
- [ ] Git + .gitignore Godot/.NET
- [ ] Projeto Godot .NET configurado (1920x1080, canvas_items/expand)
- [ ] Solução com Planalto (Godot), Planalto.Core, Planalto.Core.Tests
- [ ] Autoloads vazios, tema base, menu principal
- [ ] CLAUDE.md
- [ ] Build e testes passando

### Fase 1 — Núcleo de simulação
- [ ] Rng, Calendar, GameState
- [ ] Character, atributos, traços, recursos
- [ ] Eleitorado, modelo de voto, pesquisas
- [ ] ElectionRules (proporcional e majoritário) com testes
- [ ] Carregamento e validação de JSON + DataValidator

### Fase 2 — Criação de personagem
- [ ] Modelo Appearance no Core + randomização com pesos + serialização
- [ ] Arte placeholder gerada por código para todas as categorias
- [ ] PortraitRenderer com shader de tingimento, camadas trás/frente, expressões e cache
- [ ] Tela de criação com 4 abas, aleatório, desfazer, presets
- [ ] Santinho, outdoor e compositor de logotipo
- [ ] Geração de NPCs usando o mesmo sistema

### Fase 3 — Fatia vertical: Vereador
- [ ] 1 cidade, 6 partidos, ~10 rivais
- [ ] Hub semanal, agenda de campanha, 6 ações
- [ ] Painel de pesquisas, noite da apuração
- [ ] 30 eventos
- [ ] Save/Load
- **Marco:** campanha de vereador jogável do início ao fim e divertida

### Fase 4 — Mandato legislativo
- [ ] Votações, projetos, emendas, fidelidade, relações, capital político
- [ ] Transição para a próxima eleição

### Fase 5 — Prefeito
- [ ] Orçamento, indicadores, políticas, coalizão municipal
- [ ] Minigame de debate

### Fase 6 — Esfera estadual e federal
- [ ] 27 estados, mapa do Brasil
- [ ] Deputados, senador, governador
- [ ] Mídia, fake news, escândalos e justiça
- [ ] Envelhecimento e Espelho

### Fase 7 — Presidência
- [ ] Economia nacional, ministérios, Congresso, MPs, vetos
- [ ] Crises nacionais, impeachment
- [ ] Tela de fim de carreira com linha do tempo de retratos

### Fase 8 — Conteúdo
- [ ] 300+ eventos, 100+ projetos de lei, 60+ políticas
- [ ] Conteúdo local (eventos e personalidades) para municípios de todas as regiões
- [ ] Ampliar catálogo de aparência (meta: 30+ cabelos, 12+ barbas, 40+ roupas)
- [ ] Conquistas

### Fase 9 — Polimento
- [ ] Arte final de retratos (ilustrador ou pipeline próprio)
- [ ] Animações, áudio, jingles, música
- [ ] Tutorial na primeira campanha
- [ ] Opções e acessibilidade

### Fase 10 — Balanceamento e lançamento
- [ ] Relatórios do BalanceSim e ajustes
- [ ] Playtests externos
- [ ] Export Windows, Linux, macOS
- [ ] Página na Steam e/ou itch.io

---

## 10. CLAUDE CODE

### 10.1 CLAUDE.md
```
# Planalto — instruções para o Claude Code

## Contexto
Simulador de carreira política brasileira em Godot 4.7.2 .NET (C#). Leia docs/GDD.md e docs/ROADMAP.md antes de qualquer tarefa.

## Regras de código
- C# com nullable habilitado e TreatWarningsAsErrors.
- Lógica de jogo SOMENTE em core/Planalto.Core, sem referência a Godot.
- UI em src/UI apenas lê o estado e chama Game.Execute(...).
- Toda aleatoriedade passa pela classe Rng do Core (com semente).
- Conteúdo em data/*.json; nunca hardcode textos, peças de aparência ou números de balanceamento.
- Classes que herdam de Node são partial.
- Textos ao jogador em PT-BR via Tr(), respeitando os pronomes do personagem.

## Verificação obrigatória
- dotnet build Planalto.sln
- dotnet test core/Planalto.Core.Tests
- dotnet run --project tools/Planalto.DataValidator
- godot --headless --build-solutions --quit
Nenhuma tarefa está concluída com build quebrado, testes falhando ou dados inválidos.

## Fluxo
- Um item do ROADMAP por vez; marque o checkbox ao concluir.
- Teste junto com toda regra de negócio nova.
- Commit por funcionalidade.

## Neutralidade e representação
- Partidos, políticos, empresas, institutos e mídia fictícios. Nunca nomes reais.
- Nenhuma ideologia favorecida; toda posição tem custos e benefícios.
- Aparência do personagem nunca altera votos ou atributos.
```

### 10.2 Prompts por fase
**Fase 0:**
> Leia docs/GDD.md. Crie docs/ROADMAP.md a partir da seção 9 e execute a Fase 0. Crie o CLAUDE.md da seção 10.1. Verifique com todos os comandos da seção "Verificação obrigatória". Me mostre o plano antes de executar.

**Fase 1:**
> Execute a Fase 1 seguindo as seções 5.3 e 5.4. Comece pelo ElectionRules com testes cobrindo quociente eleitoral, quociente partidário, sobras por maiores médias, cláusula de 10% e 2º turno, com exemplos calculados nos testes.

**Fase 2:**
> Execute a Fase 2 seguindo a seção 3 e a 7.4. Primeiro o modelo Appearance no Core com testes de randomização e serialização. Depois gere a arte placeholder por código, o PortraitRenderer com shader de tingimento e a tela de criação. Me mostre o plano antes.

**Conteúdo:**
> Escreva 30 eventos de campanha de vereador no formato da seção 7.5, seguindo docs/CONTENT_GUIDE.md. Distribua entre pessoal, oportunidade, crise e escândalo potencial. Rode o DataValidator ao final.

### 10.3 Boas práticas
- Uma fase por sessão do Claude Code; sessão nova a cada fase.
- Modo de planejamento antes de fases grandes.
- Depois de cada fase, jogue 20–30 minutos e mande a lista de problemas como próxima tarefa.
- Ajuste de números via relatórios do BalanceSim.
- Arte final dos retratos é o maior investimento visual do jogo: considere um ilustrador seguindo o ART_GUIDE.md.

---

## 11. RISCOS E CUIDADOS

| Risco | Mitigação |
|---|---|
| Escopo grande demais | Fatia vertical de vereador antes de expandir |
| Simulação opaca | Tooltips e relatório pós-eleição por segmento |
| Viés político percebido | Ficção total, eixos simétricos, revisão de eventos por pessoas de visões diferentes |
| Estereótipos na aparência | Aparência sem efeito mecânico; randomização com diversidade realista; nenhum evento baseado em traço físico |
| Retratos com aparência amadora | Shader de tingimento + camadas bem ancoradas; arte final por ilustrador |
| Nomes e marcas reais | Tudo fictício, inclusive urna estilizada e institutos |
| Repetitividade | Eventos procedurais, rivais persistentes, rotas variadas, conquistas |
| Licenças de assets | Só CC0/OFL/CC BY, com `assets/CREDITS.md` |
| Claude Code sem visão do editor | Cenas em texto/código, testes headless, validação visual por você a cada fase |

## 12. MUNICÍPIOS (TODOS OS DO BRASIL)

### 12.1 Escopo
- Todos os municípios brasileiros (cerca de 5.570, contagem exata conforme a lista oficial mais recente do IBGE) mais o Distrito Federal, que tem regras próprias (sem prefeito nem vereador; Câmara Legislativa e governador).
- Qualquer município pode ser escolhido como cidade natal e como palco da primeira campanha.
- Municípios reais, autoridades fictícias.

### 12.2 Fontes de dados (oficiais e públicas)
| Dado | Fonte |
|---|---|
| Lista, código IBGE, UF, região, micro/mesorregião | IBGE — API de Localidades |
| População, urbana/rural, faixa etária, renda, religião | IBGE — Censo 2022 (SIDRA) e estimativas mais recentes |
| Eleitorado por município (total, faixa etária, gênero, escolaridade) | TSE — Portal de Dados Abertos (perfil do eleitorado) |
| Coordenadas | IBGE |
| Contornos (mapa) | IBGE — malhas territoriais, simplificadas |
Créditos das fontes em assets/CREDITS.md.

### 12.3 Pipeline de dados
- `tools/Planalto.DataImporter` (console .NET) baixa, cruza pelo código IBGE e gera `data/municipios/{UF}.json` (27 arquivos).
- Brutos em cache local (não versionados); saída versionada.
- Testes: contagem total de municípios, nenhum código duplicado, soma de população por UF igual ao total oficial, todo município com eleitorado > 0.
- Campos por município:
```json
{
  "codigo_ibge": 3550308,
  "nome": "São Paulo",
  "uf": "SP",
  "regiao": "SE",
  "microrregiao": "São Paulo",
  "lat": -23.55, "lon": -46.63,
  "populacao": 11451999,
  "eleitorado": 9300000,
  "urbana_pct": 99.1,
  "perfil": { "idade": {...}, "renda": {...}, "religiao": {...}, "escolaridade": {...} },
  "vagas_vereador": 55,
  "segundo_turno_prefeito": true,
  "porte": "metropole"
}
```
(valores ilustrativos; os reais vêm do importador)

### 12.4 Regras derivadas automaticamente
- **Vagas de vereador** pelas faixas populacionais da Constituição (art. 29, IV), de 9 a 55.
- **2º turno para prefeito** só onde o eleitorado passa de 200 mil.
- **Porte** (define o tipo de campanha):
| Porte | Critério aproximado | Características |
|---|---|---|
| Pequeno | até 20 mil hab. | Corpo a corpo decisivo, todo mundo se conhece, poucas candidaturas, pouco dinheiro, sem TV local |
| Médio | 20 mil a 100 mil | Rádio local forte, lideranças comunitárias, alianças familiares |
| Grande | 100 mil a 500 mil | TV regional, pesquisas frequentes, redes sociais relevantes |
| Metrópole | acima de 500 mil | Campanha cara, mídia intensa, eleitorado fragmentado, debates na TV |
- **Segmentos de eleitores** gerados do perfil real (idade, renda, religião, escolaridade, urbano/rural).
- **Dificuldade sugerida** mostrada na criação de personagem (cidade pequena = começo mais fácil e teto de visibilidade menor; metrópole = o contrário).

### 12.5 Simulação por nível de detalhe
| Nível | Onde se aplica | O que é simulado |
|---|---|---|
| Detalhado | Município onde o jogador está fazendo campanha ou governando | Todos os segmentos, candidatos NPC individuais, mídia local, eventos locais |
| Regional | Demais municípios do estado em eleições estaduais/federais | Agregado por microrregião, com força do partido e reconhecimento do jogador |
| Nacional | Outros estados em eleição presidencial | Agregado por UF |
- Candidatos e políticos NPC de um município só são gerados quando o município entra em foco, e depois persistem no save.
- Meta de desempenho: avançar uma semana em menos de 200 ms mesmo em eleição presidencial.

### 12.6 Interface
- Seleção de município: busca com autocompletar por nome, filtro por UF/região, mini-mapa e ficha (população, eleitorado, vagas, porte, dificuldade).
- Mapa: Brasil por UF → UF por microrregião/município (contornos simplificados), colorido por intenção de voto ou resultado.
- Eventos podem usar dados do município: `{municipio.nome}`, `{municipio.uf}`, `{municipio.porte}`, filtros por região e porte.
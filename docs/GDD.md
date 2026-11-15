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
Partidos fictícios cobrindo o espectro, na quantidade da predefinição escolhida na tela "Novo Mundo" (seção 16.1), com ideologia, tamanho, fundo eleitoral, caciques (NPCs), disciplina interna, reputação, cor e logotipo. Mecânicas: filiação, convenção, janela partidária, federações, cargos na direção, apoio em eleições majoritárias.

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
JSON com condições, texto com variáveis (`{cidade}`, `{rival.nome}`, `{jogador.pronome}`), escolhas e efeitos. Meta: 600+ eventos no lançamento (ver seção 15).

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
│   ├── Planalto.DataImporter.Tests/ # testes do importador (precisa do cache preenchido)
│   ├── Planalto.DataValidator/      # console: valida todos os JSON
│   └── Planalto.EventLint/          # console: verifica eventos (referências, variáveis, cadeias, alcance)
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
│   ├── offices.json  states.json  segments.json
│   ├── parties/                     # families.json (famílias ideológicas), blocklist.json, scenarios/ (ver 14 e 16)
│   ├── municipios/                  # {UF}.json, 27 arquivos gerados pelo DataImporter (ver 12.3)
│   ├── geo/                         # contornos simplificados da Malha Municipal para o mapa (ver 13.1)
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

**Arte de placeholder:** na Fase 2, camadas vetoriais simples (SVG → PNG) são geradas por código para todas as categorias, para o sistema funcionar antes da arte final. A arte final segue `docs/ART_GUIDE.md` (dimensões, pontos de ancoragem, nomenclatura `categoria_id_variante.png`) e pode ser feita por um ilustrador.

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

O planejamento de fases vive em `docs/ROADMAP.md`, que é a fonte única: fases, itens, ordem e estado de cada um ficam só lá. As demais seções deste documento descrevem o que construir; o ROADMAP diz quando.

---

## 10. FLUXO DE DESENVOLVIMENTO

### 10.1 Regras do projeto
As regras de código, dados, verificação, autoria e neutralidade ficam no arquivo de regras na raiz do repositório e valem para todo o trabalho: lógica de jogo só no Core, sem referência ao Godot; UI só lê o estado e envia comandos; aleatoriedade pelo Rng com semente; conteúdo e números de balanceamento em data/; e verificação obrigatória (build da solução, testes do Core, DataValidator e build headless do Godot) antes de dar qualquer tarefa por concluída.

### 10.2 Roteiro por fase
- **Fase 0:** criar docs/ROADMAP.md e executar a fundação; verificar com todos os comandos da verificação obrigatória; plano apresentado antes da execução.
- **Fase 1:** seguir as seções 5.3, 5.4 e 12; começar pelo ElectionRules com testes cobrindo quociente eleitoral, quociente partidário, sobras por maiores médias, cláusula de 10% e 2º turno, com exemplos calculados à mão nos testes.
- **Fase 2:** seguir as seções 3 e 7.4; primeiro o modelo Appearance no Core com testes de randomização e serialização; depois a arte placeholder gerada por código, o PortraitRenderer com shader de tingimento e a tela de criação; plano apresentado antes.
- **Conteúdo:** escrever eventos em lotes (ex.: 30 eventos de campanha de vereador) no formato das seções 7.5 e 15.5, seguindo docs/CONTENT_GUIDE.md e distribuídos pelas categorias da 15.2; rodar o DataValidator e o EventLint ao final.

### 10.3 Boas práticas
- Uma fase por sessão de trabalho; sessão nova a cada fase.
- Planejamento revisado antes de fases grandes.
- Depois de cada fase, jogar 20–30 minutos e transformar a lista de problemas na próxima tarefa.
- Ajuste de números via relatórios do BalanceSim.
- Arte final dos retratos é o maior investimento visual do jogo: considerar um ilustrador seguindo o ART_GUIDE.md.

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
| Desenvolvimento sem visão do editor | Cenas em texto/código, testes headless, validação visual pelo autor a cada fase |

## 12. MUNICÍPIOS (TODOS OS DO BRASIL)

### 12.1 Escopo
- Todos os municípios brasileiros (cerca de 5.570, contagem exata conforme a lista oficial mais recente do IBGE) mais o Distrito Federal, que tem regras próprias (sem prefeito nem vereador; Câmara Legislativa e governador).
- Qualquer município pode ser escolhido como cidade natal e como palco da primeira campanha.
- Municípios reais, autoridades fictícias.

### 12.2 Fontes de dados (oficiais e públicas)
| Dado | Fonte |
|---|---|
| Lista, código IBGE, UF, região, microrregião, região geográfica imediata | IBGE — API de Localidades |
| População | IBGE — Estimativas da População (SIDRA 6579), ano mais recente |
| Urbana/rural, faixa etária, renda, religião, escolaridade | IBGE — Censo 2022 (SIDRA 10211, 9514, 10296, 10198, 10061) |
| Eleitorado por município (total, faixa etária, gênero, escolaridade) | TSE — Portal de Dados Abertos (perfil do eleitorado atual) |
| Correspondência de códigos TSE ↔ IBGE | TSE — Códigos oficiais de UF e municípios |
| Vagas de vereador | TSE — Consulta de vagas da última eleição municipal |
| Coordenadas e área | IBGE — Malha Municipal 2025 (shapefile): centroide do polígono, ou ponto interior quando o centroide cai fora; área do campo AREA_KM2 |
| Contornos (mapa) | IBGE — malhas territoriais, simplificadas |
Créditos das fontes em assets/CREDITS.md.

### 12.3 Pipeline de dados
- `tools/Planalto.DataImporter` (console .NET) baixa, cruza pelo código IBGE de 7 dígitos e gera `data/municipios/{UF}.json` (27 arquivos) e `data/municipios/_fontes.json` (fonte, ano e data de download de cada campo, totais oficiais e lacunas).
- Brutos em cache local (`tools/Planalto.DataImporter/cache/`, não versionado); saída versionada.
- Testes: contagem total de municípios igual à lista do IBGE, nenhum código duplicado, soma de população por UF igual ao total oficial, todo município com eleições municipais com eleitorado > 0, soma de vagas de vereador igual ao total do TSE.
- Campos por município:
```json
{
  "codigo_ibge": 3550308,
  "nome": "São Paulo",
  "uf": "SP",
  "regiao": "SE",
  "microrregiao": { "id": 35061, "nome": "São Paulo" },
  "regiao_imediata": { "id": 350001, "nome": "São Paulo" },
  "lat": -23.65, "lon": -46.65, "coordenada_metodo": "centroide", "area_km2": 1521.2,
  "populacao": 11900000,
  "eleitorado": 9300000,
  "urbana_pct": 99.1,
  "perfil_fonte": "municipio",
  "perfil": { "populacao_censo": 11451999, "situacao": {...}, "idade": {...}, "renda": {...}, "religiao": {...}, "escolaridade": {...} },
  "eleicoes_municipais": true,
  "vagas_vereador": 55,
  "vagas_vereador_max": 55,
  "vagas_fonte": "tse_2024",
  "segundo_turno_prefeito": true,
  "porte": "metropole"
}
```
(valores ilustrativos; os reais vêm do importador)
- `microrregiao` pode ser `null`: a divisão em microrregiões está congelada desde 2017 e não existe para municípios criados depois. A divisão usada pela simulação é a **região geográfica imediata**, que cobre todos.
- O perfil guarda as contagens inteiras do Censo 2022, como publicadas; percentuais são calculados no Core, sempre normalizando cada bloco pela soma das próprias categorias, nunca pelo total publicado. Renda, religião e escolaridade vêm da amostra do Censo: o IBGE arredonda cada categoria separadamente, então a soma delas pode diferir do total em algumas unidades. Faixas etárias: 0–15, 16–17, 18–24, 25–34, 35–44, 45–59, 60–69 e 70+ (16–17 e 70+ são as faixas de voto facultativo).
- Município sem dado do Censo (criado depois de 2022) usa o perfil da UF, com `perfil_fonte: "uf"`, e a lacuna fica registrada em `_fontes.json`.
- Distrito Federal e Fernando de Noronha (distrito estadual de PE) têm `eleicoes_municipais: false`: não elegem prefeito nem vereadores.

### 12.4 Regras derivadas automaticamente
- **Vagas de vereador**: o número real de cada Câmara, fixado pela lei orgânica, vem da consulta de vagas do TSE da última eleição municipal (`vagas_fonte: "tse_2024"`). O teto da Constituição (art. 29, IV, de 9 a 55, pela população estimada) fica em `vagas_vereador_max` e é só um limite; município sem registro no TSE usa o teto (`vagas_fonte: "teto_constitucional"`).
  - Alguns municípios têm hoje vagas reais acima do teto calculado com a população atual (perderam população depois de fixar o número); as vagas reais são mantidas e a lista fica em `_fontes.json` (`excecoes_vagas`).
  - Regra no jogo, quando houver evolução da população: a cada eleição municipal, vagas = mínimo entre as vagas atuais e o teto calculado com a população daquele ano.
- **2º turno para prefeito** só onde o eleitorado passa de 200 mil.
- **Porte** (define o tipo de campanha), calculado sobre a população estimada, com os limites em `data/portes.json`:
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
| Regional | Demais municípios do estado em eleições estaduais/federais | Agregado por região geográfica imediata (IBGE), com força do partido e reconhecimento do jogador |
| Nacional | Outros estados em eleição presidencial | Agregado por UF |
- Candidatos e políticos NPC de um município só são gerados quando o município entra em foco, e depois persistem no save.
- Meta de desempenho: avançar uma semana em menos de 200 ms mesmo em eleição presidencial.

### 12.6 Interface
- Seleção de município: busca com autocompletar por nome, filtro por UF/região, mini-mapa e ficha (população, eleitorado, vagas, porte, dificuldade).
- Mapa: Brasil por UF → UF por região imediata/município (contornos simplificados), colorido por intenção de voto ou resultado.
- Eventos podem usar dados do município: `{municipio.nome}`, `{municipio.uf}`, `{municipio.porte}`, filtros por região e porte.

## 13. ELEMENTOS 2.5D E 3D

O jogo continua 2D. O 3D aparece em dois pontos: o mapa eleitoral e as cerimônias de posse. Ambos rodam em SubViewport 3D embutido na interface 2D.

### 13.1 Mapa 2.5D
- **Construção:** polígonos da Malha Municipal 2025 do IBGE, simplificados no DataImporter, extrudados em malhas 3D (uma por município, agrupadas por UF). Malhas geradas em tempo de execução a partir de data/geo/ ou pré-geradas como recursos, o que for mais rápido de carregar.
- **Níveis de detalhe:**
  - Brasil: 27 estados extrudados.
  - Estado: regiões imediatas ou municípios.
  - Município em foco: destaque com contorno brilhante.
- **Câmera:** perspectiva levemente inclinada (30–40°), zoom e arraste com o mouse, transição animada entre níveis.
- **Visualizações:**
  - Altura do relevo = força do jogador (ou do candidato escolhido) na região.
  - Cor = candidato ou partido líder, com intensidade pela margem.
  - Modo apuração: municípios "sobem" à medida que as urnas são apuradas, em ordem por região e porte, com partículas no momento da virada.
- **Interação:** passar o mouse mostra tooltip com resultado e dados do município; clicar abre a ficha.
- **Meta de desempenho:** 60 FPS na visão de um estado com todos os municípios; visão nacional com estados apenas.
- **Acessibilidade:** alternância para mapa 2D plano nas Opções.
- **Protótipo:** na Fase 3, um protótipo com os municípios de um estado valida desempenho e visual antes do mapa completo da Fase 6.

### 13.2 Cerimônias de posse em 3D
- **Quando:** a cada posse de cargo eletivo. Duração de 15 a 40 segundos, sempre pulável com qualquer tecla.
- **Escala crescente de grandeza:**

| Cargo | Cenário | Momentos |
|---|---|---|
| Vereador | Câmara municipal pequena | Juramento em grupo, assinatura do termo |
| Prefeito | Prefeitura, praça com público | Discurso na sacada, entrega da chave da cidade |
| Deputado estadual/federal | Plenário em semicírculo | Diplomação, juramento |
| Senador | Plenário do Senado (estilizado) | Juramento, cumprimentos |
| Governador | Palácio estadual (genérico) | Transmissão de cargo, discurso |
| Presidente | Rampa e palácio estilizados, carro aberto, multidão | Desfile, subida da rampa, faixa presidencial, discurso |

- **Arquitetura:** estilizada e inspirada na arquitetura brasileira, sem réplicas de edifícios reais, coerente com o universo fictício do jogo.
- **Estilo visual:** low-poly com cores chapadas e iluminação suave (estilo "diorama"), para combinar com a arte vetorial 2D e ser viável de produzir.
- **Personagem 3D:** boneco estilizado parametrizado a partir de Appearance:
  - Tom de pele, cor de cabelo/barba, grisalho, cores da roupa e cor do partido aplicados por material.
  - 8–12 formatos de cabelo e 4–6 de barba em 3D, mapeados a partir dos estilos 2D (cada estilo 2D aponta para o formato 3D mais próximo em data/appearance/parts.json).
  - Corpo pelo tipo escolhido na criação; roupa institucional do guarda-roupa.
  - Planos sempre abertos ou médios; no clímax, corte para o **retrato oficial 2D** em moldura, que é salvo na linha do tempo da carreira.
- **NPCs:** família, aliados e multidão com o mesmo sistema de bonecos, em variações aleatórias.
- **Elementos dinâmicos:** tamanho da multidão proporcional à votação; bandeiras e cores do partido; vice e família presentes quando existirem; clima e hora do dia.
- **Técnica:** cenas em .tscn com AnimationPlayer para caminhos de câmera e personagens; modelos em glTF; tudo carregado sob demanda e descarregado ao fim.
- **Ordem de produção:** primeiro a posse presidencial e a de vereador (os dois extremos da escala); depois os demais cargos.

### 13.3 Assets 3D
- Base: pacotes CC0 low-poly (Kenney, Quaternius) para pessoas, carros, árvores e mobiliário, adaptados.
- Cenários de posse e bonecos parametrizáveis: modelagem própria ou por um modelador 3D seguindo docs/ART_GUIDE.md (seção 3D: escala, número de polígonos, nomes de materiais tingíveis).
- A montagem das cenas, materiais, animações de câmera e a lógica de parametrização é feita no código do projeto; a modelagem de qualidade fica com assets ou com um artista.

## 14. PARTIDOS: ESPECTRO COMPLETO E CRIAÇÃO PELO JOGADOR

Todos os partidos são fictícios. O jogo cobre todo o espectro político democrático, sem favorecer nenhum lado.

### 14.1 Famílias ideológicas
Cada partido pertence a uma família, que define a faixa nos dois eixos (Econômico e Costumes, -100 a +100), os temas prioritários e o comportamento da IA.

| Família | Econômico | Costumes | Temas típicos |
|---|---|---|---|
| Esquerda socialista | -100 a -70 | -100 a -50 | Estatização, reforma agrária, direitos trabalhistas |
| Esquerda | -70 a -40 | -70 a -30 | Programas sociais, serviços públicos, direitos de minorias |
| Trabalhista | -60 a -30 | -30 a +10 | Emprego, salário mínimo, sindicatos, previdência |
| Centro-esquerda / social-democracia | -40 a -10 | -50 a -10 | Estado de bem-estar, educação, saúde pública |
| Ambientalista (verde) | -40 a 0 | -60 a -20 | Meio ambiente, clima, transição energética |
| Centro | -15 a +15 | -15 a +15 | Estabilidade, gestão, consenso |
| Centro pragmático ("fisiológico") | -10 a +20 | -10 a +30 | Cargos, emendas, apoio a qualquer governo |
| Democracia cristã | -10 a +30 | +30 a +70 | Família, assistência social, valores religiosos |
| Centro-direita | +10 a +40 | 0 a +40 | Responsabilidade fiscal, reformas, segurança |
| Direita liberal | +40 a +80 | -30 a +20 | Privatizações, menos impostos, livre mercado |
| Libertário | +70 a +100 | -60 a 0 | Estado mínimo, liberdades individuais |
| Direita conservadora | +30 a +70 | +50 a +90 | Segurança, família, valores tradicionais |
| Liberal-conservador | +50 a +90 | +40 a +80 | Mercado e costumes tradicionais |
| Nacional-populista de direita | -10 a +50 | +60 a +100 | Nacionalismo, ordem, antissistema |
| Agrário / ruralista | +20 a +60 | +20 a +60 | Agronegócio, propriedade rural, infraestrutura |
| Regionalista | variável | variável | Interesses de um estado ou região |

- O mundo do jogo começa com os partidos fictícios da predefinição escolhida (seção 16.1), sorteados para cobrir todas as famílias, com tamanhos diferentes (de nanicos a grandes), gerados em data/parties/ ou por geração procedural a cada nova carreira (opção no início).
- Nomes gerados por combinação de termos típicos ("Movimento", "Partido", "Aliança", "Frente", "União" + "Democrático", "Trabalhista", "Liberal", "Popular", "Nacional", "Renovador", "Social" etc.), com siglas derivadas e verificação para não coincidir com siglas ou nomes de partidos reais (lista de bloqueio em data/parties/blocklist.json).
- Partidos evoluem: fusões, federações, cisões, mudança de nome após escândalos, crescimento e declínio, novos partidos surgindo ao longo das décadas.

### 14.2 Criação de partido pelo jogador
Duas formas, à escolha do jogador:

**A) No início (modo livre):** na criação de personagem, aba "Partido", o jogador escolhe entre filiar-se a um partido existente ou criar o seu já registrado.

**B) Durante a carreira (modo realista):** fundar um partido como objetivo de carreira, seguindo o processo da Lei dos Partidos Políticos de forma simplificada:
1. Reunir fundadores (custo de capital político e relações).
2. Coletar apoio de eleitores não filiados distribuídos por pelo menos um terço dos estados (minigame de campanha de assinaturas ao longo de meses).
3. Registro no TSE (evento com chance de impugnação por irregularidades).
4. Prazo: só disputa eleição se registrado 6 meses antes.

**Personalização do partido:**
| Campo | Opções |
|---|---|
| Nome | Livre, ou gerador com sugestões |
| Sigla | Livre (2 a 8 letras), validada contra a lista de bloqueio |
| Número | Dois dígitos livres entre os não usados |
| Família ideológica | Qualquer uma da tabela 14.1, ou "personalizada" |
| Posição exata | Ajuste fino nos dois eixos dentro (ou fora, se personalizada) da faixa da família |
| Bandeiras | Escolher 3 a 5 temas prioritários com posição de -2 a +2 |
| Identidade visual | Cores primária e secundária, logotipo (ícone + fonte), slogan |
| Estatuto | Disciplina interna (rígida, moderada, frouxa), democracia interna (convenções abertas ou cúpula decide), postura (oposição permanente, independente, adesista) |

**Consequências:**
- Partido novo começa pequeno: sem fundo partidário relevante, pouco tempo de TV, sem bancada.
- O jogador é o "cacique": controla legendas, alianças e expulsões, mas carrega o desgaste de tudo que o partido faz.
- Outros políticos NPC podem migrar para o partido do jogador (na janela partidária), trazendo votos e problemas.
- Partido pode crescer até virar o maior do país, ou ser incorporado/extinto.

### 14.3 Liberdade de jogo
O Planalto é um simulador livre: o jogador pode seguir qualquer ideologia, mudar de lado, ser íntegro ou corrupto, fiel ou traidor, moderado ou radical. O jogo nunca julga moralmente; ele apenas simula consequências plausíveis (eleitorais, judiciais, midiáticas, pessoais). Toda família ideológica tem caminhos viáveis até a Presidência, com dificuldades diferentes conforme o eleitorado.

---

## 15. EVENTOS ALEATÓRIOS

### 15.1 Objetivo
Cada carreira deve parecer diferente. Meta de conteúdo: **600+ eventos** no lançamento, com variação de texto, encadeamentos e consequências de longo prazo.

### 15.2 Categorias e metas
| Categoria | Meta | Exemplos |
|---|---|---|
| Campanha | 80 | Comício esvaziado, apoio inesperado de líder local, vídeo viralizado, debate cancelado, adversário ataca sua família, pesquisa vazada |
| Mandato legislativo | 60 | Votação polêmica, pedido de emenda, troca de favores, obstrução, CPI, pressão de bancada |
| Mandato executivo | 70 | Greve de servidores, obra atrasada, licitação suspeita, hospital lotado, escola interditada, pedido de reajuste |
| Crises e desastres | 50 | Enchente, seca, deslizamento, incêndio florestal, apagão, epidemia, desabamento, onda de violência |
| Economia | 40 | Fábrica fecha, investimento chega, alta de preços, crise externa, boom de commodity, falência de empresa local |
| Escândalos | 60 | Gravação vazada, assessor investigado, denúncia anônima, delação, uso indevido de verba, nepotismo |
| Mídia e redes | 50 | Entrevista com pegadinha, fake news, meme, influenciador apoia ou ataca, documentário, cancelamento |
| Vida pessoal e família | 60 | Casamento, divórcio, filho em polêmica, doença, luto, problema de saúde, conquista pessoal |
| Bastidores e relações | 50 | Proposta de aliança, traição de aliado, mentor pede favor, rival oferece trégua, ameaça de rompimento |
| Sociedade e cultura | 40 | Carnaval, festa junina, Copa do Mundo (fictícia), protesto estudantil, marcha religiosa, festival local |
| Regionais | 40 | Específicos por região e porte: garimpo, conflito agrário, turismo, seca no sertão, porto, fronteira |
| Raros e inusitados | 20 | Acontecimentos absurdos ou cômicos com baixa probabilidade (ex.: animal invade o plenário, discurso confundido com meme mundial) |

### 15.3 Estrutura do motor de eventos
- **Condições:** cargo, fase (pré-campanha, campanha, mandato), região, porte do município, família ideológica, traços, atributos, recursos, relações, ano, eventos anteriores, flags.
- **Peso e raridade:** comum, incomum, raro, lendário; peso ajustado por condições (ex.: enchentes mais prováveis no período de chuvas da região).
- **Cooldown:** um evento (ou categoria) não se repete por um tempo mínimo; eventos únicos acontecem uma vez por carreira.
- **Encadeamento:** escolhas abrem eventos de continuação semanas ou anos depois (ex.: aceitar doação → investigação 2 anos depois → delação de terceiros).
- **Memória dos NPCs:** eventos alteram relações e ficam registrados; NPCs lembram favores e traições.
- **Variáveis de texto:** {municipio.nome}, {rival.nome}, {partido.sigla}, {jogador.pronome}, {aliado.cargo}, {ano}, valores em reais calculados.
- **Variações de texto:** cada evento pode ter 2 a 4 versões de abertura para reduzir repetição.
- **Escolhas:** 2 a 4 opções, algumas ocultas ou bloqueadas por requisito (ex.: "Usar seu contato na polícia" só com a relação adequada). Mostrar o requisito quando bloqueada.
- **Efeitos:** atributos, recursos, intenção de voto por segmento, relações, flags, esqueletos no armário, eventos futuros agendados, mudança de traços.
- **Resultados com risco:** algumas escolhas têm resultado sorteado com probabilidade influenciada por atributos (ex.: improvisar uma resposta testa Oratória).
- **Frequência:** 0 a 3 eventos por semana, mais intensos em campanha e crises; botão para ver o histórico de eventos.

### 15.4 Eventos dinâmicos (sem texto fixo)
Além dos eventos escritos, o motor gera ocorrências a partir da simulação:
- Rival despenca nas pesquisas → evento "oportunidade de atrair apoiadores dele".
- Indicador de segurança em queda há meses → evento "onda de assaltos vira manchete".
- Relação com aliado abaixo de um limite → evento "aliado ameaça romper".
- Estresse alto por muito tempo → evento de saúde.

### 15.5 Formato e produção
- Eventos em data/events/{categoria}/*.json, no formato da seção 7.5, acrescidos de "rarity", "cooldown_weeks", "unique", "chain" e "variants".
- docs/CONTENT_GUIDE.md com regras de escrita: tom, tamanho máximo, variáveis, neutralidade e checklist.
- Ferramenta tools/Planalto.EventLint: verifica referências, variáveis inexistentes, cadeias quebradas, eventos inalcançáveis e distribuição por categoria.
- BalanceSim reporta frequência real de cada evento em 1.000 carreiras, para encontrar eventos que nunca aparecem ou aparecem demais.
- Regra de conteúdo: nenhum evento cita pessoas, partidos ou empresas reais; nenhum evento baseado em estereótipo de região, religião, raça ou gênero; tragédias tratadas com seriedade.

## 16. CONFIGURAÇÃO DO MUNDO E URNA

### 16.1 Tela "Novo Mundo" (antes da criação de personagem)
O jogador configura o cenário político da carreira. Cada campo pode ser editado à mão, sorteado individualmente ou travado antes de um sorteio geral.

**Predefinições:**
| Predefinição | Partidos | Descrição |
|---|---|---|
| Brasil realista | 28 a 32 | Muitos partidos, centro pragmático forte, federações |
| Multipartidário moderado | 12 a 16 | Padrão do jogo |
| Poucos partidos | 4 a 6 | Disputas mais polarizadas |
| Bipartidário | 2 | Experimental: dois grandes blocos |
| Personalizado | 2 a 35 | Tudo livre |

**Lista de partidos:** cada linha mostra logotipo, sigla, nome, número, família ideológica, tamanho e cores. Para cada partido:
| Campo | Editável | Aleatório |
|---|---|---|
| Nome | Livre | Gerador (seção 14.1) |
| Sigla | Livre, validada contra a lista de bloqueio | Derivada do nome |
| Número | 10 a 99, sem repetição | Sorteado entre os livres |
| Família ideológica | Qualquer uma da 14.1 | Sorteada respeitando a cobertura do espectro |
| Posição nos eixos | Ajuste fino | Sorteada dentro da faixa da família |
| Tamanho inicial | Nanico, pequeno, médio, grande, dominante | Distribuição realista |
| Cores e logotipo | Livres | Gerados, evitando cores repetidas |
| Personalidade da cúpula | Ideológica, pragmática, fisiológica, personalista | Sorteada |

**Ações:** adicionar partido, remover partido, duplicar, sortear linha, sortear tudo (respeitando os travados), restaurar predefinição, salvar e carregar cenários (user://scenarios/).

**Validações:** pelo menos um partido de cada lado do eixo econômico quando houver 4 ou mais partidos (aviso, não bloqueio); números e siglas únicos; soma de tamanhos coerente.

**Opções gerais do mundo:** ano inicial (2028 por padrão), dificuldade, frequência de eventos (baixa, normal, alta, caótica), modo Ironman, nível de realismo jurídico (relaxado ou rigoroso).

### 16.2 Números de candidato
Seguem a regra real:
| Cargo | Dígitos | Formato |
|---|---|---|
| Prefeito, governador, presidente | 2 | Número do partido |
| Senador | 3 | Número do partido + 1 dígito |
| Deputado federal | 4 | Número do partido + 2 dígitos |
| Vereador, deputado estadual/distrital | 5 | Número do partido + 3 dígitos |
- O jogador escolhe os dígitos finais na aba "Campanha" da criação de personagem (seção 3.4), entre os livres no partido.
- Número próprio pode ser mantido entre eleições (bônus pequeno de reconhecimento por "número fácil de lembrar" se o jogador repetir os dígitos ao subir de cargo).
- Ao trocar de partido, o número muda: perda pequena de reconhecimento.

### 16.3 A urna (minigame do dia da eleição)
No domingo de eleição, antes da apuração, o jogador vota como qualquer eleitor.

**Cena:** fila na seção eleitoral de uma escola do município (ilustração 2D com parallax), mesário confere o documento, cabine.

**Urna:** estilizada e inspirada na urna eletrônica brasileira, sem reproduzir brasões ou marcas oficiais:
- Teclado numérico 0–9 e as teclas BRANCO, CORRIGE, CONFIRMA, clicáveis e pelo teclado do computador (Enter = confirma, Backspace = corrige).
- Tela mostra o cargo, os campos de dígitos e, ao completar o número, o retrato, o nome de urna e a sigla do partido do candidato.
- Número inexistente: aviso de voto nulo, como na urna real. Só o número do partido em eleição proporcional: voto de legenda.
- Ordem dos cargos igual à real: municipal (vereador, prefeito); geral (deputado federal, deputado estadual/distrital, senador(es), governador, presidente).
- Som de tecla e som de encerramento originais, inspirados nos da urna, e tela final "FIM".

**Regras no jogo:**
- O voto do jogador conta como um voto real na simulação daquela eleição.
- O jogador pode votar em si mesmo, em aliados, em rivais, em branco ou nulo; o voto é secreto e não tem efeito mecânico além do próprio voto, exceto se o jogador escolher "votar diante das câmeras", que gera manchete.
- Disponível também quando o jogador não é candidato (eleições em que ele apenas vota).
- Pode ser pulado; nesse caso o jogador escolhe "votar em mim mesmo" ou "não votar" (com eventual manchete de abstenção).
- Conquista: "Eleito por um voto" quando o voto do jogador decide a última vaga ou a eleição.

**Acessibilidade:** modo com áudio de confirmação dos números, como o recurso de acessibilidade da urna real, e opção de fonte grande.

### 16.4 Cenário "Brasil 2026" (espelho do cenário atual)
Predefinição adicional na tela "Novo Mundo": o mundo começa com a estrutura política real do Brasil após as eleições de 2026, com **nomes fictícios**.

**O que é espelhado do real:**
- Número de partidos registrados e federações existentes (quais partidos estão federados entre si).
- Bancadas de cada partido na Câmara dos Deputados, no Senado, nas Assembleias Legislativas, e o número de governadores e prefeitos por partido.
- Distribuição do fundo eleitoral e do tempo de TV proporcional às bancadas.
- Família ideológica de cada partido, com base em classificação acadêmica publicada (pesquisa com especialistas), registrada como fonte.
- Partido do presidente e composição da base governista e da oposição no início do jogo.

**O que é sempre fictício:**
- Nomes, siglas, números, cores e logotipos dos partidos (gerados com a lista de bloqueio).
- Todos os políticos, inclusive presidente, governadores, prefeitos e lideranças.
- Eventos, escândalos e histórias.

**Dados:** o DataImporter (ou uma ferramenta irmã) gera data/parties/scenarios/brasil_2026/ (um arquivo nacional e 27 arquivos estaduais) a partir de dados oficiais (TSE: partidos registrados, federações, resultados de 2026; Câmara e Senado: bancadas). A correspondência entre partido real e partido fictício existe só no pipeline de dados e nunca vai para o jogo; os arquivos finais contêm apenas os partidos fictícios com seus números.

**Escolha do jogador:** na tela "Novo Mundo", o jogador escolhe "Brasil 2026" ou qualquer outra predefinição/aleatório; o cenário espelhado também pode ser editado depois (renomear, mudar cores, ajustar tamanhos).

**Cenário municipal espelhado (todos os municípios):**
Base: resultados oficiais das eleições municipais de 2024 (e eleições suplementares posteriores, quando houver), do TSE.

Para cada município com eleição municipal:
- **Prefeitura:** partido do prefeito e do vice em exercício; se o prefeito foi reeleito em 2024 (segundo mandato consecutivo), ele fica inelegível para prefeito em 2028, como na regra real.
- **Câmara Municipal:** composição por partido (cadeiras de cada um), base do prefeito e oposição.
- **Força local dos partidos:** percentual de votos de cada partido em 2024 para vereador (legenda + nominais) e para prefeito, usado como afinidade partidária inicial dos segmentos de eleitores daquele município.
- **Federações e coligações de 2024:** quais partidos se aliaram na disputa de prefeito, usadas como tendência inicial de alianças locais.

Nível estadual (eleições de 2026): partido do governador e do vice, composição de cada Assembleia Legislativa, senadores por estado.

**Mesmas regras do cenário nacional:** os partidos são os fictícios do cenário "Brasil 2026" (a mesma correspondência real→fictício para todos os níveis); prefeitos, vereadores, governadores e deputados são sempre fictícios, gerados só quando o município ou estado entra em foco (seção 12.5) e respeitando os fatos estruturais acima (partido, se é reeleito, quantas cadeiras cada partido tem).

**Dados:** data/parties/scenarios/brasil_2026/ com um arquivo nacional e 27 arquivos estaduais (municípios agrupados por UF), contendo apenas IDs de partidos fictícios e números. Meta de tamanho: até 5 MB no total.

**Testes:** soma das cadeiras de cada Câmara igual a vagas_vereador do município; todo município com eleição tem prefeito com partido; percentuais de força local somam 100% por município; nenhum nome ou sigla real no arquivo final.

**Limitação conhecida:** trocas de partido de prefeitos e vereadores depois de 2024 não são capturadas (não há base oficial consolidada); o cenário reflete a situação eleita, ajustada por eleições suplementares.
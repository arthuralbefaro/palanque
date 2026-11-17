# PLANALTO — Simulador de Carreira Política no Brasil
> Game Design Document + Plano de Desenvolvimento
> Engine: Godot 4.7.2 .NET (C#) · Plataforma: PC (Windows, Linux, macOS)
> Versão 2.1 — novembro de 2026 (seção 17: reformulação)

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

Retrato em busto (cabeça e ombros). Até a troca na parte RV, em estilo vetorial ilustrado, composto por camadas.

**Na parte RV (DECISOES D-043):** o retrato passa a ser renderizado de um **boneco 3D estilizado, de formas suaves e proporções de desenho animado** (referências: Two Point Hospital, Overcooked, Planet Coaster; não é low-poly facetado). É o mesmo boneco que aparece no menu como cena, nas cenas da RV-B e nas posses (13.2). Pela proposta da RV (`docs/referencias/rv_cenas.html`), a criação acontece num estúdio de foto, com o santinho impresso ao lado. As categorias da tabela continuam; muda como cada uma é feita:
- **Forma** (corpo, formato do rosto, nariz, orelhas): variações dos corpos e das cabeças base.
- **Cor** (pele, olhos, cabelo e pelos, maquiagem, roupa, cor do partido): material tingível.
- **Peça** (cabelo, barba, óculos, brincos, broche): modelo trocável, preso aos pontos de troca da cabeça (`docs/ART_GUIDE.md`, contrato da cabeça substituível). Cada estilo do catálogo aponta para a peça 3D mais próxima.
- **Marcas** (rugas, olheiras, sardas, pintas): textura de detalhe da pele, com intensidade.
A aparência continua sem efeito em votos e atributos, com a diversidade do Censo (D-016).

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
| Data de nascimento | Define idade inicial (17 a 45 anos). Para disputar vereador é preciso ter 18 anos completos no prazo do registro de candidatura, 15 de agosto (Lei 9.504/1997, art. 11, § 2º); quem não tiver começa direto na pré-campanha da primeira eleição que pode disputar, com a idade correspondente, e a criação avisa antes de confirmar (decisão do autor, 16/11). NPCs são gerados entre 25 e 45 anos |
| Estado e cidade natal | Lista de cidades jogáveis; dá bônus de reconhecimento local |
| Origem social | Periferia, classe média, família rica, zona rural, família de políticos (cada uma muda recursos iniciais e eventos) |
| Profissão anterior | Médico(a), advogado(a), professor(a), pastor(a)/líder religioso(a), sindicalista, empresário(a), policial, jornalista, influenciador(a), atleta, agricultor(a), servidor(a) público(a) e estudante — cada uma com bônus de atributo, contatos e eventos exclusivos e com idade mínima (formação e registro profissional; ex.: médico(a) a partir de 25, estudante a partir de 17). Profissão indisponível na idade aparece desativada, com a idade mínima |
| Religião | Opcional; libera/afeta eventos |
| Estado civil e família | Solteiro(a), casado(a), união estável, divorciado(a), viúvo(a); filhos (0–4). Coerentes com a idade (`data/family.json`): casamento e união estável a partir dos 18, divórcio e viuvez a partir dos 20, teto de filhos que cresce com a idade, filhos nascidos com o personagem a partir dos 16 e cônjuge de 18 anos ou mais. Cônjuge e filhos têm nome e idade e podem participar da história |
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

A pré-visualização mostra o **santinho** (frente e verso), um **adesivo**, uma **bandeira** e uma **peça para redes sociais**. Outdoor é proibido na propaganda eleitoral (Lei 9.504/1997, art. 39, § 8º) e por isso não existe no jogo (decisão do autor, 17.20, item 9; a troca na tela de criação é item da R1). Esses materiais aparecem depois no jogo (em telas de campanha, na apuração e na mídia).

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

No boneco 3D (D-043), cada expressão tem um nome fixo (`expr_sorriso`, `expr_bravo` etc., mais `piscar`, que se soma às outras), e a cabeça declara como a realiza: por animação do rosto (shape keys) ou por troca de peças do rosto (olhos, sobrancelhas, boca). O jogo pede a expressão pelo nome e não sabe qual dos dois modos a cabeça usa. As mesmas expressões valem nos retratos e nas cenas. Critério: as expressões se distinguem nos retratos de 96 px e de 512 px.

### 3.7 Envelhecimento e mudança de visual
- O personagem envelhece com o calendário: rugas, grisalho, entradas e olheiras evoluem com a idade e com o **estresse** acumulado (mandatos executivos e crises envelhecem mais rápido).
- A qualquer momento fora de campanha, o jogador pode visitar o **"Espelho"** para mudar cabelo, barba, maquiagem, óculos e roupas. Mudanças de rosto/corpo não são permitidas depois da criação, exceto por eventos (ex.: cirurgia plástica, com reação da mídia).
- Mudanças grandes de visual podem gerar uma manchete leve (sem efeito em votos, só reconhecimento).
- A tela final de carreira mostra uma **linha do tempo de retratos** (posse em cada cargo).
- **No boneco 3D (D-043):** rugas, olheiras e marcas são uma textura de detalhe da pele cuja intensidade vem da idade e do estresse; o grisalho é uma máscara nas texturas de cabelo, barba e sobrancelha. O "Espelho" troca peças (cabelo, barba, óculos, roupas) e cores, sem mexer nas formas do corpo e do rosto. O retrato de cada posse é renderizado do boneco na idade daquele momento e guardado como imagem no save, para a linha do tempo não mudar quando o boneco envelhece.

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

> A lista de ações abaixo foi a da fatia vertical. A seção 17 a substitui por compromissos com lugar e dia (17.6), equipe (17.7), verba por canal (17.8), plataforma (17.9) e lideranças (17.10).

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

> O laço semanal abaixo foi o da fatia vertical (Fases 3 a 5). A seção 17 o substitui por tempo corrente com o dia como unidade (17.4) e pela tela central (17.3).

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
│   └── ART_GUIDE.md                 # interface (paleta, tipografia, grade) e especificação das camadas de retrato
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
│       ├── CampaignMaterials/       # Santinho, Adesivo, Bandeira, PecaDeRedes, LogoComposer
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

**Renderização 2D (Godot), a de hoje, válida até a troca na parte RV:** `PortraitRenderer` empilha camadas na ordem:
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

**Renderização 3D, na parte RV (DECISOES D-043):** o modelo do Core não muda (`Appearance` e `AppearanceAging` continuam sendo a fonte); muda só quem desenha.
- **Montagem:** corpo base pelo tipo de corpo; cabeça pelo contrato da cabeça substituível (`docs/ART_GUIDE.md`); cabelo, barba, óculos e brincos presos aos pontos de troca da cabeça; roupa do guarda-roupa do contexto (3.2). Trocar uma cabeça por outra mexe só no nó da cabeça.
- **Cor:** os materiais tingíveis (`pele`, `labios`, `sobrancelha`, `cabelo`, `barba`, `iris`, roupas) recebem as cores da Appearance por shader, a partir de texturas em tons de cinza, como no 2D. Grisalho pela máscara; rugas e marcas pela textura de detalhe da pele, com a intensidade da idade.
- **Expressão:** pedida pelo nome (3.6); a cabeça a realiza por shape keys ou por troca de peças do rosto.
- **Retrato:** o boneco é renderizado em `SubViewport` com luz de estúdio fixa e enquadramento em busto; o `PortraitCache` guarda as imagens nos mesmos tamanhos (512, 256, 96), pela aparência, expressão, idade e tamanho, para listas, notícias, cartões e mapa sem custo. O retrato oficial de cada posse continua salvo como imagem no save.
- **Catálogo:** cada peça de `data/appearance/parts.json` ganha a referência à peça 3D correspondente, sem apagar a 2D enquanto as duas convivem.
- **Arte:** as peças são glTF 2.0 (`.glb`), de bases CC0 ou CC BY montadas no projeto ou de um artista (13.3 e D-045). O protótipo da RV gerou as cabeças, cabelos, corpos e roupas por scripts do Blender em `tools/rv/`, com o Blender fora do repositório.

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
> **Mudança decidida (D-043 e D-044, RV):** personagens e cenas passam a bonecos 3D estilizados de formas suaves, e o mapa, a 2D detalhado e fiel ao lugar real com a interface em janelas flutuantes (seções 13 e 17.5; proposta no ART_GUIDE). O texto abaixo descreve a direção atual, que vale até a parte RV-A.

2D vetorial ilustrado, inspirado em material de campanha brasileiro e infográficos de jornal. Interface sóbria (azul-escuro, branco), acentos verde e amarelo neutros. Fontes OFL: sans para UI (Inter/Montserrat) e condensada para manchetes (Oswald/Bebas Neue). Retratos com diversidade real de tons de pele, cabelos e traços do Brasil.

### 8.2 Telas
1. Menu principal
2. **Criação de personagem** (4 abas + pré-visualização de retrato, santinho, adesivo, bandeira e peça de redes)
3. **Espelho** (mudança de visual durante a carreira)
4. Tela central (seção 17.3), que reúne o hub, as pesquisas, as finanças, as relações e o mapa da cidade em painéis
5. Mapa eleitoral do estado e do país (seção 13.1)
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

O jogo continua 2D na interface e no mapa da cidade (17.5; DECISOES D-044). O 3D aparece no mapa eleitoral do estado e do país e, a partir da parte RV (D-043), em tudo o que mostra gente: retratos, menu como cena, cenas da RV-B e cerimônias de posse. Tudo isso roda em SubViewport 3D embutido na interface 2D, no mesmo estilo de formas suaves.

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
- **Estilo visual:** 3D estilizado com formas suaves, proporções de desenho animado e iluminação suave (referências de estilo: Two Point Hospital, Overcooked, Planet Coaster); não é low-poly facetado (DECISOES D-043). É o mesmo estilo dos retratos, do menu como cena e das cenas da RV-B.
- **Personagem 3D:** o mesmo boneco dos retratos (7.4), parametrizado a partir de Appearance:
  - Tom de pele, cor de cabelo/barba, grisalho, cores da roupa e cor do partido aplicados por material.
  - 8–12 formatos de cabelo e 4–6 de barba em 3D, mapeados a partir dos estilos 2D (cada estilo 2D aponta para o formato 3D mais próximo em data/appearance/parts.json).
  - Corpo pelo tipo escolhido na criação; roupa institucional do guarda-roupa; cabeça pelo contrato da cabeça substituível (ART_GUIDE), com as expressões da 3.6.
  - Planos sempre abertos ou médios; no clímax, corte para o **retrato oficial** em moldura, renderizado do boneco e salvo na linha do tempo da carreira.
- **NPCs:** família e aliados com nome usam o mesmo boneco e a mesma cabeça do contrato. A multidão e os figurantes usam corpos e cabeças de base CC0 com os mesmos materiais tingíveis, sem expressões no rosto e com orçamento próprio (ART_GUIDE, multidão), para que uma plateia grande não pese como um personagem com nome.
- **Relação com as cenas da RV-B (D-055):** as cenas de campanha (caminhada, comício, reunião, entrevista, notícia) e as posses usam o mesmo conjunto de bonecos, cenários e câmera; as posses entram depois, sobre o que a RV-B deixar pronto.
- **Elementos dinâmicos:** tamanho da multidão proporcional à votação; bandeiras e cores do partido; vice e família presentes quando existirem; clima e hora do dia.
- **Técnica:** cenas em .tscn com AnimationPlayer para caminhos de câmera e personagens; modelos em glTF; tudo carregado sob demanda e descarregado ao fim.
- **Ordem de produção:** primeiro a posse presidencial e a de vereador (os dois extremos da escala); depois os demais cargos.

### 13.3 Assets 3D
- **Bases (DECISOES D-045):** só CC0 ou CC BY, com a licença conferida, a prova guardada e o crédito item a item em `assets/CREDITS.md`, adaptadas ao estilo de formas suaves. Cenários, animações, corpos e multidão: KayKit e os Human Base Meshes do Blender (cabeça e corpo estilizados, ponto de partida). MPFB só como referência de expressões e de diversidade (o programa e o addon não entram no repositório). **Quaternius fora** até a confirmação da licença por escrito. Fora também: Mixamo, Ready Player Me e VRoid. Uso de ferramentas generativas: a definir pelo autor (D-045, adendo); o protótipo não usou nenhuma. A pesquisa de bases está na PENDENCIAS P-28 (fechada).
- **Cabeça, expressões e roupas:** o teste A/B do protótipo (cabeça A, de base CC0; cabeça B, gerada por código) vem antes de qualquer artista. Se o autor aprovar uma das duas, ela é usada; se reprovar as duas, um artista 3D é contratado depois de um teste pago (`docs/BRIEFING_ARTISTA_3D.md`). Em qualquer caso, a cabeça segue o contrato da cabeça substituível do ART_GUIDE (arquivos, eixos, encaixe do pescoço, pontos de troca, malhas, expressões, materiais tingíveis, orçamento), e a troca não exige mudança de código.
- **Modelagem por código:** o que o projeto gera (peças provisórias, corpos, roupas e cenários simples) sai de scripts do Blender em `tools/`, rodados com o Blender instalado na máquina e fora do repositório; o repositório guarda os scripts e os `.glb` resultantes.
- **Cenários:** posses, menu como cena (comitê, gabinete, palácio), seção eleitoral, estúdio de TV, estúdio de foto e os lugares das cenas da RV-B, sempre estilizados e sem réplica de edifício real, brasão ou marca oficial.
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
- Nomes gerados por combinação de termos típicos ("Movimento", "Partido", "Aliança", "Frente", "União" + "Democrático", "Trabalhista", "Liberal", "Popular", "Nacional", "Renovador", "Social" etc.), com siglas derivadas e verificação para não coincidir com siglas de partidos reais (nem com as mesmas letras em outra ordem) nem conter, como sequência de palavras, o nome inteiro de um partido real (lista de bloqueio em data/parties/blocklist.json).
- Logotipos usam ícones genéricos do catálogo data/parties/logo_icons.json (estrela, punho, rosa, pomba, sol, árvore, foice e martelo etc. são permitidos), mas nenhum logotipo gerado pode reproduzir o desenho de um logotipo real: formato do símbolo, disposição da sigla e paleta copiados.
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
---

## 17. REFORMULAÇÃO: IMERSÃO E PROFUNDIDADE

> Escrita depois de o autor jogar a fatia vertical (Fases 3 a 5) e reprová-la. O diagnóstico está em `docs/AUDITORIA_FASE3.md`. Esta seção substitui o laço semanal da seção 6, a lista de ações da 5.6 e as telas 4 e 5 da 8.2; as regras eleitorais, o modelo de voto, a Câmara, a prefeitura, o debate e os dados dos municípios continuam valendo. Onde esta seção e outra divergirem, vale esta. A ordem de construção é a Fase R do ROADMAP.

### 17.1 Princípios

Todo sistema novo cumpre os princípios abaixo. Cada um tem um critério que um teste ou o BalanceSim verifica; sistema que não passa não está pronto.

| | Princípio | Critério verificável |
|---|---|---|
| a | **A situação manda.** A melhor ação depende da zona, do público, da notícia, da crise ou do rival | BalanceSim, 200 campanhas por porte jogadas por um robô que escolhe sempre o compromisso de maior ganho previsto de votos: nenhum tipo de compromisso é a melhor escolha em mais de 40% das semanas, e nenhum par (tipo, zona) em mais de 25% |
| b | **O mundo se mexe sozinho.** Rivais, lideranças, mídia e economia agem com o jogador parado, e isso aparece no mapa e no feed | Teste: campanha de 40 semanas sem nenhum comando do jogador. O feed recebe pelo menos 3 notícias por semana que não citam o jogador; a liderança da intenção muda em pelo menos 1 zona; a presença de algum rival muda em mais da metade das zonas; há pelo menos 1 fato econômico a cada 8 semanas |
| c | **Pessoas com rosto e memória.** Todo NPC relevante tem nome, retrato, interesses e lembrança do que o jogador fez. Rivais respondem a ataques e mudam de estratégia | Teste: toda pessoa nos papéis de rival, liderança, equipe, doador, jornalista, assessor, secretário e vereador tem nome, aparência, pelo menos 1 interesse e lista de lembranças. Depois de um ataque do jogador, pelo menos 80% dos rivais de temperamento diferente de "calculista" respondem em até 14 dias. Rival ultrapassado pelo jogador no próprio reduto troca de plano em até 2 semanas |
| d | **A consequência fica.** Promessas cobradas no mandato, apoios com dívida, escolhas que abrem cadeias meses ou anos depois | Lint de conteúdo: toda escolha marcada como relevante grava uma entrada no Livro de compromissos ou uma lembrança em alguém; escolha relevante sem rastro reprova. Teste de carreira (campanha + mandato): pelo menos 70% das promessas geram cobrança no mandato |
| e | **Troca real.** Toda semana há ao menos uma decisão em que se ganha uma coisa e se perde outra. Semana sem decisão passa sozinha e rápido | Lint: em toda decisão, cada opção tem pelo menos um ganho e um custo de naturezas diferentes (dinheiro, tempo, relação, zona, risco, posição). BalanceSim: média de 1 ou mais decisões com troca por semana de campanha. Semana sem decisão pendente leva no máximo 7 segundos em 4x |
| f | **O mundo explica.** O efeito aparece antes do clique (previsão com incerteza) e depois (o que mudou, onde e para quem). A informação chega também por manchete, boato, conversa e pesquisa | Teste: todo comando do jogador tem previsão com faixa e explicação com lista de mudanças (o quê, onde, quem, por quê); nenhuma explicação vazia. Em 1.000 execuções simuladas, o resultado cai dentro da faixa prevista em pelo menos 80% |
| g | **Repetir cansa.** A mesma ação no mesmo lugar e público rende cada vez menos, e o jogo mostra isso antes do clique | Teste: o terceiro compromisso igual (tipo, zona, público) em 2 semanas rende no máximo 50% do primeiro, e a previsão mostra o fator de cansaço |
| h | **Objetivos que nascem do jogo.** Metas secundárias surgem da simulação e ficam visíveis num painel | Teste: em qualquer dia de campanha ou mandato há de 1 a 3 objetivos ativos, cada um com condição mensurável, prazo e origem; em uma campanha aparecem pelo menos 4 tipos diferentes |
| i | **Clareza.** Em até 10 segundos o jogador sabe o que está acontecendo, o que pede atenção e o que pode fazer | Teste automático: barra superior com no máximo 5 números; no máximo 1 elemento em cor de atenção por vez; todo acontecimento que pausa tem faixa de decisão; todo controle desativado diz o motivo. Roteiro do autor: olhar a tela por 10 segundos e responder às três perguntas |
| j | **Nada sem explicação.** Todo botão, termo, número e nome próprio do jogo se explica sozinho | Teste automático percorre todas as telas e áreas do trilho e falha se algum controle interativo não tiver tooltip nas três partes, se algum controle desativado não disser o motivo e o que falta, ou se algum termo da lista do glossário aparecer sem link para o verbete |

**Como o princípio j se cumpre:**
- **Tooltip em três partes em todo controle:** o que é (uma frase, sem jargão), o que faz (efeito esperado, com faixa de incerteza) e quanto custa (dinheiro, tempo, risco). Exemplo: "Pesquisa do instituto Prisma — mostra a intenção de voto na cidade, com margem de erro de 4 pontos. Custa R$ 35 mil e sai em 3 dias. Institutos têm tendências diferentes." O efeito e o custo vêm da previsão do Core (17.2, peça 6), nunca de texto fixo.
- **Nome fictício sempre com o que é:** "Prisma (instituto de pesquisa)", "Rádio Vale (rádio local, linha governista)", "Lúcia R. (coordenadora da sua campanha)". Na primeira ocorrência em cada painel, notícia ou cartão; nas seguintes, o nome é link para a ficha.
- **Termos técnicos sublinhados e clicáveis,** abrindo o verbete: quociente eleitoral, quociente partidário, legenda, sobras, federação, janela partidária, desincompatibilização, capital político, imagem, reconhecimento, presença, fidelidade, cansaço, margem de erro, limite de gastos, prestação de contas, emenda, indicação, requerimento, licitação, convênio, entre outros. A lista fica em `data/encyclopedia/glossary.json` e é a que o teste usa.
- **Enciclopédia** aberta por F1 de qualquer tela, com busca. Quatro partes: **Glossário** (os verbetes), **Como funciona** (as regras eleitorais reais em linguagem simples, com o artigo de lei citado e um exemplo numérico: como o quociente distribui as cadeiras, quando há segundo turno, quem pode doar), **Este mundo** (partidos, institutos, veículos, lideranças e políticos conhecidos do save atual, gerados do estado) e **Sua carreira** (linha do tempo e Livro de compromissos). Os textos fixos ficam em `data/encyclopedia/`; a parte "Este mundo" é montada do save.
- **Primeira vez:** ao abrir um sistema pela primeira vez, um cartão curto (até 40 palavras e uma seta para o controle principal), com "Entendi" e "Não mostrar dicas". Dispensado, nunca volta nesse perfil de jogador (guardado fora do save, em `user://`). Tudo o que ele diz está também na Enciclopédia.
- **Controle bloqueado diz por quê e o que falta:** "Comício: só a partir de 16 de agosto (faltam 22 semanas)", "Contratar: faltam R$ 400".

### 17.2 A espinha: seis peças que todos os sistemas usam

Para que os sistemas se entrelacem em vez de se somarem, todos são construídos sobre as mesmas seis peças do Core.

1. **Zona.** Pedaço do município em foco (17.5). Tudo acontece numa zona: compromisso, obra, liderança, reduto de rival, notícia.
2. **Pessoa.** Um só modelo para rival, liderança, membro da equipe, doador, jornalista, assessor, secretário e vereador: nome, retrato, papel, 1 a 3 **interesses** (um tema, uma zona, um objetivo próprio), **temperamento** (conciliador, combativo, calculista, leal ou volúvel), **relação** com o jogador (−100 a 100) e **memória**: até 20 lembranças, cada uma com data, fato, peso de −3 a +3 e texto ("Veio à festa da comunidade, mai/2028, +2"). Lembranças de peso 1 e 2 perdem metade do efeito a cada 52 semanas; as de peso 3 não se apagam. A relação é a soma das lembranças mais a afinidade. A ficha da pessoa mostra as lembranças por extenso.
3. **Compromisso.** Item de agenda: o quê, em que zona, em que dia e turno, para que público, com quem (17.6). Vale para campanha e para mandato.
4. **Livro de compromissos.** O que fica: **promessas** (a quem, o quê, prazo, estado), **dívidas** (a quem, o que se deve), **posições assumidas** (tema, data, onde foi dito) e **esqueletos** (o quê, gravidade, quem sabe). É lido pela cobrança no mandato, pelos ataques dos rivais, pela mídia e pela prestação de contas. Atravessa eleições.
5. **Fato e notícia.** Todo sistema emite fatos (quem fez o quê, onde, quando, com visibilidade de 0 a 3). A mídia local (17.13) transforma fatos em notícias conforme a linha de cada veículo. Fato de visibilidade 0 só é conhecido por quem estava presente, e pode vazar.
6. **Previsão, explicação e objetivo.** Todo comando do jogador tem uma previsão (faixa de resultado e fatores) e uma explicação (o que mudou, onde, para quem, por quê). Toda segunda-feira o gerador de objetivos lê o estado e propõe até 3 metas com condição, prazo e origem ("Liderar a intenção na Vila Nova até a pesquisa de setembro", "Passar Bruna C.", "Trazer Seu Anselmo para o seu lado", "Entregar a creche da Vila Nova até jun/2031"). O jogador pode fixar ou dispensar um objetivo; cumprir rende +2 de lealdade na equipe e uma lembrança positiva em quem se beneficiou.

### 17.3 Tela central

**O que é.** Uma tela só para campanha e mandato, no desenho de `docs/referencias/tela_central.png` e com as medidas de `docs/ART_GUIDE.md` (guia em vigor). Esta é a disposição até a RL; a da parte RV vem no fim desta seção:
- **Barra superior:** data com dia da semana, pausa, 1x/2x/4x, fase e contagem ("Pré-campanha · 29 sem. até a eleição"); à direita, dinheiro, reconhecimento, imagem e estresse.
- **Trilho** com 7 áreas: Campanha (no mandato, Mandato), Pessoas, Agenda, Território, Finanças, Mídia, Partido e Câmara.
- **Painel da área** (esquerda): o conteúdo da área escolhida. Em Campanha: Equipe (3/6), Verba por canal, Plataforma e próximos compromissos.
- **Mapa** (centro, sempre visível): as zonas do município. Modos: **Minha força**, **Partido líder**, **Perfil** (renda, idade, urbano e rural), **Rival** (escolher um) e, no mandato, **Obras** e **Promessas**. Marcadores: compromissos da semana, rivais em campo, lideranças (ícone do tipo, cor do alinhamento), obras com barra de andamento, acontecimentos (pulso).
- **Coluna direita:** Notícias (veículo, tempo, manchete), Objetivos (até 3) e Intenção de voto (o jogador, os dois acima e os dois abaixo, com a fonte).
- **Faixa de decisão** sobre a base do mapa: "Empreiteiro oferece doação 'por fora'. Jogo pausado. [Decidir]".

**O que o jogador decide.** Para onde olhar e por onde agir. Clicar numa zona abre a **Ficha da zona** no painel: população, eleitores, perfil, presença do jogador e dos três primeiros, lideranças conhecidas, "o que você já fez aqui", cansaço por público e o botão "Marcar compromisso aqui". Clicar numa pessoa abre a **Ficha da pessoa**: interesses, relação, lembranças, o que ela quer e o que se pode fazer com ela. Clicar numa notícia mostra o fato de origem e centraliza o mapa na zona.

**Decisões sobre o mapa.** A decisão abre num cartão de 720 px sobre o mapa escurecido; os painéis continuam legíveis para consulta. Cada opção mostra a **previsão** em até 2 linhas ("+R$ 8.000 · esqueleto grave: Lúcia e o empreiteiro saberão") e **quem vai lembrar**. Depois da escolha, o cartão vira a lista **O que mudou**, linha por linha, e fecha.

**Resultado explicado.** Todo compromisso concluído gera um cartão no feed: "Caminhada no Centro (ter, manhã): +2,1 de presença no Centro; +0,4 na Vila Nova (vizinha). Fatores: Dona Cida foi junto (+30%), terceira caminhada no Centro em duas semanas (−55%), chuva (−10%)."

**Tooltips de origem.** Todo número mostra a conta e a data do dado: "Reconhecimento 17% = média das zonas pela população: Centro 31%, Vila Nova 22%, Rural 4%".

**A troca.** Atenção: a tela mostra mais do que cabe acompanhar; o jogador escolhe que área manter aberta, e o que ele não olha anda sozinho.

**Interage com** todos os sistemas: é a única porta de entrada. **Princípios:** b, f, h, i. **Dados:** nenhum novo além dos contornos de zona (17.5).

**Reaproveita:** `Game.Execute`, `EffectSummary` (vira a explicação), `EventDialog` (vira o cartão de decisão), `PortraitView` e cache, save e autosave, o código de malhas do `MapScreen`. **Muda:** `HubScreen`, `MandateScreen` e `ExecutiveScreen` deixam de existir como telas; seu conteúdo vira painéis. O mapa da cidade é 2D plano, como na referência; o mapa 2.5D do estado (13.1) continua previsto para as esferas estadual e federal e para a apuração.

**Na parte RV: o mundo na tela inteira e a interface em janelas flutuantes (DECISOES D-044 e D-055).** O conteúdo descrito acima não muda; muda onde ele fica. Referências de composição: `docs/referencias/rv_tela_central.html` e o Geopolitical Simulator. Medidas e cores na proposta do ART_GUIDE para a RV, a aprovar pelo autor.
- **O mapa ocupa a tela inteira,** com o mapa de base da cidade real por baixo das zonas (17.5) e luz de dia e de noite pela hora do relógio.
- **Painel da área, fichas e coluna direita viram janelas semitransparentes** que flutuam sobre o mapa: arrastáveis, em pilha (só a de cima recebe clique; Esc fecha a de cima e volta à anterior; o foco fica preso no cartão de decisão). O mapa continua visível por trás.
- **A interface fica nas bordas:**
  - em cima, poucos números (no máximo 5, princípio i), com a fase e a contagem;
  - embaixo, a barra de ícones com as 7 áreas do trilho;
  - num canto, o retrato do jogador com o cargo ou a candidatura e a aprovação;
  - no outro canto, o minimapa da cidade com a legenda do modo, a data e a velocidade;
  - a faixa de decisão fica acima da barra de ícones, no centro.
- **O cartão de decisão** continua sobre o mapa escurecido, com previsão e "O que mudou".
- **Ordem (D-055):** a RL faz, na tela atual e sem trocar o estilo, a ficha da zona flutuante em pilha com Esc e os painéis atualizados por sinal do Core. A RV-A troca a disposição inteira pela de cima, junto com o mapa de base, a câmera suave travada no município, a luz de dia e de noite, o estilo final (fontes e paleta), o menu como cena e o nome "Palanque" (D-046). A RV-C põe o mundo vivo sobre o mapa (marcadores, pulso, hachura de disputa, carros, pedestres, multidão, chuva). A RV-B abre cenas curtas a partir do mapa.
- **O que o protótipo mostrou** (branch `rv-prototipo`): a composição foi montada e capturada em 1366×768 e 1920×1080, com a ficha da zona e as notícias abertas como janelas e o mapa visível entre elas. O protótipo deixou a data e a velocidade no topo, e não no canto do minimapa; com as duas janelas laterais abertas, elas cobrem cerca de metade da largura. Os dois pontos ficam para a aprovação do ART_GUIDE.

### 17.4 Tempo

**O que é.** O relógio corre. A unidade é o **dia**, com três turnos (manhã, tarde, noite). O jogador pausa e escolhe 1x, 2x ou 4x.

| Ritmo | 1x | 2x | 4x | Quando |
|---|---|---|---|---|
| Campanha | 4 s por dia | 2 s | 1 s | Do prazo de filiação até a eleição |
| Mandato | 1 s por dia | 0,5 s | 0,25 s | Fora do ano eleitoral do próprio mandato |

Uma semana de campanha sem decisão leva 7 s em 4x. Um mandato de 4 anos sem nenhuma pausa levaria 6 minutos em 4x; as pausas é que fazem a duração. Os valores ficam em `data/time.json`.

**No Core.** `AdvanceDay` é leve (meta: menos de 5 ms): executa os compromissos do dia do jogador e dos rivais, emite fatos, publica notícias, vence prazos, dispara eventos ligados a fatos. Toda segunda-feira roda a **simulação semanal** (meta mantida: menos de 200 ms): projeção de voto por zona, replanejamento dos rivais, deriva das lideranças, pagamento da equipe, economia, sorteio de eventos, objetivos, autosave. Os motores atuais continuam semanais e passam a ser chamados nesse passo.

**Pausa automática, configurável por tipo** (Opções → Tempo):

| Acontecimento | Padrão | Pode desligar |
|---|---|---|
| Decisão imediata (ex.: crise, proposta que expira hoje) | Pausa | Não |
| Decisão com prazo (ex.: "responder ao ataque até quinta") | Pausa ao chegar; o prazo corre se o jogador retomar | Não |
| Ataque contra o jogador | Pausa | Sim |
| Pesquisa publicada | Pausa | Sim |
| Rival entra numa zona em que o jogador lidera | Pausa | Sim |
| Liderança muda de lado | Pausa | Sim |
| Problema na equipe | Pausa | Sim |
| Caixa abaixo de 2 semanas de folha | Pausa | Sim |
| Semana seguinte sem nenhum compromisso marcado | Pausa no domingo | Sim |
| Compromisso concluído | Não pausa; cartão no feed | Sim |
| Notícia comum | Não pausa | Sim |

**O que o jogador decide.** Quando parar e quando deixar correr. Decisão com prazo não trava o jogo: fica na faixa com a contagem; vencido o prazo, vale a opção padrão, que a faixa mostra desde o começo ("sem resposta: o ataque fica sem contestação").

**A troca.** Decidir já, com a informação de hoje, ou esperar a pesquisa de quinta e arriscar o prazo. Velocidade alta poupa tempo do jogador e deixa passar o que ele não configurou para pausar.

**Interage com:** Agenda (compromissos caem em dia e turno), Rivais (agem nos mesmos dias), Mídia (notícia diária), Livro de compromissos (prazos). **Princípios:** b, e, i. **Dados:** `data/time.json` (ritmos, tipos de pausa e padrões).

**Reaproveita:** `Calendar` (ganha o dia), os motores semanais como estão, o sorteio de eventos (D-023), o autosave semanal. **Muda:** "Avançar semana" e "Avançar até algo acontecer" somem; o bloqueio "a semana não avança com evento pendente" vira pausa.

### 17.5 Território

**O que é.** O município em foco é dividido em **zonas**. Cada zona tem contorno no mapa, população, eleitores, perfil e, para cada candidato, **presença** (0 a 100): quanto ele é conhecido ali. O reconhecimento na cidade passa a ser a média das presenças pesada pela população. O modelo de voto roda por zona, com os mesmos 240 segmentos (D-012) repesados pelo perfil de cada zona.

| Porte | Zonas |
|---|---|
| Pequeno | 3 a 5 |
| Médio | 5 a 8 |
| Grande | 8 a 12 |
| Metrópole | 12 a 20 |

**O que existe no IBGE** (conferido em 15/11/2026 nos diretórios oficiais `geoftp.ibge.gov.br` e `ftp.ibge.gov.br/Censos/Censo_Demografico_2022/`; contagens feitas nos próprios arquivos):

| Dado | Cobertura | Formato | Serve para |
|---|---|---|---|
| Malha de Setores Censitários 2022, versão definitiva (nov/2024) | Todos os 5.570 municípios; 468.097 setores | SHP e GPKG, Brasil (738 MB em SHP) e por UF (2 a 92 MB); KML por município; sem GeoJSON; coordenadas geográficas SIRGAS 2000 | Contorno das zonas |
| Campos da malha | Todos os setores | CD_SETOR, situação (CD_SIT, 8 categorias urbano/rural), tipo (CD_TIPO), área, distrito, subdistrito, bairro, favela e comunidade urbana | Separar urbano e rural, nomear zonas |
| Agregados por setor, universo: básico (população e domicílios), sexo e idade, alfabetização, cor ou raça, características do domicílio (água, esgoto, lixo), rendimento do responsável (média e mediana) | Todos os setores, com supressão ("X") em setores muito pequenos: 132.519 das 458.772 linhas de sexo e idade têm alguma célula suprimida; o básico não tem supressão | CSV e XLSX, um arquivo Brasil por tema (9 a 136 MB) | População, idade, saneamento e nível de renda por zona |
| Distritos | 10.698, em todos os municípios | Malha e agregados | Zonas fora da sede |
| Bairros | 17.575 bairros em **895 municípios** (83,6 dos 203,08 milhões de habitantes) | Malha (26 MB) e agregados | Nome e limite de zona onde existem |
| Subdistritos | 643, em 54 municípios | Malha e agregados | Apoio nas metrópoles |
| Áreas de ponderação (amostra) | 14.270; **2.576 municípios têm duas ou mais, 2.994 têm uma só** (igual ao município) | Geometria em GPKG por UF; de-para setor → área; 35 tabelas em XLSX (44 MB) | Religião, nível de instrução e renda domiciliar per capita abaixo do município |
| Religião, instrução, renda per capita, trabalho | **Não existem por setor.** Só por área de ponderação | — | — |

Também existem, no TSE (licença CC BY declarada): eleitorado por local de votação com latitude e longitude (93.337 locais em 2024; 6,4% sem coordenada) e votação por seção.

**Conclusão.** Contorno, população, idade, saneamento e nível de renda existem para todos os municípios. Bairros oficiais só existem em 895. Religião e escolaridade, duas das cinco dimensões dos segmentos, não descem ao setor.

**Como as zonas são formadas** (novo modo `zonas` do DataImporter; os brutos ficam no cache, fora do Git):
1. Setores urbanos e rurais nunca entram na mesma zona.
2. Cada distrito fora da sede vira uma zona, ou é agrupado com o vizinho até o mínimo de população da zona. Na parte RV (o mapa de base refeito), as localidades da Base Cartográfica Contínua 1:250.000 (BC250) do IBGE (vilas, povoados, lugarejos) poderão refinar a divisão da zona rural dentro de cada distrito e dar nome às zonas rurais que hoje levam nome gerado.
3. Na sede, onde há bairros do IBGE, a zona é um grupo de bairros vizinhos e leva o nome do bairro mais populoso (`nome_fonte: "bairro_ibge"`).
4. Onde não há bairros, os setores vizinhos são agrupados por contiguidade até equilibrar a população. O nome é o do distrito (`"distrito_ibge"`) ou descritivo, gerado pela posição em relação ao núcleo mais denso: "Centro", "Zona Norte", "Rural Leste" (`"gerado"`). Nome gerado é rótulo do jogo, não dado; a Ficha da zona diz isso.
5. Contornos dissolvidos e simplificados pelo mesmo método da D-025; adjacência entre zonas gravada.
6. Atributos da zona: soma dos setores, com a regra de sigilo abaixo.
7. Testes do importador: toda zona fica dentro do polígono do município; nenhum setor fica sem zona; nenhuma zona mistura urbano e rural; a saída respeita o teto de tamanho; e os dois testes de população da regra de sigilo.

**Sigilo estatístico nos agregados por setor** (conferido nos arquivos do IBGE em 15/11/2026). O IBGE omite, com "X", células de setores muito pequenos (a regra divulgada seria menos de 5 domicílios particulares permanentes; o texto oficial não pôde ser aberto). O que os arquivos mostram:
- o agregado **básico** (população total e domicílios) **não tem nenhuma célula suprimida**; 9.327 setores têm população zero;
- o tema sexo e idade tem 132.519 linhas com alguma célula suprimida, de 458.772, e tem menos linhas que o básico (468.099), diferença ainda sem explicação;
- os outros temas usados (renda do responsável, saneamento, alfabetização) também têm supressão.

Regra:
- **População total da zona** = soma do básico. Teste: igual à população do município no Censo, sem tolerância. O importador falha, listando os casos, se achar célula suprimida no básico ou município cuja soma não bata; nada é ajustado em silêncio. Os dois registros do RS sem município (áreas das lagoas, já fora do mapa pela D-025) ficam de fora.
- **Blocos com supressão** (idade, renda, saneamento, alfabetização): célula suprimida não é estimada nem repartida. Cada bloco é normalizado pela soma das categorias publicadas na zona (mesma regra do perfil municipal). O importador grava, por zona e por bloco, a **cobertura**: população dos setores com dado publicado ÷ população da zona. Zona com cobertura abaixo de 80% num bloco usa a distribuição do município nesse bloco, com a lacuna em `_fontes.json`. Teste: a soma das categorias publicadas nunca passa da população da zona; toda zona tem cobertura gravada; nenhuma zona abaixo de 80% usa dado próprio.
- Setor sem linha no tema (a diferença entre 468.099 e 458.772) conta como suprimido.

Arquivos gerados: `data/zonas/{UF}.json` (atributos) e `data/geo/zonas/{UF}.json` (contornos). São cerca de 30 mil zonas no país (referência: os 5.570 contornos municipais ocupam 6,3 MB). **Teto: 30 MB para `data/geo/zonas/` no repositório.** O importador mede a saída e falha, sem gravar, se passar. Para caber, primeiro aumenta a tolerância de simplificação dos contornos; se não bastar, agrupa mais setores por zona (menos zonas por porte, nunca abaixo do mínimo da tabela). Tolerância e tamanho final ficam em `_fontes.json`.

**Onde não há dado por setor** (decidido pelo autor, 17.20):
- Religião e escolaridade por zona: área de ponderação onde o município tem duas ou mais; perfil municipal nos outros 2.994.
- Faixas de renda por zona: o setor só traz média e mediana do responsável. A distribuição municipal é deslocada pela razão entre a mediana da zona e a do município, marcada como estimativa de jogo.
- Eleitores por zona: até a RD, o eleitorado do TSE é repartido pela população de 16 anos ou mais de cada zona; na RD entram os locais de votação do TSE com coordenada, que também dão a votação real de 2024 por zona no cenário Brasil 2026.
- Termos de uso: o FTP do IBGE declara os arquivos públicos; a página de licença não pôde ser aberta. A R1 usa a Malha de Setores e os agregados citando a fonte; o autor confere os termos antes da RD.

**Mapa de base da cidade real (DECISOES D-044; entra na RV-A, D-055).** Por baixo das zonas, o mapa desenha a cidade como ela é. As zonas, a simulação e os dados de `data/zonas/` não mudam: o mapa de base é só o que se vê.
- **Camadas, de baixo para cima:** chão do município (área rural); área densamente edificada; mancha urbana; quadras (os quarteirões; as praças, pelo tipo de logradouro das faces, a conferir na RV-A); massas d'água; relevo sombreado (se houver fonte licenciada); faces de quadra, só de perto; rios; ferrovias; estradas de terra; rodovias pavimentadas; borda do município. Por cima, as zonas como cor translúcida do modo do mapa, as fronteiras das zonas, a zona sob o mouse, a selecionada, os marcadores e os rótulos.
- **Fontes, todas oficiais do IBGE, com crédito:**
  - **Faces de Logradouros 2022** (Censo, resultados preliminares): ruas com nome, tipo e título, nas faces de quadra das áreas urbanizadas e dos aglomerados rurais dos 5.570 municípios, com os códigos de setor e de quadra. As quadras não existem como polígono: são remontadas das faces pelo código da quadra.
  - **BC250, versão 2025:** rios com nome, massas d'água, rodovias com sigla e pavimento, ferrovias, área densamente edificada e localidades (vilas, povoados, lugarejos). Não tem curvas de nível, só pontos cotados.
  - **Elevação:** a definir. Candidatas: Topodata (INPE), ANADEM (ANA), Copernicus DEM GLO-30 e o modelo digital de elevação do IBGE; nenhuma teve a licença confirmada.
  - **Nada de OpenStreetMap** (licença ODbL) sem o autor liberar.
- **Formato:** o DataImporter gera, por município, um arquivo compacto já triangulado (áreas) e em linhas (ruas, rios, estradas), com os nomes para os rótulos, recortado no polígono do município. Onde os arquivos ficam (no repositório, baixados por município ou gerados no computador do jogador a partir do cache) é decisão do autor (PENDENCIAS P-39); até lá, ficam no cache, fora do Git.
- **Zoom e rótulos:** da cidade inteira até a rua. Cada camada de detalhe aparece a partir de uma aproximação; as linhas têm largura constante na tela. Rótulos por nível (zonas e localidades de longe; ruas, rios e rodovias de perto), com desvio de colisão e no máximo 12 nomes de zona visíveis na cidade inteira. As localidades da BC250 podem dar nome às zonas rurais que hoje levam nome gerado (item 2 acima).
- **Zonas por cima:** cor translúcida do modo, mais forte na cidade inteira e mais fraca na rua, para as ruas continuarem legíveis; fronteiras sempre por cima.
- **Luz de dia e de noite:** a hora do relógio (17.4) muda as cores do mapa de base, nunca as cores de dado (modos, partidos, marcadores).
- **Metas:** 60 FPS com zoom e arraste; mapa de base de uma metrópole pronto em menos de 3 segundos depois de a cidade entrar em foco.
- **Testes do importador:** toda quadra dentro do polígono do município; nomes de zona iguais aos de `data/zonas/`; teto de tamanho por município.
- **Medido no protótipo** (Mantena e Belo Horizonte, máquina dividida com outros programas): quadras remontadas em cerca de 80% dos códigos; BH pronta em 0,5 a 0,8 s e Mantena em menos de 0,4 s depois da primeira carga; zoom e arraste acima de 160 FPS de média; arquivo de BH com 17,8 MB (7,1 MB com gzip) no formato do protótipo, estimado em cerca de 5 MB (2,5 MB com gzip) com vértices indexados em 16 bits. Os nomes de rua vêm da fonte em caixa alta e sem acento.

**A demanda da zona.** Cada zona mostra a maior carência medida, em unidade real: "38% dos domicílios sem rede de esgoto", "renda mediana do responsável: R$ 1.412". Ela pesa nos temas dos eleitores dali e é o assunto que lideranças e imprensa levantam.

**O que o jogador decide.** Onde abrir frente e onde consolidar. A presença decai 0,6 ponto por semana em zona sem compromisso nem cabos (metade disso com cabos); 15% de cada ganho transborda para as zonas vizinhas.

**A troca.** Turno gasto numa zona não é gasto em outra. Zona populosa e disputada rende menos por turno do que zona pequena e abandonada, mas vale mais votos. Entrar no reduto de um rival provoca resposta (17.11).

**Interage com:** Agenda (todo compromisso tem zona), Verba (canais com alvo de zona), Lideranças e Rivais (têm zona), Mandato e Prefeitura (obras e promessas têm zona). **Princípios:** a, b, f, g, h. **Dados:** `data/zonas/`, `data/geo/zonas/`, `data/territory/config.json` (zonas por porte, decaimento, transbordo); na RV-A, o mapa de base por município (lugar a decidir, P-39).

**Reaproveita:** construção de segmentos a partir de um perfil (`ElectorateBuilder`), `VoteModel` sem mudança na fórmula, curva de ganho decrescente (`EffectApplier.Gain`), pipeline de contornos da D-025, NetTopologySuite só no importador. **Muda:** `Candidate.Recognition` passa a ser derivado das presenças; o apoio por grupo passa a valer por zona.

### 17.6 Agenda com lugar e dia

**O que é.** A semana é uma grade de 7 dias por 3 turnos. Cada **compromisso** ocupa um turno (o comício, dois), numa zona, para um público, com ou sem acompanhante. Substitui a energia de 100 pontos.

| Compromisso | Onde e para quem | O que rende | Condição |
|---|---|---|---|
| Caminhada | Uma zona; público geral | Presença na zona | Rende mais com cabos e liderança da zona junto |
| Reunião em casa de apoiador | Uma zona; um grupo | Apoio do grupo na zona | Exige apoiador ou liderança na zona |
| Visita a liderança | A liderança | Relação; revela o pedido | — |
| Comício | Uma zona; geral | Presença alta, imagem | Só na campanha; exige verba de Rua; esvazia se a presença na zona for menor que 20 |
| Entrevista | Um veículo | Alcance do veículo; torna posições conhecidas | Teste de Oratória; ver 17.13 |
| Gravação para as redes | Sem zona; um tema | Posição conhecida; presença entre os mais jovens | Rende conforme o social media |
| Jantar de arrecadação | Doadores | Dinheiro | Ver 17.12 |
| Reunião de equipe | Comitê | Lealdade; conselho do coordenador; informação sobre os rivais | — |
| Evento da cidade | A zona e o dia do evento | Bônus com o público presente | Só existe no dia (feira de sábado, festa da paróquia, vigília, assembleia do sindicato, jogo, festa junina) |
| Descanso e família | — | −5 de estresse | — |

**Rendimento,** mostrado na previsão antes de marcar:
`base do tipo × alcance na zona e no público × afinidade da plataforma com o público × equipe × liderança presente × ocasião × (1 ÷ (1 + 0,6 × cansaço)) × sorte`

- **Cansaço** (princípio g): cada compromisso soma 1 ao cansaço do trio (tipo, zona, público), que perde 0,25 por semana. O segundo igual na mesma semana rende 62%; o terceiro, 45%. A Ficha da zona e a previsão mostram o fator.
- **Ocasião:** evento da cidade (+50% com o público dele), tema em alta na imprensa (+30% se o compromisso trata do tema), chuva em compromisso de rua (−10% a −40%).
- **Turnos:** dois por dia sem custo; o terceiro soma +3 de estresse. Estresse acima de 70 reduz a chance nos testes de atributo.
- **Deslocamento:** em cidade grande e metrópole, dois compromissos no mesmo dia em zonas não vizinhas perdem 25% no segundo.
- **Delegar:** o coordenador manda representante (membro da equipe ou liderança aliada). Rende 50%, e quem recebeu lembra que o candidato não foi (+1 em vez de +4).

**O que o jogador decide.** O que marcar, onde, quando, com quem; que convite aceitar. Convites chegam com dia e turno fixos e conflitam entre si: a assembleia do sindicato é na quinta à noite, mesma hora do jantar com doadores.

**A troca.** Cada turno é um só. Aceitar um convite é recusar outro, e quem foi recusado lembra. Ir pessoalmente rende o dobro e ocupa o candidato.

**Interage com:** Território (zona), Equipe (delegação, multiplicadores), Lideranças (acompanhante, convites), Mídia (entrevista, tema em alta), Rivais (os compromissos públicos deles aparecem no mapa no dia; com coordenador de qualidade 4 ou 5, na véspera), Tempo (dia e turno). **Princípios:** a, e, f, g. **Dados:** `data/campaign/commitments.json` (substitui `actions.json`), `data/world/city_events.json` (tipos de evento da cidade por porte, mês e composição do município; eventos religiosos proporcionais à composição do Censo).

**Reaproveita:** `Effects`, teste de atributo e riscos das 6 ações, custo por porte, a regra "comício só a partir de 16 de agosto". **Muda:** energia semanal sai; o efeito deixa de ser constante.

### 17.7 Equipe

**O que é.** Cinco funções com pessoa nomeada e a rede de cabos eleitorais. Vagas por porte: pequeno 3, médio 4, grande 5, metrópole 6 (contando a rede de cabos).

| Função | O que faz sozinha | Qualidade 1 → 5 |
|---|---|---|
| Coordenador(a) | Organiza a agenda, delega compromissos, comanda os cabos, dá o conselho da semana | 1 → 5 compromissos delegados por semana; com 4 ou 5, vê a agenda dos rivais na véspera |
| Marqueteiro(a) | Tom da campanha, programa do horário eleitoral gratuito, material | Rendimento de comício, material e horário gratuito de 0,8 a 1,3; estreita a faixa das previsões |
| Tesoureiro(a) | Registra receitas e gastos, cuida do limite | Chance de erro contábil por semana de 8% a 0,5% |
| Advogado(a) | Responde a impugnações, pede direito de resposta | Chance de êxito de 35% a 85%; sem advogado, vale a pior opção padrão |
| Social media | Toca o canal Redes, reage a virais | Alcance de 0,7 a 1,4; risco de viral negativo de 10% a 3% |
| Cabos eleitorais | Mantêm a presença na zona, enchem comício | Por zona, em grupos de 5; rendem metade sem material gráfico |

Cada candidato a uma vaga (2 a 4 por função, gerados quando o município entra em foco) tem qualidade (mostrada como faixa até completar 4 semanas de trabalho), salário semanal, lealdade (0 a 100), temperamento, interesses e **vínculos**: "coordenou a campanha de Daniel A. em 2024", "sobrinha de Seu Anselmo". Exemplo em cidade média: Lúcia R., coordenadora, qualidade 3 a 4, R$ 900 por semana.

**Opinião.** Toda segunda, o coordenador dá um conselho calculado da projeção real, com erro maior quanto menor a qualidade: "A Vila Nova está cara demais para nós; eu concentraria no Centro". Marqueteiro opina sobre plataforma e tom; tesoureiro, sobre o limite; advogado, sobre risco. Seguir ou ignorar o conselho mexe na lealdade (±2).

**Eventos próprios.** Pede aumento; recebe proposta de um rival; briga com outro membro (o jogador escolhe um lado); traz um doador ou uma liderança pelo vínculo; comete irregularidade na rua; demitido com lealdade baixa, conta ao rival o plano e os esqueletos que conhecia.

**O que o jogador decide.** Quem contratar, quanto pagar, a quem ouvir, quem demitir.

**A troca.** Dinheiro contra qualidade; competência contra lealdade (o melhor marqueteiro da cidade trabalhou para o rival; a prima é leal e tem qualidade 2); demitir quem sabe demais.

**Interage com:** Agenda (delegação), Verba (multiplicadores), Doadores e contas (tesoureiro e advogado), Mídia (social media, marqueteiro), Rivais (assédio, vazamento), Lideranças (vínculos), Mandato (a equipe vira gabinete; quem fica de fora lembra). **Princípios:** a, c, d, e, f. **Dados:** `data/campaign/staff.json`.

**Reaproveita:** `PersonGenerator`, retratos, atributos, eventos de bastidores existentes (ex.: "brindes proibidos"). **Novo:** a Pessoa com memória (17.2).

### 17.8 Verba por canal

**O que é.** A verba semanal da campanha é repartida em quatro canais pagos, cada um com alvo. Rádio e TV não são canal de verba: são presença conquistada (abaixo).

| Canal | O que compra | Alvo | Rende mais | Rende menos |
|---|---|---|---|---|
| Rua | Carro de som, bandeiras, estrutura de comício | Zonas | Zona densa; multiplica caminhada e comício na zona | Zona rural espalhada |
| Redes | Produção e impulsionamento pago, permitido com regras (Lei 9.504/1997, art. 57-C) | Faixa de idade | 16 a 34 anos (peso 1,0); 35 a 59 (0,5) | 60 ou mais (0,15) |
| Material gráfico | Santinhos, adesivos, bandeiras e impressos dentro do permitido | Zonas | Presença baixa e duradoura; pré-requisito dos cabos | — |
| Cabos eleitorais | Pessoal por zona, até o limite de contratação pelo eleitorado (art. 100-A), em objeto de configuração | Zonas | Converte indecisos; segura a presença | Zona onde uma liderança contrária domina |

- **Retorno:** efeito = k × √(verba ÷ escala do porte) × (verba própria ÷ verba total do canal na zona)^0,5. Dobrar a verba num canal rende 41% a mais, e gritar onde todos gritam rende menos. A previsão mostra as duas contas.
- **Pré-campanha:** só Redes sem impulsionamento e material institucional do partido; os canais abrem em 16 de agosto.
- **Rádio e TV: presença conquistada.** Propaganda paga em rádio e TV é proibida (art. 44). O candidato chega ao rádio e à TV de duas formas, nenhuma comprada: **entrevistas** (compromisso da agenda, 17.6 e 17.13), que dependem da relação com o veículo e do interesse dele; e o **horário eleitoral gratuito**, cujo tempo vem do partido (D-010) e cujo rendimento vem do marqueteiro e do tom escolhido. Alcance: maior entre 35 anos ou mais, na zona rural e nos portes pequeno e médio; nulo em cidade sem emissora.
- **Proibido, e por isso fora do jogo como opção:** outdoor (art. 39, § 8º) e brindes (art. 39, § 6º). Quem distribui brinde é um cabo fazendo algo irregular (evento já existente).
- Os artigos acima estão citados de memória e são conferidos na R3 antes de virar configuração (17.20).

**O que o jogador decide.** Quanto gastar por semana, em que canais, com que alvos.

**A troca.** Todo real gasto conta no limite legal e sai do caixa da reta final. Cortar cabos de uma zona os manda para um rival, e eles lembram.

**Interage com:** Território (alvos), Equipe (multiplicadores), Doadores e contas (caixa e limite), Rivais (disputa de canal por zona). **Princípios:** a, e, f, g. **Dados:** `data/campaign/channels.json`.

**Reaproveita:** custo por porte, multiplicador de reconhecimento por porte, tempo de TV pelo partido (D-010). **Muda:** dinheiro deixa de ser gasto só por clique de ação.

### 17.9 Plataforma

**O que é.** Três camadas:
1. **Posições** em 10 temas, de −2 a +2 (GDD 5.2). O `VoteModel` já aceita posição por tema e a campanha atual nunca preenche; passa a preencher. O padrão é a posição do partido.
2. **Bandeiras:** até 3 temas que a campanha enfatiza. Cada tema tem **posição conhecida** (0 a 100%): sobe quando o jogador fala dele em entrevista, gravação, comício e debate. O eleitor que não conhece a posição presume a do partido.
3. **Promessas locais:** "calçar a Rua 3 da Vila Nova até 2030", feita a uma zona, a um grupo ou a uma liderança, escolhida de um catálogo ligado aos tipos de obra e serviço (17.15). Dá apoio imediato e entra no Livro de compromissos.

**Painel "Plataforma × públicos".** Para cada tema, quem gosta e quem não gosta, por grupo e por zona. A previsão de uma mudança aparece antes: "Segurança de 0 para +2: +1,8 ponto entre 60 anos ou mais, −1,1 entre 18 e 34; Vila Nova +0,9; Centro −0,4".

**Custo de mudar.** Incoerência = tamanho do salto × posição conhecida × (1 + mudanças nas últimas 52 semanas). Pesa na imagem, vira fato que rivais e imprensa usam, grava lembrança em lideranças com interesse no tema e, se a nova posição sair da faixa do partido, esfria a relação com a direção (menos fundo). Promessas demais derrubam a **credibilidade**, que multiplica o apoio que cada promessa nova rende.

**O que o jogador decide.** Onde se posicionar, o que enfatizar, o que prometer e a quem.

**A troca.** Toda posição agrada um público e desagrada outro; enfatizar torna a posição conhecida também por quem não gosta. Ficar vago é permitido e custa diferenciação.

**Interage com:** Território (mistura de públicos por zona), Lideranças (interesses e pedidos), Rivais (posicionam-se e atacam contradições), Mídia (entrevista pergunta sobre temas; tema em alta), Debate (o que é dito ao vivo vira posição assumida), Mandato (votos na Câmara comparados com as posições; promessas cobradas). **Princípios:** a, d, e, f. **Dados:** `data/campaign/platform.json`, `data/campaign/promises.json`.

**Reaproveita:** `ThemeCatalog`, preferências e prioridades de tema dos segmentos, `VoteCandidate.Themes`, `DebateEngine`. **Novo:** posição conhecida, promessas, credibilidade.

### 17.10 Rede de lideranças locais

**O que é.** Pessoas nomeadas que movem votos numa zona e num público. Quantidade por porte: pequeno 6 a 8, médio 10 a 14, grande 16 a 20, metrópole 24 a 30. Tipos: religiosa, comunitária, sindical, empresarial, rural.

Cada liderança tem: zona (uma ou duas), público, **influência** (1 a 5), **interesse** (um tema ou a demanda da zona), **pedido** (uma promessa de obra, uma vaga no gabinete para um indicado, uma posição pública num tema, a presença num evento ou dinheiro para um evento, que é irregular e vira esqueleto), **preço** (a relação mínima mais o pedido), alinhamento (com o jogador, com um rival, neutra) e memória.

Exemplo: Seu Anselmo, presidente da associação rural, zona Rural, influência 4, interesse na estrada vicinal. Pedido: a promessa de cascalhar 12 km. Lembranças: "Veio à assembleia, fev/2028, +2".

**Como funciona.**
- **Descobrir:** a Ficha da zona mostra as lideranças conhecidas; as outras aparecem como "?" até uma caminhada na zona ou um achado do coordenador.
- **Visitar** (compromisso): ouve o pedido. O jogador aceita (promessa no Livro), negocia (teste de Articulação) ou recusa.
- **Apoio:** liderança alinhada dá ao seu público, na sua zona, apoio igual a influência × relação ÷ 100, rende +30% nos compromissos em que acompanha e fornece cabos.
- **Entre elas:** têm rivalidades. Fechar com a presidente da associação esfria o pastor que disputa o mesmo bairro (−10).
- **Sozinhas:** sem contato por 8 semanas, a relação cai 1 por semana; recebem rivais ("Pastor Elias recebeu Daniel A. no culto de domingo", no feed); mudam de lado.

**Neutralidade.** O tipo descreve o papel social, não uma doutrina. Lideranças religiosas são sorteadas na proporção da composição religiosa do município no Censo. Qualquer interesse pode ocorrer em qualquer tipo, com pesos que não passam de 2 para 1; o DataValidator verifica.

**O que o jogador decide.** Quem procurar, o que conceder, com quem romper.

**A troca.** Cada aliado custa uma promessa ou uma dívida e afasta os desafetos dele. Manter a rede consome turnos.

**Interage com:** Território, Agenda, Plataforma (interesse e promessa), Rivais (disputam as mesmas lideranças), Equipe (vínculos), Doadores (lideranças empresariais doam), Mídia (veículo cujo dono é liderança), Mandato (cobrança), Renovação (17.16: lideranças viram candidatos). **Princípios:** a, b, c, d, e, h. **Dados:** `data/campaign/leaders.json`.

**Reaproveita:** o apoio por grupo do `VoteModel`; os 8 alvos da ação "Visita" viram tipos de liderança; `PersonGenerator`. **Muda:** apoio deixa de ser um número sem dono que decai sozinho.

### 17.11 Rivais com estratégia

**O que é.** Cada rival nomeado tem **perfil**, **plano** e **memória**.

| Perfil de campanha | Como joga |
|---|---|
| De base | Forte em 1 ou 2 zonas, com cabos e lideranças comunitárias |
| De mídia | Redes, entrevistas e horário gratuito; fino na cidade inteira |
| De máquina | Incumbente ou aliado da prefeitura; obras, emendas e lideranças |
| De causa | Um tema e um público |
| De estrutura | Dinheiro, material e equipe |

O perfil é um estilo de campanha, sorteado sem relação com ideologia ou partido.

**Plano**, refeito toda segunda: meta de votos, 2 ou 3 zonas-alvo, mistura de canais e postura diante de cada concorrente (ignorar, disputar a zona, atacar, compor). Gatilhos de mudança: pesquisa; o jogador ganhar mais de 10 pontos de presença no reduto dele; ataque; perda de uma liderança; saída de outro rival (17.16).

**Resposta a ataque,** pelo temperamento: combativo revida em 3 a 7 dias; conciliador se faz de vítima e ganha imagem na própria base; calculista aciona a Justiça Eleitoral; quem está muito à frente ignora. A lembrança do ataque atravessa a eleição: na Câmara, o rival atacado vota contra os projetos do jogador (o `ChamberVote` já pesa a relação).

**Alianças entre eles.** Dobradinha de candidato a vereador com candidato a prefeito; pacto de não agressão; divisão de zonas. E a regra real do sistema proporcional: **colegas de chapa são aliados e concorrentes ao mesmo tempo.** O painel mostra "Seu partido hoje faria 2 cadeiras; você é o 3º da chapa": o voto do colega ajuda o partido a fazer a cadeira e pode deixá-lo na frente do jogador.

**O que o jogador decide.** Na Ficha do rival (reduto, plano estimado com a confiança do coordenador, lembranças): atacar (por incoerência, esqueleto ou tema; por que canal; sem prova há risco de processo), propor pacto ou dobradinha, investigar, responder.

**A troca.** Atacar tira imagem de quem ataca entre quem rejeita campanha negativa e provoca resposta. Pacto libera agenda e fecha zonas; quebrá-lo grava −3 e vira notícia.

**Interage com:** Território (redutos, marcadores no mapa), Verba (disputa de canal), Lideranças (assédio), Equipe (assédio e vazamento), Mídia (ataques e respostas), Plataforma (contradições), Doadores e contas (denúncia), Mandato (memória na Câmara). **Princípios:** a, b, c, d, e. **Dados:** `data/campaign/rival_profiles.json`, `data/campaign/attacks.json`.

**Reaproveita:** `RaceSetup` (6 partidos, 10 nomeados, candidato coletivo, incumbentes), empenho por porte (D-026), estilos do debate, `EventState.Relations`. **Descarta:** o sorteio de ações de `NpcTurn`. O candidato coletivo continua sem plano.

### 17.12 Doadores e prestação de contas

**De onde vem o dinheiro.**

| Fonte | Regra | Custo |
|---|---|---|
| Fundo eleitoral, pela direção do partido | Cota decidida pelo cacique (NPC): relação, viabilidade na pesquisa, distância da linha do partido | Fidelidade; dívida com a direção |
| Doação de pessoa física | Doadores nomeados (4 a 12 por porte), cada um com capacidade, interesse, pedido e memória; limite de 10% da renda do doador no ano anterior (Lei 9.504, art. 23, § 1º) | Dívida no Livro; o doador cobra no mandato |
| Recursos próprios | Até 10% do limite de gastos do cargo (art. 23, § 2º-A) | Dinheiro do personagem |
| Financiamento coletivo | A partir de 15 de maio do ano da eleição (art. 22-A, § 3º); cresce com a presença nas redes | Pouco dinheiro |
| "Por fora" | Sem limite e sem registro | Esqueleto; quem entregou e quem viu sabem |

Empresa não doa (STF, ADI 4650; Lei 13.165/2015): a oferta de um empresário é doação pessoal dentro do limite ou é "por fora".

**Limite de gastos.** Por cargo e por município (Lei 9.504, art. 18-C; o TSE publica a tabela a cada eleição). Os valores reais vêm da tabela do TSE de 2024, importada e corrigida pela regra da lei (aprovado, 17.20).

**Painel Contas.** Arrecadado, gasto, barra do limite e a lista de irregularidades, cada uma com o que é e quem sabe. O **risco de contas** (0 a 100) soma gasto não declarado, doação acima do limite, fonte vedada, gasto acima do limite, erro contábil e pessoal acima do permitido. Há prestação parcial em setembro e final 30 dias depois da eleição.

**Risco real.** Depois da eleição as contas são aprovadas, aprovadas com ressalvas ou desaprovadas. Irregularidade grave mais denúncia (de rival, de ex-membro da equipe ou da imprensa) abre representação por captação ou gasto ilícito (art. 30-A), que pode cassar o diploma meses depois da posse. Meta de balanceamento: campanha com mais de 30% do limite em dinheiro "por fora" perde o mandato em 25% a 40% das carreiras em até 2 anos; campanha limpa, em menos de 1%.

**Esqueletos passam a funcionar.** Cada um tem gravidade e a lista de quem sabe. Chance semanal de vir à tona = base × gravidade × (pessoas que sabem com relação ou lealdade abaixo de −20) × exposição (cargo e posição na pesquisa).

**O que o jogador decide.** De quem aceitar, quanto declarar, quando parar de gastar.

**A troca.** Dinheiro agora contra dívida e risco depois. Campanha limpa é campanha mais pobre.

**Interage com:** Verba (gasto), Equipe (tesoureiro, advogado, quem sabe), Lideranças (empresariais), Rivais (denúncia), Mídia (reportagem), Mandato e Prefeitura (cobrança do doador, cassação). **Princípios:** c, d, e, f. **Dados:** `data/campaign/finance.json`; regras de lei num objeto de configuração com o padrão da lei vigente.

**Reaproveita:** `Effects.AddSkeleton` e a lista de esqueletos (hoje gravada e nunca lida), a cadeia "doação por fora → perguntas do jornal", `PartyFundingRulesConfig` (D-010).

### 17.13 Mídia local

**O que é.** Veículos fictícios por porte: pequeno 2 (rádio comunitária e página de notícias da cidade), médio 3 ou 4 (rádio, jornal ou portal, páginas), grande 5 (mais TV regional e segundo portal), metrópole 6 ou 7.

Cada veículo tem tipo, alcance por zona e por público, **linha editorial** (governista, crítica ou independente em relação à prefeitura; 2 temas de interesse; grau de sensacionalismo), **dono** (uma Pessoa, que pode ser doador, liderança ou político) e **jornalista principal** (Pessoa com memória). O nome sempre aparece com o que é: "Rádio Vale (rádio local, linha governista)".

**O feed.** Cada fato de visibilidade 1 ou mais é coberto pelos veículos conforme o interesse; a linha editorial decide o tom (favorável, neutro, crítico), nunca o fato: veículo não inventa. Itens de rede marcados como **boato** podem ser falsos; o jogador desmente, processa ou ignora (GDD 5.10).

**Tema em alta.** Uma semana de notícias sobre a seca aumenta o peso do tema água nas zonas rurais. O melhor assunto da semana depende do noticiário (princípio a).

**O que o jogador decide.**
- **Entrevista** (compromisso): as perguntas são sobre temas; responder torna a posição conhecida. Veículo crítico pergunta mais duro (chance menor) e convence mais (alcance com mais crédito).
- **Sugerir pauta** ao jornalista: sobre um ato próprio ou um fato de um rival; depende da relação.
- **Responder a ataque,** decisão com prazo de 48 horas: no mesmo veículo, por outro canal, direito de resposta (advogado, 2 a 4 dias), contra-ataque ou silêncio. Ataque respondido bem se apaga em 4 semanas; sem resposta, em 26.
- **Anunciar:** imprensa escrita e internet, dentro dos limites da lei (art. 43); rádio e TV só no horário gratuito.

**A troca.** Falar com o veículo amigo é seguro e convence pouco; ir ao crítico arrisca. Responder dá sobrevida ao assunto; calar deixa a versão do rival.

**Interage com:** Agenda, Plataforma (posição conhecida, tema em alta), Rivais (ataques), Equipe (social media, advogado, marqueteiro), Doadores e contas (reportagem investigativa), Lideranças (donos), todos os sistemas (fatos). **Princípios:** a, b, c, e, f. **Dados:** `data/media/outlets.json` (tipos por porte, termos de nome, linhas), `data/media/templates.json` (manchetes por tipo de fato e tom), `data/media/blocklist.json` (nomes de veículos reais a evitar; aprovado, 17.20).

**Reaproveita:** `Headline`, os 9 eventos de mídia, a manchete de pesquisa, o teste da ação "Entrevista".

### 17.14 Mandato de vereador

**Gabinete.** Assessores nomeados (2 em cidade pequena, até 6 em metrópole): chefe de gabinete, assessor legislativo (qualidade aumenta a chance do projeto na comissão), assessor de comunicação e **assessores de base**, cada um numa zona, mantendo a presença e trazendo demandas. Nomear gente da campanha ou indicados de lideranças paga dívidas; quem fica de fora lembra; parente nomeado é esqueleto.

**Instrumentos** (substituem as 7 ações de gabinete):

| Instrumento | O que o jogador escolhe | O que acontece |
|---|---|---|
| Requerimento de informação | Uma secretaria, obra ou contrato | Em 2 a 4 semanas revela dado oculto (atraso, sobrepreço, quem venceu a licitação); esfria a relação com o prefeito |
| Indicação | Um serviço ou pequena obra numa zona | O prefeito atende conforme a relação e a base; atendida, aparece no mapa "a pedido do vereador" e cumpre promessa |
| Fiscalização | Uma obra ou serviço no mapa (compromisso na zona) | Encontra normalidade, atraso ou irregularidade; então: denunciar (imprensa, inimizade do prefeito e do contratado), negociar em silêncio (ganha obra ou cargo; vira esqueleto) ou encaminhar ao Tribunal de Contas (lento e limpo) |
| Emenda | Tipo de obra, zona | Entra no mapa com prazo; a liberação depende do Executivo; a entrega é um compromisso de inauguração |
| Projeto de lei | Um dos modelos, ligado à plataforma | Tramitação atual (comissão, plenário) |
| Negociar voto | Um vereador indeciso | Ele tem um pedido (apoio ao projeto dele, emenda no reduto dele, cargo); cumprir ou não fica na memória |

**Votações.** O painel mostra a **contagem** nominal antes do voto: "5 sim, 4 não, 2 indecisos: Marta (quer a emenda da creche) e Josué (segue o líder)". O jogador define uma instrução permanente ("seguir a orientação" ou "seguir minha plataforma") e o jogo só pausa nas votações em que orientação, plataforma, promessa ou base entram em conflito.

**A base cobra.** O mapa ganha o modo "Meus votos" (a apuração por zona). Cada promessa do Livro tem prazo; lideranças vêm cobrar; promessa vencida derruba a relação, e a liderança passa a apoiar um desafiante. Assessor de base negligenciado pode sair candidato contra o jogador.

**A cidade se mexe.** O prefeito NPC toca obras, atrasa, enfrenta crises; a Mesa é eleita a cada dois anos; fiscalização pode virar CPI (pendências da D-027).

**O que o jogador decide.** Onde pôr emenda e assessor, o que fiscalizar, denunciar ou negociar, quando contrariar o partido.

**A troca.** Fidelidade ao partido contra a base; boa relação com o prefeito (indicações atendidas) contra fiscalização (visibilidade); emenda numa zona e não em outra.

**Interage com:** Território (obras, votos por zona), Agenda (fiscalização, inauguração), Equipe (gabinete), Lideranças (cobrança), Plataforma (coerência, promessas), Mídia (denúncia), Rivais (memória; novos desafiantes), Prefeitura (as mesmas obras e secretarias). **Princípios:** a, b, c, d, e, h. **Dados:** `data/legislature/instruments.json`, `data/works/types.json` (compartilhado com 17.15), mais projetos em `data/bills/`.

**Reaproveita:** `LegislatureEngine` inteiro: `ChamberVote`, fidelidade, quóruns, comissões, tramitação, os 36 projetos, relação com o prefeito, `CareerFlow`. **Descarta:** as 7 ações de efeito fixo e a emenda sem lugar.

### 17.15 Prefeitura

**Secretários.** As 8 secretarias passam a ter titular: competência (1 a 5: velocidade do indicador da área e atraso das obras), lealdade, partido, integridade (oculta) e **agenda própria**: quer ser candidato e usa a pasta para aparecer; favorece uma zona; tem um projeto de estimação; obedece ao cacique do partido dele. Entregar a secretaria a um partido aliado é nomear um dos 2 nomes que ele oferece. Demitir tira o partido da base.

**Obras no mapa.** Catálogo: posto de saúde, creche, escola, pavimentação, estrada vicinal, ponte, drenagem, praça, iluminação, terminal. Cada obra tem zona, custo, secretaria e quatro etapas com barra no mapa:

| Etapa | Duração de exemplo | O que pode acontecer |
|---|---|---|
| Projeto | 4 a 8 semanas | Depende da competência do secretário |
| Licitação | Concorrência ou pregão: 8 a 16 semanas. Contratação emergencial: 1 a 2 semanas, válida só em emergência ou calamidade (Lei 14.133/2021, art. 75, VIII) | Impugnação atrasa; emergencial sem emergência é irregularidade; empresa de doador vencendo é fato que a imprensa nota |
| Execução | 12 a 60 semanas | Atraso e aditivo conforme a empresa (fictícia, com qualidade e dono), o secretário e a chuva |
| Entrega | Compromisso de inauguração | Só se inaugura obra que existe. Nos 3 meses antes da eleição, candidato não comparece a inauguração (Lei 9.504, art. 77): obra que não fica pronta até julho não rende palanque |

**Servidores.** A folha é uma fração da receita corrente líquida, com os limites da Lei de Responsabilidade Fiscal (LC 101/2000, art. 20, III, b: 54% para o Executivo municipal; limite prudencial de 95% disso, 51,3%, acima do qual reajuste e contratação ficam vedados, art. 22). Na data-base, o sindicato dos servidores (uma liderança sindical) pede reajuste; a negociação decide entre percentual, limite e greve. A greve deixa de ser sorteio e passa a ser consequência.

**Convênios.** Com o estado e a União: valor, objeto (uma obra), contrapartida do município, prazo e prestação de contas. Dependem da relação com o governador (NPC) e de deputados aliados. Convênio não executado devolve o dinheiro e bloqueia novos.

**Câmara que negocia.** Projeto que precisa de lei mostra a contagem nominal; cada indeciso tem um pedido (obra no reduto, cargo para indicado, liberação de emenda). Vereador atendido lembra; enganado, também. Aprovação baixa somada a escândalo abre CPI e pedido de cassação (pendência da D-031).

**Orçamento e indicadores.** Continuam (D-031), agora ancorados em números reais e mostrados em unidade real (17.17).

**Crises com causa.** Enchente atinge uma zona e é menor onde há drenagem entregue; greve vem da negociação da folha; escândalo vem da integridade de um secretário ou de uma licitação; queda de arrecadação vem da economia (17.17).

**O que o jogador decide.** Quem nomear, que obra fazer e onde, por que modalidade licitar, quanto dar de reajuste, que pedido de vereador atender, que convênio buscar.

**A troca.** Competência contra coalizão; rapidez contra legalidade; reajuste contra limite fiscal; zona A contra zona B; contrapartida de convênio contra prioridade própria; promessa de campanha contra emergência.

**Interage com:** Território (obras, crises e aprovação por zona), Agenda (inauguração, visita), Plataforma (promessas cobradas), Lideranças (sindicato, cobrança), Doadores (cobrança, empresa na licitação), Mídia (linha diante da prefeitura), Mandato de vereador (os mesmos instrumentos, do outro lado), Estatísticas reais (17.17), Renovação (17.16). **Princípios:** a, b, c, d, e, f, h. **Dados:** `data/executive/secretaries.json`, `data/works/types.json`, `data/executive/procurement.json`, `data/executive/personnel.json`, `data/executive/agreements.json`; limites de lei em objeto de configuração.

**Reaproveita:** `ExecutiveEngine` quase inteiro (orçamento com mínimos e Tribunal de Contas, indicadores com atraso, aprovação por grupo, 17 políticas, coalizão, 7 crises), `ChamberVote`. **Descarta:** secretaria como contador, "Inaugurar obra" sem obra, crise só por sorteio.

### 17.16 Renovação do mundo político

**O que é.** Toda Pessoa (17.2) envelhece, pode morrer, pode sair da política e é substituída por gente nova. Vale para rivais, vereadores, prefeitos, secretários, lideranças, equipe, doadores, jornalistas e, a partir da Fase 6, deputados, senadores, governadores e presidente. O personagem do jogador segue as regras de saúde e idade da seção 4.3.

**Envelhecimento e mortalidade.**
- **Fonte principal, por UF** (decisão do autor, 17.20, item 13): **IBGE, Projeções da População, revisão 2024, tábuas de mortalidade** (`https://ftp.ibge.gov.br/Projecao_da_Populacao/Projecao_da_Populacao_2024/projecoes_2024_tab5_tabuas_mortalidade.xlsx`, 24 MB, também em ODS): Brasil, regiões e as 27 UFs, por sexo, anos de 2000 a 2070. São tábuas abreviadas (idades 0, 1, 5, 10, …, 85 e 90 ou mais), com nqx em proporção. O importador usa o ano de 2024, fixo, e a UF de residência da pessoa. Para chegar à idade simples, reparte a probabilidade de cada faixa pelas idades dela seguindo o perfil da tábua completa do Brasil; o método fica descrito em `_fontes.json`, e um teste confere que a probabilidade recomposta de cada faixa é igual à publicada.
- **Reserva, Brasil: IBGE, Tábuas Completas de Mortalidade para o Brasil, 2024** (a mais recente; arquivos de 1º/12/2025), em `https://ftp.ibge.gov.br/Tabuas_Completas_de_Mortalidade/Tabuas_Completas_de_Mortalidade_2024/`, em XLSX e ODS, uma planilha para homens, uma para mulheres e uma para ambos os sexos. Só Brasil; idades simples de 0 a 89 e grupo aberto de 90 anos ou mais. Coluna usada: Q(X), probabilidade de morte entre as idades X e X+1, por mil.
- Conferência de leitura (valores de 2024, por mil): aos 40 anos, 3,265 para homens e 1,535 para mulheres; aos 60, 12,532 e 6,888; aos 80, 62,284 e 46,531.
- O importador grava as tábuas (27 UFs e Brasil) em `data/demography/mortality.json`, com a fonte em `_fontes.json`. Personagem não binário usa a tábua de ambos os sexos.
- Chance anual de morte = Q(UF, sexo, idade) × fator pessoal. O fator vai de 0,6 a 2,5 conforme saúde e estresse acumulado (`data/demography/config.json`); o sorteio é mensal, pelo Rng. Para 90 anos ou mais, a tábua não dá idade simples: a curva é prolongada por um parâmetro de jogo declarado no mesmo arquivo.
- A tábua do Brasil é usada quando a da UF faltar ou não puder ser lida, e sempre como perfil para a idade simples. O save registra qual foi usada.

**Saídas da política.** Verificadas uma vez por mês para cada pessoa; quando mais de uma ocorre, a ordem é sorteada pelo Rng.

| Saída | Quando | O que deixa |
|---|---|---|
| Morte | Tábua do IBGE (da UF) × fator pessoal | Luto; substituição pela regra (abaixo); a base fica em disputa |
| Aposentadoria | Chance crescente a partir dos 65 anos e a cada derrota; anunciada antes do prazo de registro | Um herdeiro político indicado, ou a base solta |
| Doença | Saúde abaixo de um limite: licença (o suplente assume por mais de 120 dias) ou renúncia | Vaga temporária ou definitiva |
| Derrota eleitoral | Na apuração: tenta de novo, desce de cargo, vira liderança ou sai | Rival persistente ou vaga |
| Inelegibilidade | Lei da Ficha Limpa (LC 64/1990, art. 1º, I, com a contagem de prazos da LC 219/2025): 8 anos, por condenação colegiada, contas rejeitadas, abuso de poder ou captação ilícita | Fora das urnas pelo prazo; pode apoiar outro nome |
| Cassação | Câmara ou Justiça Eleitoral (17.12, 17.14, 17.15) | Suplente, vice ou nova eleição |
| Renúncia | Para disputar outro cargo (desincompatibilização), por escândalo ou por doença | Idem |
| Escândalo | Esqueleto que veio à tona com gravidade alta | Imagem destruída; costuma levar a renúncia, cassação ou derrota |
| Carreira fora da política | Chance pequena por ano, maior depois de derrota: empresa, magistério, mídia, cargo técnico | Vira doador, liderança, dono de veículo ou comentarista: continua no mundo, em outro papel |

**Entradas.** Toda vaga e toda disputa têm de ter gente. A cada ciclo, novos nomes surgem das fontes abaixo, com pesos em `data/demography/entries.json`:
- **lideranças locais** (17.10): a mais influente de uma zona sem representante vira pré-candidata;
- **sindicatos, igrejas e empresas:** lideranças sindicais, religiosas e empresariais lançam um nome;
- **mídia:** jornalista ou comunicador conhecido;
- **equipe:** coordenador ou assessor de base de um político que saiu;
- **famílias de políticos:** filho, cônjuge ou sobrinho de quem se aposentou, morreu ou ficou inelegível, com parte do reconhecimento e da rede do antecessor e com a memória da família em relação ao jogador;
- **gerados:** quando nada acima preenche a vaga.

A origem não determina partido nem ideologia; os pesos por origem ficam dentro de 2 para 1 entre as famílias ideológicas, e o DataValidator verifica.

**Substituição pela regra real** (parâmetros em objeto de configuração com o padrão da lei; a Enciclopédia explica cada caso):

| Cargo | Regra | Base legal |
|---|---|---|
| Vereador, deputado | Assume o suplente do partido ou da federação; sem suplente e faltando mais de 15 meses, nova eleição | CF, art. 56, §§ 1º e 2º (por simetria nos estados e municípios) |
| Senador | Assume o primeiro suplente da chapa | CF, art. 46, § 3º, e art. 56 |
| Prefeito e governador, por morte ou renúncia | Assume o vice. Vagos os dois, o presidente da Casa legislativa assume interinamente e há nova eleição: direta se a dupla vacância ocorrer na primeira metade do mandato; indireta, pela Câmara ou pela Assembleia, na segunda metade | Padrão do jogo, configurável, espelhando a CF, art. 81 (decisão do autor, 17.20, item 12). A regra de cada Lei Orgânica e de cada Constituição estadual pode diferir; a Enciclopédia diz que é simplificação |
| Prefeito e governador, por decisão da Justiça Eleitoral | Indeferimento de registro, cassação de diploma ou perda de mandato: nova eleição, indireta só se a vacância ocorrer a menos de 6 meses do fim do mandato; direta nos demais casos | Código Eleitoral, art. 224, §§ 3º e 4º, com a ADI 5525 |
| Presidente | Assume o vice; vagos os dois, eleição direta em 90 dias, ou indireta pelo Congresso em 30 dias se nos dois últimos anos | CF, arts. 79 a 81 |
| Candidato que morre na campanha | O partido substitui em até 10 dias; no caso de morte, mesmo a menos de 20 dias da eleição | Lei 9.504/1997, art. 13 |

**Morte tratada com seriedade.** Texto sóbrio, sem humor, sem causa sensacionalista e sem escolha que zombe do morto. A notícia sai em todos os veículos em tom neutro, qualquer que seja a linha editorial. Há luto oficial (três dias sem sessão na Câmara; a campanha do partido para um dia), e o jogador escolhe como se manifesta: comparecer ao velório é um compromisso; atacar a memória do morto não é uma opção oferecida. Família e aliados guardam a lembrança do que o jogador fez. As variações de texto seguem o CONTENT_GUIDE e o EventLint reprova texto de morte fora da categoria e do tom definidos.

**Feed e mapa.** Aposentadoria anunciada, morte, cassação, inelegibilidade, estreia de candidato: tudo é fato e vira notícia. No mapa, o reduto de quem saiu passa a aparecer como **zona em disputa** (hachura) até alguém liderar.

**Oportunidades e cadeias.** A saída de alguém abre, conforme o caso: uma base sem dono (objetivo novo no painel), uma liderança livre para negociar, uma vaga na chapa ou na Mesa, a corrida dos outros rivais pela mesma base, um herdeiro que herda as dívidas e as mágoas do antecessor, e a chance de ser lembrado por quem ficou.

**O que o jogador decide.** Como reagir a cada saída: disputar a base, apoiar o herdeiro, atrair a equipe de quem saiu, ocupar a vaga, ou ficar de fora.

**A troca.** A base de quem saiu custa turnos e promessas, e os outros também a querem. Apoiar o herdeiro rende a gratidão da família e fortalece um futuro concorrente.

**Interage com:** Lideranças (viram candidatos; perdem o padrinho), Rivais (replanejam; herdeiros), Equipe (migra), Mídia (cobertura), Território (zona em disputa), Mandato e Prefeitura (suplente e vice assumem; secretário que sai), Plataforma e Livro (dívidas e promessas feitas a quem saiu passam ao sucessor ou se extinguem). **Princípios:** b, c, d, h, j.

**Critério de balanceamento (BalanceSim, 40 anos de jogo).** A renovação simulada fica dentro da faixa observada, medida pela mesma régua da fonte:

| Casa | Régua | Faixa observada | Fonte | Critério |
|---|---|---|---|---|
| Câmara dos Deputados | Eleitos que não foram reeleitos ÷ cadeiras (inclui ex-deputados que voltam) | De 43,86% (1998) a 61,82% (1990), nas eleições de 1990 a 2022; média de cerca de 49% | DIAP, "Raio-X das candidaturas" (edições de 2022 e de 2026), mesma tabela reproduzida até 2014 por estudo da Consultoria Legislativa da Câmara | Cada eleição entre 40% e 62%; média das 10 eleições entre 44% e 54% |
| Câmara, régua alternativa | Deputados de primeiro mandato ÷ cadeiras | De cerca de 36% a 47% | Secretaria-Geral da Mesa da Câmara (2018: 47,37%, conferido; demais anos por notícia) | Só informativo |
| Senado | Novos ÷ vagas em disputa | De 68,5% (2010) a 85,2% (2018), nas eleições de 1994 a 2026 | DIAP; Agência Senado para 2018 e 2026 | Cada eleição entre 65% e 88% |
| Câmaras Municipais | Não reeleitos ÷ eleitos | Cerca de 59% em 2024 (fonte secundária, com dados do TSE); sem série publicada encontrada | — | Só informativo, até haver série oficial (17.20) |
| Recandidatura e sucesso | Deputados que tentam a reeleição e os que conseguem | Tentam de 74% a 87%; conseguem de 51% a 71% | DIAP | Só informativo |

As séries do DIAP e da Mesa usam definições diferentes e não se misturam. O resultado de 2026 pela régua do DIAP ainda não foi publicado.

**Dados:** `data/demography/mortality.json` (importado), `data/demography/config.json` (fator pessoal, aposentadoria, saídas), `data/demography/entries.json` (origens e pesos), textos de luto em `data/events/`.

**Reaproveita:** `PersonGenerator`, datas de nascimento e envelhecimento da aparência (Fase 2), saúde e estresse do personagem, `CareerFlow`, suplentes da apuração proporcional (a ordem de votação já existe em `ElectionOutcome`), a inelegibilidade por reeleição do cenário (D-007). **Novo:** tábua, saídas, entradas, substituição.

**Nível de detalhe.** Pessoas persistidas no save (município em foco, vagas individuais do cenário, rivais persistentes) são verificadas mês a mês. Nos municípios e estados fora de foco não há pessoas (12.5): a renovação ali é só a troca estatística de cadeiras por partido a cada eleição.

### 17.17 Estatísticas reais como ponto de partida, evolução simulada

**Regra.** O mundo começa com os números oficiais mais recentes de cada município, UF e do país. A partir do primeiro dia, tudo evolui pela simulação: decisões do jogador e dos NPCs, ciclo econômico, choques e crises sorteados pelo Rng. Nenhuma série histórica é tocada como gravação; da série recente só se tira o valor inicial e a inclinação inicial.

**Fontes** (pesquisa de 16/11/2026; "conferido" = API, FTP ou arquivo oficial aberto na pesquisa; o que não foi conferido está dito):

| Item | Fonte e conjunto | Mais recente | Nível | Acesso | Lacunas e regra de substituição |
|---|---|---|---|---|---|
| População (ponto de partida) | IBGE, Estimativas da População, SIDRA 6579 | 2026 (conferido) | Município | API SIDRA, JSON | Já usada (D-033) |
| População (trajetória) | IBGE, Projeções da População, revisão 2024 | 2000 a 2070, por sexo e idade (conferido) | Brasil, regiões, UF; **não há por município** | XLSX no FTP do IBGE | Ver "População" abaixo |
| PIB municipal | IBGE, PIB dos Municípios, SIDRA 5938 (PIB e valor adicionado por setor) | 2023 (conferido) | Município | API SIDRA | Nenhuma: 5.570 municípios. PIB per capita é calculado com a população do mesmo ano |
| Emprego formal, estoque | MTE, RAIS, microdados | 2025 (arquivos de mai/2026; não confirmado se definitivos) | Vínculo, com município | FTP do MTE, 140 a 770 MB por arquivo | Agregar no importador. Município sem vínculo: zero |
| Emprego formal, fluxo | MTE, Novo CAGED, microdados | Set/2026 (conferido) | Movimentação, com município | FTP do MTE | Só saldo; serve para a inclinação inicial |
| Finanças municipais e estaduais | Tesouro Nacional, SICONFI: DCA (receita corrente, despesa por função), RGF (receita corrente líquida, despesa com pessoal em % da RCL), RREO | DCA 2025; RGF do 3º quadrimestre de 2025 (conferido) | Município, UF, União | API `apidatalake.tesouro.gov.br`, JSON, uma chamada por ente e anexo | Ente sem entrega no ano: DCA do ano anterior (até 2 anos); sem nenhuma: mediana por habitante dos municípios do mesmo porte na UF, marcada como estimativa. Numa amostra de 151 municípios, 150 tinham a DCA de 2025; a contagem completa sai na RD |
| Transferências (FPM, FPE) | Tesouro Transparente, Transferências Constitucionais para Municípios e para Estados | Out/2026 (municípios, conferido no catálogo) | Município, UF | CSV mensal; licença ODbL | Colunas a conferir na RD |
| PIB nacional | IBGE, Contas Nacionais, SIDRA 1846 e 5932 (trimestral), 6784 (anual) | 2º tri de 2026; anual 2024 (conferido) | Brasil | API SIDRA | — |
| Inflação | IBGE, IPCA, SIDRA 1737; BCB, SGS 433 | Out/2026: 0,14% no mês (conferido) | Brasil | API do SGS, JSON | — |
| Juros | BCB, SGS 432 (meta Selic) | 13,75% ao ano (conferido). A série traz datas futuras: filtrar pela data | Brasil | API do SGS | — |
| Câmbio | BCB, SGS 1 | R$ 5,6518 em 13/11/2026 (conferido) | Brasil | API do SGS | — |
| Desemprego | IBGE, PNAD Contínua trimestral, SIDRA 4099 | 2º tri de 2026: 5,4% no Brasil (conferido) | Brasil, UF, capitais; **não desce a município** | API SIDRA | Município usa a taxa da UF como contexto; o indicador municipal de emprego é o formal (RAIS) |
| Dívida pública | BCB, SGS 13762 (bruta do governo geral) e 4513 (líquida) | Set/2026: 82,95% e 70,00% do PIB (conferido) | Brasil | API do SGS | — |
| Educação | INEP, IDEB por município e rede | 2025 (conferido) | Município × rede | Planilhas em `download.inep.gov.br/ideb/resultados/` | Anos iniciais: 5.479 de 5.569 municípios têm nota. Sem nota: a edição anterior; sem nenhuma: média da rede na UF, marcada |
| Mortalidade infantil | Ministério da Saúde, SIM (óbitos de menores de 1 ano) e SINASC (nascidos vivos), por município de residência | 2024 definitivo; 2025 e 2026 preliminares (conferido) | Registro, com município | FTP do DATASUS, arquivos .dbc (exigem conversão) | Não há indicador pronto confirmado: a taxa é calculada. Em município pequeno a taxa anual oscila: usar a soma de 3 anos; abaixo de um mínimo de nascimentos (parâmetro), a taxa da região imediata, marcada |
| Leitos | Ministério da Saúde, CNES, leitos | Set/2026 (conferido) | Estabelecimento, com município | FTP do DATASUS, .dbc | Município sem hospital: zero |
| Segurança | (a) SIM, óbitos por agressão, base do Atlas da Violência; (b) MJSP, SINESP, "Dados Nacionais de Segurança Pública" | (a) 2024; (b) 2025 completo, 2026 até setembro (conferido) | Município | (a) FTP do DATASUS; (b) XLSX no gov.br | (a) tem código IBGE e é comparável no país, mas é mais antiga; (b) é mais recente, sem código IBGE (casar por nome), é registro policial declarado pelos estados e confunde zero com não informado. **Decidido (17.20): (a), soma de 3 anos; (b) só para conferência** |
| Saneamento | IBGE, Censo 2022, SIDRA 6803 a 6806 (rede de água, abastecimento, esgotamento) | 2022 (conferido) | Município (e setor, 17.5) | API SIDRA | Cobertura completa; sem tratamento de esgoto. O SINISA (Ministério das Cidades, referência 2023) tem tratamento, é autodeclarado e incompleto: só complemento |
| Proteção social | **Sem fonte pesquisada** | — | — | — | Decidido (17.20): índice do jogo, rotulado, até haver fonte oficial |

Não confirmados e fora do desenho por ora: cobertura da atenção primária (sem arquivo ou API encontrados), IDHM municipal (o último é de 2010), tabela pronta do CAGED por município. Licença: o Tesouro declara ODbL e o TSE, CC BY; IBGE, INEP, DATASUS, MJSP e MTE publicam dados abertos sem licença explícita localizada (17.20).

**População.** A D-033 projeta cada município pela taxa geométrica entre o Censo 2022 e a estimativa de 2026, limitada entre −3% e +5% ao ano. Em 40 anos, taxa constante se afasta do que o IBGE projeta para a UF. Decidido (17.20): a taxa municipal fica como distribuição relativa e, a cada ano, o jogo reescala os municípios de cada UF para que a soma acompanhe a Projeção da População 2024 daquela UF; a migração e os choques da simulação entram como desvio sobre isso.

**Indicadores da prefeitura em unidade real.** Os sete indicadores de 0 a 100 da D-031 passam a ter âncora real. O jogador vê a unidade real; a escala de jogo fica por dentro.

| Indicador da D-031 | O que o jogador vê | Fonte |
|---|---|---|
| Educação | "IDEB anos iniciais (rede municipal): 5,4" | INEP |
| Saúde | "Mortalidade infantil: 11,8 por mil nascidos vivos"; "Leitos: 1,9 por mil habitantes" | SIM, SINASC, CNES |
| Segurança | "Homicídios: 21 por 100 mil habitantes" | SIM (Ministério da Saúde) |
| Infraestrutura | "Domicílios ligados à rede de esgoto: 62%"; "à rede de água: 91%" | Censo 2022 |
| Emprego | "Empregos formais: 24 por 100 habitantes"; "Saldo no ano: +310" | RAIS, CAGED |
| Contas públicas | "Pessoal: 49,2% da receita corrente líquida"; "Receita: R$ 212 milhões" | SICONFI |
| Proteção social | Índice do jogo, rotulado como tal, até haver fonte | — |

(Os valores acima são exemplos de formato, não dados.)

- **Conversão:** cada indicador tem, em `data/indicators/definitions.json`, unidade, sentido (maior é melhor ou pior), limites plausíveis e a função que leva a unidade real à escala de 0 a 100 e de volta (linear, ou logarítmica para taxas). O valor inicial na escala de jogo é a conversão do dado importado, não mais 50 ± 8.
- **Receita:** a receita real do SICONFI substitui a receita por habitante estimada da D-031; o orçamento por área parte da despesa por função declarada.
- **Como o eleitor julga:** a aprovação reage à **variação** desde a posse e à comparação com municípios do mesmo porte na UF, não ao nível absoluto. Cidade que parte de indicador ruim não começa condenada; cidade que parte bem não vive de herança.
- **Tooltip:** todo indicador mostra fonte, ano do dado de partida e "a partir daí, simulado".

**Modelo de evolução de cada indicador** (forma única; parâmetros por indicador em `data/indicators/model.json`):

`valor(t+1) = valor(t) + velocidade × (alvo(t) − valor(t)) + choque(t)`, limitado aos limites plausíveis.

`alvo(t) = partida + tendência(t) + efeito do orçamento + efeito das políticas + efeito das obras entregues + contexto`

- **tendência(t):** inclinação inicial tirada das últimas edições da série (ex.: variação do IDEB entre 2021, 2023 e 2025), que perde força até zerar em N anos (parâmetro). É a única herança da série histórica.
- **efeito do orçamento:** gasto da área em relação à referência do próprio município (regra atual da D-031: sensibilidade por ponto percentual).
- **efeito das políticas e das obras:** as 17 políticas e as obras entregues por zona (17.15), cada uma com efeito e prazo declarados.
- **contexto:** ciclo econômico (emprego, receita), população, indicadores vizinhos (segurança piora com desemprego alto por muito tempo, por exemplo, com peso declarado).
- **choque(t):** crises e fatos sorteados pelo Rng (fábrica que fecha ou abre, enchente, epidemia), com distribuição por tipo.
- **velocidade:** hoje 3,5% por semana para todos; passa a ser por indicador (educação muda devagar; contas públicas, depressa).
- **limites plausíveis:** piso e teto por indicador, tirados da distribuição observada entre os municípios (percentis 1 e 99 do dado importado), para que 40 anos de simulação não produzam valores impossíveis.

**Economia.**
- Na R4, um ciclo simples: crescimento = tendência + componente cíclico autorregressivo + choque, partindo dos números nacionais da tabela; ele move a receita (as transferências acompanham a arrecadação nacional), o emprego e o humor do eleitor. Parâmetros em `data/economy/cycle.json`.
- Na Fase 7, o modelo de economia nacional completo: PIB, inflação, juros, câmbio, desemprego e dívida ligados entre si e às decisões do governo federal (do jogador ou de NPC), com os mesmos pontos de partida.

**O que o jogador decide.** Nada diretamente: é a base de números sobre a qual a Prefeitura (17.15) e, depois, os governos estadual e federal decidem. A troca aparece lá: mover orçamento melhora um indicador devagar e piora outro.

**Interage com:** Prefeitura (indicadores, receita, folha), Território (saneamento e renda por zona vêm da mesma fonte), Mídia (notícias citam os números com a unidade real), Plataforma (promessa mensurável: "levar o esgoto a 80% dos domicílios"), Renovação (população). **Princípios:** b, d, f, j.

**Registro.** Cada fonte entra em `data/municipios/_fontes.json` (órgão, conjunto, ano, endereço, data do download, lacunas e regra aplicada) e em `assets/CREDITS.md`, como já é feito. Arquivos gerados: `data/indicadores_reais/{UF}.json` (municípios), `data/indicadores_reais/_ufs.json`, `data/economy/start.json` (país). A importação é a parte RD da Fase R.

**Reaproveita:** `ExecutiveEngine` (alvo e velocidade dos indicadores, aprovação por grupo, mínimos legais), `PopulationProjection` (D-033), o padrão de importação e de `_fontes.json`. **Muda:** valor inicial, unidade mostrada, receita, velocidade por indicador.

### 17.18 Vagas individuais e pacote de nomes (pacote suspenso)

> **Estado (decisão do autor, 16/11/2026): o pacote de nomes não foi aprovado e está suspenso.** As vagas individuais fictícias seguem no plano (Fase 6). A leitura de pacote não é construída; o formato abaixo fica documentado só como registro, caso a decisão seja revista.

**Vagas individuais no cenário Brasil 2026.** Cada cadeira de deputado federal (513), deputado estadual e distrital (1.059), senador (81), governador (27) e presidente passa a ser um NPC **fictício**, gerado pelo `PersonGenerator`, com o partido fictício e o cargo que o espelho já registra. Cada vaga tem um id estável, que não depende de nome: `cargo.UF.ordem`, em que a ordem é a colocação entre os eleitos daquele cargo na UF (ex.: `df.SP.017`, `de.MG.003`, `sen.BA.2`, `gov.PR`, `pres`). O id, o partido fictício e a ordem são fatos estruturais, como as cadeiras por partido que o cenário já guarda.

**Pacote opcional (suspenso; não construir).** Pela proposta, se existisse um arquivo em `user://mods/nomes/`, o jogo o leria ao criar o mundo e trocaria, para cada id presente, nome, nome de urna, ano de nascimento e gênero do NPC da vaga. Formato:

```json
{
  "formato": 1,
  "cenario": "brasil_2026",
  "titulo": "texto livre mostrado na tela Novo Mundo",
  "vagas": {
    "df.SP.017": { "nome": "…", "nome_urna": "…", "nascimento": 1975, "genero": "feminino" }
  }
}
```

- Todos os campos de uma vaga são opcionais; id desconhecido é ignorado com aviso; `nome` até 60 caracteres, `nome_urna` até 24; `genero` é `masculino`, `feminino` ou `nao_binario`; `nascimento` entre 1930 e o ano que dê a idade mínima do cargo.
- A aparência continua gerada (retrato fictício); se o gênero mudar, é sorteada de novo com a mesma semente.
- A tela Novo Mundo mostra "Pacote de nomes carregado: N vagas. Conteúdo de terceiros, não faz parte do jogo." O save registra que foi criado com pacote.

**O que nunca acontece.** O jogo e o repositório não contêm nomes reais. O pipeline de `tools/` não gera o pacote, não o baixa e não documenta de onde obtê-lo. O DataValidator continua bloqueando nomes reais em `data/`, e o teste do cenário (16.4) continua exigindo que nenhum nome ou sigla real apareça nos arquivos finais. O jogo não distribui, não hospeda e não aponta para pacotes.

**Regra de neutralidade que o pacote flexibilizaria.** Hoje vale, sem exceção: "partidos, políticos, empresas, institutos e veículos de mídia são fictícios; nunca use nomes reais de políticos" (CLAUDE.md, Neutralidade; GDD 1, pilar 3; 16.4, "o que é sempre fictício"). Com o pacote, o jogo passa a aceitar que o usuário ponha nomes reais em NPCs que a simulação envolve em eventos fictícios de escândalo, doença, derrota e morte. Aprovar o pacote exige mudar o texto dessas três regras.

**Risco jurídico** (a avaliar com advogado antes de qualquer decisão; isto não é parecer):
- **Código Civil, art. 17:** o nome da pessoa não pode ser empregado em publicações ou representações que a exponham ao desprezo público, ainda que sem intenção difamatória. Um evento de escândalo sorteado sobre um NPC com nome real é esse caso.
- **Art. 18:** sem autorização, não se pode usar o nome alheio em propaganda comercial. Divulgar o jogo mostrando ou mencionando o recurso com nomes reais entra aqui.
- **Art. 20:** a divulgação de escritos e a utilização da imagem de uma pessoa podem ser proibidas, com indenização, se lhe atingirem a honra, a boa fama ou a respeitabilidade, ou se se destinarem a fins comerciais. O jogo é comercial.
- O fato de o arquivo ser feito e instalado pelo usuário reduz, mas não afasta, o risco para quem fornece o mecanismo.

**Mitigações, se a decisão for revista:** com pacote carregado, eventos das categorias escândalo, vida pessoal, doença e morte não sorteiam NPCs de vagas renomeadas (ou usam só texto institucional); capturas de divulgação nunca são feitas com pacote; o jogo não oferece nenhuma forma de compartilhar pacotes.

### 17.19 Verificação do desenho

**Cada causa do tédio tem correção.**

| Causa (AUDITORIA_FASE3) | Correção |
|---|---|
| 1. Não há decisão, só gasto | Território (17.5), Agenda com cansaço e ocasião (17.6), Verba por canal (17.8), Plataforma (17.9); critério do princípio a no BalanceSim |
| 2. O mundo é uma planilha parada | Tempo corrente (17.4), Rivais com plano (17.11), Mídia e tema em alta (17.13), Renovação (17.16), economia e choques (17.17) |
| 3. Ninguém tem rosto, interesse ou memória | Pessoa com memória (17.2), Equipe (17.7), Lideranças (17.10), Rivais (17.11), assessores, secretários e vereadores com pedido (17.14, 17.15) |
| 4. Nada fica | Livro de compromissos (17.2), promessas (17.9), dívidas e esqueletos que funcionam (17.12), obras no mapa e base que cobra (17.14, 17.15) |
| 5. A tela não mostra o jogo | Tela central (17.3), previsão e explicação (17.2), princípio j, ART_GUIDE (Interface) |

**Cada sistema cumpre pelo menos três princípios e interage com pelo menos dois outros.**

| Sistema | Princípios | Interage com |
|---|---|---|
| 17.3 Tela central | b, f, h, i, j | Todos |
| 17.4 Tempo | b, e, i | Agenda, Rivais, Mídia, Livro |
| 17.5 Território | a, b, f, g, h | Agenda, Verba, Lideranças, Rivais, Mandato, Prefeitura |
| 17.6 Agenda | a, e, f, g | Território, Equipe, Lideranças, Mídia, Rivais, Tempo |
| 17.7 Equipe | a, c, d, e, f | Agenda, Verba, Contas, Mídia, Rivais, Lideranças, Mandato |
| 17.8 Verba por canal | a, e, f, g | Território, Equipe, Contas, Rivais |
| 17.9 Plataforma | a, d, e, f | Território, Lideranças, Rivais, Mídia, Debate, Mandato |
| 17.10 Lideranças | a, b, c, d, e, h | Território, Agenda, Plataforma, Rivais, Equipe, Doadores, Mídia, Mandato, Renovação |
| 17.11 Rivais | a, b, c, d, e | Território, Verba, Lideranças, Equipe, Mídia, Plataforma, Contas, Mandato |
| 17.12 Doadores e contas | c, d, e, f | Verba, Equipe, Lideranças, Rivais, Mídia, Mandato, Prefeitura |
| 17.13 Mídia local | a, b, c, e, f | Agenda, Plataforma, Rivais, Equipe, Contas, Lideranças |
| 17.14 Mandato de vereador | a, b, c, d, e, h | Território, Agenda, Equipe, Lideranças, Plataforma, Mídia, Rivais, Prefeitura |
| 17.15 Prefeitura | a, b, c, d, e, f, h | Território, Agenda, Plataforma, Lideranças, Doadores, Mídia, Vereador, Estatísticas, Renovação |
| 17.16 Renovação | b, c, d, h, j | Lideranças, Rivais, Equipe, Mídia, Território, Mandato, Prefeitura |
| 17.17 Estatísticas reais | b, d, f, j | Prefeitura, Território, Mídia, Plataforma |

O princípio j vale para todos os sistemas pela regra de tooltip, verbete e ficha. O Debate (5.7) continua como está e passa a gravar posições assumidas (17.9) e lembranças nos rivais (17.11). O pacote de nomes (17.18) não é sistema de jogo e está suspenso.

### 17.20 Decisões do autor

Decididas pelo autor em 16/11/2026 (DECISOES, D-035). A coluna "Decisão" vale como regra; onde ela difere do que as subseções anteriores propunham, as subseções já foram corrigidas.

| # | Assunto | Decisão | Usada em |
|---|---|---|---|
| 1 | Mapa da cidade na tela central | **Aprovado:** 2D plano. O 2.5D fica para o estado, o país e a apuração | R1 |
| 2 | Zonas de todos os municípios pela Malha de Setores e pelos agregados do Censo 2022, guardadas em `data/` | **Aprovado, com teto de 30 MB** para `data/geo/zonas/`; o importador falha se passar (17.5) | R1 |
| 3 | Nome de zona onde não há bairro oficial (4.675 dos 5.570 municípios) | **Aprovado:** nome de distrito ou nome descritivo gerado, com a Ficha da zona dizendo que o nome é do jogo | R1 |
| 4 | Religião e escolaridade por zona | **Aprovado:** área de ponderação onde o município tem duas ou mais (2.576 municípios); perfil municipal nos demais | RD, R2a |
| 5 | Faixas de renda por zona | **Aprovado:** estimativa de jogo a partir da mediana do rendimento do responsável, marcada como estimativa | R2a |
| 6 | Eleitores e votação de 2024 por zona pelos locais de votação do TSE | **Aprovado,** na RD. Até lá, o eleitorado é repartido pela população de 16 anos ou mais | RD |
| 7 | Termos de uso dos dados | **O autor confere os termos antes da RD.** A R1 pode usar a Malha de Setores e os agregados do Censo 2022 citando a fonte em `_fontes.json` e em `assets/CREDITS.md` | R1, RD |
| 8 | Ritmo do tempo | **Aprovado:** 4, 2 e 1 segundo por dia na campanha; 1, 0,5 e 0,25 no mandato; ajuste no teste da R1 | R1 |
| 9 | Regras de campanha | **Decidido com correções** (abaixo) | R1, R2b, R3 |
| 10 | Limite de gastos real por município e cargo | **Aprovado:** importar a tabela do TSE de 2024 e corrigir pela regra da lei. Se a fonte bloquear, parar e informar | R3 |
| 11 | Nomes de veículos de mídia | **Aprovado:** gerador com termos neutros e lista de bloqueio manual em `data/media/blocklist.json`, validada como a dos partidos | R3 |
| 12 | Vacância de prefeito e vice, governador e vice | **Decidido com correção:** dois caminhos (abaixo e 17.16), com teste de cada um | R4, Fase 6 |
| 13 | Tábua de mortalidade | **Alterado pelo autor:** tábua por UF (IBGE), com a do Brasil como reserva (17.16) | RD |
| 14 | Renovação das Câmaras Municipais | **Aprovado:** critério só informativo, enquanto não houver série oficial | R4 |
| 15 | Pacote de nomes | **Não aprovado. Suspenso.** As vagas individuais fictícias seguem no plano (17.18) | Fase 6 |
| 16 | População | **Aprovado:** reescalar os municípios para acompanhar as Projeções da População 2024 por UF | RD |
| 17 | Fonte de homicídios | **Aprovado:** SIM, soma de 3 anos, como ponto de partida; SINESP só para conferência | RD |
| 18 | Microdados da RAIS | **Aprovado,** com os brutos só no cache, fora do Git. Se o FTP bloquear, parar e informar | RD |
| 19 | Proteção social | **Aprovado:** índice do jogo, rotulado como tal, até haver fonte oficial | R4 |
| 20 | Aprovação do governo | **Aprovado:** pela variação desde a posse e pela comparação com municípios semelhantes, não pelo nível absoluto | R4 |
| 21 | Fontes tipográficas | **Aprovado:** Inter, Oswald e Source Serif 4 (OFL) | R1 |
| 22 | Curva de mortalidade depois dos 89 anos e fator pessoal de 0,6 a 2,5 | **Aprovado** como parâmetro de jogo declarado em `data/demography/config.json` | RD |

**Decisão 9, regras de campanha.**
- **Rádio e TV:** propaganda paga é proibida (Lei 9.504/1997, art. 44). O canal "Rádio" sai da verba. Rádio e TV passam a ser **presença conquistada**: entrevistas (17.6, 17.13) e horário eleitoral gratuito, cujo tempo vem do partido (D-010).
- **Outdoor:** proibido (art. 39, § 8º). O outdoor da criação de personagem dá lugar a material permitido (adesivo, bandeira, material impresso) e a uma peça para redes (3.4).
- **Impulsionamento pago nas redes:** permitido com regras (art. 57-C). Entra como canal de verba.
- **Cabos eleitorais:** têm limite de contratação conforme o eleitorado (art. 100-A). Entra como configuração.
- **O resto é conferido item por item na R3,** com o artigo citado. Até lá, vale a marcação abaixo.

**Artigos citados nesta seção: o que foi conferido e o que é de memória.**
- Conferidos no texto oficial durante a pesquisa (planalto.gov.br): Lei 9.504, art. 13; Código Eleitoral, art. 224, §§ 3º e 4º; CF, arts. 46, § 3º, 56, 79, 80 e 81; LC 64/1990, art. 1º, I, e LC 219/2025.
- Só por resumo, sem abrir o acórdão: ADI 5525.
- **De memória, a conferir antes de virar configuração:** Lei 9.504, arts. 18-C, 22-A, § 3º, 23, §§ 1º e 2º-A, 30-A, 39, §§ 6º e 8º, 43, 44, 57-C, 77 e 100-A; a vedação de doação de empresas (ADI 4650 e Lei 13.165/2015); LC 101/2000, arts. 20, III, b, e 22; Lei 14.133/2021, art. 75, VIII. Os da Lei 9.504 e da doação são conferidos na R3; os fiscais e de licitação, na R4.

**Decisão 12, vacância no Executivo.** Dois caminhos, cada um com seu objeto de configuração e seu teste:
1. **Morte ou renúncia** de titular e vice: padrão do art. 81 da CF, configurável: nova eleição direta se a dupla vacância ocorrer na primeira metade do mandato; indireta, pela Casa legislativa, na segunda metade.
2. **Vacância por decisão da Justiça Eleitoral** (indeferimento de registro, cassação de diploma, perda de mandato): Código Eleitoral, art. 224, §§ 3º e 4º: nova eleição, indireta só se a vacância ocorrer a menos de 6 meses do fim do mandato; direta nos demais casos.

### 17.20.1 Dados novos em `data/` (resumo)

| Pasta | Conteúdo | Origem |
|---|---|---|
| `data/zonas/`, `data/geo/zonas/` | Zonas de todos os municípios e contornos | Importador (IBGE) |
| `data/indicadores_reais/`, `data/economy/start.json` | Indicadores e economia de partida | Importador (17.17) |
| `data/demography/mortality.json` | Tábua de mortalidade | Importador (IBGE) |
| `data/time.json`, `data/territory/config.json`, `data/demography/config.json`, `data/demography/entries.json`, `data/indicators/`, `data/economy/cycle.json` | Parâmetros de jogo | Escritos e balanceados |
| `data/campaign/` (`commitments`, `staff`, `channels`, `platform`, `promises`, `leaders`, `rival_profiles`, `attacks`, `finance`) | Conteúdo da campanha | Escrito |
| `data/media/` (`outlets`, `templates`, `blocklist`), `data/world/city_events.json` | Mídia e eventos da cidade | Escrito |
| `data/legislature/instruments.json`, `data/works/types.json`, `data/executive/` (`secretaries`, `procurement`, `personnel`, `agreements`) | Mandatos | Escrito |
| `data/encyclopedia/` (`glossary`, textos de "Como funciona") | Enciclopédia | Escrito |

### 17.21 Anexo: prova de imersão

Quatro semanas de jogo no desenho novo, contadas por quem joga. Nomes de pessoas, veículos e zonas são ilustrativos. Entre colchetes, o sistema de onde cada coisa vem.

#### Relato 1 — Cidade pequena, pré-campanha, semana 2 (janeiro de 2028)

Minha cidade tem 12 mil habitantes e quatro zonas. No modo "Minha força" o mapa está quase todo cinza: 9% de presença no Centro, onde moro, e 1% no resto [17.5]. A coluna da direita me dá três objetivos; o primeiro é "Ser conhecido por 15% do Centro até março" [17.2].

Deixei o tempo correr em 2x. Na terça o jogo pausou: dois nomes para coordenar a campanha [17.7]. Lúcia R., qualidade 3 a 4, R$ 350 por semana, e o vínculo dela dizia "coordenou a campanha de Daniel A. em 2024". Meu primo Téo, qualidade 2, R$ 150, lealdade 90. Daniel é o vereador mais votado da cidade e meu colega de partido. Contratei a Lúcia. A previsão avisava: "Téo vai lembrar".

Na quinta a Rádio Vale (rádio local, linha governista) abriu o noticiário com a seca na zona Rural [17.13]. O tooltip do tema "água" mostrava alta de 30% no peso entre os eleitores de lá. Eu tinha uma caminhada marcada no Centro para sábado de manhã, dia de feira [17.6]. Troquei a tarde de sábado por uma visita a Seu Anselmo, presidente da associação rural. Ele me recebeu bem e fez o pedido: a promessa pública de cascalhar os 12 km da estrada do Córrego Fundo [17.10]. Não prometi. Disse que voltava. Ganhei +1 de relação em vez de +4, e a ficha dele agora registra "Veio, ouviu e não se comprometeu".

Enquanto isso, sem eu fazer nada: o feed mostrou Daniel no culto de domingo da Vila Nova, e o marcador dele acendeu naquela zona [17.11]; o prefeito anunciou a reforma do posto de saúde; Téo passou a aparecer como "sem função" na lista de pessoas, com relação −6.

Fechei a semana com R$ 2.100, uma coordenadora que talvez ainda fale com o Daniel, e uma dúvida: a estrada custa uma promessa que eu só pago se ganhar, mas a zona Rural tem 2.300 eleitores e ninguém do meu partido foi lá.

#### Relato 2 — Metrópole, reta final, última semana de setembro

Dezesseis zonas, 41 vagas, 3% na pesquisa de segunda. O painel da chapa diz: "Seu partido faria 3 cadeiras; você é a 4ª" [17.11]. Faltam cerca de 1.900 votos para passar o Ivan, colega de chapa, forte na Zona Leste.

Segunda, 8h, pausa: o Portal Capital (portal de notícias, linha crítica) publicou que mudei de posição sobre a tarifa de ônibus em julho. É verdade; o Livro de compromissos tem as duas datas [17.9]. A fonte é a campanha da Renata, do outro partido. Prazo de 48 horas [17.13]. Opções: responder no próprio portal (teste de Oratória, 55%), pedir direito de resposta (meu advogado tem qualidade 4: 70%, sai na quinta), contra-atacar com a doação mal explicada dela, ou calar. Pedi o direito de resposta e gravei um vídeo para as redes sobre o tema. Minha social media avisou do risco: 6% de viral negativo.

A barra do limite de gastos está em 91% [17.12]. O tesoureiro quer parar. A coordenadora quer os últimos R$ 40 mil em cabos na Zona Sul, onde a previsão dá +0,4 ponto com faixa de +0,1 a +0,8, porque dois rivais já gastam lá e meu canal Rua rende 58% [17.8]. Pus R$ 25 mil nos cabos da Zona Norte, onde só eu estou, e cortei o carro de som.

Quarta choveu. O comício do Jardim Aurora caiu de "cheio" para "meia praça" na previsão [17.6]; mantive, e o resultado veio dentro da faixa, com a explicação: chuva −35%, Dona Marlene no palanque +30%. Na volta, o trânsito tirou 25% da reunião da noite na Zona Oeste.

Sozinho, o mundo fez o seguinte: o pastor Elias, que estava comigo desde junho, apareceu no feed ao lado do Ivan [17.10], e fui ver a ficha dele: seis semanas sem visita minha. A candidata a prefeita do meu partido me ofereceu dobradinha nos santinhos, e o preço era subir no palanque dela na sexta, mesmo turno da entrevista que eu tinha marcada na rádio.

Fui ao palanque. Na quinta o direito de resposta saiu. Na pesquisa de sábado eu tinha 3,4%, ainda em quarto.

#### Relato 3 — Prefeitura, segundo ano, em crise (março de 2030)

Abri o jogo no modo "Obras". A drenagem da Baixada está parada na etapa de licitação há 11 semanas: impugnada por uma empresa perdedora [17.15]. Na terça choveu 140 mm e a Baixada alagou. O jogo pausou com o mapa pulsando naquela zona: 310 famílias fora de casa. A decisão não tinha opção boa. Decretar emergência e contratar sem licitação resolve em duas semanas, e a previsão avisava que a única empresa disponível é do Valdir, que doou para a minha campanha [17.12]: "a imprensa vai notar". Esperar o julgamento da impugnação leva de 4 a 9 semanas. Usar a equipe própria de obras atrasa a creche da Vila Nova, que é promessa minha com prazo em junho.

Meu secretário de Obras, Ramiro, veio pela cota do partido aliado: competência 2, e a ficha diz que ele quer ser candidato a vereador. Defendeu a emergência com a empresa do Valdir. A secretária de Finanças lembrou que a folha está em 52,1% da receita corrente líquida, acima do limite prudencial, e que na data-base de abril o sindicato vai pedir 8%.

Decretei a emergência e chamei a equipe própria; a creche escorregou para setembro. No Livro, a promessa ficou amarela, e Dona Cida, da associação da Vila Nova, ganhou a lembrança "Creche adiada, mar/2030, −2".

Para abrir o crédito suplementar precisei da Câmara. Contagem: 6 sim, 5 não, 2 indecisos [17.15]. Marta quer a pavimentação da rua da escola no reduto dela. Josué quer um cargo para o cunhado. Atendi a Marta: é obra, aparece no mapa e eu inauguro. O Josué votou contra e avisou a Gazeta (jornal, linha crítica), que na sexta deu a manchete "Prefeitura escolhe a quem atender".

Sem eu mexer: a aprovação caiu de 44% para 39%, e o mapa mostrou onde: Baixada 21%, Vila Nova 35%. A Rádio Vale, governista, falou do socorro; a Gazeta, da drenagem que não saiu. O governador liberou um convênio de R$ 1,8 milhão para drenagem, com contrapartida de 10% e prazo de 12 meses. E Ramiro deu entrevista por conta própria dizendo que avisou.

Objetivo novo no painel: "Entregar a drenagem da Baixada antes das chuvas de novembro".

#### Relato 4 — A mesma campanha do relato 1, semana 20 (maio de 2028)

O mapa não é mais cinza. Lidero na zona Rural com 34% de presença, tenho 27% no Centro e 8% na Vila Nova, que está pintada na cor do partido do Daniel.

Prometi a estrada em fevereiro. Hoje Seu Anselmo é aliado, com relação 62, e me cobra outra coisa: quer que eu diga em público, na Rádio Vale, que sou contra o fechamento da escola rural. A matriz da plataforma mostra o custo: +2,1 pontos na zona Rural, −0,8 no Centro, onde a prefeitura quer concentrar as turmas, e o cacique do meu partido, que apoia o prefeito, não vai gostar [17.9]. Em janeiro eu nem tinha posição conhecida para arriscar; agora 40% da cidade sabe o que eu penso sobre educação.

Marquei uma caminhada no Centro por hábito e a previsão me parou: seria a quarta em três semanas, com fator de cansaço de 39% [17.6]. Na semana 2 a mesma caminhada era o melhor uso do sábado. Hoje o melhor uso é a entrevista.

A Lúcia e o tesoureiro brigaram por causa de um gasto sem nota. O jogo pediu que eu escolhesse um lado [17.7]. Fiquei com o tesoureiro; a lealdade dela caiu para 48, e eu sei para quem ela trabalhou.

No dia 15 abriu o financiamento coletivo: R$ 1.300 na primeira semana [17.12]. No mesmo dia veio um empreiteiro com R$ 8 mil "por fora". Recusei. Com R$ 2.100 em janeiro eu teria pensado mais.

E a notícia da semana não era sobre mim. Seu Olavo, vereador há cinco mandatos, 74 anos, anunciou na Gazeta que não disputa a reeleição [17.16]. Ele fazia 400 votos na Vila Nova. O feed de sábado já mostrava o Daniel na casa dele. O coordenador de Olavo, um rapaz chamado Bruno, virou pré-candidato pelo mesmo partido. Meu painel trocou um objetivo: saiu "Ser conhecido por 15% do Centro", cumprido em março, e entrou "Conquistar ao menos uma liderança da Vila Nova antes da convenção".

**Por que a semana 20 não é a semana 2.** Na semana 2 as decisões eram de abertura: quem contratar, onde aparecer primeiro, prometer ou não. Na semana 20 são de manutenção e de disputa: honrar ou ampliar uma aliança, pagar o custo de uma posição pública, administrar uma equipe rachada, reagir a uma vaga que se abriu. Seis coisas mudaram o que é a melhor jogada: o mapa (presença desigual por zona), o cansaço (a caminhada no Centro rende 39%), as pessoas (Anselmo, Lúcia, Téo e Daniel têm lembranças que não existiam), o Livro (uma promessa feita e uma posição conhecida), o calendário (financiamento coletivo aberto, convenção chegando) e o mundo (um vereador saiu e a base dele está solta). Nenhuma decisão do relato 4 estava disponível no relato 1, e a ação que era a melhor no relato 1 é agora a que o jogo desaconselha.

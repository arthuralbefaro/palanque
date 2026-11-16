# PLANALTO — Guia de arte

> Este guia tem três partes. A seção **Interface** está escrita abaixo e vale a partir da Fase R. As seções **Retratos** (camadas, ancoragem, nomenclatura; GDD 7.4) e **3D** (escala, polígonos, materiais tingíveis; GDD 13.3) ainda não foram escritas e entram quando a arte final for encomendada (Fase 9).

## Interface

Referência aprovada: `docs/referencias/tela_central.png` (rascunho; mapa e números ilustrativos). O desenho de cada painel está no GDD, seção 17.3.

### 1. Ideia geral

A tela é uma mesa de trabalho de campanha à noite: fundo escuro, o mapa da cidade aceso no centro, papel de jornal à direita, a operação à esquerda. A cor aparece só onde há informação: no mapa, nos partidos, nos alertas. Os painéis são neutros.

Três regras que decidem os casos de dúvida:
1. **O mapa é o protagonista.** Nada cobre mais de 40% dele, exceto a apuração e a urna.
2. **Cor é dado.** Se um elemento é colorido, a cor significa algo que a legenda explica.
3. **Um destaque por vez.** Em cada momento, só um elemento da tela usa a cor de atenção.

### 2. Paleta

Todas as cores ficam no tema (`src/UI/Theme/`), com estes nomes. Nenhuma tela escreve cor em código.

**Superfícies (do fundo para a frente)**
| Nome | Hex | Uso |
|---|---|---|
| `fundo` | `#0E1116` | Fundo da janela e do mapa |
| `painel` | `#161A21` | Barra superior, trilho, painéis laterais |
| `cartao` | `#1E232C` | Cartões dentro de painel, linhas de lista |
| `cartao_alto` | `#272D38` | Cartão sob o mouse, cartão de decisão |
| `linha` | `#2F3642` | Divisórias e contornos de 1 px |

**Texto**
| Nome | Hex | Contraste sobre `painel` | Uso |
|---|---|---|---|
| `texto` | `#ECEFF4` | 15,1:1 | Valores e títulos |
| `texto_2` | `#AAB3C0` | 8,2:1 | Rótulos e corpo secundário |
| `texto_3` | `#8E99A8` | 6,0:1 | Fonte e data da notícia, dicas |

**Significado**
| Nome | Hex | Uso |
|---|---|---|
| `acao` | `#4C8DF6` | Controle selecionado, barra de verba, foco do teclado |
| `positivo` | `#3FB68B` | Variação a favor do jogador |
| `negativo` | `#E5645A` | Variação contra o jogador, risco alto |
| `atencao` | `#E9B44C` | Decisão pendente, prazo perto, jogo pausado por acontecimento |
| `neutro_dado` | `#8A94A3` | Zona sem dado, indeciso, branco e nulo |

Contraste calculado pela fórmula da WCAG 2.1. As três cores de texto têm pelo menos 4,5:1 sobre `painel`, `cartao` e `cartao_alto` (o pior caso é `texto_3` sobre `cartao_alto`, 4,8:1). As cores de significado, quando usadas em texto, ficam só sobre `painel` e `cartao` (pior caso: `negativo` sobre `cartao`, 4,7:1) e vêm sempre com sinal ou ícone.

`positivo` e `negativo` descrevem o efeito para o jogador, nunca um julgamento. Aceitar uma doação "por fora" que rende dinheiro mostra `positivo` no dinheiro e `atencao` no risco; a tela não usa cor para dizer que algo é certo ou errado.

**Mapa**
| Modo | Escala | Regra |
|---|---|---|
| Minha força | Sequencial de `#1B2A2A` a `#2FD0A5`, 5 faixas | Faixas por quintil da própria cidade; legenda com os valores |
| Partido líder | Cor primária do partido líder na zona; opacidade de 45% a 100% conforme a margem sobre o segundo | A cor vem do save (partidos fictícios). Zona empatada na margem de erro fica hachurada |
| Perfil (renda, idade, urbano/rural) | Sequencial de `#2A2620` a `#E0A458`, 5 faixas | Nunca a mesma escala de "Minha força" |
| Presença de rival | Cor primária do partido do rival | Só um rival por vez |

Zona selecionada: contorno de 3 px em `texto`. Zona sob o mouse: contorno de 2 px em `texto_2`. Zona com acontecimento: pulso em `atencao`.

**Cores de partido** são dados do mundo e podem ser qualquer uma. Por isso nenhum significado da interface usa matiz: positivo e negativo têm sempre sinal (+ ou −) e seta; cada partido aparece com sigla ou ícone ao lado da cor (modo daltônico, GDD 8.5).

### 3. Tipografia

Três famílias, todas com licença SIL Open Font License 1.1, guardadas em `assets/fonts/` e creditadas em `assets/CREDITS.md`.

| Papel | Fonte | Pesos | Onde |
|---|---|---|---|
| Interface | **Inter** | 400, 500, 600 | Rótulos, botões, valores, tabelas. Números com a opção tabular (`tnum`) para as colunas não dançarem |
| Manchete | **Oswald** | 500, 600 | Título de notícia, título de decisão, placar da apuração, data na barra superior |
| Leitura | **Source Serif 4** | 400, 400 itálico | Texto de evento, fala de NPC, corpo de notícia aberta |

Tamanhos em px lógicos (tela base 1920×1080; ver seção 9 para a conversão):

| Estilo | Fonte e tamanho | Altura de linha |
|---|---|---|
| `titulo_decisao` | Oswald 600, 32 | 38 |
| `manchete` | Oswald 500, 24 | 28 |
| `titulo_painel` | Inter 600, 22 | 28 |
| `valor` | Inter 600, 22 tabular | 28 |
| `corpo` | Inter 400, 20 | 28 |
| `leitura` | Source Serif 4 400, 22 | 32 |
| `rotulo` | Inter 500, 18 | 24 |
| `legenda` | Inter 400, 18 | 24 |

**Não existe texto menor que 18 px lógicos.** Caixa alta só em siglas de partido e no placar da apuração. Sem texto justificado. Valores em reais sempre com "R$" e separador de milhar ("R$ 22.860"); percentuais com uma casa quando vêm de pesquisa ("6,1%") e sem casa quando são estado ("17%").

### 4. Grade de painéis

Unidade: 8 px lógicos. Tela base de 1920×1080.

| Região | Posição e tamanho | Conteúdo |
|---|---|---|
| Barra superior | 1920 × 56, topo | Data, pausa, 1x/2x/4x, fase e contagem regressiva; à direita: dinheiro, reconhecimento, imagem, estresse, menu |
| Trilho | 64 × 1024, esquerda | 7 ícones de área (48 × 48, área de clique 64 × 56), o ativo com barra de 3 px em `acao` |
| Painel da área | 400 × 1024, ao lado do trilho | Conteúdo da área escolhida. Rola na vertical; nunca na horizontal |
| Mapa | 1056 × 1024, centro | Mapa, seletor de modo (em cima, à esquerda), legenda (embaixo, à esquerda), faixa de decisão (embaixo, centro) |
| Coluna direita | 400 × 1024, direita | Notícias (flexível, mínimo 360), Objetivos (até 3, 176), Intenção de voto (5 linhas, 232) |

- Margem interna dos painéis: 16. Espaço entre cartões: 8. Raio dos cantos: 8 nos cartões, 12 no cartão de decisão, 0 nas regiões coladas à borda da janela.
- Divisão entre regiões: 1 px em `linha`, sem sombra.
- **Faixa de decisão:** 720 × 72, a 16 px da base do mapa. Ícone de `atencao`, uma frase, botão "Decidir".
- **Cartão de decisão:** largura 720, altura até 640, centrado sobre o mapa. O mapa escurece 50% e perde 30% de saturação; os painéis laterais continuam visíveis e legíveis, sem aceitar cliques que mudem o estado. Nunca tela cheia.
- **Ficha de pessoa ou de zona:** abre no lugar do painel da área (400 de largura), com seta de voltar. Não é janela flutuante.
- Tooltip: largura máxima de 360, `cartao_alto`, aparece depois de 300 ms, nunca sai da janela.

Em janela mais larga que 16:9, o mapa cresce e os painéis mantêm 400. Em janela mais alta, a coluna direita dá a altura extra às Notícias.

### 5. Ícones

- Desenho próprio em SVG, grade de 24 × 24, traço de 2 px, pontas e cantos arredondados, sem preenchimento, cor herdada do tema. Guardados em `assets/icons/ui/`.
- Pacote de terceiros só com licença CC0 ou CC BY (GDD 11), com crédito. Qualquer outra licença é decisão do autor.
- Todo ícone sem rótulo ao lado tem tooltip com o nome.
- Trilho, de cima para baixo: megafone (Campanha ou Mandato), pessoas (Equipe, Lideranças e Rivais), calendário (Agenda), barras (Pesquisas e Território), cédula (Finanças), jornal (Mídia), prédio (Partido e Câmara).
- Tipos de liderança e de canal têm ícone próprio e genérico (templo é uma casa com frontão, sem símbolo de religião específica; sindicato é uma engrenagem; associação de moradores é um grupo de casas; empresarial é uma maleta; rural é uma espiga).
- Edifícios e símbolos oficiais sempre estilizados, sem brasões nem réplicas (regra de neutralidade).

### 6. Estados de controle

| Estado | Botão primário | Botão secundário | Linha de lista |
|---|---|---|---|
| Normal | Fundo `acao`, texto `fundo` (contraste 5,8:1) | Fundo `cartao`, contorno `linha`, texto `texto` | Fundo `cartao` |
| Mouse em cima | Fundo 8% mais claro | Fundo `cartao_alto` | Fundo `cartao_alto` |
| Pressionado | Fundo 8% mais escuro, desce 1 px | Igual, desce 1 px | — |
| Foco do teclado | Contorno de 2 px em `texto`, afastado 2 px | Igual | Igual |
| Selecionado | — | Fundo `acao` a 20%, contorno `acao` | Barra de 3 px em `acao` à esquerda |
| Desativado | Opacidade de 40%; **tooltip obrigatório com o motivo** | Igual | Igual |
| Perigoso (demitir, romper, renunciar) | Contorno `negativo`, texto `negativo`; pede confirmação | — | — |

- Altura mínima de qualquer controle clicável: 44. Largura mínima de botão: 96.
- Botão desativado sem motivo visível é defeito.
- Controles de tempo: pausa e três velocidades são um grupo de escolha única; o ativo usa o estado "selecionado". Pausa automática troca o ícone de pausa para `atencao` até o jogador retomar.

### 7. Densidade de informação

- **Barra superior:** no máximo 5 números. Os demais ficam no painel da área.
- **Painel da área:** no máximo 3 blocos sem rolar (ex.: Equipe, Verba, Agenda). Cada bloco tem título, até 5 linhas e "ver tudo".
- **Linha de lista:** uma ideia por linha: nome à esquerda, um valor à direita, no máximo um ícone de estado. Segundo valor só em tooltip ou na ficha.
- **Notícias:** 3 a 5 itens visíveis; cada item tem veículo e tempo (`legenda`, `texto_3`) e até 3 linhas de manchete. O resto rola.
- **Intenção de voto:** o jogador, os dois acima e os dois abaixo dele; nunca a lista inteira. A fonte ("interna", nome do instituto, data) vem no título.
- **Mapa:** no máximo 12 rótulos de zona visíveis; os demais aparecem ao aproximar. Marcadores (compromisso, rival, obra, liderança) agrupam quando são mais de 3 na mesma zona.
- **Cartão de decisão:** título, até 60 palavras de texto, 2 a 4 escolhas. Cada escolha mostra a previsão em até 2 linhas.
- **Todo número tem origem:** passar o mouse mostra a conta ("17% = média das zonas pela população: Centro 31%, Vila Nova 22%, Rural 4%…") e a data do dado. Número estimado mostra faixa ("+1,5 a +3").

Teste dos 10 segundos (princípio i): ao abrir a tela, o olho encontra, nesta ordem, a data e a fase (barra), o que pede atenção (faixa de decisão ou pulso no mapa) e o que dá para fazer (painel da área).

### 8. Animações de feedback

Durações curtas; nada bloqueia o clique seguinte. A opção "Reduzir animações" troca movimento por troca instantânea e mantém as cores.

| Momento | Animação | Duração |
|---|---|---|
| Mouse sobre controle | Troca de fundo | 80 ms |
| Clique em botão | Desce 1 px e volta | 100 ms |
| Troca de área no trilho | Painel desliza 16 px e aparece | 160 ms |
| Número muda | Conta do valor antigo ao novo; pisca em `positivo` ou `negativo` | 400 ms |
| Efeito numa zona | A zona pulsa uma vez e solta uma etiqueta ("+2,1") que sobe 24 px e some | 900 ms |
| Notícia nova | Entra por cima, as outras descem; barra de 3 px em `atencao` por 2 s | 200 ms |
| Dia passa | A data troca com um corte seco; marcador do dia anda na agenda | 120 ms |
| Pausa automática | Grupo de tempo pisca duas vezes em `atencao`; a faixa de decisão sobe da base do mapa | 250 ms |
| Abrir decisão | Mapa escurece; cartão cresce de 96% a 100% | 180 ms |
| Resultado de decisão | O cartão vira a lista "O que mudou", linha por linha | 120 ms por linha |
| Rival age numa zona | Marcador do rival aparece e pulsa na cor do partido | 600 ms |
| Mudança de modo do mapa | Cores fazem transição cruzada | 250 ms |

Som: cada linha da tabela tem um som curto próprio (GDD 8.3); volume separado nas opções.

### 9. Nenhum texto ou controle cortado em 1366×768 e 1920×1080

O projeto usa tela base de 1920×1080 com `stretch = canvas_items` e `aspect = expand`: o desenho é feito uma vez, em px lógicos, e o Godot reduz ou amplia tudo junto. Em 1366×768 o fator é 0,711: um texto de 18 px lógicos aparece com 12,8 px reais e um painel de 400 aparece com 284. Daí as regras:

1. **Tamanho mínimo:** texto de 18 px lógicos, alvo de clique de 44 px lógicos (31 px reais em 1366×768).
2. **Largura fixa só nos painéis laterais (400) e no trilho (64).** Dentro deles, todo conteúdo usa contêineres que se ajustam; nenhuma posição absoluta.
3. **Todo rótulo declara o que faz quando não cabe:** quebra em até N linhas (manchete: 3; nome de pessoa: 2; item de agenda: 2) ou corta com reticências **e** mostra o texto inteiro no tooltip. Números, valores em reais e percentuais nunca são cortados: o rótulo ao lado cede.
4. **Folga para o texto:** todo campo é dimensionado para o maior texto possível em PT-BR mais 30%. Limites de conteúdo, verificados pelo DataValidator: manchete até 70 caracteres; nome de ação até 28; rótulo de escolha até 40; nome de urna até 24; nome de zona até 22; nome de veículo até 20.
5. **Listas rolam, a tela não.** Nenhuma região rola na horizontal. A barra de rolagem vertical aparece só quando necessária e fica dentro da margem do painel.
6. **Escala de interface (opções):** 100%, 115% e 130%. Em 130% a coluna direita passa a alternar Notícias, Objetivos e Intenção em abas, e o painel da área mantém 400 px lógicos. O mapa nunca fica com menos de 640 px lógicos de largura.
7. **Janela menor que 1280×720 não é suportada:** o jogo avisa e oferece tela cheia.
8. **Verificação automática** (entra na R1, junto ao teste de fumaça da interface): para cada tela e cada área do trilho, em 1366×768, 1920×1080, 1280×800 e 2560×1080, com escala de 100% e de 130%, o teste percorre todos os controles visíveis e reprova se (a) o retângulo de algum controle sai do retângulo do pai ou da janela; (b) o texto de algum rótulo sem quebra nem reticências mede mais que a largura do rótulo; (c) algum rótulo com reticências não tem tooltip; (d) algum controle clicável mede menos de 44 px lógicos; (e) algum texto usa tamanho menor que 18. O teste roda com os textos mais longos de `data/` (o maior nome de município, de partido, de pessoa gerada e a maior manchete).
9. **Verificação pelo autor:** cada roteiro de teste da Fase R começa abrindo o jogo em 1366×768 e em 1920×1080 e pede captura de qualquer texto cortado.

## Redefinição visual (parte RV) — referências, ainda como proposta

A parte RV troca o visual de todas as telas (DECISOES D-041, adendos; as decisões do autor entram como D-043 e D-044). Até o autor aprovar a reescrita deste guia, o resto dele continua valendo. Referências guardadas no repositório:
- `docs/referencias/rv_tela_central.html`: a tela central sobre o mapa da cidade, com o mundo ocupando a tela inteira, janelas semitransparentes flutuando sobre ele e a interface nas bordas.
- `docs/referencias/rv_cenas.html`: quatro cenas das telas fora do jogo, trocadas pelos botões do topo ou pelas teclas 1 a 4:
  1. **Menu:** o comitê de campanha à noite, com o menu sobre a cena.
  2. **Criação de personagem:** um estúdio de foto, com o santinho impresso ao lado.
  3. **Urna:** a seção eleitoral, com mesários e fila.
  4. **Apuração:** uma transmissão de TV, com a Câmara se enchendo e o comitê comemorando no canto.
- Os dois arquivos foram desenhados pelo autor com um consultor. Valem como referência de **composição e de conteúdo** de cada tela, **não de estilo**: o estilo final é 3D estilizado de formas suaves. O código deles não entra no jogo.
- Referência de interface: Geopolitical Simulator. Referências de estilo dos personagens e cenários: Two Point Hospital, Overcooked, Planet Coaster.

### Cabeça substituível (contrato técnico, proposta)

O protótipo da RV usa uma cabeça provisória, montada de base livre CC0 (DECISOES D-045). Depois ela é trocada pela do artista sem mudar código. As duas seguem este contrato, o mesmo de `docs/BRIEFING_ARTISTA_3D.md`.

- **Arquivos:** glTF 2.0 binário, um por peça: `cabeca_<id>.glb`, `cabelo_<id>.glb`, `barba_<id>.glb`.
- **Eixos e escala:** Y para cima, frente para +Z, 1 unidade = 1 m. Corpo de referência de 1,60 m e cabeça de cerca de 0,30 m, do queixo ao topo do crânio.
- **Origem:** no nó `encaixe_pescoco`, sem rotação, no centro da base do pescoço (onde ele encontra os ombros), preso ao osso da cabeça do corpo. No corpo de referência de 1,60 m: encaixe a 1,22 m do chão, queixo a cerca de 1,30 m.
- **Borda do pescoço:** aberta, num plano horizontal 3 cm abaixo do encaixe.
  - Anel de 16 vértices, com 5,5 cm de raio.
  - O primeiro vértice fica na frente (+Z); os demais seguem no sentido anti-horário visto de cima.
  - Normais suaves, perpendiculares ao anel.
  - O corpo base termina num anel idêntico, no mesmo plano, e a gola cobre a emenda.
  - Se o corpo base do protótipo tiver outras medidas, este guia é atualizado com as dele antes do teste do artista.
- **Pontos de troca** (nós vazios, filhos da raiz):
  - `encaixe_cabelo`;
  - `encaixe_oculos`;
  - `encaixe_brinco_e` e `encaixe_brinco_d`.
  Cabelo, barba, óculos e brincos se prendem por eles, rígidos e sem pesos de pele.
- **Malhas:**
  - `cabeca`, com as sobrancelhas, ou `sobrancelhas` à parte;
  - `olho_e` e `olho_d`, com origem no centro de rotação;
  - `dentes` e `lingua`;
  - `cabelo` e `barba`, em arquivos próprios.
- **Expressões:** shape keys com nomes exatos. Peso 1 é a expressão inteira, e a neutra é a malha sem shape key. São as 8 expressões do GDD 3.6:
  - `expr_sorriso`, `expr_sorriso_largo`, `expr_preocupado`, `expr_bravo`, `expr_triste`, `expr_surpreso` e `expr_cansado`;
  - mais `piscar`, que se soma às demais.
  A barba e as sobrancelhas separadas levam as shape keys da região que cobrem, com os mesmos nomes. O código pede a expressão pelo nome; a cabeça declara num metadado (`modo_expressao`) como a realiza:
  - `shape_keys`: por shape key com o nome, como acima. É o modo da cabeça do artista e da cabeça A do teste A/B.
  - `pecas_do_rosto`: trocando peças ou a textura do rosto nos nós filhos `rosto_olhos`, `rosto_sobrancelhas` e `rosto_boca`, com uma variante por nome de expressão. É o modo da cabeça B do teste A/B.
  O resto do personagem não sabe qual modo a cabeça usa: trocar uma cabeça por outra só mexe no nó da cabeça.
- **Materiais tingíveis** (texturas em tons de cinza; o jogo multiplica pela cor da Appearance):
  - `pele` e `labios` (este derivado da pele);
  - `sobrancelha`, `cabelo` e `barba`, com a cor do cabelo. O grisalho vem do canal R das texturas `*_mascara`;
  - `iris`.
  Não tingíveis: `esclera`, `dentes` e `lingua`. Rugas e marcas ficam em `pele_detalhe`, com a intensidade dada pela idade.
- **Orçamento:** cabeça com olhos, dentes e língua até 8.000 triângulos; cabelo até 6.000; barba até 2.000. Texturas PNG até 2048 px, UV sem sobreposição.
- **Teste do artista:** neutra, `expr_sorriso` e `expr_bravo` (D-045, adendo). O protótipo usa as mesmas três.
- **Multidão e LOD:**
  - Pessoas com nome (retrato, cartão, plano próximo) usam a cabeça deste contrato. Em plano médio, o LOD automático do Godot reduz a malha; o artista não entrega LOD.
  - A multidão e os figurantes (plateia, fila da urna, comício, posse) usam corpos e cabeças de base CC0 (KayKit, Human Base Meshes), com os mesmos nomes de material tingível, para seguirem o mesmo sorteio de tons da Appearance.
  - Cada figurante tem até 3.000 triângulos no total, sem shape keys de rosto.
  - Nenhum figurante usa a cabeça do artista, para o custo da multidão não depender dela.
- **Cena de teste de cabeça** (faz parte do protótipo): carrega qualquer `cabeca_*.glb` e confere nomes, materiais, shape keys e orçamento. Aplica as 8 expressões, 6 tons de pele e 2 cores de cabelo, e grava retratos de 96 px e 512 px. Toda cabeça nova passa por ela antes de ser aceita.

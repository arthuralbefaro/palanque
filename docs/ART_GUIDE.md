# PLANALTO — Guia de arte

> **Proposta para a parte RV, a aprovar pelo autor.** A Parte 1 é a proposta de guia para a redefinição visual (DECISOES D-043, D-044 e D-055), escrita com o que o protótipo da RV mostrou (branch `rv-prototipo`, fora da main até a RL; capturas e prancha em `docs/referencias/rv/` nesse branch).
> **Até o autor aprovar, vale o guia atual**, reproduzido sem mudança no **Anexo A** (Interface). Onde a Parte 1 e o Anexo A divergirem, vale o Anexo A. Os retratos 2D seguem o GDD 7.4 até a troca.
> Valores marcados "do protótipo" foram usados nele e ficam como ponto de partida; os marcados **a aprovar** dependem do autor. A lista do que falta decidir está no fim da Parte 1 (seção 14) e na PENDENCIAS (P-39 e P-40).

## Parte 1 — Proposta para a parte RV

### 1. Ideia geral

O jogador está na cidade real, não diante de uma planilha. O mapa da cidade ocupa a tela inteira e mostra o lugar como ele é: ruas com nome, quarteirões, rios, estradas, vilas. As zonas e os modos do jogo ficam por cima, em cor translúcida. A interface flutua sobre o mapa em janelas semitransparentes e fica nas bordas. Gente aparece em 3D estilizado de formas suaves: no retrato, no menu, nas cenas e nas posses.

Referências:
- **Composição e conteúdo** (não estilo; o código delas não entra no jogo): `docs/referencias/rv_tela_central.html` (a tela central sobre o mapa, com as janelas e a interface nas bordas) e `docs/referencias/rv_cenas.html` (quatro cenas fora do jogo, trocadas pelos botões do topo ou pelas teclas 1 a 4: **menu** no comitê de campanha à noite; **criação de personagem** num estúdio de foto, com o santinho impresso ao lado; **urna** na seção eleitoral, com mesários e fila; **apuração** numa transmissão de TV, com a Câmara se enchendo e o comitê comemorando no canto). Os dois arquivos foram desenhados pelo autor com um consultor.
- **Interface e mapa:** Geopolitical Simulator.
- **Estilo dos personagens e cenários:** Two Point Hospital, Overcooked, Planet Coaster.

Ordem em que cada parte desta proposta entra no jogo (D-055): a pilha de janelas na **RL**; mapa de base, janelas, bordas, luz, menu como cena, estilo final e nome "Palanque" na **RV-A**; mundo vivo na **RV-C**; cenas na **RV-B**.

### 2. O que não muda

Estas regras do Anexo A continuam valendo em toda a RV, sem exceção:
1. **Texto mínimo de 18 px lógicos** (tela base de 1920×1080), alvo de clique de 44 px lógicos, nada cortado em 1366×768, 1920×1080 e 2560×1080 (Anexo A, seção 9).
2. **Um destaque por vez:** só um elemento da tela usa a cor de atenção em cada momento.
3. **Todo número explicado:** passar o mouse mostra a conta e a data do dado; número estimado mostra faixa.
4. **Cor é dado:** se algo é colorido, a cor significa algo que a legenda explica. A luz do dia e da noite e os enfeites do mundo vivo nunca usam as cores de dado.
5. Tooltip em três partes em todo controle; controle bloqueado diz por quê e o que falta; termo do glossário com link (GDD 17.1, princípio j).
6. No máximo 5 números na barra de cima; no máximo 12 nomes de zona visíveis na cidade inteira.
7. Edifícios, urnas e símbolos oficiais só estilizados, sem réplicas nem brasões (regra de neutralidade).

### 3. Cor e tipografia

- **Fontes:** as mesmas três, aprovadas pelo autor (GDD 17.20, item 21): Inter (interface), Oswald (manchete e títulos) e Source Serif 4 (leitura), com os tamanhos do Anexo A, seção 3. Caixa alta continua só em siglas de partido, no placar da apuração e, **a aprovar**, no nome do jogo no menu ("PALANQUE").
- **Paleta da interface:** a do Anexo A, seção 2 (superfícies, texto, significado e escalas dos modos do mapa), sem mudança de nome nem de valor.
- **Janela flutuante** (do protótipo): fundo `painel` com 86% de opacidade; contorno de 1 px em branco a 9%; raio de 12. O contraste do texto é medido sobre o pior fundo que a janela pode ter por trás (o mapa mais claro do dia), e não só sobre `painel`.
- **Mapa de base:** paleta própria, com nomes no tema, em dois jogos de valores, noite e dia (seção 6). Valores de noite do protótipo:

| Nome | Hex | Uso |
|---|---|---|
| `mapa_fundo` | `#0A0D12` | Fora do município |
| `mapa_chao` | `#1A2620` | Chão do município (área rural) |
| `mapa_edificada` | `#22292A` | Área densamente edificada (BC250) |
| `mapa_rua` | `#565D68` | Mancha urbana (o espaço entre as quadras, que se lê como rua) |
| `mapa_quadra` | `#2A3038` | Quadras |
| `mapa_face` | `#3A414C` | Faces de quadra, só de perto |
| `mapa_agua` | `#2C5A86` | Rios e massas d'água |
| `mapa_rodovia` | `#C9A45A` | Rodovia pavimentada |
| `mapa_terra` | `#7E7058` | Estrada de terra |
| `mapa_ferrovia` | `#8A8F98` | Ferrovia |
| `mapa_borda_municipio` | `#E8EDF3` a 85% | Borda do município |
| `mapa_borda_zona` | `#ECEFF4` a 55% | Fronteira de zona |

Rótulos do mapa (do protótipo): zona `#F3F5F8`; rua `#C9CED6`; água `#8EC0EA`; localidade `#E9DDB8`; todos com contorno de `#0A0D12` a 90% para ler sobre qualquer camada.

### 4. Mapa de base (RV-A)

**Camadas, de baixo para cima** (ordem do protótipo):

| Ordem | Camada | Fonte | Desenho |
|---|---|---|---|
| 0 | Chão do município | Malha municipal | Área, `mapa_chao` |
| 1 | Área densamente edificada | BC250 | Área, `mapa_edificada` |
| 2 | Mancha urbana | Setores urbanos da Malha de Setores 2022, unidos | Área, `mapa_rua` |
| 3 | Quadras (quarteirões) | Remontadas das faces pelo código da quadra | Área, `mapa_quadra` |
| 4 | Massas d'água | BC250 | Área, `mapa_agua` |
| — | Relevo sombreado | Fonte de elevação a definir (P-39) | Não feito no protótipo; proposta: sombra multiplicada sobre as áreas, abaixo das linhas |
| 5 | Faces de quadra | Faces de Logradouros | Linha de 1 px, só a partir de 3 m por pixel |
| 6 | Rios | BC250 | Principal 2 px (mínimo de 6 m); secundário 1 px |
| 7 | Ferrovias | BC250 | 1,5 px |
| 8 | Estradas de terra | BC250 | 1,5 px (mínimo de 6 m) |
| 9 | Rodovias pavimentadas | BC250 | 3 px (mínimo de 12 m) |
| 10 | Zonas | `data/geo/zonas/` | Cor do modo, translúcida |
| 11 | Fronteiras de zona | `data/geo/zonas/` | `mapa_borda_zona` |
| 12 | Borda do município | Malha municipal | 2,5 px, `mapa_borda_municipio` |
| 13 e 14 | Zona sob o mouse e zona selecionada | — | 2 px em `texto_2` e 3 px em `texto` (como no Anexo A) |

- **Linhas com largura constante na tela** (em pixels), com largura mínima em metros para não sumirem de perto: uma rodovia nunca fica mais fina que 12 m no chão.
- **Zonas translúcidas:** opacidade de 50% na cidade inteira e de 22% no nível da rua (do protótipo), para as ruas continuarem legíveis. As escalas de cor dos modos são as do Anexo A, seção 2.
- **Praças:** a identificar pelo tipo de logradouro das faces, com cor própria **a aprovar**; não foram desenhadas no protótipo.
- **Escala gráfica** no canto do mapa ("100 m · 1 m por ponto").
- **Rótulos por nível de zoom, com desvio de colisão:**
  - cidade inteira: zonas e localidades (no máximo 12 nomes de zona);
  - bairro: zonas, rios, rodovias com a sigla;
  - rua: nomes de rua ao longo da linha, nunca de cabeça para baixo.
  - Nenhum rótulo cobre outro rótulo nem um marcador; o que não cabe some e volta ao aproximar.
- **Nomes de rua:** a fonte traz caixa alta sem acento. O protótipo mostra em caixa mista ("R. Pavao"); o acento continua faltando (P-40).
- **O que não entra no mapa:** nada de OpenStreetMap sem o autor liberar; nenhum nome de bairro que não venha do IBGE.

### 5. Janelas flutuantes e bordas (pilha na RL; disposição na RV-A)

**Disposição** (do protótipo, em 1920×1080 lógicos):

| Elemento | Onde | Medida do protótipo | Conteúdo |
|---|---|---|---|
| Barra de cima | Topo, inteira | 69 de altura | Data, pausa, 1x/2x/4x, fase e contagem; até 5 números |
| Seletor de modo | Sobre o mapa, abaixo da barra | Botões de 44 de altura | Modos do mapa e "Cidade inteira" |
| Janela da área ou ficha | Esquerda | 483 × 690 | Ficha da zona, da pessoa ou a área escolhida |
| Janela de notícias | Direita | 483 × 660 | Notícias, objetivos e intenção de voto |
| Retrato | Canto inferior esquerdo | 483 × 195 | Retrato do jogador, partido, cargo ou candidatura e número |
| Barra de ícones | Base, centro | 800 × 84; ícones com área de 96 × 56 | As 7 áreas do trilho |
| Faixa de decisão | Acima da barra de ícones | 630 × 66 | Uma frase e "Decidir" |
| Minimapa | Canto inferior direito | 483 × 287 | Minimapa da cidade e a legenda do modo |

- A decisão do autor (D-044) põe a **data e a velocidade junto do minimapa** e a **aprovação junto do retrato**. O protótipo deixou data e velocidade na barra de cima e não mostrou aprovação. **A aprovar:** qual das duas disposições vale.
- **Janela:** título em Oswald 500, 28; subtítulo em `legenda`, `texto_3`; a barra do título arrasta a janela (tooltip "Arraste para mover a janela"); botão de fechar; a janela nunca sai da tela.
- **Pilha (RL):** só a janela de cima recebe clique; Esc fecha a de cima e volta à anterior; no cartão de decisão o foco fica preso até a escolha. As janelas se atualizam por sinal do Core, sem ler o estado a cada quadro.
- **Cartão de decisão:** como no Anexo A (720 de largura, mapa escurecido), agora sobre o mapa de base.
- **Quanto do mapa as janelas cobrem:** o Anexo A manda que nada cubra mais de 40% do mapa. Com a ficha e as notícias abertas, as duas janelas do protótipo cobrem cerca de metade da largura, a 86% de opacidade. **A aprovar:** manter os 40% (janelas mais estreitas, ou uma das laterais recolhível) ou contar só o que é opaco.
- Em janela mais larga que 16:9, o mapa cresce e as janelas mantêm a largura; em 1366×768 tudo escala junto (Anexo A, seção 9).

### 6. Luz de dia e de noite (RV-A)

- A hora do relógio (GDD 17.4) define a luz do mapa de base: dia das 6h às 18h, entardecer e amanhecer como transição, noite no resto. Os horários exatos ficam em `data/`, **a aprovar**.
- A luz muda **só as cores do mapa de base** (seção 3) e, à noite, acende pontos de luz na mancha urbana. Nunca muda as cores de dado (modos, partidos, marcadores, rótulos), que mantêm o contraste de dia e de noite.
- A paleta de dia é desenhada na RV-A, com os mesmos nomes da de noite, e aprovada pelo autor nas capturas e no vídeo do meio da parte (Mantena, de manhã e à noite).
- Em 1x, a transição é gradual; quando o tempo salta (pausa longa, avanço até a segunda), a luz troca com corte seco. "Reduzir animações" troca tudo por corte seco.

### 7. Menu como cena (RV-A)

- O menu é um lugar em 3D estilizado que muda com a carreira: **comitê de campanha à noite** (vereador e candidato), **gabinete de prefeito**, **palácio** (cargos mais altos), com o menu sobre a cena.
- **Do protótipo:** comitê à noite com 6 personagens (militantes em volta da mesa de santinhos, mapa na parede, janela com a cidade, varal de lâmpadas); painel do menu à esquerda (`#0A0E14` a 86%); título "PALANQUE" em Oswald na cor `#F3E7C4`; subtítulo "Simulador de Carreira Política" (D-046); destaque em dourado `#E9B44C`, só no "Continuar carreira"; textos `#EFE6D4` e `#C9BCA2`.
- **Nome do jogo (D-046):** "Palanque" no título da janela, no menu, nos créditos e nos textos do jogo; "Palácio do Planalto" como lugar continua permitido. Teste: nenhum texto visível usa "Planalto" como nome do jogo.
- **Do protótipo, a corrigir:** a luz deixa os rostos escuros; o cartaz do comitê fica atrás do painel; o rótulo "CONTINUAR CARREIRA" está em caixa alta, fora da regra.

### 8. Estilo dos bonecos (D-043)

- **3D estilizado, formas suaves, proporções de desenho animado; não é low-poly facetado.** Sombreado liso, cores chapadas por material, pouca textura, luz suave.
- **Personagens:** corpos e cabeças base com variações de formato; cabelo, barba e roupas como peças trocáveis; tom de pele, cabelo e roupas por material tingível; expressões pelo contrato da seção 9.
- **Leitura a 96 px:** o rosto se lê no menor retrato do cache. Olhos, sobrancelhas e boca com tamanho e contraste para as expressões se distinguirem a 96 px (critério do rosto, ROADMAP).
- **O que o protótipo mostrou que se deve evitar:** cabelo em casca colada ao crânio (parece capacete); preto de cabelo que sai acinzentado; corpo e roupa sem caimento (parecem sacos); mãos sem dedos (parecem luvas); pescoço fino e comprido; rosto realista demais ou com cara de criança.
- **Diversidade:** tons de pele pela paleta da Appearance e pelo Censo (D-016); idades de 17 a 90 anos, com grisalho e rugas pela idade; corpos variados. Aparência nunca altera votos nem atributos.
- **Multidão e figurantes:** corpos e cabeças de base CC0, com os mesmos materiais tingíveis, sem expressões no rosto (seção 9, multidão).
- **Bases:** só CC0 ou CC BY, com licença conferida e crédito (D-045); Quaternius fora.

### 9. Cabeça substituível (contrato técnico)

Toda cabeça (a provisória, as cabeças A e B do teste A/B e a do artista, se houver) segue este contrato, o mesmo de `docs/BRIEFING_ARTISTA_3D.md`. Trocar uma cabeça por outra não exige mudança de código.

- **Arquivos:** glTF 2.0 binário, um por peça: `cabeca_<id>.glb`, `cabelo_<id>.glb`, `barba_<id>.glb`.
- **Eixos e escala:** Y para cima, frente para +Z, 1 unidade = 1 m. Corpo de referência de 1,60 m e cabeça de cerca de 0,30 m, do queixo ao topo do crânio.
- **Origem:** no nó `encaixe_pescoco`, sem rotação, no centro da base do pescoço (onde ele encontra os ombros), preso ao osso da cabeça do corpo. No corpo de referência de 1,60 m: encaixe a 1,22 m do chão, queixo a cerca de 1,30 m.
- **Borda do pescoço:** aberta, num plano horizontal 3 cm abaixo do encaixe.
  - Anel de 16 vértices, com 5,5 cm de raio.
  - O primeiro vértice fica na frente (+Z); os demais seguem no sentido anti-horário visto de cima.
  - Normais suaves, perpendiculares ao anel.
  - O corpo base termina num anel idêntico, no mesmo plano, e a gola cobre a emenda.
  - Conferido no protótipo: o corpo base usa estas medidas, e as cabeças A e B fecham no anel (16 pontos, 5,5 cm de raio médio, nenhum ponto de borda fora).
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
- **Expressões:** nomes exatos. Peso 1 é a expressão inteira, e a neutra é a malha sem expressão aplicada. São as 8 expressões do GDD 3.6:
  - `expr_sorriso`, `expr_sorriso_largo`, `expr_preocupado`, `expr_bravo`, `expr_triste`, `expr_surpreso` e `expr_cansado`;
  - mais `piscar`, que se soma às demais.
  A barba e as sobrancelhas separadas levam as expressões da região que cobrem, com os mesmos nomes. O código pede a expressão pelo nome; a cabeça declara num metadado (`modo_expressao`) como a realiza:
  - `shape_keys`: por shape key com o nome, como acima. É o modo da cabeça do artista e da cabeça A do teste A/B.
  - `pecas_do_rosto`: trocando peças ou a textura do rosto nos nós filhos `rosto_olhos`, `rosto_sobrancelhas` e `rosto_boca`, com uma variante por nome de expressão. É o modo da cabeça B do teste A/B.
  O resto do personagem não sabe qual modo a cabeça usa: trocar uma cabeça por outra só mexe no nó da cabeça.
- **Materiais tingíveis** (texturas em tons de cinza; o jogo multiplica pela cor da Appearance):
  - `pele` e `labios` (este derivado da pele);
  - `sobrancelha`, `cabelo` e `barba`, com a cor do cabelo. O grisalho vem do canal R das texturas `*_mascara`;
  - `iris`.
  Não tingíveis: `esclera`, `dentes` e `lingua`. Rugas e marcas ficam em `pele_detalhe`, com a intensidade dada pela idade.
- **Orçamento:** cabeça com olhos, dentes e língua até 8.000 triângulos; cabelo até 6.000; barba até 2.000. Texturas PNG até 2048 px, UV sem sobreposição. No modo `pecas_do_rosto`, o protótipo contou o pior caso visível (uma variante de cada peça por vez: 4.786 triângulos na cabeça B, 8.322 no arquivo); **a aprovar** junto com as convenções abaixo.
- **Teste do artista:** neutra, `expr_sorriso` e `expr_bravo` (D-045, adendo). O protótipo usa as mesmas três.
- **Multidão e LOD:**
  - Pessoas com nome (retrato, cartão, plano próximo) usam a cabeça deste contrato. Em plano médio, o LOD automático do Godot reduz a malha; o artista não entrega LOD.
  - A multidão e os figurantes (plateia, fila da urna, comício, posse) usam corpos e cabeças de base CC0 (KayKit, Human Base Meshes), com os mesmos nomes de material tingível, para seguirem o mesmo sorteio de tons da Appearance.
  - Cada figurante tem até 3.000 triângulos no total, sem expressões no rosto.
  - Nenhum figurante usa a cabeça do artista, para o custo da multidão não depender dela.
- **Cena de teste de cabeça:** carrega qualquer `cabeca_*.glb` e confere nomes, materiais, expressões e orçamento. Aplica as 8 expressões, 6 tons de pele e 2 cores de cabelo, e grava retratos de 96 px e 512 px. Toda cabeça nova passa por ela antes de ser aceita. Existe no protótipo e vira parte do alvo de fumaça `rv`.

**Duas convenções que o protótipo adotou, a aprovar pelo autor:**
1. **Sufixo numérico do Blender ignorado:** ao comparar nomes, o sufixo que o Blender acrescenta a nomes repetidos (`.001`, `.002`…) é descartado. Ex.: a variante `expr_bravo.001` vale como `expr_bravo`. Sem isso, toda peça do rosto da cabeça B teria de ser renomeada à mão depois de exportada.
2. **Material `interior_boca` aceito:** material extra, não tingível, para o fundo da boca aberta (a cabeça B o usa; a cena de teste dá aviso). Se aprovado, entra na lista dos não tingíveis, ao lado de `esclera`, `dentes` e `lingua`.

### 10. Mundo vivo (RV-C)

Tudo pela hora e pelo estado do Core; **nada sorteado só para enfeite que contradiga o Core** (D-055).

| Elemento | Regra |
|---|---|
| Carros e pedestres | Pela densidade de população da zona e pela hora (menos à noite); só nas ruas e calçadas do mapa de base |
| Carro de som e cabos do rival | Só no reduto do rival e só quando ele está em campo naquela zona |
| Multidão | No lugar do evento ou do comício, do tamanho do público calculado pelo Core |
| Chuva | Por zona, só quando a chuva muda a previsão do compromisso |
| Personagens a caminho | O ícone do jogador e os dos rivais andam pelas ruas até o compromisso |
| Marcadores | Compromissos do jogador, rivais em campo e lideranças na cor do alinhamento; agrupam quando são mais de 3 na mesma zona (Anexo A, seção 7) |
| Pulso | Onde acontece um fato; no máximo 3 por segundo na tela, em `atencao` |
| Zona em disputa | Hachurada (empate na margem de erro, Anexo A, seção 2) |
| Efeito proporcional à importância | Quase nada no clique comum; grande na apuração; sem tremer a tela |
| Áudio ambiente | Rua, multidão, rádio, pela zona e pela hora; volume próprio nas Opções; assets CC0 ou CC BY com crédito |

- **"Reduzir piscadas"** nas Opções: tira o pulso e os brilhos, mantém as cores e os marcadores.
- O mundo vivo nunca cobre rótulos, números nem a faixa de decisão, e nunca usa as cores de dado para enfeite.
- **Proposta, a aprovar:** a qualidade leve (seção 12) reduz carros e pedestres, mas mantém tudo o que carrega informação (carro de som, multidão, chuva, marcadores).

### 11. Cenas (RV-B)

- **Curtas e puláveis** com um clique ou Esc; **opção para desligar** nas Opções; com as cenas desligadas, o jogo segue igual.
- **O resultado da ação aparece no fim da cena** e é o mesmo que o Core aplicou (a cena não sorteia nada).
- **Ordem:** primeiro **caminhada** (o personagem na rua, eleitores reagindo) e **comício** (palco, multidão do tamanho do público, bandeiras do partido); depois **reunião** com liderança, **entrevista** no estúdio de rádio ou TV e **notícia** como telejornal ou capa de jornal.
- **Câmera:** planos abertos e médios, como nas posses (GDD 13.2); plano próximo só de pessoa com nome.
- **Gente:** bonecos da seção 8 com a cabeça escolhida no teste A/B (ou a do artista); figurantes pela regra da multidão.
- **Cenários:** estilizados, sem réplica de lugar real, emissora real, brasão ou marca oficial.
- **Telas fora do jogo, pela referência `rv_cenas.html`:** estúdio de foto na criação (com o santinho), seção eleitoral na urna, estúdio de TV na apuração. **Proposta, a confirmar pelo autor:** entram na RV-B, com os mesmos bonecos (a D-055 não as cita).
- **Duração:** a fixar com o autor nas capturas do meio da RV-B (as posses do GDD 13.2 têm de 15 a 40 s).

### 12. Qualidade gráfica: cheia e leve

| | Cheia | Leve |
|---|---|---|
| Sombras | Sim | Não |
| Brilho das luzes (glow) | Sim | Não |
| Resolução do 3D | 100% | 75%, com FSR |
| Interface e mapa 2D | Resolução cheia | Resolução cheia |
| Mundo vivo | Densidade cheia | Menos carros e pedestres (seção 10, a aprovar) |

- Vale para tudo o que é 3D (menu, cenas, posses, retratos ao vivo); o mapa da cidade é 2D e não muda.
- **Medido no protótipo** (menu com 6 personagens, 1920×1080, Radeon integrada, máquina dividida com outros programas): qualidade cheia de 31 a 36 FPS; leve de 50 a 58. **O menu não chegou a 60 FPS em qualidade cheia nessa placa.**
- **A aprovar:** se a qualidade leve vira opção nas Opções, qual é o padrão e qual é a placa de vídeo-alvo dos 60 FPS.

### 13. Desempenho e verificação

- **Metas do autor:** 60 FPS com zoom e arraste no mapa; mapa de base de uma metrópole pronto em menos de 3 s. **Propostas:** 60 FPS no menu com 6 personagens e na cena com a multidão, na placa-alvo; 60 FPS com o mundo vivo ligado numa metrópole (RV-C).
- **Medidas do protótipo**, três rodadas, máquina dividida com outros programas, sincronia vertical desligada (`docs/referencias/rv/medidas.md` no branch):

| O que | Rodada 1 | Rodada 2 | Rodada 3 |
|---|---|---|---|
| Mapa de Belo Horizonte pronto | 777 ms (com a campanha) | 479 ms | 565 ms |
| Mapa de Mantena pronto | 1.900 ms (com o mundo) | 383 ms | 81 ms |
| Zoom e arraste em Mantena, média (1% pior) | 218 (55) | 233 (131) | 230 (124) |
| Zoom e arraste em BH, média (1% pior) | 160 (29) | 168 (96) | 167 (96) |
| Menu, cabeça A, cheia | 32 (27) | 32 (26) | 31 (20) |
| Menu, cabeça A, leve | 56 (36) | 55 (31) | 56 (41) |
| Menu, cabeça B, cheia | 34 (26) | 36 (24) | 36 (20) |
| Menu, cabeça B, leve | 50 (8) | 58 (46) | 56 (45) |

- **Remedir** com a máquina livre antes de fixar as metas da placa-alvo.
- **Verificação de cada captura** contra este guia, em 1366×768, 1920×1080 e 2560×1080: texto de 18 px ou mais, um destaque por vez, nenhum rótulo cobrindo outro, todo número com tooltip.

### 14. Pontos em aberto (para o autor)

1. Aprovar esta proposta, inteira ou em partes; até lá vale o Anexo A.
2. Disposição das bordas: data e velocidade no topo (protótipo) ou junto do minimapa (D-044); aprovação junto do retrato.
3. Regra dos 40% do mapa com janelas translúcidas.
4. As duas convenções do contrato da cabeça (seção 9).
5. Qualidade leve como opção, padrão e placa-alvo (seção 12).
6. Julgamento das cabeças A e B pela prancha (critério do rosto, ROADMAP); nenhuma foi declarada boa o bastante.
7. Caixa alta no nome do jogo no menu; cor das praças; horários do dia e da noite.
8. Parte em que entram as telas fora do jogo (estúdio de foto, urna, apuração) e a duração das cenas.

---

## Anexo A — Guia em vigor: Interface (sem mudança)

> Este anexo é o guia atual, que vale a partir da Fase R e até o autor aprovar a Parte 1. As seções **Retratos** (camadas, ancoragem, nomenclatura; GDD 7.4) e **3D** (escala, polígonos, materiais tingíveis; GDD 13.3) do guia original não chegaram a ser escritas; o 3D passa a ser coberto pela Parte 1 (seções 8 e 9).

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
| Minha força | Sequencial de `#1B2A2A` a `#2FD0A5`, contínua de 0 a 100 (posição na escala pela raiz quadrada da presença, para as presenças baixas do começo não saírem da mesma cor) | Legenda compacta: a barra do gradiente com as marcas 0, 10, 25, 50 e 100, no mesmo lugar da escala; sem os valores de cada zona |
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
| Efeito numa zona | A zona pulsa uma vez e solta uma etiqueta ("+2,1"), texto liso sobre uma pílula escura, sem contorno, que sobe 24 px, fica nítida até 550 ms e some | 900 ms |
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

Atualizado em 2026-11-17 14:00, depois de: respostas do autor às capturas da RL e ao relatório do meio-dia (D-059 e D-060). Em andamento: as cinco correções da RL antes da tag `fase-rl`, o resto da RV-A (noite, partido líder pela margem, câmera), o cansaço por veículo pelo caminho "baixar o pico" e a amostra corrigida do BalanceSim. Próximo: integrar a RL e criar a `fase-rl`; depois mandato e prefeitura no relógio, antes da RV-C.

# Planalto — contexto para retomar o projeto num chat novo

Este documento é para quem não vê o repositório. Ele resume o que importa; os nomes de arquivo citados servem só para pedir coisas ao Claude Code.

## 1. Quem e o quê

- **Autor e único dono:** Arthur Albefaro. Conversa em português.
- **Projeto:** Planalto, simulador de carreira política brasileira para PC. O jogador cria um personagem, começa como cidadão numa cidade real do Brasil, disputa vereador e pode subir até presidente. É um simulador livre: qualquer ideologia, ética ou estratégia; o jogo nunca julga moralmente, só simula consequências (eleitorais, judiciais, midiáticas, pessoais).
- **Divisão do trabalho:**
  - O **Claude Code**, rodando na máquina do autor (Windows), escreve código, dados, testes e documentação, faz commits locais e escreve relatórios.
  - O **consultor** (um chat como este) revisa os relatórios, responde às dúvidas de design e escreve os prompts que o autor cola no Claude Code.
  - O **autor** joga cada parte, decide o que é decisão dele e aprova.

## 2. Stack, ambiente e comandos

- **Engine:** Godot 4.7.2 .NET (C#), 2D, com mapa 2.5D e cerimônias 3D previstas. Só PC (Windows, Linux, macOS). Tela base de 1920×1080, escala `canvas_items`, aspecto `expand`.
- **.NET:** SDK 10.0.201 instalado; projetos em net8.0. C# com nullable e warnings como erro.
- **Godot** instalado em `C:\Godot`, chamado por `godot` (atalho no PATH).
- **Solução `Planalto.sln`:**
  - projeto Godot na raiz: interface, em `src/`;
  - `core/Planalto.Core`: toda a lógica do jogo, .NET puro, sem Godot;
  - `core/Planalto.Core.Tests`: testes xUnit;
  - `tools/`: DataImporter (baixa IBGE, TSE e as outras fontes e gera `data/`), DataImporter.Tests, DataValidator, EventLint, PlaceholderArt (arte provisória) e BalanceSim (robô que joga centenas de campanhas por porte e mede os critérios de balanceamento: `dotnet run -c Release --project tools/Planalto.BalanceSim -- --campanhas 200 --cidades 10 --semente 1`).
- **Pacotes permitidos:** GodotSharp, xUnit e NetTopologySuite (este só no importador). Nada novo sem o autor aprovar.
- **Conteúdo:** todo texto, número de balanceamento e dado fica em `data/` (JSON). Dados brutos baixados ficam num cache fora do Git.
- **Verificação obrigatória**, antes de dar qualquer tarefa por concluída:
  - `dotnet build Planalto.sln`
  - `dotnet test core/Planalto.Core.Tests`
  - `dotnet run --project tools/Planalto.DataValidator`
  - `godot --headless --build-solutions --quit`
  - se o importador mudou: `dotnet test tools/Planalto.DataImporter.Tests`
- **Testes de fumaça da interface com janela.** Sem janela o Godot não entrega cliques de mouse, então estes precisam de janela aberta. Comando: `godot --path . res://src/UI/Debug/UiShot.tscn -- --target=<alvo> --perfil=<nome>` (código 0 = passou; o perfil isola saves e opções de cada cópia de trabalho).
  - Na main são 31 alvos: perfil, newworld, creator, creator17, picker, hub, campaign, map, mandate, prefeitura, eventos, layout, explicacao, agenda, zona, lideranca, equipe, verba, plataforma, objetivos, rivais, midia, contas, mapa_pequeno, gabinete, obras, fiscal, indicadores, entressafra, opcoes, tracos. O branch `rl` acrescenta `rl`; o `rva`, `rva` e `rv_prototipo`.
  - O alvo `objetivos` caiu uma vez com falha de memória no coletor do .NET (P-44); repetido, passou.
- **Portão antes de integrar na main:** build da solução e do projeto Godot sem avisos, testes do Core e do importador, DataValidator, EventLint, import e build do Godot, todos os alvos de fumaça com janela em perfil isolado e, quando muda balanceamento, o BalanceSim.
- **Jogar:** `godot --path .` na raiz do repositório, ou F5 no editor.

## 3. Regras absolutas

- **Autoria:**
  - Commits só em nome de Arthur Albefaro. Nenhuma menção a IA, Claude ou Anthropic em commits, código, comentários, docs, créditos ou metadados.
  - Três proteções: configuração local do Git, configuração do Claude Code sem atribuição, e um hook de commit que apaga linhas de atribuição.
  - Proibido: `--no-verify`, push sem pedido, `reset --hard`, `rebase`.
- **Commits:** em português, no imperativo, `tipo(escopo): descrição`. Tipos: feat, fix, refactor, test, data, art, audio, docs, chore. Um commit por funcionalidade verificada.
- **Neutralidade:**
  - Partidos, políticos, empresas, institutos e veículos são sempre fictícios. Municípios, estados e dados demográficos são reais.
  - Nomes e siglas de partido são validados contra uma lista de bloqueio de partidos reais.
  - Logotipos usam ícones genéricos e nunca copiam um real.
  - Aparência nunca altera votos nem atributos. Nenhum evento com estereótipo regional, religioso, racial ou de gênero; tragédias com seriedade.
  - O espectro democrático inteiro tem caminho viável até a Presidência.
- **Dados:**
  - Todos os 5.570 municípios, só de fontes oficiais (IBGE, TSE e, a partir da Fase R, Tesouro, INEP, DATASUS, MTE e Banco Central).
  - Nunca um número escrito à mão. Lacunas ficam registradas, com a regra de substituição.
  - Se uma fonte bloquear o download, o trabalho para e informa endereço e arquivo.
- **Arquitetura:** lógica só no Core; a interface só lê o estado e manda comandos; toda aleatoriedade passa por um gerador com semente.
- **Lei:** parâmetros que vêm de lei ficam em objetos de configuração com o valor da lei vigente e o artigo citado.

## 4. Direção do jogo

**Visão.** Um jogo em que o jogador perde a noção da hora porque o mundo está vivo, as pessoas lembram e as escolhas pesam. As referências são Geopolitical Simulator (mapa no centro, tempo correndo, mundo que age sozinho), Crusader Kings (pessoas com memória e histórias que nascem da simulação) e Football Manager (gestão com equipe, informação imperfeita e trocas). As diferenças:
- escala local e brasileira: a cidade real em zonas, com dados do Censo, antes do país;
- regras eleitorais e leis brasileiras reais;
- ficção neutra em tudo que é pessoa ou partido;
- carreira de um personagem só, que envelhece.

**Os 10 princípios** (GDD 17.1). Todo sistema novo precisa cumprir, e cada um tem critério testável.
- a) **A situação manda.** A melhor ação depende de zona, público, notícia, crise ou rival. Nenhuma ação é a melhor em mais de 40% das semanas, no simulador de balanceamento.
- b) **O mundo se mexe sozinho.** Rivais, lideranças, mídia e economia agem com o jogador parado, e isso aparece no mapa e no feed.
- c) **Pessoas com rosto e memória.** Todo NPC relevante tem nome, retrato, interesses e lembranças do jogador; rivais respondem e mudam de plano.
- d) **A consequência fica.** Promessas são cobradas, apoios criam dívida, escolhas abrem cadeias meses ou anos depois.
- e) **Troca real.** Toda semana há uma decisão em que se ganha algo e se perde algo; semana sem decisão passa sozinha e rápido.
- f) **O mundo explica.** Previsão com incerteza antes do clique, e "o que mudou, onde e para quem" depois; nunca só "feito".
- g) **Repetir cansa.** A mesma ação, no mesmo lugar e público, rende cada vez menos, e o jogo avisa antes.
- h) **Objetivos que nascem do jogo.** Metas secundárias surgem da simulação e ficam num painel.
- i) **Clareza.** Em 10 segundos o jogador sabe o que acontece, o que pede atenção e o que pode fazer.
- j) **Nada sem explicação.** Todo controle tem tooltip (o que é, o que faz, quanto custa); nomes fictícios vêm com o que são; termos técnicos abrem um verbete; há uma Enciclopédia (F1), dicas de primeira vez e motivo em todo botão bloqueado.

**As seis peças da espinha comum** (GDD 17.2). Todos os sistemas usam estas peças, para se entrelaçarem em vez de se somarem.
1. **Zona:** pedaço do município, formado por setores do Censo 2022. Tudo acontece numa zona.
2. **Pessoa:** um modelo único para rival, liderança, equipe, doador, jornalista, assessor, secretário e vereador. Tem interesses, temperamento, relação e até 20 lembranças datadas.
3. **Compromisso:** item da agenda, com o quê, onde, quando (dia e turno) e para quem.
4. **Livro de compromissos:** promessas, dívidas, posições assumidas e esqueletos (irregularidades com a lista de quem sabe). Atravessa eleições.
5. **Fato e notícia:** todo sistema emite fatos; a mídia os transforma em notícias conforme a linha editorial.
6. **Previsão, explicação e objetivo:** todo comando tem previsão e explicação; um gerador propõe até 3 objetivos por semana.

## 5. Design em resumo (GDD, seção por seção)

1. **Visão geral:** conceito, referências e pilares (autêntico ao Brasil, toda escolha cobra preço, ficção neutra, seu político, cada carreira única). Carreira completa de 6 a 12 horas.
2. **Stack:** Godot .NET com Core em .NET puro testável sem a engine.
3. **Criação de personagem:** quatro abas.
   - Aparência em camadas tingíveis, com expressões, envelhecimento e "Espelho".
   - Vestuário por contexto.
   - Identidade: nome, nome de urna, gênero e pronomes, cidade, origem, profissão, família, traços.
   - Campanha: número, slogan, logotipo, jingle, santinho, adesivo, bandeira e peça de redes. O outdoor saiu por ser proibido em lei.
   - Mais a aba Partido.
4. **Carreira:** calendário eleitoral real (1º turno no primeiro domingo de outubro, campanha a partir de 16 de agosto, filiação 6 meses antes); escada vereador → prefeito ou deputado → senador ou governador → presidente. A vitória é terminar a Presidência com aprovação acima de 40%.
5. **Sistemas:**
   - atributos (Carisma, Oratória, Articulação, Gestão, Integridade) e recursos;
   - ideologia em dois eixos e 10 temas;
   - eleitorado em 240 segmentos por município, com modelo de utilidade e softmax;
   - apuração com as regras reais;
   - partidos, campanha, debate de cartas, mandatos, mídia, escândalos, NPCs e eventos.
6. **Laço semanal antigo:** substituído pela seção 17.
7. **Arquitetura:** pastas, fluxo comando → Core → sinal → interface, retratos, formato de evento, save com versões, balanceamento por simulação de milhares de carreiras.
8. **Arte e áudio:** vetorial sóbrio, fontes livres, acessibilidade. As regras da interface estão no ART_GUIDE.
9. **Roadmap:** fica no ROADMAP.md (fonte única).
10. **Fluxo de desenvolvimento.**
11. **Riscos e cuidados.**
12. **Municípios:** todos, com fontes oficiais, vagas reais de vereador, porte (pequeno até 20 mil, médio até 100 mil, grande até 500 mil, metrópole) e simulação por nível de detalhe (detalhado no município em foco, agregado no resto).
13. **2.5D e 3D:** mapa do estado e do país extrudado; a partir da parte RV, tudo o que mostra gente em 3D estilizado de formas suaves (D-043): retratos, menu como cena, cenas da RV-B e posses, sempre puláveis. Bases CC0 (KayKit, Human Base Meshes), Quaternius fora, artista só se o autor reprovar as duas cabeças do teste A/B (D-045). O mapa da cidade continua 2D.
14. **Partidos:** 16 famílias ideológicas simétricas; partido criado pelo jogador, no início ou fundado durante a carreira pela Lei dos Partidos.
15. **Eventos:** meta de 600 ou mais em 12 categorias, com condições, raridade, cooldown, cadeias e variáveis.
16. **Novo Mundo:** predefinições de quantidade de partidos; urna jogável no dia da eleição; cenário "Brasil 2026", que espelha bancadas, prefeitos e força local reais com nomes fictícios.
17. **Reformulação** (a seção que manda agora):
   - **17.1** Os princípios a–j acima.
   - **17.2** As seis peças acima.
   - **17.3 Tela central:**
     - barra superior com data, pausa, 1x/2x/4x, fase e no máximo 5 números;
     - trilho com 7 áreas: Campanha ou Mandato, Pessoas, Agenda, Território, Finanças, Mídia, Partido e Câmara;
     - painel da área à esquerda e mapa 2D das zonas sempre visível no centro;
     - coluna direita com Notícias, Objetivos e Intenção de voto;
     - decisões em cartão sobre o mapa, que pausam o jogo;
     - na RV-A (D-044), o mapa ocupa a tela inteira e os painéis viram janelas semitransparentes sobre ele, com a interface nas bordas (barra de ícones embaixo, retrato num canto, minimapa no outro); a pilha de janelas com Esc entra antes, na RL. A ficha da zona flutuante foi aceita pelo autor (D-059), estreita ou encaixada no painel esquerdo, sem cobrir o resultado no mapa.
   - **17.4 Tempo:**
     - o dia é a unidade, com 3 turnos;
     - campanha a 4, 2 ou 1 segundo por dia; mandato a 1, 0,5 ou 0,25;
     - simulação pesada às segundas;
     - pausa automática configurável por tipo de acontecimento.
   - **17.5 Território:**
     - de 3 a 20 zonas por cidade, conforme o porte, montadas dos setores do Censo 2022; teto de 30 MB para os contornos;
     - cada candidato tem presença por zona, que decai e transborda para as vizinhas;
     - religião e escolaridade por área de ponderação do IBGE; regra própria para dados omitidos por sigilo;
     - na RV-A (D-044), um mapa de base da cidade real por baixo das zonas: ruas com nome e quadras (Faces de Logradouros 2022), rios, estradas, ferrovias e localidades (BC250 do IBGE), relevo se houver fonte licenciada; zoom até a rua, rótulos sem colisão, luz de dia e de noite. Do OpenStreetMap, só os nomes de bairro onde o IBGE não tem bairro, com crédito (D-058); pontos de interesse esperam o autor liberar (P-45).
   - **17.6 Agenda:**
     - compromissos (caminhada, reunião, visita, comício, entrevista, gravação, jantar, evento da cidade, descanso);
     - fórmula de rendimento com cansaço (o terceiro igual rende 45%), ocasião, deslocamento e delegação.
   - **17.7 Equipe:** coordenador, marqueteiro, tesoureiro, advogado, social media e cabos, com qualidade, salário, lealdade, vínculos, conselho semanal e eventos próprios.
   - **17.8 Verba:**
     - quatro canais pagos: Rua, Redes com impulsionamento, Material gráfico e Cabos (estes com limite legal);
     - retorno decrescente e disputa por zona;
     - rádio e TV não se compram: chega-se a eles por entrevista e horário gratuito.
   - **17.9 Plataforma:** posição em 10 temas, bandeiras, "posição conhecida", promessas locais, custo de mudar e credibilidade.
   - **17.10 Lideranças:** de 6 a 30 pessoas por cidade (religiosas, comunitárias, sindicais, empresariais, rurais), com zona, influência, pedido, preço, rivalidades e memória.
   - **17.11 Rivais:**
     - perfil de campanha e plano refeito toda semana;
     - resposta a ataque conforme o temperamento;
     - pactos e dobradinhas;
     - colega de chapa é aliado e concorrente ao mesmo tempo.
   - **17.12 Dinheiro:**
     - fundo do partido, pessoa física com limite, recursos próprios, financiamento coletivo e dinheiro "por fora", que vira esqueleto;
     - limite de gastos do TSE, julgamento das contas e cassação possível.
   - **17.13 Mídia local:** veículos fictícios com linha editorial (muda o tom, nunca o fato), tema em alta, entrevista, resposta a ataque com prazo e boatos.
   - **17.14 Vereador:** gabinete, requerimento, indicação, fiscalização com três saídas, emenda que vira obra no mapa, negociação de voto com contagem nominal e base que cobra promessas.
   - **17.15 Prefeito:**
     - secretários com competência e agenda própria;
     - obras com etapas (licitação ou contratação emergencial) no mapa;
     - folha de pagamento com limites da Lei de Responsabilidade Fiscal e convênios;
     - Câmara que negocia e crises com causa.
   - **17.16 Renovação do mundo:**
     - envelhecimento e morte pela tábua de mortalidade do IBGE por UF;
     - saídas: aposentadoria, doença, derrota, Ficha Limpa, cassação, renúncia, escândalo, outra carreira;
     - entradas: lideranças, sindicatos, igrejas, empresas, mídia, herdeiros;
     - substituição pela regra real e morte tratada com seriedade;
     - meta de 40 anos: renovação da Câmara dos Deputados entre 40% e 62% por eleição e do Senado entre 65% e 88% (faixas do DIAP).
   - **17.17 Estatísticas reais:**
     - ponto de partida oficial: PIB municipal, finanças do Tesouro, IDEB, mortalidade infantil, homicídios pelo SIM, saneamento do Censo, emprego da RAIS e economia nacional pelo Banco Central;
     - evolução simulada por tendência inicial, orçamento, políticas, obras, contexto e choques;
     - o jogador vê sempre a unidade real.
   - **17.18 Vagas individuais fictícias** para todas as cadeiras do cenário Brasil 2026. O pacote de nomes reais está suspenso.
   - **17.19** Verificação do desenho.
   - **17.20** Decisões do autor.
   - **17.21** Quatro relatos de semanas de jogo ("prova de imersão"), usados como régua do que se quer.

## 6. Estado atual

- **Fases concluídas:** 0 (fundação), 1 (núcleo: regras eleitorais, municípios, partidos, cenário Brasil 2026, eleitorado e pesquisas), 2 (criação de personagem), 3 (fatia vertical de vereador), 4 (mandato de vereador e fundação de partido), 5 (prefeito e debate). Da Fase 6, dois itens: contornos dos municípios e população projetada com vagas por eleição.
- **Testes na main (17/11):** 1.033 no Core (1.048 no branch `rl`), 121 no importador, 31 alvos de fumaça com janela.
- **Teste do autor:** jogou a fatia vertical e reprovou: "pouca coisa pra fazer, confuso, feio, muito raso", "chato e saturável", "parece o Referee Simulator da Play Store".
- **Diagnóstico** (documento de auditoria), com as 5 causas do tédio:
  1. não há decisão, só gasto: 6 ações de efeito fixo, e repetir "Redes sociais" é sempre o melhor;
  2. o mundo é uma planilha parada;
  3. ninguém tem rosto nem memória;
  4. nada fica: tudo decai, os esqueletos nunca voltam;
  5. a tela não mostra o jogo.
  As regras do Core são boas e ficam; o que muda é a camada de decisão e a interface.
- **Fase R** (reformulação, **concluída** com a `fase-r-final`): feita em corujão autorizado pelo autor em 16/11/2026, na ordem R1 → RD → R2a → R2b → R3 → R4, cada parte com uma tag local. No corujão, dúvida vira escolha conservadora registrada em PENDENCIAS; download bloqueado vira lacuna registrada; falta de janela vira parte "com pendências". Agora vêm as partes de leitura e imersão (RL, RV-A, RV-C, RV-B), mais abaixo.
  - **R1:** trazer o branch `wip/idade-inicial` (idade inicial de 17 a 45 anos, profissão Estudante, família coerente) com testes; importar as zonas; tela central; tempo corrente; previsão e explicação; tooltips; Enciclopédia; troca do outdoor.
  - **RD:** dados reais de partida.
  - **R2a:** território, agenda, lideranças, Livro, BalanceSim.
  - **R2b:** equipe, verba, plataforma, objetivos.
  - **R3:** rivais, mídia, doadores.
  - **R4:** mandatos novos, indicadores reais, renovação no município.
- **R1 feita, com pendências (tag `fase-r1`):**
  - idade inicial de 17 anos;
  - 28.135 zonas montadas com setores do Censo 2022 para os 5.570 municípios (20,5 MB de contornos);
  - tela central com mapa 2D das zonas, tempo por dia, previsão antes do clique, Enciclopédia (F1), dicas e tooltips em três partes;
  - material de campanha permitido no lugar do outdoor.
  Faltam: mandato e prefeitura na tela central, tooltips nas outras telas, tela de Opções das pausas, ajustes do mapa em cidade pequena. Ainda não começaram: RD, R2a, R2b, R3, R4.
- **RD feita, com pendências (tag `fase-rd`):**
  - indicadores reais de partida de todos os municípios (PIB, emprego formal, finanças do SICONFI, IDEB, mortalidade infantil, homicídios, leitos, saneamento), com 2.681 lacunas registradas e a regra de cada uma;
  - economia nacional de partida, tábuas de mortalidade por UF, projeção da população por UF;
  - eleitores, religião e escolaridade por zona;
  - receita real na prefeitura; números oficiais do município em F1 → Este mundo.
  Faltam: votação de 2024 por zona, transferências (FPM, FPE) e saldo do CAGED.
- **R2a feita, com pendências (tag `fase-r2a`):**
  - presença e voto por zona, demanda da zona, mapa "Minha força";
  - agenda de 7 dias por 3 turnos com 9 tipos de compromisso, previsão com cansaço, eventos da cidade e convites (saíram a energia e as 6 ações);
  - pessoa com memória, Livro de compromissos, lideranças com pedido;
  - BalanceSim: o critério "nenhum tipo é o melhor em mais de 40% das semanas" passa em cidade pequena (30%) e média (34%) e não passa em grande (41%) e metrópole (60%).
  - 581 testes no Core, 15 alvos de fumaça com janela.
- **R2b feita, com pendências (tag `fase-r2b`):**
  - equipe de cinco funções com conselho, decisões e delegação; verba em quatro canais com disputa e saturação; plataforma com posição conhecida, bandeiras e promessas; objetivos na coluna direita;
  - lei dos canais em `ChannelLawConfig` (cabos, impulsionamento, vedações); verbetes de propaganda reescritos pelo texto conferido;
  - BalanceSim: decisões com troca passam; canal de verba fora em três portes, par fora no pequeno, tipo fora na metrópole (P-20); o caixa não comporta equipe e verba antes dos doadores (P-21).
  - 673 testes no Core, 19 alvos de fumaça com janela.
- **R3 feita, com pendências (tag `fase-r3`):**
  - rivais com plano, gatilhos, ataques, pactos e painel da chapa; mídia local com veículos, linha, tema em alta, pauta e horário gratuito; ataques contra o jogador com prazo; doadores, limite de gastos, contas, esqueletos e representação;
  - 32 eventos ligados a pessoas, zonas e fatos; EventLint cobra rastro, troca e texto de morte; lei de campanha conferida no texto oficial (`docs/referencias/LEIS_CONFERIDAS.md`);
  - BalanceSim com 200 campanhas: tipo e canal passam nos quatro portes; par do porte pequeno (entrevista) e régua da D-026 não fecham (P-26);
  - testes de fumaça com perfil isolado (`--perfil`); testes lentos com a categoria `Lento`.
- **Depois da R3, na main:** mapa de cidade pequena (rural pelos distritos do IBGE, 31.026 zonas, versão 2 dos ids; save antigo recusado com aviso; abertura no núcleo urbano) e correções do teste do autor na `fase-r1` (P-24, itens 1 a 8 e encaixes dos retratos 2D).
- **R4 feita, com pendências (tag `fase-r4`):**
  - mandato de vereador, prefeitura e entressafra na tela central (fecha a P-01); gabinete, indicação, emenda, fiscalização, contagem nominal, Mesa e CPI, base que cobra o Livro; secretariado, obras com licitação, folha e mesa de negociação, mínimos, repasse, convênios, crises com causa;
  - 11 indicadores em unidade real com evolução semanal; renovação do mundo político (morte pela tábua da UF, suplente, vice, vacância pela CF e pelo Código Eleitoral, luto);
  - BalanceSim de mandato: instrumento mais escolhido de 28% a 30% (passa); campanha igual à da `fase-r3`;
  - 1.014 testes no Core, 88 no importador, 29 alvos de fumaça com janela.
- **Cansaço por veículo** (branch `cansaco-veiculo`, fora da main): a entrevista cansa por veículo, e as vagas valem 3 dias e somem se não usadas. Com 200 campanhas por porte o par passa (6% a 14%), mas o tipo de compromisso por semana fica em 62% a 73% (critério: 40%), porque a entrevista domina. O autor escolheu "baixar o pico" da entrevista (D-059); em medição.
- **fase-r-final feita, com pendências (tag `fase-r-final`):** correções da criação (traços que não liberavam o botão, biografia, acentos); tooltips em três partes em todas as telas (P-02); Opções → Tempo e religar dicas (P-03); mapa de cidade pequena fechado (P-09); cliques dos testes de fumaça firmes (P-33); 1.024 testes no Core, 32 alvos de fumaça.
- **Depois da Fase R (D-054 e D-055):** RL (leitura mínima: relógio, painel Você, painel de zonas, "Fazer agora", número flutuante, retratos) → RV-A (cidade real no mapa, estilo final) → RV-C (mundo vivo) → RV-B (cenas), cada uma com tag e capturas no meio.
- **Entrega da RV (17/11), só textos na main; protótipo no branch `rv-prototipo`, que não entra na main antes da RL:**
  - **Decisões:** D-043 (retratos e personagens em bonecos 3D estilizados de formas suaves, estilo Two Point Hospital, Overcooked e Planet Coaster, não low-poly facetado) e D-044 (mapa da cidade 2D detalhado e fiel ao lugar real; interface em janelas flutuantes sobre o mapa).
  - **GDD** 3.1, 3.6, 3.7, 7.4, 13, 17.3 e 17.5 atualizados; **ART_GUIDE** reescrito como proposta a aprovar (Parte 1), com o guia atual intacto no Anexo A, que vale até a aprovação; **ROADMAP** com a parte RV completa: cada item diz se entra na RL, na RV-A, na RV-C ou na RV-B.
  - **O protótipo:** mapa de base de Mantena e Belo Horizonte (ruas com nome, quadras remontadas das faces de logradouro, rios, estradas, ferrovias, localidades; sem relevo) com as zonas translúcidas por cima, uma janela flutuante e a interface nas bordas; menu como cena (comitê à noite, 6 personagens, "PALANQUE"); teste A/B de cabeça (A, de base CC0, por shape keys; B, gerada por código, por troca de peças do rosto), as duas aprovadas no contrato técnico, com prancha de retratos em 96, 256 e 512 px.
  - **Medidas** (Radeon integrada, máquina dividida com outros programas): mapa de BH pronto em 0,5 a 0,8 s (meta: menos de 3 s); zoom e arraste de 160 a 233 FPS (meta: 60). O menu ficou em 31 a 36 FPS em qualidade cheia e 50 a 58 em leve: **não chegou a 60 em qualidade cheia**.
  - **Autocrítica:** a A parece criança e realista demais; a B chega mais perto das referências; nas duas as expressões mal se distinguem a 96 px, o cabelo parece capacete, corpos e roupas parecem sacos e as mãos, luvas. **Nenhuma cabeça foi declarada boa o bastante; quem julga é o autor.**
- **17/11, depois da fase-r-final, na main:**
  - **Régua da D-026 em faixas (D-057, D-060):** partido mais forte eleito de 65% a 90% no pequeno e no médio e de 40% a 60% no grande e na metrópole; sorteado de 5% a 25% (no médio, até 35%, efeito do quociente com 11 vagas); sem campanha de 0% a 1%. Linha de base com a amostra antiga: mais forte 73/84/46/49%, sorteado 19/39/11/12%, sem campanha 0%. Medir de novo depois do cansaço por veículo.
  - **Sorteado do médio** (`docs/balance/sorteado_medio.md`): nenhum parâmetro mexe só nele; metade do excesso era da amostra do BalanceSim (cada cidade jogava um só partido), que está sendo corrigida (cada cidade joga os dois); com as 10 cidades nos dois partidos, 32%. O candidato C (empenho e reconhecimento do médio) não foi aplicado.
  - **Nomes de bairro do OpenStreetMap (D-058):** 4.675 zonas urbanas trocaram o nome gerado pelo bairro do OSM, com crédito no mapa e ODbL registrada; nomes do IBGE intactos; ids iguais.
- **RL (branch `rl`, fora da main até as correções):** relógio de 6h a 22h na barra, linha do dia com marcas em 6h, 9h, 12h, 15h, 18h e 22h, painel Você fixo (turnos, estresse, caixa), zonas no painel Território, "Fazer agora" (sem turno livre hoje, vira "Fazer amanhã de manhã"; o terceiro turno do dia custa +3 de estresse), número flutuante na zona, ficha da zona flutuante em pilha com Esc. O mundo age todo dia (aprovado, D-059). O autor viu as capturas e pediu cinco correções antes da tag `fase-rl`: linhas finas soltas no mapa; legenda de "Minha força" de 0 a 100 com gradiente; ficha estreita ou encaixada, sem cobrir o resultado; número flutuante nítido; intenção de voto na pré-campanha pela estimativa da equipe, marcada como estimativa.
- **Depois da `fase-rl`, antes da RV-C (D-059):** mandato e prefeitura com relógio, linha do dia e "Fazer agora".
- **RV-A (branch `rva`, criado do `rl` com o protótipo):** mapa de base de Mantena sob as zonas translúcidas (quadras, ruas com nome, rios, rodovias, localidades), zoom da cidade à rua travado no município, luz de dia e de noite pelo relógio, crédito "Mapa de base: IBGE · © colaboradores do OpenStreetMap". Mantena pronta em 13 a 17 ms e a 277–280 FPS; Belo Horizonte em 0,21 a 0,27 s e a 199–201 FPS, na placa de referência (Ryzen 5 7520U com Radeon integrada, D-059). Direção aprovada; em andamento: noite visível (luz de rua, quarteirões com luz, rural escuro), "Partido líder" com intensidade pela margem, câmera abrindo no núcleo urbano. O mapa de base fica no cache, fora do Git, até a decisão da P-39.
- **Tags:** `fase-r1`, `fase-rd`, `fase-r2a`, `fase-r2b`, `fase-r3`, `fase-r4`, `fase-r-final`. Próxima: `fase-rl`.
- **Documentos novos:** auditoria, seção 17 do GDD, guia de arte (em vigor: paleta escura, Inter/Oswald/Source Serif 4, grade de 400 + mapa + 400 px, regras para nada cortar em 1366×768; proposta da RV por cima, a aprovar), roteiros de teste de cada parte e lista de pendências.

## 7. Decisões

**Registro de decisões (D-001 a D-035), uma linha cada:**
- D-001: ideologia dos partidos reais espelhados pela pesquisa de Bolognesi, Ribeiro, Codato e Silva (2025, *Opinião Pública*).
- D-002: partidos fundidos levam a média das siglas de origem; renomeados mantêm o escore.
- D-003: partido real sem classificação publicada entra com família sorteada.
- D-004: eixo econômico pelo escore publicado; costumes com variação sorteada; só famílias gerais para partidos reais.
- D-005: Amapá usa a Câmara e a Assembleia de 2022 (resultado de 2026 nulo na fonte).
- D-006: base do governo = coligação do presidente eleito; oposição = coligação do segundo colocado.
- D-007: prefeito "reeleito" em 2024 identificado pelo título de eleitor eleito em 2020.
- D-008: números de partidos fictícios evitam os números reais.
- D-009: o cenário Brasil 2026 é editável campo a campo, sem adicionar nem remover partidos.
- D-010: fundo eleitoral e tempo de TV calculados no Core pelas regras da lei.
- D-011: municípios sem resultado de 2024 ficam pendentes ou usam 2020; nenhum número inventado.
- D-012: 240 segmentos por município (zona × idade × renda × religião × escolaridade).
- D-013: afinidade partidária dos segmentos ajustada à força local de 2024.
- D-014: modelo de voto (utilidade, softmax, brancos e nulos, comparecimento) e pesquisas com viés simétrico.
- D-015: calibração contra os prefeitos de 2024 (erro médio de 11 pontos, vencedor certo em 72%).
- D-016: tom de pele dos NPCs pela composição de cor ou raça do Censo por UF, sem efeito mecânico.
- D-017: prenomes do IBGE por UF e década; sobrenomes por lista com pesos.
- D-018: telas montadas em C#; textos por `Tr()`.
- D-019: o jogo começa na semana 1 de 2028, já filiado.
- D-020: partido criado na criação de personagem entra como nanico.
- D-021: disputa com 6 partidos, 10 rivais nomeados e um "candidato coletivo" por partido.
- D-022: ritmo semanal, energia de 100, custos por porte, ganhos decrescentes. Substituído pela Fase R.
- D-023: 0 a 3 eventos por semana, sorteados com raridade e cooldown.
- D-024: urna, apuração com 12 parciais, saves (autosave, véspera da eleição, ironman).
- D-025: contornos simplificados da Malha Municipal 2025 (6,3 MB) para o mapa 2.5D.
- D-026: balanceamento da campanha (o eleitor só vota em quem conhece; rivais mais ativos por porte).
- D-027: mandato de vereador (Câmara, base, pauta, voto NPC, fidelidade com advertência, sanção e expulsão).
- D-028: transição de carreira e janela partidária só no fim do mandato.
- D-029: fundação de partido pela Lei 9.096 simplificada (566.265 assinaturas no Brasil 2026).
- D-030: candidatura a prefeito; idade mínima de 18 anos aferida em 15/8 para vereador.
- D-031: mandato de prefeito (orçamento por área, mínimos de saúde e educação, 7 indicadores, 17 políticas, secretarias, 7 crises).
- D-032: debate de TV com 3 rodadas e 6 cartas.
- D-033: população projetada e vagas de vereador pelo teto constitucional a cada eleição.
- D-034: Fase R criada a partir da auditoria; regras do Core mantidas.
- D-035: decisões do autor sobre o GDD 17.20, listadas abaixo.
- D-037: respostas do autor às pendências da R1; teto de 20 MB em `data/zonas/`; corujão estendido à Fase R inteira.
- D-038: escolhas da RD (finanças do DF pelo ente estadual, RAIS de estabelecimentos, homicídios por agressão, declarações implausíveis descartadas, eleitores por zona pela moradia).
- D-042 e D-048: escolhas da R3 e o balanceamento com 200 campanhas; D-041 e D-045 a D-047: respostas do autor (integração, retratos e mapa da RV, artista, nome "Palanque", autoria com colaboradores).
- D-043: retratos e personagens em bonecos 3D estilizados de formas suaves (não low-poly facetado), os mesmos dos retratos, do menu, das cenas e das posses; o que o protótipo mostrou e o que fica para o autor.
- D-044: mapa da cidade 2D detalhado e fiel ao lugar real (faces de logradouro, BC250, relevo se licenciado; zoom até a rua; zonas translúcidas por cima; luz de dia e de noite) e interface em janelas flutuantes; medidas do protótipo e o que fica para o autor.
- D-040: escolhas da R2b (equipe sem função vale o pior padrão, delegação a 50%, verba cobrada toda segunda, pré-campanha com teto, incoerência em 52 semanas, 7 tipos de objetivo).
- D-039: escolhas da R2a (presença de partida, alcance pela fatia da cidade, reunião só com liderança do público, evento dividindo cansaço com a caminhada, dificuldade dos rivais, como o BalanceSim mede a "melhor escolha da semana").
- D-049: zona rural por distritos e o mapa de cidade pequena; D-050: escolhas da R4 (indicadores com piso e teto nos percentis 1 e 99, parâmetros de sucessão, contas só fora de campanha, entressafra com 3 turnos).
- D-036: idade inicial de 17 a 45 anos (NPCs 25 a 45), 18 anos aferidos em 15/8 para vereador, profissões com idade mínima, Estudante, família coerente com a idade.
- D-051: critério de idade média dos vereadores nas Câmaras pequenas; D-052: reservada para o cansaço por veículo, registrada quando ele entrar na main.
- D-053: pacote de nomes privado, só para uso do autor, fora do repositório; nenhum nome real em dados, docs, código, testes ou commits.
- D-054 e D-055: depois da Fase R, quatro partes na ordem RL → RV-A → RV-C → RV-B, cada uma com tag e capturas no meio.
- D-056: escolhas da fase-r-final (tooltips, Opções, mapa de cidade pequena, testes de fumaça).
- D-057: régua da D-026 em faixas; nenhuma alavanca aplicada; medir de novo depois do cansaço por veículo.
- D-058: nomes de bairro do OpenStreetMap só onde o IBGE não tem bairro, com crédito e ODbL; o resto do OSM fora.
- D-059: respostas às capturas da RL (cinco correções, P-41 aprovada, mandato e prefeitura no relógio antes da RV-C) e à RV-A (noite, partido líder pela margem, câmera, pontos de interesse só se liberados, placa de referência); cansaço pelo caminho "baixar o pico".
- D-060: amostra do BalanceSim corrigida; candidato C não aplicado; sorteado do médio até 35%.

**Decisões do GDD 17.20, com a escolha do autor:**
1. Mapa da cidade em 2D plano: aprovado.
2. Zonas pelo Censo 2022 em `data/`: aprovado, com teto de 30 MB.
3. Nome de zona sem bairro oficial (distrito ou nome gerado): aprovado.
4. Religião e escolaridade por área de ponderação: aprovado.
5. Renda por zona estimada pela mediana: aprovado.
6. Eleitores e voto de 2024 por zona pelos locais de votação do TSE: aprovado, na RD.
7. Termos de uso: as fontes são usadas citando-as; o autor confere depois.
8. Ritmo do tempo: aprovado.
9. Regras de campanha, com correções:
   - rádio e TV fora da verba (art. 44), viram presença conquistada;
   - outdoor proibido (art. 39, § 8º) e trocado na criação;
   - impulsionamento pago nas redes é canal (art. 57-C);
   - limite de cabos pelo eleitorado (art. 100-A);
   - o resto é conferido na R3; o que é de memória fica marcado.
10. Limite de gastos pela tabela do TSE: aprovado.
11. Lista de bloqueio de veículos: aprovado.
12. Vacância no Executivo, com correção: morte ou renúncia seguem o padrão do art. 81 da CF, configurável; decisão da Justiça Eleitoral segue o Código Eleitoral, art. 224, §§ 3º e 4º. Um teste para cada caminho.
13. Alterada: tábua de mortalidade por UF, com a do Brasil como reserva.
14. Renovação de vereadores só informativa: aprovado.
15. Pacote de nomes reais: **não aprovado, suspenso**. As vagas fictícias seguem.
16. População reescalada pelas Projeções por UF: aprovado.
17. Homicídios pelo SIM: aprovado.
18. RAIS com brutos só no cache: aprovado.
19. Proteção social como índice do jogo: aprovado.
20. Aprovação pela variação: aprovado.
21. Fontes Inter, Oswald e Source Serif 4: aprovado.
22. Parâmetros de mortalidade como parâmetros de jogo: aprovado.

## 8. Pendências

- **Abertas com o autor em 17/11:**
  - P-39: licença das Faces de Logradouros, fonte de relevo e onde guardar o mapa de base de cada cidade;
  - P-40: julgar as cabeças A e B pela prancha, aprovar o ART_GUIDE da RV (Parte 1, seção 14) e as convenções do contrato da cabeça; qualidade leve. A placa de referência foi respondida (D-059);
  - P-42: zonas rurais com nome do OSM, como cumprir a ODbL na distribuição, crédito sempre ou só onde há nome do OSM, leitura de que distrito do IBGE é nome oficial;
  - P-45: pontos de interesse do OSM no zoom de rua;
  - pastas de trabalho antigas com arquivos gerados soltos (`rd-main`, `..planalto-teste`), à espera da limpeza.
- **Respondidas pelo autor (D-037):** P-05, P-06 e P-08 fechadas; P-04 adiada para a R3. **P-41, P-44 e a placa da P-40:** D-059 e D-060.
- **Novas, da R4:** P-30 (agenda por dia no mandato), P-31 (objetivos do mandato), P-36 (parâmetros a confirmar, o que ficou fora), P-35 (pacote de nomes privado, D-053, depois da `fase-r-final`). P-01, P-29 e P-34 (D-051) fechadas.
- **Novas, da R3:** P-26 (BalanceSim: par do pequeno e régua da D-026), P-27 (o que ficou para o autor: tabela do TSE, acórdãos, 15 ou 9 meses, lista de veículos), P-32 (sobras da interface), P-33 (ambiente compartilhado dos testes de fumaça).
- **Novas, da R2b:** P-20 (BalanceSim fora em parte dos portes), P-21 (caixa não comporta equipe e verba), P-22 (o que depende da R3), P-23 (sobras da interface).
- **Novas, da R2a:** P-15 (princípio a não passa em cidade grande e metrópole), P-16 (lint de rastro e eventos antigos), P-17 (números de jogo ainda no código), P-18 (caixa aperta nas cidades maiores), P-19 (sobras da interface; quintil ou intervalo no mapa).
- **Novas, da RD:** P-10 (votação de 2024 por zona não ligada), P-11 (eleitores por zona: onde se vota ou onde se mora), P-12 (parâmetros de jogo novos), P-13 (termos de uso das fontes não IBGE), P-14 (transferências e CAGED).
- **Do autor, com resposta:**
  - P-04: artigos de lei citados de memória nos tooltips do material de campanha;
  - P-05: a intenção por zona estimada (a da cidade repesada por idade e situação) basta até a R2a?;
  - P-06: termos de uso do IBGE;
  - P-08: 15 MB de atributos de zonas no repositório.
- **Da fase-r-final:** P-38 (opções sem efeito, Zona Sudoeste de Mesquita, testes lentos). P-02, P-03, P-09 fechadas.
- **Novas, depois da R4:** P-35 (pacote de nomes privado, D-053), P-37 (itens da RL fora da leitura mínima).
- **Da entrega da RV, decisões do autor:**
  - P-39 (substitui as opções da P-25): licença das Faces de Logradouros (não confirmada) e termos da BC250; fonte de elevação (Topodata, ANADEM, Copernicus ou MDE do IBGE, nenhuma confirmada) ou ficar sem relevo; como guardar o mapa de base: (b) pré-gerado e baixado por município (0,3 a 3 MB cada, 1 a 3 GB no país, fora do repositório, depende da licença), (c) só cidades pequenas no repositório com teto, ou (d) gerado pelo importador no computador do jogador. Gerar na hora a partir dos brutos (a) exigiria pacote novo no jogo.
  - P-40: julgar as cabeças A e B pela prancha (aprovando uma, o artista não é chamado; reprovando as duas, teste pago do artista); aprovar duas convenções do contrato da cabeça (sufixo numérico do Blender, como `expr_bravo.001`, ignorado; material `interior_boca` aceito); se a qualidade leve vira opção; a placa de vídeo-alvo dos 60 FPS, remedindo com a máquina livre; a licença dos Human Base Meshes na página oficial; os pontos em aberto da proposta do ART_GUIDE; em que parte entram os retratos 3D e as cenas de urna, apuração e estúdio de foto (proposta: RV-B).
- **Do Claude Code, mais à frente:** conferir no texto oficial os artigos de lei citados de memória antes de virarem configuração (R3 e R4).

## 9. Próximos passos ao abrir um chat novo

1. Colar este documento e dizer em que ponto o autor está (o que jogou, o que achou).
2. Se o Claude Code parou por limite de uso: mandar retomar o corujão (frase na seção 10).
3. Ler o relatório mais recente no topo do PROGRESSO.md. Conferir se a RL entrou com as cinco correções e a tag `fase-rl`, e jogar o `ROTEIRO_TESTE_RL.md`.
4. Para a RV: julgar as cabeças A e B pela prancha e ler a proposta do ART_GUIDE (cópias fora do repositório em `C:\dev\capturas-rv\`), e responder à P-39, à P-40, à P-42 e à P-45. Olhar as capturas e o vídeo da RV-A no branch `rva`.
5. Ver o resultado do cansaço por veículo com o pico da entrevista baixado e a régua medida de novo com a amostra corrigida.
6. Transformar as impressões em ajustes: por parte, o que manter, cortar ou mudar, e quais decisões tomar.
7. Revisar a lista de pendências e o registro de decisões; reverter o que não agradar.

## 10. Como pedir trabalho ao Claude Code

**Formato de prompt**, em seções com tags:
- `<contexto>`: o que ler primeiro (CLAUDE.md, GDD seção 17, ART_GUIDE, ROADMAP, DECISOES) e a situação atual;
- `<tarefa>`: o que fazer, numerado, concreto;
- `<restricoes>`: regras do CLAUDE.md e limites da tarefa (o que não tocar);
- `<verificacao>`: os comandos obrigatórios, testes específicos e checagens a fazer antes de entregar;
- `<entrega>`: commits (tipo e escopo), ROADMAP marcado, resumo pedido.

**Frases de retomada:**
- "continue de onde parou, seguindo o corujão da Fase R";
- "releia CLAUDE.md, ROADMAP, DECISOES, PENDENCIAS e PROGRESSO e continue a próxima parte pendente".

**Boas práticas que funcionaram:**
- pedir tudo concreto (nomes, números, critérios testáveis);
- separar o que é decisão do autor;
- exigir os testes de fumaça com janela, porque sem janela os cliques não são testados;
- pedir prints nas duas resoluções (1366×768 e 1920×1080);
- pedir que o Claude Code critique o próprio trabalho contra os princípios e os relatos da prova de imersão.

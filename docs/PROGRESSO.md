# Progresso

## Relatório para o autor (atualizado a cada parte)

**Estado em 2026-11-17, depois da `fase-r-final`.** Este bloco é reescrito ao fim de cada parte.

- **Tags prontas:** `fase-r1`, `fase-rd`, `fase-r2a`, `fase-r2b`, `fase-r3`, `fase-r4` e `fase-r-final`, todas com pendências.
- **Por qual tag começar a testar:** `fase-r-final`, com `docs/ROTEIRO_TESTE_R_FINAL.md` (10 minutos) e, para o mandato, `docs/ROTEIRO_TESTE_R4.md` (20 minutos). A `fase-r-final` traz as correções que você achou na criação de personagem jogando a `fase-r4` (traços, biografia, acentos), tooltips em todas as telas, a tela de Opções e o mapa de Mesquita na sede. A `fase-r-final` contém tudo das anteriores, o mapa de cidade pequena e as correções do seu teste da `fase-r1` (itens 1 a 9 da P-24). Acrescenta o mandato de vereador, a prefeitura e a entressafra na tela central, os indicadores reais e a renovação do mundo político. **Comece uma carreira nova:** saves anteriores à divisão rural por distritos são recusados com aviso.
- **BalanceSim de cada tag** (critério do princípio a: tipo de compromisso e canal de verba até 40% das semanas; par tipo e zona até 25%; no mandato, instrumento até 40%):

| Tag | Campanhas por porte | Tipo (pequeno / médio / grande / metrópole) | Par | Canal de verba | Outros critérios |
|---|---|---|---|---|---|
| `fase-r1`, `fase-rd` | — | sem BalanceSim | — | — | — |
| `fase-r2a` | 48 | 30,2% / 34,1% / **41,4%** / **59,8%** | 22,6% / 15,5% / 12,6% / 11,5% | (não existia) | — |
| `fase-r2b` | 48 | 27,0% / 35,9% / 36,3% / **60,6%** | **29,8%** / 18,1% / 14,2% / 12,8% | **45,0%** / **49,0%** / **49,5%** / 39,2% | decisões com troca 1,8 a 2,4 por semana (passa) |
| `fase-r3` | **200** | 27,9% / 27,2% / 27,6% / 36,9% | **30,5%** / 22,6% / 19,0% / 17,5% | 33,6% / 30,2% / 37,0% / 29,1% | decisões com troca 2,7 a 2,9 (passa); contas: limpa 0,00%, "por fora" 36,2% (passa); sem campanha nunca eleito (passa); régua da D-026 **não fecha** (P-26) |
| `fase-r4`, `fase-r-final` | **200** | 27,8% / 26,8% / 27,5% / 37,2% | **30,2%** / 22,0% / 19,0% / 17,7% | 33,1% / 31,3% / 35,4% / 29,9% | decisões com troca 2,7 a 2,9 (passa); contas: limpa 0,00%, "por fora" 35,6% (passa); sem campanha nunca eleito (passa); **mandato: instrumento mais escolhido 28,1% / 29,8% / 28,9% / 28,8% (passa)**; régua da D-026 **não fecha** (P-26) |

  Em negrito, fora do critério. O par do porte pequeno é a entrevista no veículo de maior alcance, não uma zona; as tags saíram com ele pendente, como autorizado. Na `fase-r4`, a régua ficou pior que na `fase-r3` em três portes por causa do mapa de cidade pequena, não da R4 (P-26).
- **Seus pedidos desta rodada:**
  - **Cansaço por veículo:** está pronto e testado no branch `cansaco-veiculo`, mas **não foi integrado**. O par do pequeno cai para 19,0%, só que o tipo sobe para 82,5%, porque a entrevista vira a melhor opção de quase toda semana. Há três caminhos para você escolher (P-26).
  - **Régua da D-026:** o relatório com a tabela, os parâmetros, o mecanismo e as alavancas testadas fica em `docs/balance/regua_d026.md` quando a medição terminar. Nada foi ajustado; a decisão é sua.
  - **Blender:** importação de teste conferida; a P-29 está fechada e o protótipo da RV vai montar as duas cabeças do teste A/B.
- **Atenção:** rodadas de fumaça da R4 anteriores ao perfil isolado gravaram na pasta real do jogo (`%APPDATA%\Godot\app_userdata\Planalto\saves`). Se você tinha um autosave seu ali, ele pode ter sido trocado (P-33). Nada foi apagado.
- **Pendências que precisam de você** (docs/PENDENCIAS.md):
  - **P-26:** régua da D-026 (qual alavanca, se ela continua valendo) e o caminho do cansaço por veículo.
  - **P-36:** piso e teto dos indicadores e os prazos de sucessão a confirmar. A P-34 (idade média nas Câmaras pequenas) foi fechada pela D-051.
  - **P-27:** tabela de limites de gastos do TSE (baixar à mão), acórdãos não lidos, 15 meses (Constituição) ou 9 meses (Código Eleitoral) para vaga de vereador sem suplente, lista de bloqueio de veículos escrita de memória.
  - **P-30 e P-31:** agenda por dia e objetivos no mandato (ficaram para depois, como você decidiu).
  - **P-25:** ruas do IBGE no mapa (agora parte da RV) e a licença.
  - **P-11, P-12 e P-13:** eleitores por zona onde se mora; parâmetros novos da RD; termos das fontes que não são do IBGE.
  - **P-19, P-23 e P-32:** quintil ou intervalo no mapa; o que ficou fora do ART_GUIDE nas telas.
  - **P-10 e P-14:** votação de 2024 por zona; transferências e CAGED.
- **Decisões conservadoras tomadas sozinho, a revisar** (DECISOES D-038 a D-042, D-048 a D-050):
  1. Reunião com apoiadores só onde há liderança conhecida daquele público; apoio de conversa com teto.
  2. Evento da cidade divide o cansaço com a caminhada; alcance de um turno limitado a 10 vezes a base.
  3. Pedido de dinheiro de liderança aceito vira esqueleto; o jogo avisa e não julga.
  4. Equipe: sem marqueteiro o rendimento é 1,0; delegado rende 50% e quem recebe lembra.
  5. Verba: na pré-campanha só Redes sem impulsionamento e material institucional, com teto.
  6. Plataforma: incoerência conta as mudanças em 52 semanas; posição desconhecida vale a do partido.
  7. Ataque: o dano é penalidade separada da imagem; responder não pausa (vencido o prazo, vale o silêncio).
  8. Balanceamento da R3: canais e rivais recalibrados (D-048); a régua da D-026 não fechou.
  9. Rural por distritos: 200 moradores mínimos por zona e até 4 zonas extras por município (D-049).
  10. R4: piso e teto dos indicadores nos percentis 1 e 99; posse do suplente em 7 dias e eleição da cadeira em 90; eleição suplementar resolvida sozinha; contas julgadas só fora de campanha (D-050).
  11. As da RD: declarações implausíveis descartadas; Brasília pelo ente estadual; RAIS de estabelecimentos.

## fase-r-final — Tooltips, Opções, mapa de cidade pequena e correções da criação (2026-11-17, com pendências)

**O que entrou**
- **Correções da criação achadas pelo autor na `fase-r4`:**
  - traços: os 2 sorteados com o personagem ficavam escondidos e contavam, então "Começar carreira" não liberava; a mensagem agora diz quantos e quais estão marcados;
  - fundo do traço marcado com o mouse em cima;
  - biografia: acompanha idade, partido, profissão e cidade até ser editada à mão, com "pelo"/"pela" pelo nome do partido e frase própria para estudante;
  - atributos com acento.
- **P-02:** tooltips em três partes e links do glossário no menu, Novo Mundo, criação (5 abas e partido próprio), seletor de cidade, mapa do estado, debate, urna, apuração e fundação de partido. Opções que ainda não mudam a simulação dizem isso (P-38).
- **P-03:** tela de Opções pelo menu e pela tela central, aba Tempo com cada pausa (cinco novas desligáveis) e "Religar as dicas".
- **P-09:** fechada; Mesquita (MG) abre na sede, e os modos Minha força e Partido líder dão contraste.
- **P-33:** cliques dos testes de fumaça esperam o controle pronto e conferem que chegaram, sem repetir; carreiras de teste com semente fixa; um teste do Core que dependia do hash de texto do .NET ficou estável.

**Números dos testes**
- 1.024 testes no Core, todos passando; 88 no importador; DataValidator e EventLint limpos; build do Godot limpo.
- 32 alvos de fumaça com janela, todos com código 0 na mesma rodada, com `--perfil=main`: os 29 da R4 mais `opcoes`, `tracos` (traços clicados com o mouse, biografia, acentos) e Mesquita no `mapa_pequeno`.
- Capturas em `docs/referencias/r-final/`.

**BalanceSim:** a parte não mexe em campanha nem em parâmetros; vale o da `fase-r4`.

**As 5 coisas de que menos gostei no meu próprio trabalho**
1. **O bloqueio dos traços passou por quatro partes sem teste com o mouse:** o alvo `creator` sorteava tudo antes de conferir e escondia o defeito.
2. **A biografia errada voltou porque o item 5 da R1 foi conferido só no santinho;** agora o alvo `tracos` confere a biografia também.
3. **A Zona Sudoeste de Mesquita** junta sede e vila; corrigi só o enquadramento (P-38).
4. **Os testes de fumaça ficaram mais lentos:** `explicacao` e `layout` jogam uma carreira de prefeito até a urna.
5. **Opções sem efeito ainda à vista** (Dificuldade, Realismo jurídico, Cúpula, estatuto do partido): o tooltip avisa, mas é ruído na tela.

## R4 — Mandatos na nova tela, com gestão nova (2026-11-16, com pendências)

**O que entrou**
- **Tela central para o mandato, a prefeitura e a entressafra** (fecha a P-01 e o item 9 da P-24): mesma moldura da campanha, trilho de áreas, mapa no centro; as telas antigas foram removidas. As previsões da prefeitura são montadas no Core.
- **Vereador (17.14):** gabinete com assessores nomeados e quem lembra; indicação, emenda com obra no mapa, fiscalização com achados e três saídas, requerimento de informação; contagem nominal com negociação de voto; instrução permanente de voto; Mesa e CPI; base que cobra as promessas do Livro, com o caminho para cumprir.
- **Prefeitura (17.15):** secretariado com competência e coalizão; obras com licitação (modalidades, prazo e base legal) e prazo no mapa; inauguração vedada nos três meses antes da eleição (art. 77 da Lei 9.504); folha com as faixas de alerta, prudencial e limite e a mesa de negociação (art. 22 da LRF); mínimos de saúde e educação; repasse à Câmara (art. 29-A); convênios; Câmara que negocia; crises com causa.
- **Indicadores reais (17.17):** 11 indicadores em unidade real, com fonte, estimativa marcada, variação no mandato, comparação com o estado e "por que mudou".
- **Renovação do mundo político (17.16):** idade, morte pela tábua da UF, saídas e entradas, luto oficial, suplente e vice pela regra, vacância no Executivo pelos arts. 79 a 81 da CF e pelo art. 224 do Código Eleitoral.
- **Eventos de mandato** legislativo e executivo; contas de campanha julgadas no mandato e na entressafra, com sucessão real na cassação.
- **Entressafra:** agenda fora de campanha com caminhada, visita, entrevista e posição; a ida à pré-campanha leva a presença.
- **Integração com a main:** o save confere as zonas do governo e da entressafra; a imagem aparece em faixa nomeada no mandato e na prefeitura; o mapa usa o enquadramento do núcleo urbano; o BalanceSim ganhou a opção `--mundo`.

**Números dos testes**
- 1.014 testes no Core (778 na R3), dos quais 798 rápidos rodam em cerca de 30 s; 88 no importador; DataValidator e EventLint limpos; build do Godot limpo.
- 29 alvos de fumaça com janela, com `--perfil=main`: os 24 anteriores mais `gabinete`, `obras`, `fiscal`, `indicadores` e `entressafra`; `mandate` e `prefeitura` passaram a exercitar a tela central. Na rodada da main, 28 passaram e o `explicacao` falhou uma vez: a etapa "licitação" na ficha da obra saía sem o link do glossário, e só aparecia quando a obra estava nessa etapa ao abrir a ficha. Corrigido no jogo; depois disso `explicacao` passou 3 de 3 seguidas, e `obras`, `gabinete` e `layout` também passaram.
- Capturas em 1366×768 e 1920×1080 em `docs/referencias/r4/`, em cidade pequena e metrópole.
- Renovação em 40 anos e 8 municípios: nenhuma cadeira vaga além da regra (maior tempo, 0 dias); toda morte de titular com o desfecho da regra; idade média +1,0 ano no conjunto (nas Câmaras de 9 a 11 cadeiras, pela média de 20 sementes: D-051). Indicadores: ida e volta sem perda (erro relativo de 6e-15) e 40 anos dentro dos limites.

**BalanceSim**
- **Campanha** (200 por porte; `docs/balance/r4.md`): tabela no Relatório acima. A R4 não muda a campanha; tipo, canal, decisões, contas e "sem campanha" passam nos quatro portes; o par do pequeno segue pendente (30,2%, não piorou).
- **Mandato** (24 mandatos por porte, mundo ligado): instrumento mais escolhido em 28,1%, 29,8%, 28,9% e 28,8% das semanas (limite 40%); os seis instrumentos aparecem como melhor escolha; nenhum mandato com erro.

**As 5 coisas de que menos gostei no meu próprio trabalho**
1. **O mandato ainda é por semana, não por dia:** inaugurar e fiscalizar acontecem no clique, sem lugar na agenda (P-30).
2. **Os objetivos do mandato são só o Livro:** falta o gerador de objetivos que a campanha tem (P-31).
3. **Os caminhos raros de sucessão nunca aparecem sozinhos:** em 40 anos toda vaga teve suplente; a eleição indireta só existe nos testes.
4. **A tela do prefeito é densa:** nove áreas no trilho, e o cartão das contas fica longo no painel de 400 px; na metrópole o mapa encosta nos botões de modo.
5. **O robô do BalanceSim de mandato julga por uma conta minha** (pontos de presença por linha da previsão); o critério passa, mas mede o meu juízo tanto quanto o jogo.

**Autocrítica de imersão (GDD 17.21)**
- **Relato 3 (prefeitura em crise), já acontece:** crise num cartão com a causa; mesa de negociação com o pedido e a folha; vedações citando o artigo; obra atrasada no mapa com "+N sem". **Falta:** a crise vir de uma escolha antiga nomeada ("o corte da saúde em março"); o secretário com agenda própria agir contra você na Câmara.
- **Relatos 1, 2 e 4, no mandato:** a base cobra com nome, prazo e caminho; o luto chega num cartão sóbrio e suspende a sessão. **Falta:** a cobrança acontecer num lugar e num dia, não só num cartão (P-30).

## R3 — Rivais com estratégia, mídia local e doadores (2026-11-16, com pendências)

**O que entrou**
- **Rivais:** perfil, plano semanal com zonas-alvo e postura, gatilhos (pesquisa, entrada no reduto, ataque, perda de liderança), resposta a ataque pelo temperamento, pactos e dobradinha, ficha do rival com plano percebido e confiança da leitura, painel da chapa ("Quem puxa quem"); atacar, investigar e propor pacto com previsão.
- **Mídia local:** veículos fictícios com tipo, linha, dono e jornalista; feed a partir de fatos, com tom pela linha e marca de boato; tema em alta; entrevista num veículo escolhido; sugerir pauta; anúncio na imprensa escrita com o limite do art. 43 (rádio e TV bloqueados pelo art. 44); horário eleitoral gratuito com o tempo do partido.
- **Ataques contra você, com prazo:** cartão, faixa com a contagem e cinco respostas com previsão (direito de resposta só com advogado).
- **Doadores e contas:** fontes (fundo do partido, pessoa física com teto de 10% da renda, recursos próprios, financiamento coletivo desde 15 de maio, "por fora"), limite de gastos, painel Contas com risco e fatores, esqueletos com "quem sabe" e chance de vir à tona, prazos de prestação, julgamento depois da eleição e representação que pode cassar o diploma.
- **Eventos:** os 32 eventos reescritos e ligados a pessoas, zonas e fatos; 5 eventos dinâmicos disparados por fatos; o cartão mostra pessoas, zona e veículo como links. O EventLint cobra rastro (lembrança ou Livro), troca e texto de morte; o CONTENT_GUIDE ganhou as variáveis de zona, liderança, pessoa e veículo e essas regras.
- **Lei conferida no texto oficial** (três leituras; `docs/referencias/LEIS_CONFERIDAS.md`) antes de virar configuração: `CampaignFinanceLawConfig`, `PropagandaLawConfig`, `ChannelLawConfig` conferido.
- **Mapa:** modo Disputa, etiquetas clicáveis dos rivais, tema em alta por zona. **Enciclopédia:** 24 verbetes e 3 artigos.
- **Testes de fumaça com perfil isolado** (`--perfil`, `user://smoke/<perfil>/`) e o alvo `perfil`.

**Números dos testes**
- 778 testes no Core (673 na R2b), dos quais os rápidos rodam em cerca de 10 s; 76 no importador; DataValidator e EventLint limpos; build do Godot limpo.
- 23 alvos de fumaça com janela, todos com código 0 na mesma rodada, com `--perfil=main`: os 19 anteriores mais `rivais`, `midia`, `contas` e `perfil`.
- Capturas em 1366×768 e 1920×1080 em `docs/referencias/r3/` (46 arquivos), examinadas em duas rodadas.
- Campanha de 40 semanas sem comando (princípio b), resposta a ataque em até 14 dias, nenhuma notícia sem fato, boato sempre marcado, nenhum nome de veículo na lista de bloqueio: testes do Core.

**BalanceSim** (200 campanhas por porte, 10 cidades; `docs/balance/r3.md`): tabela no Relatório acima. Canal de verba e tipo de compromisso passam nos quatro portes pela primeira vez; contas e "sem campanha nunca" passam; o par do porte pequeno (entrevista) e a régua de dificuldade da D-026 não fecham (P-26).

**As 5 coisas de que menos gostei no meu próprio trabalho**
1. **A dificuldade não bate com a régua.** O partido mais forte se elege menos do que devia e o sorteado, mais; não achei alavanca que mexa num sem mexer no outro.
2. **O par do porte pequeno é sempre a entrevista na rádio comunitária.** Em cidade pequena, com dois veículos, a melhor jogada da semana se repete.
3. **Textos com gênero fixo** nas previsões ("ele não ataca você") com candidatas e jornalistas mulheres na tela.
4. **Uma faixa de decisão para tudo:** com dois prazos abertos (ataque e oferta), a tela mostra um.
5. **Testes de fumaça frágeis sob carga:** quatro alvos falharam de forma intermitente logo depois de cliques simulados quando vários agentes rodavam juntos; com a máquina livre passam, mas o clique precisa ficar mais firme.

**Autocrítica de imersão (GDD 17.21)**
- **Relato 1, já acontece:** veículo com nome e linha noticiando fato da cidade; tema em alta com +30% na previsão e no mapa; marcador do rival que abre a ficha. **Falta:** o tema em alta raramente aparece na semana 2 (precisa de uma semana de notícias); cidade pequena tem só dois veículos.
- **Relato 2, já acontece:** "faltam N votos" no painel da chapa; ataque com prazo e chance por opção; direito de resposta pelo advogado; barra do limite de gastos. **Falta:** tesoureiro e coordenadora disputando o limite; dobradinha oferecida pela candidata a prefeita com conflito de agenda; a prova nomeada no contra-ataque.
- **Relato 4, já acontece:** financiamento coletivo com a data; oferta "por fora" recusável com previsão; entrevista num veículo escolhido sobre um tema. **Falta:** o pedido da liderança amarrado a um veículo ("diga isso na Rádio Vale").
- **Relato 3** (prefeitura em crise) é da R4.

## R2b — Equipe, verba, plataforma e objetivos (2026-11-16, com pendências)

**O que entrou**
- **Equipe:** cinco funções (coordenação, marketing, redes, tesouraria, jurídico), candidatos com retrato, vínculo, qualidade em faixa, salário e lealdade; previsão antes de contratar e de demitir, com quem vai lembrar (inclusive o parente preterido); conselho da semana do coordenador; brigas e pedidos da equipe como decisão no cartão; demitido de lealdade baixa que conhece esqueleto pode vazar.
- **Delegar:** "Mandar representante" na marcação; rende 50%, não ocupa o turno, e quem recebe lembra que você não foi.
- **Verba por canal:** Rua, Redes, Material gráfico e Cabos, cada um com alvo, retorno decrescente (dobrar rende cerca de 41% a mais), disputa com a verba dos rivais no mesmo alvo e saturação; o comício depende da verba de Rua; modo "Verba" no mapa; resultado da semana passada explicado.
- **Lei em configuração:** limite de cabos pelo eleitorado (art. 100-A), impulsionamento só identificado e só na campanha (art. 57-C), nenhum gasto possível com rádio, TV, outdoor ou brinde. Os verbetes de pré-campanha, horário eleitoral e propaganda foram reescritos pelo texto conferido (`docs/referencias/LEIS_CONFERIDAS.md`) e perderam a marca "a conferir".
- **Plataforma:** posição em 10 temas, até 3 bandeiras, posição conhecida pelo eleitor (falar do tema em entrevista, gravação e comício aumenta), custo de mudar com incoerência, promessas a zona ou grupo com credibilidade; tudo entra no Livro.
- **Objetivos:** de 1 a 3 na coluna da direita, de 7 tipos, com condição, medida de hoje, prazo e origem; fixar e dispensar; o cumprido vira cartão no feed.
- **Da P-19:** "Repetir a semana anterior", aviso de estresse acima do limite e a célula da agenda sem corte.
- **BalanceSim ampliado:** o robô contrata, reparte verba, se posiciona, promete e delega.

**Números dos testes**
- 673 testes no Core (eram 581 na R2a); 76 no importador; DataValidator limpo, com o lint de decisões (ganho e custo de naturezas diferentes); build do Godot limpo.
- 19 alvos de fumaça com janela, todos passando: os 15 anteriores mais `equipe`, `verba`, `plataforma` e `objetivos`.
- Capturas em 1366×768 e 1920×1080 em `docs/referencias/r2b/` (29 arquivos), examinadas em duas rodadas de correção.
- Dobrar a verba de um canal rende entre 35% e 45% a mais; verba de rival no mesmo canal e zona reduz o efeito (testes do Core).
- Em qualquer dia há de 1 a 3 objetivos; 6 tipos ou mais em toda campanha simulada (o princípio h pede 4).
- Campanha inteira jogada sozinha em 3 sementes por porte, sem erro.

**BalanceSim** (48 campanhas por porte em 8 cidades; entre parênteses, a R2a)

| Porte | Tipo de compromisso (até 40%) | Par tipo e zona (até 25%) | Canal de verba (até 40%) | Decisões com troca por semana (1 ou mais) | Eleito no partido mais forte | Turnos forçados |
|---|---|---|---|---|---|---|
| Pequeno | 27,0% (30,2%) | **29,8%** (22,6%) | **45,0%**, Cabos | 2,37 | 22 de 24 (20) | 15,3% (7,7%) |
| Médio | 35,9% (34,1%) | 18,1% (15,5%) | **49,0%**, Rua | 2,20 | 24 de 24 (24) | 54,9% (32,3%) |
| Grande | 36,3% (41,4%) | 14,2% (12,6%) | **49,5%**, Rua | 1,98 | 23 de 24 (24) | 54,8% (38,4%) |
| Metrópole | **60,6%** (59,8%) | 12,8% (11,5%) | 39,2%, Rua | 1,82 | 9 de 24 (16) | 60,9% (48,8%) |

Passa: decisões com troca em todos os portes; tipo de compromisso em pequeno, médio e grande (o grande passou a fechar: 41,4% → 36,3%). Não passa: canal de verba em três portes, par no pequeno, tipo na metrópole (P-20). De novo 48 campanhas por porte, não 200.

**As 5 coisas de que menos gostei no meu próprio trabalho**
1. **O dinheiro não fecha.** Com folha e verba o robô passa mais da metade dos turnos arrecadando nas cidades médias e maiores, mesmo com os preços pela metade do GDD. Só os doadores da R3 resolvem, e até lá a R2b é jogada com caixa apertado demais.
2. **A verba tem um canal dominante.** Cabos (pequeno) e Rua (médio e grande) são o melhor destino em quase metade das semanas da campanha oficial; três rodadas de ajuste não bastaram.
3. **Os números são pequenos e pouco palpáveis.** A previsão fala em "+0,5 ponto de apoio" e "+0,43 de presença"; para vereador, mudar de posição dá "de −0,01 a −0,01" na cidade. Falta traduzir em votos.
4. **O painel de 400 px não comporta as previsões.** Verba, mudança de posição e Campanha (na primeira visita) pedem rolagem; os Objetivos tomam o dobro da altura do guia.
5. **A equipe ainda fala pouco.** Só o coordenador aconselha; marqueteiro, tesoureiro e advogado não opinam, e o jogo não pausa para apresentar os candidatos a coordenador como no relato 1.

**Autocrítica de imersão (GDD 17.21)**
- **Relato 1, já acontece:** três objetivos na coluna; candidatos com faixa de qualidade, salário, lealdade e vínculo; a previsão avisa que o parente preterido vai lembrar.
- **Relato 1, falta:** a pausa que apresenta os nomes do coordenador (hoje só um objetivo empurra); ano e grau no vínculo; o preterido em Pessoas; a rádio, o tema em alta e o anúncio do prefeito (R3); o mapa "quase todo cinza".
- **Relato 2, já acontece:** "a sua fatia rende X%" contra a verba dos rivais; cabos onde só você está; o Livro com as datas das posições; comício que depende da Rua.
- **Relato 2, falta:** barra do limite de gastos, tesoureiro e coordenadora discutindo o gasto, alguém usando a mudança de posição contra você, direito de resposta, painel da chapa (quase tudo R3).
- **Relato 4, já acontece:** a briga entre coordenadora e tesoureiro, com o cartão e a lealdade nova; a previsão por zona antes de mudar de posição; "X% conhecem a sua posição"; o objetivo cumprido que sai e dá lugar a outro.
- **Relato 4, falta:** reação da direção do partido à posição; veículo na entrevista; financiamento coletivo e doadores (R3).

## R2a — Território, agenda, lideranças e Livro de compromissos (2026-11-16, com pendências)

**O que entrou**
- **Território na simulação:** presença de cada candidato por zona (0 a 100), com decaimento de 0,6 por semana nas zonas sem compromisso e 15% de transbordo para as vizinhas; os 240 segmentos repartidos pelas zonas (idade, renda, religião e escolaridade de cada uma, fechando com o município segmento a segmento); voto calculado zona a zona, e o da cidade é a soma; demanda da zona em unidade real; reconhecimento = média das presenças.
- **Agenda com lugar e dia:** 9 tipos de compromisso em grade de 7 dias por 3 turnos, com previsão antes de marcar (faixa, fatores e cansaço), resultado explicado depois, chuva prevista, eventos da cidade que só existem no dia (os religiosos na proporção do Censo do município) e convites com dia e turno fixos. Saem a energia semanal e as 6 ações.
- **Pessoa com memória e Livro:** um modelo de pessoa com interesses, temperamento e até 20 lembranças datadas (as de peso 3 não se apagam); Livro com promessas, dívidas, posições e esqueletos, que atravessa a eleição e o save.
- **Lideranças:** de 6 a 30 por cidade, com zona, público, influência, interesse e pedido; visitar revela o pedido; aceitar, negociar, ouvir sem se comprometer ou recusar gravam lembrança; apoio por zona; rivalidades; deriva sem contato; rivais que as procuram e aparecem no feed.
- **BalanceSim:** robô que marca o compromisso de maior ganho previsto de votos, com relatório por porte (`docs/balance/r2a.md`).
- **Interface:** área Agenda, modo "Minha força", marcadores no mapa, Ficha da zona, área Pessoas com Ficha da pessoa, cartão do pedido de liderança com previsão e "quem vai lembrar", resultado explicado no feed, Livro na Enciclopédia, 12 verbetes novos, pausas novas e dicas de primeira vez.

**Números dos testes**
- 581 testes no Core (eram 496 na RD); 76 no importador; DataValidator limpo; build do Godot limpo.
- 15 alvos de fumaça com janela, todos passando: os 12 anteriores mais `agenda`, `zona` e `lideranca`.
- Capturas em 1366×768 e 1920×1080 em `docs/referencias/r2a/` (24 arquivos), examinadas em duas rodadas de correção.
- Princípio g (cansaço): o segundo compromisso igual rende 62% e o terceiro, 45%; a previsão mostra o fator.
- Princípio f: em 1.000 execuções, 100% dos resultados dentro da faixa prevista.
- Campanha inteira jogada sozinha em 3 sementes por porte, com save e load no meio, terminando igual à sem salvar.

**BalanceSim** (48 campanhas por porte em 8 cidades; critério: 40% por tipo, 25% por par)

| Porte | Tipo com a opção de maior ganho da semana | Par (tipo, zona) mais repetido | Eleito no partido mais forte | Eleito em partido sorteado |
|---|---|---|---|---|
| Pequeno | 30,2% | 22,6% | 20 de 24 | 5 de 24 |
| Médio | 34,1% | 15,5% | 24 de 24 | 5 de 24 |
| Grande | **41,4%** (fora) | 12,6% | 24 de 24 | 10 de 24 |
| Metrópole | **59,8%** (fora) | 11,5% | 16 de 24 | 2 de 24 |

O critério passa em pequeno e médio e não passa em grande e metrópole (P-15). Não rodei as 200 campanhas por porte do critério: foram 48, por tempo de máquina.

**As 5 coisas de que menos gostei no meu próprio trabalho**
1. **O princípio a não fecha nas cidades grandes.** O cansaço empata os tipos, mas a caminhada em zona nova ganha por um fio quase toda semana. Na prática o robô ainda caminha em 49% a 62% dos turnos nessas cidades.
2. **Dinheiro mal resolvido.** Nas cidades médias e maiores o robô passa de um terço a metade dos turnos arrecadando (P-18).
3. **O mundo ainda quase não se mexe sozinho.** Os rivais só "fizeram campanha em X"; não há mídia, tema em alta nem rival com plano (R3).
4. **Uma liderança só faz um pedido,** e as promessas de obra não têm como ser cumpridas antes do mandato; "posições assumidas" no Livro fica vazio até a R2b.
5. **Marcar a semana dá trabalho:** 3 ou mais cliques por compromisso, sem repetir semana; e o estresse chega a 100 sem consequência visível.

**Autocrítica de imersão (GDD 17.21)**
- **Relato 1, já acontece:** o mapa abre em "Minha força" com a zona natal acima das outras; a feira de sábado existe como evento da cidade; a visita traz o pedido, e "Veio, ouviu e não se comprometeu" (+1) fica na ficha; rivais aparecem no feed e no mapa sem o jogador agir.
- **Relato 1, falta:** os três objetivos, o coordenador a contratar e o primo preterido (R2b); a rádio, o tema em alta e o anúncio do prefeito (R3); o rival no "culto de domingo", com ficha; o mapa "quase todo cinza".
- **Relato 4, já acontece:** presença desigual por zona; o aviso de cansaço antes de confirmar, com ordinal e percentual; aliada com lembranças; liderança que muda de lado, com pausa; promessa no Livro com prazo.
- **Relato 4, falta:** segundo pedido de quem já é aliado; posição pública com custo (R2b); briga de equipe e financiamento coletivo (R2b, R3); o vereador que desiste e abre a base (R4); objetivos que trocam.
- Das seis coisas que deveriam mudar a melhor jogada entre a semana 2 e a semana 20, a R2a entrega o mapa, o cansaço e as pessoas (só lideranças), parte do Livro e parte do mundo.

## RD — Dados reais de partida (2026-11-16, com pendências)

**O que entrou**
- **Indicadores reais** de todos os municípios em `data/indicadores_reais/` (6,3 MB): PIB e valor adicionado por setor (IBGE, 2023), emprego formal (RAIS 2025), finanças (SICONFI: receita corrente, despesa por função, RCL e despesa com pessoal), IDEB dos anos iniciais (INEP 2025, com a série), mortalidade infantil, homicídios e leitos (SIM, SINASC e CNES), saneamento (Censo 2022). Contexto das UFs em `_ufs.json` e economia nacional em `data/economy/start.json`.
- **Lacunas registradas:** 2.681, cada uma com a regra aplicada (2.323 de mortalidade infantil pela região imediata; 251 de IDEB; 70 de finanças; 35 de despesa com pessoal; 1 de PIB e 1 de saneamento, do município criado depois do Censo).
- **Receita real na prefeitura:** `ExecutiveEngine.AnnualRevenue` usa a receita corrente declarada; sem dado, vale a estimativa da D-031.
- **Mortalidade e população:** tábuas por UF e Brasil em `data/demography/` (a probabilidade recomposta de cada faixa é igual à publicada) e projeção municipal reescalada pela projeção de cada UF.
- **Zonas:** eleitores pelos locais de votação do TSE em 5.361 municípios e religião e escolaridade pelas áreas de ponderação em 16.592 zonas; teto de 20 MB em `data/zonas/` (16,8 MB).
- **Importador:** leitor de .xlsx, leitor de .dbc do DATASUS (descompressão "implode" portada do zlib/contrib/blast), download por FTP, baixador retomável do SICONFI, tudo sem pacote novo. Brutos só no cache (cerca de 9 GB).
- **Na tela:** F1 → Este mundo mostra os números oficiais de partida do município em foco, com unidade, fonte, ano e a marca de substituição.

**Números dos testes**
- 496 testes no Core (eram 455), 76 no importador (eram 28); DataValidator limpo (108 arquivos); build do Godot limpo.
- 12 alvos de fumaça com janela, todos passando. A RD não acrescenta alvo.
- Finanças: declaradas em 5.504 de 5.571 municípios; 56 pelo exercício anterior; 11 estimadas pela mediana do porte na UF.
- Totais por UF: a soma do PIB dos municípios confere com o total da UF (diferença só de arredondamento) e a dos domicílios é igual, sem tolerância.
- Conferência de leitura: tábua do Brasil aos 40, 60 e 80 anos igual aos seis valores do GDD; Selic 13,75%, câmbio R$ 5,6518 e dívida de 82,95% e 70,00% do PIB, como no GDD.

**BalanceSim:** não se aplica à RD (só dados).

**As 5 coisas de que menos gostei no meu próprio trabalho**
1. **Os números ainda não mudam o jogo.** Só a receita da prefeitura usa o dado real; os indicadores continuam de 0 a 100 até a R4.
2. **Eleitores por zona saíram do jeito aprovado e não servem ao modelo** (P-11): local de votação não é moradia. Tive de escolher sozinho o que entra no voto.
3. **Três itens ficaram de fora:** votação de 2024 por zona (P-10), transferências e CAGED (P-14).
4. **Mortalidade infantil pela região imediata em 2.323 municípios** (42%): a regra dos 300 nascidos vivos é minha e esconde a taxa própria de muita cidade pequena.
5. **Não há captura de tela desta parte** e o cartão da Enciclopédia é uma lista corrida de números, sem a ficha de município que o roteiro antigo previa.

**Autocrítica de imersão (GDD 17.21):** a RD não acrescenta nada aos relatos; ela dá a base do relato 3 (folha em % da receita corrente líquida, receita real, obras) que a R4 vai usar.

## Relatório da manhã (corujão da Fase R, 2026-11-16) — R1

**Partes concluídas:** só a **R1**, tag local `fase-r1`, **com pendências** (docs/PENDENCIAS.md). RD, R2a, R2b, R3 e R4 não começaram. Parei de propósito: preferi entregar a R1 testada, olhada em captura e corrigida em duas rodadas, a abrir a RD pela metade.

**O que jogar, nesta ordem:**
1. `docs/ROTEIRO_TESTE_R1.md` (20 minutos). Comece pela criação com 17 anos e o material de campanha novo; depois a tela central numa cidade média que você conheça e numa cidade pequena.
2. Se sobrar tempo, jogue uma campanha inteira em 4x e anote onde o jogo parou sozinho e se parou demais.

**As 5 coisas de que menos gostei no meu próprio trabalho:**
1. **O jogo ainda é raso.** A R1 troca a tela, mas as 6 ações continuam com efeito igual em qualquer zona; "Redes sociais" segue sendo a melhor ação quase sempre. O mapa informa, mas ainda não muda nenhuma decisão. Isso só muda na R2a.
2. **Mandato e prefeitura ficaram nas telas antigas** (P-01). Quem chegar à posse vai sentir o salto de qualidade para trás.
3. **Mapa de cidade pequena** (P-09): as zonas urbanas são pequenas perto da rural, os rótulos se sobrepõem e, quando um partido lidera tudo, o mapa fica de uma cor só.
4. **A intenção por zona é uma estimativa** (P-05): a da cidade repesada por idade e situação urbana ou rural. É honesta na tela ("estimativa por zona"), mas não é o voto por zona que a R2a vai calcular.
5. **Tooltips em três partes só na tela central e na Enciclopédia** (P-02), e não há tela de Opções para as pausas (P-03).

**Pendências que precisam do autor:** P-04 (artigos de lei citados de memória nos tooltips do material de campanha), P-05 (a estimativa por zona basta até a R2a?), P-06 (termos de uso do IBGE), P-08 (15 MB de atributos de zonas no repositório).

**Testes e números finais:**
- 455 testes no Core (eram 433) e 28 no importador; DataValidator limpo; build do Godot limpo.
- 12 alvos de fumaça com janela, todos passando: `newworld`, `creator`, `creator17` (novo), `picker`, `hub`, `campaign`, `map`, `mandate`, `prefeitura`, `eventos`, `layout`, `explicacao` (novo).
- Zonas: 28.135 em 5.570 municípios, 20,5 MB de contornos (teto de 30 MB) e 15 MB de atributos. A população das zonas bate com o Censo sem tolerância em 5.569 municípios; Boa Esperança do Norte (criado depois do Censo) tem uma zona só. Pela regra de sigilo, 1.423 blocos de perfil (idade ou renda) usam o do município.
- Previsão: em 1.000 execuções das ações, 100% dos resultados caíram na faixa prevista (critério: 80%).
- Tempo: sete dias produzem exatamente o mesmo estado que uma semana do motor antigo.

**Defeito de teste encontrado e corrigido:** o clique simulado dos testes de fumaça usava coordenadas da janela. Com a janela em 1965 px de largura, ele errava o botão "Mapa" da tela nova. Agora usa as coordenadas do viewport, e o teste `eventos` confere o clique num hub novo antes de tudo.

## R1 — Tela central, mapa da cidade e tempo corrente (2026-11-16, com pendências)
- **Idade inicial (D-036):** branch `wip/idade-inicial` trazido com os testes que faltavam: 17 a 45 anos, 18 aferidos em 15/8, profissões com idade mínima, Estudante, família coerente. Corrigi duas falhas do branch: validação que ainda exigia 18 anos e chave "uniao_estavel" que não carregava.
- **Zonas:** modo `zonas` do importador, com Malha de Setores e agregados do Censo 2022 (básico, idade, renda do responsável). Setores agrupados sem misturar urbano e rural; nomes de bairro ou distrito do IBGE, ou nomes do jogo. Contornos com tolerância proporcional ao tamanho de cada zona.
- **Core:** `Clock` (dia, semana no domingo, pausas por `data/time.json`), `Forecaster` (faixa de cada efeito e chance dos desfechos), `ZoneProjection` (intenção por zona, estimativa) e Enciclopédia em `data/encyclopedia/glossary.json`.
- **Tela central:**
  - barra com data, pausa, 1x/2x/4x, fase e quatro números com legenda;
  - trilho com 7 áreas e painel da área;
  - mapa 2D das zonas, com 4 modos, zoom, arraste e ficha da zona;
  - notícias e intenção de voto;
  - cartão de decisão sobre o mapa, com previsão por escolha e "O que mudou";
  - dicas de primeira vez, F1 em qualquer tela, tooltips em três partes;
  - fontes Inter, Oswald e Source Serif 4, e ícones próprios.
- **Criação:** outdoor (proibido) trocado por adesivo, bandeira e peça de redes.

**Autocrítica de imersão (portão 4):** joguei pelo teste de fumaça `campaign` (38 semanas, sem eleição) e pelos testes de autoplay do Core, e reli os quatro relatos do GDD 17.21. Contra o relato 1, faltam:
- o coordenador a contratar e o vínculo com o rival;
- o Seu Anselmo e o pedido dele;
- a Rádio Vale e o tema em alta;
- o rival aparecendo numa zona.

O que a R1 já dá é o palco: a cidade real em zonas, o dia passando, a previsão antes do clique e o motivo de cada bloqueio. A semana 20 ainda é igual à semana 2, porque nada no mapa muda a melhor ação e nenhuma pessoa lembra de nada. O feed só mostra eventos do próprio jogador e pesquisas, então o mundo ainda não "se mexe sozinho" na tela. É exatamente o que R2a (território, agenda, lideranças, Livro) e R3 (rivais e mídia) atacam.


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

## Correções eleitorais (2026-11-15)
Janela partidária só no ano da eleição em que o mandato acaba (vereador: municipal; deputados: geral). Idade mínima de 18 anos aferida no prazo do registro (15/8). Meta de assinaturas do Brasil 2026 com os votos válidos reais para a Câmara (566.265). CONTENT_GUIDE atualizado: só as fases pre_campanha e campanha; eventos de mandato ainda não existem. Adendos em D-028 a D-030; 433 testes no Core.

## Resumo da noite (modo corujão, 2026-11-15)
- **Concluído:** Fase 3 (UI da campanha, urna, apuração, save/load, mapa 2.5D, balanceamento D-026, roteiro em docs/ROTEIRO_TESTE_FASE3.md), Fase 4 (mandato de vereador, transição, filiação, fundação de partido), Fase 5 (candidatura e mandato de prefeito, debate) e 2 dos 11 itens da Fase 6 (contornos; população e vagas por eleição). Decisões D-021 a D-033.
- **Testes:** 428 no Core, 28 no importador; teste de fumaça da UI em 8 fluxos (newworld, creator, hub, picker, campaign, map, mandate, prefeitura), sem janela e com janela.
- **Pendências:** nenhuma em docs/PENDENCIAS.md (nada bloqueou). Uma exceção "Handle is not initialized", vista uma vez numa rodada com janela, não se repetiu em 5 rodadas; o timer da urna ganhou proteção contra tela já liberada.
- **Adiados de propósito (registrados nas decisões):** eventos de mandato (vereador e prefeito), eleição da Mesa, CPIs, vetos e cassação do prefeito.
- **Por onde retomar:** jogar o roteiro da Fase 3 (dá para seguir até a posse, o mandato e, na pré-campanha seguinte, disputar a prefeitura); depois, o resto da Fase 6 (estados, deputados, senador e governador, simulação regional e nacional, mídia e justiça, Espelho, evolução dos partidos, urna completa, mapa completo).

## Correções de interface (2026-11-16)
Travamento depois de evento: a janela fechava, mas o fundo escuro (que bloqueia o mouse) ficava no hub; agora sai junto. Resultado de cada ação mostra o efeito medido (reconhecimento, dinheiro, imagem, apoio, relações), nunca só "feito". Novo Mundo com ícones de sortear e travar (tooltip) e tabela que cabe na tela; aba Partido da criação em duas linhas. Testes de fumaça novos, com janela: `eventos` (as 80 opções dos 32 eventos clicadas com o mouse, conferindo que o hub volta a aceitar cliques; sem "Handle is not initialized") e `layout` (1920x1080 e 1366x768, falha se algum controle passar da janela). No modo sem janela o Godot não entrega cliques de mouse à interface, por isso esses dois rodam com janela.

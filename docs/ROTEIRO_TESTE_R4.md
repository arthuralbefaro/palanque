# Roteiro de teste — R4 (mandatos na nova tela, com gestão nova)

> Atualizado ao fim da interface da R4, com os nomes reais dos controles. Tempo: 20 minutos. Mandato de vereador, prefeitura e entressafra saíram das telas antigas e ficam na tela central, com a mesma moldura da campanha: barra do topo com o tempo corrente, trilho de áreas, painel de 400 px, mapa das zonas no centro, Notícias e Objetivos à direita. O que se testa aqui é se governar dá o que decidir, se o jogo diz o efeito antes do clique e se a cidade continua sendo um lugar.

## Antes
1. Compile: `godot --headless --import --path .` (há cinco ícones novos) e `godot --headless --build-solutions --quit`.
2. Jogue uma vez em janela de 1366×768 e uma vez em 1920×1080. Anote ou capture qualquer texto cortado, sobreposto ou pequeno demais. As capturas de referência estão em `docs/referencias/r4/`.
3. Para chegar depressa ao mandato: novo jogo numa cidade pequena, deixe a campanha correr em 4x, faça pelo menos uma promessa a uma zona (Campanha → Plataforma → Prometer) e vote em você. Eleito, a apuração leva à posse e a tela central abre na área **Mandato**. Para a prefeitura (partes 4 a 6), troque a candidatura em **Campanha → Disputar para prefeito** antes de 16 de agosto.
4. O tempo no mandato corre mais depressa que na campanha (1 s por dia em 1x). O jogo para sozinho quando chega decisão, votação em conflito, achado de fiscalização, mesa de negociação, cobrança do Livro, obra pronta ou luto.

## 1. Vereador: a tela e a pauta (3 min)
1. Olhe a tela por 10 segundos. A barra diz **Vereador · cidade · faltam N sem.** e cinco números: **turnos**, **capital**, **sua força**, **com o partido** e **governo**. Passe o mouse em cada um: o tooltip diz o que é, o que faz e quanto custa?
2. O trilho tem sete áreas: **Mandato**, **Gabinete**, **Instrumentos**, **Mesa e CPI**, **Base**, **Indicadores** e **Partido**. Passe por todas.
3. Em **Mandato**, leia **Pede atenção** (cada linha leva à área que resolve) e **Instrução permanente de voto**. Escolha **Seguir a orientação do partido**: aparece **O que mudou**.
4. Solte o tempo em 2x até aparecer um projeto em **Pauta da Câmara**. O cartão do projeto mostra a autoria, o quórum, **Contagem hoje: N sim, N não, N indeciso(s)**, a orientação do partido e o seu voto se nada mudar. Marque **Sim**, **Não** ou **Abstenção**.
5. Clique **Ver a contagem nominal**. A ficha lista **Indecisos**, **Votam sim** e **Votam não**, com o motivo de cada um. Um indeciso com pedido tem **Negociar o voto**: clique, leia a previsão do acordo (o que ele pede, o que vira promessa no Livro, quem vai lembrar) e decida se fecha com **Fechar o acordo**.
6. Com a instrução ligada, o jogo parou em alguma votação? A faixa dizia **Votação em conflito na pauta** e o motivo estava no cartão do projeto?

## 2. Vereador: gabinete e instrumentos (5 min)
1. **Gabinete**. O alto diz as vagas da cidade. Em **Quem pode ser nomeado**, cada nome tem retrato, qualidade e origem (trabalhou na sua campanha, indicação de uma liderança, parente, nome de mercado); **nomear cumpre promessa** marca quem está ligado a uma promessa do Livro.
2. Clique num nome. Escolha a função (**Chefe de gabinete**, **Assessor legislativo**, **Assessor de comunicação** ou **Assessor de base**). O assessor de base pede uma zona: clique nela no mapa. Leia **Previsão de nomear** e **Quem vai lembrar**; clique **Nomear**. Quem ficou de fora lembrou?
3. **Instrumentos** → **Indicação**. Escolha em **O que pedir**, clique numa zona do mapa e leia a **Previsão**: a chance de o prefeito atender, os turnos, a relação com o prefeito. **Fazer a indicação**. O resultado aparece em **O que mudou** e nas Notícias.
4. **Instrumentos** → **Emenda**. Escolha a obra e a zona no mapa; **Destinar a emenda**. Troque o mapa para o modo **Obras**: a obra apareceu na zona, com marcador? Passe o mouse na zona.
5. **Instrumentos** → **Fiscalização**. Escolha uma obra em **Alvo** (o mapa centraliza nela) e **Fiscalizar**. Se houver achado, a ficha **Achado: atraso** ou **Achado: irregularidade** mostra três saídas, cada uma com a previsão: **Denunciar**, **Negociar em silêncio** e **Mandar ao Tribunal de Contas**. Os custos eram diferentes e claros?
6. **Instrumentos** → **Requerimento de informação** sobre a mesma obra. Semanas depois, a resposta aparece em **Respostas recebidas**.
7. Tente usar um quarto instrumento na mesma semana: o botão bloqueado diz que os turnos acabaram e quando voltam?
8. **Mesa e CPI**: quem preside, quando é a próxima eleição da Mesa e o que falta para pedir uma CPI. Com uma irregularidade conhecida, **Pedir CPI** mostra as assinaturas previstas.

## 3. Vereador: a base cobra (2 min)
1. **Base**. A lista de zonas mostra a presença de hoje e a da posse; as da base vêm marcadas. O modo **Base** do mapa pulsa nas zonas que cobram presença.
2. Em **Promessas no Livro**, cada promessa mostra **Quem cobra**, **Prazo** e **Como cumprir**, com botões que levam ao instrumento certo (**Indicação**, **Emenda**, **Abrir o gabinete**, **Abrir a pauta**).
3. Deixe o tempo correr. Quando uma liderança cobra, o jogo para com o cartão **… cobra você**: quem cobra, **O que você prometeu**, o prazo e **Como cumprir**. O botão do cartão levou ao instrumento com a zona já escolhida?
4. Na coluna direita, **Objetivos** lista as promessas mais urgentes. Clicar numa abre a Base.

## 4. Prefeitura: governo, secretariado e obras (5 min)
1. Eleito prefeito, a tela abre em **Governo**. A barra mostra **aprovação**, **energia**, **folha**, **obras** e **capital**. O trilho tem nove áreas: **Governo**, **Secretariado**, **Obras**, **Pessoal e fiscal**, **Convênios**, **Câmara**, **Orçamento e políticas**, **Indicadores** e **Partido**.
2. **Secretariado**. Cada pasta mostra o titular, o partido, a competência (em faixa nas primeiras semanas) e a lealdade. Abra uma pasta: a ficha mostra a **Agenda própria** do secretário e a **Previsão de demitir**. **Demitir** pede confirmação. Com a pasta vaga, escolha entre os nomes oferecidos (os dos partidos e os técnicos), leia **Previsão de nomear** e clique **Nomear**. Você sentiu a troca entre competência e coalizão?
3. **Obras** → **Iniciar uma obra**. Escolha o **Tipo de obra**, clique na zona no mapa, leia a **Previsão** (custo, pasta, prazo, promessa que cumpre) e **Iniciar a obra**.
4. Solte o tempo. Quando o projeto termina, o jogo avisa e a obra aparece em **Esperando você**. Abra-a: em **Modalidade de licitação**, cada modalidade tem prazo, base legal e, quando não cabe, **Não pode:** com o motivo. A contratação por emergência sem emergência reconhecida avisa que é irregular. Escolha uma.
5. Modo **Obras** do mapa: a cor da zona é a etapa da obra mais adiantada; os marcadores mostram cada obra, as prontas e as atrasadas (**+N sem**).
6. Quando uma obra sua fica pronta, abra-a: **Previsão de inaugurar**, **Inaugurar** e **Entregar sem cerimônia**. Na campanha de reeleição, nos três meses antes da eleição, **Inaugurar** fica bloqueado e o motivo cita o art. 77 da Lei 9.504/1997; durante a campanha, as obras ficam na área **Obras do mandato** do trilho (o último ícone).
7. Uma crise chega num cartão sobre o mapa, com **Causa:** e as opções com efeito e custo. A causa era algo que você poderia ter evitado?

## 5. Prefeitura: dinheiro, convênios e Câmara (3 min)
1. **Pessoal e fiscal**. A barra da folha tem três marcas: **alerta**, **prudencial** e **limite**. Embaixo, o que cada faixa bloqueia, com o artigo. Na data-base, **Mesa de negociação** abre: a ficha mostra o pedido, a folha de hoje e as respostas (**Conceder o pedido**, **Fazer contraproposta**, **Dar só a revisão geral**, **Negar reajuste**), cada uma com a chance de a categoria aceitar. Com a folha acima do prudencial, as respostas vedadas dizem **Não pode:** e citam o art. 22.
2. Na mesma área: **Mínimos de saúde e educação** (com o mínimo em reais e a fatia de hoje) e **Repasse à Câmara** (com o teto do art. 29-A).
3. **Convênios**: abra uma oferta, escolha a zona no mapa, leia a previsão e **Assinar o convênio** ou **Recusar**.
4. **Câmara**: em **Políticas que precisam de lei**, abra a contagem de uma. Os pedidos dos indecisos eram concretos (obra no reduto, cargo)? **Fechar o acordo** antes de mandar o projeto.
5. **Orçamento e políticas**: mexa nas fatias, **Ajustar para 100%**, **Aplicar o orçamento**. Abra um programa: **Decretar** ou **Enviar à Câmara**.

## 6. Indicadores e renovação (1 min)
1. **Indicadores**. Cada linha mostra o valor na unidade real e a variação no mandato (▲ melhorou, ▼ piorou). Abra um: o valor, a unidade, a **Fonte** e o ano, **Dado de partida**, **Neste mandato**, **Comparação com o estado** e **Por que mudou**, com os fatores. Indicador estimado traz o cartão **Estimativa** com a regra.
2. **Ver as cadeiras**: cada cadeira com titular, partido e idade, os suplentes de cada partido e **Posses e saídas recentes**, com a regra aplicada.
3. Se alguém morrer durante o teste: a notícia vem num cartão de **Luto**, sem cor de destaque, e a Câmara fica sem sessão naquela semana.

## 7. Entressafra (1 min)
1. Sem mandato e sem eleição aberta (perca a eleição ou, no mandato, troque de partido fora da janela em **Partido**), a tela central abre em **Agenda**, com **Sem mandato · cidade · pré-campanha em N sem.**
2. **Caminhar numa zona**: clique na zona no mapa, leia a previsão, **Caminhar**. Repita na mesma zona: a previsão mostra **Fatores:** com a repetição. Na quarta ação da semana, o botão diz que os turnos acabaram.
3. **Lideranças** → clique num nome → **Visitar**. **Posições** → escolha um tema, um valor de −2 a +2 e **Assumir a posição**: foi para o Livro? **Dar uma entrevista** com esse tema.
4. **Ir para a pré-campanha de AAAA**: a campanha abre na mesma tela, com a presença e as posições que você deixou.

## O que avaliar
- **Clareza:** em 10 segundos, em qualquer uma das três situações, dá para dizer o que está acontecendo, o que pede atenção e o que dá para fazer?
- **Antes do clique:** alguma decisão foi tomada sem previsão, ou algum botão bloqueado ficou sem motivo?
- **Lugar:** você governa uma cidade com zonas ou uma lista de números? O mapa serviu para decidir onde pôr a emenda e a obra?
- **Pessoas:** algum assessor, secretário ou vereador virou personagem para você? As lembranças fizeram diferença?
- **Peso:** a campanha voltou para cobrar no mandato? A cobrança levou ao que fazer?
- **Gestão:** a prefeitura dá o que decidir toda semana sem virar planilha? O que você faria no piloto automático?
- **Números reais:** os indicadores em unidade real ajudam a entender se a cidade melhora?
- **Ritmo:** as pausas automáticas foram demais ou de menos?
- **Seriedade:** mortes, enchentes e escândalos foram tratados sem piada e sem julgamento?

## O que ainda não muda
- **Objetivos no mandato** são as promessas mais urgentes do Livro (ou uma meta geral, sem promessa aberta); não há Fixar nem Dispensar como na campanha.
- **Modo "Meus votos"** (a apuração por zona) não existe; o modo **Base** mostra a presença e as zonas da base.
- **Agenda em dias e turnos do dia** é só da campanha: no mandato e na entressafra os turnos são por semana, sem dia marcado, e a inauguração acontece na hora do clique.
- **Rivais, mídia local e contas de campanha** não têm tela aqui: o julgamento das contas anda sozinho no mandato e aparece nas Notícias; a interface deles é de outra parte.
- **Opções de pausa:** as pausas do mandato seguem os padrões de `data/time.json`; não há tela para ligar e desligar cada uma.
- **Prefeito na campanha de reeleição:** o governo segue sozinho; só a entrega de obra pronta fica com você.
- **Retratos e arte** são os provisórios.

# Roteiro de teste — R2a (território, agenda, lideranças, Livro de compromissos)

> Atualizado ao fim da interface da R2a, com os nomes reais dos controles. Tempo: 20 minutos. As 6 ações de efeito fixo e a energia semanal saíram: agora tudo é compromisso com dia, turno, zona e público. O que se testa aqui é se a cidade virou um lugar (zonas, pessoas, agenda) e se o jogo explica antes e depois de cada escolha.

## Antes
1. Compile: `godot --headless --build-solutions --quit`. Numa cópia nova do repositório, rode antes `godot --headless --import --path .` (há ícones novos).
2. Jogue uma vez em janela de 1366×768 e uma vez em 1920×1080. Anote ou capture qualquer texto cortado, sobreposto ou pequeno demais. As capturas de referência desta parte estão em `docs/referencias/r2a/`.
3. Use uma cidade pequena (3 a 5 zonas) para os passos 1 a 6 e abra uma metrópole no passo 7.

## 1. Dez segundos (2 min)
1. Novo jogo → Brasil 2026 → personagem de 30 a 45 anos → uma cidade pequena que você conheça.
2. Com a tela central aberta, olhe 10 segundos sem mexer no mouse. Responda:
   - Que dia é e em que fase você está? (barra de cima)
   - Onde você é conhecido e onde não é? (o mapa abre no modo **Minha força**; a legenda embaixo à esquerda dá os valores)
   - Cite duas coisas que você pode fazer agora.
3. A barra de cima tem só quatro números: **caixa**, **conhecem você**, **como te veem**, **estresse**. Passe o mouse em **conhecem você**: o tooltip mostra de que zonas o número vem?

## 2. Agenda: marcar com previsão (5 min)
1. Trilho → **Agenda** (terceiro ícone). A grade tem 7 dias por 3 turnos (**manhã**, **tarde**, **noite**); hoje tem a barra azul à esquerda; dia com **chuva** vem marcado. **Semana seguinte** e **Semana anterior** trocam a semana mostrada.
2. Clique num **+** de hoje. Abre **Marcar compromisso** no painel, com **Tipo**, **Zona (ou clique no mapa)** e o bloco **Previsão**. Troque o **Tipo** algumas vezes: a descrição e a previsão mudam? Com **Reunião com apoiadores** aparece **Público**; com **Visita a liderança**, **Quem visitar**.
3. Com a marcação aberta, clique noutra zona no mapa: a **Zona** da marcação troca?
4. Escolha **Caminhada** na zona em que você mora e clique **Confirmar**. A célula da grade fica azul; em **Marcados nesta semana** aparece a linha inteira.
5. Marque a mesma caminhada, na mesma zona, nos dois dias seguintes. Na terceira, a **Previsão** mostra o cartão vermelho **Cansaço: 3º compromisso igual em … em pouco tempo: rende 45%**? Você entendeu, antes de confirmar, que ia render menos?
6. Tente **Comício**: **Confirmar** fica desativado e a previsão diz **Não dá para marcar:** com o motivo e quantas semanas faltam? Tente **Reunião com apoiadores** numa zona em que você não é conhecido: o motivo diz o que falta?
7. Clique numa célula já marcada: abre o compromisso, com **Desmarcar**. Desmarque um.
8. Encha os três turnos de um dia: a previsão do terceiro mostra o estresse a mais?

## 3. O tempo corre e o jogo explica (3 min)
1. Solte o tempo em **2x**. A cada dia, os compromissos marcados acontecem sozinhos e viram um cartão em **Notícias** (coluna da direita): "Caminhada em … (ter 4, manhã)", o que mudou, onde, e **Fatores:** com cada um e o percentual.
2. O resultado ficou dentro da faixa que a previsão mostrava? A zona vizinha ganhou um pouco?
3. Clique num cartão do feed: o mapa centraliza na zona?
4. Deixe chegar o domingo sem marcar nada para a semana seguinte: o jogo pausa com a faixa **A semana que vem não tem nenhum compromisso marcado. Jogo pausado.** e o botão **Abrir a agenda**?
5. Deixe passar a segunda-feira: aparecem no feed, sem você fazer nada, coisas como "Fulano (vereador, SIGLA) fez campanha em …" e, no mapa, a etiqueta com a sigla do partido naquela zona? Se foi numa zona em que você lidera a presença, o jogo pausou e ofereceu **Ver a zona**?

## 4. Ficha da zona (3 min)
1. Clique numa zona no mapa. A **Ficha da zona** abre no painel da esquerda, com **Voltar**.
2. Confira: moradores e eleitores; de onde vem o nome (bairro ou distrito do IBGE, ou "nome dado pelo jogo"); **Demanda da zona** em número real, comparada com a cidade; **Presença aqui (0 a 100)** (você e os três mais conhecidos); **Lideranças** (as conhecidas e quantas ainda são "?"); **O que você já fez aqui**; **Cansaço**; **Perfil**.
3. A demanda e o perfil fazem sentido para quem conhece a cidade?
4. Clique **Marcar compromisso aqui**: a marcação abre com essa zona já escolhida e, ao confirmar, você volta para a ficha?
5. No mapa, troque para **Partido líder**: as cores mudam conforme o voto de cada zona? Volte para **Minha força** depois de alguns compromissos: a zona em que você trabalhou clareou?
6. Marcadores: o calendário azul (compromisso seu nesta semana), os ícones de liderança (verde: apoia você; cinza: neutra; cor de partido: apoia um rival) e a sigla do rival visto. Com mais de três na zona, viram um número; o tooltip da zona lista todos.

## 5. Pessoas e pedido (5 min)
1. Trilho → **Pessoas**. Em **Lideranças que você conhece**, cada linha traz o nome com o que a pessoa é ("Fulana (presidente de associação de moradores)"), a zona, a relação e de que lado está.
2. Clique numa liderança: abre a ficha, com retrato, **Onde atua**, **Interesses**, **Pedido** (ainda não ouvido), **Relação com você** e as lembranças.
3. Clique **Marcar visita** e **Confirmar**. Solte o tempo.
4. No dia da visita o jogo pausa: faixa **… fez um pedido. Jogo pausado.** e o cartão **… faz um pedido** sobre o mapa. As quatro respostas são **Aceitar**, **Negociar**, **Ouvir sem se comprometer** e **Recusar**. Cada uma mostra, embaixo, a previsão e **Quem vai lembrar**. Você entendeu o que cada uma custa antes de clicar?
5. **Ver o mapa** (na faixa) esconde o cartão para você consultar o mapa e a ficha; **Decidir** traz de volta.
6. Escolha uma resposta. O cartão vira **O que mudou**: a lembrança, a promessa (ou a dívida, ou o esqueleto) e se ela mudou de lado. **Continuar**.
7. Abra a ficha de novo: a lembrança está lá, com data e pontos? Se o pedido era de dinheiro, a tela avisou que é gasto irregular, sem dar lição de moral?
8. Faça uma caminhada numa zona com "?" na ficha: às vezes o feed diz "Na caminhada em … você conheceu …".
9. Na Agenda, se aparecer um **Convite**, leia o cartão: ele diz o que acontece se **Aceitar** e se **Recusar**?

## 6. Livro de compromissos e Enciclopédia (2 min)
1. Aperte **F1** → **Sua carreira**. O **Livro de compromissos** mostra as promessas (a quem, o quê, prazo, estado), dívidas, posições assumidas e esqueletos (com quem sabe)?
2. Em **Glossário**, leia **Presença**, **Cansaço**, **Demanda da zona**, **Liderança** e **Pedido**. Em **Como funciona**, leia **Como funciona a agenda**. Você entendeu sem ter jogado antes?
3. Clique em três termos sublinhados na tela (por exemplo "compromisso", "cansaço", "Livro de compromissos"): o verbete abre?
4. As dicas de primeira vez apareceram na Agenda, em Pessoas e na Ficha da zona? **Entendi** fecha uma; **Sem dicas** desliga todas.
5. Salve, saia e carregue: a agenda, o Livro e as lembranças continuam iguais?

## 7. Metrópole (opcional, 2 min)
1. Novo jogo numa capital. O mapa com 12 a 20 zonas continua legível? A Agenda e a Ficha da zona continuam cabendo?
2. Marque dois compromissos no mesmo dia em zonas distantes: a previsão do segundo mostra **Deslocamento entre zonas distantes no mesmo dia**?

## O que avaliar
- **Lugar:** você pensou a cidade por zonas? O mapa em **Minha força** mudou o que você decidiu fazer?
- **A situação manda:** nas semanas 1, 3 e 5, o melhor compromisso foi o mesmo? Se foi, anote qual e por quê.
- **Repetir cansa:** o aviso de cansaço chegou antes do clique? Mudou a sua escolha?
- **Pessoas:** você lembra o nome de uma liderança e o que ela quer? Alguma resposta a um pedido foi difícil?
- **Explicação:** os cartões de resultado dizem o que mudou e por quê? Faltou explicar alguma coisa? Sobrou texto?
- **Trabalho:** marcar a agenda virou burocracia em algum momento? Quantos cliques para marcar uma semana?
- **Beleza e clareza:** a Agenda, as fichas e o cartão do pedido estão no nível do resto da tela? O que está feio ou apertado?
- **Ritmo:** as pausas automáticas param demais ou de menos?
- **Defeitos:** cortes de texto, travamentos, controles que não respondem.

## O que ainda não muda nesta parte
- **Equipe, verba por canal, plataforma e painel de objetivos são da R2b.** Ainda não há coordenador, delegação de compromisso, cabos, posição conhecida por tema nem metas na coluna da direita. O comício custa um valor fixo.
- **Rivais com plano e mídia são da R3.** Os rivais já se mexem sozinhos pelas zonas e procuram lideranças, mas sem estratégia nem resposta ao que você faz; as notícias ainda não têm veículo nem linha editorial, e clicar numa notícia ainda não mostra o fato de origem.
- **Entrevista, gravação e jantar** ainda não têm veículo, tema nem doadores: rendem um efeito geral.
- **Não há tela de Opções:** as pausas automáticas usam o padrão de `data/time.json`.
- **Mandato e prefeitura** continuam nas telas antigas.
- Se a agenda agradar e a campanha ainda parecer curta de decisões difíceis, é o esperado: as trocas de dinheiro, equipe e posição pública entram na R2b.

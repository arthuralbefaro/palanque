# Roteiro de teste — R2b (equipe, verba, plataforma, objetivos)

> Atualizado ao fim da interface da R2b, com os nomes reais dos controles. Tempo: 20 minutos. A R2a deu lugar e agenda à campanha; a R2b põe gente, dinheiro e posição pública em jogo. O que se testa aqui é se cada escolha custa alguma coisa e se o jogo diz o custo antes do clique.

## Antes
1. Compile: `godot --headless --import --path .` (há dois ícones novos) e `godot --headless --build-solutions --quit`.
2. Jogue uma vez em janela de 1366×768 e uma vez em 1920×1080. Anote ou capture qualquer texto cortado, sobreposto ou pequeno demais. As capturas de referência desta parte estão em `docs/referencias/r2b/`.
3. Use uma cidade média (4 a 8 zonas, 4 vagas na equipe) para os passos 1 a 6. O passo 4 fica mais rico depois de 16 de agosto: se quiser, deixe o tempo correr em 4x até a campanha oficial antes de fazê-lo.

## 1. A área Campanha (2 min)
1. Novo jogo → cidade média. A tela abre na área **Campanha** (primeiro ícone do trilho).
2. O painel tem três blocos, cada um com **Ver tudo**: **Equipe (0/4)**, **Verba por canal** (quatro linhas com barra: Rua, Redes, Material gráfico, Cabos eleitorais) e **Plataforma** (bandeiras, posição conhecida, promessas e credibilidade). Embaixo, **Próximos compromissos**.
3. Sem abrir nada: dá para saber quanta gente você tem, quanto gasta por semana e o que a cidade sabe do que você pensa?
4. Olhe a coluna da direita: entre **Notícias** e **Intenção de voto** há o bloco **Objetivos**. Volte a ele no passo 5.

## 2. Equipe (5 min)
1. **Ver tudo** em Equipe. A tela mostra as vagas da cidade, a folha, e as cinco funções (**Coordenação**, **Marketing**, **Tesouraria**, **Jurídico**, **Redes sociais**), cada uma com dois a quatro nomes: retrato, "Nome (nome para a função …)", qualidade em faixa, salário por semana e lealdade.
2. Clique num nome da Coordenação. A ficha mostra **Qualidade (1 a 5)** em faixa, **Salário por semana**, **Lealdade (0 a 100)**, o **Vínculo** por extenso ("Já trabalhou em campanha de …", "É da sua família"), o temperamento e os interesses. O cartão **Previsão de contratar** diz o que a função passa a fazer, a folha nova, quantas semanas o caixa paga e **Quem vai lembrar**. Há um parente entre os nomes? A previsão avisa que ele vai lembrar de ter sido preterido?
3. Compare dois nomes da mesma função antes de decidir: houve troca (o melhor é mais caro, ou veio de um rival, ou é menos leal)? Clique **Contratar**.
4. Contrate mais uma pessoa de outra função. Tente contratar até encher as vagas: o botão bloqueado diz o que falta?
5. **Agenda** → clique num turno livre → **Mandar representante**. O cartão **Com representante** diz que rende 50%, que não ocupa o seu turno e que quem recebe lembra que você não foi; a linha de baixo diz quantas delegações da semana foram usadas. **Confirmar**. O turno continua livre na grade e o compromisso aparece em **Marcados nesta semana** como "… (com representante)".
6. Solte o tempo até a segunda-feira. Em Equipe aparece o cartão **Conselho da semana**, com **Seguir** (abre a marcação na zona aconselhada e mostra o progresso) e **Ignorar** (diz o custo em lealdade). Siga numa semana e ignore na outra: o conselho era bom?
7. Em alguma segunda-feira o jogo pausa com a faixa **… espera uma decisão sua. Jogo pausado.** e um cartão sobre o mapa: pedido de aumento, proposta de um rival, briga na equipe, uma liderança ou um doador trazido pelo vínculo. Cada opção mostra a previsão e **Quem vai lembrar**; o tooltip diz em que você ganha e em que paga. **Ver o mapa** esconde o cartão; **Decidir** traz de volta. Depois da escolha o cartão vira **O que mudou**; **Continuar** fecha.
8. Abra a ficha de alguém contratado → **Demitir** (botão vermelho). Abre o cartão **Demitir …?** com a previsão; **Confirmar a demissão** ou **Manter na equipe**. Depois de demitir, a ficha da pessoa guarda a lembrança?
9. Deixe o caixa ficar baixo (ou contrate gente cara): quando ele paga menos de duas semanas de folha, o jogo pausa com **O caixa paga menos de 2 semanas de folha. Jogo pausado.** e o botão **Ver a equipe e a folha**?

## 3. Verba por canal (5 min)
1. **Campanha** → **Ver tudo** em Verba por canal. Antes de 16 de agosto, o cartão do alto explica o que está fechado e por quê; cada canal mostra o alvo e onde tem verba.
2. Clique em **Rua**: o cartão diz **Fechado até 16 de agosto (faltam … semanas)**. Aperte **+ R$ …**: **Aplicar** fica bloqueado e o cartão **Não dá para aplicar:** diz a data e o artigo.
3. **Voltar** → **Redes**. Escolha a faixa em **Alvo: faixa de idade**, aperte **+ R$ …** e leia a **Previsão por semana**: a linha **Retorno** (a conta da sua própria verba, com "dobrar rende cerca de 41% a mais"), a linha **Disputa** (a sua verba contra a dos rivais no mesmo alvo), o efeito, a faixa de presença e de apoio, e cada fator. Você entendeu as duas contas? **Aplicar**.
4. Aperte **+** de novo, acima do teto da pré-campanha: o bloqueio cita o impulsionamento e o art. 57-C?
5. **Material gráfico**: escolha a zona em **Alvo: zona (ou clique no mapa)**; clicar numa zona no mapa troca o alvo. Aplique um valor.
6. No mapa, modo **Verba**: as zonas com verba ficam azuis, com a etiqueta do canal; passe o mouse numa zona: o tooltip compara a sua verba com a dos rivais, canal por canal?
7. Deixe a semana virar. Em **Semana passada** cada canal tem o resultado explicado: o que rendeu, **Contas:** (retorno × disputa = efeito) e **Fatores:**. Na segunda semana igual, a previsão mostra o cartão vermelho **Saturação**?
8. Depois de 16 de agosto: **Agenda** → **Comício** numa zona sem verba de Rua. **Confirmar** fica bloqueado e o motivo diz quanto de verba de Rua falta e onde pôr? Ponha a verba em **Rua** naquela zona e tente de novo.
9. **Cabos eleitorais**: a tela mostra **… de … contratações permitidas** e o artigo. Contrate um grupo: a rede passou a ocupar uma vaga da equipe? Na semana seguinte, zere os cabos de uma zona: a previsão avisa para onde eles vão?
10. Clique **O que a lei permite na propaganda**: o artigo explica por que não há verba de rádio, TV, outdoor nem brinde?

## 4. Plataforma (5 min)
1. **Campanha** → **Ver tudo** em Plataforma. Em **Plataforma × públicos**, cada um dos dez temas mostra **Você**, **partido** e **conhecida**; à direita, o quanto o público escolhido concorda com a sua posição (de −1 a +1, com seta). Troque o público na lista (uma zona, depois um grupo): os números mudam? Os três temas marcados **pesa mais** fazem sentido para aquele público?
2. Clique num tema. A tela mostra a escala (o que é −2 e o que é +2), a sua posição, a do partido, a **posição conhecida** e, embaixo, **Quem gosta e quem não gosta**, por grupo e por zona.
3. Em **Assumir ou mudar de posição**, clique noutro número. A **Previsão** aparece antes de valer: o salto, a incoerência (com "você mudou de posição … vezes nas últimas 52 semanas"), o efeito na cidade e, por grupo e por zona, quanto os seus votos mudam. Você entendeu quem ganha e quem perde? **Confirmar a posição**. O cartão **O que mudou** diz a imagem perdida e quem vai lembrar.
4. Mude de volta na semana seguinte: o custo de incoerência subiu?
5. **Tornar bandeira da campanha** em três temas; no quarto, o botão bloqueado diz por quê?
6. **Agenda** → **Entrevista** (ou Gravação, ou Comício) → **Tema de que você vai falar**. A previsão mostra **Posição conhecida em …: +… pontos**. Depois do compromisso, a posição conhecida subiu?
7. **Plataforma** → **Prometer a uma zona ou a um grupo**. Escolha **A quem** e **O quê**; a **Previsão** mostra o apoio em faixa, a credibilidade antes e depois e o prazo. **Prometer**. Faça quatro ou cinco promessas: a credibilidade caiu e a previsão da última mostrou isso?
8. **F1** → **Sua carreira**: o **Livro de compromissos** mostra cada **Posição assumida · tema**, com o lado, a data e onde foi dita, e as promessas?

## 5. Objetivos (2 min)
1. Leia os até três objetivos da coluna da direita. Cada um tem a condição, a barra de progresso, **Hoje:** e a meta, o prazo e **Origem:** (o texto inteiro está no tooltip do cartão).
2. Clique no alfinete (**Fixar**): o objetivo fica marcado "fixado" e não sai até o prazo. Clique no X (**Dispensar**) de outro: saiu sem punição?
3. Cumpra um (o de presença numa zona costuma ser o mais direto). Na segunda-feira seguinte aparece o cartão **Objetivo cumprido: …** em Notícias, e outro objetivo entra no lugar?
4. Olhe os objetivos nas semanas 1, 5 e 10: mudaram com a situação?

## 6. Enciclopédia, dicas e sobras da R2a (1 min)
1. As dicas de primeira vez apareceram em Equipe, Verba por canal e Plataforma?
2. **F1** → **Glossário**: leia **Equipe**, **Lealdade**, **Vínculo**, **Verba**, **Canal**, **Posição conhecida**, **Incoerência** e **Credibilidade**. Em **Como funciona**: **Como funciona a equipe**, **a verba**, **a plataforma** e **os objetivos**. Entendeu sem ter jogado?
3. Na Agenda, o botão **Repetir a semana anterior** marca de novo os compromissos da semana anterior? A célula da grade mostra "Caminhada" inteiro?
4. Encha três turnos por dia até o estresse passar do limite: aparece o cartão vermelho **Estresse em …** dizendo o que ele causa?

## O que avaliar
- **Trocas:** cite duas decisões em que você perdeu algo de propósito (dinheiro, lealdade, um público, credibilidade).
- **Pessoas:** alguém da equipe virou personagem para você? Você lembra de onde veio e a quem é ligado?
- **As duas contas:** a previsão da verba mudou onde você gastou? Você pôs dinheiro onde ninguém estava?
- **Posição pública:** alguma posição ou promessa de semanas atrás voltou para cobrar? Ficar vago pareceu uma opção de verdade?
- **Objetivos:** deram direção ou viraram lista de tarefas? Algum pareceu sem sentido?
- **Excesso:** somando a R2a, a campanha tem sistemas demais para acompanhar? Qual você cortaria? Quantas vezes por semana o jogo pausou?
- **Clareza:** os números pequenos (apoio de +0,5 ponto, voto de −7% entre um grupo) dizem alguma coisa? O que faltou explicar?
- **Beleza e clareza:** as telas novas estão no nível da tela central? O painel de 400 px aguenta a matriz e a previsão, ou pede rolagem demais?
- **Defeitos:** cortes de texto, travamentos, controles que não respondem.

## O que ainda não muda nesta parte
- **Rivais com plano são da R3.** A verba dos rivais em cada canal é uma estimativa fixa pelos redutos deles; eles não respondem ao que você gasta, não atacam a sua mudança de posição e não usam o que um ex-membro da sua equipe contou. A proposta de rival a alguém da sua equipe existe, mas o rival ainda não tem estratégia.
- **Mídia é da R3.** A entrevista ainda não tem veículo nem linha editorial; a mudança de posição e a promessa viram cartão no feed, mas nenhum jornal as explora. Rádio e TV como presença conquistada (horário gratuito com o tempo do partido) também entram na R3.
- **Doadores e prestação de contas são da R3.** O doador trazido pelo vínculo rende dinheiro, mas não tem nome nem interesse; o erro nas contas vira esqueleto no Livro, mas ninguém julga as contas ainda; não há limite de gastos na tela. O advogado é contratado e ainda não tem o que fazer.
- **O marqueteiro ainda não opina** sobre plataforma e tom, nem o tesoureiro sobre o limite: só o coordenador dá conselho.
- **Promessas e posições ainda não são cobradas no mandato** (R4): entram no Livro e ficam lá.
- **Não há tela de Opções:** as pausas automáticas usam o padrão de `data/time.json`.

# Roteiro de teste — R3 (rivais com estratégia, mídia local, doadores e contas)

> Atualizado ao fim da interface da R3, com os nomes reais dos controles. Tempo: 20 minutos. A R2 deu lugar, agenda, gente e dinheiro à campanha; a R3 põe adversários que jogam, uma imprensa que lê o que acontece e um caixa com limite e risco. O que se testa aqui é se você passa a jogar contra alguém e se o dinheiro vira decisão.

## Antes
1. Compile: `godot --headless --import --path .` e `godot --headless --build-solutions --quit`.
2. Jogue uma vez em janela de 1366×768 e uma vez em 1920×1080. Anote ou capture qualquer texto cortado, sobreposto ou pequeno demais. As capturas de referência desta parte estão em `docs/referencias/r3/`.
3. Use uma cidade média ou grande (pelo menos uma rádio e um jornal). Contrate alguém na **Coordenação** logo no começo (Campanha → Equipe): a leitura dos rivais depende disso. Para os passos 5 e 6 ajuda ter também **Jurídico** e **Tesouraria**.
4. Ataques e ofertas de doação aparecem sozinhos com o tempo correndo. Se em 4 ou 5 semanas de jogo não vier nenhum, anote: é um dado para o balanceamento.

## 1. Pessoas: a chapa e os rivais (3 min)
1. Trilho → **Pessoas** (segundo ícone). No alto, o bloco **Chapa do seu partido** com a frase "Seu partido hoje faria N cadeiras; você é o Kº da chapa" e **Ver tudo**. Abaixo, **Principais rivais**, **Prefeitura** e **Lideranças que você conhece**.
2. **Ver tudo** na chapa. O painel mostra os votos projetados do partido, o quociente eleitoral e a lista **Quem puxa quem**: cada nome com os votos, a parte dos votos da chapa, quanto isso é de um quociente e se **entra** ou **fica de fora**. Você entendeu por que o colega é aliado e concorrente?
3. **Voltar** → clique num rival. A ficha tem: **Plano percebido** (com a linha "Leitura de … Confiança da leitura: X%"), perfil, postura diante de você, **Redutos**, **Zonas-alvo desta semana** (só com confiança bastante), **Ver no mapa**, **Verba vista**, **Pacto**, **Ataques trocados**, **Pontos fracos que você conhece**, **O que dá para fazer** e **Lembranças de você**.
4. Abra a ficha de outro rival de perfil diferente. Dá para dizer onde cada um é forte e o que está tentando fazer? Sem coordenador a ficha avisa que você só vê os redutos?

## 2. Atacar, investigar e pacto (4 min)
1. Na ficha de um rival, **Investigar · R$ …**: o tooltip diz o custo, a chance de achar e a de ele ficar sabendo. Clique. O cartão **O que mudou** diz o que saiu.
2. **Atacar**. Escolha **Por quê** (por incoerência, por esqueleto ou por tema), a **Prova** ou o **Tema**, e **Por onde** (nas redes, por um veículo, no palanque). Leia o cartão **Previsão**: a imagem que ele perde em faixa, a que você perde, o custo, o risco de processo quando não há prova, a reação esperada e **Quem vai lembrar**. Troque de "sem prova" para uma prova (se a investigação achou): a previsão mudou?
3. **Confirmar o ataque** (botão vermelho). Volta à ficha com **O que mudou**. Abra **Atacar** de novo: o botão está bloqueado e o cartão **Não dá para atacar:** diz quantos dias faltam?
4. Deixe o tempo correr uma ou duas semanas. O rival reagiu? Como você soube (feed, faixa, mapa)?
5. Na ficha de outro rival, leia **Previsão do pacto de não agressão** (chance de aceitar, zonas que se fecham, o que você ganha e paga) e clique **Propor pacto de não agressão**. Se aceitar, o cartão **Pacto** lista as zonas fechadas.
6. Com um pacto em vigor: **Território** → uma zona fechada → **Marcar compromisso aqui** → tipo Caminhada. O cartão vermelho avisa que o compromisso quebra o pacto, e **Confirmar** fica vermelho. Cancele ou quebre de propósito e veja a notícia e a lembrança na ficha dele.
7. Na ficha de um candidato a prefeito (bloco **Prefeitura**), a proposta é **Propor dobradinha**.

## 3. Ataque contra você, com prazo (3 min)
1. Quando um rival ataca você, o jogo pausa com a faixa **Ataque de … contra você: responda até … (faltam 2 dias). Sem resposta, vale o silêncio. Jogo pausado.** e os botões **Responder** e **Fechar aviso**. O cartão do ataque aparece em **Notícias** e no alto da área **Mídia**.
2. Feche o aviso e solte o tempo por um dia: o tempo anda, e a faixa continua com a contagem ("faltam 2 dias", "falta 1 dia", "vence hoje").
3. **Responder**. A tela **Ataque contra você** mostra o texto, quem atacou e por onde, quanto tira da sua imagem hoje, o cartão do prazo e cinco respostas, cada uma com a previsão e o botão **Escolher esta resposta**: **Responder no mesmo espaço**, **Responder por outro canal**, **Pedir direito de resposta**, **Contra-atacar**, **Ficar em silêncio**. Sem advogado na equipe, o direito de resposta está bloqueado e diz o que falta?
4. Responda a um ataque e deixe o prazo de outro vencer. Nas semanas seguintes a diferença apareceu (a ficha do rival, em **Ataques trocados**, mostra quanto cada ataque ainda tira)?
5. Se o ataque vier como **BOATO**, as respostas mudam de nome (desmentir, processar, ignorar) e a tela avisa que pode ser falso.

## 4. Mídia (4 min)
1. Trilho → **Mídia** (sexto ícone). De cima para baixo: **Ataques contra você** (se houver), **Tema em alta**, **Veículos da cidade**, **Horário eleitoral gratuito** e **Notícias**.
2. Em **Veículos da cidade**, cada linha diz o nome com o que é ("Rádio … (rádio local, linha governista)"), quanto dos eleitores alcança, como trata você (amigo, crítico, neutro) e a relação com o jornalista.
3. Clique numa rádio ou TV. A ficha tem **Linha editorial**, **Com você**, **Temas de interesse**, **Alcance** por zona, **Quem manda e quem escreve** (nomes clicáveis) e **O que dá para fazer**. Em **Anunciar**, o botão está bloqueado e o cartão cita o artigo da lei que proíbe propaganda paga em rádio e TV.
4. **Marcar entrevista aqui**. A marcação abre com o campo **Veículo** já escolhido. Troque entre um veículo amigo e um crítico e compare a **Previsão**: o alcance, o crédito e a chance de se sair bem mudam?
5. Em **Tema de que você vai falar**, os temas em alta vêm marcados com "em alta"; escolha um e veja o cartão de contorno verde na previsão com o bônus. Confirme.
6. Volte a **Mídia** → um jornal → **Sugerir pauta**: escolha o **Fato**, leia a **Previsão da pauta** (chance de entrar, tom provável, quem vai lembrar) e clique **Sugerir a pauta**. A segunda da semana ao mesmo jornalista fica bloqueada com o motivo.
7. No mesmo jornal, **Anunciar · R$ …**: antes de 16 de agosto está bloqueado com a data; na campanha oficial a **Previsão do anúncio** mostra a presença por zona e o limite ("até 10 anúncios por veículo… Restam N"). Anuncie duas vezes no mesmo dia: a segunda é barrada?
8. Em **Notícias**, cada cartão tem veículo, data, tom, manchete e a linha **Fato de origem**. Procure o mesmo fato em dois veículos de linhas diferentes: o fato é o mesmo e o tom muda? Boato vem com a marca **BOATO de rede**?
9. No mapa, botão **Disputa**: as zonas mais claras são as mais apertadas; as etiquetas mostram os rivais vistos na semana (clique numa: abre a ficha) e o ícone de jornal marca as zonas com tema em alta. O tooltip da zona diz quem lidera e por quanto.

## 5. Dinheiro e contas (4 min)
1. Trilho → **Finanças** (quinto ícone). O bloco **Contas** mostra a barra **Limite de gastos**, o **Risco das contas (0 a 100)** com a faixa, e as ofertas de doação à espera de resposta. **Ver tudo** abre o painel.
2. No painel **Contas**: limite, risco, **O que pesa no risco** (cada irregularidade com o valor, quanto soma, **Quem sabe** e a chance de vir à tona), **De onde veio o dinheiro**, os cartões de **Recursos próprios**, **Fundo eleitoral** (quem decide e por quê) e **Financiamento coletivo** (abre em 15 de maio), a lista **Doadores** e os **Prazos da prestação de contas**.
3. Quando um doador faz uma oferta, o jogo pausa com a faixa **… fez uma oferta de doação: responda até …** e o botão **Decidir**. A tela **Oferta de doação** mostra quem é, o que pede em troca e uma previsão por resposta: **Aceitar com recibo, até o teto legal**, **Recusar** e, conforme a oferta, **Aceitar o valor inteiro com recibo, acima do teto** ou **Aceitar "por fora"**. Cada uma diz o dinheiro, o **Risco das contas: de X para Y**, a dívida e quem vai lembrar; a "por fora" diz **Quem fica sabendo**.
4. Aceite uma doação com recibo. A dívida está no Livro de compromissos (F1 → **Sua carreira**)?
5. Numa oferta "por fora" (se não vier nesta campanha, use outro save): aceite e volte ao painel Contas. A irregularidade aparece com quem sabe? Recusar doeu? Aceitar deu medo?
6. Demita, com lealdade baixa, alguém da equipe que sabia do dinheiro "por fora" (Tesouraria ou Coordenação). O cartão do esqueleto passa a mostrar **Podem falar** e uma chance por semana?
7. Gaste até chegar perto do limite: a barra enche e fica vermelha ao passar; o gasto acima do limite entra em **O que pesa no risco**.

## 6. Eventos com gente do save (1 min)
1. Quando um evento abrir o cartão de decisão, veja a lista logo abaixo do texto: as pessoas envolvidas com retrato pequeno e nome com o que é, a **Zona** e o veículo. Clique num nome: a ficha abre no painel da esquerda com o cartão ainda aberto.
2. A previsão de cada opção bate com o que aconteceu depois da escolha?

## 7. Depois da eleição (1 min, com o save da noite da apuração)
1. Termine a campanha. Na tela do mandato (ou da entressafra) aparece o cartão **Contas da eleição de …**, com as três etapas: **Prestação de contas final**, **Julgamento das contas** e **Representação por captação ou gasto ilícito**.
2. Avance as semanas: quando sai uma novidade, o cartão ganha contorno amarelo e a linha **Novidade desta semana**. O resultado fez sentido com o que você fez na campanha?
3. Fora de mandato, **Ir para a pré-campanha de …** para na semana em que as contas têm novidade.

## O que avaliar
- **Adversários:** você escolheu um rival para vencer? Sentiu que ele jogava contra você, ou só que números mudavam?
- **Surpresa com causa:** algo o surpreendeu e, olhando a ficha ou o feed, tinha explicação?
- **Leitura parcial:** a "confiança da leitura" fez você valorizar o coordenador, ou passou despercebida?
- **Prazo sem travar:** responder a um ataque com o tempo correndo criou tensão ou só incômodo? Quantas pausas por semana?
- **Mídia:** a linha editorial mudou a que veículo você foi? O tema em alta mudou do que você falou?
- **Tensão do dinheiro:** recusar dinheiro doeu? O risco das contas ficou na sua cabeça durante a campanha?
- **Clareza:** os números de imagem ("pontos") e de risco dizem alguma coisa? O que faltou explicar?
- **Neutralidade:** algum veículo, rival, doador ou liderança pareceu caricatura de um lado político, de uma religião ou de uma região?
- **Excesso:** a área Mídia e o painel Contas pedem rolagem demais? O que você tiraria?
- **Defeitos:** cortes de texto, travamentos, controles que não respondem.

## O que ainda não muda nesta parte
- **O mandato continua na tela antiga** (R4): o cartão das contas aparece nela, mas a cobrança do doador, a memória do rival atacado na Câmara e os veículos locais ainda não entram no dia a dia do mandato.
- **Limite de gastos é conta do jogo:** a tabela oficial por município ainda não foi incorporada; o painel avisa.
- **Sem tela de Opções:** as pausas de "ataque contra você" e de "decisão com prazo" seguem o padrão de `data/time.json`.
- **Horário gratuito sem bancada nos mundos gerados:** só o cenário Brasil 2026 tem a bancada federal que divide o tempo em 90% e 10%; nos outros mundos o tempo é repartido por igual, e a tela diz isso.
- **A Justiça Eleitoral ainda é só resultado:** direito de resposta, processo por ataque sem prova e representação saem como desfecho com chance; não há audiência nem defesa.
- **Sem segundo turno nas contas:** o prazo de 20 dias depois do segundo turno existe na regra, mas a tela mostra só o de 30 dias.
- **Rivais não propõem pacto a você** nem dobradinha: a iniciativa é sempre sua.

# Roteiro de teste — RL, leitura mínima

> Tempo: 12 minutos. A RL não troca o estilo (fontes, paleta e tema são os de antes): ela faz a tela mostrar o que o jogo já calcula. Relógio e linha do dia na barra de cima, painel **Você** sempre visível, painel de zonas ligado ao mapa, **Fazer agora** como ação principal, número flutuante depois da ação, retratos no noticiário e a ficha da zona flutuando sobre o mapa, numa pilha de janelas com Esc. Capturas em `docs/referencias/rl/` (semanas 1, 10 e 20) e `docs/referencias/rl/meio/` (comparação com `docs/referencias/rl_tela_central.html`).

## Antes
1. Compile: `godot --headless --import --path .` e `godot --headless --build-solutions --quit`.
2. Jogue uma vez em 1366×768 e uma vez em 1920×1080. Capture qualquer texto cortado ou sobreposto.
3. **Teste dos 10 segundos (princípio i):** ao abrir a tela central, olhe 10 segundos e responda: o que está acontecendo? o que pede atenção? o que posso fazer? Anote as três respostas.

## 1. Barra de cima, relógio e linha do dia (2 min)
1. Comece uma carreira em Mantena (MG). A barra de cima tem só: data, **Pausa**, **1x**, **2x**, **4x**, a fase com a contagem ("38 sem. até a eleição") e os botões **Mapa**, **Salvar**, **Menu**, **Opções** e **?**.
2. Na segunda linha: o relógio grande (06:00), a linha do dia com **Manhã**, **Tarde** e **Noite** e, embaixo, as marcas **6h, 9h, 12h, 15h, 18h, 22h**. À direita, "Depois: …" com o próximo compromisso depois de hoje.
3. Clique **1x**: o relógio anda e o traço branco corre pela linha. Um dia leva 4 s em 1x.
4. Às 22h o dia vira: aparece sobre o mapa a faixa "Fim do dia · começa …", que some quando o relógio passa das 7h.
5. Passe o mouse num turno da linha: o tooltip diz o horário, o que há nele e o que o clique faz. Clique num turno **livre**: abre a marcação naquele turno. Clique **Pausa**.

## 2. Painel Você (2 min)
1. No alto da coluna esquerda: **Você**, a candidatura (cargo, partido e número), **Turnos livres hoje** (uma bolinha por turno: verde livre, azul marcado, cinza feito ou passado), **Turnos livres na semana**, **Estresse** com a barra e a marca vermelha do limite, **Dinheiro**, **Conhecem você**, **Como te veem** e **Eleição**.
2. Passe o mouse em cada linha: o tooltip diz o que é, o que limita e quanto custa (por exemplo, quantos turnos por dia saem de graça e quanto estresse custa o seguinte; a folha e a verba que saem na segunda).
3. Clique cada ícone do trilho (Campanha, Pessoas, Agenda, Território, Finanças, Mídia, Partido): o painel **Você** fica sempre no mesmo lugar.

## 3. Painel de zonas e mapa (3 min)
1. A tela abre na área **Território**: uma linha por zona com presença (e a barrinha), **líder:** da intenção, **você: Nº**, **demanda:**, a última visita, **lideranças:** e **cansaço:**.
2. Troque a ordem em **Ordenar por presença** (presença, eleitores, sua posição, visita mais antiga, lideranças, cansaço).
3. Clique uma linha: a zona fica com contorno no mapa e a linha ganha a barra azul, **Fazer agora: Caminhada**, **Ver a ficha** e **Centralizar**.
4. Clique outra zona **no mapa**: a linha dela fica destacada (a lista rola até ela) e a ficha da zona abre flutuando sobre o mapa.

## 4. Fazer agora e número flutuante (3 min)
1. Na ficha flutuante, no bloco **Agir aqui**, escolha o tipo (Caminhada, Reunião em casa de apoiador, Visita a liderança, Comício). A previsão diz **quando** (hoje, em que turno) e o efeito com a faixa.
2. Clique **Fazer agora**. A agenda não abre: o relógio anda até o fim do turno, a zona solta "+X presença" no mapa (e números menores nas vizinhas) e o resultado entra no noticiário.
3. Repita até o terceiro turno do dia: o botão continua **Fazer agora** e a previsão mostra "Turno além dos 2 sem custo no dia: +3 de estresse".
4. Sem turno livre hoje, o botão vira **Fazer amanhã de manhã** e a previsão diz que é amanhã. O dia só vira se você clicar.
5. Bloqueado (por exemplo, **Comício** na pré-campanha, ou sem dinheiro), o botão fica apagado e diz o motivo embaixo e no tooltip.
6. **Agendar para outro dia** abre a marcação por cima da ficha.

## 5. Pilha de janelas e Esc (1 min)
1. Com a marcação aberta por cima da ficha: a ficha fica escurecida e não aceita clique; o mapa também não.
2. Aperte **Tab** várias vezes: o foco não sai da marcação.
3. **Esc** fecha a marcação (volta à ficha); **Esc** de novo fecha a ficha. Num cartão de decisão, Esc não fecha nada: a decisão é obrigatória.

## 6. Retratos e noticiário (1 min)
1. Na coluna da direita, cada notícia sobre alguém tem o retrato da pessoa: o seu nos cartões de **Sua agenda**, o do rival em **Na rua**, o da liderança nas descobertas.

## O que anotar
- As três respostas do teste dos 10 segundos.
- Se o ritmo do relógio (4 s por dia em 1x) ficou bom para ler a linha do dia.
- Se a ficha flutuante cobre demais o mapa (420 px, à esquerda).
- As escolhas "a confirmar" da P-41 em `docs/PENDENCIAS.md`.

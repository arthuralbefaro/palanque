# Roteiro de teste — Fase 3 (campanha de vereador)

Dez minutos para jogar uma campanha de vereador do começo à apuração e anotar impressões. Antes de abrir o jogo, compile uma vez: `godot --headless --build-solutions --quit` (ou abra o projeto no editor). Depois rode `godot --path .` na raiz do repositório, ou aperte F5 no editor.

## 1. Mundo (1 min)

1. Menu principal → **Novo Jogo**.
2. Em **Predefinição**, escolha **Brasil 2026** (partidos fictícios que espelham os reais, com força local tirada das eleições de 2024).
3. Olhe a tabela de partidos sem editar nada e clique em **Continuar**.

## 2. Personagem (2 min)

1. Clique em **Sortear tudo** para ganhar tempo.
2. Aba **Identidade** → **Escolher cidade**: busque uma cidade pequena ou média que você conheça. A ficha mostra porte, vagas e dificuldade sugerida.
3. Aba **Partido**: a lista agora vem **ordenada pela força de cada partido na cidade escolhida**.
   - Primeira campanha: escolha o primeiro da lista.
   - Se sobrar tempo depois, jogue de novo com um partido do meio ou do fim da lista. Partido sem força local quase nunca elege ninguém em cidade pequena, porque o quociente eleitoral é alto.
4. Aba **Campanha**: confira número, slogan e o santinho.
5. **Começar carreira**.

## 3. Campanha (4 min)

O hub mostra a semana. A barra de cima traz data, fase, semanas até a eleição, energia, dinheiro, reconhecimento e imagem.

1. **Agenda da semana**: você tem 100 de energia por semana. As ações custam energia e dinheiro. Até 16 de agosto é pré-campanha e o **Comício** fica bloqueado (passe o mouse sobre o botão para ver o motivo). **Arrecadação** traz dinheiro.
2. Faça duas ou três ações e leia o resultado de cada uma em **Resultados**, embaixo da agenda.
3. **Pesquisas**: encomende a **Pesquisa interna** uma vez para ver onde você está. Os institutos publicam sozinhos a cada 3 semanas durante a campanha. Sua linha aparece em amarelo, mesmo fora dos 8 primeiros.
4. **Avançar até algo acontecer** pula semanas até surgir um evento ou uma pesquisa. Quando aparecer uma **janela de evento**, leia e escolha. Escolhas com requisito que você não cumpre aparecem desativadas, com o motivo.
5. **Mapa** (barra de cima): mapa 2.5D do seu estado. A altura é a força do seu partido em cada município, e a cor é o partido mais forte. Use a roda do mouse para zoom, arraste com o botão esquerdo e passe o mouse para ver os dados. Em **Altura pela força de**, troque o partido e veja o relevo mudar. Sua cidade tem contorno amarelo. **Voltar** retorna ao hub.
6. **Salvar** guarda num slot manual. Depois **Menu → Carregar** retoma do mesmo ponto.
7. Continue avançando (de preferência fazendo ações toda semana) até aparecer **Ir para a urna**.

## 4. Urna (1 min)

1. A urna pede vereador e depois prefeito. Digite pelo teclado numérico na tela ou pelo teclado físico (Enter confirma, Backspace corrige).
2. Teste uma vez: o número de outro candidato mostra foto e partido; 2 dígitos no voto de vereador viram voto de legenda; **BRANCO** e um número inexistente (nulo) também funcionam.
3. Vote em você e escolha um prefeito. Aparece **FIM**.

## 5. Apuração (1 min)

1. As parciais sobem sozinhas. **Pular para o resultado** vai direto ao fim.
2. No fim: vereadores mais votados (você em amarelo), cadeiras por partido, quociente eleitoral, prefeito eleito e o veredito, com o botão **Tomar posse** ou **Continuar a carreira**.

## Dificuldade esperada (DECISOES D-026)

Com o partido mais forte da cidade e ações toda semana, a simulação automática se elege em quase todas as cidades pequenas e médias, em cerca de 2/3 das grandes e em pouco mais da metade das metrópoles. Com um partido sorteado, raramente se elege. Sem fazer campanha, nunca. Um jogador escolhendo ações com critério deve ir melhor que a simulação, que escolhe ações ao acaso.

## O que avaliar (anote o que puder)

- **Clareza**: dá para entender o que cada ação faz, por que algo está bloqueado e o que mudou na semana?
- **Escolhas**: alguma ação é sempre a melhor? Alguma nunca vale a pena?
- **Dificuldade**: a cidade e o partido que você escolheu deram uma campanha justa?
- **Eventos**: os textos soam brasileiros e plausíveis? Alguma escolha pareceu sem consequência?
- **Pesquisas e apuração**: os números convencem? A apuração cria suspense?
- **Mapa**: o relevo e as cores ajudam a ler o estado? Está fluido?
- **Defeitos**: travamentos, textos cortados, botões que não respondem, números estranhos.

## Limitações conhecidas

- Arte provisória (retratos e logotipos de placeholder), sem áudio e só em português.
- Depois da apuração, **Tomar posse** (se eleito) leva ao mandato de vereador (Fase 4) e **Continuar a carreira** (se não) leva à entressafra até a próxima eleição. Na pré-campanha seguinte dá para disputar a prefeitura (Fase 5). Se sobrar tempo, vale olhar essas telas, mas o foco deste roteiro é a campanha.
- O mapa é o protótipo da Fase 3: só o estado da sua cidade, sem modo apuração nem alternativa 2D plana (Fase 6).
- O balanceamento é o primeiro ajuste. O fino fica para o BalanceSim da Fase 10.

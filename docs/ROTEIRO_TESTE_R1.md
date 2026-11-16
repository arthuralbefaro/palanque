# Roteiro de teste — R1 (tela central, mapa da cidade, tempo corrente)

> Escrito antes da implementação, a partir do GDD 17.1 a 17.5 e do ART_GUIDE. Ao fim da R1 os nomes dos controles são conferidos com a tela real e este roteiro é atualizado. Tempo: 20 minutos. O conteúdo de jogo ainda é o da Fase 3 (6 ações, eventos, pesquisas); o que se testa aqui é a tela, o tempo e a explicação.

## Antes
1. Compile: `godot --headless --build-solutions --quit`.
2. Jogue uma vez em janela de 1366×768 e uma vez em 1920×1080 (Opções → Vídeo). Em cada uma, anote ou capture qualquer texto cortado, sobreposto ou pequeno demais.

## 1. Dez segundos (princípio i)
1. Novo jogo → Brasil 2026 → sortear personagem → uma cidade média que você conheça → começar.
2. Assim que a tela central abrir, olhe por 10 segundos e tire a mão do mouse. Responda sem procurar:
   - Que dia é e em que fase você está?
   - Há algo pedindo sua atenção agora? O quê?
   - Cite duas coisas que você pode fazer.
3. Se alguma resposta exigiu procurar, anote onde o olho foi primeiro.

## 2. Mapa
1. O mapa mostra as zonas da sua cidade? Os nomes fazem sentido para quem conhece a cidade? Anote zona com nome estranho ou limite errado.
2. Troque entre os modos **Partido líder** e **Perfil**. A legenda explica as cores?
3. Passe o mouse e clique numa zona: a Ficha da zona abre no painel da esquerda, com população e eleitores. O número tem tooltip dizendo de onde veio?
4. Repita numa cidade pequena (3 a 5 zonas) e numa metrópole (12 a 20). O mapa continua legível?

## 3. Tempo
1. Solte a pausa em 1x, depois 2x e 4x. A data anda por dia? Uma semana sem decisão passa em cerca de 7 segundos em 4x?
2. Deixe correr até o jogo pausar sozinho. A faixa sobre o mapa diz por que pausou? O botão **Decidir** abre o cartão sobre o mapa, com os painéis ainda legíveis?
3. Opções → Tempo: desligue a pausa em "Pesquisa publicada" e confira que a próxima pesquisa só aparece no feed.
4. Tente fazer o jogo andar com uma decisão imediata aberta: ele deve recusar e dizer por quê.

## 4. Antes e depois do clique (princípio f)
1. Passe o mouse sobre cada uma das 6 ações. O tooltip diz o que é, o que faz (com faixa) e quanto custa?
2. Execute três ações. Em cada uma, o cartão "O que mudou" diz o quê, quanto e por quê? O resultado ficou dentro da faixa prevista?
3. Num evento, cada escolha mostra a previsão antes de você escolher?

## 5. Nada sem explicação (princípio j)
1. Procure um controle sem tooltip. Procure por 2 minutos; anote cada um que achar.
2. Ache um botão desativado: o tooltip diz o motivo e o que falta?
3. Clique em três termos sublinhados (por exemplo "quociente eleitoral", "imagem", "margem de erro"). O verbete abre e você entendeu?
4. Aperte F1 em três telas diferentes (menu, tela central, urna). Em "Este mundo", os partidos, institutos e rivais do seu save estão lá?
5. Todo nome fictício que apareceu veio com o que é ("Prisma (instituto de pesquisa)")?
6. A explicação de primeira vez apareceu? Dispense uma, saia e volte: ela não pode reaparecer.

## 6. Mandato e prefeitura na tela nova
1. Carregue um save com mandato de vereador (ou avance até a posse): pauta, ações e notícias estão em painéis da mesma tela, com o mapa visível?
2. Idem para a prefeitura: orçamento, políticas, secretarias e crise (a crise abre como cartão sobre o mapa).

## O que avaliar
- **Beleza:** a tela parece um jogo que você abriria de novo? O que ainda está feio?
- **Clareza:** as três perguntas dos 10 segundos.
- **Ritmo:** as velocidades estão boas? Pausa demais ou de menos?
- **Mapa:** as zonas ajudam a enxergar a cidade?
- **Explicação:** faltou explicar alguma coisa? Sobrou texto em algum tooltip?
- **Defeitos:** cortes de texto, travamentos, controles que não respondem.

## O que ainda não muda nesta parte
As 6 ações continuam com efeito igual em qualquer zona; rivais ainda não têm plano; não há equipe, verba por canal, plataforma nem lideranças. Isso é a R2 e a R3. Se a tela agradar e o jogo ainda parecer raso, é o esperado nesta etapa.

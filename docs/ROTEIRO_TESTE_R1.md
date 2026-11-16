# Roteiro de teste — R1 (tela central, mapa da cidade, tempo corrente)

> Atualizado ao fim da R1 (tag `fase-r1`), com os nomes reais dos controles. Tempo: 20 minutos. O conteúdo de jogo ainda é o da Fase 3 (6 ações, eventos, pesquisas); o que se testa aqui é a tela, o mapa, o tempo e a explicação. A R1 ficou **com pendências** (docs/PENDENCIAS.md): mandato e prefeitura ainda estão nas telas antigas, e não há tela de Opções para as pausas.

## Antes
1. Compile: `godot --headless --build-solutions --quit`. Se fontes ou ícones aparecerem quadrados, rode também `godot --headless --import --path .` uma vez.
2. Jogue uma vez em janela de 1366×768 e uma vez em 1920×1080. Em cada uma, anote ou capture qualquer texto cortado, sobreposto ou pequeno demais.

## 1. Criação: 17 anos e material de campanha (2 min)
1. Novo jogo → Brasil 2026 → aba **Identidade**: ponha idade **17** e aniversário em **dezembro**. Deve aparecer o aviso de que você não terá 18 anos no prazo do registro de 2028.
2. Profissões acima da idade aparecem desativadas com "(a partir de N anos)"; **Estudante** está disponível.
3. Estado civil e filhos: com 17 anos, casamento e filhos acima do teto ficam desativados.
4. Na pré-visualização, no lugar do outdoor, agora há **adesivo**, **bandeira** e **peça para redes**. Passe o mouse em cada um.
5. Volte para uma idade de 30 a 45 para o resto do roteiro (com 17 anos a carreira começa fora da disputa até 2032).

## 2. Dez segundos (princípio i)
1. Escolha uma cidade média que você conheça e comece.
2. Assim que a tela central abrir, olhe por 10 segundos e tire a mão do mouse. Responda sem procurar:
   - Que dia é e em que fase você está? (barra de cima: data, **Pré-campanha · cidade · N sem. até a eleição**)
   - Há algo pedindo sua atenção agora?
   - Cite duas coisas que você pode fazer.
3. Se alguma resposta exigiu procurar, anote onde o olho foi primeiro.

## 3. Mapa da cidade
1. O mapa mostra as zonas da sua cidade? Os nomes fazem sentido para quem conhece a cidade? Anote zona com nome estranho ou limite errado. (Nomes de bairro vêm do IBGE; "Centro", "Zona Norte" e semelhantes são nomes do jogo, usados onde não há bairro oficial.)
2. Botões no alto do mapa: **Partido líder**, **Renda**, **Idosos**, **Urbana/rural**, **Cidade inteira**. A legenda embaixo à esquerda explica as cores?
3. Roda do mouse aproxima; botão direito arrasta; duplo clique volta ao enquadramento. Em cidade pequena, o mapa abre na área urbana; **Cidade inteira** mostra a zona rural.
4. Passe o mouse numa zona (tooltip com moradores e partido líder) e clique: a ficha abre no painel da esquerda, com moradores, domicílios, área, renda, idade e intenção por partido. A fonte e o aviso "estimativa por zona" estão claros?
5. Repita numa cidade pequena (3 a 5 zonas) e numa metrópole (12 a 20).

## 4. Tempo
1. Na barra de cima: ícone de **pausa**, **1x**, **2x**, **4x**. Solte em 1x, depois 4x. A data anda dia a dia? Uma semana passa em cerca de 7 segundos em 4x?
2. Deixe correr até o jogo parar sozinho (evento, pesquisa publicada, início da campanha). O motivo aparece? O evento abre num cartão sobre o mapa, com os painéis dos lados visíveis?
3. Com um cartão aberto, os botões de velocidade ficam desativados; o tooltip diz por quê?

## 5. Antes e depois do clique (princípio f)
1. Área **Campanha** (primeiro ícone do trilho): passe o mouse em cada ação. O tooltip diz o que é, o que faz (com faixa, por exemplo "reconhecimento +2,1 a +4,3") e quanto custa?
2. Faça três ações. Em **O que mudou**, cada linha diz o quê e quanto mudou? O resultado ficou dentro da faixa prevista?
3. Num evento, cada escolha mostra a **Previsão** embaixo do botão. Depois de escolher, o cartão mostra **O que mudou**.

## 6. Nada sem explicação (princípio j)
1. Procure por 2 minutos um controle sem tooltip na tela central. Anote cada um.
2. Ache um botão desativado (o **Comício** na pré-campanha): o tooltip começa com "Bloqueado:" e diz quantas semanas faltam?
3. Clique em três termos sublinhados (por exemplo "reconhecimento", "imagem", "margem de erro", "pré-campanha"). O verbete abre e você entendeu?
4. Aperte **F1** na tela central e no menu principal. Abra **Como funciona**, **Este mundo** (partidos, institutos e rivais do seu save) e **Sua carreira**.
5. Todo nome fictício que apareceu veio com o que é? ("Prisma (instituto de pesquisa)", por exemplo, no tooltip de Finanças)
6. A dica de primeira vez apareceu em cada área? Clique **Entendi** numa, saia e volte: ela não pode reaparecer. **Sem dicas** desliga todas.

## 7. O que ainda está nas telas antigas
1. Se chegar ao mandato ou à prefeitura, as telas são as da Fase 4 e 5 (PENDENCIAS P-01). Anote só se algo quebrou.

## O que avaliar
- **Beleza:** a tela parece um jogo que você abriria de novo? O que ainda está feio?
- **Clareza:** as três perguntas dos 10 segundos.
- **Ritmo:** as velocidades estão boas? O jogo para demais ou de menos?
- **Mapa:** as zonas ajudam a enxergar a cidade? Os nomes convencem?
- **Explicação:** faltou explicar alguma coisa? Sobrou texto em algum tooltip?
- **Defeitos:** cortes de texto, travamentos, controles que não respondem.

## O que ainda não muda nesta parte
As 6 ações continuam com efeito igual em qualquer zona; rivais ainda não têm plano; não há equipe, verba por canal, plataforma nem lideranças. Isso é a R2a, a R2b e a R3. Se a tela agradar e o jogo ainda parecer raso, é o esperado nesta etapa.

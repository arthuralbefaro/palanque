# Roteiro de teste — fase-r-final (tooltips fora da tela central, Opções → Tempo e mapa de cidade pequena)

> Tempo: 10 minutos. A `fase-r-final` fecha o que a R1 deixou pendente: tooltips em três partes e links do glossário nas telas que ficam fora da tela central (P-02), a tela de Opções com a aba Tempo (P-03) e a conferência do mapa de cidade pequena (P-09). O visual das telas não mudou: só ganharam tooltip e link. Capturas de referência em `docs/referencias/r-final/`.

## Antes
1. Compile: `godot --headless --import --path .` e `godot --headless --build-solutions --quit`.
2. Jogue uma vez em janela de 1366×768 e uma vez em 1920×1080. Anote ou capture qualquer texto cortado, sobreposto ou pequeno demais.
3. Em cada tela abaixo, a regra é a mesma: todo botão, lista, caixa de marcar, campo e barra deslizante, com o mouse parado em cima, mostra um tooltip com **o que é**, **O que faz:** e **Quanto custa:**. Controle apagado começa com **Bloqueado:** e diz o que falta. Palavra sublinhada é termo do glossário: o clique abre o verbete na Enciclopédia.

## 1. Menu principal e Opções (2 min)
1. No menu, passe o mouse em **Novo Jogo**, **Carregar**, **Opções** e **Sair**.
2. Clique **Opções**. A janela abre na aba **Tempo**, com **Pausas automáticas** e **Dicas de primeira vez**.
3. Cada pausa tem a caixa de marcar e, à direita, o estado em palavras (**ligada**, **desligada** ou **sempre ligada**). Passe o mouse numa desligável, por exemplo **Pesquisa publicada**: o tooltip diz o que acontece com ela desligada (onde o acontecimento continua à vista)?
4. Passe o mouse em **Decisão imediata** ou **Mudança de fase**: a caixa está apagada e o tooltip começa com **Bloqueado:** e diz por que ela não se desliga.
5. As pausas do mandato e da prefeitura estão na lista: **Votação na pauta da Câmara sem o seu voto marcado**, **Achado da fiscalização do vereador esperando decisão**, **Data-base dos servidores**, **Cobrança de promessa ou dívida do Livro de compromissos**, **Luto oficial** e **Obra sua pronta para a entrega**. As da R3 também: **Ataque contra você** e **Decisão com prazo (oferta de doação)**.
6. **Religar as dicas** está apagado com o motivo (nenhuma dica foi fechada ainda). **Fechar** (ou Esc).

## 2. Novo Mundo e criação (3 min)
1. **Novo Jogo**. No Novo Mundo, passe o mouse na **Predefinição**, em **Sortear tudo**, **Restaurar predefinição**, **Adicionar partido**, **Salvar cenário**, **Carregar cenário**, em **Ano inicial**, **Dificuldade**, **Eventos**, **Realismo jurídico**, **Modo Ironman**, e numa linha da tabela (sigla, nome, número, família, cores, logotipo, cúpula, o dado e o cadeado de cada campo, **Duplicar** e **Remover**).
2. Escolha a predefinição **Brasil 2026** (cenário espelhado): **Adicionar partido**, **Duplicar** e **Remover** ficam apagados e dizem por quê.
3. Atenção a dois tooltips que dizem a verdade incômoda: **Dificuldade**, **Realismo jurídico** e **Cúpula** ficam gravados no mundo, mas ainda não mudam a simulação.
4. **Continuar**. Na criação, passe o mouse nas cinco abas (**Aparência**, **Vestuário**, **Identidade**, **Campanha**, **Partido**) e em alguns controles de cada uma. Os de aparência dizem que a aparência nunca altera votos nem atributos.
5. Em **Identidade**, clique **Escolher cidade**: no seletor, passe o mouse na busca, nos filtros **Todas as UFs** e **Todas as regiões**, na lista e em **Escolher esta cidade** (apagado até escolher um nome). Escolha uma cidade média ou maior.
6. Em **Partido**, marque **Criar meu partido** e passe o mouse nos campos novos (os do estatuto também dizem que ainda não mudam a simulação). Volte a **Filiar-se a um partido existente** e escolha um partido.
7. Apague o nome de urna: **Começar carreira** fica apagado e o tooltip diz o que falta. Preencha de novo e comece.

## 3. Tela central: Opções pela barra de cima (2 min)
1. Na barra de cima, ao lado de **Menu**, clique **Opções**. O tempo para.
2. Desmarque **Domingo com a semana seguinte sem nenhum compromisso marcado**: o estado à direita vira **desligada**. **Fechar**.
3. Deixe a semana seguinte vazia e solte o tempo em **1x** até o domingo: o jogo não para mais com o aviso da semana vazia.
4. Feche uma dica de primeira vez com **Entendi** (a da Agenda, por exemplo). Abra **Opções** de novo: **Religar as dicas** está liberado. Clique, **Fechar**: a dica volta na área.
5. Saia para o menu e entre de novo: a pausa continua desligada (as opções ficam gravadas fora do save).

## 4. Debate, urna e apuração (2 min)
1. Dispute a prefeitura numa cidade média ou maior (**Campanha → Disputar para prefeita/prefeito**) e deixe o tempo correr até o debate. Na tela do debate, passe o mouse em **Alvo do ataque**, em cada **Jogar** (o tooltip traz a carta e o risco) e, no fim, em **Voltar à campanha**.
2. No dia da eleição, na urna, passe o mouse nas teclas, em **BRANCO**, **CORRIGE**, **CONFIRMA**, **Pular: votar em mim mesmo** e **Pular: não votar**. Digite os dois algarismos de um partido para vereador: a frase **Aperte CONFIRMA para votar na legenda** tem **legenda** sublinhado.
3. Na apuração, **Pular para o resultado**, **Quociente eleitoral** sublinhado, **Tomar posse** (ou **Continuar a carreira**) e **Nova carreira**.

## 5. Mapa de cidade pequena (1 min)
1. Comece uma carreira em Mesquita (MG) ou Mantena (MG). O mapa abre no núcleo urbano e no modo **Minha força**: as zonas têm tons diferentes, mesmo antes de qualquer compromisso?
2. Troque para **Partido líder**: com um partido à frente em todas as zonas, o tom muda com a vantagem (mais forte onde ele vence com folga). Os nomes de zona não se cobrem.
3. **Cidade inteira** e **Núcleo urbano**: os nomes continuam legíveis e dentro do mapa?
4. **Mapa** (barra de cima) abre o mapa do estado: passe o mouse no seletor de partido e em **Voltar**.

# Pendências

Bloqueios e decisões novas surgidos durante o corujão da Fase R (autorização de 2026-11-16). Cada item: parte, o que ficou faltando, o que foi feito provisoriamente, o que o autor precisa fazer e como reverter.

## R1 — com pendências

### P-01 · Mandato de vereador e prefeitura ainda nas telas antigas
- **Falta:** o item "Painéis com o conteúdo atual" da R1 pede que mandato e prefeitura virem áreas da tela central e que as telas antigas saiam. Só a campanha foi para a tela central.
- **Provisório:** fora da campanha, o jogo usa as telas de mandato, prefeitura e entressafra como estavam. Na campanha de reeleição, o vereador já usa a tela central.
- **Para fechar:** levar pauta, votos e ações de gabinete (mandato) e orçamento, políticas, secretarias e crise (prefeitura) para áreas do trilho, com tooltips em três partes; o tempo corrente do mandato (1 s por dia em 1x) já está em `data/time.json`.
- **Do autor:** nada; é trabalho pendente.

### P-02 · Tooltips em três partes só na tela central e na Enciclopédia
- **Falta:** o teste `explicacao` percorre a tela central (7 áreas, 4 modos do mapa, ficha de zona) e a Enciclopédia. Criação de personagem, Novo Mundo, urna, apuração, debate, mandato e prefeitura ainda têm tooltips antigos ou nenhum.
- **Para fechar:** estender o teste a essas telas e preencher os tooltips.

### P-03 · Opções → Tempo sem tela
- **Falta:** as pausas automáticas são configuráveis no código (`Settings.SetPause`, padrões em `data/time.json`), mas não há tela de Opções para o jogador ligar ou desligar cada uma, nem para religar as dicas.
- **Provisório:** valem os padrões de `data/time.json`.

### P-04 · Artigos de lei citados de memória nos tooltips do material de campanha · **adiada para a R3 (D-037)**
- **O quê:** os tooltips de adesivo (Lei 9.504/1997, art. 38), bandeira (art. 37, § 6º) e peça de redes (art. 57-C) e o verbete "O que a lei permite na propaganda" citam artigos de memória.
- **Decisão do autor (D-037):** a conferência fica para a R3. Até lá, esses textos e os verbetes "Pré-campanha" e "Horário eleitoral gratuito" mostram a marca "(artigo a conferir)" (`a_conferir` em `data/encyclopedia/glossary.json`).
- **Para fechar na R3:** conferir no texto oficial, corrigir o que estiver errado e tirar a marca.

### P-05 · Intenção por zona é estimativa · **fechada (D-037)**
- **O quê:** na R1, a intenção de voto de cada zona é a da cidade repesada pelo perfil de idade e pela situação (urbana ou rural) da zona (`ZoneProjection`). A tela diz "estimativa por zona". O voto calculado zona a zona, com presença por zona, é a R2a.
- **Mapeamento de idade (escolha de jogo):** a faixa de 15 a 19 anos do Censo é repartida igualmente: 2/5 para 16 e 17 anos e 2/5 para 18 a 34; a de 30 a 39, metade para 18 a 34 e metade para 35 a 59.
- **Decisão do autor (D-037):** a aproximação basta até a R2a.

### P-06 · Termos de uso das fontes do IBGE · **fechada (D-037), com lembrete de lançamento**
- **O quê:** a R1 usa a Malha de Setores e os agregados por setor do Censo 2022 citando a fonte (`data/zonas/_fontes.json` e `assets/CREDITS.md`), como autorizado (GDD 17.20, item 7).
- **Decisão do autor (D-037):** termos aceitos para uso com citação da fonte "IBGE".
- **Antes do lançamento:** conferir o ofício original.

### P-07 · Boa Esperança do Norte (MT) sem setores
- **O quê:** o município foi criado depois do Censo 2022 e não tem setores no agregado. Ficou com uma zona só, com o contorno municipal e sem perfil por zona; a lacuna está em `data/zonas/_fontes.json`. É a mesma lacuna já registrada para o perfil municipal (D-011 e 1B).

### P-08 · Tamanho de data/zonas/ · **fechada (D-037)**
- **O quê:** os atributos das zonas (`data/zonas/`, cerca de 15 MB) não tinham teto; o teto de 30 MB do autor vale para os contornos (`data/geo/zonas/`, 20,5 MB).
- **Decisão do autor (D-037):** 15 MB aceitos; teto de 20 MB para `data/zonas/`. O importador mede a saída e falha, sem gravar, se passar (`ZonasPaths.MaxZonasBytes`); o teste `Toda_zona_tem_contorno_e_os_contornos_cabem_no_teto` confere a pasta.

### P-09 · Mapa em cidade pequena (autocrítica visual, segunda rodada)
- **O quê:** em cidade pequena, as zonas urbanas são minúsculas perto da zona rural; o enquadramento inicial aproxima o núcleo urbano, mas os rótulos de zonas vizinhas se sobrepõem ("Centro" e "Zona Nordeste" em Mesquita/MG) e, quando um partido lidera em todas as zonas, o modo Partido líder fica de uma cor só. Captura: `docs/referencias/r1/central_cidade_pequena_1920.png`.
- **Para fechar:** rótulos com desvio de colisão; o modo "Minha força" da R2a (presença do jogador por zona) dá contraste mesmo quando o líder é o mesmo; considerar subdividir a zona rural em cidades pequenas com rural dominante.

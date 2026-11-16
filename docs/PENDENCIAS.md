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

### P-04 · Artigos de lei citados de memória nos tooltips do material de campanha
- **O quê:** os tooltips de adesivo (Lei 9.504/1997, art. 38), bandeira (art. 37, § 6º) e peça de redes (art. 57-C) e o verbete "O que a lei permite na propaganda" citam artigos de memória.
- **A confirmar pelo autor**, ou na R3, quando os artigos de campanha forem conferidos no texto oficial (já é item da R3).
- **Como reverter:** textos em `src/UI/CampaignMaterials/CampaignMaterials.cs` e `data/encyclopedia/glossary.json`.

### P-05 · Intenção por zona é estimativa
- **O quê:** na R1, a intenção de voto de cada zona é a da cidade repesada pelo perfil de idade e pela situação (urbana ou rural) da zona (`ZoneProjection`). A tela diz "estimativa por zona". O voto calculado zona a zona, com presença por zona, é a R2a.
- **Mapeamento de idade (escolha de jogo):** a faixa de 15 a 19 anos do Censo é repartida igualmente: 2/5 para 16 e 17 anos e 2/5 para 18 a 34; a de 30 a 39, metade para 18 a 34 e metade para 35 a 59.
- **A confirmar pelo autor:** se a aproximação basta até a R2a.

### P-06 · Termos de uso das fontes do IBGE
- **O quê:** a R1 usa a Malha de Setores e os agregados por setor do Censo 2022 citando a fonte (`data/zonas/_fontes.json` e `assets/CREDITS.md`), como autorizado (GDD 17.20, item 7). O autor confere os termos antes da RD.

### P-07 · Boa Esperança do Norte (MT) sem setores
- **O quê:** o município foi criado depois do Censo 2022 e não tem setores no agregado. Ficou com uma zona só, com o contorno municipal e sem perfil por zona; a lacuna está em `data/zonas/_fontes.json`. É a mesma lacuna já registrada para o perfil municipal (D-011 e 1B).

### P-08 · Tamanho de data/zonas/
- **O quê:** os atributos das zonas (`data/zonas/`, cerca de 15 MB) não têm teto; o teto de 30 MB do autor vale para os contornos (`data/geo/zonas/`, 20,5 MB).
- **A confirmar pelo autor:** se os 15 MB de atributos são aceitáveis no repositório. Se não, o perfil por zona pode ir para um arquivo binário ou ser reduzido às faixas usadas.

### P-09 · Mapa em cidade pequena (autocrítica visual, segunda rodada)
- **O quê:** em cidade pequena, as zonas urbanas são minúsculas perto da zona rural; o enquadramento inicial aproxima o núcleo urbano, mas os rótulos de zonas vizinhas se sobrepõem ("Centro" e "Zona Nordeste" em Mesquita/MG) e, quando um partido lidera em todas as zonas, o modo Partido líder fica de uma cor só. Captura: `docs/referencias/r1/central_cidade_pequena_1920.png`.
- **Para fechar:** rótulos com desvio de colisão; o modo "Minha força" da R2a (presença do jogador por zona) dá contraste mesmo quando o líder é o mesmo; considerar subdividir a zona rural em cidades pequenas com rural dominante.

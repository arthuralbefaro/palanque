# Roteiro de teste — RD (dados reais de partida)

> Parte de dados: quase nada muda na tela. Tempo: 20 minutos. Tag: `fase-rd`. Abra o jogo com `godot --path .` em 1366×768 e depois em 1920×1080; se algum texto aparecer cortado, capture.

## 1. Conferir com o que você conhece (8 min)
1. Comece uma carreira num município que você conhece bem. Aperte **F1** e abra **Este mundo**: o primeiro cartão é "**(nome do município) (UF): números oficiais de partida**".
2. Leia os números: IDEB dos anos iniciais (rede municipal), mortalidade infantil, leitos por mil habitantes, homicídios por 100 mil, domicílios ligados à rede de água e de esgoto, empregos formais por 100 habitantes, despesa com pessoal em % da receita corrente líquida, receita corrente por habitante, PIB por habitante e a receita corrente da prefeitura.
3. Cada número traz a unidade, a fonte e o ano. Algum parece errado para quem conhece a cidade? Anote o município, o indicador e o que você esperava.
4. Repita com um município pequeno (menos de 20 mil habitantes). A mortalidade infantil deve vir marcada "taxa da região imediata" quando o município tem menos de 300 nascidos vivos em 3 anos.
5. Repita com Brasília: o IDEB vem marcado "rede distrital" e as finanças são as do Distrito Federal.

## 2. Lacunas (4 min)
1. Abra `data/indicadores_reais/_fontes.json` e vá ao fim, em `resumo` e `lacunas`. Há 2.681 lacunas registradas: 2.323 de mortalidade infantil pela região imediata, 251 de IDEB, 70 de finanças, 35 de despesa com pessoal, 1 de PIB e 1 de saneamento (Boa Esperança do Norte/MT, criado depois do Censo).
2. Procure por "descartada": são as declarações ao Tesouro com erro evidente (por exemplo, despesa com pessoal de 805% da receita). O jogo não corrige o número: descarta, usa a regra de substituição e registra o valor declarado.
3. Escolha um município da lista e confira no jogo (F1 → Este mundo) se o número aparece com a marca da substituição.

## 3. Fontes e créditos (3 min)
1. `assets/CREDITS.md` lista cada fonte nova com órgão, conjunto e ano?
2. Em `data/indicadores_reais/_fontes.json`, o bloco `fontes` traz endereço e data de download de cada campo. Abra um endereço e confira um número de um município.
3. `data/economy/start.json` tem os números nacionais de partida (PIB, IPCA em 12 meses, Selic, câmbio, desocupação, dívida). Batem com o que você vê nas páginas oficiais?

## 4. Zonas (3 min)
1. Os dados novos das zonas estão em `data/zonas/{UF}.json` (campos `eleitores`, `eleitores_fonte`, `perfil.religiao` e `perfil.escolaridade`); a ficha da zona só passa a usá-los na tag `fase-r2a`.
2. Em `data/zonas/_fontes.json`, o bloco `zonas_rd` diz quantas zonas ganharam religião e escolaridade pelas áreas de ponderação (16.592) e quantos municípios têm eleitores por local de votação (5.361).

## 5. Decisões que dependem de você (2 min)
Estão em `docs/PENDENCIAS.md`, de P-10 a P-14:
- eleitores por zona: o TSE diz onde se vota, não onde se mora; 4.344 zonas habitadas ficam sem eleitor. O que entra no modelo de voto?
- parâmetros de jogo novos (mortalidade, faixas plausíveis, mínimo de nascidos vivos);
- homicídios só por agressão (sem intervenção legal);
- termos de uso das fontes que não são do IBGE;
- o que ficou de fora: votação de 2024 por zona, transferências (FPM e FPE) e saldo do CAGED.

## O que avaliar
- Os números reais deixam o mundo mais crível?
- Alguma estatística, mostrada assim, poderia ser lida como julgamento sobre uma cidade ou região? (O jogo só mostra o dado e a fonte.)
- O tamanho dos arquivos novos está aceitável? `data/indicadores_reais/` 6,3 MB; `data/zonas/` 16,8 MB (teto de 20 MB); `data/demography/` 0,5 MB.

## O que ainda não muda nesta parte
Os indicadores da prefeitura continuam na escala de 0 a 100 da tela antiga; a conversão para a unidade real, o modelo de evolução e a aprovação pela variação entram na R4. Só a receita da prefeitura já usa o número real.

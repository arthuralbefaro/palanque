# Roteiro de teste — RD (dados reais de partida)

> Escrito antes da implementação, a partir do GDD 17.16 e 17.17. Parte de dados: quase nada muda na tela. Tempo: 15 minutos. Ao fim da RD é atualizado com os caminhos reais dos arquivos.

## 1. Conferir com o que você conhece
1. Escolha três municípios que você conhece bem (um pequeno, um médio, um grande).
2. Na ficha de cada um (seletor de município e Enciclopédia → Este mundo), leia os indicadores importados: população, PIB, receita da prefeitura, educação, saúde, segurança, saneamento e emprego formal.
3. Algum número parece errado para quem conhece a cidade? Anote o município, o indicador e o que você esperava.
4. Cada número tem tooltip com a fonte e o ano?

## 2. Lacunas
1. Abra `data/municipios/_fontes.json` (ou o relatório do importador) e veja a lista de lacunas por indicador.
2. Escolha um município com lacuna e confira no jogo: o número aparece marcado como substituído, com a regra usada?

## 3. Fontes e créditos
1. `assets/CREDITS.md` lista cada fonte nova com órgão, conjunto e ano?
2. Para duas fontes à sua escolha, abra o endereço registrado e confira um número de um município.

## 4. Decisões que dependem de você
1. O relatório da RD traz o tamanho dos arquivos novos em `data/`. Está aceitável para o repositório e para o jogo?
2. Fonte de homicídios: o relatório compara as fontes oficiais nos mesmos três municípios. Qual você prefere?

## O que avaliar
- Os números reais deixam o mundo mais crível?
- Alguma estatística, mostrada assim, poderia ser lida como julgamento sobre uma cidade ou região? (O jogo só mostra o dado e a fonte.)

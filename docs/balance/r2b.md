# BalanceSim — campanha de vereador (R2b)

Robô que marca, em cada turno livre, o compromisso de maior ganho previsto de votos e, toda semana, contrata (o candidato de melhor qualidade esperada que o caixa sustenta), reparte a verba pelos canais (em parcelas, sempre no destino em que a próxima parcela rende mais votos), assume nas bandeiras a posição de maior ganho previsto, faz promessas enquanto a credibilidade está inteira, delega caminhadas e responde aos eventos da equipe. 48 campanhas por porte, em 8 cidades sorteadas por porte (semente 1), metade no partido mais forte da cidade e metade num partido sorteado.

Critérios (GDD 17.1 e ROADMAP, R2b):
- princípio a: nenhum tipo de compromisso é a melhor escolha em mais de 40% das semanas; nenhum par (tipo, zona) em mais de 25%;
- verba: nenhum canal é o melhor destino (o da primeira parcela, com o plano vazio) em mais de 40% das semanas. Medido nas semanas da campanha oficial, em que os quatro canais estão abertos; na pré-campanha só há dois (Redes sem impulsionamento e Material institucional), e a divisão entre eles vai na tabela como informação;
- princípio e: média de 1 ou mais decisões com troca por semana (pedido de liderança, evento, evento da equipe, conselho da semana, convite, contratação, mudança de posição, promessa local).

## Porte pequeno

48 campanhas concluídas, 0 com erro, em 209 s. Cidades: Porto Walter/AC, São José da Safira/MG, Campo Redondo/RN, Sertão Santana/RS, Piedade do Rio Grande/MG, Nova Esperança do Sudoeste/PR, Gravatal/SC, Agricolândia/PI.

| Tipo de compromisso | Semanas em que teve a opção de maior ganho | Turnos em que foi a melhor escolha | Semanas em que foi o mais marcado |
|---|---|---|---|
| Ir a evento da cidade | 27,0% | 3,9% | 0,1% |
| Visita a liderança | 26,9% | 9,8% | 7,9% |
| Caminhada | 16,7% | 32,2% | 42,3% |
| Reunião com apoiadores | 16,5% | 8,5% | 9,2% |
| Entrevista | 7,4% | 29,8% | 30,9% |
| Gravação para as redes | 3,7% | 14,9% | 7,8% |
| Comício | 1,8% | 0,8% | 1,8% |

- Tipo com a opção de maior ganho da semana: **27,0%** das semanas (limite 40%). Informativo: o tipo mais marcado foi a melhor escolha em 32,2% dos turnos e o mais marcado da semana em 42,3% das semanas.
- Par (tipo, zona) mais repetido: **29,8%** das semanas no conjunto das campanhas (limite 25%), medido turno a turno; 38,9% na pior campanha isolada pela opção de maior ganho da semana.
- Eleitos: 22 de 24 no partido mais forte; 1 de 24 em partido sorteado. Posição mediana entre os nomeados: 9.
- Por campanha, em média: 521 compromissos, 5,1 decisões de pedido de liderança, 8,4 promessas no Livro, 2,2 lideranças aliadas no fim, 24,4 eventos; reconhecimento final 66,4%.
- Turnos de escolha forçada (arrecadar sem caixa ou descansar com estresse alto), fora da estatística acima: 15,3%.

| Canal de verba | Melhor destino na campanha oficial | Melhor destino na pré-campanha | Verba planejada por campanha | Votos por R$ 1.000 da primeira parcela (campanha oficial) |
|---|---|---|---|---|
| Cabos eleitorais | 45,0% | 0,0% | R$ 51 | 25,07 |
| Redes | 20,1% | 57,9% | R$ 1.452 | 13,98 |
| Rua | 19,0% | 0,0% | R$ 371 | 20,61 |
| Material gráfico | 15,9% | 42,1% | R$ 2.214 | 12,77 |

- Canal que mais vezes foi o melhor destino: **45,0%** das semanas da campanha oficial (limite 40%), em 6,0 semanas por campanha. Semanas com verba repartida, por campanha: 19,5 de 38,0.
- Decisões com troca: **2,37** por semana (mínimo 1). Por tipo, por campanha: conselho da semana 30,5; evento 24,4; convite 15,9; evento da equipe 7,0; pedido de liderança 5,1; promessa local 2,5; contratação 2,4; mudança de posição 2,3.
- Equipe: 2,4 contratações por campanha, 1,6 pessoas na equipe no fim, 7,0 eventos da equipe, 109,4 compromissos delegados. Plataforma: 2,3 mudanças de posição e 2,5 promessas locais.
- Objetivos: 6,7 tipos por campanha (mínimo entre as campanhas: 6; o princípio h pede 4), 5,2 cumpridos.
- **FORA dos critérios.**

## Porte medio

48 campanhas concluídas, 0 com erro, em 273 s. Cidades: Bom Despacho/MG, Nova Xavantina/MT, Ipirá/BA, Aldeias Altas/MA, Tupaciguara/MG, Sobradinho/BA, Arcos/MG, Ouro Fino/MG.

| Tipo de compromisso | Semanas em que teve a opção de maior ganho | Turnos em que foi a melhor escolha | Semanas em que foi o mais marcado |
|---|---|---|---|
| Reunião com apoiadores | 35,9% | 19,5% | 31,6% |
| Caminhada | 22,0% | 39,9% | 57,4% |
| Ir a evento da cidade | 20,6% | 3,7% | 1,1% |
| Visita a liderança | 12,5% | 4,0% | 3,6% |
| Entrevista | 5,6% | 18,1% | 4,4% |
| Gravação para as redes | 3,2% | 14,7% | 1,6% |
| Comício | 0,2% | 0,2% | 0,2% |

- Tipo com a opção de maior ganho da semana: **35,9%** das semanas (limite 40%). Informativo: o tipo mais marcado foi a melhor escolha em 39,9% dos turnos e o mais marcado da semana em 57,4% das semanas.
- Par (tipo, zona) mais repetido: **18,1%** das semanas no conjunto das campanhas (limite 25%), medido turno a turno; 31,0% na pior campanha isolada pela opção de maior ganho da semana.
- Eleitos: 24 de 24 no partido mais forte; 6 de 24 em partido sorteado. Posição mediana entre os nomeados: 4.
- Por campanha, em média: 523 compromissos, 3,9 decisões de pedido de liderança, 6,9 promessas no Livro, 1,8 lideranças aliadas no fim, 24,9 eventos; reconhecimento final 49,5%.
- Turnos de escolha forçada (arrecadar sem caixa ou descansar com estresse alto), fora da estatística acima: 54,9%.

| Canal de verba | Melhor destino na campanha oficial | Melhor destino na pré-campanha | Verba planejada por campanha | Votos por R$ 1.000 da primeira parcela (campanha oficial) |
|---|---|---|---|---|
| Rua | 49,0% | 0,0% | R$ 121 | 59,82 |
| Material gráfico | 21,5% | 57,9% | R$ 3.133 | 54,46 |
| Cabos eleitorais | 14,9% | 0,0% | R$ 27 | 55,70 |
| Redes | 14,6% | 42,1% | R$ 1.575 | 46,75 |

- Canal que mais vezes foi o melhor destino: **49,0%** das semanas da campanha oficial (limite 40%), em 6,0 semanas por campanha. Semanas com verba repartida, por campanha: 22,7 de 38,0.
- Decisões com troca: **2,20** por semana (mínimo 1). Por tipo, por campanha: evento 24,9; convite 21,9; conselho da semana 21,5; evento da equipe 4,8; pedido de liderança 3,9; promessa local 2,4; mudança de posição 2,3; contratação 1,9.
- Equipe: 1,9 contratações por campanha, 1,1 pessoas na equipe no fim, 4,8 eventos da equipe, 31,6 compromissos delegados. Plataforma: 2,3 mudanças de posição e 2,4 promessas locais.
- Objetivos: 6,5 tipos por campanha (mínimo entre as campanhas: 6; o princípio h pede 4), 4,5 cumpridos.
- **FORA dos critérios.**

## Porte grande

48 campanhas concluídas, 0 com erro, em 402 s. Cidades: Taboão da Serra/SP, Cambé/PR, Timon/MA, Teresópolis/RJ, Poços de Caldas/MG, Almirante Tamandaré/PR, Formosa/GO, Araxá/MG.

| Tipo de compromisso | Semanas em que teve a opção de maior ganho | Turnos em que foi a melhor escolha | Semanas em que foi o mais marcado |
|---|---|---|---|
| Reunião com apoiadores | 36,3% | 19,2% | 29,1% |
| Caminhada | 32,0% | 49,2% | 64,1% |
| Ir a evento da cidade | 15,9% | 2,9% | 1,5% |
| Visita a liderança | 9,9% | 3,5% | 3,7% |
| Entrevista | 4,8% | 14,2% | 1,5% |
| Gravação para as redes | 1,2% | 11,1% | 0,1% |

- Tipo com a opção de maior ganho da semana: **36,3%** das semanas (limite 40%). Informativo: o tipo mais marcado foi a melhor escolha em 49,2% dos turnos e o mais marcado da semana em 64,1% das semanas.
- Par (tipo, zona) mais repetido: **14,2%** das semanas no conjunto das campanhas (limite 25%), medido turno a turno; 26,9% na pior campanha isolada pela opção de maior ganho da semana.
- Eleitos: 23 de 24 no partido mais forte; 8 de 24 em partido sorteado. Posição mediana entre os nomeados: 5.
- Por campanha, em média: 521 compromissos, 4,7 decisões de pedido de liderança, 7,1 promessas no Livro, 2,5 lideranças aliadas no fim, 25,2 eventos; reconhecimento final 42,5%.
- Turnos de escolha forçada (arrecadar sem caixa ou descansar com estresse alto), fora da estatística acima: 54,8%.

| Canal de verba | Melhor destino na campanha oficial | Melhor destino na pré-campanha | Verba planejada por campanha | Votos por R$ 1.000 da primeira parcela (campanha oficial) |
|---|---|---|---|---|
| Rua | 49,5% | 0,0% | R$ 365 | 47,81 |
| Cabos eleitorais | 19,7% | 0,0% | R$ 198 | 48,75 |
| Material gráfico | 15,9% | 61,6% | R$ 5.958 | 43,42 |
| Redes | 14,9% | 38,4% | R$ 3.104 | 40,49 |

- Canal que mais vezes foi o melhor destino: **49,5%** das semanas da campanha oficial (limite 40%), em 6,0 semanas por campanha. Semanas com verba repartida, por campanha: 29,7 de 38,0.
- Decisões com troca: **1,98** por semana (mínimo 1). Por tipo, por campanha: convite 26,9; evento 25,2; conselho da semana 10,4; pedido de liderança 4,7; promessa local 2,6; evento da equipe 2,4; mudança de posição 2,3; contratação 0,7.
- Equipe: 0,7 contratações por campanha, 0,5 pessoas na equipe no fim, 2,4 eventos da equipe, 14,9 compromissos delegados. Plataforma: 2,3 mudanças de posição e 2,6 promessas locais.
- Objetivos: 6,6 tipos por campanha (mínimo entre as campanhas: 6; o princípio h pede 4), 3,6 cumpridos.
- **FORA dos critérios.**

## Porte metropole

48 campanhas concluídas, 0 com erro, em 685 s. Cidades: Osasco/SP, Belo Horizonte/MG, Porto Velho/RO, Fortaleza/CE, Aparecida de Goiânia/GO, Salvador/BA, Santo André/SP, Jaboatão dos Guararapes/PE.

| Tipo de compromisso | Semanas em que teve a opção de maior ganho | Turnos em que foi a melhor escolha | Semanas em que foi o mais marcado |
|---|---|---|---|
| Caminhada | 60,6% | 62,6% | 82,3% |
| Ir a evento da cidade | 13,1% | 2,2% | 1,5% |
| Reunião com apoiadores | 12,3% | 9,3% | 8,5% |
| Visita a liderança | 7,9% | 3,0% | 3,2% |
| Entrevista | 5,3% | 12,8% | 4,0% |
| Gravação para as redes | 0,7% | 10,1% | 0,4% |

- Tipo com a opção de maior ganho da semana: **60,6%** das semanas (limite 40%). Informativo: o tipo mais marcado foi a melhor escolha em 62,6% dos turnos e o mais marcado da semana em 82,3% das semanas.
- Par (tipo, zona) mais repetido: **12,8%** das semanas no conjunto das campanhas (limite 25%), medido turno a turno; 29,6% na pior campanha isolada pela opção de maior ganho da semana.
- Eleitos: 9 de 24 no partido mais forte; 1 de 24 em partido sorteado. Posição mediana entre os nomeados: 9.
- Por campanha, em média: 517 compromissos, 4,0 decisões de pedido de liderança, 6,5 promessas no Livro, 1,8 lideranças aliadas no fim, 23,3 eventos; reconhecimento final 27,9%.
- Turnos de escolha forçada (arrecadar sem caixa ou descansar com estresse alto), fora da estatística acima: 60,9%.

| Canal de verba | Melhor destino na campanha oficial | Melhor destino na pré-campanha | Verba planejada por campanha | Votos por R$ 1.000 da primeira parcela (campanha oficial) |
|---|---|---|---|---|
| Rua | 39,2% | 0,0% | R$ 988 | 22,47 |
| Redes | 27,1% | 37,7% | R$ 8.062 | 18,92 |
| Cabos eleitorais | 19,8% | 0,0% | R$ 625 | 21,06 |
| Material gráfico | 13,9% | 62,3% | R$ 13.312 | 19,95 |

- Canal que mais vezes foi o melhor destino: **39,2%** das semanas da campanha oficial (limite 40%), em 6,0 semanas por campanha. Semanas com verba repartida, por campanha: 35,6 de 38,0.
- Decisões com troca: **1,82** por semana (mínimo 1). Por tipo, por campanha: convite 31,0; evento 23,3; conselho da semana 4,7; pedido de liderança 4,0; promessa local 2,8; mudança de posição 2,3; evento da equipe 1,0; contratação 0,3.
- Equipe: 0,3 contratações por campanha, 0,2 pessoas na equipe no fim, 1,0 eventos da equipe, 3,1 compromissos delegados. Plataforma: 2,3 mudanças de posição e 2,8 promessas locais.
- Objetivos: 6,6 tipos por campanha (mínimo entre as campanhas: 6; o princípio h pede 4), 2,7 cumpridos.
- **FORA dos critérios.**

## Resumo e calibração da R2b

Medição final (as tabelas acima): 48 campanhas por porte, 8 cidades, semente 1. Entre parênteses, o número do relatório da R2a (`docs/balance/r2a.md`, robô só de agenda), quando o critério já existia.

| Porte | Tipo de compromisso (até 40%) | Par tipo e zona (até 25%) | Canal de verba (até 40%) | Decisões com troca por semana (1 ou mais) | Eleitos no partido mais forte | Turnos forçados |
|---|---|---|---|---|---|---|
| Pequeno | 27,0% (30,2%) | **29,8%** (22,6%) | **45,0%**, Cabos | 2,37 | 22 de 24 (20) | 15,3% (7,7%) |
| Médio | 35,9% (34,1%) | 18,1% (15,5%) | **49,0%**, Rua | 2,20 | 24 de 24 (24) | 54,9% (32,3%) |
| Grande | 36,3% (41,4%) | 14,2% (12,6%) | **49,5%**, Rua | 1,98 | 23 de 24 (24) | 54,8% (38,4%) |
| Metrópole | **60,6%** (59,8%) | 12,8% (11,5%) | 39,2%, Rua | 1,82 | 9 de 24 (16) | 60,9% (48,8%) |

Em negrito, o que ficou fora do critério.

### As três rodadas

Cada rodada foi medida com 16 campanhas em 4 cidades por porte. Coluna da direita: fatia do canal que mais vezes foi o melhor destino, em pequeno / médio / grande / metrópole.

| Rodada | O que mudou | Canal mais repetido |
|---|---|---|
| medição de partida | `channels.json` como escrito de início (Rua 1,2; Redes 1,0 de presença e 0,008 de apoio; Material 0,9; Cabos só apoio, 0,012) | 58% / 76% / 93% / 100% |
| 1 | rendimento por real igualado entre os canais (Rua 1,0; Redes 4,5 e 0,036; Cabos com presença 0,8 e apoio 0,1; grupo de cabos de R$ 300 para R$ 200). A estatística do melhor destino deixou de depender do caixa do robô | 60% / 69% / 75% / 88% |
| 2 | custos trazidos para o caixa que existe antes dos doadores da R3: salários pela metade (125 a 750 por semana no porte médio), escala da verba de 2.000 para 1.000, grupo de cabos a R$ 100, verba de Rua do comício de 1.500 para 800, teto da pré-campanha de 300 para 150, verba dos rivais de 600 para 300. Regra nova: **saturação da verba** (princípio g), cada semana de verba no mesmo canal e alvo rende menos (fator 0,5; cai 0,34 por semana). Cabos com presença 1,0 e apoio 0,12. Imagem perdida no vídeo que pega mal de 0,03 para 0,015. Correção: falar da posição padrão (a do partido) não muda mais o que o eleitorado enxerga | 49% / 54% / 48% / 94% |
| 3 | saturação mais forte (fator 0,8). No robô: 6% do caixa na verba toda semana (pelo menos uma parcela), só contrata quem o caixa paga por boa parte da campanha, só delega com a agenda garantida | 45% / 49% / 50% / 39% (medição final) |

### O que ficou fora e por quê

- **Canal de verba acima de 40% em três portes** (45,0%, 49,0% e 49,5%). Os quatro canais rendem quase o mesmo por real (coluna "votos por R$ 1.000"), e a saturação faz o melhor destino rodar quando há verba toda semana, que é o caso da metrópole (verba em 35,6 de 38 semanas; 39,2%). Nos outros portes o robô reparte verba em 20 a 30 semanas e quase tudo na pré-campanha, em que só Redes e Material abrem: na campanha oficial sobra pouco caixa para Rua e Cabos, a saturação deles quase não sobe e o mesmo canal volta a ser o melhor. Tentado: igualar o rendimento, baratear os canais, a saturação (duas forças) e a regra de gasto do robô. Não tentado, por ter acabado o limite de três rodadas: guardar caixa da pré-campanha para a campanha oficial, e pôr na conta do robô o valor indireto de cada canal (a Rua multiplica caminhada e comício, os cabos seguram a presença, o material destrava os cabos).
- **Par (tipo, zona) de 29,8% no porte pequeno.** O par mais repetido é a entrevista, que não tem zona (a fatia do par é igual à fatia de turnos da entrevista, 29,8%, como já era na R2a, com 22,6%). O que mudou na R2b: o robô delega cerca de 109 compromissos por campanha no porte pequeno; esses compromissos somam cansaço às zonas, e a entrevista passa a ser a melhor escolha do próprio candidato mais vezes. É hipótese: não foi isolada numa rodada própria.
- **Tipo de compromisso de 60,6% na metrópole** (caminhada). Já estava em 59,8% no relatório da R2a; a R2b não mexeu nas bases da agenda. No porte grande, o tipo caiu de 41,4% para 36,3%.

### O que a simulação mostrou além dos critérios

- **O caixa não comporta os sistemas novos.** A campanha da R2a mal paga a própria agenda: o jantar de arrecadação repetido rende cada vez menos e os doadores só entram na R3. Com folha e verba, o robô passa mais turnos arrecadando (de 32% para 55% no porte médio, de 49% para 61% na metrópole) e, no partido mais forte, se elege no mesmo nível do robô da R2a nos portes pequeno, médio e grande (22, 24 e 23 de 24, contra 20, 24 e 24) e menos na metrópole (9 de 24 contra 16 de 24). Isso com salários e canais já pela metade do preço de partida. Para a R3: com doadores e fundo partidário, recalibrar salários, escala da verba e verba dos rivais (os números de partida estão na rodada 2).
- **Decisões com troca:** de 1,8 a 2,4 por semana. Sem contar o conselho da semana (que só existe com coordenador), continua acima de 1 em todos os portes.
- **Objetivos:** 6 tipos ou mais em toda campanha simulada (o princípio h pede 4).
- **Comício:** quase não aparece (0,2% a 1,8% das semanas, e nada nos portes grande e metrópole), porque agora depende de verba de Rua na zona e o robô raramente tem caixa para isso na campanha oficial. Na R2a já era raro (0% a 3,3%).

# Briefing: teste pago de cabeça 3D estilizada

**Projeto:** Planalto, jogo independente de simulação de carreira política no Brasil, feito no Godot 4.
**O que é este teste:** uma peça paga e curta, antes de um contrato maior. Se o teste for aprovado, o contrato seguinte cobre as demais cabeças, as expressões completas e as roupas.

| Item | Valor |
|---|---|
| Valor do teste | [preencher] |
| Prazo | [preencher] |
| Contato | [preencher] |
| Arquivo de referência anexo | Corpo base do protótipo (`.glb`), para escala e encaixe do pescoço |

## 1. O que entregar

1. **1 cabeça** adulta, em tom de pele médio. O autor envia a cor de referência; o tom vem da tintura descrita abaixo, não da textura.
2. **3 expressões:** neutra (a malha base), sorriso (`expr_sorriso`) e preocupado (`expr_preocupado`). Entregar também `piscar`, se couber no prazo.
3. **1 cabelo** curto ou médio, em peça separada, encaixado na cabeça.

Arquivos:
- `cabeca_teste.glb` e `cabelo_teste.glb`;
- os arquivos-fonte (`.blend` ou equivalente), com as texturas em PNG;
- uma imagem de cada expressão, de frente e a três quartos, com fundo neutro.

## 2. Estilo

- **3D estilizado, de formas suaves e proporções de desenho animado.** Referências de estilo: Two Point Hospital, Overcooked e Planet Coaster.
- **Não é low-poly facetado e não é realista.** Superfícies lisas, poucos detalhes finos, leitura clara de longe.
- O rosto tem de ser legível em retrato pequeno (96 px de altura) e bonito em retrato grande (512 px).
- As expressões são distinguíveis à primeira vista, sem exagero cômico. É um jogo de política tratado com seriedade.
- O rosto não pode lembrar nenhuma pessoa real, em especial nenhum político.
- A cabeça base será reutilizada para gente de 17 a 90 anos e de toda a diversidade da população brasileira. Variações de formato virão depois, no contrato. Evite traços que prendam a base a uma só origem.

## 3. Formato técnico

- **glTF 2.0 binário (`.glb`).** Y para cima, frente voltada para +Z (padrão do glTF), 1 unidade = 1 metro.
- **Escala:** corpo de referência com 1,60 m. A cabeça mede cerca de 0,30 m do queixo ao topo do crânio, cerca de 5,3 cabeças de altura. Confira no corpo base anexo.
- **Origem:** no nó vazio `encaixe_pescoco`, na base do pescoço, no centro. O pescoço desce 3 cm abaixo desse ponto, para a emenda ficar sob a gola.
- **Pontos de troca:** nós vazios, filhos da raiz da cabeça.
  - `encaixe_cabelo`: topo do crânio; é a origem do cabelo.
  - `encaixe_oculos`: ponte do nariz.
  - `encaixe_brinco_e` e `encaixe_brinco_d`: lóbulos esquerdo e direito.
- **Olhos:** malhas separadas `olho_e` e `olho_d`, com a origem no centro de rotação.
- **Malhas:** `cabeca` (com sobrancelhas, ou malha `sobrancelhas` à parte), `olho_e`, `olho_d`, `dentes`, `lingua`. O cabelo vem em arquivo próprio, na malha `cabelo`, preso por `encaixe_cabelo`, sem pesos de pele.
- **Expressões por shape keys** (morph targets no glTF), na malha `cabeca`, com estes nomes exatos:
  - peso 1 equivale à expressão inteira; a neutra é a malha sem shape key;
  - se as sobrancelhas forem malha à parte, levam as mesmas shape keys com os mesmos nomes.
- **Nomes de expressão do jogo:** `expr_sorriso`, `expr_sorriso_largo`, `expr_preocupado`, `expr_bravo`, `expr_triste`, `expr_surpreso`, `expr_cansado` e `piscar`. No teste, só `expr_sorriso` e `expr_preocupado` são obrigatórias.
- **Ossos:** não são obrigatórios. Um osso `mandibula` é aceito, desde que os nomes acima não mudem.

## 4. Limite de polígonos e texturas

| Peça | Limite |
|---|---|
| Cabeça, com olhos, dentes e língua | 8.000 triângulos |
| Cabelo | 6.000 triângulos |
| Barba (no contrato, não no teste) | 2.000 triângulos |
| Texturas | PNG, até 2048 px (1024 recomendado); UV sem sobreposição |

## 5. Materiais tingíveis

O jogo aplica a cor de cada personagem por cima de texturas em tons de cinza. Por isso, as texturas dos materiais tingíveis **não levam cor**: só luz, sombra e detalhe, em tons de cinza neutros.

| Material (nome exato) | Tingido? | Observação |
|---|---|---|
| `pele` | Sim | Tom de pele da pessoa |
| `labios` | Sim | Derivado do tom de pele |
| `sobrancelha` | Sim | Cor do cabelo |
| `cabelo` | Sim | Textura auxiliar `cabelo_mascara`, canal R = onde o grisalho aparece |
| `iris` | Sim | Cor dos olhos |
| `esclera` | Não | — |
| `dentes` | Não | — |
| `lingua` | Não | — |

- Rugas e marcas, se houver, vão numa textura à parte em tons de cinza, `pele_detalhe`. O jogo controla a intensidade pela idade.
- A imagem de entrega das expressões é feita com o tom médio de referência aplicado. A textura entregue continua em cinza.

## 6. Como o teste será conferido

O jogo tem uma cena de teste que carrega a cabeça e confere quatro coisas:
- os nomes de nós, malhas, materiais e shape keys;
- o limite de triângulos;
- a tintura em 6 tons de pele e 2 cores de cabelo;
- a leitura das expressões nos retratos de 96 px e de 512 px.

O autor julga o estilo pelas referências da seção 2.

## 7. Direitos e licença

- **Cessão total dos direitos patrimoniais** sobre tudo o que for entregue (modelos, texturas, arquivos-fonte e imagens) ao contratante, por escrito, para uso, modificação, distribuição e venda, sem limite de tempo nem de território. Isso inclui publicar os arquivos-fonte num repositório público.
- O artista recebe crédito nominal nos créditos do jogo (`assets/CREDITS.md`) e pode mostrar o trabalho no portfólio depois do lançamento público, ou antes, com autorização.
- **Trabalho original:**
  - sem uso de inteligência artificial generativa;
  - sem partes de terceiros, salvo assets CC0, listados com origem e link;
  - sem semelhança intencional com pessoa real.
- O texto final da cessão é revisado por advogado antes da assinatura. Esta seção resume o que se pede, não é o contrato.

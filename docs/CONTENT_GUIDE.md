# Guia de conteúdo de eventos

Regras para escrever eventos em `data/events/{categoria}/*.json` (GDD 7.5 e 15). Toda mudança passa pelo `dotnet run --project tools/Planalto.EventLint`, que não pode apontar erro.

## Formato
- `id` em minúsculas com `_`, começando pela categoria (`campanha_...`); a pasta é a categoria (uma das 12 da seção 15.2; o EventLint recusa outras).
- `phases`: só `pre_campanha` e/ou `campanha`. São as únicas fases que o motor sorteia hoje (da criação do personagem, ou da abertura da pré-campanha no prazo de filiação, até a véspera da eleição); qualquer outro valor reprova a validação.
- `rarity`: `comum`, `incomum`, `raro` ou `lendario`; `cooldown_weeks` é o intervalo mínimo entre repetições; `unique: true` acontece uma vez por carreira.
- `chain_only: true` marca continuações: só disparam quando agendadas por `schedule` de outra escolha.
- `conditions` (todas opcionais; o evento só entra no sorteio se todas valerem):
  - `chance` (0 a 1), sorteada depois das demais;
  - `money_lt` / `money_gt`: dinheiro do personagem;
  - `recognition_lt` / `recognition_gt` (0 a 100) e `image_lt` / `image_gt` (-1 a 1): os do jogador como candidato na campanha em curso;
  - `stress_gt`;
  - `porte` (ids de `portes.json`) e `regiao` (`N`, `NE`, `CO`, `SE`, `S`): do município em foco;
  - `trait` / `not_trait` (ids de `traits.json`);
  - `flag` / `not_flag`: flags ligadas por `set_flags` de outros eventos;
  - `attribute_min` (`carisma`, `oratoria`, `articulacao`, `gestao`, `integridade`);
  - `weeks_to_election_lt` / `weeks_to_election_gt`: semanas até o 1º turno;
  - `needs_rival`: existe rival nomeado na disputa do jogador (de vereador ou de prefeito, conforme a candidatura);
  - `needs_skeleton`: há esqueleto no armário.
- `variants`: de 1 a 4 aberturas diferentes para o mesmo evento.
- `choices`: de 2 a 4, cada uma com `effects`, `result` e, se houver risco, `test` (atributo, `bonus`, textos e efeitos de sucesso e fracasso). `requires` (`money_min`, `attribute_min`, `flag`, `political_capital_min`) bloqueia a escolha e o jogo mostra o motivo.
- `portrait`: `rival` mostra o rival sorteado; sem o campo, o retrato é do jogador. `portrait_expression` define a expressão.

## Ainda não existem
- **Eventos de mandato.** Nas telas de mandato de vereador, de prefeitura e de entressafra, o motor de eventos não roda: ele depende do estado da campanha (DECISOES D-027 e D-031). As categorias `mandato_legislativo`, `mandato_executivo`, `crises` e `economia` existem no EventLint, mas eventos escritos nelas só disparariam na campanha. As crises da prefeitura são outro sistema, em `data/executive/crises.json`. Não escreva eventos de mandato até o motor ganhar essas fases.

## Variáveis
`{municipio.nome}`, `{municipio.uf}`, `{municipio.porte}`, `{rival.nome}`, `{rival.partido}`, `{partido.sigla}`, `{partido.nome}`, `{jogador.nome}`, `{jogador.pronome}` (ele/ela/elu), `{jogador.o}` (o/a/e, para concordância: `eleit{jogador.o}`), `{ano}`, `{valor}` (quantia em reais proporcional ao porte), `{semanas}` (até a eleição).

## Tom e tamanho
- Texto curto: até três frases na abertura e uma no resultado.
- Segunda pessoa ("você"), PT-BR do dia a dia, sem gírias datadas.
- Toda escolha tem custo e benefício plausíveis; nenhuma é "a certa".

## Neutralidade (CLAUDE.md)
- Nenhum evento cita pessoas, partidos, empresas ou marcas reais.
- Nenhum evento se baseia em estereótipo regional, religioso, racial ou de gênero.
- O jogo não julga moralmente: mostra consequências (eleitorais, judiciais, midiáticas, pessoais).
- Tragédias são tratadas com seriedade e nunca como piada.

## Checklist antes do commit
1. `dotnet run --project tools/Planalto.EventLint` sem erros.
2. `dotnet run --project tools/Planalto.DataValidator` sem erros.
3. Releia com o pronome feminino, masculino e neutro.

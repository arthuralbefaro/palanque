# Guia de conteúdo de eventos

Regras para escrever eventos em `data/events/{categoria}/*.json` (GDD 7.5 e 15). Toda mudança passa pelo `dotnet run --project tools/Planalto.EventLint`, que não pode apontar erro.

## Formato
- `id` em minúsculas com `_`, começando pela categoria (`campanha_...`); a pasta é a categoria.
- `phases`: `pre_campanha` e/ou `campanha` (novas fases entram com o mandato, Fase 4).
- `rarity`: `comum`, `incomum`, `raro` ou `lendario`; `cooldown_weeks` é o intervalo mínimo entre repetições; `unique: true` acontece uma vez por carreira.
- `chain_only: true` marca continuações: só disparam quando agendadas por `schedule` de outra escolha.
- `conditions`: `chance`, `money_lt/gt`, `recognition_lt/gt`, `image_lt/gt`, `stress_gt`, `porte`, `regiao`, `trait`, `not_trait`, `flag`, `not_flag`, `attribute_min`, `weeks_to_election_lt/gt`, `needs_rival`, `needs_skeleton`.
- `variants`: de 1 a 4 aberturas diferentes para o mesmo evento.
- `choices`: de 2 a 4, cada uma com `effects`, `result` e, se houver risco, `test` (atributo, `bonus`, textos e efeitos de sucesso e fracasso). `requires` bloqueia a escolha e o jogo mostra o motivo.

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

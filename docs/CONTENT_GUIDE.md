# Guia de conteúdo de eventos

Regras para escrever eventos em `data/events/{categoria}/*.json` (GDD 7.5, 15 e 17). Toda mudança passa pelo `dotnet run --project tools/Planalto.EventLint`, que não pode apontar erro (sai com código 0).

## Formato
- `id` em minúsculas com `_`, começando pela categoria (`campanha_...`); a pasta é a categoria (uma das 12 da seção 15.2; o EventLint recusa outras).
- `phases`: só `pre_campanha` e/ou `campanha`. São as únicas fases que o motor sorteia hoje (da criação do personagem, ou da abertura da pré-campanha no prazo de filiação, até a véspera da eleição); qualquer outro valor reprova a validação.
- `rarity`: `comum`, `incomum`, `raro` ou `lendario`; `cooldown_weeks` é o intervalo mínimo entre repetições; `unique: true` acontece uma vez por carreira.
- `chain_only: true` marca continuações: só disparam quando agendadas por `schedule` de outra escolha. A continuação volta com as mesmas pessoas e a mesma zona do evento que a agendou.
- `luto: true` marca evento de morte e luto (ver "Texto de morte").
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
  - `needs_skeleton`: há esqueleto no armário;
  - `needs_commitment` (id de compromisso, ex.: `comicio`): só com um compromisso desse tipo marcado na agenda;
  - `zone_kind` (`urbana` ou `rural`): a zona do evento tem de ser dessa situação;
  - `outlet_kind` (`emissora`, `escrita` ou `rede`): o veículo do evento tem de ser desse tipo;
  - `trigger` (tipo de fato, ou `tipo:subtipo`): evento dinâmico (GDD 15.4). Não entra no sorteio da semana: dispara quando um fato desse tipo é emitido, com a `chance` do evento, e envolve as pessoas, a zona e o tema do fato. Em uso: `lideranca_perdida`, `tema_em_alta`, `plano_rival:reduto`, `esqueleto`, `pesquisa:queda`.
- `variants`: de 1 a 4 aberturas diferentes para o mesmo evento.
- `choices`: de 2 a 4, cada uma com `label` (até 40 caracteres), `effects`, `result` e, se houver risco, `test` (atributo, `bonus`, textos e efeitos de sucesso e fracasso). `requires` (`money_min`, `attribute_min`, `flag`, `political_capital_min`) bloqueia a escolha e o jogo mostra o motivo.
- `portrait`: `rival` mostra o rival do evento; sem o campo, o retrato é do jogador. `portrait_expression` define a expressão.

## Efeitos
Além de `money`, `recognition`, `image`, `stress`, `health`, `political_capital`, `support`, `attributes`, `set_flags`, `clear_flags`, `rival_image`, `rival_recognition`, `relation` e `schedule`:
- `memories`: lembranças gravadas nas pessoas do evento. Cada uma tem `quem` (papel: `rival`, `lideranca`, `jornalista`, `doador`, `colega`, `dirigente`, `equipe`), `fato` (chave estável), `peso` (−3 a 3, nunca 0; peso 3 não se apaga), `pontos` (efeito na relação, com o mesmo sinal do peso) e `texto` (como aparece na ficha da pessoa).
- `promise`: promessa no Livro de compromissos. `para` é um papel de pessoa ou `zona`; `objeto` (obra, posicao, presenca…); `texto`; `prazo_semanas` (depois da posse; 0 = antes da eleição).
- `debt`: dívida no Livro, com `para` (papel) e `texto`.
- `add_skeleton` (`type`, `severity`) com `skeleton_known_by` (papéis que ficam sabendo). Se a escolha também traz `money` positivo e o tipo é `caixa_dois` ou `gasto_nao_declarado`, o dinheiro entra nas contas da campanha como "por fora".
- `zone_presence`: presença na zona do evento (na mesma unidade dos compromissos).
- `cancel_commitment` (id de compromisso): desmarca o compromisso desse tipo marcado na agenda.
- `fact` (`tipo`, `texto`, `visibilidade` de 0 a 3): fato que a escolha emite; com visibilidade 1 ou mais, vira notícia nos veículos.
- `energy` não existe mais: o EventLint reprova. O custo de tempo é `stress`.

## Rastro: toda escolha deixa lembrança ou entrada no Livro (GDD 17.1, princípio d)
- **Toda escolha** grava uma lembrança em alguém (`memories`) ou uma entrada no Livro de compromissos (`promise`, `debt` ou `add_skeleton`). Com `test`, o rastro tem de ficar nos dois desfechos (ou nos efeitos certos da escolha). Agendar uma continuação não conta: o que fica tem de estar no save.
- Escolha que legitimamente não envolve ninguém nem compromete nada (assunto pessoal do candidato, acaso de campanha) leva a marca `sem_rastro` com o motivo por extenso (15 caracteres ou mais). O EventLint lista todas as marcadas no relatório; use com parcimônia. `sem_rastro` numa escolha que deixa rastro, ou numa escolha `relevant`, reprova.
- O que vai para o Livro ou abre cadeia (`add_skeleton`, `promise`, `debt`, `schedule`) só pode estar em escolha marcada `relevant: true`.
- Evento que cita uma pessoa do save deve gravar a lembrança nela: prefira `memories` a `sem_rastro` sempre que houver alguém envolvido.

## Troca: ganho e custo de naturezas diferentes (GDD 17.1, princípio e)
- Em toda escolha `relevant: true`, declare `gain` e `cost`, cada um com pelo menos uma natureza entre `dinheiro`, `tempo`, `relacao`, `zona`, `risco` e `posicao`. Nenhuma natureza pode estar dos dois lados.
- O que se declara tem de bater com os efeitos: `dinheiro` (money), `tempo` (stress; `cancel_commitment` é custo), `relacao` (relation, support, pontos de `memories`), `zona` (recognition, `zone_presence`), `posicao` (image), `risco` (só custo: `add_skeleton`, `promise`, `debt`, `schedule` ou `test`).
- Nenhuma escolha é "a certa": se uma opção só ganha, falta o custo.

## Ainda não existem
- **Eventos de mandato.** Nas telas de mandato de vereador, de prefeitura e de entressafra, o motor de eventos não roda: ele depende do estado da campanha (DECISOES D-027 e D-031). As categorias `mandato_legislativo`, `mandato_executivo`, `crises` e `economia` existem no EventLint, mas eventos escritos nelas só disparariam na campanha. As crises da prefeitura são outro sistema, em `data/executive/crises.json`. Não escreva eventos de mandato até o motor ganhar essas fases.
- **Eventos de morte e luto.** As regras de texto abaixo já são cobradas pelo EventLint (`EventLint.CheckDeathText` e a marca `luto`), mas a renovação do mundo político (GDD 17.16) ainda não emite o fato de morte nem agenda o velório: não há evento de luto em `data/events/` neste momento. Os textos de `data/demography/renewal.json` passam pela mesma função quando forem ligados.
- **Variáveis de secretário, assessor e vereador.** Só existem os papéis da lista de variáveis abaixo.
- **Condições sobre o save das pessoas** (relação mínima com a liderança, linha do veículo): o evento envolve quem o motor sorteia entre os que existem; ainda não dá para exigir uma relação ou uma linha editorial.

## Variáveis
Todas são resolvidas pelo motor ao disparar o evento; o EventLint reprova variável que não esteja nesta lista.

**Gerais:** `{municipio.nome}`, `{municipio.uf}`, `{municipio.porte}`, `{partido.sigla}`, `{partido.nome}`, `{jogador.nome}`, `{jogador.pronome}` (ele/ela/elu), `{jogador.o}` (o/a/e, para concordância: `eleit{jogador.o}`), `{ano}`, `{valor}` (quantia em reais proporcional ao porte), `{semanas}` (até a eleição).

**Pessoas do save** (usar a variável faz o evento envolver a pessoa; se o save não tiver ninguém no papel, o evento não entra no sorteio):
- `{rival.nome}`, `{rival.partido}`: rival nomeado da disputa do jogador (o do fato de origem ou da cadeia; senão um dos mais conhecidos);
- `{lideranca.nome}`, `{lideranca.papel}` ("presidente de associação de moradores"): liderança local (a do fato ou da cadeia; senão uma que o jogador conhece);
- `{colega.nome}`: colega de chapa (mesmo partido, mesmo cargo);
- `{dirigente.nome}`: dirigente local do partido do jogador (o que decide a cota do fundo eleitoral);
- `{doador.nome}`: doador pessoa física da cidade;
- `{equipe.nome}`: alguém contratado na equipe da campanha;
- `{jornalista.nome}`: jornalista principal do veículo do evento.

**Zona e tema:**
- `{zona.nome}`: a zona do evento (a do fato, a da liderança ou sorteada pela população; `zone_kind` restringe a urbana ou rural). É nela que valem `zone_presence` e a promessa `para: "zona"`;
- `{tema.nome}`: tema do fato de origem (eventos dinâmicos) ou sorteado.

**Veículo de mídia (R3):**
- `{veiculo.nome}`: o nome do veículo **já com o que ele é**, por exemplo "Rádio Vale (rádio local, linha governista)". Não acrescente tipo nem linha no texto. `outlet_kind` escolhe o tipo (`emissora` para rádio e TV, `escrita` para jornal, `rede` para página de notícias);
- `{jornalista.nome}`: o jornalista principal desse veículo.
- Nunca escreva nome de veículo no texto: os veículos são gerados por cidade, e nomes reais são barrados por `data/media/blocklist.json`.

## Tom e tamanho
- Texto curto: até três frases na abertura e uma no resultado. Rótulo de escolha com até 40 caracteres.
- Segunda pessoa ("você"), PT-BR do dia a dia, sem gírias datadas.
- Toda escolha tem custo e benefício plausíveis; nenhuma é "a certa".
- Não fale de ações que não existem no jogo. Os compromissos são os de `data/campaign/commitments.json`; propaganda paga em rádio, TV, outdoor e brinde é vedada por lei e não existe como escolha legítima.

## Texto de morte (GDD 17.16, "morte tratada com seriedade")
Vale para todo texto de morte em `data/`: abertura, resultado e rótulo de escolha de evento, manchete e texto de luto. O EventLint reprova o que fugir (`EventLint.CheckDeathText` para um texto; a marca `luto` para o evento inteiro).
- **Texto sóbrio.** Sem exclamação e sem palavra em caixa alta.
- **Sem humor.** Nada de piada, meme, brincadeira ou riso no texto; evento de morte não fica nas categorias `raros` nem `sociedade`. As permitidas são `vida_pessoal`, `bastidores`, `campanha`, `mandato_legislativo` e `mandato_executivo`.
- **Sem causa sensacionalista.** Diga que a pessoa morreu, a idade e o que ela era; não descreva a causa com adjetivo de impacto ("brutal", "chocante", "terrível") nem use "urgente" ou "bomba".
- **Nenhuma escolha zomba do morto.** Não se oferece ironizar, debochar, comemorar ou tirar proveito; atacar a memória do morto não é opção. O jogador escolhe como se manifesta: comparecer ao velório é um compromisso.
- **Notícia em tom neutro.** O fato de morte (`morte`) sai em todos os veículos em tom neutro, qualquer que seja a linha editorial (o motor garante).
- **Luto oficial.** Três dias sem sessão na Câmara; a campanha do partido para um dia.
- **A lembrança fica.** Família e aliados guardam a lembrança do que o jogador fez: toda escolha de um evento de luto grava `memories`.
- Todo evento cujo texto fale de morte, velório, enterro ou luto tem de trazer `luto: true`; sem a marca, o EventLint reprova. Cuidado com figuras de linguagem ("a pauta morreu"): escreva de outro jeito.

## Neutralidade (CLAUDE.md)
- Nenhum evento cita pessoas, partidos, empresas, veículos ou marcas reais.
- Nenhum evento se baseia em estereótipo regional, religioso, racial ou de gênero.
- O jogo não julga moralmente: mostra consequências (eleitorais, judiciais, midiáticas, pessoais). Dinheiro "por fora" tem consequência, não sermão.
- Tragédias são tratadas com seriedade e nunca como piada.

## Checklist antes do commit
1. `dotnet run --project tools/Planalto.EventLint` sem erros (código de saída 0). Confira a lista "Escolhas marcadas como sem rastro": cada motivo faz sentido?
2. `dotnet run --project tools/Planalto.DataValidator` sem erros.
3. Toda escolha grava lembrança ou entrada no Livro, ou tem `sem_rastro` com motivo.
4. Toda escolha `relevant` tem `gain` e `cost` de naturezas diferentes, sustentados pelos efeitos.
5. Toda pessoa, zona e veículo citados vêm de variável (nenhum nome escrito à mão); `{veiculo.nome}` sem tipo nem linha repetidos no texto.
6. Nenhuma menção a `energy` nem a ação que não existe; rótulos com até 40 caracteres.
7. Se o texto fala de morte: `luto: true`, categoria permitida e as regras de "Texto de morte".
8. Releia com o pronome feminino, masculino e neutro.

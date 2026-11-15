# Planalto — instruções para o Claude Code

## Contexto
Simulador de carreira política brasileira em Godot 4.7.2 .NET (C#).
Antes de qualquer tarefa, leia docs/GDD.md e docs/ROADMAP.md.
Dono do projeto e único autor: Arthur Albefaro.

## AUTORIA — REGRA ABSOLUTA
- Você NÃO é autor, coautor ou colaborador de nada neste repositório.
- Commits: nunca adicione "Co-Authored-By", "Generated with Claude Code", "Claude-Session", links para claude.ai/claude.com/anthropic.com, emojis de robô ou qualquer menção a IA, Claude ou Anthropic na mensagem.
- Nunca altere git config user.name / user.email nem use --author. O autor é sempre o configurado no repositório (Arthur Albefaro).
- Nunca desative, contorne ou edite os hooks em .githooks/ nem use --no-verify.
- Código, comentários, documentação, README, CREDITS, telas de créditos do jogo, metadados de export, nomes de arquivos: nenhuma menção a IA, Claude, Anthropic ou "gerado automaticamente".
- Pull requests e issues: mesmas regras, sem rodapé de atribuição.
- Créditos do jogo listam apenas Arthur Albefaro e os autores de assets de terceiros com licença.

## Mensagens de commit
- Em português, no imperativo, formato: `tipo(escopo): descrição`
  - tipos: feat, fix, refactor, test, data, art, audio, docs, chore
  - ex.: `feat(eleicoes): implementa distribuição de sobras por maiores médias`
- Um commit por funcionalidade concluída e verificada.
- Nunca faça push sem eu pedir.

## Regras de código
- C# com nullable habilitado e TreatWarningsAsErrors.
- Lógica de jogo SOMENTE em core/Planalto.Core, sem nenhuma referência ao Godot.
- UI em src/UI apenas lê o estado e chama Game.Execute(...).
- Toda aleatoriedade passa pela classe Rng do Core, com semente.
- Conteúdo em data/*.json; nunca hardcode textos, peças de aparência, municípios ou números de balanceamento.
- Classes que herdam de Node são `partial`.
- Textos ao jogador em PT-BR via Tr(), respeitando os pronomes do personagem.
- Nomes: PascalCase para tipos e métodos, camelCase para locais, _camelCase para campos privados.
- Comentários em português, só quando o "porquê" não for óbvio.

## Dados dos municípios
- O jogo inclui TODOS os municípios do Brasil. Nunca reduza a lista, nunca use amostra ou lista inventada.
- Dados vêm só de fontes oficiais (IBGE, TSE) via tools/Planalto.DataImporter. Nunca escreva números de população ou eleitorado à mão.
- Os arquivos gerados em data/municipios/ são commitados; os brutos baixados ficam em tools/Planalto.DataImporter/cache/ (no .gitignore).
- Toda mudança no importador deve manter o teste que confere a contagem total de municípios e a soma de população por UF contra o total oficial.
- Simulação por nível de detalhe (ver GDD seção 12): nunca simule todos os municípios em detalhe ao mesmo tempo.

## Verificação obrigatória (antes de dar qualquer tarefa como concluída)
- dotnet build Planalto.sln
- dotnet test core/Planalto.Core.Tests
- dotnet run --project tools/Planalto.DataValidator
- godot --headless --build-solutions --quit
Nenhuma tarefa está concluída com build quebrado, testes falhando, dados inválidos ou erros no console.

## Fluxo de trabalho
- Um item do ROADMAP por vez; marque o checkbox ao concluir.
- Tarefas grandes: mostre o plano antes de executar.
- Teste junto com toda regra de negócio nova.
- Se uma instrução do GDD estiver ambígua ou contraditória, pergunte em vez de inventar.
- Ao final, resuma em poucas linhas o que mudou e o que eu devo testar no jogo.

## Neutralidade e representação
- Partidos, políticos, empresas, institutos e veículos de mídia são fictícios. Nunca use nomes reais de políticos, partidos ou marcas.
- Municípios, estados e dados demográficos são reais; prefeitos, vereadores e personalidades locais são fictícios.
- Nenhuma ideologia é favorecida; toda posição tem custos e benefícios.
- Aparência do personagem nunca altera votos ou atributos.
- Nenhum evento baseado em estereótipo regional, religioso, racial ou de gênero.
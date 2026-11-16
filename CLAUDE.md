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
- Pacotes NuGet: só os aprovados — xUnit e dependências de teste; Godot.NET.Sdk/GodotSharp no projeto Godot; NetTopologySuite e NetTopologySuite.IO.Esri.Shapefile SOMENTE em tools/Planalto.DataImporter. Nenhum pacote novo no Core ou no projeto Godot sem eu aprovar.
- Parâmetros que vêm de lei (percentuais, prazos, limites) ficam em objetos de configuração com padrão igual à lei vigente (ex.: ProportionalRulesConfig). Em dúvida sobre regra eleitoral, pergunte.

## Dados dos municípios
- O jogo inclui TODOS os municípios do Brasil. Nunca reduza a lista, nunca use amostra ou lista inventada.
- Dados vêm só de fontes oficiais (IBGE e TSE; ver GDD 12.2) via tools/Planalto.DataImporter, cruzados pelo código IBGE de 7 dígitos. Nunca escreva números de população, eleitorado, vagas ou coordenadas à mão.
- Se uma fonte bloquear o download automatizado, não contorne: pare e me informe a URL e o nome do arquivo para eu colocar no cache.
- Lacunas (ex.: município criado depois do Censo): use só alternativa aprovada e registre em data/municipios/_fontes.json. Nunca invente número.
- Os arquivos gerados em data/municipios/ são commitados; os brutos baixados ficam em tools/Planalto.DataImporter/cache/ (no .gitignore).
- Perfil do Censo é gravado como publicado; percentuais sempre normalizados pela soma das categorias do bloco, nunca pelo total publicado.
- Vagas de vereador são as reais do TSE; o teto do art. 29, IV é só limite (regra de jogo no GDD 12.4).
- Agregação regional por região geográfica imediata (microrregiao pode ser null). Municípios com eleicoes_municipais = false (DF e Fernando de Noronha) não têm eleição municipal.
- Toda mudança no importador deve manter os testes: contagem igual à lista do IBGE, códigos únicos, população por UF igual ao total oficial (sem tolerância), eleitorado > 0 onde há eleição municipal, soma de vagas igual ao total do TSE e todo ponto dentro do polígono do próprio município.
- Simulação por nível de detalhe (ver GDD seção 12): nunca simule todos os municípios em detalhe ao mesmo tempo.

## Verificação obrigatória (antes de dar qualquer tarefa como concluída)
- dotnet build Planalto.sln
- dotnet test core/Planalto.Core.Tests
- dotnet run --project tools/Planalto.DataValidator
- godot --headless --build-solutions --quit
- Se o DataImporter mudou: dotnet test tools/Planalto.DataImporter.Tests (precisa do cache preenchido)
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
- Nenhuma ideologia é favorecida; toda posição tem custos e benefícios. O espectro democrático completo é coberto por famílias ideológicas fictícias (GDD 14), todas com caminho viável até a Presidência.
- Nomes e siglas de partidos, gerados ou digitados pelo jogador, são validados contra data/parties/blocklist.json para nunca coincidir com partidos reais: sigla igual ou com as mesmas letras em outra ordem, ou nome que contenha o nome inteiro de um partido real.
- Ícones de logotipo são genéricos (estrela, punho, rosa, pomba, foice e martelo etc. são permitidos), mas nenhum logotipo gerado pode reproduzir o desenho de um logotipo real: formato do símbolo, disposição da sigla e paleta copiados.
- Cenários espelhados do real (GDD 16.4): a correspondência partido real → fictício existe só no pipeline de dados em tools/ e nunca vai para data/ nem para o jogo; os arquivos finais têm só IDs fictícios e números, e um teste garante que nenhum nome ou sigla real aparece neles.
- O jogo nunca julga moralmente: só simula consequências plausíveis (eleitorais, judiciais, midiáticas, pessoais).
- Aparência do personagem nunca altera votos ou atributos.
- Nenhum evento cita pessoas, partidos ou empresas reais nem se baseia em estereótipo regional, religioso, racial ou de gênero; tragédias são tratadas com seriedade.
- Urna, edifícios e símbolos oficiais só em versões estilizadas: sem réplicas de edifícios reais, brasões ou marcas oficiais; sons originais.

## Autonomia
- Trabalhe em sequência pelas fases do ROADMAP sem pedir aprovação de plano entre etapas.
- Diante de ambiguidade ou decisão de design, escolha a opção mais coerente com o GDD, registre em docs/DECISOES.md (data, fase, pergunta, decisão, alternativas, como reverter) e siga em frente.
- Só pare e pergunte quando:
  1. um download ou site for bloqueado e não houver fonte oficial alternativa;
  2. uma decisão envolver classificar ou retratar entidades reais (partidos, pessoas, instituições) sem fonte publicada que a sustente;
  3. a ação for destrutiva ou irreversível (apagar dados, reescrever histórico do Git, push);
  4. a verificação obrigatória continuar falhando depois de 3 tentativas de correção.
- Ao terminar cada etapa: verificação obrigatória, commits, ROADMAP marcado, resumo de 3 linhas em docs/PROGRESSO.md, e siga para a próxima sem esperar.
- Se o contexto for compactado, releia CLAUDE.md, docs/ROADMAP.md, docs/DECISOES.md e docs/PROGRESSO.md antes de continuar.
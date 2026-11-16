# Planalto — instruções para o Claude Code

## Contexto
Simulador de carreira política brasileira em Godot 4.7.2 .NET (C#).
Antes de qualquer tarefa, leia docs/GDD.md e docs/ROADMAP.md.
Dono do projeto: Arthur Albefaro. Colaboradores convidados por ele podem contribuir em nome próprio (ver Autoria).

## AUTORIA — REGRA ABSOLUTA
- Você NÃO é autor, coautor ou colaborador de nada neste repositório.
- Commits: nunca adicione "Co-Authored-By", "Generated with Claude Code", "Claude-Session", links para claude.ai/claude.com/anthropic.com, emojis de robô ou qualquer menção a IA, Claude ou Anthropic na mensagem.
- Nunca altere git config user.name / user.email nem use --author. Os commits do autor e os feitos nesta máquina pelo assistente saem sempre em nome de Arthur Albefaro, sem menção a IA.
- Colaboradores convidados pelo autor assinam os próprios commits com o próprio nome. As regras desta seção sobre IA valem para eles também, e o hook `.githooks/commit-msg` apaga linhas de atribuição a IA de qualquer commit. Quem clona o repositório ativa o hook com `git config core.hooksPath .githooks`.
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
  - A cada commit, só os testes rápidos: `dotnet test core/Planalto.Core.Tests --filter "Categoria!=Lento"` (menos de 1 minuto).
  - Antes de cada tag, a suíte inteira (`dotnet test core/Planalto.Core.Tests`) e o BalanceSim (`dotnet run --project tools/Planalto.BalanceSim`).
  - Teste novo que passe de 2 segundos (carreira ou campanha inteira, muitos anos, muitas sementes, balanceamento) nasce com `[Trait("Categoria", "Lento")]`.
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

## Fase R (reformulação)
- **Autorização do autor (2026-11-16), válida só para a execução noturna iniciada nessa data:** modo corujão em toda a Fase R. Nesta execução, as paradas "pare e espere o autor" viram **revisão posterior por tag**: cada parte termina com a tag local (fase-r1, fase-rd, fase-r2a, fase-r2b, fase-r3, fase-r4) e o trabalho segue para a próxima; o autor joga as partes de manhã, na ordem das tags. Trabalhe sem perguntar; bloqueios e decisões novas vão para docs/PENDENCIAS.md (com a escolha provisória e como reverter) e o trabalho segue no que não depende deles. Ordem nesta execução: R1 → RD → R2a → R2b → R3 → R4. Nunca push, reset --hard, rebase ou --no-verify. Fora desta execução, valem as regras abaixo.
- **Extensão da autorização (D-037, 2026-11-16):** o autor não testa por enquanto e autoriza seguir a Fase R inteira sem esperar pelo teste dele: RD → R2a → R2b → R3 → R4, cada uma com a sua tag, e depois o resto das pendências da R1 (P-02, P-03, P-09) com a tag `fase-r-final`. Nenhuma parte espera aprovação para começar a seguinte. Parar ao terminar a `fase-r-final`; não começar a Fase 6. O relatório único para o autor fica no topo do docs/PROGRESSO.md, atualizado a cada parte.
- **Precedência nesta execução:** a autorização do corujão prevalece sobre toda instrução de "pare", "pergunte" ou "espere o autor" do CLAUDE.md e do ROADMAP, inclusive dentro de cada parte. Nos casos em que antes o trabalho pararia:
  - **decisão nova ou dúvida de lei:** escolher a leitura mais conservadora e coerente com o GDD, citar o artigo ou a fonte, registrar em docs/PENDENCIAS.md como "a confirmar pelo autor" com como reverter, e seguir;
  - **download bloqueado ou manual:** registrar em PENDENCIAS.md (URL, arquivo, onde pôr no cache), usar a substituição prevista marcada como lacuna, e seguir;
  - **termos de uso das fontes da RD:** o autor confere depois; nesta execução a RD roda citando as fontes;
  - **sem janela para os testes de fumaça:** registrar em PENDENCIAS.md, marcar a parte "com pendências", seguir só com o que não depende de tela (Core, dados, testes sem janela) e tentar os testes com janela de novo ao fim de cada parte.
  Continuam valendo sempre: nada de push, reset --hard, rebase, --no-verify ou apagar dados; autoria e neutralidade.
- **Portão de cada parte nesta execução:** (1) itens do ROADMAP feitos e marcados; (2) verificação obrigatória, testes da parte e testes de fumaça com janela; (3) nas partes com tela, prints em 1366×768 e 1920×1080 examinados contra o ART_GUIDE, a referência e os princípios f, i e j, com até duas rodadas de correção (o que sobrar vai para PENDENCIAS.md com o print); (4) autocrítica de imersão: jogar com o robô do BalanceSim ou um teste de carreira, reler a prova de imersão do GDD 17.21 e anotar no PROGRESSO onde ainda fica aquém; (5) roteiro de teste atualizado com os nomes reais; (6) resumo no PROGRESSO, docs/CONTEXTO.md atualizado e tag. Parte que não passa num portão fica "com pendências"; a seguinte só começa se não depender do que falhou.
- **docs/CONTEXTO.md:** documento para o autor colar num chat novo, sem acesso ao repositório. Máximo de 400 linhas, resume em vez de apontar, primeira linha "Atualizado em AAAA-MM-DD HH:MM, depois de: ...". Atualizar ao fim de cada parte, a cada decisão ou pendência nova ou resolvida e sempre antes de parar; commit `docs(contexto)`.
- Fora da autorização acima, a seção Autonomia não vale para a Fase R do ROADMAP (R1, R2a, R2b, R3, RD, R4). O desenho está no GDD, seção 17; o diagnóstico, em docs/AUDITORIA_FASE3.md.
- Uma parte por vez. Ao terminar uma parte: verificação obrigatória, testes automáticos listados na parte, **testes de fumaça com janela** (abaixo), roteiro de teste da parte atualizado (docs/ROTEIRO_TESTE_R1.md, R2A, R2B, R3, RD, R4), commits, resumo em docs/PROGRESSO.md. Depois **pare e espere o autor jogar e aprovar**. Não comece a parte seguinte sem aprovação explícita.
- **Nenhuma parte está pronta sem rodar os testes de fumaça que exigem janela.** Sem janela o Godot não entrega cliques de mouse à interface, então a verificação obrigatória (toda sem janela) não os cobre. Comando, na raiz, depois do build: `godot --path . res://src/UI/Debug/UiShot.tscn -- --target=<alvo>`, sem `--headless`; código de saída 0 = passou. Alvos que exigem janela: `eventos`, `layout` e `explicacao`, e os da R2a: `agenda` (marca compromissos clicando nos turnos, confere a previsão com o cansaço no terceiro igual, bloqueio com motivo, desmarcar e os cartões de resultado no feed), `zona` (clica numa zona no mapa, abre a ficha, usa "Marcar compromisso aqui", confere o marcador e o feed que centraliza o mapa) e `lideranca` (marca visita pela ficha da pessoa, responde ao pedido aceitando, confere a lembrança e a promessa no Livro pela Enciclopédia), e os da R2b: `equipe` (contrata um coordenador clicando com a previsão antes, delega um compromisso, confere a pausa de caixa abaixo de duas semanas de folha, ignora o conselho da semana, responde a uma decisão da equipe forçada por `TriggerStaffEvent`, repete a semana anterior, confere o aviso de estresse e demite com confirmação), `verba` (confere o bloqueio antes de 16 de agosto com o motivo, reparte verba em dois canais vendo as duas contas, confere o resultado explicado, o comício sem verba de Rua, o modo Verba do mapa e o limite legal de cabos), `plataforma` (muda uma posição vendo a previsão por grupo e por zona, confere a posição assumida no Livro, define bandeiras, faz uma promessa a uma zona e escolhe o tema de uma entrevista) e `objetivos` (confere de 1 a 3 objetivos na coluna direita, fixa um, dispensa outro e cumpre um, que vira cartão no feed), e os da R3: `rivais` (abre a ficha do rival pela área Pessoas, pelo marcador do mapa e pelo feed, ataca com a previsão antes e confere o bloqueio do segundo ataque, investiga, propõe pacto e vê o aviso de quebra na marcação, responde pela faixa a um ataque com prazo sem travar o tempo, deixa outro prazo vencer e abre o painel da chapa), `midia` (confere os veículos com linha e alcance, o horário gratuito com o artigo, o anúncio bloqueado em emissora com o art. 44, marca entrevista com o veículo escolhido e o tema em alta na previsão, o modo Disputa do mapa, as notícias com o fato de origem e o boato marcado, sugere pauta, anuncia na imprensa escrita até bater no limite do art. 43 e abre um cartão de evento com pessoas e veículo clicáveis) e `contas` (confere a barra do limite e o risco em faixa, responde a uma oferta de doação com recibo e a outra "por fora" vendo a previsão, confere o esqueleto com quem sabe e a chance de vir à tona, as fontes e os prazos e, depois da eleição, o julgamento das contas na tela do mandato). O `layout` e o `explicacao` também percorrem as telas da R2b e da R3. Rode também, com janela, `newworld`, `creator`, `creator17`, `picker`, `hub`, `campaign`, `map`, `mandate` e `prefeitura`. Arquivo novo em assets/ (fonte, ícone) só carrega depois de `godot --headless --import --path .`. A P-24 acrescentou `mapa_pequeno` (Mantena, duas cidades pequenas com distritos e uma metrópole em 1366×768 e 1920×1080: confere que nenhum nome de zona cobre outro nome ou marcador e que todos ficam dentro do mapa, que o enquadramento inicial mostra o núcleo urbano, clica em "Cidade inteira" e volta, e confere que o save da divisão de zonas anterior é recusado com a mensagem). Cada parte da Fase R acrescenta os próprios alvos e os registra aqui e no cabeçalho da Fase R do ROADMAP. Se não for possível abrir janela, não dê a parte como pronta: pare e informe.
- **Dados de usuário nos testes de fumaça:** toda rodada passa `--perfil=<nome do próprio branch>` (com `/` trocado por `_`); os saves, as opções, as dicas vistas, os presets e os cenários da rodada ficam em `user://smoke/<perfil>/`, apagado no início da rodada (sem `--perfil`, vale o nome do branch git). O jogo normal continua em `user://`. Todo acesso a esses arquivos passa por `src/Autoload/UserPaths.cs`; não grave em `user://` por outro caminho. O alvo `perfil` confere o isolamento: duas rodadas em paralelo com perfis diferentes saem as duas com código 0.
- **Processos do Godot:** nenhum agente encerra processos do Godot que não iniciou. Para interromper uma rodada, encerre só pelo PID do próprio processo; nunca por nome (`taskkill /IM`, `pkill godot`, `Stop-Process -Name`).
- Antes de começar uma parte, confira no GDD 17.20 as decisões do autor que ela usa (registradas na D-035). Se surgir uma decisão nova, não escolha por conta própria: pergunte.
- Todo sistema novo cumpre os princípios do GDD 17.1 (a–j) e tem os testes que os verificam. Sistema que não passa no critério não está pronto, mesmo com build e testes verdes.
- Interface: siga docs/ART_GUIDE.md, seção Interface. Todo controle tem tooltip em três partes; todo termo do glossário tem link para o verbete; controle bloqueado diz por quê e o que falta.
- Regras de lei novas (limites de gasto e de doação, condutas vedadas, limites fiscais, substituição de mandatários) ficam em objetos de configuração com o padrão da lei vigente e a citação do artigo. Em dúvida, pergunte.
- Dados novos (zonas, economia, finanças, indicadores sociais, mortalidade) seguem as regras de "Dados dos municípios": só fonte oficial, via tools/Planalto.DataImporter, lacunas registradas em _fontes.json, nenhum número escrito à mão.
- O pacote de nomes (GDD 17.18) não foi aprovado pelo autor e está suspenso: não implemente nada dele. As vagas individuais fictícias seguem no plano. O repositório e data/ nunca contêm nomes reais.
- Propaganda: nada de gasto com rádio, TV, outdoor ou brinde (Lei 9.504, arts. 44 e 39); rádio e TV são presença conquistada por entrevista e horário gratuito. Artigos citados de memória no GDD 17.20 são conferidos no texto oficial antes de virar configuração.
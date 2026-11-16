# CONTEXTO DO PROJETO PLANALTO — para retomar em nova conversa

## Quem e o quê
- Autor e único dono: **Arthur Albefaro**. Conversa em português.
- Projeto: **Planalto**, simulador de carreira política brasileira. O jogador cria um personagem, começa como cidadão, disputa vereador e sobe até presidente. Simulador livre: qualquer ideologia, ética ou estratégia, o jogo nunca julga moralmente, só simula consequências.
- Desenvolvimento feito pelo **Claude Code** rodando na máquina do Arthur (Windows), com esta conversa servindo de "consultor": eu reviso relatórios do Claude Code, respondo às perguntas dele e escrevo os prompts.

## Stack e ambiente
- **Godot 4.7.2 .NET (C#)**, 2D com elementos 2.5D/3D pontuais. Só PC (Windows, Linux, macOS), sem Web.
- .NET SDK 10.0.201 instalado (projetos em net8.0, runtime 8.0.28 presente).
- Godot em `C:\Godot`, com atalhos `godot`/`godot.cmd` em `C:\Users\arthu\bin` no PATH.
- Solução: `Planalto.sln` com projeto Godot (`Planalto.csproj`), `core/Planalto.Core` (.NET puro, sem Godot), `core/Planalto.Core.Tests` (xUnit), `tools/Planalto.DataValidator`, `tools/Planalto.DataImporter` (+ `.Tests`), previstos `tools/Planalto.BalanceSim` e `tools/Planalto.EventLint`.
- Pacotes aprovados: Godot.NET.Sdk/GodotSharp, xUnit, NetTopologySuite (só no DataImporter).
- Skills instaladas no Claude Code (via `npx skills add ... --agent claude-code`): dotnet/skills (run-tests, code-testing-agent, analyzing-dotnet-performance, crap-score) e gamedev-skills/awesome-gamedev-agent-skills (godot-ui-control, godot-animation, game-ui-ux, card-game, visual-novel).

## Regras absolutas
- **Autoria:** commits só em nome de Arthur Albefaro. Nenhuma menção a IA, Claude ou Anthropic em commits, código, comentários, docs ou créditos. Três camadas de proteção: `git config` local, `.claude/settings.json` com `"attribution": {"commit": "", "pr": ""}`, e hook `.githooks/commit-msg` (core.hooksPath) que apaga linhas de atribuição. Nunca `--no-verify`, nunca push sem pedido.
- **Neutralidade:** partidos, políticos, empresas, institutos e mídia sempre fictícios. Municípios, estados e dados demográficos reais. Lista de bloqueio de siglas e nomes reais (atuais e extintos desde 1945, incluindo nomes "contidos", ex.: "Partido Liberal Brasileiro" bloqueado). Ícones genéricos liberados (estrela, tucano, foice e martelo etc.), mas nenhum logotipo pode copiar o desenho de um real. Aparência nunca altera votos ou atributos. Nenhum evento com estereótipo; tragédias com seriedade.
- **Commits:** em português, `tipo(escopo): descrição` (feat, fix, refactor, test, data, art, audio, docs, chore).

## Arquivos de controle no repositório
- `CLAUDE.md` (regras, verificação obrigatória, autonomia), `docs/GDD.md` (design, seções 1–16), `docs/ROADMAP.md` (fonte única das fases), `docs/DECISOES.md` (D-001 a D-020+), `docs/PROGRESSO.md`, `docs/PENDENCIAS.md` (modo corujão), `docs/ROTEIRO_TESTE_FASE3.md` (a ser criado).
- Verificação obrigatória: `dotnet build Planalto.sln`, `dotnet test core/Planalto.Core.Tests`, `dotnet run --project tools/Planalto.DataValidator`, `godot --headless --build-solutions --quit` (+ testes do importador quando ele mudar).

## Design (resumo do GDD)
- **Cargos:** vereador → prefeito/dep. estadual → dep. federal → senador/governador → presidente. Calendário real: turno = 1 semana; eleições alternadas a cada 2 anos; 1º turno no 1º domingo de outubro, 2º turno no último domingo de outubro; campanha a partir de 16/08; filiação 6 meses antes. Jogo começa em 2028 (semana ISO 1).
- **Regras eleitorais fiéis à lei:** quociente eleitoral/partidário, cláusula de 10%, sobras com 80/20, 3ª etapa aberta (decisão do STF), sobras obtidas e não preenchidas contam no divisor, desempate pelo mais idoso; parâmetros em `ProportionalRulesConfig`.
- **Personagem:** 4 abas (Aparência detalhada, Vestuário por contexto, Identidade, Campanha com santinho/jingle/logotipo) + aba Partido. Retratos 2D em camadas com shader de tingimento, expressões, envelhecimento e "Espelho". Atributos: Carisma, Oratória, Articulação, Gestão, Integridade. Traços de personalidade.
- **Municípios:** todos os 5.571, dados oficiais (IBGE, TSE, Censo 2022, Malha 2025), vagas reais de 2024 + teto constitucional, porte, região imediata, simulação por nível de detalhe.
- **Partidos:** 16 famílias ideológicas em dois eixos; gerador fictício; tela "Novo Mundo" com predefinições (Brasil realista, Multipartidário moderado — padrão 12–16, Poucos, Bipartidário, Personalizado, **Brasil 2026**); edição/sorteio/trava por campo; jogador pode criar o próprio partido (no início ou fundando na carreira).
- **Cenário Brasil 2026:** espelho da estrutura real pós-eleição 2026 (30 partidos, 5 federações, bancadas, governadores, Assembleias, prefeitos e Câmaras de 2024 de todos os municípios, força local), com nomes fictícios. Ideologia pela pesquisa de Bolognesi, Ribeiro, Codato e Silva (2025, *Opinião Pública*), regra diagonal + variação sorteada no eixo de costumes. Correspondência real→fictício só no pipeline em `tools/`.
- **Eleitorado:** 240 segmentos por município, modelo de utilidade + softmax, pesquisas com institutos fictícios.
- **Eventos:** meta 600+ em 12 categorias, com condições, raridade, cooldown, cadeias, variáveis e eventos dinâmicos.
- **Urna:** minigame no dia da eleição, voto do jogador conta; números de candidato pela regra real (2/3/4/5 dígitos).
- **2.5D/3D:** mapa eleitoral 2.5D (polígonos extrudados, modo apuração) e cerimônias de posse em 3D low-poly (bonecos parametrizados a partir da aparência; clímax corta para o retrato 2D).

## Status em 15/11/2026, 20h40
- Concluído: Fase 0, Fase 1 inteira (1A lógica, 1B municípios, 1C mundo e partidos, 1D cenário Brasil 2026, 1E eleitorado e pesquisas). 322+ testes.
- Em andamento: Fase 2 (criação de personagem, Novo Mundo, seleção de município), já com decisões D-016 a D-020.
- **Modo corujão ativo:** Claude Code rodando a noite toda sem perguntar, registrando bloqueios em PENDENCIAS.md, escrevendo o roteiro de teste da Fase 3 e um teste automatizado de campanha completa, e seguindo para as Fases 4, 5... Permissões liberadas em `.claude/settings.json` (deny para push, reset --hard, rebase, rm -rf); sessão iniciada com `claude --permission-mode acceptEdits`.
- Ajustes pedidos e possivelmente pendentes: prenomes de NPCs por década de nascimento (IBGE, meta 150+ por sexo) e item na Fase 9 para converter telas em `.tscn` editáveis.

## Próximos passos ao acordar
1. Ler `docs/PROGRESSO.md` e `docs/PENDENCIAS.md`.
2. Se o Claude Code parou por limite de uso: "continue de onde parou, seguindo o modo corujão".
3. Jogar a campanha de vereador seguindo `docs/ROTEIRO_TESTE_FASE3.md` e trazer as impressões (diversão, clareza, ritmo, bugs) para transformar em ajustes.
4. Revisar `docs/DECISOES.md` e reverter o que não agradar.

## Formato dos prompts que usamos
Seções em tags: `<contexto>` (ler CLAUDE.md, GDD, ROADMAP), `<tarefa>`, `<restricoes>`, `<verificacao>` (comandos obrigatórios + testes específicos), `<entrega>` (commits, ROADMAP, resumo curto). Com o modo de autonomia, sem pedir aprovação de plano.
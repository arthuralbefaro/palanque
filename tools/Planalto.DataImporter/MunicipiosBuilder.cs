using System.Globalization;
using Planalto.Core.Elections;
using Planalto.Core.Municipios;

namespace Planalto.DataImporter;

public sealed record BuildResult(List<MunicipioUfFile> Files, FontesMunicipios Fontes);

/// <summary>Cruza as fontes pelo código IBGE e aplica as regras do Core (vagas, 2º turno, porte).</summary>
public static class MunicipiosBuilder
{
    // Sem prefeito nem vereadores: o Distrito Federal (CF, art. 32) e Fernando de Noronha,
    // distrito estadual de Pernambuco. São identificados por código, não por dado estatístico.
    private const string DistritoFederalUf = "DF";
    private const int FernandoDeNoronha = 2605459;

    public static BuildResult Build(RawSources raw, PorteCatalog portes)
    {
        var lacunas = new List<Lacuna>();
        var municipios = new List<Municipio>();
        var errors = new List<string>();

        foreach (IbgeMunicipio ibge in raw.Municipios.OrderBy(m => m.Id))
        {
            try
            {
                municipios.Add(BuildMunicipio(ibge, raw, portes, lacunas));
            }
            catch (ImportException ex)
            {
                errors.Add($"{ibge.Id} {ibge.Nome} ({ibge.UfSigla}): {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            throw new ImportException("Municípios com dados faltando:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        var files = municipios
            .GroupBy(m => m.Uf)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new MunicipioUfFile { Uf = g.Key, Municipios = [.. g] })
            .ToList();

        var populacaoPorUf = raw.Municipios
            .Select(m => (m.UfId, m.UfSigla))
            .Distinct()
            .OrderBy(u => u.UfSigla, StringComparer.Ordinal)
            .ToDictionary(
                u => u.UfSigla,
                u => raw.Estimativa.TryGetValue(u.UfId.ToString(CultureInfo.InvariantCulture), out long p)
                    ? p
                    : throw new ImportException($"Estimativa sem o total da UF {u.UfSigla}."));

        var excecoes = municipios
            .Where(m => m.EleicoesMunicipais && m.VagasVereador > m.VagasVereadorMax)
            .Select(m => new ExcecaoVagas
            {
                CodigoIbge = m.CodigoIbge,
                Nome = m.Nome,
                Uf = m.Uf,
                VagasVereador = m.VagasVereador,
                VagasVereadorMax = m.VagasVereadorMax,
            })
            .ToList();

        var fontes = new FontesMunicipios
        {
            // Data do download mais recente: com o mesmo cache, a saída é idêntica.
            GeradoEm = raw.Files.Values.Max(f => f.DownloadedAt),
            Campos = BuildCampos(raw),
            TotaisOficiais = new TotaisOficiais
            {
                TotalMunicipios = raw.Municipios.Count,
                PopulacaoPorUf = populacaoPorUf,
                VagasVereadorTse2024 = raw.VagasVereador2024Total,
            },
            Lacunas = lacunas,
            ExcecoesVagas = excecoes,
        };

        return new BuildResult(files, fontes);
    }

    private static Municipio BuildMunicipio(IbgeMunicipio ibge, RawSources raw, PorteCatalog portes, List<Lacuna> lacunas)
    {
        string codigo = ibge.Id.ToString(CultureInfo.InvariantCulture);
        string ufCodigo = ibge.UfId.ToString(CultureInfo.InvariantCulture);

        RegiaoRef imediata = ibge.RegiaoImediata ?? throw new ImportException("sem região geográfica imediata.");
        if (ibge.Microrregiao is null)
        {
            lacunas.Add(Lacuna(ibge, "microrregiao",
                "Município criado depois do congelamento das microrregiões do IBGE (2017).",
                "microrregiao = null; a simulação regional usa a região geográfica imediata."));
        }

        PontoMunicipio ponto = raw.Pontos.TryGetValue(ibge.Id, out PontoMunicipio? pm)
            ? pm
            : throw new ImportException($"sem polígono na Malha Municipal {Malha.Ano}.");

        long populacao = raw.Estimativa.TryGetValue(codigo, out long p)
            ? p
            : throw new ImportException($"sem estimativa de população {raw.EstimativaAno}.");

        // Perfil do Censo 2022; município criado depois do Censo usa o da UF.
        bool hasCenso = Sources.CensoBlocks.All(b => raw.Censo[b.Name].Values.ContainsKey(codigo));
        string perfilLocalidade = hasCenso ? codigo : ufCodigo;
        if (!hasCenso)
        {
            lacunas.Add(Lacuna(ibge, "perfil",
                "Município sem dados no Censo 2022 (criado depois do recenseamento).",
                $"perfil da UF ({ibge.UfSigla}) no Censo 2022, com perfil_fonte = \"uf\"."));
        }

        PerfilCenso perfil = BuildPerfil(raw, perfilLocalidade);

        int tseCodigo = raw.TseCodePorIbge.TryGetValue(ibge.Id, out int t)
            ? t
            : throw new ImportException("sem correspondência na tabela de códigos TSE ↔ IBGE.");
        long eleitorado = raw.EleitoradoPorTse.GetValueOrDefault(tseCodigo);

        bool eleicoesMunicipais = ibge.UfSigla != DistritoFederalUf && ibge.Id != FernandoDeNoronha;
        bool temRegistroVagas = raw.VagasVereador2024PorTse.TryGetValue(tseCodigo, out int vagasReais);

        int vagas;
        int vagasMax;
        string vagasFonte;
        if (!eleicoesMunicipais)
        {
            if (temRegistroVagas)
            {
                throw new ImportException("marcado sem eleições municipais, mas tem vagas de vereador no TSE em 2024.");
            }

            (vagas, vagasMax, vagasFonte) = (0, 0, Municipio.VagasFonteSemEleicao);
        }
        else
        {
            if (eleitorado <= 0)
            {
                throw new ImportException("sem eleitorado no perfil do eleitorado do TSE.");
            }

            vagasMax = ElectionRules.VagasVereador(populacao);
            if (temRegistroVagas)
            {
                (vagas, vagasFonte) = (vagasReais, Municipio.VagasFonteTse2024);
            }
            else
            {
                (vagas, vagasFonte) = (vagasMax, Municipio.VagasFonteTeto);
                lacunas.Add(Lacuna(ibge, "vagas_vereador",
                    "Sem registro de vagas de vereador na eleição ordinária de 2024 do TSE.",
                    "teto do art. 29, IV da Constituição com a população estimada."));
            }
        }

        return new Municipio
        {
            CodigoIbge = ibge.Id,
            Nome = ibge.Nome,
            Uf = ibge.UfSigla,
            Regiao = ibge.RegiaoSigla,
            Microrregiao = ibge.Microrregiao,
            RegiaoImediata = imediata,
            Lat = ponto.Lat,
            Lon = ponto.Lon,
            CoordenadaMetodo = ponto.Metodo,
            AreaKm2 = ponto.AreaKm2,
            Populacao = populacao,
            Eleitorado = eleitorado,
            UrbanaPct = PerfilCenso.UrbanaPct(perfil.Situacao["urbana"], perfil.PopulacaoCenso),
            PerfilFonte = hasCenso ? Municipio.PerfilFonteMunicipio : Municipio.PerfilFonteUf,
            Perfil = perfil,
            EleicoesMunicipais = eleicoesMunicipais,
            VagasVereador = vagas,
            VagasVereadorMax = vagasMax,
            VagasFonte = vagasFonte,
            SegundoTurnoPrefeito = eleicoesMunicipais && ElectionRules.TemSegundoTurno(eleitorado),
            Porte = portes.Classify(populacao),
        };
    }

    private static PerfilCenso BuildPerfil(RawSources raw, string localidade)
    {
        Dictionary<string, long> Block(string name) =>
            raw.Censo[name].Values.TryGetValue(localidade, out Dictionary<string, long>? v)
                ? new Dictionary<string, long>(v, StringComparer.Ordinal)
                : throw new ImportException($"Censo 2022 sem o bloco '{name}' para a localidade {localidade}.");

        long Total(string name) =>
            raw.Censo[name].Totals.TryGetValue(localidade, out long total)
                ? total
                : throw new ImportException($"Censo 2022 sem o total de '{name}' para a localidade {localidade}.");

        long populacaoCenso = Total("idade");
        long situacaoTotal = Total("situacao");
        if (situacaoTotal != populacaoCenso)
        {
            throw new ImportException($"Censo 2022: população por idade ({populacaoCenso}) e por situação ({situacaoTotal}) diferentes.");
        }

        return new PerfilCenso
        {
            PopulacaoCenso = populacaoCenso,
            Situacao = Block("situacao"),
            Idade = Block("idade"),
            Renda = Block("renda"),
            Religiao = Block("religiao"),
            Escolaridade = Block("escolaridade"),
        };
    }

    private static List<FonteCampo> BuildCampos(RawSources raw)
    {
        FonteCampo Campo(string campo, string fonte, int ano, string fileKey) => new()
        {
            Campo = campo,
            Fonte = fonte,
            Ano = ano,
            Url = raw.Files[fileKey].Url,
            BaixadoEm = raw.Files[fileKey].DownloadedAt,
            Observacao = fileKey.StartsWith("censo_", StringComparison.Ordinal)
                && Sources.CensoBlocks.Single(b => $"censo_{b.Name}" == fileKey).FromSample
                ? Sources.SampleNote
                : null,
        };

        int localidadesAno = raw.Files["localidades"].DownloadedAt.Year;
        return
        [
            Campo("codigo_ibge, nome, uf, regiao, microrregiao, regiao_imediata", "IBGE — API de Localidades", localidadesAno, "localidades"),
            Campo("lat, lon, coordenada_metodo, area_km2", $"IBGE — Malha Municipal {Malha.Ano} (BR_Municipios_{Malha.Ano})", Malha.Ano, "malha")
                with { Observacao = Malha.MetodoDescricao },
            Campo("populacao, porte, vagas_vereador_max", "IBGE — Estimativas da População (SIDRA 6579)", raw.EstimativaAno, "estimativa"),
            Campo("perfil.situacao, urbana_pct", "IBGE — Censo Demográfico 2022 (SIDRA 10211)", 2022, "censo_situacao"),
            Campo("perfil.idade, perfil.populacao_censo", "IBGE — Censo Demográfico 2022 (SIDRA 9514)", 2022, "censo_idade"),
            Campo("perfil.renda", "IBGE — Censo Demográfico 2022 (SIDRA 10296)", 2022, "censo_renda"),
            Campo("perfil.religiao", "IBGE — Censo Demográfico 2022 (SIDRA 10198)", 2022, "censo_religiao"),
            Campo("perfil.escolaridade", "IBGE — Censo Demográfico 2022 (SIDRA 10061)", 2022, "censo_escolaridade"),
            Campo("eleitorado, segundo_turno_prefeito", $"TSE — Perfil do eleitorado atual (gerado em {raw.EleitoradoDataGeracao:dd/MM/yyyy})", raw.EleitoradoDataGeracao.Year, "eleitorado"),
            Campo("correspondência TSE ↔ IBGE", "TSE — Códigos oficiais de UF e municípios segundo o TSE e o IBGE", raw.Files["tse_codigos"].DownloadedAt.Year, "tse_codigos"),
            Campo("vagas_vereador", "TSE — Consulta de vagas, Eleições Municipais 2024 (eleição ordinária)", 2024, "vagas"),
        ];
    }

    private static Lacuna Lacuna(IbgeMunicipio m, string campo, string descricao, string alternativa) => new()
    {
        CodigoIbge = m.Id,
        Nome = m.Nome,
        Campo = campo,
        Descricao = descricao,
        Alternativa = alternativa,
    };
}

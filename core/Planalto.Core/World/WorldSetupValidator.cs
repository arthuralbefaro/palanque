using Planalto.Core.Parties;

namespace Planalto.Core.World;

/// <summary>Validações da tela "Novo Mundo" (GDD 16.1). Nunca lança exceção: devolve erros e avisos.</summary>
internal static class WorldSetupValidator
{
    /// <summary>A cobertura do espectro só é exigida (como aviso) a partir deste número de partidos.</summary>
    private const int SpectrumCheckMinParties = 4;

    public static WorldValidationReport Validate(WorldSetup setup)
    {
        var issues = new List<WorldIssue>();
        WorldContent content = setup.Content;
        PartyContent parties = content.Parties;

        WorldPreset? preset = content.Presets.Find(setup.PresetId);
        if (preset is null)
        {
            issues.Add(Error(WorldIssueCode.PresetDesconhecida, detail: setup.PresetId));
        }
        else if (!preset.Available)
        {
            issues.Add(Error(WorldIssueCode.PresetIndisponivel, detail: preset.UnavailableReason));
        }

        if (setup.Parties.Count < setup.MinParties || setup.Parties.Count > setup.MaxParties)
        {
            issues.Add(Error(WorldIssueCode.QuantidadeDePartidosForaDaFaixa, detail: setup.Parties.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        foreach (PartyDraft party in setup.Parties)
        {
            ValidateParty(party, content, issues);
        }

        ValidateUniqueness(setup.Parties, parties, issues);
        ValidateSpectrum(setup.Parties, issues);
        ValidateSizes(setup.Parties, parties.Generator, issues);
        ValidateOptions(setup.Options, content.Options, issues);
        return new WorldValidationReport(issues);
    }

    private static void ValidateParty(PartyDraft party, WorldContent content, List<WorldIssue> issues)
    {
        PartyContent parties = content.Parties;
        string id = party.Id;

        if (string.IsNullOrWhiteSpace(party.Name))
        {
            issues.Add(Error(WorldIssueCode.NomeVazio, id));
        }
        else if (parties.Blocklist.CheckName(party.Name) is BlockMatch nameMatch)
        {
            issues.Add(Error(WorldIssueCode.NomeBloqueado, id, detail: nameMatch.Entry.Name));
        }

        string sigla = party.Sigla ?? "";
        int length = PartyBlocklist.NormalizeSigla(sigla).Length;
        SiglaRules siglaRules = parties.Generator.Sigla;
        if (!sigla.All(char.IsLetter))
        {
            issues.Add(Error(WorldIssueCode.SiglaComCaracteresInvalidos, id, detail: sigla));
        }
        else if (length < siglaRules.MinLength || length > siglaRules.MaxLength)
        {
            issues.Add(Error(WorldIssueCode.SiglaComTamanhoInvalido, id, detail: sigla));
        }
        else if (parties.Blocklist.CheckSigla(sigla) is BlockMatch siglaMatch)
        {
            WorldIssueCode code = siglaMatch.Reason == BlockReason.SiglaIgual
                ? WorldIssueCode.SiglaBloqueada
                : WorldIssueCode.SiglaBloqueadaPorOrdemDasLetras;
            issues.Add(Error(code, id, detail: siglaMatch.Entry.Sigla));
        }

        if (!content.NumberRules.IsValidPartyNumber(party.Number))
        {
            issues.Add(Error(WorldIssueCode.NumeroForaDaFaixa, id, detail: party.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        FamilyDefinition? family = parties.Families.Find(party.FamilyId);
        if (family is null)
        {
            issues.Add(Error(WorldIssueCode.FamiliaDesconhecida, id, detail: party.FamilyId));
        }

        if (!party.Position.IsValid)
        {
            issues.Add(Error(WorldIssueCode.PosicaoForaDosEixos, id));
        }
        else if (family is not null && !family.Contains(party.Position))
        {
            issues.Add(Warning(WorldIssueCode.PosicaoForaDaFaixaDaFamilia, id, detail: family.Id));
        }

        if (party.Colors is null || !OkColor.IsValidHex(party.Colors.Primary) || !OkColor.IsValidHex(party.Colors.Secondary))
        {
            issues.Add(Error(WorldIssueCode.CorInvalida, id));
        }

        if (!parties.LogoIcons.Contains(party.LogoIcon))
        {
            issues.Add(Error(WorldIssueCode.LogotipoDesconhecido, id, detail: party.LogoIcon));
        }
    }

    private static void ValidateUniqueness(IReadOnlyList<PartyDraft> list, PartyContent parties, List<WorldIssue> issues)
    {
        double similar = parties.Generator.Colors.MinDistanceFloor;
        for (int i = 0; i < list.Count; i++)
        {
            for (int j = i + 1; j < list.Count; j++)
            {
                PartyDraft a = list[i];
                PartyDraft b = list[j];
                if (!string.IsNullOrWhiteSpace(a.Name) && parties.Blocklist.NormalizeName(a.Name) == parties.Blocklist.NormalizeName(b.Name))
                {
                    issues.Add(Error(WorldIssueCode.NomeRepetido, a.Id, b.Id, a.Name));
                }

                string siglaA = PartyBlocklist.NormalizeSigla(a.Sigla ?? "");
                if (siglaA.Length > 0 && siglaA == PartyBlocklist.NormalizeSigla(b.Sigla ?? ""))
                {
                    issues.Add(Error(WorldIssueCode.SiglaRepetida, a.Id, b.Id, a.Sigla));
                }

                if (a.Number == b.Number)
                {
                    issues.Add(Error(WorldIssueCode.NumeroRepetido, a.Id, b.Id, a.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                }

                if (OkColor.TryParseHex(a.Colors?.Primary, out OkLab colorA)
                    && OkColor.TryParseHex(b.Colors?.Primary, out OkLab colorB)
                    && colorA.DistanceTo(colorB) < similar)
                {
                    issues.Add(Warning(WorldIssueCode.CoresParecidas, a.Id, b.Id));
                }
            }
        }
    }

    private static void ValidateSpectrum(IReadOnlyList<PartyDraft> list, List<WorldIssue> issues)
    {
        if (list.Count < SpectrumCheckMinParties)
        {
            return;
        }

        if (!list.Any(p => p.Position.Economic < 0))
        {
            issues.Add(Warning(WorldIssueCode.EspectroSemEsquerdaEconomica));
        }

        if (!list.Any(p => p.Position.Economic > 0))
        {
            issues.Add(Warning(WorldIssueCode.EspectroSemDireitaEconomica));
        }
    }

    private static void ValidateSizes(IReadOnlyList<PartyDraft> list, PartyGeneratorConfig config, List<WorldIssue> issues)
    {
        if (list.Count == 0)
        {
            return;
        }

        SizeWarningConfig limits = config.SizeWarnings;
        if (!list.Any(p => p.Size >= limits.MinMainSize))
        {
            issues.Add(Warning(WorldIssueCode.NenhumPartidoPrincipal));
        }

        int dominant = list.Count(p => p.Size == PartySize.Dominante);
        if (dominant > limits.MaxDominant)
        {
            issues.Add(Warning(WorldIssueCode.DominantesDemais, detail: dominant.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var sizes = list.Select(p => p.Size).ToList();
        foreach (PartyDraft party in list.Where(p => p.Size == PartySize.Nanico))
        {
            if (config.ShareOf(PartySize.Nanico, sizes) > limits.MaxNanicoShare)
            {
                issues.Add(Warning(WorldIssueCode.NanicoGrandeDemais, party.Id));
            }
        }
    }

    private static void ValidateOptions(WorldOptions options, WorldOptionsCatalog catalog, List<WorldIssue> issues)
    {
        if (!catalog.IsValidStartYear(options.StartYear))
        {
            issues.Add(Error(WorldIssueCode.AnoInicialInvalido, detail: options.StartYear.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (!catalog.HasDifficulty(options.Difficulty))
        {
            issues.Add(Error(WorldIssueCode.DificuldadeDesconhecida, detail: options.Difficulty));
        }
    }

    private static WorldIssue Error(WorldIssueCode code, string? partyId = null, string? otherId = null, string? detail = null) =>
        new(code, WorldIssueSeverity.Error, partyId, otherId, detail);

    private static WorldIssue Warning(WorldIssueCode code, string? partyId = null, string? otherId = null, string? detail = null) =>
        new(code, WorldIssueSeverity.Warning, partyId, otherId, detail);
}

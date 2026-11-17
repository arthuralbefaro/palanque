namespace Planalto.Core.Territory;

/// <summary>
/// De onde vem o nome de uma zona (D-058), para a Ficha da zona e o crédito do mapa. A interface só exibe estes textos.
/// </summary>
public static class ZoneNameOrigin
{
    /// <summary>Crédito exigido pela licença ODbL onde aparece nome de bairro do OpenStreetMap.</summary>
    public const string CreditoOsm = "© colaboradores do OpenStreetMap";

    /// <summary>Fonte do nome em poucas palavras ("IBGE", "OpenStreetMap", "nome gerado").</summary>
    public static string Fonte(Zona zona) => zona.NomeFonte switch
    {
        Zona.NomeBairro or Zona.NomeDistrito => "IBGE",
        Zona.NomeOsm => "OpenStreetMap",
        _ => "nome gerado",
    };

    /// <summary>Frase da Ficha da zona com a origem do nome, terminada em ponto.</summary>
    public static string Frase(Zona zona) => zona.NomeFonte switch
    {
        Zona.NomeBairro => "Nome do bairro: IBGE (o bairro mais populoso da zona, pela malha de bairros do Censo 2022).",
        Zona.NomeOsm => $"Nome do bairro: OpenStreetMap (o IBGE não tem bairro oficial aqui; {CreditoOsm}, licença ODbL).",
        Zona.NomeDistrito => "Nome do distrito: IBGE (nome oficial do distrito).",
        _ => "Nome gerado pelo jogo, pela posição na cidade: aqui não há bairro oficial.",
    };

    /// <summary>Crédito do mapa da cidade: só quando alguma zona tem nome do OpenStreetMap; null nas outras.</summary>
    public static string? Credito(ZonasMunicipio municipio) => municipio.UsaOpenStreetMap ? CreditoOsm : null;
}

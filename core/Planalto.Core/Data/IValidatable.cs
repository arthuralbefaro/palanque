namespace Planalto.Core.Data;

/// <summary>Conteúdo carregado de JSON que sabe conferir as próprias regras.</summary>
public interface IValidatable
{
    IEnumerable<string> Validate();
}

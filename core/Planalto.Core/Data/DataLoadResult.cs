namespace Planalto.Core.Data;

public sealed class DataLoadResult<T>
    where T : class
{
    internal DataLoadResult(T? value, IReadOnlyList<string> errors)
    {
        Value = value;
        Errors = errors;
    }

    /// <summary>Conteúdo carregado; null só quando o JSON nem pôde ser lido.</summary>
    public T? Value { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool IsValid => Value is not null && Errors.Count == 0;

    public T GetValueOrThrow() =>
        IsValid ? Value! : throw new DataValidationException(Errors);
}

public sealed class DataValidationException(IReadOnlyList<string> errors)
    : Exception("Dados inválidos:" + Environment.NewLine + string.Join(Environment.NewLine, errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

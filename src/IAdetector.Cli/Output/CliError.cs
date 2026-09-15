namespace IAdetector.Cli.Output;

/// <summary>
/// Códigos de error del contrato Arsenal y su mapeo a código de salida. Dos de ellos no
/// aplican a este proyecto y se documentan como tales en el manifiesto en vez de inventarles
/// un significado: la detección es 100% local, no hay API externa que autentique
/// (<c>auth</c>) ni cuota que agotar (<c>rate_limit</c>).
/// </summary>
internal static class ErrorCodes
{
    public const string Usage = "usage";
    public const string NotFound = "not_found";
    public const string Auth = "auth";
    public const string RateLimit = "rate_limit";
    public const string Network = "network";
    public const string Api = "api";
    public const string Internal = "internal";

    public static int ExitCodeFor(string code) => code switch
    {
        Usage => ExitCodes.Usage,
        NotFound => ExitCodes.NotFound,
        Auth => ExitCodes.Auth,
        RateLimit => ExitCodes.RateLimit,
        Network => ExitCodes.Network,
        // `api` e `internal` comparten el 1: "falló y no fue culpa de cómo se invocó".
        _ => ExitCodes.Failure,
    };
}

internal static class ExitCodes
{
    public const int Ok = 0;
    public const int Failure = 1;
    public const int Usage = 2;
    public const int NotFound = 3;
    public const int Auth = 4;
    public const int RateLimit = 5;
    public const int Network = 6;

    /// <summary>Tabla que publica <c>describe --json</c>, para que un agente no tenga que adivinarla.</summary>
    public static IReadOnlyList<(int Code, string Name, string Meaning)> Table { get; } =
    [
        (Ok, "ok", "Éxito, incluido un resultado vacío."),
        (Failure, "api", "El análisis falló o devolvió algo inesperado (modelo corrupto, error interno)."),
        (Usage, "usage", "Error de uso: flag desconocido, formato inválido, argumentos incompatibles."),
        (NotFound, "not_found", "No se encontró el archivo, directorio o modelo indicado."),
        (Auth, "auth", "No aplica: la detección es 100% local, no hay API key ni credenciales."),
        (RateLimit, "rate_limit", "No aplica: no hay servicio externo con cuota."),
        (Network, "network", "Fallo de red o timeout — solo alcanzable en 'models download'."),
    ];
}

/// <summary>Error del contrato: código estable, mensaje humano y una pista accionable obligatoria.</summary>
internal sealed record CliError(string Code, string Message, string Hint)
{
    public int ExitCode => ErrorCodes.ExitCodeFor(Code);
}

/// <summary>
/// Excepción que transporta un <see cref="CliError"/> hasta el borde de la aplicación, donde
/// se convierte en envelope de error + código de salida. Evita que cada comando tenga que
/// repetir el mismo par "imprimir en stderr / devolver N".
/// </summary>
internal sealed class CliException(CliError error) : Exception(error.Message)
{
    public CliError Error { get; } = error;

    public static CliException Usage(string message, string hint) =>
        new(new CliError(ErrorCodes.Usage, message, hint));

    public static CliException NotFound(string message, string hint) =>
        new(new CliError(ErrorCodes.NotFound, message, hint));
}

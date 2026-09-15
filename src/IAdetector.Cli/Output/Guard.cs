using System.CommandLine;

namespace IAdetector.Cli.Output;

/// <summary>
/// Borde común de todos los comandos: resuelve los flags globales, ejecuta el cuerpo y convierte
/// cualquier fallo en sobre de error + código de salida.
/// </summary>
/// <remarks>
/// Va aquí y no alrededor de <c>InvokeAsync</c> en <c>Program</c> porque System.CommandLine
/// atrapa las excepciones de la acción por su cuenta y las imprime como un volcado de pila: el
/// <c>try/catch</c> de más afuera nunca llega a verlas. Envolver el cuerpo de la acción es el
/// único punto donde el contrato de errores se puede cumplir de verdad.
/// </remarks>
internal static class Guard
{
    public static int Run(ParseResult parseResult, string command, Func<OutputContext, int> body) =>
        RunAsync(parseResult, command, context => Task.FromResult(body(context))).GetAwaiter().GetResult();

    public static async Task<int> RunAsync(
        ParseResult parseResult,
        string command,
        Func<OutputContext, Task<int>> body)
    {
        // Si los propios flags globales son inválidos no hay contexto en el que confiar, así que
        // se reporta en el formato por defecto antes que arriesgar un segundo fallo al reportar.
        OutputContext context;
        try
        {
            context = GlobalOptions.Resolve(parseResult);
        }
        catch (CliException exception)
        {
            return Renderer.WriteError(FallbackContext(), command, exception.Error);
        }

        try
        {
            return await body(context).ConfigureAwait(false);
        }
        catch (CliException exception)
        {
            return Renderer.WriteError(context, command, exception.Error);
        }
        catch (OperationCanceledException)
        {
            context.Note("Interrumpido.");
            return 130;
        }
        catch (Exception exception)
        {
            return Renderer.WriteError(context, command, Classify(exception));
        }
    }

    private static OutputContext FallbackContext() => new(
        OutputFormat.Table,
        OutputFile: null,
        Quiet: false,
        NoColor: true,
        Core.Abstractions.DetectionCacheMode.ReadWrite);

    /// <summary>
    /// Traduce una excepción inesperada al código del contrato más honesto que se pueda afirmar.
    /// </summary>
    private static CliError Classify(Exception exception) => exception switch
    {
        FileNotFoundException or DirectoryNotFoundException => new CliError(
            ErrorCodes.NotFound,
            exception.Message,
            "Verifica la ruta indicada."),

        UnauthorizedAccessException => new CliError(
            ErrorCodes.NotFound,
            exception.Message,
            "El proceso no tiene permiso para leer esa ruta."),

        HttpRequestException or TaskCanceledException or TimeoutException => new CliError(
            ErrorCodes.Network,
            exception.Message,
            "Sólo `models download` sale a la red. Revisa la conexión y reintenta."),

        IOException => new CliError(
            ErrorCodes.Api,
            exception.Message,
            "Fallo de entrada/salida. Reintenta; si persiste, revisa el disco."),

        _ => new CliError(
            ErrorCodes.Internal,
            exception.Message,
            "Error inesperado. Reejecuta con --format json para ver el detalle completo."),
    };
}

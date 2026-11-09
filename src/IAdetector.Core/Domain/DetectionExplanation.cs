namespace IAdetector.Core.Domain;

/// <summary>
/// Rango típico de la puntuación normalizada de una señal en texto humano de referencia
/// (percentiles 10, 50 y 90 del corpus de calibración, solo sobre muestras en las que la
/// señal midió de verdad). Es una referencia de ese corpus, no una verdad universal.
/// </summary>
public sealed record HumanRange(double P10, double Median, double P90, int Samples);

/// <summary>Estados posibles de una señal en una <see cref="SignalDiagnostic"/>.</summary>
public static class SignalStatus
{
    /// <summary>La señal no pudo medir el texto (HasData = false).</summary>
    public const string NoData = "sin_datos";

    /// <summary>La puntuación cae entre los percentiles 10 y 90 humanos.</summary>
    public const string InHumanRange = "en_rango_humano";

    /// <summary>La puntuación supera el percentil 90 humano: más «de IA» que el 90% del texto humano de referencia.</summary>
    public const string AboveHumanRange = "mas_ia_que_el_rango_humano";

    /// <summary>La puntuación queda bajo el percentil 10 humano: aún más «humana» que lo típico.</summary>
    public const string BelowHumanRange = "mas_humano_que_lo_tipico";

    /// <summary>No hay rango de referencia calculado para esta señal.</summary>
    public const string NoReference = "sin_referencia";
}

/// <summary>
/// Diagnóstico accionable de una señal: qué midió, cuánto empuja el resultado final
/// (contribución al logit del ensemble calibrado), dónde cae frente al rango humano de
/// referencia y qué hacer si está fuera de rango. Es la base del bucle de reescritura:
/// un agente puede ordenar las señales por contribución y atacar primero la que más
/// empuja, en vez de reescribir a ciegas.
/// </summary>
/// <param name="Coefficient">Coeficiente calibrado de la señal (β), o null si el ensemble no la conoce.</param>
/// <param name="LogitContribution">β × puntuación normalizada: cuánto suma esta señal al logit (antes de la sigmoide). Null si no hay coeficiente.</param>
/// <param name="Reference">Rango humano de referencia de la puntuación normalizada, o null si no se calculó.</param>
/// <param name="Recommendation">Qué hacer con esta señal; vacío si está en rango o sin referencia.</param>
public sealed record SignalDiagnostic(
    string Name,
    bool HasData,
    double RawValue,
    double NormalizedScore,
    double? Coefficient,
    double? LogitContribution,
    HumanRange? Reference,
    string Status,
    string Recommendation);

/// <summary>
/// Explicación completa y prescriptiva de una detección: el resultado, el diagnóstico por
/// señal y las acciones priorizadas (las señales fuera del rango humano ordenadas por su
/// contribución al logit, de mayor a menor).
/// </summary>
public sealed record DetectionExplanation(
    Verdict Verdict,
    double AiProbability,
    double Confidence,
    int SignalsWithData,
    int SignalsTotal,
    IReadOnlyList<SignalDiagnostic> Signals,
    IReadOnlyList<string> PrioritizedActions,
    TextLanguage Language,
    IReadOnlyList<string> Warnings);

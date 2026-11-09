namespace IAdetector.Core.Domain;

/// <summary>
/// Resultado de una única señal de detección sobre un <see cref="TextSample"/>.
/// </summary>
/// <param name="Name">Identificador estable de la señal (ej. "burstiness", "compression-ratio").</param>
/// <param name="RawValue">Valor crudo de la métrica, en su unidad original (útil para depurar/calibrar).</param>
/// <param name="NormalizedScore">Probabilidad de IA aportada por esta señal, en [0,1]. 1 = fuertemente IA.</param>
/// <param name="Weight">Peso relativo de la señal dentro del ensemble.</param>
/// <param name="Explanation">Explicación en lenguaje natural del resultado, para modo verbose.</param>
/// <param name="HasData">
/// Si la señal pudo medir el texto de verdad. Cuando es <c>false</c> (texto demasiado corto,
/// modelo no descargado, idioma no soportado…), <see cref="NormalizedScore"/> es el 0.5
/// convencional de «sin evidencia», no una observación: ningún consumidor debe tratarlo
/// como una medición, y el ensemble no debe emitir veredicto si ninguna señal tiene datos.
/// </param>
public sealed record SignalScore(
    string Name,
    double RawValue,
    double NormalizedScore,
    double Weight,
    string Explanation,
    bool HasData = true);

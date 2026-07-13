namespace IAdetector.Infrastructure.Signals;

/// <summary>Utilidades compartidas por las señales estadísticas para mapear una métrica cruda a una probabilidad [0,1].</summary>
internal static class ScoreNormalization
{
    /// <summary>
    /// Interpola linealmente (con clamp) <paramref name="value"/> entre dos puntos de referencia:
    /// <paramref name="valueAtZero"/> (métrica típica de texto humano → score 0) y
    /// <paramref name="valueAtOne"/> (métrica típica de texto de IA → score 1). El orden de los
    /// puntos determina la dirección de la relación, así que funciona igual para métricas
    /// directamente o inversamente correlacionadas con "generado por IA".
    /// </summary>
    public static double MapToProbability(double value, double valueAtZero, double valueAtOne)
    {
        if (valueAtZero == valueAtOne)
        {
            return 0.5;
        }

        var t = (value - valueAtZero) / (valueAtOne - valueAtZero);
        return Math.Clamp(t, 0.0, 1.0);
    }

    /// <summary>Factor de peso aplicado cuando una señal no tuvo suficiente texto para ser confiable.</summary>
    public const double InsufficientDataWeightFactor = 0.2;
}

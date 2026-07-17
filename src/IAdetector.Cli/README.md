# IAdetector CLI

CLI (`iadetector`) para detectar si un texto fue generado por IA, **100% local** (sin llamar a ninguna API externa de pago). Ver el README principal del repositorio (`../../README.md`) para la arquitectura completa y más detalles.

## Uso

```bash
iadetector detect archivo.txt [--json] [--verbose] [--no-color]
iadetector detect --text "texto a analizar" [--verbose]
iadetector batch <directorio> [--pattern *.txt] [--recursive] [--max-files 1000] [--output resultados.csv]
iadetector models download --model <id> [--force]
iadetector models list
```

Modelos disponibles para `models download`: `roberta-openai-detector` (clasificador), `distilgpt2-perplexity` (señal de perplexity). Ninguno se descarga automáticamente durante una detección; sin ellos, esas señales quedan neutrales y el resto del ensemble sigue funcionando.

# IAdetector

Detecta si un texto fue generado por IA, **100% local**: sin llamar a ninguna API externa de pago (OpenAI, GPTZero, Originality.ai, etc.). Combina señales estadísticas/estilométricas puras con dos modelos ONNX pequeños que corren localmente (un clasificador RoBERTa y un modelo de perplexity DistilGPT-2), calibradas mediante regresión logística sobre datos reales (HC3).

Consumible de dos formas sobre el mismo motor:

- **Servidor MCP** (`IAdetector.Mcp`) — tools para que un agente (Claude Code, VS Code Copilot, etc.) analice texto.
- **CLI** (`IAdetector.Cli`, comando `iadetector`) — para uso directo en terminal o scripts.

## Arquitectura

Clean Architecture pragmática, 4 proyectos productivos:

```
src/
  IAdetector.Core/            Dominio + casos de uso. Cero dependencias externas.
  IAdetector.Infrastructure/  Señales (estadísticas + ONNX), ensemble calibrado, descarga de modelos.
  IAdetector.Mcp/             Servidor MCP (stdio), vía ModelContextProtocol C# SDK.
  IAdetector.Cli/             CLI (comando `iadetector`), vía System.CommandLine.
tools/
  IAdetector.Calibration/     Herramienta de desarrollo: extrae features para recalibrar el ensemble.
tests/                        xUnit: Core, Infrastructure, e integración end-to-end (corpus HC3 held-out).
scripts/calibration/          Dataset de calibración + script Python (scikit-learn) que ajusta los pesos.
```

Siete señales combinadas en `CalibratedEnsembleScorer`: burstiness, type-token ratio, entropía léxica, ratio de compresión, repetición de n-gramas, clasificador ONNX (RoBERTa) y perplexity (DistilGPT-2). Ver `EnsembleWeights.json` para los coeficientes calibrados.

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Para publicar binarios Native AOT en Windows: Visual Studio Build Tools con el workload de C++ (`vswhere.exe` debe estar accesible; normalmente en `C:\Program Files (x86)\Microsoft Visual Studio\Installer\`).

## Instalación

### CLI

**Opción A — `dotnet tool` (requiere el runtime .NET instalado):**

```bash
dotnet pack src/IAdetector.Cli -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg IAdetector.Cli
iadetector detect ejemplo.txt
```

**Opción B — binario nativo standalone (sin necesidad de instalar .NET):**

```bash
dotnet publish src/IAdetector.Cli -c Release -r win-x64 -p:PublishAot=true -o ./publish/cli
./publish/cli/IAdetector.Cli.exe detect ejemplo.txt
```

Cambiar `win-x64` por `linux-x64` según la plataforma.

### Servidor MCP

**Opción A — publicar como paquete NuGet (`dnx`, sin clonar el repo):**

```bash
dotnet pack src/IAdetector.Mcp -c Release -o ./nupkg
```

Configurar el host MCP (ej. `.vscode/mcp.json`):

```json
{
  "servers": {
    "IAdetector": {
      "type": "stdio",
      "command": "dnx",
      "args": ["IAdetector.Mcp", "--version", "0.1.0-beta", "--yes"]
    }
  }
}
```

**Opción B — binario nativo standalone apuntado directamente:**

```bash
dotnet publish src/IAdetector.Mcp -c Release -r win-x64 -p:PublishAot=true -o ./publish/mcp
```

```json
{
  "servers": {
    "IAdetector": {
      "type": "stdio",
      "command": "C:/ruta/a/publish/mcp/IAdetector.Mcp.exe"
    }
  }
}
```

## Modelos ONNX

Los modelos (clasificador RoBERTa ~126MB, DistilGPT-2 ~237MB) **no se descargan automáticamente** durante una detección — hay que descargarlos explícitamente una vez:

```bash
iadetector models download --model roberta-openai-detector
iadetector models download --model distilgpt2-perplexity
iadetector models list
```

Se cachean en `%LOCALAPPDATA%\IAdetector\models` (Windows) con verificación de integridad SHA-256. Sin ellos, esas dos señales quedan neutrales (peso reducido) y el resto del ensemble sigue funcionando.

## Uso de la CLI

```bash
iadetector detect archivo.txt [--json] [--verbose] [--no-color]
iadetector detect --text "texto a analizar" [--verbose]
iadetector batch <directorio> [--pattern *.txt] [--recursive] [--max-files 1000] [--output resultados.csv]
iadetector models download --model <id> [--force]
iadetector models list
```

## Tools MCP

- `detect_text(text, verbose)` — texto inline (máx. 20 000 caracteres).
- `detect_file(path, verbose)` — archivo local (máx. 5MB; fragmenta automáticamente textos largos, muestreando si son muy grandes).
- `batch_detect_directory(path, pattern, recursive, maxFiles)` — varios archivos a la vez.
- `explain_detection(text)` — como `detect_text` pero siempre con el desglose completo de señales.

## Desarrollo

```bash
dotnet build
dotnet test tests/IAdetector.Core.Tests
dotnet test tests/IAdetector.Infrastructure.Tests   # requiere modelos descargados para las señales ONNX
dotnet test tests/IAdetector.Integration.Tests       # requiere modelos + scripts/calibration/data/open_qa.jsonl
```

### Recalibrar el ensemble

```bash
dotnet run --project tools/IAdetector.Calibration -- <dataset.jsonl formato HC3> scripts/calibration/data/features.csv
python scripts/calibration/fit_logistic_regression.py scripts/calibration/data/features.csv src/IAdetector.Infrastructure/Ensemble/EnsembleWeights.json
```

## Limitaciones conocidas

- Los umbrales de las señales estadísticas puras (Fase 1) son heurísticos iniciales; solo el ensemble completo está formalmente calibrado.
- El clasificador RoBERTa fue entrenado para detectar salidas de GPT-2 (2019); generaliza de forma imperfecta a estilos de LLMs más recientes — de ahí el ensemble con señales complementarias.
- La señal de perplexity es una versión simplificada (un solo modelo) de la idea de Fast-DetectGPT/Binoculars, no la razón completa entre dos modelos.
- Calibrado sobre un subconjunto de HC3 (inglés); no validado específicamente en español u otros idiomas.

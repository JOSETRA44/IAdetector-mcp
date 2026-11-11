# IAdetector

<!-- mcp-name: io.github.JOSETRA44/iadetector-mcp -->

Detecta si un texto fue generado por IA, **100% local**: sin llamar a ninguna API externa de pago (OpenAI, GPTZero, Originality.ai, etc.). Combina señales estadísticas/estilométricas puras con dos modelos ONNX pequeños que corren localmente (un clasificador RoBERTa y un modelo de perplexity DistilGPT-2), calibradas mediante regresión logística sobre datos reales (HC3).

Consumible de dos formas sobre el mismo motor:

- **Servidor MCP** (`IAdetector.Mcp`) — tools para que un agente (Claude Code, VS Code Copilot, etc.) analice texto.
- **CLI** (`IAdetector.Cli`, comando `iadetector`) — para uso directo en terminal o scripts.


[![NuGet](https://img.shields.io/nuget/v/IAdetector.Cli.svg)](https://www.nuget.org/packages/IAdetector.Cli)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/JOSETRA44/iadetector-mcp/blob/main/LICENSE)
[![MCP](https://img.shields.io/badge/MCP-server-blue.svg)](https://modelcontextprotocol.io)

## Qué puedes preguntarle

| Pregunta | Herramienta que responde |
|---|---|
| "¿Este texto lo escribió una IA?" | `detect_text` |
| "Analiza este archivo" | `detect_file` |
| "Revisa los 500 documentos de esta carpeta" | `batch_detect_directory` |
| "¿Por qué dio ese veredicto?" | `explain_detection` |

**Todo ocurre en tu máquina.** No hay API externa, no hay clave, y el texto
analizado **nunca sale del equipo**. Es la diferencia esencial frente a GPTZero,
Originality.ai o cualquier detector en la nube: puedes analizar un manuscrito
inédito o un documento confidencial sin entregárselo a nadie.

> [!IMPORTANT]
> **Un resultado es evidencia, nunca prueba.** La detección de texto generado por
> IA es probabilística y tiene falsos positivos reales, que además afectan de
> forma desproporcionada a quien escribe en un idioma que no es el suyo y a la
> prosa técnica muy formulaica.
>
> No uses esta herramienta como única base para acusar a un estudiante o a un
> trabajador. Sirve para priorizar qué revisar con criterio humano, no para
> decidir por ti.

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

**Opción A — `dotnet tool` desde NuGet (un solo comando, requiere el runtime .NET):**

```bash
dotnet tool install --global IAdetector.Cli
iadetector detect ejemplo.txt
```

(Para instalar desde el código fuente en vez de NuGet: `dotnet pack src/IAdetector.Cli -c Release -o ./nupkg` y luego `dotnet tool install --global --add-source ./nupkg IAdetector.Cli`.)

**Opción B — binario nativo standalone (sin necesidad de instalar .NET):**

```bash
dotnet publish src/IAdetector.Cli -c Release -r win-x64 -p:PublishAot=true -o ./publish/cli
./publish/cli/IAdetector.Cli.exe detect ejemplo.txt
```

Cambiar `win-x64` por `linux-x64` según la plataforma.

### Servidor MCP

**Opción A — paquete NuGet con `dnx` (sin clonar el repo ni instalar nada antes):**

Configurar el host MCP (ej. `.vscode/mcp.json`) y `dnx` descarga y ejecuta el servidor desde NuGet:

```json
{
  "servers": {
    "IAdetector": {
      "type": "stdio",
      "command": "dnx",
      "args": ["IAdetector.Mcp", "--version", "1.0.0", "--yes"]
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
cat archivo.txt | iadetector detect               # también lee de stdin si no hay archivo ni --text
iadetector batch <directorio> [--pattern *.txt] [--recursive] [--max-files 1000] [--output resultados.csv]
iadetector models download --model <id> [--force]
iadetector models list
iadetector models verify [--model <id>]
```

## Tools MCP

- `detect_text(text, verbose)` — texto inline (máx. 20 000 caracteres).
- `detect_file(path, verbose)` — archivo local (máx. 5MB; fragmenta automáticamente textos largos, muestreando si son muy grandes).
- `batch_detect_directory(path, pattern, recursive, maxFiles)` — varios archivos a la vez.
- `explain_detection(text)` — explicación prescriptiva: por señal, si midió, cuánto empuja el resultado (contribución al logit), su rango típico en texto humano de referencia y qué cambiar si está fuera de rango, con acciones priorizadas.
- `compare_versions(originalText, rewrittenText)` — compara una reescritura con su original (sin estado): cambio de probabilidad y por señal, con salvaguarda de significado (si la reescritura pierde el vocabulario de contenido del original, el resultado es `no_concluyente` aunque la probabilidad baje). Ver `docs/AGENT_LOOP.md`.

**Para agentes que consumen estas tools**: el contrato completo (campos, semántica, protocolo del bucle, fiabilidad por idioma y límites) está en [`docs/AGENT_GUIDE.md`](docs/AGENT_GUIDE.md).

## Skills para agentes (Claude Code)

`skills/` (layout plano estándar `skills/<nombre>/SKILL.md`, el mismo que usan proyectos como Vercel o Supabase para distribuir sus propias skills) incluye dos skills instalables con [`npx skills`](https://github.com/vercel-labs/skills):

- **`iadetector-setup`** — instalar, publicar y configurar el CLI/MCP, descargar los modelos ONNX, registrar el servidor en `.mcp.json`.
- **`iadetector-usage`** — interpretar los resultados (veredicto, probabilidad, confianza) y comunicarlos sin sobre-afirmar certeza.

Nota: viven en `skills/` (rastreado por git) y no en `.claude/skills/` (ignorado — es estado local por sesión/máquina). Cada usuario materializa las que quiera usar en su propio `.claude/skills/` local corriendo el comando de instalación de abajo; no se commitea esa copia.

Para instalarlas en otro proyecto una vez este repo esté en GitHub:

```bash
npx skills add <usuario>/IAdetector-mcp --skill iadetector-setup --skill iadetector-usage
```

O directamente desde una copia local del repo (sin necesidad de GitHub):

```bash
npx skills add /ruta/a/IAdetector-mcp --skill '*'
```

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

## CLI for agents

Authorship screening that never leaves the machine — no text is sent anywhere.

This CLI follows [`ARSENAL-SPEC.md`](../ARSENAL-SPEC.md), the contract every research
tool in this workspace shares: an agent that can drive one can drive all of them.

### Discover it without reading this file

```bash
iadetector describe --json          # every command, argument, row field and example
iadetector describe <command> --json
```

The manifest is derived from the parser itself, so it cannot drift out of date.

### Output

`-f, --format` accepts `table`, `json`, `jsonl`, `csv`, `md`; `-o FILE` writes to a file; `-q` silences notes.
**Data goes to stdout, notes and progress to stderr** — piping to `jq` always yields
parseable JSON.

`--format json` returns the standard envelope:

```json
{
  "ok": true,
  "command": "detect",
  "source": "iadetector",
  "fetched_at": "2026-08-21T14:03:11Z",
  "cached": false,
  "count": 10,
  "meta": {},
  "data": {},
  "rows": []
}
```

`count` always equals `len(rows)`. An empty result is exit 0, not an error. Failures
return an error envelope carrying `error.code`, `error.message` and an actionable
`error.hint`, printed to stdout in machine formats so a pipeline can inspect it.

### Exit codes

| Code | Meaning |
|---|---|
| 0 | Éxito, incluido un resultado vacío. |
| 1 | El análisis falló o devolvió algo inesperado (modelo corrupto, error interno). |
| 2 | Error de uso: flag desconocido, formato inválido, argumentos incompatibles. |
| 3 | No se encontró el archivo, directorio o modelo indicado. |
| 4 | No aplica: la detección es 100% local, no hay API key ni credenciales. |
| 5 | No aplica: no hay servicio externo con cuota. |
| 6 | Fallo de red o timeout — solo alcanzable en 'models download'. |

### Cache

Results are cached on disk and survive between invocations, so a repeated query is
instant. `--refresh` refetches and rewrites; `--no-cache` skips it entirely; the
envelope reports `cached`.

```bash
iadetector cache stats -f json
iadetector cache clear
```

### Recipes

```bash
iadetector detect informe.txt -f json
iadetector detect --text "un párrafo cualquiera" --verbose -f json
cat ensayo.md | iadetector detect -f json
iadetector batch corpus/ -f csv -o corpus.csv
iadetector batch corpus/ --verbose -f jsonl --max 500
```

Or through the workspace-wide entry point: `arsenal run iadetector <command> ...`,
and `arsenal doctor iadetector --live` to check this CLI still honours the contract.

## Troubleshooting

**`El modelo no está descargado`**
Los modelos ONNX no vienen incluidos. Descárgalos una sola vez:
`iadetector models download`. Ocupan unos cientos de MB y quedan en la caché de
la aplicación; míralas con `iadetector models list`.

**El primer análisis tarda mucho más que el resto**
Es la carga inicial de los modelos ONNX en memoria. Los siguientes son rápidos, y
un texto ya analizado sale de la caché de resultados al instante.

**Reanalizar un corpus vuelve a tardar lo mismo**
Comprueba que no estés pasando `--no-cache` o `--refresh`. Con
`iadetector cache stats` ves cuántos resultados hay guardados. La clave incluye
la versión de los modelos: si actualizas los modelos, la caché anterior queda
obsoleta a propósito.

**Un texto muy corto da confianza baja**
Con pocas palabras las señales estadísticas no tienen material suficiente. Por
debajo de un párrafo, el resultado no es informativo — y así lo reporta.

## Documentation

| Document | What's in it |
|---|---|
| [`CHANGELOG.md`](CHANGELOG.md) | What changed in each release |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Dev setup, the CLI contract, how to run the tests |
| [`SECURITY.md`](SECURITY.md) | What this server sends and where; how to report a vulnerability |
| [`server.json`](server.json) | Registry metadata for the official MCP registry |
| [`../ARSENAL-SPEC.md`](../ARSENAL-SPEC.md) | The CLI contract shared by every research server here |

## Licence

MIT — see [`LICENSE`](LICENSE).

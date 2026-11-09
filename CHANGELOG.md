# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and
this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Bucle de reescritura para agentes** (Fase 2, ver `docs/AGENT_LOOP.md`):
  `explain_detection` es ahora prescriptivo — por señal: si midió, contribución real al
  logit (β × puntuación), rango humano de referencia (percentiles del corpus de
  calibración, generados por `scripts/calibration/make_reference_ranges.py`) y guía de
  reescritura cuando está fuera de rango, con acciones priorizadas — y la nueva tool
  **`compare_versions`** compara original y reescritura sin estado, con salvaguarda de
  significado (similitud léxica de contenido < 0,5 ⇒ `no_concluyente`). Nuevo proyecto
  de pruebas `IAdetector.Mcp.Tests`; 16 pruebas nuevas entre Core, Infrastructure y MCP.
- **Evaluación de calibración con trazabilidad** (`docs/CALIBRATION.md`): la herramienta
  de extracción vuelca `question_id`, `source` y `<señal>_hasdata` por fila, con números
  en cultura invariante, y el script de ajuste evalúa con GroupKFold por pregunta
  reportando accuracy, Brier, ECE y AUROC. Resultado de la reevaluación 2026-11-09:
  accuracy agrupada 0,9697 (la validación por filas no inflaba la cifra en este dataset),
  cobertura medida por señal (riqueza léxica y entropía solo miden el 3,9% de las
  respuestas de HC3) y coeficientes de producción confirmados sin cambios.

### Fixed

- **La prueba de integración ya no «pasa» en silencio**: sin dataset held-out o sin los
  modelos ONNX descargados se omite con su motivo (`Xunit.SkippableFact`) en vez de
  contar como superada sin haber medido nada.

- **Evidencia insuficiente ya no produce veredicto.** `SignalScore` declara ahora si la
  señal midió el texto de verdad (`HasData`): texto demasiado corto, modelo ONNX no
  descargado o idioma no soportado devuelven el 0.5 convencional de «sin datos» marcado
  como tal. Si ninguna señal tiene datos, el ensemble devuelve `Mixed`, probabilidad 0.5
  y confianza 0, en vez del «probablemente humano» con ≈78% de confianza que producía el
  intercept calibrado sobre puros 0.5 imputados (un texto vacío salía «humano»).
- **La confianza mide el acuerdo real entre señales.** La dispersión se calcula contra la
  media ponderada de las señales, como decía la documentación, y no contra la probabilidad
  sigmoide final, que castigaba la confianza justo cuando las señales coincidían en un
  extremo.

## [0.1.0-beta] - 2026-08-22

### Added

- Conformance with [`ARSENAL-SPEC.md`](../ARSENAL-SPEC.md), the CLI contract shared
  by every research server in this workspace:
  - Five output formats: `table`, `json`, `jsonl`, `csv`, `md`.
  - A stable JSON envelope (`ok`, `command`, `source`, `fetched_at`, `cached`,
    `count`, `meta`, `data`, `rows`) and a matching error envelope carrying an
    actionable `hint`.
  - Seven documented exit codes, so a script can branch on *why* a call failed.
  - A disk cache shared across invocations, with `--refresh`, `--no-cache` and a
    `cache stats|clear|path` subcommand.
  - `describe --json`: a machine-readable manifest derived from the parser
    itself, so it cannot drift from the real commands.
  - UTF-8 output forced on Windows consoles, which default to a code page that
    cannot encode accented text.
- Caché de resultados: reanalizar un corpus no repite la inferencia.
- `server.json` for the official MCP registry, plus `LICENSE`, `SECURITY.md` and
  `CONTRIBUTING.md`.

### Changed

- Data goes to stdout and notes to stderr, so piping to `jq` always yields
  parseable output.

[Unreleased]: https://github.com/JOSETRA44/iadetector-mcp/compare/v0.1.0-beta...HEAD
[0.1.0-beta]: https://github.com/JOSETRA44/iadetector-mcp/releases/tag/v0.1.0-beta

# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and
this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- **Publicación en NuGet corregida**: la versión de `IAdetector.Mcp` seguía en
  `0.1.0-beta` y el `server.json` interno del paquete era aún la plantilla, con
  marcadores `<your GitHub username here>`; el `server.json` de la raíz apuntaba
  además al paquete de la CLI en vez de al del servidor. Los tres quedan en 1.0.0
  y apuntando a `IAdetector.Mcp`, que es el paquete que el registro MCP instala
  con `dnx`. Instalación en un comando: `dotnet tool install -g IAdetector.Cli`.

## [1.0.0] - 2026-11-10

Primera release completa: el detector queda con calibración verificada en inglés y
español, bucle prescriptivo para agentes, cobertura completa de los modelos ONNX e
integridad verificable de los modelos descargados.

### Added

- **Contrato para agentes** (Fase 5): `docs/AGENT_GUIDE.md` consolida el contrato
  completo de las tools MCP y la CLI (campos y su semántica, protocolo del bucle,
  fiabilidad por idioma, límites duros y límites honestos de comunicación). Skills
  del repositorio actualizadas (`compare_versions`, perfil en español, `models
  verify`). (El workflow de CI está preparado pero pendiente de subir: requiere el
  alcance `workflow` en el token de GitHub.)
- **Rendimiento e integridad** (Fase 4, ver `docs/PERFORMANCE.md`):
  - Las señales ONNX ya no truncan en silencio: si el texto excede la ventana del
    modelo, se evalúan ventanas repartidas por todo el texto (RoBERTa hasta 4,
    perplexity hasta 2) y se agregan. Verificado en las respuestas más largas de
    HC3: 0 veredictos cambiados, accuracy idéntica; el corpus ES de la Fase 3
    produce features byte a byte idénticas (cabe en las ventanas).
  - Los fragmentos de documentos largos se analizan en paralelo (concurrencia
    acotada a 4), agregando en el orden original.
  - `iadetector models verify`: audita los SHA-256 de los modelos descargados
    contra el catálogo. `models download` ahora verifica también los archivos
    ya presentes y vuelve a descargar los corruptos.
  - La huella de la caché de resultados incluye la versión del motor
    (`engine=4`): un cambio de semántica del análisis invalida las entradas
    anteriores aunque pesos y modelos no cambien.
  - Herramienta de benchmark reproducible en `tools/IAdetector.Benchmarks`
    (texto corto típico del bucle de agentes: ~0,8–1,0 s en caliente en la VM de
    desarrollo; los textos largos pagan ~2× por la cobertura completa).
- **Perfil calibrado en español** (Fase 3.2, ver `docs/SPANISH.md`):
  `EnsembleWeightsEs.json`, calibrado sobre un corpus propio de 568 textos
  (371 temas; humanos de Wikipedia ≤2021 y dominio público, IA de un LLM) con
  GroupKFold por tema: accuracy 0,9067, AUROC 0,9582. El scorer enruta por
  idioma detectado y el explicador usa los coeficientes y rangos humanos del
  perfil activo (`HumanReferenceRangesEs.json`). En el held-out de la Fase 3
  (IA de otro generador), la detección en español pasa de 0/18 a 12/18 con
  1/21 falsos positivos. La huella de versión de la caché cubre ambos perfiles.
- **Detección de idioma y aviso de fiabilidad en español** (Fase 3, ver
  `docs/SPANISH.md`): `DetectionResult` lleva `detectedLanguage` (detector por
  palabras funcionales, ES/EN/Unknown) y `explain_detection` incluye `language` y
  `warnings`. La medición con un corpus en español (21 humanos de dominio público +
  18 textos de IA; `scripts/calibration/data/es/`) mostró que el fallo en español no
  son los falsos positivos sino la ceguera: los modelos ONNX ingleses puntúan ≈0 todo
  texto en español y 0/18 textos de IA fueron detectados. Sin cambios de coeficientes:
  la corrección es la calibración de un perfil en español (decisión documentada).
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

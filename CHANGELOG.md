# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and
this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

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

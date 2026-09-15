# Security Policy

## Reporting a vulnerability

Please report security issues privately rather than in a public issue:
open a [security advisory](https://github.com/JOSETRA44/iadetector-mcp/security/advisories/new) on this repository.

Include what you did, what happened, and what you expected. You can expect an
acknowledgement within a few days.

## What this server sends, and where

Outbound requests go only to:

- `huggingface.co (solo para descargar los modelos)`

No telemetry, no analytics, and no data is sent anywhere else.

## Credentials

This server needs none. There is no API key to leak.

## Scope

This project is a client for a public data source. Vulnerabilities in the
upstream API itself are out of scope here — report those to its operator.

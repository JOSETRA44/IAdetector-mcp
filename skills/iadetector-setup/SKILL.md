---
name: iadetector-setup
description: Guía para instalar, publicar y configurar IAdetector (CLI `iadetector` + servidor MCP), incluyendo la descarga de los modelos ONNX (no se descargan solos) y cómo registrar el servidor MCP en `.mcp.json`/`.vscode/mcp.json`/la config de Claude Desktop. Usa esta skill SIEMPRE que el usuario pida instalar, compilar, publicar, empaquetar, o configurar IAdetector; cuando `detect_text`/`detect_file`/`batch_detect_directory`/`explain_detection` fallen porque el modelo no está descargado; o cuando el usuario quiera añadir este servidor MCP a un host (Claude Code, VS Code, Claude Desktop) — aunque no mencione "IAdetector" por nombre, solo "el detector de IA que hicimos" o similar.
---

# Instalar y configurar IAdetector

IAdetector es un servidor MCP + CLI que detecta texto generado por IA, 100% local (sin llamar a ninguna API externa). Esta skill cubre la parte de *poner en marcha* el proyecto — para interpretar los resultados una vez que ya funciona, usa la skill `iadetector-usage`.

## Requisito único: .NET 10 SDK

```bash
dotnet --version   # debe empezar con 10.
```

Si falta, instalar desde https://dotnet.microsoft.com/download/dotnet/10.0 (en Windows, `winget install Microsoft.DotNet.SDK.10` también funciona). Publicar binarios Native AOT en Windows además requiere que `vswhere.exe` esté en el PATH (normalmente en `C:\Program Files (x86)\Microsoft Visual Studio\Installer\`) — solo hace falta para AOT, no para `dotnet run`/`dotnet tool`.

## Paso 1 — Elegir cómo distribuir el CLI y el servidor MCP

Hay dos formas, no hace falta elegir una sola — se puede tener ambas:

| Forma | Cuándo usarla | Comando |
|---|---|---|
| `dotnet tool` / `dnx` | Ya se tiene el SDK de .NET instalado; más simple de mantener actualizado | `dotnet pack src/IAdetector.Cli -c Release -o ./nupkg` |
| Binario nativo (Native AOT) | Distribuir a alguien sin .NET instalado, o se quiere el arranque más rápido posible | `dotnet publish src/IAdetector.Cli -c Release -r win-x64 -p:PublishAot=true -o ./publish/cli` |

Mismo patrón para `src/IAdetector.Mcp` en vez de `src/IAdetector.Cli`. Cambiar `win-x64` por `linux-x64` en Linux.

**Para desarrollo/pruebas rápidas sin empaquetar nada**, tanto el CLI como el MCP se pueden ejecutar directo con `dotnet run --project src/IAdetector.Cli -- detect archivo.txt` — pero **no uses `dotnet run` como comando de un servidor MCP en producción**: el build de MSBuild escribe progreso a stdout antes de arrancar el proceso, lo que corrompe el framing JSON-RPC del transporte stdio. Para el servidor MCP, apunta siempre a un binario ya compilado (ver Paso 2).

## Paso 2 — Registrar el servidor MCP en el host

El servidor usa transporte **stdio**; el formato es el mismo en Claude Code (`.mcp.json` en la raíz del proyecto), VS Code (`.vscode/mcp.json`) y Claude Desktop (`claude_desktop_config.json`), solo cambia la clave raíz (`mcpServers` vs `servers`).

**Apuntando a un binario AOT ya publicado** (más simple, sin dependencias de .NET runtime en tiempo de ejecución):

```json
{
  "mcpServers": {
    "iadetector": {
      "command": "/ruta/absoluta/a/publish/mcp/IAdetector.Mcp"
    }
  }
}
```

**Apuntando a un dll compilado, vía `dotnet`** (útil mientras se desarrolla, sin publicar AOT cada vez):

```json
{
  "mcpServers": {
    "iadetector": {
      "command": "dotnet",
      "args": ["/ruta/absoluta/a/IAdetector.Mcp.dll"]
    }
  }
}
```

Tras editar la config, hay que **reiniciar la sesión del host** (Claude Code/VS Code/Claude Desktop no recargan servidores MCP en caliente).

## Paso 3 — Descargar los modelos ONNX (paso manual, no automático)

Dos de las siete señales de detección (`onnx-classifier`, `perplexity`) requieren modelos ONNX (~126MB y ~237MB) que **no se descargan solos** durante una detección — es una decisión deliberada para no sorprender con cientos de MB de tráfico la primera vez que se llama a una tool:

```bash
iadetector models download --model roberta-openai-detector
iadetector models download --model distilgpt2-perplexity
iadetector models list   # confirma que ambos quedaron "descargado"
iadetector models verify # audita los SHA-256 contra el catálogo (útil si algo huele a corrupto)
```

`models download` verifica el hash de lo que baja **y de lo que ya existía**: un archivo corrupto se vuelve a descargar solo. Si `models verify` reporta `corrupt` y no hay red para repararlo, las señales de ese modelo cargarán mal o quedarán fuera — reintenta la descarga.

Se cachean en `%LOCALAPPDATA%\IAdetector\models` (Windows) o el equivalente XDG en Linux, con verificación SHA-256. Sin ellos, esas dos señales quedan neutrales con peso reducido — el resto del ensemble (5 señales estadísticas) sigue funcionando, solo que con menos precisión.

Si `detect_text`/`detect_file` devuelven una señal con explicación `"Modelo '...' no descargado. Ejecuta 'iadetector models download'..."`, es exactamente este paso el que falta — no es un error, es el comportamiento esperado hasta que se descarguen.

## Paso 4 — Verificar que funciona

**CLI:**
```bash
iadetector detect --text "Un texto de prueba cualquiera para confirmar que el detector responde." --verbose
```

**MCP**, sin necesidad de un host completo, usando el MCP Inspector CLI (requiere Node.js):
```bash
npx @modelcontextprotocol/inspector --cli -- dotnet /ruta/a/IAdetector.Mcp.dll --method tools/list
npx @modelcontextprotocol/inspector --cli -- dotnet /ruta/a/IAdetector.Mcp.dll --method tools/call --tool-name detect_text --tool-arg "text=texto de prueba con suficientes palabras para no disparar la señal de datos insuficientes"
```
(Nota el orden: `--` y el comando del servidor van pegados a `--cli`, y `--method`/`--tool-name`/`--tool-arg` van después.)

Si ambos responden con un JSON de veredicto/probabilidad, la instalación está completa.

## Problemas comunes

- **"An error occurred invoking 'X'" sin más detalle** al llamar una tool MCP con un path/archivo inválido: revisa el mensaje completo, no solo el prefijo — el SDK antepone ese texto genérico pero añade el motivo real después de los dos puntos.
- **AOT falla al publicar en Windows** con un error relacionado al linker nativo: falta `vswhere.exe` en el PATH (ver Requisito único arriba).
- **`detect_file`/`batch_detect_directory` no encuentran el archivo/directorio**: usa siempre rutas absolutas — son procesos con su propio directorio de trabajo, que no necesariamente coincide con el del host.

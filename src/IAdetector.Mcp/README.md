<!-- mcp-name: io.github.JOSETRA44/iadetector-mcp -->

# IAdetector MCP Server

Servidor MCP que detecta si un texto fue generado por IA, **100% local** (sin llamar a ninguna API externa de pago). Ver el README principal del repositorio (`../../README.md`) para la arquitectura completa, cómo descargar los modelos ONNX y más detalles.

Se construye como aplicación autocontenida (self-contained): no requiere el runtime .NET instalado en la máquina destino. Por eso debe compilarse por separado para cada plataforma objetivo. Por defecto, este proyecto compila para:

- `win-x64`
- `linux-x64`

Para soportar más plataformas, edita `<RuntimeIdentifiers />` en el `.csproj`.

## Tools disponibles

- `detect_text(text, verbose)` — analiza un texto inline (máx. 20 000 caracteres).
- `detect_file(path, verbose)` — analiza un archivo local (máx. 5MB).
- `batch_detect_directory(path, pattern, recursive, maxFiles)` — analiza varios archivos de un directorio.
- `explain_detection(text)` — explicación prescriptiva por señal (contribución al logit, rango humano de referencia, guía de reescritura) con acciones priorizadas.
- `compare_versions(originalText, rewrittenText)` — compara original y reescritura sin estado, con salvaguarda de significado; ver `docs/AGENT_LOOP.md` en la raíz del repo.

Ninguna de estas tools llama a un servicio externo. El clasificador ONNX y la señal de perplexity requieren haber descargado sus modelos primero (ver `iadetector models download` en el paquete CLI, `IAdetector.Cli`); si no están descargados, esas señales quedan neutrales y el resto del ensemble sigue funcionando normalmente.

## Desarrollo local

Para probar este servidor desde el código fuente sin usar un paquete publicado, configura tu IDE para correr el proyecto directamente con `dotnet run`:

```json
{
  "servers": {
    "IAdetector": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "<RUTA A src/IAdetector.Mcp>"]
    }
  }
}
```

- [Use MCP servers in VS Code (Preview)](https://code.visualstudio.com/docs/copilot/chat/mcp-servers)
- [Use MCP servers in Visual Studio (Preview)](https://learn.microsoft.com/visualstudio/ide/mcp-servers)

## Publicar en NuGet.org

```bash
dotnet pack -c Release
dotnet nuget push bin/Release/*.nupkg --api-key <tu-api-key> --source https://api.nuget.org/v3/index.json
```

## Usar el servidor publicado en NuGet.org

Una vez publicado, se configura con el comando `dnx` (VS Code: `.vscode/mcp.json`; Visual Studio: `.mcp.json`):

```json
{
  "servers": {
    "IAdetector": {
      "type": "stdio",
      "command": "dnx",
      "args": ["IAdetector.Mcp", "--version", "1.0.1", "--yes"]
    }
  }
}
```

## Más información

Este servidor usa el SDK oficial [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) para C#.

- [Documentación oficial de MCP](https://modelcontextprotocol.io/)
- [Especificación del protocolo](https://spec.modelcontextprotocol.io/)
- [MCP C# SDK](https://modelcontextprotocol.github.io/csharp-sdk)

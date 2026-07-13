using IAdetector.Infrastructure.DependencyInjection;
using IAdetector.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Todos los logs van a stderr: stdout está reservado para los mensajes del protocolo MCP.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddIAdetectorInfrastructure();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    // Registro explícito (no WithToolsFromAssembly): el escaneo por reflexión no es compatible
    // con la publicación Native AOT prevista para la Fase 6.
    .WithTools<DetectionTools>();

await builder.Build().RunAsync();

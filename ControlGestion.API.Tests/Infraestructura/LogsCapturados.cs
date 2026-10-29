using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ControlGestion.API.Tests.Infraestructura;

/// <summary>
/// Guarda todo lo que la API escribe en el log, desde Trace, con sus scopes y excepciones: para probar que un secreto no
/// aparece en ningún mensaje.
/// </summary>
public sealed class LogsCapturados : ILoggerProvider
{
    public ConcurrentQueue<string> Lineas { get; } = new();

    public ILogger CreateLogger(string categoria) => new Registro(this, categoria);

    public void Dispose() { }

    private sealed class Registro(LogsCapturados logs, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState estado) where TState : notnull
        {
            logs.Lineas.Enqueue($"Scope {categoria}: {estado}");
            return null;
        }

        public bool IsEnabled(LogLevel nivel) => true;

        public void Log<TState>(LogLevel nivel, EventId evento, TState estado, Exception? excepcion,
            Func<TState, Exception?, string> formato) =>
            logs.Lineas.Enqueue($"{nivel} {categoria}: {formato(estado, excepcion)} {excepcion}");
    }
}

using System.Text;

namespace RegistroTrabajo.Api.Infrastructure;

/// <summary>
/// Guarda en un archivo diario (<c>errores-AAAA-MM-DD.log</c>) todo lo que la aplicación registre como Error o Crítico, con su excepción completa.
/// Complementa la tabla ErroresTecnicos: sigue funcionando aunque la base de datos falle. Nunca interrumpe la aplicación si no puede escribir.
/// </summary>
public sealed class ArchivoErroresLoggerProvider : ILoggerProvider
{
    private readonly string _carpeta;
    private readonly int _diasConservar;
    private readonly object _bloqueo = new();

    public ArchivoErroresLoggerProvider(string carpeta, int diasConservar = 30)
    {
        _carpeta = carpeta;
        _diasConservar = diasConservar;
        Limpiar();
    }

    public ILogger CreateLogger(string categoryName) => new ArchivoErroresLogger(this, categoryName);

    public void Dispose() { }

    internal void Escribir(string texto)
    {
        try
        {
            lock (_bloqueo)
            {
                Directory.CreateDirectory(_carpeta);
                var archivo = Path.Combine(_carpeta, $"errores-{DateTime.UtcNow:yyyy-MM-dd}.log");
                File.AppendAllText(archivo, texto, Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Sin permiso de escritura, disco lleno, etc.: registrar el error no debe causar otro error.
        }
    }

    private void Limpiar()
    {
        try
        {
            if (!Directory.Exists(_carpeta)) return;
            var limite = DateTime.UtcNow.AddDays(-_diasConservar);
            foreach (var f in Directory.EnumerateFiles(_carpeta, "errores-*.log"))
                if (File.GetLastWriteTimeUtc(f) < limite) File.Delete(f);
        }
        catch (Exception) { }
    }
}

internal sealed class ArchivoErroresLogger(ArchivoErroresLoggerProvider proveedor, string categoria) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var sb = new StringBuilder();
        sb.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss'Z'")).Append(" [").Append(logLevel == LogLevel.Critical ? "CRITICO" : "ERROR").Append("] ")
          .Append(categoria).Append(" | ").AppendLine(formatter(state, exception));
        if (exception is not null) sb.AppendLine(exception.ToString());
        sb.AppendLine();
        proveedor.Escribir(sb.ToString());
    }
}

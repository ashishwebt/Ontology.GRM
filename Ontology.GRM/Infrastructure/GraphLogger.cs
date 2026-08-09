namespace Ontology.GRM.Infrastructure;

/// <summary>Captures diagnostic events raised by a <see cref="GraphContext"/>.</summary>
public sealed class GraphLogger
{
    private readonly List<GraphLogEntry> _entries = [];

    public event EventHandler<GraphLogEntry>? EntryWritten;

    public IReadOnlyList<GraphLogEntry> Entries => _entries.AsReadOnly();

    public void Log(GraphLogLevel level, string message, Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var entry = new GraphLogEntry(DateTimeOffset.UtcNow, level, message, exception);
        _entries.Add(entry);
        EntryWritten?.Invoke(this, entry);
    }
}

public enum GraphLogLevel
{
    Information,
    Error
}

public sealed record GraphLogEntry(
    DateTimeOffset Timestamp,
    GraphLogLevel Level,
    string Message,
    Exception? Exception);

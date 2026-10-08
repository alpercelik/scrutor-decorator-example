namespace ScrutorExample.Core.Services;

/// <summary>
/// Artificial delay applied by <see cref="ProductService"/> so that caching and
/// logging decorators produce visible effects when the samples run.
/// </summary>
/// <param name="Duration">How long each "database" call should take.</param>
public sealed record SimulatedLatency(TimeSpan Duration)
{
    /// <summary>The default latency used by the samples: 250 ms.</summary>
    public static readonly SimulatedLatency Default = new(TimeSpan.FromMilliseconds(250));
}

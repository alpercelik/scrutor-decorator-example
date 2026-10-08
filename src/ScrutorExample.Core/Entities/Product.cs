namespace ScrutorExample.Core.Entities;

/// <summary>
/// A product in the (fake) catalogue.
/// </summary>
/// <param name="Id">Stable identifier of the product.</param>
/// <param name="Sku">Human readable stock keeping unit code.</param>
/// <param name="Name">Display name.</param>
/// <param name="Price">Unit price in EUR.</param>
public sealed record Product(Guid Id, string Sku, string Name, decimal Price)
{
    /// <summary>
    /// Timestamp of the last time the object was materialised from the "database".
    /// Handy to prove that a cached response did <b>not</b> hit the underlying service.
    /// </summary>
    public DateTimeOffset RetrievedAt { get; init; } = DateTimeOffset.UtcNow;
}

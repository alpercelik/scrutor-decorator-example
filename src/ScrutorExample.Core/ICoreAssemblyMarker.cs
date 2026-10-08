namespace ScrutorExample.Core;

/// <summary>
/// Marker type used to point Scrutor at the assembly that should be scanned.
/// </summary>
/// <remarks>
/// Referencing a concrete type is more robust than <c>Assembly.GetExecutingAssembly()</c>
/// or a magic assembly-name string: if the assembly is renamed, this still compiles.
/// </remarks>
public interface ICoreAssemblyMarker;

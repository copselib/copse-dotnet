using System.Runtime.CompilerServices;

namespace Copse.Core
{
  /// <summary>Helpers for reading <see cref="NodeTraversalStrategies"/> flags.</summary>
  public static class NodeTraversalStrategiesExtensions
  {
    /// <summary>True when every flag in <paramref name="strategies"/> is set on
    /// <paramref name="nodeTraversalStrategies"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasNodeTraversalStrategies(
      this NodeTraversalStrategies nodeTraversalStrategies,
      NodeTraversalStrategies strategies)
    {
      return (nodeTraversalStrategies & strategies) == strategies;
    }
  }
}

using System.Runtime.CompilerServices;

namespace Copse.Core
{
  public static class NodeTraversalStrategiesExtensions
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasNodeTraversalStrategies(
      this NodeTraversalStrategies nodeTraversalStrategies,
      NodeTraversalStrategies strategies)
    {
      return (nodeTraversalStrategies & strategies) == strategies;
    }
  }
}

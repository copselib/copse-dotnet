using System;

namespace Copse.SimpleSerializer
{
  // A dedicated delegate is required because Func<...> cannot have a ref-struct
  // (ReadOnlySpan<char>) parameter.
  /// <summary>
  /// Maps a serialized value, handed as a slice of the source text, to a node. Parsing straight
  /// off the span (e.g. <c>chars => int.Parse(chars)</c>) allocates no intermediate string.
  /// </summary>
  public delegate TNode SpanMap<TNode>(ReadOnlySpan<char> chars);
}

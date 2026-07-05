using System;

namespace Copse.SimpleSerializer
{
  // A map from a slice of the source text to a value. Unlike Func<string, TValue> it receives a
  // ReadOnlySpan<char>, so a caller can parse straight off the source (e.g. int.Parse(chars)) without
  // ever materializing an intermediate string. A dedicated delegate is required because Func<...>
  // cannot have a ref-struct (ReadOnlySpan<char>) parameter.
  public delegate TValue SpanMap<TValue>(ReadOnlySpan<char> chars);
}

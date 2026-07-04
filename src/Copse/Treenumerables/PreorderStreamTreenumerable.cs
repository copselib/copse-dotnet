using Copse.Core;
using Copse.Treenumerators;
using System;

namespace Copse.Treenumerables
{
  /// <summary>
  /// A tree streaming from a forward-only preorder source: the streaming tier of
  /// <see cref="PreorderTreenumerable{TValue, TStore}"/>, and deliberately only an
  /// <see cref="IDepthFirstTreenumerable{TValue}"/> -- a one-pass source cannot affordably serve
  /// the breadth-first dimension, so under the traversal-dimension split that request does not
  /// typecheck (escalate explicitly via Memoize/Materialize). See TRAVERSAL_DIMENSION_SPLIT.md.
  ///
  /// <para>Each treenumerator acquisition invokes the factory for a fresh stream and OWNS it
  /// (disposal closes it): re-enumeration re-reads the source -- the standard lazy contract. A
  /// single-shot source is a factory that throws on its second invocation.</para>
  /// </summary>
  public sealed class PreorderStreamTreenumerable<TValue, TStream> : IDepthFirstTreenumerable<TValue>
    where TStream : IPreorderStream<TValue>
  {
    public PreorderStreamTreenumerable(Func<TStream> streamFactory)
    {
      _StreamFactory = streamFactory;
    }

    private readonly Func<TStream> _StreamFactory;

    public ITreenumerator<TValue> GetDepthFirstTreenumerator()
      => new PreorderStreamDepthFirstTreenumerator<TValue, TStream>(_StreamFactory());
  }
}

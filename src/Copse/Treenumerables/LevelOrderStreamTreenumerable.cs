using Copse.Core;
using Copse.Treenumerators;
using System;

namespace Copse.Treenumerables
{
  /// <summary>
  /// A tree streaming from a forward-only level-order source: the streaming tier of
  /// <see cref="LevelOrderTreenumerable{TValue, TStore}"/>, and deliberately only an
  /// <see cref="IBreadthFirstTreenumerable{TValue}"/> -- a one-pass level-order source has no
  /// bounded strategy for the depth-first dimension at all, so under the traversal-dimension
  /// split that request does not typecheck (escalate explicitly via Memoize/Materialize).
  /// See TRAVERSAL_DIMENSION_SPLIT.md.
  ///
  /// <para>Each treenumerator acquisition invokes the factory for a fresh stream and OWNS it
  /// (disposal closes it): re-enumeration re-reads the source -- the standard lazy contract. A
  /// single-shot source is a factory that throws on its second invocation.</para>
  /// </summary>
  public sealed class LevelOrderStreamTreenumerable<TValue, TStream> : IBreadthFirstTreenumerable<TValue>
    where TStream : ILevelOrderStream<TValue>
  {
    public LevelOrderStreamTreenumerable(Func<TStream> streamFactory)
    {
      _StreamFactory = streamFactory;
    }

    private readonly Func<TStream> _StreamFactory;

    public ITreenumerator<TValue> GetBreadthFirstTreenumerator()
      => new LevelOrderStreamBreadthFirstTreenumerator<TValue, TStream>(_StreamFactory());
  }
}

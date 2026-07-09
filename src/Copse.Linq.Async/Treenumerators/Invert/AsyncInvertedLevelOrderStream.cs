using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerators
{
  // The mirror as a stream transform: an ILevelOrderStream whose groups are the source's
  // level-order child groups with each LEVEL's families reversed and each family's ITEMS
  // reversed -- which is exactly the mirrored tree's level-order groups encoding (reversing
  // every sibling group reverses each level end-to-end). The flat family's breadth-first
  // playback does all the visit-contract work; this class only reorders.
  //
  // O(width): one level tier is buffered at a time (a tier must complete before its LAST
  // family -- the mirror's first -- can be served), in two REUSED flat buffers (values in
  // forward order + each family's end offset), so steady-state traversal allocates nothing
  // per node. Families are cut from the inner breadth-first visit stream by the contract's
  // parenthood encoding: a front's children are scheduled between its own visits, so each
  // first visit closes the previous front's family, and a first visit one level deeper seals
  // the tier.
  //
  // Consumer skips on the mirror discard from this buffer, not from the source: the inner
  // treenumerator is driven TraverseAll a tier at a time (the eager-skip price memo replays
  // also accept). Owns the inner treenumerator; disposing the stream disposes it.
  internal sealed class AsyncInvertedLevelOrderStream<TValue> : IAsyncLevelOrderStream<TValue>
  {
    public AsyncInvertedLevelOrderStream(IAsyncTreenumerator<TValue> inner)
    {
      _Inner = inner;
    }

    private readonly IAsyncTreenumerator<TValue> _Inner;

    // The reused tier buffers: values of the tier's families in forward order, plus each
    // family's END offset into them (family f spans [ends[f-1], ends[f])).
    private readonly List<TValue> _TierValues = new List<TValue>();
    private readonly List<int> _TierFamilyEnds = new List<int>();

    // Serving cursors: family ordinal counted from the BACK (mirror order), items served from
    // the back of the family.
    private bool _TierInstalled;
    private int _Group;
    private int _Item;

    // Collector state.
    private bool _RootsCollected;
    private bool _InnerExhausted;
    private int _CollectorLevelDepth;

    public async ValueTask<LevelOrderRead<TValue>> TryReadNextInGroupAsync()
    {
      if (!_TierInstalled && !await TryCollectNextTierAsync().ConfigureAwait(false))
        return default;

      if (_Group >= _TierFamilyEnds.Count)
        return default;

      var forwardIndex = _TierFamilyEnds.Count - 1 - _Group;
      var end = _TierFamilyEnds[forwardIndex];
      var start = forwardIndex == 0 ? 0 : _TierFamilyEnds[forwardIndex - 1];

      if (_Item >= end - start)
        return default;

      var value = _TierValues[end - 1 - _Item];
      _Item++;

      return new LevelOrderRead<TValue>(value);
    }

    public ValueTask<int> SkipGroupRemainderAsync()
    {
      if (!_TierInstalled || _Group >= _TierFamilyEnds.Count)
        return new ValueTask<int>(0);

      var forwardIndex = _TierFamilyEnds.Count - 1 - _Group;
      var end = _TierFamilyEnds[forwardIndex];
      var start = forwardIndex == 0 ? 0 : _TierFamilyEnds[forwardIndex - 1];

      var remaining = end - start - _Item;
      _Item = end - start;

      return new ValueTask<int>(remaining);
    }

    public async ValueTask<bool> TryMoveToNextGroupAsync()
    {
      if (!_TierInstalled && !await TryCollectNextTierAsync().ConfigureAwait(false))
        return false;

      _Item = 0;
      _Group++;

      if (_Group < _TierFamilyEnds.Count)
        return true;

      return await TryCollectNextTierAsync().ConfigureAwait(false);
    }

    // Pull the next tier of families from the inner stream into the reused buffers (group 0's
    // tier is the roots). False when no further tier will ever have a group.
    private async ValueTask<bool> TryCollectNextTierAsync()
    {
      if (_RootsCollected && _InnerExhausted)
        return false;

      _TierValues.Clear();
      _TierFamilyEnds.Clear();
      _Group = 0;
      _Item = 0;

      if (!_RootsCollected)
      {
        _RootsCollected = true;

        while (await _Inner.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (_Inner.Mode == TreenumeratorMode.SchedulingNode)
          {
            _TierValues.Add(_Inner.Node);
          }
          else if (_Inner.VisitCount == 1)
          {
            // The first front: the roots tier is sealed; that front's own family opens
            // implicitly as the next tier's first.
            _CollectorLevelDepth = _Inner.Position.Depth;
            _TierFamilyEnds.Add(_TierValues.Count);
            _TierInstalled = true;
            return true;
          }
        }

        _InnerExhausted = true;
        _TierFamilyEnds.Add(_TierValues.Count);
        _TierInstalled = true;
        return true;
      }

      while (await _Inner.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
      {
        if (_Inner.Mode == TreenumeratorMode.SchedulingNode)
        {
          _TierValues.Add(_Inner.Node);
        }
        else if (_Inner.VisitCount == 1)
        {
          // Close the previous front's family; a deeper front additionally seals the tier
          // (its own family carries into the next collection, implicitly).
          _TierFamilyEnds.Add(_TierValues.Count);

          if (_Inner.Position.Depth != _CollectorLevelDepth)
          {
            _CollectorLevelDepth = _Inner.Position.Depth;
            return true;
          }
        }
      }

      _InnerExhausted = true;
      _TierFamilyEnds.Add(_TierValues.Count); // the last front's family
      return true;
    }

    public ValueTask DisposeAsync() => _Inner.DisposeAsync();
  }
}

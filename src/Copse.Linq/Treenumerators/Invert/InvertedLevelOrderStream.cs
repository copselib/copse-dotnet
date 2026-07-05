using Copse.Core;
using System.Collections.Generic;

namespace Copse.Linq.Treenumerators
{
  // The mirror as a stream transform: an ILevelOrderStream whose groups are the source's
  // level-order child groups with each LEVEL's families reversed and each family's ITEMS
  // reversed -- which is exactly the mirrored tree's level-order groups encoding (reversing
  // every sibling group reverses each level end-to-end). The flat family's breadth-first
  // playback does all the visit-contract work; this class only reorders.
  //
  // O(width): one level tier of families is buffered at a time (a tier must complete before
  // its LAST family -- the mirror's first -- can be served). Families are cut from the inner
  // breadth-first visit stream by the contract's parenthood encoding: a front's children are
  // scheduled between its own visits, so each first visit closes the previous front's family
  // and opens its own, and a first visit one level deeper seals the tier.
  //
  // Consumer skips on the mirror discard from this buffer, not from the source: the inner
  // treenumerator is driven TraverseAll a tier at a time (the eager-skip price memo replays
  // also accept). Owns the inner treenumerator; disposing the stream disposes it.
  internal sealed class InvertedLevelOrderStream<TValue> : ILevelOrderStream<TValue>
  {
    public InvertedLevelOrderStream(ITreenumerator<TValue> inner)
    {
      _Inner = inner;
    }

    private readonly ITreenumerator<TValue> _Inner;

    // Serving state: the current tier, served family-reversed and item-reversed.
    private List<List<TValue>> _Tier;
    private int _Group; // ordinal within the tier, counted from the BACK (mirror order)
    private int _Item;  // items served from the current group, counted from the back

    // Collector state (always one family ahead: a tier is sealed by the next tier's first
    // front, whose family must carry over).
    private bool _RootsCollected;
    private bool _InnerExhausted;
    private int _CollectorLevelDepth;
    private List<List<TValue>> _PendingTier = new List<List<TValue>>();
    private List<TValue> _OpenFamily = new List<TValue>();

    private List<TValue> CurrentGroup()
      => _Group < _Tier.Count ? _Tier[_Tier.Count - 1 - _Group] : null;

    public bool TryReadNextInGroup(out TValue value)
    {
      if (_Tier == null && !TryCollectNextTier())
      {
        value = default;
        return false;
      }

      var group = CurrentGroup();

      if (group == null || _Item >= group.Count)
      {
        value = default;
        return false;
      }

      value = group[group.Count - 1 - _Item];
      _Item++;
      return true;
    }

    public int SkipGroupRemainder()
    {
      if (_Tier == null && !TryCollectNextTier())
        return 0;

      var group = CurrentGroup();

      if (group == null)
        return 0;

      var remaining = group.Count - _Item;
      _Item = group.Count;
      return remaining;
    }

    public bool TryMoveToNextGroup()
    {
      if (_Tier == null && !TryCollectNextTier())
        return false;

      _Item = 0;
      _Group++;

      if (_Group < _Tier.Count)
        return true;

      return TryCollectNextTier();
    }

    // Pull the next tier of families from the inner stream (group 0's tier is the roots).
    // False when no further tier will ever have a group.
    private bool TryCollectNextTier()
    {
      if (!_RootsCollected)
      {
        _RootsCollected = true;

        var roots = new List<TValue>();

        while (_Inner.MoveNext(NodeTraversalStrategies.TraverseAll))
        {
          if (_Inner.Mode == TreenumeratorMode.SchedulingNode)
          {
            roots.Add(_Inner.Node);
          }
          else if (_Inner.VisitCount == 1)
          {
            // The first front: the roots tier is sealed and this front's family opens.
            _CollectorLevelDepth = _Inner.Position.Depth;
            InstallTier(new List<List<TValue>> { roots });
            return true;
          }
        }

        _InnerExhausted = true;
        InstallTier(new List<List<TValue>> { roots });
        return true;
      }

      if (_InnerExhausted)
        return false;

      while (_Inner.MoveNext(NodeTraversalStrategies.TraverseAll))
      {
        if (_Inner.Mode == TreenumeratorMode.SchedulingNode)
        {
          _OpenFamily.Add(_Inner.Node);
        }
        else if (_Inner.VisitCount == 1)
        {
          _PendingTier.Add(_OpenFamily);
          _OpenFamily = new List<TValue>();

          if (_Inner.Position.Depth != _CollectorLevelDepth)
          {
            // A front one level deeper: the tier is sealed, and this front's family (the one
            // just opened) carries into the next collection.
            _CollectorLevelDepth = _Inner.Position.Depth;
            InstallCollectedTier();
            return true;
          }
        }
      }

      _InnerExhausted = true;
      _PendingTier.Add(_OpenFamily);
      _OpenFamily = new List<TValue>();
      InstallCollectedTier();
      return true;
    }

    private void InstallCollectedTier()
    {
      InstallTier(_PendingTier);
      _PendingTier = new List<List<TValue>>();
    }

    private void InstallTier(List<List<TValue>> tier)
    {
      _Tier = tier;
      _Group = 0;
      _Item = 0;
    }

    public void Dispose() => _Inner.Dispose();
  }
}

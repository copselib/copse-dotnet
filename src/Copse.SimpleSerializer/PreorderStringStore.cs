using System;

namespace Copse.SimpleSerializer
{
  // A depth-first-serialized tree ("a(b(d,e),c)") as a LAZILY-PARSED preorder store: the
  // incremental version of the old eager parse (a value followed by '(' is a parent, backfilled
  // at its matching ')'; ',' separates siblings), pulled one committed value or one subtree
  // close at a time, exactly as far as some traversal's frontier demands. Retiring the eager
  // parse was the point of the whole serialization redesign: composing costs nothing, early-out
  // never touches the rest of the string, and the value map runs once per node ever reached.
  //
  // Values ride the shared value-token layer (ValueTokenStringScanner): quoted values may
  // contain ANY character, unquoted trailing line endings at end of input are ignored (files
  // end in newlines), and the zero-copy span mapping survives for every token that is a
  // contiguous slice of the source.
  //
  // The string is its own random-access character buffer, so the store affords BOTH dimensions
  // (full ITreenumerable citizenship via PreorderTreenumerable); parsed values and spans are
  // retained as they materialize -- the same growing-capture shape as the memo's DFT buffer,
  // with subtreeSizes[i] == 0 meaning node i's subtree is still OPEN. One store is shared by
  // every treenumerator of the same Deserialize result: parse once, replay many. Single-threaded
  // by contract, like every treenumerator in the library.
  //
  // Detection replaces the retired layout header (see TRAVERSAL_DIMENSION_SPLIT.md): the caller
  // states the layout by choosing DeserializeDepthFirstTree, and an UNQUOTED level-order
  // structural character ('|' or ';') proves the string is the wrong layout (or corrupt) -- a
  // FormatException rather than a silently mis-parsed tree.
  internal sealed class PreorderStringStore<TValue> : IPreorderStore<TValue>
  {
    public PreorderStringStore(string tree, SpanMap<TValue> map)
    {
      _Scanner = new ValueTokenStringScanner(tree);
      _Map = map;
    }

    private readonly ValueTokenStringScanner _Scanner;
    private readonly SpanMap<TValue> _Map;

    private readonly RefAppendOnlyList<TValue> _Values = new RefAppendOnlyList<TValue>();
    private readonly RefAppendOnlyList<int> _SubtreeSizes = new RefAppendOnlyList<int>();
    private readonly RefSemiDeque<int> _Open = new RefSemiDeque<int>(); // indices of parents whose ')' hasn't been parsed yet

    private bool _Exhausted;

    public bool EnsureBuffered(int index)
    {
      while (!_Exhausted && _Values.Count <= index)
        ParseStep();

      return index < _Values.Count;
    }

    public int EnsureSubtreeClosed(int index)
    {
      while (!_Exhausted && _SubtreeSizes[index] == 0)
        ParseStep();

      return _SubtreeSizes[index];
    }

    public int GetSubtreeSize(int index) => _SubtreeSizes[index];

    public TValue GetValue(int index) => _Values[index];

    // Advance the parse until it makes progress an Ensure loop can observe: a value committed,
    // a subtree closed, or the string exhausted (which closes everything still open).
    private void ParseStep()
    {
      while (_Scanner.TryScanEvent(out var hasValue, out var terminator))
      {
        switch (terminator)
        {
          case '(':
            if (hasValue)
            {
              Commit(asParent: true);
              return;
            }

            break;

          case ',':
            if (hasValue)
            {
              Commit(asParent: false);
              return;
            }

            break;

          case ')':
          {
            if (hasValue)
              Commit(asParent: false);

            var closed = _Open.RemoveLast();
            _SubtreeSizes[closed] = _Values.Count - closed;

            return;
          }

          case '|':
          case ';':
            throw new FormatException(
              $"Unexpected '{terminator}' near index {_Scanner.Position}: this is a level-order structural " +
              "character, so the string is not a depth-first-serialized tree (use DeserializeBreadthFirstTree).");

          default: // end of text
            if (hasValue)
              Commit(asParent: false);

            while (_Open.Count > 0)
            {
              var closed = _Open.RemoveLast();
              _SubtreeSizes[closed] = _Values.Count - closed;
            }

            _Exhausted = true;
            return;
        }
      }

      _Exhausted = true;
    }

    private void Commit(bool asParent)
    {
      var index = _Values.Count;

      _Values.AddLast(_Map(_Scanner.ValueChars));
      _SubtreeSizes.AddLast(asParent ? 0 : 1); // a parent's size is backfilled when it closes

      if (asParent)
        _Open.AddLast(index);
    }

    // The unboxed handle the playback treenumerators are instantiated over: a struct type
    // argument specializes the generic, so the store calls inline instead of interface-
    // dispatching (the same pattern as the memo's store adapters).
    internal readonly struct Handle : IPreorderStore<TValue>
    {
      public Handle(PreorderStringStore<TValue> store)
      {
        _Store = store;
      }

      private readonly PreorderStringStore<TValue> _Store;

      public bool EnsureBuffered(int index) => _Store.EnsureBuffered(index);
      public int EnsureSubtreeClosed(int index) => _Store.EnsureSubtreeClosed(index);
      public int GetSubtreeSize(int index) => _Store.GetSubtreeSize(index);
      public TValue GetValue(int index) => _Store.GetValue(index);
    }
  }
}

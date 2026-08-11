namespace Copse
{
  /// <summary>
  /// The result of a parent pull: whether the handle has a parent and, if so, which one. The upward
  /// twin of <see cref="ChildResult{TNode}"/>, sharing its rationale: a named result struct (not a
  /// tuple) returned BY VALUE -- it stores nothing and uses no <c>out</c> param, so the shape stays
  /// legal for a future async twin's await-strip transcription. When <see cref="HasParent"/> is
  /// false the node is a root, <see cref="Parent"/> is <c>default</c>, and must not be read.
  ///
  /// <para>Unlike <see cref="ChildResult{TNode}"/> this carries a bare handle, not a
  /// <see cref="NodeAndSiblingIndex{THandle}"/>: a child pull knows the sibling index it is handing
  /// out, but a general adjacency source cannot know its parent's sibling index without consulting
  /// the grandparent. (PoC decision, WALKER_DESIGN.md -- a flat store could afford it; the
  /// hierarchical family cannot.)</para>
  /// </summary>
  public readonly struct ParentResult<THandle>
  {
    public ParentResult(THandle parent)
    {
      HasParent = true;
      Parent = parent;
    }

    public readonly bool HasParent;
    public readonly THandle Parent;
  }
}

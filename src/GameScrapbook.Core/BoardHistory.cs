namespace GameScrapbook.Core;

/// <summary>
/// Bounded, image-free snapshots. PNG files remain immutable so delete/undo and
/// the previous manifest can continue referring to them. Undo changes snippets,
/// including archive state, not preferences or the user's current window position.
/// </summary>
public sealed class BoardHistory(int capacity = 100)
{
    private readonly int _capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    private readonly List<List<SnipRecord>> _undo = [];
    private readonly List<List<SnipRecord>> _redo = [];

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Record(BoardDocument beforeMutation)
    {
        ArgumentNullException.ThrowIfNull(beforeMutation);
        Push(_undo, CloneSnips(beforeMutation.Snips));
        _redo.Clear();
    }

    public BoardDocument? Undo(BoardDocument current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (!CanUndo) return null;
        Push(_redo, CloneSnips(current.Snips));
        return WithSnips(current, Pop(_undo));
    }

    public BoardDocument? Redo(BoardDocument current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (!CanRedo) return null;
        Push(_undo, CloneSnips(current.Snips));
        return WithSnips(current, Pop(_redo));
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private void Push(List<List<SnipRecord>> stack, List<SnipRecord> snips)
    {
        stack.Add(snips);
        if (stack.Count > _capacity) stack.RemoveAt(0);
    }

    private static List<SnipRecord> Pop(List<List<SnipRecord>> stack)
    {
        List<SnipRecord> result = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return result;
    }

    private static BoardDocument WithSnips(BoardDocument current, List<SnipRecord> snips)
    {
        BoardDocument result = current.DeepClone();
        result.Snips = CloneSnips(snips);
        return result;
    }

    private static List<SnipRecord> CloneSnips(IEnumerable<SnipRecord> snips) => snips.Select(snip => snip.Clone()).ToList();
}

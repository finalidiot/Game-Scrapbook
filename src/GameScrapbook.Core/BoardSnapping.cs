namespace GameScrapbook.Core;

/// <summary>Caller-selected snap bounds in board units; these may include card chrome.</summary>
public readonly record struct BoardSnapRect(string Id, double X, double Y, double Width, double Height, bool IsArchived = false);

/// <summary>Guide coordinates identify the exact target edges used for each snap.</summary>
public readonly record struct BoardSnapResult(double X, double Y, bool SnappedX, bool SnappedY,
    double? VerticalGuide = null, double? HorizontalGuide = null);

public static class BoardSnapping
{
    /// <summary>
    /// Join or align nearby edges without modifying any input. The caller converts a
    /// screen-space threshold to board units, keeping the apparent snap distance steady
    /// at different zoom levels. Targets must also be close on the perpendicular axis,
    /// so a card elsewhere on the board does not attract the moving card.
    /// </summary>
    public static BoardSnapResult Snap(BoardSnapRect moving, IEnumerable<BoardSnapRect> targets,
        double tolerance, double maximumPosition = 90_000)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (!IsValid(moving)) throw new ArgumentException("Moving bounds must have an identifier and finite, positive dimensions.", nameof(moving));
        if (!double.IsFinite(tolerance) || tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (!double.IsFinite(maximumPosition) || maximumPosition < 0) throw new ArgumentOutOfRangeException(nameof(maximumPosition));

        double x = Math.Clamp(moving.X, 0, maximumPosition);
        double y = Math.Clamp(moving.Y, 0, maximumPosition);
        if (moving.IsArchived) return new BoardSnapResult(x, y, false, false);
        Candidate? bestX = null, bestY = null;

        foreach (BoardSnapRect target in targets)
        {
            if (target.IsArchived || string.Equals(target.Id, moving.Id, StringComparison.Ordinal) || !IsValid(target)) continue;
            double right = target.X + target.Width, bottom = target.Y + target.Height;
            if (Gap(y, y + moving.Height, target.Y, bottom) <= tolerance)
            {
                // Adjacent edges win exact distance ties over same-edge alignment.
                Consider(ref bestX, target.X - moving.Width, target.X, 0, target.Id, x);
                Consider(ref bestX, right, right, 0, target.Id, x);
                Consider(ref bestX, target.X, target.X, 1, target.Id, x);
                Consider(ref bestX, right - moving.Width, right, 1, target.Id, x);
            }
            if (Gap(x, x + moving.Width, target.X, right) <= tolerance)
            {
                Consider(ref bestY, target.Y - moving.Height, target.Y, 0, target.Id, y);
                Consider(ref bestY, bottom, bottom, 0, target.Id, y);
                Consider(ref bestY, target.Y, target.Y, 1, target.Id, y);
                Consider(ref bestY, bottom - moving.Height, bottom, 1, target.Id, y);
            }
        }

        return new BoardSnapResult(bestX?.Position ?? x, bestY?.Position ?? y,
            bestX.HasValue, bestY.HasValue, bestX?.Guide, bestY?.Guide);

        void Consider(ref Candidate? best, double position, double guide, int kind, string targetId, double proposed)
        {
            if (!double.IsFinite(position) || position < 0 || position > maximumPosition) return;
            double distance = Math.Abs(position - proposed);
            if (distance > tolerance) return;
            var candidate = new Candidate(position, guide, distance, kind, targetId);
            if (best is not Candidate previous || Compare(candidate, previous) < 0) best = candidate;
        }
    }

    private static bool IsValid(BoardSnapRect bounds) => !string.IsNullOrWhiteSpace(bounds.Id)
        && double.IsFinite(bounds.X) && double.IsFinite(bounds.Y)
        && double.IsFinite(bounds.Width) && bounds.Width > 0
        && double.IsFinite(bounds.Height) && bounds.Height > 0
        && double.IsFinite(bounds.X + bounds.Width) && double.IsFinite(bounds.Y + bounds.Height);

    private static double Gap(double start, double end, double otherStart, double otherEnd) =>
        Math.Max(0, Math.Max(otherStart - end, start - otherEnd));

    private static int Compare(Candidate first, Candidate second)
    {
        int comparison = first.Distance.CompareTo(second.Distance);
        if (comparison != 0) return comparison;
        comparison = first.Kind.CompareTo(second.Kind);
        if (comparison != 0) return comparison;
        comparison = string.CompareOrdinal(first.TargetId, second.TargetId);
        if (comparison != 0) return comparison;
        comparison = first.Position.CompareTo(second.Position);
        return comparison != 0 ? comparison : first.Guide.CompareTo(second.Guide);
    }

    private readonly record struct Candidate(double Position, double Guide, double Distance, int Kind, string TargetId);
}

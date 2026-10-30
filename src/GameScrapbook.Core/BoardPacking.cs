namespace GameScrapbook.Core;

/// <summary>Compacts active image rectangles without changing their dimensions.</summary>
public static class BoardPacking
{
    private const double MaximumDimension = 100_000;
    private const double MaximumCoordinate = 1_000_000;
    private const double MaximumPlacementPosition = 90_000;

    /// <summary>
    /// Place active snips in their existing top-to-bottom/left-to-right reading order,
    /// using a deterministic skyline layout anchored at the origin. Every later image
    /// touches an earlier image. Archived records and the input enumeration are untouched;
    /// returned records retain the original enumeration order for straightforward ID updates.
    /// </summary>
    public static IReadOnlyList<BoardSnapRect> Magnetise(IEnumerable<BoardSnapRect> rectangles)
    {
        ArgumentNullException.ThrowIfNull(rectangles);
        BoardSnapRect[] all = rectangles.Take(BoardValidation.MaximumSnips + 1).ToArray();
        if (all.Length > BoardValidation.MaximumSnips)
            throw new ArgumentException("The board contains too many snips to arrange.", nameof(rectangles));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (BoardSnapRect rectangle in all)
        {
            if (string.IsNullOrWhiteSpace(rectangle.Id) || !ids.Add(rectangle.Id))
                throw new ArgumentException("Snips need nonempty, unique identifiers.", nameof(rectangles));
            if (!rectangle.IsArchived && !IsValid(rectangle))
                throw new ArgumentException("Active snips need finite coordinates and positive dimensions within the board limits.", nameof(rectangles));
        }

        BoardSnapRect[] active = all.Where(rectangle => !rectangle.IsArchived)
            .OrderBy(rectangle => rectangle.Y).ThenBy(rectangle => rectangle.X)
            .ThenBy(rectangle => rectangle.Id, StringComparer.Ordinal).ToArray();
        if (active.Length == 0) return all;

        double area = active.Sum(rectangle => rectangle.Width * rectangle.Height);
        double targetWidth = Math.Max(active.Max(rectangle => rectangle.Width),
            Math.Min(MaximumPlacementPosition, Math.Sqrt(area) * 1.4));
        var skyline = new List<Segment> { new(0, targetWidth, 0) };
        var packed = new Dictionary<string, BoardSnapRect>(StringComparer.Ordinal);
        // At floor level, an image must reach an existing vertical edge rather than
        // sitting below a high bridge without touching the rest of the arrangement.
        var leftEdges = new Dictionary<double, double>();
        var rightEdges = new Dictionary<double, double>();

        foreach (BoardSnapRect rectangle in active)
        {
            int count = skyline.Count;
            var maximum = new int[count];
            int head = 0, tail = 0, endIndex = 0;
            double bestX = 0, bestY = double.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                double x = skyline[index].Left, right = x + rectangle.Width;
                if (x > MaximumPlacementPosition || right > targetWidth) break;
                while (head < tail && maximum[head] < index) head++;
                while (endIndex < count && skyline[endIndex].Left < right)
                {
                    while (head < tail && skyline[maximum[tail - 1]].Height <= skyline[endIndex].Height) tail--;
                    maximum[tail++] = endIndex++;
                }
                double y = skyline[maximum[head]].Height;
                if (y > MaximumPlacementPosition || (y == 0 && packed.Count > 0
                    && !TouchesFloorNeighbour(x, right, rectangle.Height))) continue;
                if (y < bestY || (y == bestY && x < bestX)) { bestX = x; bestY = y; }
            }
            if (!double.IsFinite(bestY))
                throw new InvalidOperationException("The snips are too large to arrange within the board's coordinate limits.");

            double placedRight = bestX + rectangle.Width, top = bestY + rectangle.Height;
            var next = new List<Segment>(count + 2);
            bool inserted = false;
            foreach (Segment segment in skyline)
            {
                if (segment.Right <= bestX || segment.Left >= placedRight) Add(segment);
                else
                {
                    if (segment.Left < bestX) Add(new Segment(segment.Left, bestX, segment.Height));
                    if (!inserted) { Add(new Segment(bestX, placedRight, top)); inserted = true; }
                    if (segment.Right > placedRight) Add(new Segment(placedRight, segment.Right, segment.Height));
                }
            }
            skyline = next;
            packed.Add(rectangle.Id, rectangle with { X = bestX, Y = bestY });
            Remember(leftEdges, bestX, bestY);
            Remember(rightEdges, placedRight, bestY);

            void Add(Segment segment)
            {
                if (segment.Right <= segment.Left) return;
                if (next.Count > 0 && next[^1].Right == segment.Left && next[^1].Height == segment.Height)
                    next[^1] = next[^1] with { Right = segment.Right };
                else next.Add(segment);
            }
        }

        return all.Select(rectangle => packed.TryGetValue(rectangle.Id, out BoardSnapRect replacement) ? replacement : rectangle).ToArray();

        bool TouchesFloorNeighbour(double left, double right, double height) =>
            (rightEdges.TryGetValue(left, out double leftNeighbourY) && leftNeighbourY < height)
            || (leftEdges.TryGetValue(right, out double rightNeighbourY) && rightNeighbourY < height);
    }

    private static bool IsValid(BoardSnapRect rectangle) => double.IsFinite(rectangle.X) && double.IsFinite(rectangle.Y)
        && Math.Abs(rectangle.X) <= MaximumCoordinate && Math.Abs(rectangle.Y) <= MaximumCoordinate
        && double.IsFinite(rectangle.Width) && rectangle.Width > 0 && rectangle.Width <= MaximumDimension
        && double.IsFinite(rectangle.Height) && rectangle.Height > 0 && rectangle.Height <= MaximumDimension;

    private static void Remember(Dictionary<double, double> edges, double x, double y)
    {
        if (!edges.TryGetValue(x, out double previous) || y < previous) edges[x] = y;
    }

    // Candidate maxima are evaluated with a monotone queue in O(k) for k skyline
    // segments. Updating the skyline is O(k), so the full arrangement is O(n²),
    // rather than scanning every segment for every candidate for every image.
    private readonly record struct Segment(double Left, double Right, double Height);
}

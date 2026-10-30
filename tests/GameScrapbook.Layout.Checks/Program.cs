using GameScrapbook.Core;

int passed = 0;
try
{
    Run("nearby snips join at the right edge and align their tops", () =>
    {
        var result = Snap(new("moving", 195, 104, 100, 60), new("target", 100, 100, 100, 80));
        Check(result.X == 200 && result.Y == 100 && result.SnappedX && result.SnappedY, "right join and top alignment");
        Check(result.VerticalGuide == 200 && result.HorizontalGuide == 100, "edge guides");
    });
    Run("nearby snips join at the left edge", () =>
    {
        var result = Snap(new("moving", 15, 100, 90, 60), new("target", 100, 100, 100, 80));
        Check(result.X == 10 && result.VerticalGuide == 100, "left join must account for moving width");
    });
    Run("vertical joins respect image bounds supplied by the caller", () =>
    {
        var result = Snap(new("moving", 100, 178, 100, 80), new("target", 100, 100, 100, 80));
        Check(result.Y == 180 && result.HorizontalGuide == 180, "image edges join without a footer gap");
    });
    Run("nearby snips join above a target", () =>
    {
        var result = Snap(new("moving", 100, 15, 100, 90), new("target", 100, 100, 100, 80));
        Check(result.Y == 10 && result.HorizontalGuide == 100, "top join must account for moving height");
    });
    Run("stacked cards can align their left or right edges", () =>
    {
        var target = new BoardSnapRect("target", 100, 100, 200, 80);
        var left = Snap(new("moving", 104, 184, 80, 60), target);
        var right = Snap(new("moving", 216, 184, 80, 60), target);
        Check(left.X == 100 && left.Y == 180, "left alignment");
        Check(right.X == 220 && right.Y == 180 && right.VerticalGuide == 300, "right alignment");
    });
    Run("side-by-side cards can align bottom edges", () =>
    {
        var result = Snap(new("moving", 204, 116, 80, 60), new("target", 100, 100, 100, 80));
        Check(result.X == 200 && result.Y == 120 && result.HorizontalGuide == 180, "bottom alignment");
    });
    Run("distant cards do not attract an otherwise aligned card", () =>
    {
        var moving = new BoardSnapRect("moving", 195, 500, 100, 60);
        var result = Snap(moving, new("target", 100, 100, 100, 80));
        Check(result.X == moving.X && result.Y == moving.Y && !result.SnappedX && !result.SnappedY, "perpendicular distance must gate attraction");
    });
    Run("self and archived cards do not attract movement", () =>
    {
        var moving = new BoardSnapRect("moving", 195, 104, 100, 60);
        var result = BoardSnapping.Snap(moving,
            [new("moving", 100, 100, 100, 80), new("archived", 100, 100, 100, 80, true)], 10);
        Check(!result.SnappedX && !result.SnappedY && result.X == 195 && result.Y == 104, "self/archive filtered");
    });
    Run("invalid target geometry is ignored without poisoning movement", () =>
    {
        var result = BoardSnapping.Snap(new("moving", 195, 104, 100, 60),
            [new("nan", double.NaN, 100, 100, 80), new("zero", 100, 100, 0, 80),
             new("infinite", 100, 100, double.PositiveInfinity, 80), new("", 100, 100, 100, 80)], 10);
        Check(!result.SnappedX && !result.SnappedY && result.X == 195 && result.Y == 104, "invalid targets ignored");
    });
    Run("invalid moving bounds and thresholds are rejected", () =>
    {
        Expect<ArgumentException>(() => BoardSnapping.Snap(new("moving", double.NaN, 0, 10, 10), [], 10));
        Expect<ArgumentException>(() => BoardSnapping.Snap(new("moving", 0, 0, 0, 10), [], 10));
        Expect<ArgumentOutOfRangeException>(() => BoardSnapping.Snap(new("moving", 0, 0, 10, 10), [], -1));
        Expect<ArgumentOutOfRangeException>(() => BoardSnapping.Snap(new("moving", 0, 0, 10, 10), [], double.NaN));
        Expect<ArgumentOutOfRangeException>(() => BoardSnapping.Snap(new("moving", 0, 0, 10, 10), [], 10, -1));
    });
    Run("snap threshold is inclusive and does not attract beyond it", () =>
    {
        var target = new BoardSnapRect("target", 100, 100, 100, 80);
        var inside = BoardSnapping.Snap(new("moving", 190, 120, 30, 20), [target], 10);
        var outside = BoardSnapping.Snap(new("moving", 189.99, 120, 30, 20), [target], 10);
        Check(inside.X == 200 && inside.SnappedX, "exact threshold snaps");
        Check(outside.X == 189.99 && !outside.SnappedX, "beyond threshold remains free");
    });
    Run("tie resolution is deterministic regardless of target enumeration order", () =>
    {
        var moving = new BoardSnapRect("moving", 204, 120, 100, 20);
        var a = new BoardSnapRect("a", 308, 100, 100, 80);
        var z = new BoardSnapRect("z", 100, 100, 100, 80);
        var first = BoardSnapping.Snap(moving, [z, a], 10);
        var second = BoardSnapping.Snap(moving, [a, z], 10);
        Check(first == second && first.X == 208 && first.VerticalGuide == 308, "ordinal ID resolves equal adjacency distance");
    });
    Run("closest edge wins over the first target in the enumeration", () =>
    {
        var moving = new BoardSnapRect("moving", 204, 120, 100, 20);
        var result = BoardSnapping.Snap(moving,
            [new("first", 100, 100, 100, 80), new("nearer", 306, 100, 100, 80)], 10);
        Check(result.X == 206 && result.VerticalGuide == 306, "nearest edge selected");
    });
    Run("clamping stays finite and inside the draggable board range", () =>
    {
        var low = BoardSnapping.Snap(new("moving", -20, -2, 100, 80), [], 10);
        var high = BoardSnapping.Snap(new("moving", 100_000, 95_000, 100, 80), [], 10);
        Check(low.X == 0 && low.Y == 0 && !low.SnappedX && !low.SnappedY, "lower bounds");
        Check(high.X == 90_000 && high.Y == 90_000, "upper bounds");
    });
    Run("snap candidates outside the allowed position range are excluded", () =>
    {
        var result = BoardSnapping.Snap(new("moving", 3, 100, 100, 20), [new("target", -5, 100, 20, 80)], 10);
        Check(result.X == 3 && !result.SnappedX, "must not clamp an invalid snap into a misleading edge match");
    });
    Run("zero threshold only matches exact edges", () =>
    {
        var target = new BoardSnapRect("target", 100, 100, 100, 80);
        var exact = BoardSnapping.Snap(new("moving", 200, 120, 50, 20), [target], 0);
        var near = BoardSnapping.Snap(new("moving", 200.01, 120, 50, 20), [target], 0);
        Check(exact.SnappedX && exact.X == 200, "exact edge match");
        Check(!near.SnappedX && near.X == 200.01, "nonzero gap with zero tolerance");
    });
    Run("nearby diagonal corners can snap on both axes", () =>
    {
        var result = BoardSnapping.Snap(new("moving", 206, 186, 50, 50), [new("target", 100, 100, 100, 80)], 6);
        Check(result.X == 200 && result.Y == 180 && result.SnappedX && result.SnappedY, "corner proximity enables both axes");
    });
    Run("fractional positions remain exact and inputs are not changed", () =>
    {
        var moving = new BoardSnapRect("moving", 197.5, 100.25, 50, 40);
        var target = new BoardSnapRect("target", 100.125, 100.25, 100.25, 80);
        var result = BoardSnapping.Snap(moving, [target], 3);
        Check(result.X == 200.375 && result.Y == 100.25, "fractional join");
        Check(moving.X == 197.5 && target.Width == 100.25, "inputs remain immutable");
    });
    Run("archived moving cards are not snapped", () =>
    {
        var result = BoardSnapping.Snap(new("moving", 195, 104, 100, 60, true), [new("target", 100, 100, 100, 80)], 10);
        Check(result.X == 195 && result.Y == 104 && !result.SnappedX && !result.SnappedY, "archived source ignored");
    });
    Run("caller-provided zoom tolerance changes attraction without changing geometry", () =>
    {
        var moving = new BoardSnapRect("moving", 193, 120, 30, 20);
        var target = new BoardSnapRect("target", 100, 100, 100, 80);
        var normal = BoardSnapping.Snap(moving, [target], 10);
        var zoomed = BoardSnapping.Snap(moving, [target], 10 / 3d);
        Check(normal.X == 200 && !zoomed.SnappedX && zoomed.X == 193, "screen-space threshold supplied by host");
    });
    Run("magnetise anchors active images and preserves identifiers, dimensions and input order", () =>
    {
        BoardSnapRect[] input = [new("b", 500, 100, 160, 90), new("a", 200, 100, 80, 60), new("c", 0, 500, 120, 80)];
        var result = BoardPacking.Magnetise(input);
        Check(result.Select(rectangle => rectangle.Id).SequenceEqual(input.Select(rectangle => rectangle.Id)), "returned order and identities");
        for (int i = 0; i < input.Length; i++)
            Check(result[i].Width == input[i].Width && result[i].Height == input[i].Height, "dimensions cannot change");
        Check(result.Single(rectangle => rectangle.Id == "a").X == 0 && result.Single(rectangle => rectangle.Id == "a").Y == 0, "reading-order first image anchors at origin");
        Check(input[0].X == 500 && input[1].X == 200, "input positions untouched");
        CheckPacked(result);
    });
    Run("magnetise leaves archived records exactly unchanged", () =>
    {
        var archived = new BoardSnapRect("archive", -80.5, 210.25, 400, 300, true);
        var result = BoardPacking.Magnetise([archived, new("active", 300, 400, 80, 60)]);
        Check(result[0] == archived && result[1].X == 0 && result[1].Y == 0, "archive remains untouched");
    });
    Run("magnetise handles empty, archive-only and single-image boards", () =>
    {
        Check(BoardPacking.Magnetise([]).Count == 0, "empty arrangement");
        var archived = new BoardSnapRect("archive", 99, 88, 40, 30, true);
        Check(BoardPacking.Magnetise([archived]).Single() == archived, "archive-only arrangement");
        var single = BoardPacking.Magnetise([new("one", 900_000, 900_000, 500, 250)]).Single();
        Check(single.X == 0 && single.Y == 0 && single.Width == 500 && single.Height == 250, "far-away image rescued to origin without resizing");
    });
    Run("magnetise is deterministic across input enumeration order", () =>
    {
        BoardSnapRect[] input = [new("c", 200, 200, 160, 50), new("b", 100, 100, 70, 200), new("a", 100, 100, 130, 120)];
        var first = BoardPacking.Magnetise(input).OrderBy(rectangle => rectangle.Id).ToArray();
        var second = BoardPacking.Magnetise(input.Reverse()).OrderBy(rectangle => rectangle.Id).ToArray();
        Check(first.SequenceEqual(second), "same image identities and reading order must produce the same layout");
        CheckPacked(first);
    });
    Run("magnetise packs mixed image sizes without overlap or disconnected images", () =>
    {
        BoardSnapRect[] input = [new("a", 0, 0, 150, 100), new("b", 0, 200, 300, 10),
            new("c", 0, 300, 50, 10), new("d", 0, 400, 20, 200), new("e", 0, 500, 175, 35),
            new("f", 0, 600, 45, 90), new("g", 0, 700, 400, 20)];
        CheckPacked(BoardPacking.Magnetise(input));
    });
    Run("magnetise preserves fractional sizes and does not create intentional spacing", () =>
    {
        var result = BoardPacking.Magnetise([new("a", 10, 20, 100.25, 50.5), new("b", 10, 80, 80.125, 40.75), new("c", 20, 120, 30.25, 55.125)]);
        Check(result[0].Width == 100.25 && result[1].Height == 40.75, "fractional dimensions preserved");
        CheckPacked(result);
    });
    Run("magnetise rejects duplicate identifiers and malformed active geometry", () =>
    {
        Expect<ArgumentException>(() => BoardPacking.Magnetise([new("duplicate", 0, 0, 10, 10), new("duplicate", 20, 20, 10, 10)]));
        Expect<ArgumentException>(() => BoardPacking.Magnetise([new("bad", 0, 0, -1, 10)]));
        Expect<ArgumentException>(() => BoardPacking.Magnetise([new("bad", double.NaN, 0, 10, 10)]));
        Expect<ArgumentException>(() => BoardPacking.Magnetise([new("bad", 0, 0, double.PositiveInfinity, 10)]));
        Expect<ArgumentException>(() => BoardPacking.Magnetise([new("bad", 0, 0, 100_001, 10)]));
        Expect<ArgumentException>(() => BoardPacking.Magnetise([new("bad", 1_000_001, 0, 10, 10)]));
        Expect<ArgumentException>(() => BoardPacking.Magnetise(Enumerable.Range(0, BoardValidation.MaximumSnips + 1)
            .Select(i => new BoardSnapRect(i.ToString(), 0, 0, 10, 10))));
        Expect<InvalidOperationException>(() => BoardPacking.Magnetise(
            [new("large-a", 0, 0, 100_000, 100_000), new("large-b", 500_000, 500_000, 100_000, 100_000)]));
    });
    Run("magnetise remains compact for a board of equally-sized snips", () =>
    {
        var input = Enumerable.Range(0, 100).Select(i => new BoardSnapRect(i.ToString("D3"), i % 10 * 200, i / 10 * 200, 100, 100));
        var result = BoardPacking.Magnetise(input);
        CheckPacked(result);
        Check(result.Max(rectangle => rectangle.X + rectangle.Width) <= 1400.001
            && result.Max(rectangle => rectangle.Y + rectangle.Height) <= 800.001, "roughly rectangular arrangement rather than an unbounded strip");
    });
    Run("magnetise supports the full normal-size board limit", () =>
    {
        var input = Enumerable.Range(0, BoardValidation.MaximumSnips)
            .Select(i => new BoardSnapRect(i.ToString("D5"), i % 100 * 400, i / 100 * 250, 320, 200));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = BoardPacking.Magnetise(input);
        watch.Stop();
        Check(result.Count == BoardValidation.MaximumSnips, "all images retained");
        Check(result.All(rectangle => rectangle.X >= 0 && rectangle.X <= 90_000 && rectangle.Y >= 0 && rectangle.Y <= 90_000), "placement bounds retained");
        Check(result.All(rectangle => rectangle.Width == 320 && rectangle.Height == 200 && rectangle.X % 320 == 0 && rectangle.Y % 200 == 0), "equal-size images occupy exact non-overlapping cells");
        Check(result.Select(rectangle => (rectangle.X, rectangle.Y)).Distinct().Count() == result.Count, "no duplicate cell positions");
        Console.WriteLine($"  Arranged {result.Count:N0} snips in {watch.ElapsedMilliseconds:N0} ms.");
    });
    Console.WriteLine($"PASS: {passed} layout checks.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("FAIL: " + ex);
    return 1;
}

BoardSnapResult Snap(BoardSnapRect moving, BoardSnapRect target) => BoardSnapping.Snap(moving, [target], 10);
void Run(string name, Action check) { check(); passed++; Console.WriteLine("PASS " + name); }
static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void CheckPacked(IReadOnlyList<BoardSnapRect> rectangles)
{
    const double epsilon = 1e-8;
    BoardSnapRect[] active = rectangles.Where(rectangle => !rectangle.IsArchived).ToArray();
    for (int i = 0; i < active.Length; i++)
    {
        Check(active[i].X >= 0 && active[i].Y >= 0, "negative packed position");
        for (int j = i + 1; j < active.Length; j++)
        {
            bool overlap = Math.Min(active[i].X + active[i].Width, active[j].X + active[j].Width) - Math.Max(active[i].X, active[j].X) > epsilon
                && Math.Min(active[i].Y + active[i].Height, active[j].Y + active[j].Height) - Math.Max(active[i].Y, active[j].Y) > epsilon;
            Check(!overlap, $"overlapping packed images: {active[i].Id}/{active[j].Id}");
        }
    }
    if (active.Length == 0) return;
    var reached = new HashSet<int> { 0 };
    bool changed;
    do
    {
        changed = false;
        for (int i = 0; i < active.Length; i++)
            if (!reached.Contains(i) && reached.Any(j => Touches(active[i], active[j]))) { reached.Add(i); changed = true; }
    } while (changed);
    Check(reached.Count == active.Length, "every packed image must connect to the arrangement");

    static bool Touches(BoardSnapRect first, BoardSnapRect second)
    {
        double overlapX = Math.Min(first.X + first.Width, second.X + second.Width) - Math.Max(first.X, second.X);
        double overlapY = Math.Min(first.Y + first.Height, second.Y + second.Height) - Math.Max(first.Y, second.Y);
        return ((Math.Abs(first.X + first.Width - second.X) < epsilon || Math.Abs(second.X + second.Width - first.X) < epsilon) && overlapY > epsilon)
            || ((Math.Abs(first.Y + first.Height - second.Y) < epsilon || Math.Abs(second.Y + second.Height - first.Y) < epsilon) && overlapX > epsilon);
    }
}
static void Expect<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException("Expected " + typeof(T).Name);
}

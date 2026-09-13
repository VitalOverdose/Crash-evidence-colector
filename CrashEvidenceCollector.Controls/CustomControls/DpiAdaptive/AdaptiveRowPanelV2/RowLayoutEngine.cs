namespace ProfessorSnowsVideoDownloader.CustomControls;

internal sealed class RowLayoutResult
{
    public RowLayoutResult(int[] widths, int[] gaps, int overflow) { Widths = widths; Gaps = gaps; Overflow = overflow; }
    public int[] Widths { get; }
    public int[] Gaps { get; }
    public int Overflow { get; }
    public int UsedWidth => Widths.Sum() + Gaps.Sum();
}

internal static class RowLayoutEngine
{
    public static RowLayoutResult Fit(int[] authoredWidths, int[] minimumWidths, bool[] springs,
        int[] authoredGaps, int availableWidth, StripSpringMode springMode,
        StripGapCompressionMode gapCompressionMode)
    {
        ArgumentNullException.ThrowIfNull(authoredWidths);
        ArgumentNullException.ThrowIfNull(minimumWidths);
        ArgumentNullException.ThrowIfNull(springs);
        ArgumentNullException.ThrowIfNull(authoredGaps);
        if (minimumWidths.Length != authoredWidths.Length || springs.Length != authoredWidths.Length)
            throw new ArgumentException("Width, minimum-width, and spring arrays must have the same length.");
        if (authoredGaps.Length != Math.Max(0, authoredWidths.Length - 1))
            throw new ArgumentException("There must be exactly one fewer gap than item widths.");

        int[] widths = authoredWidths.Select((width, index) => Math.Max(Math.Max(1, minimumWidths[index]), width)).ToArray();
        int[] floors = minimumWidths.Select(width => Math.Max(1, width)).ToArray();
        int[] gaps = authoredGaps.Select(gap => Math.Max(0, gap)).ToArray();
        int available = Math.Max(0, availableWidth);
        int[] springIndexes = Enumerable.Range(0, springs.Length).Where(index => springs[index]).ToArray();

        // David's law: springs SHARE the space not used by fixed controls EQUALLY — their
        // authored widths are irrelevant. (The old rule added the spare on top of authored
        // widths, so two springs authored differently stayed unequal forever and a
        // spring-centered group never actually centered.)
        if (springIndexes.Length > 0 && CanExpand(springMode))
        {
            int fixedTotal = gaps.Sum();
            for (int index = 0; index < widths.Length; index++)
                if (!springs[index]) fixedTotal += widths[index];

            int springSpace = available - fixedTotal;
            int floorSum = springIndexes.Sum(index => floors[index]);
            if (springSpace >= floorSum)
            {
                int share = springSpace / springIndexes.Length;
                int remainder = springSpace % springIndexes.Length;
                foreach (int index in springIndexes)
                    widths[index] = Math.Max(floors[index], share + (remainder-- > 0 ? 1 : 0));
                // A floor can push one spring above its share; the contraction pass below
                // settles any resulting overflow against the other springs.
            }
            else
            {
                foreach (int index in springIndexes) widths[index] = floors[index];
            }
        }

        int total = widths.Sum() + gaps.Sum();

        if (total > available)
        {
            int overflow = total - available;
            if (CanContract(springMode) && springIndexes.Length > 0)
                overflow -= ReduceWidths(widths, floors, springIndexes, overflow);
            if (overflow > 0) overflow -= ReduceGaps(gaps, overflow, gapCompressionMode);
            if (overflow > 0)
                overflow -= ReduceWidths(widths, floors, Enumerable.Range(0, widths.Length).ToArray(), overflow);
        }

        int remainingOverflow = Math.Max(0, widths.Sum() + gaps.Sum() - available);
        return new RowLayoutResult(widths, gaps, remainingOverflow);
    }

    private static bool CanExpand(StripSpringMode mode) => mode is StripSpringMode.Expands or StripSpringMode.Both;
    private static bool CanContract(StripSpringMode mode) => mode is StripSpringMode.Contracts or StripSpringMode.Both;

    private static int ReduceWidths(int[] widths, int[] floors, int[] indexes, int needed)
    {
        int reduced = 0;
        while (needed > 0)
        {
            bool changed = false;
            foreach (int index in indexes)
            {
                if (needed == 0) break;
                if (widths[index] <= floors[index]) continue;
                widths[index]--;
                needed--;
                reduced++;
                changed = true;
            }
            if (!changed) break;
        }
        return reduced;
    }

    private static int ReduceGaps(int[] gaps, int needed, StripGapCompressionMode mode)
    {
        int before = gaps.Sum();
        if (mode == StripGapCompressionMode.RightToLeft)
        {
            for (int index = gaps.Length - 1; index >= 0 && needed > 0; index--)
            {
                int reduction = Math.Min(gaps[index], needed);
                gaps[index] -= reduction;
                needed -= reduction;
            }
        }
        else
        {
            while (needed > 0)
            {
                bool changed = false;
                for (int index = 0; index < gaps.Length && needed > 0; index++)
                {
                    if (gaps[index] == 0) continue;
                    gaps[index]--;
                    needed--;
                    changed = true;
                }
                if (!changed) break;
            }
        }
        return before - gaps.Sum();
    }
}

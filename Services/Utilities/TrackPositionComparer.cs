namespace DiscogScrobblerMVC.Services;

/// <summary>
/// Orders Discogs-style track positions numerically inside digit runs (<c>2</c> before <c>10</c>, <c>A2</c> before <c>A10</c>).
/// </summary>
public sealed class TrackPositionComparer : IComparer<string?>
{
    public static TrackPositionComparer Instance { get; } = new();

    private TrackPositionComparer()
    {
    }

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
            return 0;
        if (string.IsNullOrWhiteSpace(x))
            return string.IsNullOrWhiteSpace(y) ? 0 : 1;
        if (string.IsNullOrWhiteSpace(y))
            return -1;

        ReadOnlySpan<char> left = x.AsSpan().Trim();
        ReadOnlySpan<char> right = y.AsSpan().Trim();

        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            var leftIsDigit = char.IsAsciiDigit(left[leftIndex]);
            var rightIsDigit = char.IsAsciiDigit(right[rightIndex]);
            if (leftIsDigit && rightIsDigit)
            {
                var leftNumber = ReadUInt(left, ref leftIndex);
                var rightNumber = ReadUInt(right, ref rightIndex);
                var numberComparison = leftNumber.CompareTo(rightNumber);
                if (numberComparison != 0)
                    return numberComparison;
            }
            else
            {
                var leftChar = char.ToUpperInvariant(left[leftIndex]);
                var rightChar = char.ToUpperInvariant(right[rightIndex]);
                if (leftChar != rightChar)
                    return leftChar.CompareTo(rightChar);
                leftIndex++;
                rightIndex++;
            }
        }

        return left.Length - right.Length;
    }

    private static long ReadUInt(ReadOnlySpan<char> value, ref int index)
    {
        long number = 0;
        while (index < value.Length && char.IsAsciiDigit(value[index]))
        {
            number = number * 10 + (value[index] - '0');
            index++;
        }

        return number;
    }
}

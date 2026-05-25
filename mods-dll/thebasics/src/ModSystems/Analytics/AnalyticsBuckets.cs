namespace thebasics.ModSystems.Analytics;

internal static class AnalyticsBuckets
{
    public static string Count(int count)
    {
        return count switch
        {
            <= 0 => "0",
            <= 5 => "1-5",
            <= 10 => "6-10",
            <= 20 => "11-20",
            <= 50 => "21-50",
            <= 100 => "51-100",
            _ => "101+"
        };
    }
}

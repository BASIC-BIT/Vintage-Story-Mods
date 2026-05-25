using System;

namespace thebasics.ModSystems.Analytics;

public static class AnalyticsBuckets
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

    public static string Duration(TimeSpan duration)
    {
        return duration.TotalMinutes switch
        {
            < 1 => "<1m",
            < 5 => "1-5m",
            < 30 => "5-30m",
            < 120 => "30-120m",
            _ => "120m+"
        };
    }
}

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TopGil;

internal static class TimeHelper
{
    internal static long MeasureExecutionTime(Action action)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }

    internal static async Task<long> MeasureExecutionTimeAsync(Func<Task> action)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        await action();
        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }
}

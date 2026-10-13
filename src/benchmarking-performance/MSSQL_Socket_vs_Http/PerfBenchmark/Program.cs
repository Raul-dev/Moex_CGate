using BenchmarkDotNet.Running;

namespace PerfBenchmark;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<NetworkProtocolsBenchmark>();
    }
}

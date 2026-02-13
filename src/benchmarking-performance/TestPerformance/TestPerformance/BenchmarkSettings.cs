namespace TestPerformance
{
    public class BenchmarkSettings
    {
        public string InputPath { get; set; } = string.Empty;
        public int IterationCount { get; set; } = 1;
        public int InvocationCount { get; set; } = 1;
        public string? ConnectionString { get; set; }
    }
}
namespace TestPerformance
{
    public class BenchmarkSettings
    {
        public string InputPath { get; set; } = string.Empty;
        public int Iterations { get; set; } = 1;
        public string? ConnectionString { get; set; }
    }
}
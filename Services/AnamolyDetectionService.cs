using InsightEngine.Models.Agents;

namespace InsightEngine.Services
{
    public class AnomalyAlert
    {
        public string Column { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // Spike, Drop, AllZeros, SuddenJump
        public string Message { get; set; } = string.Empty;
        public double Value { get; set; }
        public int RowIndex { get; set; }
        public string Severity { get; set; } = string.Empty; // Warning, Critical
    }

    public class AnomalyDetectionService
    {
        private readonly ILogger<AnomalyDetectionService> _logger;

        public AnomalyDetectionService(ILogger<AnomalyDetectionService> logger)
        {
            _logger = logger;
        }

        public List<AnomalyAlert> DetectAnomalies(
            List<Dictionary<string, object>> data)
        {
            var alerts = new List<AnomalyAlert>();

            if (data == null || data.Count < 3) return alerts;

            var firstRow = data.First();
            var numericColumns = firstRow
                .Where(kv => kv.Value != null && IsNumeric(kv.Value))
                .Select(kv => kv.Key)
                .ToList();

            foreach (var column in numericColumns)
            {
                var values = data
                    .Select((r, i) => new { Index = i, Val = r.ContainsKey(column) && r[column] != null ? Convert.ToDouble(r[column]) : (double?)null })
                    .Where(x => x.Val.HasValue)
                    .ToList();

                if (!values.Any()) continue;

                var doubles = values.Select(v => v.Val!.Value).ToList();

                // Check all zeros
                if (doubles.All(v => v == 0))
                {
                    alerts.Add(new AnomalyAlert
                    {
                        Column = column,
                        Type = "AllZeros",
                        Message = $"Column '{column}' contains all zero values. Possible data issue.",
                        Severity = "Warning"
                    });
                    continue;
                }

                var avg = doubles.Average();
                var stdDev = CalculateStdDev(doubles, avg);

                if (stdDev == 0) continue;

                // Spike / Drop detection
                for (int i = 0; i < values.Count; i++)
                {
                    var val = values[i].Val!.Value;
                    var zScore = Math.Abs(val - avg) / stdDev;

                    if (zScore > 3)
                    {
                        alerts.Add(new AnomalyAlert
                        {
                            Column = column,
                            Type = val > avg ? "Spike" : "Drop",
                            Message = $"Extreme {'s'}pike detected in '{column}': {val:N2} (avg: {avg:N2}, {zScore:N1}σ away)",
                            Value = val,
                            RowIndex = values[i].Index,
                            Severity = "Critical"
                        });
                    }
                    else if (zScore > 2)
                    {
                        alerts.Add(new AnomalyAlert
                        {
                            Column = column,
                            Type = val > avg ? "Spike" : "Drop",
                            Message = $"Unusual value in '{column}': {val:N2} (avg: {avg:N2}, {zScore:N1}σ away)",
                            Value = val,
                            RowIndex = values[i].Index,
                            Severity = "Warning"
                        });
                    }
                }

                // Sudden jump detection (consecutive difference)
                for (int i = 1; i < doubles.Count; i++)
                {
                    var prev = doubles[i - 1];
                    var curr = doubles[i];
                    if (prev == 0) continue;

                    var changePct = Math.Abs((curr - prev) / Math.Abs(prev)) * 100;

                    if (changePct > 200)
                    {
                        alerts.Add(new AnomalyAlert
                        {
                            Column = column,
                            Type = "SuddenJump",
                            Message = $"Sudden {changePct:N0}% change in '{column}' between row {i} and {i + 1}",
                            Value = curr,
                            RowIndex = i,
                            Severity = changePct > 500 ? "Critical" : "Warning"
                        });
                    }
                }
            }

            return alerts;
        }

        private double CalculateStdDev(List<double> values, double avg)
        {
            var sumSq = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumSq / values.Count);
        }

        private bool IsNumeric(object value)
        {
            return value is int or long or double or float or decimal or short or byte;
        }
    }
}
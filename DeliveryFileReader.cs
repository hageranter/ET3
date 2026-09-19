using DeliveryRoutePlanner.Models;

namespace DeliveryRoutePlanner;

public static class DeliveryFileReader
{
    public static (List<Delivery> Deliveries, List<string> SkippedRows) ReadDeliveries(string filePath)
    {
        var deliveries = new List<Delivery>();
        var skippedRows = new List<string>();
        var lines = File.ReadAllLines(filePath);

        for (int i = 1; i < lines.Length; i++) // skip header row (empty file -> loop never runs)
        {
            var line = lines[i].Trim();
            if (line.Length == 0) continue;

            try
            {
                var parts = line.Split(',');

                string id = parts[0];
                string area = parts[1];
                int priority = int.Parse(parts[2]);

                var unitWeights = parts[3]
                    .Split(';')
                    .Select(w => double.Parse(w.Trim()))
                    .ToList();

                if (unitWeights.Any(w => w <= 0))
                {
                    throw new FormatException($"unit weight must be positive (found {unitWeights.First(w => w <= 0)}kg)");
                }

                deliveries.Add(new Delivery(id, area, priority, unitWeights));
            }
            catch (Exception ex)
            {
                skippedRows.Add($"Row {i + 1} (\"{line}\"): {ex.Message}");
            }
        }

        return (deliveries, skippedRows);
    }
}

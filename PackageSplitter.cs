using DeliveryRoutePlanner.Models;

namespace DeliveryRoutePlanner;

public static class PackageSplitter
{
    public static (List<Delivery> Packable, List<string> Excluded) Split(List<Delivery> deliveries, double capacityKg)
    {
        var packable = new List<Delivery>();
        var excluded = new List<string>();

        foreach (var delivery in deliveries)
        {
            if (delivery.TotalWeight <= capacityKg)
            {
                packable.Add(delivery);
                continue;
            }

            var tooHeavy = delivery.UnitWeights.Where(w => w > capacityKg).ToList();
            var shippable = delivery.UnitWeights.Where(w => w <= capacityKg).ToList();

            foreach (var weight in tooHeavy)
            {
                excluded.Add($"Delivery {delivery.Id} ({delivery.Area}): a {weight}kg unit exceeds {capacityKg}kg capacity on its own - cannot split further.");
            }

            if (shippable.Count == 0)
                continue;

            var sortedWeights = shippable.OrderByDescending(w => w).ToList();
            var partWeights = new List<List<double>>();
            var partTotals = new List<double>();

            foreach (var weight in sortedWeights)
            {
                int fitIndex = -1;
                for (int i = 0; i < partTotals.Count; i++)
                {
                    if (partTotals[i] + weight <= capacityKg)
                    {
                        fitIndex = i;
                        break;
                    }
                }

                if (fitIndex == -1)
                {
                    partWeights.Add(new List<double> { weight });
                    partTotals.Add(weight);
                }
                else
                {
                    partWeights[fitIndex].Add(weight);
                    partTotals[fitIndex] += weight;
                }
            }

            for (int i = 0; i < partWeights.Count; i++)
            {
                packable.Add(new Delivery(
                    $"{delivery.Id}-part{i + 1}",
                    delivery.Area,
                    delivery.Priority,
                    partWeights[i]));
            }
        }

        return (packable, excluded);
    }
}

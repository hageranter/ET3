using DeliveryRoutePlanner.Models;

namespace DeliveryRoutePlanner;

public static class ReportPrinter
{
    public static void PrintReport(List<Trip> trips, List<string> excludedReasons)
    {
        for (int i = 0; i < trips.Count; i++)
        {
            var trip = trips[i];
            Console.WriteLine($"Trip {i + 1} (Total: {trip.TotalWeight}kg)");

            foreach (var delivery in trip.Deliveries)
            {
                Console.WriteLine($"  - {delivery.Id} | {delivery.Area} | Priority {delivery.Priority} | {delivery.TotalWeight}kg");
            }

            Console.WriteLine();
        }

        Console.WriteLine("Summary");
        Console.WriteLine($"  Total trips: {trips.Count}");

        int totalDeliveries = trips.Sum(t => t.Deliveries.Count);
        Console.WriteLine($"  Total deliveries placed: {totalDeliveries}");

        if (excludedReasons.Count > 0)
        {
            Console.WriteLine($"  Excluded ({excludedReasons.Count}):");
            foreach (var reason in excludedReasons)
            {
                Console.WriteLine($"    - {reason}");
            }
        }
        else
        {
            Console.WriteLine("  Excluded: none");
        }
    }
}

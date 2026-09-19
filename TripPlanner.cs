using DeliveryRoutePlanner.Models;

namespace DeliveryRoutePlanner;

public static class TripPlanner
{
    public static List<Trip> PlanTrips(List<Delivery> deliveries, double capacityKg)
    {
        var allTrips = new List<Trip>();

        var tiers = deliveries
            .GroupBy(d => d.Priority)
            .OrderBy(tier => tier.Key);

        foreach (var tier in tiers)
        {
            var tierTrips = PackTier(tier.ToList(), capacityKg);
            allTrips.AddRange(tierTrips);
        }

        return allTrips;
    }

    private static List<Trip> PackTier(List<Delivery> tierDeliveries, double capacityKg)
    {
        var trips = new List<Trip>();

        var areaGroups = tierDeliveries.GroupBy(d => d.Area);

        foreach (var areaGroup in areaGroups)
        {
            foreach (var delivery in areaGroup)
            {
                var sameAreaTrip = trips.FirstOrDefault(t =>
                    t.Deliveries.Any(d => d.Area == delivery.Area) &&
                    t.TotalWeight + delivery.TotalWeight <= capacityKg);

                var targetTrip = sameAreaTrip
                    ?? trips.FirstOrDefault(t => t.TotalWeight + delivery.TotalWeight <= capacityKg);

                if (targetTrip == null)
                {
                    targetTrip = new Trip();
                    trips.Add(targetTrip);
                }

                targetTrip.Add(delivery);
            }
        }

        return trips;
    }
}

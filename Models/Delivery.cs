namespace DeliveryRoutePlanner.Models;

public class Delivery
{
    public string Id { get; }
    public string Area { get; }
    public int Priority { get; }
    public double TotalWeight { get; }
    public List<double> UnitWeights { get; }

    public Delivery(string id, string area, int priority, double totalWeight, List<double> unitWeights)
    {
        Id = id;
        Area = area;
        Priority = priority;
        TotalWeight = totalWeight;
        UnitWeights = unitWeights;
    }
}

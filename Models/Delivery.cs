namespace DeliveryRoutePlanner.Models;

public class Delivery
{
    public string Id { get; }
    public string Area { get; }
    public int Priority { get; }
    public List<double> UnitWeights { get; }

    public double TotalWeight => UnitWeights.Sum();
    public int Quantity => UnitWeights.Count;

    public Delivery(string id, string area, int priority, List<double> unitWeights)
    {
        Id = id;
        Area = area;
        Priority = priority;
        UnitWeights = unitWeights;
    }
}

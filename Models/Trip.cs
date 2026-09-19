namespace DeliveryRoutePlanner.Models;

public class Trip
{
    public List<Delivery> Deliveries { get; } = new();

    public double TotalWeight => Deliveries.Sum(d => d.TotalWeight);

    public void Add(Delivery delivery)
    {
        Deliveries.Add(delivery);
    }
}

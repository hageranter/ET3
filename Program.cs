using DeliveryRoutePlanner;
using DeliveryRoutePlanner.Models;

const double vehicleCapacityKg = 10.0;

string filePath = args.Length > 0 ? args[0] : "SampleInput/sample_deliveries.csv";

List<Delivery> deliveries;
List<string> skippedRows;

try
{
    (deliveries, skippedRows) = DeliveryFileReader.ReadDeliveries(filePath);
}
catch (FileNotFoundException)
{
    Console.WriteLine($"Input file not found: {filePath}");
    return;
}

var splitResult = PackageSplitter.Split(deliveries, vehicleCapacityKg);
var trips = TripPlanner.PlanTrips(splitResult.Packable, vehicleCapacityKg);

var allExcludedReasons = skippedRows.Concat(splitResult.Excluded).ToList();

ReportPrinter.PrintReport(trips, allExcludedReasons);

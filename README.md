# Delivery Route Planner

A small console program that groups delivery requests into vehicle trips, respecting priority, area, and a 10kg capacity per trip.

## How to Run

Requires the .NET SDK (targets `net10.0`).

Run with the bundled sample data:
```
dotnet run
```

Run with a different input file:
```
dotnet run -- path/to/your/file.csv
```

## Input Format

CSV with a header row: `id,area,priority,unit_weights`

Note: this replaces the single `weight` column from the assignment's sample table with `unit_weights`. The assignment allows choosing/documenting the input format freely, and this change is what makes the "Extra Feature" below (splitting overweight requests) possible — see that section for why.

- `id` — unique identifier for the delivery request.
- `area` — delivery area name.
- `priority` — lower number = more urgent, handled first.
- `unit_weights` — semicolon-separated weight (kg) of each individual unit in the request. A single item is just one number (e.g. `4.5`); a bundled request of several units lists each one's actual weight (e.g. `17;1` for two units weighing 17kg and 1kg).

Sample files in `SampleInput/`:
- `sample_deliveries.csv` — exercises every rule and edge case described below.
- `sample_deliveries_empty.csv` — header only, no delivery rows.

## Solution Approach

The program runs a straight pipeline: **read → split oversized packages → pack into trips → print report.**

1. **Read** (`DeliveryFileReader`): parses the CSV into `Delivery` objects. Malformed rows are skipped and reported rather than crashing the whole run.
2. **Split** (`PackageSplitter`): any request whose total weight exceeds the 10kg capacity is handled at the individual-unit level. Units that are individually too heavy to ever fit in a trip are excluded and reported by themselves; the remaining units are bin-packed (largest-first, "first-fit-decreasing") into as few sub-deliveries as possible, each within capacity. If a request has both shippable and unshippable units, the shippable ones still go out — only the oversized units are declined.
3. **Pack** (`TripPlanner`): deliveries are grouped into priority tiers, processed strictly from most to least urgent. **A trip never contains two different priority values** — once a tier is fully packed, its trips are locked and the next tier always starts a new trip. This guarantees priority is never violated: a more urgent delivery can never end up in a later trip than a less urgent one. Within a tier, deliveries are grouped by area, and each one is placed into a same-area trip with room if one exists, otherwise any other trip in the tier with room, otherwise a new trip.
4. **Report** (`ReportPrinter`): prints each trip's contents and total weight, then a summary of trip/delivery counts and everything excluded (with a reason).

## Most Difficult Part

Getting the priority-vs-area tension right without quietly breaking the priority rule. An early version let any delivery fill leftover space in *any* previously opened trip, which packed tighter but could let a less-urgent delivery ship in an earlier trip than a more-urgent one purely because of leftover capacity — a subtle violation of "lower priority numbers should be handled first." Locking trips to a single priority tier fixes this completely, at the cost of occasionally wasting a little capacity at tier boundaries.

The other tricky part was the package-splitting feature: my first version assumed every unit in a multi-unit request weighed the same (`total ÷ quantity`), which gives wrong answers for a real bundle of different-weight items. Switching the input to store each unit's actual weight, and bin-packing at the individual-unit level, fixed that but meant redesigning the CSV schema and the splitter after they'd already been written.

## Situations Where the Grouping May Not Be Optimal

- **Tier boundaries waste capacity.** If a priority tier's last trip has, say, 3kg of unused space, the next tier is not allowed to use it, even if one of its deliveries would fit — trips never mix priorities. This is deliberate (it makes the priority guarantee absolute) but it means the result isn't the tightest possible packing.
- **Area placement is greedy, not globally optimal.** Within a tier, each delivery is placed into the first trip that fits (preferring same area), not re-evaluated against every possible arrangement. A different processing order could occasionally produce fewer trips or tighter area clustering.
- **Splitting uses first-fit-decreasing bin packing**, a well-known approximation. It's simple and generally good but isn't guaranteed to use the mathematically minimum number of parts in every case.

## At 1,000,000 Deliveries

- `TripPlanner.PackTier` searches through all trips opened so far for every delivery it places (`trips.FirstOrDefault(...)`). Within a very large single priority tier, this is effectively O(n²) and would get noticeably slow.
- `DeliveryFileReader` loads the entire file into memory at once (`File.ReadAllLines`) and builds every `Delivery` object up front. At 1,000,000+ rows this holds a large amount of data in memory simultaneously; a streaming, line-by-line reader would scale much better.
- The `GroupBy`/`OrderBy` calls used to form priority tiers and area groups allocate intermediate collections — fine at small scale, but extra overhead at very large scale.

## What I'd Improve With Another Day

- Replace the linear trip search in `PackTier` with a lookup keyed by area (e.g. a dictionary of open trips per area) to avoid rescanning every trip for every delivery.
- Make `DeliveryFileReader` stream the file instead of loading it fully into memory, for very large inputs.
- Add basic input validation for nonsensical values (negative or zero weights), currently a known limitation — see below.
- Add automated tests instead of relying on the hand-traced sample data for verification.

## Extra Feature: Splittable Overweight Packages

Instead of declining every delivery over the 10kg capacity outright, the program checks whether the request is actually divisible. The CSV records a request as `unit_weights` — the weight of every individual unit in that request (e.g. `17;1` for two units weighing 17kg and 1kg). My first version of this feature instead stored a single total `weight_kg` plus a `quantity`, and assumed every unit weighed the same (`unit_weight = weight_kg / quantity`), which made splitting a simple division. I chose to change it to record each unit's actual weight instead, to make the program more realistic — a bundled request rarely has perfectly identical items, and the uniform-weight assumption would silently give wrong answers for one that doesn't (e.g. 18kg over 2 units could really be 17kg + 1kg, not 9kg + 9kg).

With real per-unit weights, a request with `quantity > 1` represents multiple separate physical units, so if some of those units individually fit within capacity, they still get shipped — split into their own sub-deliveries — even if other units in the same request are too heavy to ever ship. Only units that are individually oversized are declined. This was chosen deliberately over an all-or-nothing approach: since the units are genuinely separate objects, there's no reason to hold a shippable unit hostage to an unrelated oversized one just because they arrived in the same request. Every declined unit is reported individually with the reason, so nothing is silently dropped.

## Known Limitations

- Duplicate delivery ids are not validated. This doesn't break packing, just makes the output slightly confusing to read if it happens.
- The program trusts that each `unit_weights` value accurately reflects a real individual unit's weight; there is no way to verify this against the input alone (negative and zero weights are rejected, but there's no way to catch, say, a mistyped 5kg entered as 50kg).

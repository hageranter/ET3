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

CSV with a header row: `id,area,priority,weight_kg,unit_weights`

- `id`, `area`, `priority` — standard fields (unique id, delivery area, urgency — lower number = more urgent).
- `weight_kg` — the package's total weight. Same field, same meaning as the assignment's original sample table — always present, used directly.
- `unit_weights` — **optional**, blank for every normal delivery. Only filled in for a bundled request that might need splitting: a semicolon-separated list of each individual unit's actual weight (e.g. `17;1` for two units weighing 17kg and 1kg). This is what makes the "Extra Feature" below possible. It's read only when `weight_kg` exceeds the vehicle capacity — for a normal delivery it's not looked at at all.

Note: `weight_kg` and `unit_weights` are not cross-checked against each other — see Known Limitations.

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

- **Trips never mix priorities, so some space goes unused.** If a priority-1 trip ends with 3kg of empty space, a priority-2 delivery that would fit there still can't use it — it has to start a new trip instead. This is on purpose (it's what keeps the priority rule airtight), but it means the packing isn't as tight as it could be.
- **Deliveries go into the first trip that fits, not the best one.** Within a priority group, each delivery is placed into the first trip with room (same area preferred), without checking every possible arrangement. A different order could sometimes fit everything into fewer trips.
- **Splitting is fast, not perfect.** When a package needs to be split, it uses a simple, well-known method (biggest units first, into the first part with room). This usually works well, but a more thorough approach could occasionally use one less part.

## At 1,000,000 Deliveries

- **Finding a trip gets slower as the list grows.** For every delivery, the program checks all trips opened so far in that priority group to find one with room. With that many deliveries, this checking adds up and the program would noticeably slow down.
- **The whole input file loads into memory at once.** The program reads the entire file before processing any of it. At 1,000,000+ rows, that's a lot of data held in memory at the same time — reading it one line at a time instead would use far less memory.
- **Sorting and grouping adds some overhead.** Grouping deliveries by priority and area creates extra copies of the data behind the scenes. Not noticeable on a small file, but it adds up at a very large scale.

## What I'd Improve With Another Day

- Replace the linear trip search in `PackTier` with a lookup keyed by area (e.g. a dictionary of open trips per area) to avoid rescanning every trip for every delivery.
- Make `DeliveryFileReader` stream the file instead of loading it fully into memory, for very large inputs.
- Add basic input validation for nonsensical values (negative or zero weights), currently a known limitation — see below.
- Add automated tests instead of relying on the hand-traced sample data for verification.

## Extra Feature: Splittable Overweight Packages

Instead of declining every delivery over the 10kg capacity outright, the program checks whether the request is actually divisible:

- If `weight_kg` is over capacity but `unit_weights` is blank (no breakdown given), it's treated like a single indivisible product — declined and reported, since there's no way to know how to divide it.
- If `weight_kg` is over capacity and `unit_weights` is provided, splitting happens at the individual-unit level, using each unit's real weight rather than assuming they're all equal. My first version of this feature assumed every unit in a multi-unit request weighed the same (`total ÷ quantity`), which gives wrong answers for a real bundle of different-weight items (e.g. 18kg over 2 units could really be 17kg + 1kg, not 9kg + 9kg). Recording each unit's actual weight instead makes the program more realistic, at the cost of needing an extra optional column.

Because units are genuinely separate physical objects, if some of them individually fit within capacity, they still get shipped — split into their own sub-deliveries — even if other units in the same request are too heavy to ever ship. Only the individually-oversized units are declined. This was chosen deliberately over an all-or-nothing approach: there's no reason to hold a shippable unit hostage to an unrelated oversized one just because they arrived in the same request. Every declined unit is reported individually with the reason, so nothing is silently dropped.

## Known Limitations

- `weight_kg` and `unit_weights` are not cross-validated against each other. If a hand-edited file had a `weight_kg` that didn't match the true sum of its `unit_weights`, the mismatch wouldn't be caught — `weight_kg` decides whether splitting is attempted at all, and `unit_weights` (if present) is trusted as-is for how to divide it. Accepted as a trusted-input assumption rather than adding validation for a case the assignment doesn't require handling.
- Duplicate delivery ids are not validated. This doesn't break packing, just makes the output slightly confusing to read if it happens.
- Negative and zero weights are rejected, but there's no way to catch a plausible-but-wrong value, like a mistyped 5kg entered as 50kg.

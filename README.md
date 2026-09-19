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
- `weight_kg` — the package's total weight.
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

- **Balancing priority and area without breaking the priority rule.** An early version let any delivery fill leftover space in any open trip. That packed tighter, but could let a less-urgent delivery ship in an earlier trip than a more-urgent one, just because there was room — a quiet violation of "lower priority handled first." Fix: lock each trip to a single priority tier. Trade-off: a little capacity gets wasted at tier boundaries.
- **Splitting packages correctly.** My first version assumed every unit in a multi-unit request weighed the same (`total ÷ quantity`). That's wrong for a real bundle of different-weight items. Fix: store and split on each unit's actual weight instead — meant redesigning the CSV format and the splitter after they were already written.

## Situations Where the Grouping May Not Be Optimal

- **Trips never mix priorities**, so leftover space at a tier boundary can't be reused — on purpose, to keep the priority rule absolute.
- **Deliveries go into the first trip that fits, not the best one** — a different order could sometimes pack everything into fewer trips.
- **Splitting uses a fast, well-known method (first-fit-decreasing), not a guaranteed-optimal one** — it can occasionally use one extra part.

## At 1,000,000 Deliveries

- **Placing each delivery gets slower as more trips exist.** The program checks every trip made so far to find one with room — at that scale, this adds up and slows things down.
- **The whole file loads into memory before anything is processed.** Reading it one line at a time instead would use far less memory.
- **Grouping deliveries by priority and area adds a little overhead**, which becomes noticeable at that scale.

## What I'd Improve With Another Day

- Replace the linear trip search in `PackTier` with a lookup keyed by area (e.g. a dictionary of open trips per area) to avoid rescanning every trip for every delivery.
- Make `DeliveryFileReader` stream the file instead of loading it fully into memory, for very large inputs.
- Add validation that `weight_kg` actually matches the sum of `unit_weights` instead of trusting both as-is — see Known Limitations.
- Add automated tests instead of relying on the hand-traced sample data for verification.

## Extra Feature: Splittable Overweight Packages

Instead of declining every delivery over the 10kg capacity outright, the program checks whether the request is actually divisible:

- If `weight_kg` is over capacity but `unit_weights` is blank (no breakdown given), it's treated like a single indivisible product — declined and reported, since there's no way to know how to divide it.
- If `weight_kg` is over capacity and `unit_weights` is provided, splitting happens at the individual-unit level, using each unit's real weight rather than assuming they're all equal. My first version of this feature assumed every unit in a multi-unit request weighed the same (`total ÷ quantity`), which gives wrong answers for a real bundle of different-weight items (e.g. 18kg over 2 units could really be 17kg + 1kg, not 9kg + 9kg). Recording each unit's actual weight instead makes the program more realistic, at the cost of needing an extra optional column.

Because units are genuinely separate physical objects, if some of them individually fit within capacity, they still get shipped — split into their own sub-deliveries — even if other units in the same request are too heavy to ever ship. Only the individually-oversized units are declined. This was chosen deliberately over an all-or-nothing approach: there's no reason to hold a shippable unit hostage to an unrelated oversized one just because they arrived in the same request. Every declined unit is reported individually with the reason, so nothing is silently dropped.

## Known Limitations

- **`weight_kg` and `unit_weights` aren't cross-checked.** If they disagreed in a hand-edited file, the mismatch wouldn't be caught — `weight_kg` alone decides whether to split, `unit_weights` is trusted as-is for how.
- **Duplicate delivery ids aren't validated.** Doesn't break packing, just makes the output slightly confusing to read.
- **Negative/zero weights are rejected, but plausible-but-wrong values aren't.** A mistyped 5kg entered as 50kg would pass through unnoticed.

## Tools Used

- **Claude Code** (Anthropic's AI coding agent) — I designed every decision myself first (the tier-isolation algorithm, the splitting logic, the partial-fulfillment call, the CSV schema), then used Claude Code as an implementation partner: reviewing every file it wrote and pushing back whenever something didn't match what I wanted — for example, rejecting an early uniform-weight assumption in the splitting logic, and catching an inaccurate line in an earlier draft of this README.
- **`state.md`** (included in this repo) — my own running design log, kept throughout the build to record every decision and trade-off as I made it, so I always had a clear record to check the implementation against.

I have hands-on experience directing AI coding agents and agentic workflows, and that's how I approach them here too: a way to move faster on a plan I own and fully understand, not a substitute for understanding it myself.

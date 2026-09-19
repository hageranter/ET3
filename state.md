# State — Delivery Route Planner

Working log of decisions made so far. Update this file as we go.

## Decisions made

- **Language:** C#
- **Input format:** CSV — matches the assignment's own sample table (id, area, priority, weight), no parsing library needed.

## Algorithm approach (revised — strict tier isolation)

1. Read all deliveries from the CSV into a list.
2. Filter out invalid deliveries (weight > 10kg vehicle capacity — can never fit in any trip). Report these separately instead of crashing or silently dropping them.
3. Group the remaining valid deliveries into **priority tiers** (all priority-1 together, all priority-2 together, etc.), processed strictly in ascending priority order.
4. **Within each tier:**
   - Group the tier's deliveries by area.
   - For each delivery, try to place it into a trip (within this tier only) that already contains deliveries from the same area and has room.
   - If no such trip, try any other open trip in this tier that has room.
   - If nothing fits, open a new trip.
5. Once a tier is fully packed, its trips are **locked** — the next tier always starts a brand-new trip. Trips are never shared across two different priority values.
6. Concatenate all tiers' trip lists in order to produce the final result.

**Why this approach:** priority is never even arguably violated — since two different priority values can never share a trip, a more urgent delivery can never end up dispatched after (in a higher trip number than) a less urgent one. Within a tier, same-area deliveries are actively grouped and trips are packed efficiently (not just naive left-to-right fill), which is a real improvement over a single global sort + sequential pass. Capacity is never exceeded by construction, and every valid delivery ends up in exactly one trip.

**Known trade-off (to state honestly in README):** locking tiers to separate trips can waste a little capacity at tier boundaries — e.g. if priority-1's last trip has 3kg of leftover space, a priority-2 delivery that would fit isn't allowed to use it, since it would mean two different priority values sharing a trip. We accept this because it makes the priority guarantee absolute and simple to explain, rather than allowing tighter packing at the cost of an occasional (rare, non-delaying) priority-order inversion between trips.

## Edge cases — handling plan

Assignment's 4 required edge cases:
- **No deliveries:** program should run cleanly and report zero trips, not error. Verified: `sample_deliveries_empty.csv` → "Total trips: 0", no crash.
- **Package heavier than 10kg:** see "Extra feature — splittable overweight packages" below; not a blanket decline anymore.
- **Multiple deliveries same priority:** handled naturally by the tier-isolation algorithm above (same-priority items form one tier, packed together by area).
- **Adding next package would exceed capacity:** handled naturally by the tier-isolation algorithm above (falls to next open trip in the tier, or a new one).

Additional edge cases found by testing (not required by the assignment, but decided to handle — "Option B"):
- **Malformed row** (missing/blank field, non-numeric value): originally crashed with a raw .NET stack trace (`FormatException`/`IndexOutOfRangeException`). Now: `DeliveryFileReader.ReadDeliveries` wraps each row's parsing in a try/catch — a bad row is skipped and reported (e.g. `Row 3 ("2,Maadi,1,"): could not be parsed, skipped.`) instead of aborting the whole file. Verified with a row with a blank weight and a row with a non-numeric priority — both skipped, other valid rows still processed.
- **Missing input file:** originally crashed with a raw `FileNotFoundException` stack trace. Now: `Program.cs` catches it specifically and prints one friendly line (`Input file not found: <path>`) before exiting. Verified.
- **Negative/zero unit weight:** now validated in `DeliveryFileReader` — throws inside the same try/catch as parsing (`unit weight must be positive (found ...)`), so it's skipped and reported like any other malformed row. Verified with `-3.0` and `0` test rows.
- **Not addressed (documented as a known limitation for the README):** duplicate ids are not validated — cosmetic only, doesn't break packing. Left out deliberately to avoid over-engineering beyond what the assignment asks for.

Also changed the malformed-row catch to report `ex.Message` instead of a generic "could not be parsed" string — gives a more specific reason (e.g. `The input string '' was not in a correct format.`) for free.

## CSV schema (revised — real per-unit weights)

`id,area,priority,unit_weights`

- `unit_weights` = semicolon-separated list of each individual unit's actual weight, e.g. `4.5` (one unit) or `17;1` (two units, weighing 17kg and 1kg respectively).
- `TotalWeight` and `Quantity` are derived, not stored: `TotalWeight = sum(unit_weights)`, `Quantity = count(unit_weights)`.

**Why this replaced the earlier `weight_kg` + `quantity` design:** the original schema assumed every unit within a request weighs the same (`unit_weight = weight_kg / quantity`), which silently gives wrong answers for a real bundle of different-weight items (e.g. 18kg over 2 units could be 17+1, not 9+9). Storing actual per-unit weights removes that assumption entirely.

## Extra feature — splittable overweight packages (revised — unit-level bin packing)

Instead of blanket-declining every delivery over the 10kg capacity, decide per individual unit:

1. If `TotalWeight <= 10kg` → passes through unchanged, no splitting needed.
2. Otherwise, split the request's unit weights into two groups: units that individually exceed capacity (**can never ship, ever** — excluded and reported one by one) and units that individually fit.
3. The fitting units are bin-packed into as few "part" deliveries as possible using **first-fit-decreasing**: sort them largest-first, place each into the first part with room, else start a new part. Parts are suffixed ids (e.g. `3-part1`, `3-part2`), same area/priority as the original — ordinary deliveries from that point on, flowing through the normal tier + area packing algorithm with no special-casing needed there.

A single-unit oversized delivery (`quantity == 1`, over capacity) is just the degenerate case of step 2 — no separate check needed.

This is a preprocessing step that runs before the tier-packing algorithm, doubling as the assignment's required "one additional feature."

**Decision — partial fulfillment, not all-or-nothing:** when a multi-unit request has some units that fit and some that don't, the shippable units still ship; only the individually-oversized units are declined and reported. Considered the alternative (decline the entire request if any single unit can't be shipped) but rejected it — since `quantity > 1` already models genuinely separate physical units (that's the premise the splitting feature is built on), there's no reason to hold a perfectly shippable unit hostage to an unrelated oversized one in the same request. Nothing is silently lost either way — the report always names exactly which unit was declined and why.

## Output format

Console-only (no output file). For each trip: trip number, its deliveries (id, area, priority, weight), and the trip's total weight. Followed by a final summary section: total trips, total valid deliveries placed, and any excluded unsplittable-overweight deliveries with the reason.

## Sample input files (SampleInput/)

**`sample_deliveries.csv`** — includes the assignment's original 5 rows (1-5, normal baseline) plus rows built to exercise every rule/edge case:

| id | area | priority | unit_weights | purpose |
|----|------|----------|--------------|---------|
| 1-5 | mixed | mixed | single values | assignment's original sample data (normal case) |
| 6 | Maadi | 1 | `3.0` | same area + same priority as row 2 → should cluster into the same trip |
| 7 | Giza | 2 | `12.0` | single indivisible unit over capacity → unsplittable, excluded & reported |
| 8 | Heliopolis | 3 | `5;5;5;5` | 4 equal-weight units, 20kg total → splits into two full 10kg parts |
| 9 | Dokki | 1 | `15;15` | both units individually exceed capacity → fully unsplittable, excluded & reported |
| 10 | Nasr City | 2 | `17;1` | non-uniform units — the 17kg unit is excluded & reported individually, the 1kg unit still ships normally |

Traced by hand: tier 1 (priority 1) → row2+row6 cluster in Trip A (5.0kg, Maadi), row4 alone in Trip B (7.0kg, Zamalek); row9 fully excluded (both units too heavy). Tier 2 (priority 2) → row1 (4.5kg) + `10-part1` (1.0kg, the shippable remainder of row10) share a trip via same-area match, then row5 (3.5kg) fills the same trip's leftover space (9.0kg total, cross-area fill); row7 and the 17kg part of row10 are excluded. Tier 3 (priority 3) → row3 alone (1.2kg), plus row8's two split parts each filling their own trip exactly (10kg + 10kg).

**`sample_deliveries_empty.csv`** — header row only, no data rows, for the no-deliveries edge case.

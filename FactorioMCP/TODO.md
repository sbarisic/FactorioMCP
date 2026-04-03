# TODO

Planned features, improvements, and tasks. See [CONTRIBUTING.md](CONTRIBUTING.md) for workflow and CPX scale.

---

## Bugs

*No active bugs*

### Uncategorized

*No uncategorized items*

---

## Features

### High Priority

- [ ] **High-Level Task Primitives** — Compound tools (`GatherResource`, `RefuelEntity`, `Smelt`) that collapse 10–20 atomic calls into 1 **(CPX 4)**
- [ ] **Semantic Area Perception** — `SummarizeArea`, `WhatAmILookingAt(direction)`, `FindBuildableArea(w, h)` for structured spatial awareness **(CPX 3)**
- [ ] **Smart Inserter Placement** — `place_inserter(target, side, direction)` with auto tile-offset calculation; `insert_between(src, dst)` for 1-tile gap placement **(CPX 3)**
- [ ] **Ghost Placement Validation** — Place ghosts first, validate placement/orientation/connectivity, return corrective errors before committing **(CPX 3)**
- [ ] **Belt Path Tool** — `connect_with_belts(start, end)` using Lua pathfinding instead of LLM placing individual segments **(CPX 3)**

### Medium Priority

- [ ] **Factory Analysis Tools** — `GetProductionStatus`, `FindUnpoweredEntities`, `FindIdleMachines`, `FindMissingInputs(x, y)` **(CPX 3)**
- [ ] **Logistics Flow Tracking** — Trace entity connectivity through belts/inserters, classify by role, build item flow graph **(CPX 4)**
- [ ] **Craft & Factory Planning** — `PlanCraft(item, count)` recipe tree; `PlanFactory(goal)` ordered steps to reduce hallucinated plans **(CPX 3)**
- [ ] **Vision Screenshot** — `take_screenshot()` with entity bounds overlay, directional indicators, and map legend for vision models **(CPX 3)**
- [ ] **Power Network Topology** — Trace pole connectivity graph from producers through poles to consumers **(CPX 3)**
- [ ] **Inventory Intelligence** — `EnsureItem(item, count)` auto-craft/gather; `GetInventorySummary` condensed output **(CPX 2)**

### Low Priority

- [ ] **Pickup Items** — `pickup_items(radius)` to collect dropped ground items **(CPX 1)**
- [ ] **Collision Slot Query** — `get_available_slots(x, y)` for unblocked adjacent tiles **(CPX 2)**
- [ ] **Smart Entity Placement** — `PlaceEntitySmart(entity, near)` with automatic position selection **(CPX 2)**
- [ ] **Blueprint Capture** — `blueprint_area(x1, y1, x2, y2)` to save and reuse designs **(CPX 2)**
- [ ] **Utility Tools** — `GetReachableEntities`, `CountItemInWorld`, `EstimateTravelTime` **(CPX 2)**
- [ ] **Logistics Tools** — Logistic robots, request/inspect logistic networks **(CPX 3)**
- [ ] **Combat Tools** — Attack, turret management, enemy detection, defense **(CPX 3)**
- [ ] **Train Management Tools** — Train control, station management, schedules **(CPX 3)**

### On Hold

- [ ] **Helper Factorio Mod** — `remote.call()` interfaces for async events. Blocked on: confirming RCON polling limitations **(CPX 5)**

---

## Improvements

### High Priority

- [ ] **Realistic Mining** — Replace instant-cheat `mine_*` with `player.mining_state` and poll for completion. Handle continuous mining, interruption, inventory full, and entity destruction **(CPX 4)**
- [ ] **Building Memory Resilience** — Treat as rebuildable cache: `RebuildBuildingMemory`, `InvalidateAndRescan` on failure, `ValidateKnownEntities` sampling. Add `LastSeenTick`/`Confidence` fields **(CPX 4)**

### Low Priority

- [ ] **Multiplayer Player Targeting** **(CPX 2)**

---

## Documentation

### Medium Priority

- [ ] **Condense PROMPT.md** — Rewrite to ≤300 lines while preserving essential instructions **(CPX 2)**

---

## Code Cleanup & Technical Debt

### Low Priority

- [ ] **Building Memory Spatial Indexing** — Replace flat list with chunk-based `Dictionary<(int, int), List<TrackedBuilding>>` for O(1) spatial lookups **(CPX 3)**

---

## Completed

See [DONE.md](DONE.md) for completed items.

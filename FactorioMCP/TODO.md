# FactorioMCP

A list of planned features, improvements, and tasks for this project.

> **CPX (Complexity Points)** - 1 to 5 scale:
> - **1** - Single file component
> - **2** - Single file component, but possible small changes in other files
> - **3** - Single file component with multiple dependencies, no architecture changes
> - **4** - Multi file component with multiple dependencies and significant logic, possible minor architecture changes
> - **5** - Large feature spanning multiple components and subsystems, major architecture changes

> Instructions for the TODO list:
- Move all completed TODO items into a separate Completed document (DONE.md) and simplify by consolidating/combining similar ones and shortening the descriptions where possible

> How TODO file should be iterated:
- First handle the Uncategorized section, if any similar issues already are on the TODO list, increase their priority instead of adding duplicates (categorize all at once)
- When Uncategorized section is empty, start by fixing Active Bugs
- After Active Bugs, handle the rest of the TODO file by priority and complexity (High priority takes precedance, then CPX points).

---

## Project Overview

**FactorioMCP** is an MCP (Model Context Protocol) server written in C# that connects to a running Factorio game instance via RCON and exposes game-control tools to an AI agent. The AI player behaves realistically — it walks instead of teleporting, waits for crafting recipes to finish, checks proximity before interacting, and does not cheat.

### Architecture

```
LLM (Claude/GPT/etc) ←→ [MCP Server (C#, stdio)] ←→ [RCON TCP] ←→ Factorio Game
```

- **MCP side**: Uses the [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) (`ModelContextProtocol` NuGet) to expose tools via stdio transport.
- **Factorio side**: Connects via Source RCON protocol over TCP to send Lua commands (`/c ...`) using the [Factorio Lua API](https://lua-api.factorio.com/latest/). See [`LUA_API.md`](LUA_API.md) for the bundled local API reference.
- **Realistic AI constraint**: No teleportation, no instant crafting, no spawning items — the AI must play within normal game mechanics.

### Solution Structure

| Project | Target | Purpose |
|---------|--------|---------|
| **FactorioMCP** | .NET 9 | MCP server console app — RCON client, game service, MCP tool definitions, hosting |
| **FactorioMCP.Tests** | .NET 9 | Unit tests for RCON protocol, service layer, and tool behavior |

```
FactorioMCP.Tests → FactorioMCP
```

### Current Architecture

| System | Status | Description |
|--------|--------|-------------|
| RCON Client | ✅ Functional | Low-level Source RCON protocol over TCP with auto-reconnection and exponential backoff |
| Factorio Service | ✅ Functional | High-level game operations (movement, crafting, building, world queries) |
| Energy Service | ✅ Functional | Electric network statistics (production/consumption/satisfaction) and per-entity power inspection |
| Goal Planner | ✅ Functional | AI goal tracking with state machine lifecycle, ordered steps, suspend/resume, and JSON file persistence |
| Building Memory | ✅ Functional | Tracks placed buildings with spatial queries, auto-tracking on place/mine, labels, and JSON file persistence |
| Vision Service | ✅ Functional | Annotated screenshots with entity bounding box overlays, inserter direction arrows, numbered labels, and structured map legends for vision-model analysis |
| MCP Tools | ✅ Functional | Movement, inventory/crafting, entity placement/mining, entity interaction (insert/remove items, inspect), world scanning, proximity checking, chat message, goal planning, energy management, research, building memory, vision screenshots, task primitives (gather/refuel/smelt) tools exposed via MCP SDK |
| MCP Hosting | ✅ Functional | Program.cs wiring with DI, configuration, stdio transport |
| Realistic Behaviors | ✅ Functional | Walking with real physics, crafting with real queue, proximity validation on place/mine, wait/polling for crafting completion, position arrival, and game tick timing — no cheating |

Legend: ✅ Functional | 🔶 Partial/WIP | ⬜ Planned

### Factorio Setup

Launch Factorio with RCON enabled:

```sh
# Singleplayer
factorio.exe --rcon-port 27015 --rcon-password mypassword

# Dedicated server
factorio --start-server save.zip --rcon-port 27015 --rcon-password mypassword
```

### Configuration

RCON connection settings are read from environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `FACTORIO_RCON_HOST` | `127.0.0.1` | RCON server host |
| `FACTORIO_RCON_PORT` | `27015` | RCON server port |
| `FACTORIO_RCON_PASSWORD` | `mypassword` | RCON password |
| `FACTORIO_GOALS_FILE` | `goals.json` | File path for goal planner persistence |
| `FACTORIO_BUILDINGS_FILE` | `buildings.json` | File path for building memory persistence |

---

## Features

### High Priority

*No high priority items*


### Medium Priority

- [ ] **Smart Entity Placement** — `PlaceEntitySmart(entity, near)` e.g. place a stone-furnace near iron-ore. Backend picks best available position automatically. **(CPX 2)**
- [ ] **Pickup Items** — `pickup_items(radius)`: simulate holding the 'F' key to collect items dropped on the ground within a radius. **(CPX 1)**
- [ ] **Factory Analysis Tools** — `GetProductionStatus`: what's being produced, bottlenecks, idle machines. `FindUnpoweredEntities`: list entities without power. `FindIdleMachines`: list machines not working. `FindMissingInputs(x, y)`: returns which input items an assembler/furnace is missing. Essential for AI to understand and debug factory state. **(CPX 3)**
- [ ] **Craft & Factory Planning** — `PlanCraft(item, count)`: returns full recipe tree with required intermediates and raw materials. `PlanFactory(goal)`: given a high-level goal like "automate iron plates", return rough ordered steps. Reduces LLM hallucinated plans. **(CPX 3)**

### Low Priority

- [ ] **Power Network Topology** — Trace how electricity flows through the physical network from producers (boilers, solar panels) through electric poles to consumers. Map the pole connectivity graph and show which entities are powered by which network segment. Complements existing `GetElectricNetwork` (aggregate stats) and `InspectEntityPower` (per-entity) with topological awareness for planning expansions and diagnosing coverage gaps. **(CPX 3)**
- [ ] **Logistics Tools** — Manage logistic robots, request items from logistic network, inspect logistic zones. See [`LuaEntity`](LUA_API.md#world--entities) and [`LuaForce`](LUA_API.md#research--recipes). **(CPX 3)**
- [ ] **Combat Tools** — Attack entities, manage turrets, check enemy positions, defensive operations. See [`LuaEntity`](LUA_API.md#world--entities) and [`LuaSurface`](LUA_API.md#world--entities). **(CPX 3)**
- [ ] **Train Management Tools** — Control trains, manage stations, set schedules, inspect train networks. See [`LuaEntity`](LUA_API.md#world--entities) for train/station entities. **(CPX 3)**
- [ ] **Logistics Flow Tracking** — Given a specific entity, trace the full tree of linked entities through belts and inserters, including flow direction. Track miner output positions and directions as flow starting points. Record which entity outputs to which, enabling the AI to understand how items move through the factory, plan logistics, debug crafting chains, and ensure belts feed into chests so items don't pile up. Create MCP tools to query and visualize the item flow graph. **(CPX 4)**

### ON HOLD

- [ ] **Helper Factorio Mod** — Optional Lua mod installed in Factorio that exposes `remote.call()` interfaces for advanced async state tracking (crafting completion events, walking arrival detection, pathfinding). Only needed if RCON polling proves too limited. See [`LUA_API.md` events](LUA_API.md#key-events) and [RCON notes](LUA_API.md#rcon-specific-notes). **(CPX 5)**


---

## Improvements

### High Priority

*No high priority items*

### Medium Priority

*No medium priority items*

### Low Priority

- [ ] **Multiplayer Player Targeting** — All Lua commands use `game.connected_players[1]` which is unsafe in multiplayer: wrong player if host changes slot order, breaks with multiple clients on headless server. Should use a configurable player index or name-based lookup. Affects PathfindingService, FactorioService, MiningService, and all tool Lua snippets. **(CPX 3)**

### ON HOLD

*No items on hold*

---

## Documentation

### High Priority

*No high priority items*

### Medium Priority

- [ ] **Condense PROMPT.md** — Rewrite the AI prompt file to be 300 lines max while preserving all essential instructions and context. Prefer high level functions instead of low level primitives, and remove any redundant or obvious information. **(CPX 2)**

### Low Priority

- [ ] **Keep TOOLS.md in sync with code** — Add a process note (e.g. in PROMPT.md or a contributing guide) to update TOOLS.md whenever a new MCP tool is added or an existing one is modified, so documentation stays accurate. **(CPX 1)**

### On Hold

*No items on hold*

---

## Code Cleanup & Technical Debt

### Code Refactoring

#### High Priority

*No high priority items*

#### Medium Priority

*No medium priority items*

#### Low Priority

- [ ] **Lua `script.on_event` handler overwrite risk** — `EnsurePathHandlerInstalledAsync` registers `on_script_path_request_finished` via `script.on_event()` which replaces any existing handler. If another mod or script registers the same event, navigation breaks silently. No handler chaining or isolation. Low risk in single-mod RCON usage but fragile for extensibility. **(CPX 1)**
- [ ] **`nav_results` TTL cleanup assumes stable tick rate** — Path results older than 600 ticks (~10 seconds) are deleted in `GetPathResultAsync`. Under server lag, pathfinder overload, or backlog spikes, valid results can be cleaned up prematurely before they are polled. Consider using wall-clock timestamps or increasing the TTL margin. **(CPX 1)**

---

## Known Issues / Bugs

### Active Bugs

*No active bugs*

### Uncategorized (Analyze and create TODO entries in above appropriate sections with priority. Do not fix or implement them just yet. Assign complexity points where applicable. Do not delete this section when you are done, just empty it)

*No uncategorized items*

### Rejected / Not Applicable

The following reported issues were investigated and found to be **not bugs** in the Factorio 2 context:

- ~~`execute_lua` example uses Factorio 1.x API (`game.table_to_json`)~~ — Investigated: no `game.table_to_json()` reference exists anywhere in the codebase — not in tool descriptions, tool code, prompt files, or documentation. `LUA_API.md` correctly documents `table_to_json()` under the `LuaHelpers` class (accessed via `helpers.table_to_json()` in Factorio 2). Not a code bug.
- ~~`string.Create(CultureInfo.InvariantCulture, $$"""...""")` misuse~~
- ~~`{{"{"}}{...}{{"}"}}` produces invalid Lua~~ — In `$$"""` raw strings, this correctly produces `{{...}}` which is valid Lua table-of-tables for area construction. Not a bug.
- ~~`invert=true` in `find_entities_filtered`~~ — Verified in Factorio 2 API docs: `EntitySearchFilters` has an `invert` boolean field ("Whether the filters should be inverted"). Valid API usage.
- ~~`player.crafting_queue` is invalid~~ — Verified in Factorio 2 API: `LuaPlayer.crafting_queue` returns `array[CraftingQueueItem]?`. Valid property.
- ~~`get_contents()` returns `{name = count}` dict~~ — In Factorio 2, `LuaInventory.get_contents()` returns `array[ItemWithQualityCount]` where each item has `.name`, `.quality`, `.count` fields. The `item.name` and `item.count` access pattern is correct.
- ~~`e.type == "assembling-machine"` is wrong~~ — `assembling-machine` IS the prototype type for all assembling machines (1/2/3 are entity names). Valid check.
- ~~C# ternary `{{(reverse ? "true" : "false")}}` in raw strings~~ — Works correctly in `$$"""` raw string interpolation. Compiles and produces valid Lua.
- ~~`TransferAllItemsAsync` inventory iteration with `for i = 1, #inv`~~ — Factorio inventories support numeric indexing and `#inv` returns the slot count. The `stack.valid_for_read` guard correctly skips empty slots.
- ~~`#inv` for slot count reporting~~ — `#inv` returns the inventory size (number of slots) which is the intended value for slot count display.
- ~~Unsafe `defines.direction.{{direction}}`~~ — Direction values are validated in C# with `ArgumentException.ThrowIfNullOrWhiteSpace` and come from controlled MCP tool parameters, not arbitrary user input.
- ~~RCON polling too slow for Factorio tick rate (50ms vs 16.67ms)~~ — `PollInterval` is already 50ms (~3x per tick). Polling faster adds RCON round-trip overhead without benefit since `GameCommandQueue` serializes all operations. The polling loop checks position and issues direction commands — going faster than 50ms would flood RCON with no meaningful improvement in path-following accuracy.
- ~~Global state coupling (`_pathHandlerInstalled`, `_lastDirection`) across concurrent calls~~ — `GameCommandQueue` uses `SemaphoreSlim(1,1)` to serialize all MCP tool calls. Only one `WalkToAsync` can execute at a time, so `_lastDirection` and `storage.walk_dir` cannot be corrupted by concurrent access.
- ~~`_lastDirection` not preventing RCON spam / missing Lua `walk_changed` counter~~ — The `if (_lastDirection != direction)` guard in `FollowWaypointsAsync` already prevents redundant RCON calls. `SetWalkingDirectionAsync` sets `_lastDirection` before the RCON call. The suggested `storage.walk_changed` counter would never be read by any code — the on_tick handler simply reads `storage.walk_dir` each tick regardless of a change counter. Direction oscillation in tight corners is a real concern, but is caused by `CalculateDirection` sector boundaries, not missing spam prevention — the guard works correctly.
- ~~`waypoints[segIndex + 1]` index out-of-bounds risk~~ — Line 153 checks `if (segIndex >= waypoints.Count - 1)` and returns "arrived" before line 179 accesses `waypoints[segIndex + 1]`. The guard guarantees `segIndex + 1` is always a valid index. `AdvanceSegment` cannot return a value exceeding `waypoints.Count - 1` due to its loop condition `while (segIndex < waypoints.Count - 1)`. Degenerate paths (1-segment, duplicate endpoints) are handled by `ProjectOntoSegment` returning 1.0 for zero-length segments, which advances `segIndex` to `Count - 1` and triggers the arrival check. Not a bug.
- ~~Nullable `_rcon` used without null-check in `ValidateBuildingMemoryAsync`~~ — The method already has `if (_rcon is null)` guard at the top returning a structured error. Not a bug.
- ~~`GetProperty()` calls in `MiningService.MineResourceAsync` throw on malformed JSON~~ — The `GetProperty` calls on lines 151-152 are only reached after a `success=true` check, and the Lua scripts always include all expected properties in their JSON output. The actual failure mode (RCON returning a Lua error string) would crash at `JsonDocument.Parse` before any `GetProperty` call — the same pattern used throughout the entire codebase. Not a targeted bug.
- ~~`WaitForCraftingAsync` empty-queue check fails on whitespace inside `[]`~~ — The Lua script in `GetCraftingQueueAsync` hardcodes `'{"queue":[]}'` for empty queues — no whitespace inside brackets. The output format is fully controlled by our code. Not a real failure path.

---

## Testing

### High Priority

*No high priority items*

### Medium Priority

*No medium priority items*

### Low Priority

*No low priority items*

---

## Design Principles

- Prefer high-level tools over primitives
- Avoid forcing LLM to do geometry
- Minimize number of tool calls
- Return structured data (not text)

## Notes

- This is for Factorio 2, not Factorio 1. Some API calls and behaviors may differ from Factorio 1. Keep the Factorio 2 Lua API docs handy — see [`LUA_API.md`](LUA_API.md) for the bundled local reference.
- Try to edit files and use tools WITHOUT POWERSHELL where possible, shell scripts get stuck and then manually terminate. Do not use powershell commands unless absolutely necessary
- Do not be afraid to break backwards compatibility if new changes will simplify or improve the project
- When implementing or modifying Lua scripts, reference [`LUA_API.md`](LUA_API.md) and the bundled `LuaAPI/` HTML docs to verify correct API calls, parameter names, and return types. All current Lua snippets have been audited and confirmed correct against the Factorio API docs.
- Problem solutions need to be optimized, performant and well thought out before implementation, avoid quick fixes
- Keep files below 1000 lines, split when they get too large. Either partial classes or split into multiple smaller classes that handle a single functionality.
- **Realistic AI constraint**: The AI player must not teleport, spawn items, or skip crafting time. It should walk to locations, wait for crafting to finish, and only interact with entities within range. This is a core design principle.
- **RCON is the bridge**: All game control goes through RCON → Lua. The [Factorio Lua API](https://lua-api.factorio.com/latest/) is the authoritative reference; see [`LUA_API.md`](LUA_API.md) for the local copy and [RCON-specific notes](LUA_API.md#rcon-specific-notes).
- **RCON player resolution**: `game.player` is `nil` in RCON context. All Lua scripts use `game.connected_players[1]` which works for both singleplayer and dedicated server RCON. For future multiplayer support, a player index/name parameter would be needed. See [`LUA_API.md` RCON notes](LUA_API.md#rcon-specific-notes).
- Factorio installation folder is located in `E:\Games\Factorio2`

---

## Completed

See [DONE.md](DONE.md) for completed items.

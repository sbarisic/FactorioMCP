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
| MCP Tools | ✅ Functional | Movement, inventory/crafting, entity placement/mining, entity interaction (insert/remove items, inspect), world scanning, proximity checking, chat message, goal planning, energy management, research, building memory, task primitives (gather/refuel/smelt) tools exposed via MCP SDK |
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

- [ ] **Smart Inserter Placement** — `place_inserter(target_x, target_y, side, direction)`: accept a target entity and a side (top/bottom/left/right) plus direction (inbound/outbound). C# backend calculates the exact tile offset based on entity size. Also `insert_between(source_pos, destination_pos)` to auto-place an inserter in the 1-tile gap between two entities with correct orientation. **(CPX 3)**
- [ ] **Ghost Placement Validation** — LLM places ghost entities first, C# validates placement (prototype, orientation, connectivity). Returns corrective errors like "Inserter at {10,11} is pointing at a wall, not the Assembler" to create a feedback loop before committing real entities. **(CPX 3)**


### Medium Priority

- [ ] **Factory Analysis Tools** — `GetProductionStatus`: what's being produced, bottlenecks, idle machines. `FindUnpoweredEntities`: list entities without power. `FindIdleMachines`: list machines not working. `FindMissingInputs(x, y)`: returns which input items an assembler/furnace is missing. Essential for AI to understand and debug factory state. **(CPX 3)**
- [ ] **Logistics Flow Tracking** — Given a specific entity, trace the full tree of linked entities through belts and inserters, including flow direction. Track miner output positions and directions as flow starting points. Record which entity outputs to which, enabling the AI to understand how items move through the factory, plan logistics, debug crafting chains, and ensure belts feed into chests so items don't pile up. Create MCP tools to query and visualize the item flow graph. **(CPX 4)**
- [ ] **Craft & Factory Planning** — `PlanCraft(item, count)`: returns full recipe tree with required intermediates and raw materials. `PlanFactory(goal)`: given a high-level goal like "automate iron plates", return rough ordered steps. Reduces LLM hallucinated plans. **(CPX 3)**
- [ ] **Vision Screenshot** — `take_screenshot()`: return a base64 screenshot image for vision models to identify bottlenecks or plan layouts. Overlay entity bounds, directional indicators (inserter drop/pickup positions as absolute coordinates), and metadata as a "Map Legend" to give the image depth/context. **(CPX 3)**
- [ ] **Power Network Topology** — Trace how electricity flows through the physical network from producers (boilers, solar panels) through electric poles to consumers. Map the pole connectivity graph and show which entities are powered by which network segment. Complements existing `GetElectricNetwork` (aggregate stats) and `InspectEntityPower` (per-entity) with topological awareness for planning expansions and diagnosing coverage gaps. **(CPX 3)**
- [ ] **Inventory Intelligence** — `EnsureItem(item, count)`: auto-crafts or gathers if the player doesn't have enough. `GetInventorySummary`: returns condensed key-value inventory (fewer tokens than full inventory dump). **(CPX 2)**

### Low Priority

- [ ] **Pickup Items** — `pickup_items(radius)`: simulate holding the 'F' key to collect items dropped on the ground within a radius. **(CPX 1)**
- [ ] **Collision Slot Query** — `get_available_slots(x, y)`: return a list of adjacent tiles around an entity that aren't blocked by pipes or buildings (collision masking). **(CPX 2)**
- [ ] **Smart Entity Placement** — `PlaceEntitySmart(entity, near)` e.g. place a stone-furnace near iron-ore. Backend picks best available position automatically. **(CPX 2)**
- [ ] **Utility Tools** — `GetReachableEntities(type, max_distance)`: filter entities by reach distance. `CountItemInWorld(item)`: count item across all containers, not just player inventory. `EstimateTravelTime(x, y)`: estimate walk time to a position. **(CPX 2)**
- [ ] **Logistics Tools** — Manage logistic robots, request items from logistic network, inspect logistic zones. See [`LuaEntity`](LUA_API.md#world--entities) and [`LuaForce`](LUA_API.md#research--recipes). **(CPX 3)**
- [ ] **Combat Tools** — Attack entities, manage turrets, check enemy positions, defensive operations. See [`LuaEntity`](LUA_API.md#world--entities) and [`LuaSurface`](LUA_API.md#world--entities). **(CPX 3)**
- [ ] **Train Management Tools** — Control trains, manage stations, set schedules, inspect train networks. See [`LuaEntity`](LUA_API.md#world--entities) for train/station entities. **(CPX 3)**

### ON HOLD

- [ ] **Helper Factorio Mod** — Optional Lua mod installed in Factorio that exposes `remote.call()` interfaces for advanced async state tracking (crafting completion events, walking arrival detection, pathfinding). Only needed if RCON polling proves too limited. See [`LUA_API.md` events](LUA_API.md#key-events) and [RCON notes](LUA_API.md#rcon-specific-notes). **(CPX 5)**


---

## Improvements

### High Priority

*No high priority items*

### Medium Priority

*No medium priority items*

### Low Priority

- [ ] **Multiplayer Player Targeting**

### ON HOLD

*No items on hold*

---

## Documentation

### High Priority

*No high priority items*

### Medium Priority

- [ ] **Condense PROMPT.md** — Rewrite the AI prompt file to be 300 lines max while preserving all essential instructions and context. **(CPX 2)**

### Low Priority

*No low priority items*

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

*No low priority items*

---

## Known Issues / Bugs

### Active Bugs

- [ ] **Stale Building Memory After Manual Removal** — Building memory tracks entities placed by the AI but has no way to detect when entities are removed outside the MCP tools (e.g. manually by a player, destroyed by enemies, or removed by other mods). `GetAllBuildings`/`GetClosestBuildingOfType`/`GetBuildingsNear` return phantom entries for entities that no longer exist in the world, causing `InspectEntity` and `MoveToBuilding` to walk to empty positions. Needs a validation/reconciliation mechanism — either periodic world verification (scan tracked positions for actual entities), on-demand validation before returning results, or a `ValidateBuildingMemory` tool that prunes stale entries. **(CPX 3)**

### Uncategorized (Analyze and create TODO entries in above appropriate sections with priority. Do not fix or implement them just yet. Assign complexity points where applicable. Do not delete this section when you are done, just empty it)

- [ ] **RCON First-Call Returns `"nothing"` After Server Restart** — Intermittently, the first RCON command after MCP server restart returns the literal string `"nothing"` instead of expected JSON output from `rcon.print()`. Subsequent identical calls succeed immediately. Observed with `GetPlayerPosition` but may affect any first command. `GetGameTick` called in parallel at the same time succeeded, suggesting it's timing-related rather than a connection issue. Possibly caused by `InitializeChatListenerAsync` (the startup Lua command) interfering with subsequent response reads, or a warm-up delay in the RCON connection. **(CPX 2)**

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

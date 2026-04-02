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
| MCP Tools | ✅ Functional | Movement, inventory/crafting, entity placement/mining, entity interaction (insert/remove items, inspect), world scanning, proximity checking, chat message, goal planning, energy management, research, building memory tools exposed via MCP SDK |
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

- [ ] **Blueprint & Ghost Support** — Place blueprints or ghost entities for planned construction. Manage blueprint books. See [`LuaRecord`](LUA_API.md#rcon--game) for blueprint string operations. **(CPX 4)**

### Medium Priority

- [ ] **Inventory Management Tools** — Drop items, swap items, transfer between inventories, and other inventory operations beyond crafting. See [`LuaInventory`/`LuaControl`](LUA_API.md#player--control) and [`defines.inventory`](LUA_API.md#key-defines). **(CPX 2)**
- [ ] **Remote Area Scanning Tools** — Query entities, resources, and terrain at arbitrary map coordinates instead of only near the player. Extends existing scan tools (`GetNearbyEntities`, `ScanResources`, `ScanTiles`) with x/y center parameters for long-range planning without walking. See [`LuaSurface`](LUA_API.md#world--entities) for `find_entities_filtered()`/`find_tiles_filtered()`. **(CPX 2)**

### Low Priority

- [ ] **Logistics Tools** — Manage logistic robots, request items from logistic network, inspect logistic zones. See [`LuaEntity`](LUA_API.md#world--entities) and [`LuaForce`](LUA_API.md#research--recipes). **(CPX 3)**
- [ ] **Combat Tools** — Attack entities, manage turrets, check enemy positions, defensive operations. See [`LuaEntity`](LUA_API.md#world--entities) and [`LuaSurface`](LUA_API.md#world--entities). **(CPX 3)**
- [ ] **Train Management Tools** — Control trains, manage stations, set schedules, inspect train networks. See [`LuaEntity`](LUA_API.md#world--entities) for train/station entities. **(CPX 3)**

### ON HOLD

- [ ] **Helper Factorio Mod** — Optional Lua mod installed in Factorio that exposes `remote.call()` interfaces for advanced async state tracking (crafting completion events, walking arrival detection, pathfinding). Only needed if RCON polling proves too limited. See [`LUA_API.md` events](LUA_API.md#key-events) and [RCON notes](LUA_API.md#rcon-specific-notes). **(CPX 5)**
- [ ] **MCP Resources (Read-Only State)** — Expose game state as MCP Resources (production stats, map info, recipe database) using `[McpServerResource]` for passive context without tool calls. See [`LuaFlowStatistics`](LUA_API.md#energy), [`LuaForce`](LUA_API.md#research--recipes), and [`LuaGameScript`](LUA_API.md#rcon--game). **(CPX 3)**

---

## Improvements

### High Priority

- [ ] **Command Queuing & Sequencing** — Queue commands to avoid race conditions when the AI sends multiple commands rapidly. Ensure one Lua command completes before the next is sent. **(CPX 3)**

### Medium Priority

*No medium priority items*

### Low Priority

- [ ] **RCON Multi-Packet Response Handling**
- [ ] **Multiplayer Player Targeting** — Support specifying which player to control in multiplayer games instead of always using `game.player` (which is only valid in singleplayer RCON context). Use `game.players[name]` or `game.connected_players`. See [`LuaGameScript`](LUA_API.md#rcon--game) and [RCON notes](LUA_API.md#rcon-specific-notes). **(CPX 3)**

### ON HOLD

*No items on hold*

---

## Documentation

### High Priority

*No high priority items*

### Medium Priority

*No medium priority items*

### Low Priority

- [ ] **LM Studio Setup Guide** — Add LM Studio connection instructions to the README as a recommended local-model setup. Include default model suggestion (`qwen/qwen3-vl-4b`), MCP client configuration, and any LM Studio-specific notes for connecting to the stdio MCP server. **(CPX 1)**
- [ ] **Architecture Decision Records** — Document key decisions: why RCON over mod API, why realistic AI constraints, why stdio transport. **(CPX 1)**

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

*No active bugs*

### Uncategorized (Analyze and create TODO entries in above appropriate sections with priority. Do not fix or implement them just yet. Assign complexity points where applicable. Do not delete this section when you are done, just empty it)

*No uncategorized items*

---

## Testing

### High Priority

*No high priority items*

### Medium Priority

*No medium priority items*

### Low Priority

- [ ] **RCON Integration Tests (Manual)** — Tests that connect to a real Factorio RCON instance. Marked as `[Explicit]`/skipped by default so they don't run in CI. Validates auth, command execution, and error handling against the real game. **(CPX 2)**

---

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

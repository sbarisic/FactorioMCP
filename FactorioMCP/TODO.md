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
- **Factorio side**: Connects via Source RCON protocol over TCP to send Lua commands (`/c ...`) using the [Factorio Lua API](https://lua-api.factorio.com/latest/).
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
| MCP Tools | 🔶 Partial/WIP | Movement, inventory/crafting, entity placement/mining, world scanning, and proximity checking tools exposed via MCP SDK |
| MCP Hosting | ✅ Functional | Program.cs wiring with DI, configuration, stdio transport |
| Realistic Behaviors | 🔶 Partial/WIP | Walking with real physics, crafting with real queue, proximity validation on place/mine — no cheating |

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
| `FACTORIO_RCON_PASSWORD` | *(required)* | RCON password |

---

## Features

### High Priority

*No high priority items*

### Medium Priority

- [ ] **Wait / Polling Mechanisms** — Wait for crafting queue to empty, wait until player reaches a target position (within tolerance), wait for a specified game tick count. These enable realistic pacing without the AI spamming commands. **(CPX 3)**

### Low Priority

- [ ] **Research Tools** — Start research, get available technologies, get research progress. **(CPX 2)**
- [ ] **Raw Lua Execution Tool** — Execute arbitrary Lua code for advanced operations not covered by specific tools. Include safety warning in tool description. **(CPX 1)**
- [ ] **Recipe & Technology Query Tools** — Query available recipes, recipe ingredients/products, technology prerequisites. Helps the AI plan crafting chains. **(CPX 2)**
- [ ] **Map / Terrain Scanning** — Scan tiles around the player for resource patches (iron, copper, stone, coal, oil), water, terrain type. Helps the AI decide where to build. **(CPX 3)**
- [ ] **Blueprint & Ghost Support** — Place blueprints or ghost entities for planned construction. **(CPX 4)**

### ON HOLD

- [ ] **Helper Factorio Mod** — Optional Lua mod installed in Factorio that exposes `remote.call()` interfaces for advanced async state tracking (crafting completion events, walking arrival detection, pathfinding). Only needed if RCON polling proves too limited. **(CPX 5)**
- [ ] **MCP Resources (Read-Only State)** — Expose game state as MCP Resources (production stats, map info, recipe database) using `[McpServerResource]` for passive context without tool calls. **(CPX 3)**

---

## Improvements

### High Priority

*No high priority items*

### Medium Priority

*No medium priority items*

### Low Priority

- [ ] **RCON Multi-Packet Response Handling** — Handle responses that span multiple RCON packets (responses > 4096 bytes). Send an empty follow-up packet to detect end of multi-packet response. **(CPX 2)**
- [ ] **Multiplayer Player Targeting** — Support specifying which player to control in multiplayer games instead of always using `game.player` (which is only valid in singleplayer RCON context). Use `game.players[name]` or `game.connected_players`. **(CPX 3)**
- [ ] **Command Queuing & Sequencing** — Queue commands to avoid race conditions when the AI sends multiple commands rapidly. Ensure one Lua command completes before the next is sent. **(CPX 3)**

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

- [ ] **MCP Tool Integration Tests** — Verify tools resolve from DI, accept correct parameters, and call the expected service methods. **(CPX 3)**

### Low Priority

- [ ] **RCON Integration Tests (Manual)** — Tests that connect to a real Factorio RCON instance. Marked as `[Explicit]`/skipped by default so they don't run in CI. Validates auth, command execution, and error handling against the real game. **(CPX 2)**

---

## Notes

- Try to edit files and use tools WITHOUT POWERSHELL where possible, shell scripts get stuck and then manually terminate. Do not use powershell commands unless absolutely necessary
- Do not be afraid to break backwards compatibility if new changes will simplify or improve the project
- Problem solutions need to be optimized, performant and well thought out before implementation, avoid quick fixes
- Keep files below 1000 lines, split when they get too large. Either partial classes or split into multiple smaller classes that handle a single functionality.
- **Realistic AI constraint**: The AI player must not teleport, spawn items, or skip crafting time. It should walk to locations, wait for crafting to finish, and only interact with entities within range. This is a core design principle.
- **RCON is the bridge**: All game control goes through RCON → Lua. The [Factorio Lua API](https://lua-api.factorio.com/latest/) is the authoritative reference for available operations.
- **Singleplayer RCON caveat**: In singleplayer, `game.player` refers to the local player. In multiplayer/dedicated server, use `game.players[index]` or `game.connected_players`.
- Factorio installation folder is located in `E:\Games\Factorio2`

---

## Completed

See [DONE.md](DONE.md) for completed items.

# FactorioMCP — Completed Items

Items completed from the [TODO list](TODO.md).

---

## Features

- [x] **RCON Client (CPX 2)** — Source RCON protocol over TCP in `Rcon/RconClient.cs` and `Rcon/RconPacket.cs`. Supports authentication, command execution, and Lua execution via `/c` prefix. Uses `BinaryPrimitives` for little-endian packet framing, async I/O throughout, and proper cancellation token support.
- [x] **Factorio Service Layer (CPX 3)** — `Services/FactorioService.cs` wrapping RCON Lua calls with typed methods: Walk, StopWalking, GetPlayerPosition, GetInventory, Craft, GetCraftingQueue, PlaceEntity, MineEntityAt, GetNearbyEntities, GetResearchStatus, ExecuteRawLua. Uses `string.Create` with `InvariantCulture` for safe double formatting in Lua scripts.
- [x] **MCP Server Hosting & Wiring (CPX 2)** — Program.cs rewritten with Generic Host, DI registration of RconClient/FactorioService, `RconConnectionService` hosted service for RCON connection on startup from environment variables, MCP server with stdio transport and assembly-scanned tool discovery. Added `ModelContextProtocol` and `Microsoft.Extensions.Hosting` packages.
- [x] **Movement Tools (CPX 2)** — `Tools/MovementTools.cs` with MCP tools: `WalkForDuration` (walk direction + seconds then stop, returns position), `StopWalking`, `GetPlayerPosition`. Uses real physics-based walking via `player.walking_state`.
- [x] **Inventory & Crafting Tools (CPX 2)** — `Tools/InventoryTools.cs` with MCP tools: `GetInventory`, `Craft` (real crafting queue via `player.begin_crafting`), `GetCraftingQueue`.
- [x] **Entity Placement & Mining Tools (CPX 2)** — `Tools/EntityTools.cs` with MCP tools: `PlaceEntity` (place from inventory at coordinates with direction, validates inventory and position), `MineEntity` (mine entity at coordinates into inventory).
- [x] **World Scanning Tools (CPX 2)** — `Tools/WorldTools.cs` with MCP tools: `GetNearbyEntities` (entities within radius), `GetResearchStatus` (current research and progress). `GetPlayerPosition` already available in MovementTools.
- [x] **Proximity Checks (CPX 2)** — `PlaceEntityAsync` validates `build_distance`, `MineEntityAtAsync` validates `reach_distance` before executing. Added `CheckDistanceAsync` service method and `CheckDistance` MCP tool in WorldTools for pre-flight range checks. Returns clear "Out of range" errors with distances when player is too far.
- [x] **Wait / Polling Mechanisms (CPX 3)** — `Tools/WaitTools.cs` with MCP tools: `WaitForCrafting` (poll until queue empties), `WaitForPosition` (poll until player reaches target within tolerance), `WaitForTicks` (poll until game ticks elapse), `GetGameTick` (current tick). All use configurable poll interval and timeout with JSON status responses (`complete`/`arrived`/`timeout`). Added `ScriptedRconClient` test double for polling tests. 20 new tests (98 total). TOOLS.md updated with new Wait & Polling section.
- [x] **Movement Pathfinding & Obstacle Avoidance (CPX 4)** — Player gets stuck when walking into entities. Implemented stuck detection + automatic detour in the Lua `on_tick` walking handler. Tracks player position each tick; after 10 ticks with no movement, switches to a perpendicular direction for 15 ticks to navigate around the obstacle, then resumes the original direction. Alternates detour sides (left/right) on repeated stuck events. Uses `global.walk_state` for state persistence. `WalkForDuration` MCP tool now also reports a `"stuck"` status with a warning when the player fails to move at all. Updated `WalkAndStop_MovesPlayer` integration test to try multiple directions. Added 2 new unit tests (`WalkAsync_IncludesStuckDetectionLogic`, `StopWalkingAsync_ClearsWalkState`).

---

## Improvements

- [x] **RCON Reconnection & Resilience (CPX 2)** — `RconClient` now stores connection parameters, recreates `TcpClient` on connection loss, and retries with exponential backoff (up to 3 attempts). `ExecuteAsync` catches `IOException`/`SocketException`/`ObjectDisposedException` and auto-reconnects before retrying the command. `RconConnectionService` retries startup connection up to 5 times with backoff and logs warnings via `ILogger`. Thread-safe via `SemaphoreSlim`.
- [x] **Structured Lua Responses (CPX 2)** — All `FactorioService` methods now return JSON via `rcon.print()` instead of ad-hoc text. Responses use flat keys (e.g. `{"success":true,"entity":"stone-furnace","x":5,"y":-2}`) for reliable AI parsing. Error responses include structured `error` codes (`out_of_range`, `invalid_position`, `missing_item`, `no_entity`). TOOLS.md updated with all JSON response examples.
- [x] **MCP Tool Call Logging (CPX 2)** — Added `McpRequestFilter<CallToolRequestParams, CallToolResult>` in `Program.cs` via `WithRequestFilters` + `AddCallToolFilter`. Logs tool name and JSON-serialized arguments to stderr on invocation and completion. Uses `Console.Error` to avoid interfering with MCP stdio transport on stdout. Zero per-tool changes — all tools are covered automatically by the SDK filter pipeline.
- [x] **RCON Client Connection Logging (CPX 2)** — Added `ILogger<RconClient>` to `RconClient` with optional constructor parameter (defaults to `NullLogger` for test compatibility). Logs: initial connection attempts (Debug), successful authentication (Information), connection loss during command execution (Warning with exception), each reconnection attempt with attempt/max count (Information), reconnection success (Information), per-attempt failures with backoff timing (Warning), final reconnection failure (Error), and connection close events (Debug).

---

## Documentation

- [x] **README.md (CPX 1)** — Project overview, prerequisites, setup instructions, env var configuration, how to run, MCP client connection examples (Claude Desktop, VS Code Copilot), available tools reference, project structure.
- [x] **Available Tools Reference (CPX 1)** — `TOOLS.md` with detailed documentation for all 15 MCP tools: parameters, types, return values, example outputs, and AI prompt engineering tips. Linked from README.
- [x] **Lua API Audit** — Verified all Lua snippets in `FactorioService.cs` against bundled Factorio API HTML docs. Confirmed correct: LuaPlayer (walking_state, build_distance, reach_distance, begin_crafting, crafting_queue, get_main_inventory, get_item_count, remove_item), LuaSurface (find_entities_filtered, can_place_entity, create_entity), LuaForce (current_research, research_progress), LuaEntity (mine), LuaRCON (print), defines (direction). All calls match the API reference.
- [x] **Example mcp.json (CPX 1)** — Added `.vscode/mcp.json` pre-configured with `dotnet run --project` command and all default environment variables (`FACTORIO_RCON_HOST`, `FACTORIO_RCON_PORT`, `FACTORIO_RCON_PASSWORD`). Uses `${workspaceFolder}` for portable project path. Updated README VS Code section to reference the bundled file with manual setup in collapsible details.

---

## Bug Fixes

- [x] **RCON `game.player` is nil fix** — Discovered via live testing that `game.player` returns `nil` in RCON context (both singleplayer and dedicated server). Replaced all 12 Lua snippets in `FactorioService.cs` to use `game.connected_players[1]` which correctly resolves the first connected player. All service methods (Walk, StopWalking, GetPlayerPosition, GetInventory, Craft, GetCraftingQueue, PlaceEntity, MineEntity, GetNearbyEntities, CheckDistance, GetResearchStatus, WaitForPosition) now work against a live Factorio instance. Added 14 live integration tests in `LiveGameTests.cs` confirming all tools function correctly.
- [x] **`MineNearbyResource_AddsToInventory` integration test fix** — Test picked the first mineable entity (not closest), walked once without re-checking distance, and crashed on empty RCON response. Fixed to pick the closest entity, retry walking up to 5 times with distance re-checks, and handle empty mine responses gracefully.
- [x] **`MineEntityAtAsync` Factorio 2 API fix** — `entity.mine{inventory=player.get_main_inventory()}` fails in Factorio 2 (`"inventory" must be a script inventory or entity inventory`). Switched to `player.mine_entity(e, true)` which correctly mines as-if-player, handles inventory transfer automatically, and raises proper game events (`on_player_mined_entity`, etc.).
- [x] **`WalkAndStop_MovesPlayer` Flaky Test Fix** — Integration test failed when the player was blocked by nearby entities. Updated to try multiple directions (south, east, north, west) and succeed if the player moves in any of them. Proper diagnostic output shows how far the player moved in each attempt.

---

## Testing

- [x] **Test Project Setup (CPX 1)** — `FactorioMCP.Tests` project with xUnit, project reference to `FactorioMCP`, `InternalsVisibleTo` for test access.
- [x] **RCON Packet Serialization Tests (CPX 2)** — Extracted `ToBytes()` and `FromPayload()` serialization methods onto `RconPacket`, refactored `RconClient` to use them. 20 unit tests covering wire format (byte order, sizes, null terminators), all three packet types (Auth/ExecCommand/ResponseValue), round-trip serialization, UTF-8 bodies, edge cases (empty body, negative id, large body, too-small payload).
- [x] **FactorioService Lua Command Tests (CPX 2)** — 58 unit tests in `FactorioServiceTests.cs` verifying all service methods produce correct Lua commands. Uses `CapturingRconClient` test double (overrides virtual `ExecuteAsync`) to capture commands without TCP. Tests cover: correct Lua API calls, JSON output structure, parameter interpolation with InvariantCulture, direction/recipe handling, proximity/inventory validation inclusion, argument validation (null, whitespace, zero, negative), and `/c` prefix on all commands. Made `RconClient.ExecuteAsync` virtual and unsealed the class for testability.

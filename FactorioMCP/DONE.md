# FactorioMCP — Completed Items

Items completed from the [TODO list](TODO.md).

---

## Features

- [x] **RCON Client (CPX 2)** — Source RCON protocol over TCP in `Rcon/RconClient.cs` and `Rcon/RconPacket.cs`. Supports authentication, command execution, and Lua execution via `/c` prefix. Uses `BinaryPrimitives` for little-endian packet framing, async I/O throughout, and proper cancellation token support.
- [x] **Factorio Service Layer (CPX 3)** — `Services/FactorioService.cs` wrapping RCON Lua calls with typed methods: Walk, StopWalking, GetPlayerPosition, GetInventory, Craft, GetCraftingQueue, PlaceEntity, MineEntityAt, GetNearbyEntities, GetResearchStatus, ExecuteRawLua. Uses `string.Create` with `InvariantCulture` for safe double formatting in Lua scripts.
- [x] **MCP Server Hosting & Wiring (CPX 2)** — Program.cs rewritten with Generic Host, DI registration of RconClient/FactorioService, `RconConnectionService` hosted service for RCON connection on startup from environment variables, MCP server with stdio transport and assembly-scanned tool discovery. Added `ModelContextProtocol` and `Microsoft.Extensions.Hosting` packages.
- [x] **Movement Tools (CPX 2)** — `Tools/MovementTools.cs` with MCP tools: `WalkForDuration` (walk direction + seconds then stop, returns position), `StopWalking`, `GetPlayerPosition`. Uses real physics-based walking via `player.walking_state`.
- [x] **Inventory & Crafting Tools (CPX 2)** — `Tools/InventoryTools.cs` with MCP tools: `GetInventory`, `Craft` (real crafting queue via `player.begin_crafting`), `GetCraftingQueue`.

---

## Testing

- [x] **Test Project Setup (CPX 1)** — `FactorioMCP.Tests` project with xUnit, project reference to `FactorioMCP`, `InternalsVisibleTo` for test access.

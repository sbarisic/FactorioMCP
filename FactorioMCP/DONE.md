# FactorioMCP — Completed Items

Items completed from the [TODO list](TODO.md).

---

## Features

- [x] **RCON Client (CPX 2)** — Source RCON protocol over TCP in `Rcon/RconClient.cs` and `Rcon/RconPacket.cs`. Supports authentication, command execution, and Lua execution via `/c` prefix. Uses `BinaryPrimitives` for little-endian packet framing, async I/O throughout, and proper cancellation token support.
- [x] **Factorio Service Layer (CPX 3)** — `Services/FactorioService.cs` wrapping RCON Lua calls with typed methods: Walk, StopWalking, GetPlayerPosition, GetInventory, Craft, GetCraftingQueue, PlaceEntity, MineEntityAt, GetNearbyEntities, GetResearchStatus, ExecuteRawLua. Uses `string.Create` with `InvariantCulture` for safe double formatting in Lua scripts.

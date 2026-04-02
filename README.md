# FactorioMCP

An MCP (Model Context Protocol) server that connects to a running Factorio game via RCON and lets an AI agent play the game realistically — walking, crafting, building — without cheating.

```
LLM (Claude/GPT/etc) ←→ [MCP Server (C#, stdio)] ←→ [RCON TCP] ←→ Factorio Game
```

## Key Principle

The AI player **does not cheat**. It walks to locations using real game physics, waits for crafting to finish, and only interacts with entities within range. No teleportation, no spawning items, no skipping time.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Factorio](https://factorio.com/) (any edition that supports RCON)

## Setup

### 1. Launch Factorio with RCON

**Singleplayer:**
```sh
factorio.exe --rcon-port 27015 --rcon-password mypassword
```

**Dedicated server:**
```sh
factorio --start-server save.zip --rcon-port 27015 --rcon-password mypassword
```

### 2. Configure Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `FACTORIO_RCON_HOST` | `127.0.0.1` | RCON server host |
| `FACTORIO_RCON_PORT` | `27015` | RCON server port |
| `FACTORIO_RCON_PASSWORD` | `mypassword` | RCON password |
| `FACTORIO_GOALS_FILE` | `goals.json` | File path for goal planner persistence |
| `FACTORIO_BUILDINGS_FILE` | `buildings.json` | File path for building memory persistence |

### 3. Build

```sh
dotnet build
```

## Connecting an MCP Client

### Claude Desktop

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "factorio": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/FactorioMCP"],
      "env": {
        "FACTORIO_RCON_PASSWORD": "mypassword"
      }
    }
  }
}
```

### VS Code (GitHub Copilot)

A pre-configured [`.vscode/mcp.json`](.vscode/mcp.json) is included in the repository. Open the workspace in VS Code and it will be detected automatically. Adjust the environment variables if needed.

<details>
<summary>Manual setup</summary>

Add to your `.vscode/mcp.json`:

```json
{
  "servers": {
    "factorio": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "${workspaceFolder}/FactorioMCP"],
      "env": {
        "FACTORIO_RCON_HOST": "127.0.0.1",
        "FACTORIO_RCON_PORT": "27015",
        "FACTORIO_RCON_PASSWORD": "mypassword"
      }
    }
  }
}
```

</details>

## Available Tools

> **Full reference**: See [TOOLS.md](TOOLS.md) for detailed parameters, return values, and AI prompt engineering tips.

### Movement

| Tool | Description |
|------|-------------|
| `WalkForDuration` | Walk in a direction for N seconds, then stop. Returns position after walking. |
| `StopWalking` | Stop the player from walking immediately. |
| `GetPlayerPosition` | Get the player's current map position. |

### Inventory & Crafting

| Tool | Description |
|------|-------------|
| `GetInventory` | List all items and counts in the player's main inventory. |
| `Craft` | Begin crafting items using a recipe. Respects real crafting time. |
| `GetCraftingQueue` | Get the current crafting queue contents. |

### Entity Placement & Mining

| Tool | Description |
|------|-------------|
| `PlaceEntity` | Place an entity from inventory at map coordinates (with direction). Automatically tracked in building memory. |
| `MineEntity` | Mine/remove an entity at map coordinates into inventory. Automatically untracked from building memory. |

### Entity Interaction

| Tool | Description |
|------|-------------|
| `InsertItems` | Insert items from player inventory into a machine (fuel, ore, etc.). |
| `RemoveItems` | Remove items from a machine's inventory into player inventory. |
| `InspectEntity` | Inspect entity status, inventories, fuel, recipe, and health. |

### World Scanning

| Tool | Description |
|------|-------------|
| `GetNearbyEntities` | List all entities near the player within a given radius. |
| `CheckDistance` | Check distance to coordinates and whether within build/reach range. |
| `ScanResources` | Scan for resource patches (ores, oil) with type summaries and center coordinates. |
| `ScanTiles` | Scan terrain tiles for type counts (grass, sand, water, etc.). |

### Wait & Polling

| Tool | Description |
|------|-------------|
| `WaitForCrafting` | Wait for the crafting queue to empty. |
| `WaitForPosition` | Wait until the player reaches target coordinates. |
| `WaitForTicks` | Wait for a specified number of game ticks to elapse. |
| `GetGameTick` | Get the current game tick. |

### Chat

| Tool | Description |
|------|-------------|
| `InitializeChatListener` | Initialize/re-initialize the chat message event listener. |
| `GetChatMessages` | Get chat messages from the in-game chat log (with polling support). |
| `SendChatMessage` | Send an `[AI]`-tagged chat message visible to all players. |

### Goal Planning

| Tool | Description |
|------|-------------|
| `SetGoal` | Create a new goal with optional ordered steps. |
| `GetActiveGoal` | Get the currently active goal with step details. |
| `GetAllGoals` | Get a summary of all goals (completed, failed, suspended, pending). |
| `AdvanceGoalStep` | Mark the current step as completed and advance. |
| `AddGoalSteps` | Add new steps to the active goal. |
| `CompleteGoal` | Mark the active goal as completed. |
| `FailGoal` | Mark the active goal as failed with a reason. |
| `SuspendGoal` | Suspend the active goal to work on something urgent. |
| `ResumeGoal` | Resume a previously suspended goal. |

### Energy

| Tool | Description |
|------|-------------|
| `GetElectricNetwork` | Get electric network stats: production, consumption, satisfaction, accumulators. |
| `InspectEntityPower` | Inspect per-entity power status: connection, energy, buffer, drain. |

### Research

| Tool | Description |
|------|-------------|
| `GetResearchStatus` | Get current research technology and progress percentage. |
| `GetAvailableTechnologies` | List all technologies available for research (prerequisites met). |
| `StartResearch` | Start researching a technology. |

### Recipe & Technology Queries

| Tool | Description |
|------|-------------|
| `GetRecipeDetails` | Get recipe ingredients, products, crafting time, and category. |
| `GetAvailableRecipes` | List all currently unlocked recipes. |
| `GetTechnologyDetails` | Get technology prerequisites, effects, cost, and ingredients. |

### Building Memory

| Tool | Description |
|------|-------------|
| `GetAllBuildings` | Get all tracked buildings with names, positions, directions, and labels. |
| `GetBuildingsNear` | Get tracked buildings near a position within a radius. |
| `FindBuildingsByType` | Find all tracked buildings of a specific entity type. |
| `GetBuildingSummary` | Get counts of each tracked building type. |
| `UpdateBuildingLabel` | Set or remove a label on a tracked building. |
| `ClearBuildingMemory` | Clear all tracked buildings from AI memory (not from game world). |

### Advanced

| Tool | Description |
|------|-------------|
| `ExecuteLua` | Execute arbitrary Lua code via RCON. Use for advanced operations not covered by other tools. |

## Project Structure

```
FactorioMCP/
├── Program.cs                          # Host setup, DI, MCP server wiring
├── Rcon/
│   ├── RconClient.cs                   # Source RCON protocol over TCP
│   └── RconPacket.cs                   # RCON packet types and record
├── Models/
│   ├── Goal.cs                         # Goal/step data model
│   └── TrackedBuilding.cs              # Building memory data model
├── Services/
│   ├── FactorioService.cs              # High-level game operations via Lua
│   ├── EnergyService.cs                # Electric network inspection via Lua
│   ├── GoalPlannerService.cs           # Goal tracking with JSON persistence
│   ├── BuildingMemoryService.cs        # Building tracking with JSON persistence
│   ├── GameCommandQueue.cs             # Serializes concurrent MCP tool calls
│   └── RconConnectionService.cs        # RCON connection on startup
└── Tools/
    ├── MovementTools.cs                # Walk, stop, get position
    ├── InventoryTools.cs               # Inventory, crafting
    ├── EntityTools.cs                  # Place/mine entities
    ├── InteractionTools.cs             # Insert/remove items, inspect entities
    ├── WorldTools.cs                   # Nearby entities, distance, scan
    ├── WaitTools.cs                    # Wait for crafting, position, ticks
    ├── ChatTools.cs                    # Chat message listener, send/receive
    ├── GoalTools.cs                    # Goal planning and tracking
    ├── EnergyTools.cs                  # Electric network and power inspection
    ├── ResearchTools.cs                # Research status, start research
    ├── RecipeTools.cs                  # Recipe and technology queries
    ├── BuildingTools.cs                # Building memory queries
    └── LuaTools.cs                     # Raw Lua execution

FactorioMCP.Tests/                      # Unit tests (xUnit)
```

## License

See repository for license details.
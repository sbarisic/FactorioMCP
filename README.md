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
| `PlaceEntity` | Place an entity from inventory at map coordinates (with direction). |
| `MineEntity` | Mine/remove an entity at map coordinates into inventory. |

### World Scanning

| Tool | Description |
|------|-------------|
| `GetNearbyEntities` | List all entities near the player within a given radius. |
| `CheckDistance` | Check distance to coordinates and whether within build/reach range. |
| `GetResearchStatus` | Get current research technology and progress percentage. |

## Project Structure

```
FactorioMCP/
├── Program.cs                          # Host setup, DI, MCP server wiring
├── Rcon/
│   ├── RconClient.cs                   # Source RCON protocol over TCP
│   └── RconPacket.cs                   # RCON packet types and record
├── Services/
│   ├── FactorioService.cs              # High-level game operations via Lua
│   └── RconConnectionService.cs        # RCON connection on startup
└── Tools/
    ├── MovementTools.cs                # MCP movement tools
    ├── InventoryTools.cs               # MCP inventory/crafting tools
    ├── EntityTools.cs                  # MCP entity placement/mining tools
    └── WorldTools.cs                   # MCP world scanning tools

FactorioMCP.Tests/                      # Unit tests (xUnit)
```

## License

See repository for license details.
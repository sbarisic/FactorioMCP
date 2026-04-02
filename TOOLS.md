# FactorioMCP — Available Tools Reference

All MCP tools currently exposed by the FactorioMCP server. Use this as a reference for AI agent prompt engineering and client integration.

> **Realistic constraint**: All tools respect normal game mechanics. The player walks with real physics, crafting takes real time, and no items are spawned or teleported.

---

## Movement Tools

### `WalkForDuration`

Walk in a direction for a specified duration, then stop automatically.

| Parameter | Type | Description |
|-----------|------|-------------|
| `direction` | `string` | Direction to walk: `north`, `south`, `east`, `west`, `northeast`, `northwest`, `southeast`, `southwest` |
| `seconds` | `double` | Duration to walk in seconds (e.g. `2.5`) |

**Returns**: JSON with player position after walking, e.g.:
```json
{"status":"walking","direction":"north","x":12.5,"y":-3.75}
```

**Example prompt usage**:
> "Walk north for 3 seconds to get closer to the iron ore patch."

---

### `StopWalking`

Stop the player from walking immediately.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: `"Stopped walking."`

Note: The underlying walk/stop commands also return JSON position data, but the tool returns a fixed string.

---

### `GetPlayerPosition`

Get the player's current map position.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: JSON position, e.g.:
```json
{"x":12.5,"y":-3.75}
```

**Example prompt usage**:
> "Check my current position before deciding which direction to walk."

---

## Inventory & Crafting Tools

### `GetInventory`

List all items and their counts in the player's main inventory.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: JSON object with items array, e.g.:
```json
{"items":[{"name":"iron-plate","count":50},{"name":"copper-plate","count":30},{"name":"iron-gear-wheel","count":10}]}
```

**Example prompt usage**:
> "Check what materials I have before deciding what to craft."

---

### `Craft`

Begin crafting items using a recipe. The items are queued in the real crafting queue — the player must wait for them to finish.

| Parameter | Type | Description |
|-----------|------|-------------|
| `recipe` | `string` | Recipe name (e.g. `iron-gear-wheel`, `electronic-circuit`, `transport-belt`) |
| `count` | `int` | Number of items to craft |

**Returns**: JSON with crafting result, e.g.:
```json
{"status":"crafting","recipe":"iron-gear-wheel","requested":5,"queued":5}
```
The `queued` count may be less than `requested` if ingredients are insufficient.

**Example prompt usage**:
> "Craft 10 iron gear wheels so I can build an assembling machine."

---

### `GetCraftingQueue`

Get the player's current crafting queue showing what is being crafted and how many remain.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: JSON object with queue array, e.g.:
```json
{"queue":[{"recipe":"iron-gear-wheel","count":5},{"recipe":"electronic-circuit","count":3}]}
```
Returns `{"queue":[]}` when the queue is empty.

**Example prompt usage**:
> "Check if crafting is done before trying to place the assembling machine."

---

## Entity Placement & Mining Tools

### `PlaceEntity`

Place an entity from the player's inventory at the specified map coordinates. Validates proximity (must be within build distance), inventory contents, and position validity before placing.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `entityName` | `string` | *(required)* | Entity/item name (e.g. `stone-furnace`, `transport-belt`, `assembling-machine-1`) |
| `x` | `double` | *(required)* | X coordinate on the map |
| `y` | `double` | *(required)* | Y coordinate on the map |
| `direction` | `string` | `"north"` | Direction the entity faces: `north`, `south`, `east`, `west`, `northeast`, `northwest`, `southeast`, `southwest` |

**Returns**: JSON with success status:
```json
{"success":true,"entity":"stone-furnace","x":5,"y":-2}
```
Error responses:
```json
{"success":false,"error":"out_of_range","distance":12.3,"limit":6}
{"success":false,"error":"invalid_position","entity":"stone-furnace","x":5,"y":-2}
{"success":false,"error":"missing_item","entity":"stone-furnace"}
```

**Example prompt usage**:
> "Place a stone furnace at coordinates 5, -2 facing north."

---

### `MineEntity`

Mine/remove an entity at the specified map coordinates. Validates proximity (must be within reach distance) before mining. Mined items are added to the player's inventory.

| Parameter | Type | Description |
|-----------|------|-------------|
| `x` | `double` | X coordinate of the entity to mine |
| `y` | `double` | Y coordinate of the entity to mine |

**Returns**: JSON with success status:
```json
{"success":true,"entity":"stone-furnace"}
```
Error responses:
```json
{"success":false,"error":"out_of_range","distance":8.5,"limit":6}
{"success":false,"error":"no_entity","x":5,"y":-2}
```

**Example prompt usage**:
> "Mine the entity at coordinates 5, -2 to pick it up."

---

## World Scanning Tools

### `GetNearbyEntities`

Get a list of all entities near the player within a given radius.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `10` | Search radius around the player in tiles |

**Returns**: JSON object with entities array, e.g.:
```json
{"entities":[{"name":"iron-ore","x":3.5,"y":-1.5},{"name":"stone-furnace","x":5,"y":-2},{"name":"transport-belt","x":6,"y":-2}]}
```

**Example prompt usage**:
> "Scan nearby entities within 20 tiles to find iron ore."

---

### `CheckDistance`

Check the distance from the player to target map coordinates and whether the target is within build range (for placing) and reach range (for mining/interacting). Use this before `PlaceEntity` or `MineEntity` to verify the player is close enough.

| Parameter | Type | Description |
|-----------|------|-------------|
| `x` | `double` | X coordinate of the target position |
| `y` | `double` | Y coordinate of the target position |

**Returns**: JSON distance report, e.g.:
```json
{"distance":4.2,"build_in_range":true,"build_limit":6,"reach_in_range":true,"reach_limit":6}
```
When out of range:
```json
{"distance":9.1,"build_in_range":false,"build_limit":6,"reach_in_range":false,"reach_limit":6}
```

**Example prompt usage**:
> "Check if I'm close enough to coordinates 5, -2 before placing a furnace."

---

### `ScanResources`

Scan for resource patches (ores, oil, etc.) within a radius of the player. Returns a summary of each resource type found.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `50` | Search radius around the player in tiles |

**Returns**: JSON with resource summaries:
```json
{"scan_radius":50,"resources":[{"name":"iron-ore","patches":42,"total_amount":8400,"center_x":15.5,"center_y":-8.0},{"name":"copper-ore","patches":28,"total_amount":5600,"center_x":-12.0,"center_y":3.5}]}
```

**Example prompt usage**:
> "Scan for resources within 100 tiles to find the nearest iron ore patch."

---

### `ScanTiles`

Scan tiles around the player to get terrain information. Returns a summary of tile types and their counts.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `16` | Search radius around the player in tiles |

**Returns**: JSON with tile type counts:
```json
{"scan_radius":16,"tiles":[{"name":"grass-1","count":320},{"name":"dirt-1","count":85},{"name":"water","count":15}]}
```

**Example prompt usage**:
> "Check the terrain around me to find a good flat area for building."

---

## Wait & Polling Tools

### `WaitForCrafting`

Wait for the crafting queue to empty. Polls the queue periodically until all items are crafted or the timeout is reached.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `pollIntervalSeconds` | `double` | `1.0` | How often to check the queue in seconds |
| `timeoutSeconds` | `double` | `60` | Maximum time to wait in seconds before giving up |

**Returns**: JSON with completion status:
```json
{"status":"complete","queue":[]}
```
On timeout:
```json
{"status":"timeout","remaining":{"queue":[{"recipe":"iron-gear-wheel","count":3}]}}
```

**Example prompt usage**:
> "Craft 20 iron gear wheels and wait for them to finish."

---

### `WaitForPosition`

Wait until the player reaches a target position within a given tolerance. Polls the player's position periodically. The player must already be walking.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `targetX` | `double` | *(required)* | Target X coordinate to reach |
| `targetY` | `double` | *(required)* | Target Y coordinate to reach |
| `tolerance` | `double` | `2.0` | Arrived when within this many tiles of the target |
| `pollIntervalSeconds` | `double` | `0.5` | How often to check position in seconds |
| `timeoutSeconds` | `double` | `30` | Maximum time to wait in seconds before giving up |

**Returns**: JSON with arrival status:
```json
{"status":"arrived","tolerance":2,"position":{"x":9.8,"y":-1.2,"distance":0.28}}
```
On timeout:
```json
{"status":"timeout","target_x":10,"target_y":-1,"position":{"x":5.2,"y":3.1}}
```

**Example prompt usage**:
> "Walk north and wait until I reach coordinates 10, -1."

---

### `WaitForTicks`

Wait for a specified number of game ticks to elapse. Factorio runs at 60 ticks per second at normal speed.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ticks` | `int` | *(required)* | Number of game ticks to wait (60 = 1 second at 1x speed) |
| `pollIntervalSeconds` | `double` | `0.5` | How often to check the tick count in seconds |
| `timeoutSeconds` | `double` | `30` | Maximum real-time seconds to wait before giving up |

**Returns**: JSON with completion status:
```json
{"status":"complete","start_tick":1000,"end_tick":1060,"elapsed":60}
```
On timeout:
```json
{"status":"timeout","start_tick":1000,"current_tick":1020,"target_tick":1060}
```

**Example prompt usage**:
> "Wait 300 ticks (5 seconds) for the furnace to smelt some iron."

---

### `GetGameTick`

Get the current game tick. Useful for measuring elapsed time between operations.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: JSON with current tick:
```json
{"tick":12345}
```

**Example prompt usage**:
> "Check the current game tick to measure how long smelting takes."

---

## Entity Interaction Tools

### `InsertItems`

Insert items from the player's inventory into a machine/entity at the specified position. Use this to fuel burner entities, load furnaces with ore, or stock assembling machines.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | *(required)* | X coordinate of the target entity |
| `y` | `double` | *(required)* | Y coordinate of the target entity |
| `itemName` | `string` | *(required)* | Item name to insert (e.g. `coal`, `iron-ore`, `copper-plate`) |
| `count` | `int` | *(required)* | Number of items to insert |
| `inventoryType` | `string` | `"fuel"` | Target inventory: `fuel`, `furnace_source`, `furnace_result`, `chest`, `assembling_machine_input`, `assembling_machine_output` |

**Returns**: JSON with success status:
```json
{"success":true,"entity":"stone-furnace","item":"coal","inserted":5,"requested":5}
```
Error responses:
```json
{"success":false,"error":"out_of_range","distance":8.5,"limit":6}
{"success":false,"error":"no_entity","x":5,"y":-2}
{"success":false,"error":"no_items","item":"coal","available":0}
{"success":false,"error":"no_inventory","entity":"transport-belt","inventory_type":"fuel"}
```

**Example prompt usage**:
> "Insert 10 coal into the furnace at 5, -2 as fuel."

---

### `RemoveItems`

Remove items from a machine/entity's inventory into the player's inventory. Use this to collect smelted plates from furnaces, take crafted items from assemblers, or empty chests.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | *(required)* | X coordinate of the target entity |
| `y` | `double` | *(required)* | Y coordinate of the target entity |
| `itemName` | `string` | *(required)* | Item name to remove (e.g. `iron-plate`, `copper-plate`) |
| `count` | `int` | *(required)* | Number of items to remove |
| `inventoryType` | `string` | `"furnace_result"` | Source inventory: `fuel`, `furnace_source`, `furnace_result`, `chest`, `assembling_machine_input`, `assembling_machine_output` |

**Returns**: JSON with success status:
```json
{"success":true,"entity":"stone-furnace","item":"iron-plate","removed":10,"requested":10}
```

**Example prompt usage**:
> "Collect all iron plates from the furnace at 5, -2."

---

### `InspectEntity`

Inspect an entity at the specified position to see its status, inventory contents, fuel level, recipe, and other details.

| Parameter | Type | Description |
|-----------|------|-------------|
| `x` | `double` | X coordinate of the entity to inspect |
| `y` | `double` | Y coordinate of the entity to inspect |

**Returns**: JSON with entity details:
```json
{"success":true,"entity":"stone-furnace","type":"furnace","position":{"x":5,"y":-2},"status":"working","health":200,"max_health":200,"inventories":{"fuel":[{"name":"coal","count":3}],"furnace_source":[{"name":"iron-ore","count":12}],"furnace_result":[{"name":"iron-plate","count":8}]},"burner":{"remaining_burning_fuel":1200.0,"heat":165.0,"heat_capacity":200.0}}
```

**Example prompt usage**:
> "Check the status of the furnace at 5, -2 to see if it has fuel and ore."

---

## Chat Tools

### `InitializeChatListener`

Initialize the chat message listener. Registers an event handler that captures in-game chat messages. Called automatically on startup — can be called again to re-initialize if lost after a game reload.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: JSON with status:
```json
{"status":"initialized","existing_messages":0}
```

---

### `GetChatMessages`

Get chat messages from the in-game chat log. Use the `sinceTick` parameter to only get new messages since the last poll.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `sinceTick` | `long` | `0` | Only return messages after this game tick. Pass `latest_tick` from a previous response to get only new ones. |

**Returns**: JSON with messages:
```json
{"messages":[{"tick":4200,"player":"player1","message":"hello AI"}],"count":1,"latest_tick":4200}
```

**Example prompt usage**:
> "Check for new chat messages from the player."

---

### `SendChatMessage`

Send a chat message visible to all connected players. Automatically tagged with `[AI]`.

| Parameter | Type | Description |
|-----------|------|-------------|
| `message` | `string` | The message text to send |

**Returns**: JSON with status:
```json
{"status":"sent"}
```

**Example prompt usage**:
> "Tell the player I'm going to start building the smelting area."

---

## Goal Planning Tools

Goals help the AI track what it's working toward, maintain progress through ordered steps, and resume after interruptions. Only one goal can be active at a time. Goals persist across server restarts.

### `SetGoal`

Create a new goal with a description and optional ordered steps. If no goal is currently active, the new goal is automatically activated.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `description` | `string` | *(required)* | What the goal aims to achieve (e.g. `Build iron smelting setup`) |
| `steps` | `List<string>` | `null` | Optional ordered list of steps (e.g. `["Mine stone", "Craft furnace", "Place furnace"]`) |

**Example prompt usage**:
> "Set a goal to build an iron smelting setup with steps: mine stone, craft furnace, place furnace, add fuel."

---

### `GetActiveGoal`

Get the currently active goal with full step details. Use this to check what to work on, especially after an interruption.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

---

### `GetAllGoals`

Get a summary of all goals including completed, failed, suspended, and pending goals.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

---

### `AdvanceGoalStep`

Mark the current step as completed and advance to the next step.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `notes` | `string` | `null` | Optional notes about what was accomplished |

---

### `AddGoalSteps`

Add new steps to the end of the active goal's step list.

| Parameter | Type | Description |
|-----------|------|-------------|
| `steps` | `List<string>` | Step descriptions to add |

---

### `CompleteGoal`

Mark the active goal as completed. Remaining in-progress steps are marked completed and pending steps are skipped.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `notes` | `string` | `null` | Optional notes about the completed goal |

---

### `FailGoal`

Mark the active goal as failed with a reason.

| Parameter | Type | Description |
|-----------|------|-------------|
| `reason` | `string` | Reason why the goal failed |

---

### `SuspendGoal`

Suspend the active goal to work on something more urgent. Progress is preserved for later resumption.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

---

### `ResumeGoal`

Resume a previously suspended goal. No other goal can be currently active.

| Parameter | Type | Description |
|-----------|------|-------------|
| `goalId` | `string` | The ID of the suspended goal to resume (from `GetAllGoals`) |

---

## Energy Tools

### `GetElectricNetwork`

Get electric network statistics from the nearest electric pole within a radius. Returns power production and consumption rates (watts), satisfaction percentage, and accumulator charge levels.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `50` | Search radius around the player to find electric poles |

**Returns**: JSON with network statistics:
```json
{"status":"ok","network_id":1,"pole":"small-electric-pole","pole_x":10.5,"pole_y":-3.5,"total_production_watts":900.0,"total_consumption_watts":600.0,"satisfaction_percent":100.0,"accumulator_count":0,"accumulator_charge_joules":0.0,"accumulator_capacity_joules":0.0,"accumulator_charge_percent":0.0,"producers":[{"name":"steam-engine","watts":900.0}],"consumers":[{"name":"assembling-machine-1","watts":600.0}]}
```
When no poles found:
```json
{"status":"no_poles_found","player_x":12.5,"player_y":-3.5,"radius":50}
```

**Example prompt usage**:
> "Check the electric network to see if I have enough power for another assembling machine."

---

### `InspectEntityPower`

Inspect the power status of an entity at specific coordinates. Returns network connection, energy stored, buffer size, drain, and generation rate.

| Parameter | Type | Description |
|-----------|------|-------------|
| `x` | `double` | X coordinate of the entity |
| `y` | `double` | Y coordinate of the entity |

**Returns**: JSON with power details:
```json
{"status":"ok","name":"assembling-machine-1","type":"assembling-machine","x":10.0,"y":-5.0,"connected_to_network":true,"network_id":1,"energy_joules":500.0,"buffer_size_joules":5000.0,"charge_percent":10.0,"drain_watts":30.0}
```

**Example prompt usage**:
> "Check why the assembling machine at 10, -5 isn't working — is it connected to power?"

---

## Research Tools

### `GetResearchStatus`

Get the current research status and progress for the player's force.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: JSON research status:
```json
{"researching":true,"technology":"automation","progress":0.452}
```
When no research is active:
```json
{"researching":false}
```

**Example prompt usage**:
> "Check research progress before deciding what to do next."

---

### `GetAvailableTechnologies`

Get all technologies currently available for research — enabled, not yet researched, and with all prerequisites met.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: JSON with available technologies:
```json
{"technologies":[{"name":"automation","cost":10,"ingredients":[{"name":"automation-science-pack","count":1}]},{"name":"logistics","cost":20,"ingredients":[{"name":"automation-science-pack","count":1}]}],"count":2}
```

**Example prompt usage**:
> "What technologies can I research right now?"

---

### `StartResearch`

Start researching a technology by adding it to the research queue. Begins immediately if no research is in progress.

| Parameter | Type | Description |
|-----------|------|-------------|
| `technology` | `string` | Technology name (e.g. `automation`, `logistics`, `steel-processing`) |

**Returns**: JSON with success status:
```json
{"success":true,"technology":"automation","cost":10,"ingredients":[{"name":"automation-science-pack","count":1}]}
```
Error responses:
```json
{"success":false,"error":"unknown_technology","technology":"invalid-tech"}
{"success":false,"error":"already_researched","technology":"automation"}
```

**Example prompt usage**:
> "Start researching automation so I can unlock assembling machines."

---

## Recipe & Technology Query Tools

### `GetRecipeDetails`

Get details about a specific recipe — ingredients, products, crafting time, and category.

| Parameter | Type | Description |
|-----------|------|-------------|
| `recipe` | `string` | Recipe name (e.g. `iron-gear-wheel`, `electronic-circuit`, `transport-belt`) |

**Returns**: JSON with recipe details:
```json
{"success":true,"name":"iron-gear-wheel","enabled":true,"energy":0.5,"category":"crafting","ingredients":[{"type":"item","name":"iron-plate","amount":2}],"products":[{"type":"item","name":"iron-gear-wheel","amount":1,"probability":1}]}
```

**Example prompt usage**:
> "What ingredients do I need to craft transport belts?"

---

### `GetAvailableRecipes`

Get all recipes currently available (unlocked) for crafting. Returns each recipe's name, category, and crafting time.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: JSON with recipes:
```json
{"recipes":[{"name":"iron-gear-wheel","category":"crafting","energy":0.5},{"name":"electronic-circuit","category":"crafting","energy":0.5}],"count":2}
```

**Example prompt usage**:
> "What recipes can I currently craft?"

---

### `GetTechnologyDetails`

Get details about a specific technology — prerequisites, effects (recipe unlocks), research cost, and required science packs.

| Parameter | Type | Description |
|-----------|------|-------------|
| `technology` | `string` | Technology name (e.g. `automation`, `logistics`, `steel-processing`) |

**Returns**: JSON with technology details:
```json
{"success":true,"name":"automation","researched":false,"enabled":true,"cost":10,"prerequisites":[],"effects":[{"type":"unlock-recipe","recipe":"assembling-machine-1"}],"ingredients":[{"name":"automation-science-pack","count":1}]}
```

**Example prompt usage**:
> "What does the automation technology unlock?"

---

## Building Memory Tools

Buildings are automatically tracked when placed via `PlaceEntity` and untracked when mined via `MineEntity`. These tools query the AI's building memory for factory awareness.

### `GetAllBuildings`

Get all buildings the AI has placed, with their names, positions, directions, and labels.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Example prompt usage**:
> "Show me all buildings I've placed to review the factory layout."

---

### `GetBuildingsNear`

Get buildings near a specific position within a radius, sorted by distance.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | *(required)* | X coordinate of the center position |
| `y` | `double` | *(required)* | Y coordinate of the center position |
| `radius` | `double` | `20` | Search radius in tiles |

**Example prompt usage**:
> "What buildings are near coordinates 10, -5?"

---

### `FindBuildingsByType`

Find all buildings of a specific entity type.

| Parameter | Type | Description |
|-----------|------|-------------|
| `entityName` | `string` | Entity name to search for (e.g. `stone-furnace`, `transport-belt`) |

**Example prompt usage**:
> "Where are all my stone furnaces?"

---

### `GetBuildingSummary`

Get a summary of all building types and their counts.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Example prompt usage**:
> "How many of each building type have I placed?"

---

### `UpdateBuildingLabel`

Set or update a label on a building at the specified position. Labels help identify building purpose.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | *(required)* | X coordinate of the building |
| `y` | `double` | *(required)* | Y coordinate of the building |
| `label` | `string?` | *(required)* | Label text (e.g. `iron smelter #1`), or `null` to remove |

**Example prompt usage**:
> "Label the furnace at 5, -2 as 'iron smelter #1'."

---

### `ClearBuildingMemory`

Clear all tracked buildings from memory. Does NOT remove buildings from the game world — only from the AI's memory.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

---

## Lua Tools

### `ExecuteLua`

Execute arbitrary Lua code on the Factorio game instance via RCON. Use `rcon.print()` to return data. The player is accessed via `game.connected_players[1]`.

| Parameter | Type | Description |
|-----------|------|-------------|
| `luaCode` | `string` | Lua code to execute |

> ⚠️ **WARNING**: Executes code directly on the game server with no sandboxing. Incorrect Lua can corrupt game state or crash the server. Prefer specific tools when available.

**Example prompt usage**:
> "Run a custom Lua script to check the player's character inventory bonus slots."

---

## Tips for AI Agent Prompt Engineering

- **Always check position** before walking. Use `GetPlayerPosition` to know where the player is, then calculate which direction and how long to walk.
- **Check distance before interacting**. Use `CheckDistance` to verify you're within build/reach range before calling `PlaceEntity`, `MineEntity`, `InsertItems`, `RemoveItems`, or `InspectEntity`. Walk closer if out of range.
- **Check inventory before crafting or placing**. Use `GetInventory` to verify ingredients or items before calling `Craft` or `PlaceEntity`.
- **Wait for crafting to finish**. After calling `Craft`, use `WaitForCrafting` to block until the queue empties before using crafted items.
- **Wait for arrival**. After `WalkForDuration`, use `WaitForPosition` to confirm the player reached the destination.
- **Use tick-based waits for game mechanics**. Use `WaitForTicks` for furnace smelting, inserter cycles, or other game-tick-dependent operations (60 ticks = 1 second at normal speed).
- **Scan before placing**. Use `GetNearbyEntities` to see what's around, `ScanResources` to find ore patches, and `ScanTiles` to check terrain.
- **Use goals for multi-step tasks**. `SetGoal` with steps keeps track of progress. Call `AdvanceGoalStep` after each step, and `SuspendGoal`/`ResumeGoal` for interruptions.
- **Monitor power before expanding**. Use `GetElectricNetwork` before adding machines. Use `InspectEntityPower` to debug unpowered entities.
- **Look up recipes before crafting**. Use `GetRecipeDetails` to check ingredients, and `GetAvailableRecipes` to see what's unlocked.
- **Use building memory**. `GetBuildingsNear` and `FindBuildingsByType` help avoid duplicates and plan layouts.
- **Walking is physics-based**. Short durations (0.5–1s) give fine-grained movement; longer durations (3–5s) cover more ground.
- **Directions are cardinal + diagonal**. All eight compass directions: `north`, `south`, `east`, `west`, `northeast`, `northwest`, `southeast`, `southwest`.
- **Interact with entities properly**. Use `InsertItems` to load machines, `RemoveItems` to collect output, and `InspectEntity` to check status before and after.
- **Communicate with players**. Use `SendChatMessage` to explain what you're doing, and `GetChatMessages` to listen for player instructions.

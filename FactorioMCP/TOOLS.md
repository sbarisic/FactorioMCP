# FactorioMCP — Available Tools Reference

All MCP tools currently exposed by the FactorioMCP server. Use this as a reference for AI agent prompt engineering and client integration.

> **Realistic constraint**: All tools respect normal game mechanics. The player walks with real physics, crafting takes real time, and no items are spawned or teleported.

---

## Movement Tools

### `WalkToPosition`

Walk toward a target position until arrival, getting stuck, or timeout. Automatically calculates the best walking direction from the player's current position, polls and re-adjusts course periodically. Includes automatic obstacle avoidance. Returns status: `arrived`, `stuck`, or `timeout` with final position and distance.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `targetX` | `double` | *(required)* | Target X coordinate to walk toward |
| `targetY` | `double` | *(required)* | Target Y coordinate to walk toward |
| `tolerance` | `double` | `2.0` | Arrived when within this many tiles of the target |
| `pollIntervalSeconds` | `double` | `0.5` | How often to check position and adjust direction in seconds |
| `timeoutSeconds` | `double` | `30` | Maximum time to walk in seconds before giving up |

**Returns**: JSON with arrival status:
```json
{"status":"arrived","x":19.8,"y":0.1,"target_x":20,"target_y":0,"distance":0.22,"tolerance":2.0}
```
When stuck:
```json
{"status":"stuck","x":5.2,"y":3.1,"target_x":20,"target_y":0,"distance":15.1,"tolerance":2.0}
```
On timeout:
```json
{"status":"timeout","x":12.5,"y":-1.2,"target_x":20,"target_y":0,"distance":7.6,"tolerance":2.0}
```

---

### `StopWalking`

Stop the player from walking immediately.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: `"Stopped walking."`

---

### `GetPlayerPosition`

Get the player's current map position.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: JSON position:
```json
{"x":12.5,"y":-3.75}
```

---

## Inventory & Crafting Tools

### `GetInventory`

List all items and their counts in the player's main inventory, plus capacity information.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: JSON with items, total slots, and free slots:
```json
{"items":[{"name":"iron-plate","count":50},{"name":"copper-plate","count":30}],"total_slots":80,"free_slots":65}
```

---

### `Craft`

Begin crafting items using a recipe. Items are queued in the real crafting queue — the player must wait for them to finish.

| Parameter | Type | Description |
|-----------|------|-------------|
| `recipe` | `string` | Recipe name (e.g. `iron-gear-wheel`, `electronic-circuit`) |
| `count` | `int` | Number of items to craft |

**Returns**: JSON with crafting result:
```json
{"status":"crafting","recipe":"iron-gear-wheel","requested":5,"queued":5}
```
Error responses:
```json
{"status":"no_materials","recipe":"iron-gear-wheel","requested":5,"queued":0}
{"status":"error","error":"unknown_recipe"}
```

---

### `GetCraftingQueue`

Get the player's current crafting queue showing what is being crafted and how many remain.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: JSON with queue:
```json
{"queue":[{"recipe":"iron-gear-wheel","count":5}]}
```

---

### `DropItems`

Drop items from the player's inventory onto the ground at the player's position. Items can be picked up later.

| Parameter | Type | Description |
|-----------|------|-------------|
| `itemName` | `string` | Item name to drop (e.g. `iron-plate`, `wood`, `coal`) |
| `count` | `int` | Number of items to drop |

**Returns**: JSON with drop result.

---

### `TransferAllItems`

Bulk transfer all items from an entity's inventory into the player's inventory. Stops early if inventory is full.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | *(required)* | X coordinate of the entity |
| `y` | `double` | *(required)* | Y coordinate of the entity |
| `inventoryType` | `string` | `"chest"` | Source inventory: `fuel`, `furnace_source`, `furnace_result`, `chest`, `assembling_machine_input`, `assembling_machine_output` |

**Returns**: JSON with transfer results including `inventory_full` flag.

---

### `GetEntityInventory`

Get the contents of a specific entity's inventory at a position. Returns all items, counts, total slots, and empty slots.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | *(required)* | X coordinate of the entity |
| `y` | `double` | *(required)* | Y coordinate of the entity |
| `inventoryType` | `string` | `"chest"` | Inventory to inspect: `fuel`, `furnace_source`, `furnace_result`, `chest`, `assembling_machine_input`, `assembling_machine_output` |

**Returns**: JSON with inventory contents.

---

## Building & Mining Tools

### `PlaceEntity`

Place an entity from the player's inventory at the specified map coordinates. Validates proximity (must be within build distance), inventory contents, and position validity. Automatically tracked in building memory.

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

---

### `MineEntity`

Mine/remove a non-resource entity (building) at the specified map coordinates. Must be within reach range. Mined items go to the player's inventory. Automatically removed from building memory. For mining resource entities (ore patches), use `MineResource` instead.

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
{"success":false,"error":"use_mine_resource","entity":"iron-ore","amount":100,"message":"Resource entities must be mined with the MineResource tool for realistic mining duration"}
```

---

### `MineResource`

Mine resource entities (ore patches) with realistic timing. The player character mines one unit at a time using normal game mechanics — no instant extraction. Specify how many units to mine and the tool will start mining, wait for the specified amount to be extracted, then stop. Returns the actual number mined.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | *(required)* | X coordinate of the resource entity to mine |
| `y` | `double` | *(required)* | Y coordinate of the resource entity to mine |
| `count` | `int` | `1` | Number of resource units to mine |
| `pollIntervalSeconds` | `double` | `0.5` | How often to check mining progress in seconds |
| `timeoutSeconds` | `double` | `60` | Maximum time to wait for mining in seconds |

**Returns**: JSON with mining results:
```json
{"success":true,"status":"complete","entity":"iron-ore","mined":5,"requested":5,"remaining":95,"depleted":false}
```
When resource is fully depleted:
```json
{"success":true,"status":"depleted","entity":"iron-ore","mined":3,"requested":10,"remaining":0,"depleted":true}
```
On timeout:
```json
{"success":true,"status":"timeout","entity":"iron-ore","mined":2,"requested":5,"remaining":98,"depleted":false}
```
Error responses:
```json
{"success":false,"error":"out_of_range","distance":12.3,"limit":6}
{"success":false,"error":"no_resource","x":5,"y":5}
```

---

### `PreviewInserterPlacement`

Preview what an inserter would pick up from and drop to if placed at the given position and direction. Does NOT place anything — purely informational for planning inserter layouts. An inserter **picks up from the OPPOSITE side** of its direction and **drops to the side it faces**. Use this before placing an inserter to verify it will connect the correct source and destination entities.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | *(required)* | X coordinate where the inserter would be placed |
| `y` | `double` | *(required)* | Y coordinate where the inserter would be placed |
| `direction` | `string` | `"north"` | Direction the inserter would face (= DROP direction). Pickup is from the opposite side |

**Returns**: JSON with pickup/drop positions and entities found:
```json
{"success":true,"inserter_position":{"x":6,"y":3},"direction":"east","pickup":{"x":5,"y":3,"entities":[{"name":"burner-mining-drill","type":"mining-drill"}]},"drop":{"x":7,"y":3,"entities":[{"name":"stone-furnace","type":"furnace"}]},"can_place":true}
```
Error response:
```json
{"success":false,"error":"invalid_direction","direction":"invalid"}
```

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

---

### `RemoveItems`

Remove items from a machine/entity's inventory into the player's inventory. Use this to collect smelted plates from furnaces, take crafted items from assemblers, or empty chests. Reports `inventory_full` flag if player inventory is full.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | *(required)* | X coordinate of the target entity |
| `y` | `double` | *(required)* | Y coordinate of the target entity |
| `itemName` | `string` | *(required)* | Item name to remove (e.g. `iron-plate`, `copper-plate`) |
| `count` | `int` | *(required)* | Number of items to remove |
| `inventoryType` | `string` | `"furnace_result"` | Source inventory: `fuel`, `furnace_source`, `furnace_result`, `chest`, `assembling_machine_input`, `assembling_machine_output` |

**Returns**: JSON with success status:
```json
{"success":true,"entity":"stone-furnace","item":"iron-plate","removed":10,"requested":10,"inventory_full":false}
```

---

### `InspectEntity`

Inspect an entity at the specified position to see its status, inventory contents, fuel level, recipe, health, and other details.

| Parameter | Type | Description |
|-----------|------|-------------|
| `x` | `double` | X coordinate of the entity to inspect |
| `y` | `double` | Y coordinate of the entity to inspect |

**Returns**: JSON with entity details:
```json
{"success":true,"entity":"stone-furnace","type":"furnace","position":{"x":5,"y":-2},"status":"working","health":200,"max_health":200,"inventories":{"fuel":[{"name":"coal","count":3}],"furnace_source":[{"name":"iron-ore","count":12}],"furnace_result":[{"name":"iron-plate","count":8}]},"burner":{"remaining_burning_fuel":1200.0,"heat":165.0,"heat_capacity":200.0}}
```

---

## World Scanning Tools

### `GetNearbyEntities`

Get a list of all entities within a given radius. Defaults to scanning around the player. Provide `centerX`/`centerY` to scan a remote area without walking there.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `10` | Search radius around the center in tiles |
| `centerX` | `double?` | `null` | Optional X coordinate to center the scan on (omit to use player position) |
| `centerY` | `double?` | `null` | Optional Y coordinate to center the scan on (omit to use player position) |

**Returns**: JSON with entities array:
```json
{"entities":[{"name":"iron-ore","x":3.5,"y":-1.5},{"name":"stone-furnace","x":5,"y":-2}]}
```

---

### `CheckDistance`

Check the distance from the player to target map coordinates and whether the target is within build range (for placing) and reach range (for mining/interacting).

| Parameter | Type | Description |
|-----------|------|-------------|
| `x` | `double` | X coordinate of the target position |
| `y` | `double` | Y coordinate of the target position |

**Returns**: JSON distance report:
```json
{"distance":4.2,"build_in_range":true,"build_limit":6,"reach_in_range":true,"reach_limit":6}
```

---

### `ScanResources`

Scan for resource patches (ores, oil, etc.) within a radius. Returns a summary of each resource type found. Supports remote scanning via `centerX`/`centerY`.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `50` | Search radius around the center in tiles |
| `centerX` | `double?` | `null` | Optional X coordinate to center the scan on (omit to use player position) |
| `centerY` | `double?` | `null` | Optional Y coordinate to center the scan on (omit to use player position) |

**Returns**: JSON with resource summaries:
```json
{"scan_radius":50,"resources":[{"name":"iron-ore","patches":42,"total_amount":8400,"center_x":15.5,"center_y":-8.0}]}
```

---

### `ScanTiles`

Scan tiles to get terrain information. Returns tile type counts. Supports remote scanning via `centerX`/`centerY`.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `16` | Search radius around the center in tiles |
| `centerX` | `double?` | `null` | Optional X coordinate to center the scan on (omit to use player position) |
| `centerY` | `double?` | `null` | Optional Y coordinate to center the scan on (omit to use player position) |

**Returns**: JSON with tile type counts:
```json
{"scan_radius":16,"tiles":[{"name":"grass-1","count":320},{"name":"dirt-1","count":85},{"name":"water","count":15}]}
```

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

---

### `GetAvailableRecipes`

Get all recipes currently available (unlocked) for crafting. Returns each recipe's name, category, and crafting time.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: JSON with recipes:
```json
{"recipes":[{"name":"iron-gear-wheel","category":"crafting","energy":0.5}],"count":42}
```

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

---

### `CheckCraftFeasibility`

Check whether a recipe can be crafted with the player's current inventory. Returns whether crafting is possible, the maximum craftable count, and a per-ingredient breakdown showing how many are needed, available, and missing. Use this before crafting to verify you have the materials, or to plan resource gathering.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `recipe` | `string` | *(required)* | Recipe name to check (e.g. `iron-gear-wheel`, `electronic-circuit`) |
| `count` | `int` | `1` | Number of items you want to craft |

**Returns**: JSON with feasibility details:
```json
{"success":true,"recipe":"iron-gear-wheel","requested":5,"can_craft":true,"craftable_count":5,"ingredients":[{"name":"iron-plate","needed":10,"available":50,"missing":0}]}
```
When ingredients are missing:
```json
{"success":true,"recipe":"iron-gear-wheel","requested":5,"can_craft":false,"craftable_count":2,"ingredients":[{"name":"iron-plate","needed":10,"available":4,"missing":6}]}
```

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

---

### `GetAvailableTechnologies`

Get all technologies currently available for research — enabled, not yet researched, and with all prerequisites met.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: JSON with available technologies:
```json
{"technologies":[{"name":"automation","cost":10,"ingredients":[{"name":"automation-science-pack","count":1}]}],"count":2}
```

---

### `StartResearch`

Start researching a technology by adding it to the research queue.

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

---

## Energy Tools

### `GetElectricNetwork`

Get electric network statistics from the nearest electric pole within a radius. Returns power production and consumption rates (watts), satisfaction percentage, and accumulator charge levels.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `50` | Search radius around the player to find electric poles |

**Returns**: JSON with network statistics:
```json
{"status":"ok","network_id":1,"pole":"small-electric-pole","pole_x":10.5,"pole_y":-3.5,"total_production_watts":900.0,"total_consumption_watts":600.0,"satisfaction_percent":100.0,"accumulator_count":0,"producers":[{"name":"steam-engine","watts":900.0}],"consumers":[{"name":"assembling-machine-1","watts":600.0}]}
```
When no poles found:
```json
{"status":"no_poles_found","player_x":12.5,"player_y":-3.5,"radius":50}
```

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

---

## Blueprint & Ghost Tools

### `PlaceGhostEntity`

Place a ghost entity (construction plan) at the specified position. Does NOT require the item in inventory — ghosts are free placement plans. Bots or players fill them in later.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `entityName` | `string` | *(required)* | Entity prototype name (e.g. `stone-furnace`, `transport-belt`) |
| `x` | `double` | *(required)* | X coordinate on the map |
| `y` | `double` | *(required)* | Y coordinate on the map |
| `direction` | `string` | `"north"` | Direction the entity faces: `north`, `south`, `east`, `west`, `northeast`, `northwest`, `southeast`, `southwest` |

**Returns**: JSON with placement result.

---

### `PlaceBlueprintString`

Build a blueprint from a base64 blueprint string at a position. Entities are placed as ghosts unless materials are available.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `blueprintString` | `string` | *(required)* | Blueprint string (base64-encoded, starts with `0`) |
| `x` | `double` | *(required)* | X coordinate for the blueprint center |
| `y` | `double` | *(required)* | Y coordinate for the blueprint center |
| `direction` | `string` | `"north"` | Direction to rotate the blueprint: `north`, `south`, `east`, `west` |
| `buildMode` | `string` | `"normal"` | Build mode: `normal` (fail if blocked), `forced` (deconstruct nature), `superforced` (deconstruct all) |

**Returns**: JSON with blueprint placement results.

---

### `GetGhostEntities`

Scan for ghost entities (planned constructions) near a position.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `50` | Search radius in tiles |
| `centerX` | `double?` | `null` | Optional X coordinate of scan center (omit to use player position) |
| `centerY` | `double?` | `null` | Optional Y coordinate of scan center (omit to use player position) |

**Returns**: JSON with ghost entity names, positions, and directions.

---

### `CreateBlueprintFromArea`

Capture entities in a rectangular area as a blueprint string. The resulting string can be used with `PlaceBlueprintString` to reproduce the layout elsewhere.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x1` | `double` | *(required)* | Left X coordinate of the capture area |
| `y1` | `double` | *(required)* | Top Y coordinate of the capture area |
| `x2` | `double` | *(required)* | Right X coordinate of the capture area |
| `y2` | `double` | *(required)* | Bottom Y coordinate of the capture area |
| `includeTiles` | `bool` | `false` | Whether to include tiles (like concrete) in the blueprint |

**Returns**: JSON with exported blueprint string.

---

### `RevokeGhostEntity`

Remove/cancel ghost entities at a position within a radius.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | *(required)* | X coordinate to search for ghosts |
| `y` | `double` | *(required)* | Y coordinate to search for ghosts |
| `radius` | `double` | `1` | Search radius around the position |

**Returns**: JSON with revocation result.

---

## Building Memory Tools

Buildings are automatically tracked when placed via `PlaceEntity` and untracked when mined via `MineEntity`. These tools query the AI's building memory for factory awareness.

### `GetAllBuildings`

Get all buildings the AI has placed, with their names, positions, directions, and labels.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

---

### `GetBuildingsNear`

Get buildings near a specific position within a radius, sorted by distance.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | *(required)* | X coordinate of the center position |
| `y` | `double` | *(required)* | Y coordinate of the center position |
| `radius` | `double` | `20` | Search radius in tiles |

---

### `FindBuildingsByType`

Find all buildings of a specific entity type.

| Parameter | Type | Description |
|-----------|------|-------------|
| `entityName` | `string` | Entity name to search for (e.g. `stone-furnace`, `transport-belt`) |

---

### `GetBuildingSummary`

Get a count summary of all building types placed.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

---

### `UpdateBuildingLabel`

Set or update a label on a building at the specified position. Labels help identify building purpose.

| Parameter | Type | Description |
|-----------|------|-------------|
| `x` | `double` | X coordinate of the building |
| `y` | `double` | Y coordinate of the building |
| `label` | `string?` | Label text (e.g. `iron smelter #1`), or `null` to remove |

---

### `ClearBuildingMemory`

Clear all tracked buildings from memory. Does NOT remove buildings from the game world — only from the AI's memory.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

---

## Goal Planning Tools

Goals help the AI track what it's working toward, maintain progress through ordered steps, and resume after interruptions. Only one goal can be active at a time. Goals persist across server restarts.

### `SetGoal`

Create a new goal with a description and optional ordered steps. Auto-activates if no other goal is active.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `description` | `string` | *(required)* | What the goal aims to achieve |
| `steps` | `List<string>?` | `null` | Optional ordered list of steps |

---

### `GetActiveGoal`

Get the currently active goal with full step details and progress.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

---

### `GetAllGoals`

Get a summary of all goals (completed, failed, suspended, pending).

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

---

### `AdvanceGoalStep`

Mark the current step as completed and advance to the next step.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `notes` | `string?` | `null` | Optional notes about what was accomplished |

---

### `AddGoalSteps`

Add new steps to the active goal (appended to the end).

| Parameter | Type | Description |
|-----------|------|-------------|
| `steps` | `List<string>` | Step descriptions to add |

---

### `CompleteGoal`

Mark the active goal as completed.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `notes` | `string?` | `null` | Optional notes about the completed goal |

---

### `FailGoal`

Mark the active goal as failed with a reason.

| Parameter | Type | Description |
|-----------|------|-------------|
| `reason` | `string` | Reason why the goal failed |

---

### `SuspendGoal`

Suspend the active goal to work on something more urgent. Preserves progress.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

---

### `ResumeGoal`

Resume a previously suspended goal by its ID. No other goal can be currently active.

| Parameter | Type | Description |
|-----------|------|-------------|
| `goalId` | `string` | The ID of the suspended goal to resume (from `GetAllGoals`) |

---

## Chat Tools

### `InitializeChatListener`

Initialize/re-initialize the chat message listener. Registers an event handler that captures in-game chat messages. Called automatically on startup.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: JSON with status:
```json
{"status":"initialized","existing_messages":0}
```

---

### `GetChatMessages`

Get in-game chat messages. Use `sinceTick` to poll only new messages since last check.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `sinceTick` | `long` | `0` | Only return messages after this game tick |

**Returns**: JSON with messages:
```json
{"messages":[{"tick":4200,"player":"player1","message":"hello AI"}],"count":1,"latest_tick":4200}
```

---

### `SendChatMessage`

Send a chat message visible to all players. Automatically tagged with `[AI]`.

| Parameter | Type | Description |
|-----------|------|-------------|
| `message` | `string` | The message text to send |

**Returns**: JSON with status:
```json
{"status":"sent"}
```

---

## Wait & Timing Tools

### `WaitForCrafting`

Poll until the crafting queue empties or timeout is reached. Use after `Craft` to wait for items to finish.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `pollIntervalSeconds` | `double` | `1.0` | How often to check the queue in seconds |
| `timeoutSeconds` | `double` | `60` | Maximum time to wait in seconds |

**Returns**: JSON with completion status:
```json
{"status":"complete","queue":[]}
```
On timeout:
```json
{"status":"timeout","remaining":{"queue":[{"recipe":"iron-gear-wheel","count":3}]}}
```

---

### `WaitForPosition`

Poll until the player reaches a target position within tolerance, or timeout. The player must already be walking.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `targetX` | `double` | *(required)* | Target X coordinate to reach |
| `targetY` | `double` | *(required)* | Target Y coordinate to reach |
| `tolerance` | `double` | `2.0` | Arrived when within this many tiles of the target |
| `pollIntervalSeconds` | `double` | `0.5` | How often to check position in seconds |
| `timeoutSeconds` | `double` | `30` | Maximum time to wait in seconds |

**Returns**: JSON with arrival status:
```json
{"status":"arrived","tolerance":2,"position":{"x":9.8,"y":-1.2,"distance":0.28}}
```

---

### `WaitForTicks`

Wait for N game ticks to pass. Factorio runs at 60 ticks per second at normal speed.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ticks` | `int` | *(required)* | Number of game ticks to wait (60 = 1 second at 1x speed) |
| `pollIntervalSeconds` | `double` | `0.5` | How often to check the tick count in seconds |
| `timeoutSeconds` | `double` | `30` | Maximum real-time seconds to wait |

**Returns**: JSON with completion status:
```json
{"status":"complete","start_tick":1000,"end_tick":1060,"elapsed":60}
```

---

### `GetGameTick`

Get the current game tick. Useful for measuring elapsed time.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: JSON with current tick:
```json
{"tick":12345}
```

---

## Status Tools

### `GetFactoryStatus`

Get a comprehensive factory status snapshot in a single call. Aggregates game state (position, inventory, crafting queue, research, nearby resources, nearby entities, electric power) with C#-side state (building summary, active goal) into one response. Use this to get a broad overview of the current game state before making decisions.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `resourceScanRadius` | `double` | `50` | Radius to scan for resource patches |
| `entityScanRadius` | `double` | `20` | Radius to scan for nearby entities |
| `electricPoleRadius` | `double` | `50` | Radius to search for electric poles |

**Returns**: JSON with all status sections:
```json
{"position":{"x":12.5,"y":-3.8},"inventory":{"items":[{"name":"iron-plate","count":50}],"total_slots":80,"free_slots":65},"crafting_queue":[],"research":{"active":true,"technology":"automation","progress":0.452},"nearby_resources":{"scan_radius":50,"resources":[{"name":"iron-ore","patches":42,"total_amount":8400,"center_x":15.5,"center_y":-8.0}]},"nearby_entities":{"scan_radius":20,"types":[{"name":"stone-furnace","count":3}]},"power":{"available":true,"production_watts":900.0,"consumption_watts":600.0,"satisfaction_percent":100.0},"building_summary":{"status":"ok","total_buildings":5,"type_count":2,"types":[{"entity_name":"stone-furnace","count":3},{"entity_name":"burner-mining-drill","count":2}]},"active_goal":{"description":"Automate iron plates","status":"active","current_step":2,"step_count":5}}
```

---

## Advanced Tools

### `ExecuteLua`

Execute arbitrary Lua code via RCON. Use `rcon.print()` to return data. Access the player via `game.connected_players[1]`.

| Parameter | Type | Description |
|-----------|------|-------------|
| `luaCode` | `string` | Lua code to execute |

> ⚠️ **WARNING**: Executes code directly on the game server with no sandboxing. Incorrect Lua can corrupt game state or crash the server. Prefer specific tools when available.

---

## Tips for AI Agent Prompt Engineering

- **Always check position** before walking. Use `GetPlayerPosition` to know where the player is, then use `WalkToPosition` to reach the target.
- **Check distance before interacting**. Use `CheckDistance` to verify you're within build/reach range before calling `PlaceEntity`, `MineEntity`, `InsertItems`, `RemoveItems`, or `InspectEntity`. Walk closer if out of range.
- **Check feasibility before crafting**. Use `CheckCraftFeasibility` to verify ingredients before calling `Craft`. This shows exactly what's missing.
- **Wait for crafting to finish**. After calling `Craft`, use `WaitForCrafting` to block until the queue empties before using crafted items.
- **Use tick-based waits for game mechanics**. Use `WaitForTicks` for furnace smelting, inserter cycles, or other game-tick-dependent operations (60 ticks = 1 second at normal speed).
- **Scan before placing**. Use `GetNearbyEntities` to see what's around, `ScanResources` to find ore patches, and `ScanTiles` to check terrain.
- **Use goals for multi-step tasks**. `SetGoal` with steps keeps track of progress. Call `AdvanceGoalStep` after each step, and `SuspendGoal`/`ResumeGoal` for interruptions.
- **Monitor power before expanding**. Use `GetElectricNetwork` before adding machines. Use `InspectEntityPower` to debug unpowered entities.
- **Look up recipes before crafting**. Use `GetRecipeDetails` to check ingredients, and `GetAvailableRecipes` to see what's unlocked.
- **Use building memory**. `GetBuildingsNear` and `FindBuildingsByType` help avoid duplicates and plan layouts.
- **Interact with entities properly**. Use `InsertItems` to load machines, `RemoveItems` to collect output, and `InspectEntity` to check status before and after.
- **Communicate with players**. Use `SendChatMessage` to explain what you're doing, and `GetChatMessages` to listen for player instructions.
- **Use remote scanning**. Pass `centerX`/`centerY` to `GetNearbyEntities`, `ScanResources`, and `ScanTiles` to scout distant areas without walking there.
- **Plan with blueprints**. Use `PlaceGhostEntity` to plan layouts without committing items, `CreateBlueprintFromArea` to capture working sections, and `PlaceBlueprintString` to replicate them.

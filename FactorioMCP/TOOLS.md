# FactorioMCP — Tools Reference

All MCP tools exposed by the FactorioMCP server. Grouped by category.

> **Core constraint:** All tools respect real game mechanics. The player walks with A* pathfinding, crafting takes real time, and no items are spawned or teleported.

---

## Movement

### `WalkToPosition`
Walk to a target using Factorio's built-in A* pathfinder. Navigates around buildings, water, and cliffs. Draws a debug path overlay in-game.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `targetX` | `double` | required | Target X coordinate |
| `targetY` | `double` | required | Target Y coordinate |
| `tolerance` | `double` | `2.0` | Arrived when within this many tiles |
| `timeoutSeconds` | `double` | `30` | Max walk time before giving up |

**Returns:** `{ "status": "arrived"|"stuck"|"timeout"|"no_path", "x", "y", "distance" }`

---

### `GetPlayerPosition`
Get the player's current map position.

**Returns:** `{ "x": 12.5, "y": -3.75 }`

---

## Inventory & Crafting

### `GetInventory`
Get all items and counts in the player's main inventory.

**Returns:** `{ "items": [{ "name", "count" }], "total_slots", "free_slots" }`

---

### `CheckCraftFeasibility`
Check whether a recipe can be crafted with the current inventory. Shows max craftable count and per-ingredient breakdown. **Always call this before `Craft`.**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `recipe` | `string` | required | Recipe name (e.g. `iron-gear-wheel`) |
| `count` | `int` | `1` | Number of items to check |

**Returns:** `{ "can_craft": bool, "craftable_count", "ingredients": [{ "name", "needed", "available", "missing" }] }`

---

### `Craft`
Queue a recipe for crafting. Items take real time — call `WaitForCrafting` afterwards.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `recipe` | `string` | required | Recipe name |
| `count` | `int` | required | Number to craft |

**Returns:** `{ "status": "crafting"|"no_materials"|"error", "queued" }`

---

### `GetCraftingQueue`
Get current crafting queue contents.

**Returns:** `{ "queue": [{ "recipe", "count" }] }`

---

### `DropItems`
Drop items from inventory onto the ground at the player's position.

| Parameter | Type | Description |
|-----------|------|-------------|
| `itemName` | `string` | Item to drop |
| `count` | `int` | Number to drop |

---

### `TransferAllItems`
Bulk transfer all items from an entity's inventory into the player inventory. Stops if inventory is full.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | required | Entity X |
| `y` | `double` | required | Entity Y |
| `inventoryType` | `string` | `"chest"` | `fuel`, `furnace_source`, `furnace_result`, `chest`, `assembling_machine_input`, `assembling_machine_output` |

**Returns:** `{ "transferred": [...], "inventory_full": bool }`

---

### `GetEntityInventory`
Inspect an entity's inventory contents, slot count, and empty slots.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | required | Entity X |
| `y` | `double` | required | Entity Y |
| `inventoryType` | `string` | `"chest"` | See `TransferAllItems` for valid values |

---

### `GetInventorySummary`
Get a condensed inventory as item-name:count pairs. Much more compact than `GetInventory` — use this when you just need to know what the player has.

**Returns:** `{ "iron-plate": 50, "coal": 20, ... }`

---

### `EnsureItem`
Check if the player has enough of an item. If not, reports whether it can be crafted and what ingredients are missing. Does NOT auto-craft.

| Parameter | Type | Description |
|-----------|------|-------------|
| `itemName` | `string` | e.g. `iron-plate`, `electronic-circuit` |
| `count` | `int` | Required quantity |

**Returns:** `{ "has_enough", "have", "need", "can_craft", "missing_ingredients": [...] }`

---

## Building & Mining

### `PlaceEntity`
Place an entity from inventory at map coordinates. Validates proximity, inventory, and position. Auto-tracked in building memory.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `entityName` | `string` | required | e.g. `stone-furnace`, `transport-belt` |
| `x` | `double` | required | |
| `y` | `double` | required | |
| `direction` | `string` | `"north"` | `north`, `south`, `east`, `west` (and diagonals) |

> **Inserter critical:** `direction` = the PICKUP side (where the arm reaches to GRAB items). Drop is ALWAYS on the OPPOSITE side. Think: `direction = where items come FROM`. Always use `PreviewInserterPlacement` first.

> **Belt tip:** `direction` = the way items FLOW (arrow direction). Use `PlanBeltRoute` to calculate a full path.

**Returns:** `{ "success": true, "entity", "x", "y" }` or `{ "success": false, "error": "out_of_range"|"invalid_position"|"missing_item" }`

---

### `MineEntity`
Mine a building (non-resource) at coordinates. Must be within reach range. Auto-removed from building memory. For ore patches use `MineResource`.

| Parameter | Type | Description |
|-----------|------|-------------|
| `x` | `double` | |
| `y` | `double` | |

**Returns:** `{ "success": true, "entity" }` or `{ "success": false, "error" }`

---

### `MineResource`
Mine resource patches (ore, stone, coal) with realistic one-unit-at-a-time timing.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | required | Resource entity X |
| `y` | `double` | required | Resource entity Y |
| `count` | `int` | `1` | Units to mine |
| `pollIntervalSeconds` | `double` | `0.5` | Check interval |
| `timeoutSeconds` | `double` | `60` | Max wait |

**Returns:** `{ "success": true, "status": "complete"|"depleted"|"timeout", "entity", "mined", "requested", "remaining", "depleted" }`

---

### `RotateEntity`
Rotate a building clockwise (or counter-clockwise). Validates reach range. Updates direction in building memory.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | required | |
| `y` | `double` | required | |
| `reverse` | `bool` | `false` | Counter-clockwise if true |

**Returns:** `{ "success": true, "entity", "new_direction" }`

---

### `PreviewInserterPlacement`
Preview what an inserter would pick up from and drop to at a given position and direction — **without placing anything**. Always call this before placing inserters.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | required | Where the inserter would go |
| `y` | `double` | required | |
| `direction` | `string` | `"north"` | PICKUP direction (drop is opposite) |

**Returns:** `{ "inserter_position", "direction", "pickup": { "x", "y", "entities": [...] }, "drop": { "x", "y", "entities": [...] }, "can_place" }`

---

### `PlaceInserter`
Place an inserter adjacent to a target entity with automatic direction calculation. Specify which side and whether items should flow in or out.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `inserterName` | `string` | required | e.g. `burner-inserter`, `inserter`, `fast-inserter` |
| `targetX` | `double` | required | X of the TARGET entity (not inserter position) |
| `targetY` | `double` | required | Y of the TARGET entity |
| `side` | `string` | required | `north`, `south`, `east`, `west` — which side to place on |
| `inbound` | `bool` | `true` | `true` = drops INTO target; `false` = picks FROM target |

**Returns:** `{ "success": true, "x", "y", "direction", "entity" }`

---

### `InsertBetween`
Auto-place an inserter in the 1-tile gap between two adjacent entities. Calculates position and direction automatically.

| Parameter | Type | Description |
|-----------|------|-------------|
| `inserterName` | `string` | Inserter to place |
| `sourceX` | `double` | X of source entity (items come FROM here) |
| `sourceY` | `double` | |
| `destX` | `double` | X of destination entity (items go TO here) |
| `destY` | `double` | |

**Returns:** `{ "success": true, "x", "y", "direction", "entity" }`

---

### `PlaceEntitySmart`
Auto-place an entity near a target position. Searches outward in a spiral for the nearest valid placement. Validates inventory and build distance. Use instead of `PlaceEntity` when you want the backend to pick the best spot.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `entityName` | `string` | required | e.g. `stone-furnace`, `burner-mining-drill` |
| `nearX` | `double` | required | X coordinate to place near |
| `nearY` | `double` | required | Y coordinate to place near |
| `direction` | `string` | `"north"` | Direction the entity faces |
| `searchRadius` | `double` | `10` | How far from target to search |

**Returns:** `{ "success": true, "entity", "x", "y" }`

---

## Entity Interaction

### `InsertItems`
Insert items from player inventory into a machine. Used to fuel drills, load furnaces with ore, or stock assemblers.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | required | |
| `y` | `double` | required | |
| `itemName` | `string` | required | e.g. `coal`, `iron-ore` |
| `count` | `int` | required | |
| `inventoryType` | `string` | `"fuel"` | `fuel`, `furnace_source`, `furnace_result`, `chest`, `assembling_machine_input`, `assembling_machine_output` |

**Returns:** `{ "success": true, "entity", "item", "inserted", "requested" }` or error

---

### `RemoveItems`
Take items from a machine into the player inventory.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | required | |
| `y` | `double` | required | |
| `itemName` | `string` | required | |
| `count` | `int` | required | |
| `inventoryType` | `string` | `"furnace_result"` | See `InsertItems` |

**Returns:** `{ "success": true, "removed", "transferred", "inventory_full" }`

---

### `InspectEntity`
Inspect entity status, inventory contents, fuel, recipe, health. For inserters, also shows pickup/drop tile positions.

| Parameter | Type | Description |
|-----------|------|-------------|
| `x` | `double` | |
| `y` | `double` | |

---

### `PickupItems`
Pick up items dropped on the ground near the player — like holding 'F' in-game.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `10` | Search radius in tiles |

**Returns:** `{ "success", "picked_up": [{ "name", "count" }], "total_items" }`

---

## World Scanning

### `GetNearbyEntities`
List entities within a radius. Supports remote scanning via optional center.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `10` | Search radius |
| `centerX` | `double?` | player pos | Optional scan center X |
| `centerY` | `double?` | player pos | Optional scan center Y |

---

### `CheckDistance`
Check distance from player to coordinates and build/reach range status.

| Parameter | Type | Description |
|-----------|------|-------------|
| `x` | `double` | |
| `y` | `double` | |

**Returns:** `{ "distance", "build_in_range", "build_limit", "reach_in_range", "reach_limit" }`

---

### `ScanResources`
Scan for resource patches. Returns type summary with patch count, total amount, and center coordinates.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `50` | |
| `centerX` | `double?` | player pos | |
| `centerY` | `double?` | player pos | |

---

## Perception

### `SummarizeArea`
Structured overview of a circular area: resources, machines (grouped by type with working/idle counts), threats, and free space estimate.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `50` | |
| `centerX` | `double?` | player pos | |
| `centerY` | `double?` | player pos | |

---

### `WhatAmILookingAt`
Directional raycast — returns entities along a compass direction sorted by distance.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `direction` | `string` | required | `north`, `south`, `east`, `west` (and diagonals) |
| `range` | `double` | `30` | How far to look in tiles |
| `width` | `double` | `3` | Width of the look cone |

---

### `FindBuildableArea`
Find a flat, empty rectangular area for factory placement. Searches outward from player (or center). By default, ore patches block placement to avoid wasting ore — set `allowOrePatches=true` only when placing mining drills.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `width` | `int` | required | Required width in tiles |
| `height` | `int` | required | Required height in tiles |
| `searchRadius` | `double` | `50` | Max search distance |
| `centerX` | `double?` | player pos | |
| `centerY` | `double?` | player pos | |
| `allowOrePatches` | `bool` | `false` | Allow building on ore (use for mining drills) |

**Returns:** `{ "success", "top_left": { "x", "y" }, "center": { "x", "y" }, "distance" }`

---

## Target Finding

### `FindNearest`
Find the nearest entity by name or type within a radius. Returns position, distance, direction, and total count found.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `entityType` | `string` | required | Name (e.g. `stone-furnace`) or type (e.g. `furnace`, `resource`) |
| `radius` | `double` | `100` | |

**Returns:** `{ "success", "entity", "x", "y", "distance", "direction", "total_found" }`

---

### `FindBestResourcePatch`
Find the optimal resource patch using a distance + richness heuristic. Returns best patch and up to 3 alternatives.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `resourceName` | `string` | required | e.g. `iron-ore`, `coal` |
| `radius` | `double` | `200` | |

**Returns:** `{ "success", "best_patch": { "center_x", "center_y", "total_amount", "entity_count" }, "alternatives": [...] }`

---

### `GetClosestBuildingOfType`
Find the closest AI-tracked building of a given entity type. Returns position, label, distance, and up to 3 other matches.

| Parameter | Type | Description |
|-----------|------|-------------|
| `entityName` | `string` | e.g. `stone-furnace`, `wooden-chest` |

---

## Navigation (Find + Walk)

### `MoveToEntity`
Find the nearest entity matching a name or type and walk to it in one call.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `entityType` | `string` | required | Name or type |
| `radius` | `double` | `100` | Search radius |
| `tolerance` | `double` | `2.0` | Arrival tolerance |
| `timeoutSeconds` | `double` | `30` | Walk timeout |

**Returns:** `{ "success", "target_type", "target", "target_x", "target_y", "walk_status", "player_x", "player_y", "remaining_distance" }`

---

### `MoveToResource`
Find the best resource patch and walk to its center in one call.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `resourceName` | `string` | required | e.g. `iron-ore` |
| `radius` | `double` | `200` | Search radius |
| `tolerance` | `double` | `5.0` | Arrival tolerance |
| `timeoutSeconds` | `double` | `60` | Walk timeout |

---

### `MoveToBuilding`
Find a tracked building by label or entity name and walk to it. Searches building memory — label first (case-insensitive), then entity name.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `searchTerm` | `string` | required | Label or entity name (e.g. `main smelter`, `stone-furnace`) |
| `tolerance` | `double` | `2.0` | |
| `timeoutSeconds` | `double` | `30` | |

---

## High-Level Tasks

### `GatherResource`
Find a resource patch → walk → find nearest entity → mine. All in one call. Uses realistic tick-based mining.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `resource` | `string` | required | e.g. `iron-ore`, `coal` |
| `count` | `int` | `10` | Units to mine |
| `searchRadius` | `double` | `200` | Patch search radius |
| `timeoutSeconds` | `double` | `120` | Total timeout |

**Returns:** `{ "success", "operation": "gather", "resource", "requested", "mined", "status", "walk_status", ... }`

---

### `RefuelEntity`
Walk to an entity and insert fuel — in one call.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | required | Entity X |
| `y` | `double` | required | Entity Y |
| `fuelItem` | `string` | `"coal"` | Fuel item name |
| `count` | `int` | `5` | Fuel items to insert |
| `walkTimeoutSeconds` | `double` | `30` | |

**Returns:** `{ "success", "operation": "refuel", "inserted", "status", "entity", ... }`

---

### `Smelt`
Find a furnace → walk → insert ore + fuel → wait for smelting → collect output. All in one call. Searches building memory first, falls back to world entity search.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ore` | `string` | required | e.g. `iron-ore` |
| `outputItem` | `string` | required | e.g. `iron-plate` |
| `count` | `int` | `10` | Ore to insert |
| `fuel` | `string` | `"coal"` | |
| `fuelCount` | `int` | `5` | |
| `furnaceX` | `double?` | auto-find | Optional specific furnace X |
| `furnaceY` | `double?` | auto-find | Optional specific furnace Y |
| `timeoutSeconds` | `double` | `180` | Total timeout |

**Returns:** `{ "success", "operation": "smelt", "ore_inserted", "fuel_inserted", "output_collected", "status", ... }`

---

## Wait & Timing

### `WaitForCrafting`
Poll until the crafting queue empties. Call after every `Craft`.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `pollIntervalSeconds` | `double` | `1.0` | |
| `timeoutSeconds` | `double` | `60` | |

**Returns:** `{ "status": "complete"|"timeout" }`

---

### `WaitForPosition`
Poll until the player reaches a target position. The player must already be walking.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `targetX` | `double` | required | |
| `targetY` | `double` | required | |
| `tolerance` | `double` | `2.0` | |
| `pollIntervalSeconds` | `double` | `0.5` | |
| `timeoutSeconds` | `double` | `30` | |

---

### `WaitForItemCount`
Poll until the player's inventory has at least N of an item. More efficient than polling `GetInventory` manually.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `itemName` | `string` | required | e.g. `iron-plate` |
| `targetCount` | `int` | required | Minimum count |
| `pollIntervalSeconds` | `double` | `1.0` | |
| `timeoutSeconds` | `double` | `60` | |

**Returns:** `{ "status": "satisfied"|"timeout", "item", "current_count", "target_count" }`

---

### `WaitForEntityStatus`
Poll until an entity reaches a target status. Status names follow `defines.entity_status`.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | required | |
| `y` | `double` | required | |
| `targetStatus` | `string` | required | e.g. `working`, `idle`, `no_fuel`, `no_power` |
| `pollIntervalSeconds` | `double` | `1.0` | |
| `timeoutSeconds` | `double` | `60` | |

**Returns:** `{ "status": "satisfied"|"timeout"|"error", "current_status", "target_status" }`

---

### `WaitForEntityInventory`
Poll until an entity's inventory contains at least N of an item.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | required | |
| `y` | `double` | required | |
| `itemName` | `string` | required | |
| `targetCount` | `int` | required | |
| `inventoryType` | `string` | `"chest"` | |
| `pollIntervalSeconds` | `double` | `1.0` | |
| `timeoutSeconds` | `double` | `60` | |

**Returns:** `{ "status": "satisfied"|"timeout"|"error", "item", "current_count", "target_count" }`

---

## Research

### `GetResearchStatus`
Get current research technology and progress percentage.

**Returns:** `{ "researching": bool, "technology", "progress" }`

---

### `GetAvailableTechnologies`
List technologies with prerequisites met and not yet researched.

**Returns:** `{ "technologies": [{ "name", "cost", "ingredients": [{ "name", "count" }] }], "count" }`

---

### `StartResearch`
Queue a technology for research.

| Parameter | Type | Description |
|-----------|------|-------------|
| `technology` | `string` | e.g. `automation`, `steel-processing` |

**Returns:** `{ "success", "technology", "cost", "ingredients" }`

---

## Recipes & Crafting Info

### `GetRecipeDetails`
Get ingredients, products, crafting time, and category for a recipe.

| Parameter | Type | Description |
|-----------|------|-------------|
| `recipe` | `string` | e.g. `iron-gear-wheel` |

---

### `GetAvailableRecipes`
List all unlocked recipes with name, category, and crafting time.

---

### `GetTechnologyDetails`
Get prerequisites, effects (recipe unlocks), cost, and science pack requirements for a technology.

| Parameter | Type | Description |
|-----------|------|-------------|
| `technology` | `string` | e.g. `automation` |

---

### `PlanCraft`
Recursively plan a full crafting chain for an item. Expands the recipe tree showing all intermediates and raw materials needed with exact quantities. Uses actual Factorio recipe data.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `item` | `string` | required | e.g. `electronic-circuit`, `automation-science-pack` |
| `count` | `int` | `1` | Number to craft |

**Returns:** `{ "item", "count", "recipe_tree": { ... }, "raw_materials": [{ "name", "count" }], "player_stock" }`

---

## Energy & Power

### `GetElectricNetwork`
Get network stats from nearest electric pole: production/consumption (watts), satisfaction %, accumulator levels.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `50` | Search radius for poles |

**Returns:** `{ "status", "network_id", "pole", "total_production_watts", "total_consumption_watts", "satisfaction_percent", "producers": [...], "consumers": [...] }`

---

### `InspectEntityPower`
Per-entity power diagnostics: network connection, energy stored, buffer, drain, generation.

| Parameter | Type | Description |
|-----------|------|-------------|
| `x` | `double` | |
| `y` | `double` | |

---

## Blueprints & Ghosts

### `PlaceGhostEntity`
Place a ghost (construction plan) — no items required.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `entityName` | `string` | required | |
| `x` | `double` | required | |
| `y` | `double` | required | |
| `direction` | `string` | `"north"` | |

---

### `PlaceBlueprintString`
Build from a base64 blueprint string. Entities placed as ghosts unless materials available.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `blueprintString` | `string` | required | Starts with `0` |
| `x` | `double` | required | Blueprint center X |
| `y` | `double` | required | Blueprint center Y |
| `direction` | `string` | `"north"` | Rotation |
| `buildMode` | `string` | `"normal"` | `normal`, `forced` (clear trees), `superforced` (clear all) |

---

### `GetGhostEntities`
Scan for ghost entities near a position.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `50` | |
| `centerX` | `double?` | player pos | |
| `centerY` | `double?` | player pos | |

---

### `CreateBlueprintFromArea`
Capture a rectangular area as an exportable blueprint string.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x1` | `double` | required | Left X |
| `y1` | `double` | required | Top Y |
| `x2` | `double` | required | Right X |
| `y2` | `double` | required | Bottom Y |
| `includeTiles` | `bool` | `false` | Include concrete/tiles |

---

### `RevokeGhostEntity`
Cancel ghost entities at a position.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | required | |
| `y` | `double` | required | |
| `radius` | `double` | `1` | Search radius |

---

### `ValidateGhostPlacements`
Validate ghost entity placements in an area. Checks each ghost for blocked positions and inserters pointing at nothing useful. Use after placing ghosts to verify the plan before committing real entities.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `50` | Search radius |
| `centerX` | `double?` | player pos | |
| `centerY` | `double?` | player pos | |

---

## Belt Planning

### `PlanBeltRoute`
Calculate belt tile positions and directions for a route between two points. Supports straight lines and L-shaped routes. Returns an ordered list to place with `PlaceEntity`.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `startX` | `double` | required | Start X (items enter here) |
| `startY` | `double` | required | |
| `endX` | `double` | required | End X (items exit here) |
| `endY` | `double` | required | |
| `turnPreference` | `string` | `"horizontal_first"` | `horizontal_first` or `vertical_first` for L-shaped routes |

**Returns:** Ordered list of `{ "x", "y", "direction" }` tiles.

---

## Building Memory

Buildings are auto-tracked on `PlaceEntity` and auto-removed on `MineEntity`.

### `GetAllBuildings`
Get all tracked buildings with names, positions, directions, and labels.

---

### `GetBuildingsNear`
Get buildings within a radius of a position, sorted by distance.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | required | Center X |
| `y` | `double` | required | Center Y |
| `radius` | `double` | `20` | |

---

### `FindBuildingsByType`
Find all tracked buildings of a specific entity type.

| Parameter | Type | Description |
|-----------|------|-------------|
| `entityName` | `string` | e.g. `stone-furnace` |

---

### `GetBuildingSummary`
Get counts per building type.

---

### `UpdateBuildingLabel`
Set or remove a label on a building. Labels help identify purpose (e.g. `iron smelter #1`).

| Parameter | Type | Description |
|-----------|------|-------------|
| `x` | `double` | |
| `y` | `double` | |
| `label` | `string?` | Text label, or `null` to remove |

---

### `ValidateBuildingMemory`
Check all tracked positions via RCON and prune any that no longer exist in the game world. Run this when buildings may have been removed by players or events.

---

### `ClearBuildingMemory`
Clear all buildings from AI memory. **Does NOT remove buildings from the game.**

---

## Goal Planning

Only one goal can be active at a time. Goals persist across server restarts.

### `SetGoal`
Create a goal with ordered steps. Auto-activates if no goal is currently active.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `description` | `string` | required | What the goal achieves |
| `steps` | `List<string>?` | none | Optional ordered steps |

---

### `GetActiveGoal`
Get the currently active goal, step progress, and completed steps.

---

### `GetAllGoals`
Get a summary of all goals (active, completed, failed, suspended, pending).

---

### `AdvanceGoalStep`
Mark the current step as completed and advance to the next.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `notes` | `string?` | none | Optional completion notes |

---

### `AddGoalSteps`
Append new steps to the active goal.

| Parameter | Type | Description |
|-----------|------|-------------|
| `steps` | `List<string>` | Steps to add |

---

### `CompleteGoal`
Mark the active goal as completed.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `notes` | `string?` | none | |

---

### `FailGoal`
Mark the active goal as failed.

| Parameter | Type | Description |
|-----------|------|-------------|
| `reason` | `string` | Why it failed |

---

### `SuspendGoal`
Suspend the active goal (preserves progress) to handle something urgent.

---

### `ResumeGoal`
Resume a suspended goal. No other goal can be active.

| Parameter | Type | Description |
|-----------|------|-------------|
| `goalId` | `string` | From `GetAllGoals` |

---

## Chat

### `GetChatMessages`
Read in-game chat messages from players.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `sinceTick` | `long` | `0` | Only return messages after this tick. Pass `latest_tick` from last response to get only new messages. |

**Returns:** `{ "messages": [{ "tick", "player", "message" }], "count", "latest_tick" }`

---

### `SendChatMessage`
Send a chat message to all players. Auto-tagged with `[AI]`.

| Parameter | Type | Description |
|-----------|------|-------------|
| `message` | `string` | |

---

## Vision

### `TakeScreenshot`
Take an annotated PNG screenshot of the game world. Draws entity bounding boxes (color-coded by type), numbered labels, and inserter direction arrows. Returns both the image and a structured **Map Legend** text listing every visible entity.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `centerX` | `double?` | player pos | |
| `centerY` | `double?` | player pos | |
| `zoom` | `double` | `1.0` | Higher = more zoomed in |
| `width` | `int` | `1920` | Screenshot width |
| `height` | `int` | `1080` | Screenshot height |

**Returns:** `CallToolResult` with `ImageContentBlock` (PNG) + `TextContentBlock` (Map Legend). Works for both vision and text-only models.

---

## Status

### `GetFactoryStatus`
Comprehensive factory snapshot in a single call: position, inventory, crafting queue, research, nearby resources, nearby entities, power status, building summary, active goal, and **item flow connections** (inserter-mediated machine-to-machine links and drill outputs, with belt-to-belt transfers filtered out).

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `resourceScanRadius` | `double` | `50` | |
| `entityScanRadius` | `double` | `20` | |
| `electricPoleRadius` | `double` | `50` | |
| `flowSummaryRadius` | `double` | `50` | Radius for item flow connections. Set to 0 to disable. |

**`item_flow` array:** Each entry is `{ "from", "from_x", "from_y", "to", "to_x", "to_y" }` showing a direct item transfer via inserter or drill output. Only connections where at least one side is a machine (not belt-to-belt) are included.

---

## Advanced

### `ExecuteLua`
Execute arbitrary Lua code via RCON (`/silent-command`). Use `rcon.print()` to return data. Player accessed via `game.connected_players[1]`.

> ⚠️ No sandboxing. Incorrect Lua can corrupt game state. Prefer specific tools when available.

| Parameter | Type | Description |
|-----------|------|-------------|
| `luaCode` | `string` | Lua code to execute |

---

### `ReconnectRcon`
Force a full RCON reconnection. Use when commands return `"nothing"` or the connection is stale. Auto-recovery triggers after 3 consecutive `"nothing"` responses, but this allows manual recovery.

---

## Energy & Power (Extended)

### `GetPowerNetworkTopology`
Map the electric pole connectivity graph within a radius. Groups all poles by their network segment and lists producers (generators/boilers/solar) and consumers (machines) per network. Returns pole adjacency (neighbours) for visualising how power flows across the area.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `80` | Search radius in tiles |

**Returns:** `{ "status", "network_count", "radius", "networks": [{ "network_id", "pole_count", "poles": [{ "name", "x", "y", "neighbours" }], "producer_count", "producers": [...], "consumer_count", "consumers": [...] }] }`

---

## Combat

### `ScanEnemies`
Scan for enemy units (biters, spitters), spawners (nests), and worms within a radius. Returns positions grouped by category and the nearest military enemy.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `100` | Search radius in tiles |

**Returns:** `{ "status", "radius", "unit_count", "spawner_count", "worm_count", "units": [...], "spawners": [...], "worms": [...], "nearest_enemy" }`

---

### `GetDefenses`
Find all player-owned turrets within a radius. Returns type, position, ammo count, kill count, and current shooting target for each turret.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `80` | Search radius in tiles |

**Returns:** `{ "status", "radius", "turret_count", "turrets": [{ "name", "type", "x", "y", "ammo_count", "kills", "shooting_target" }] }`

---

## Train Management

### `GetTrains`
List all trains on the player's surface. Returns ID, state, position, speed, locomotive/cargo wagon count, and current station for each train.

**Returns:** `{ "status", "train_count", "trains": [{ "id", "state", "manual_mode", "x", "y", "has_path", "speed", "locomotive_count", "cargo_wagon_count", "station" }] }`

---

### `GetTrainStops`
List all train stops on the player's surface with name, position, and docked train ID.

**Returns:** `{ "status", "stop_count", "stops": [{ "name", "x", "y", "stopped_train_id" }] }`

---

### `InspectTrain`
Inspect a specific train by numeric ID. Returns state, speed, schedule (station names), and cargo contents.

| Parameter | Type | Description |
|-----------|------|-------------|
| `trainId` | `uint` | Numeric train ID from `GetTrains` |

**Returns:** `{ "status", "id", "state", "manual_mode", "has_path", "speed", "schedule": [{ "index", "station" }], "cargo": [{ "name", "count" }] }`

---

### `SetTrainMode`
Switch a train between manual (stopped, script-controlled) and automatic (schedule-driven) mode.

| Parameter | Type | Description |
|-----------|------|-------------|
| `trainId` | `uint` | Numeric train ID |
| `manual` | `bool` | `true` = manual control, `false` = follow schedule |

**Returns:** `{ "status", "id", "manual_mode" }`

---

## Logistics Network

### `GetLogisticNetwork`
Get statistics for the logistic network at the player's position: robot counts (logistic and construction), robot limit, and counts of provider/requester/storage entities.

**Returns:** `{ "status", "network_id", "name", "all_logistic_robots", "available_logistic_robots", "all_construction_robots", "available_construction_robots", "robot_limit", "provider_count", "requester_count", "storage_count", "cell_count" }`

---

### `GetNetworkContents`
Get the complete item inventory of the logistic network at the player's position. Lists all items in provider and storage chests.

**Returns:** `{ "status", "network_id", "item_count", "items": [{ "name", "count" }] }`

---

### `GetRobotStatus`
Get a breakdown of logistic and construction robot activity: idle vs. busy counts and a sample of active robot positions.

**Returns:** `{ "status", "network_id", "logistic_idle", "logistic_busy", "construction_idle", "construction_busy", "busy_robots_sample": [...] }`

---

## Logistics Flow Tracking

### `GetFlowGraph`
Build a directed item-flow graph for the area around the player. Scans belts, inserters, and mining drills and returns directed edges showing which entity feeds which other entity, with connection type.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `30` | Search radius in tiles (keep small for readability) |

**Returns:** `{ "status", "radius", "edge_count", "edges": [{ "type", "from_name", "from_type", "from_x", "from_y", "to_name", "to_type", "to_x", "to_y" }] }`

---

### `TraceItemFlow`
Trace the downstream flow of items from a specific entity using BFS. Follows inserter drops and belt outputs up to the given depth. **Belt segments are automatically collapsed** into single nodes with length info — a 50-tile belt run counts as one hop with `belt_length: 50`, so depth budget is spent on machines and inserters, not individual belt tiles.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | required | X coordinate of starting entity |
| `y` | `double` | required | Y coordinate of starting entity |
| `depth` | `int` | `5` | Maximum hops to follow downstream (belt segments = 0 hops) |

**Returns:** `{ "status", "start_name", "start_x", "start_y", "node_count", "nodes": [{ "name", "type", "x", "y", "depth", "belt_length?", "end_x?", "end_y?" }], "edges": [{ "from_name", "from_x", "from_y", "to_name", "to_x", "to_y", "kind", "belt_length?" }] }`

Edge kinds: `"belt_segment"` (collapsed belt run), `"belt"` (splitter/underground), `"inserter"`, `"drill_output"`

---

### `PreviewBeltPlacement`
Preview what a transport belt at (x,y) facing direction D would connect to. Shows the output side (where items flow), three input sides (behind, left, right), nearby inserters, existing entities at position, and whether placement is possible. The belt equivalent of `PreviewInserterPlacement`.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `x` | `double` | required | X coordinate where belt would be placed |
| `y` | `double` | required | Y coordinate where belt would be placed |
| `direction` | `string` | `"north"` | Direction items flow (arrow direction): north, south, east, west |
| `beltType` | `string` | `"transport-belt"` | Belt type for placement check |

**Returns:** `{ "success", "belt_position", "direction", "belt_type", "output": { "x", "y", "entities" }, "input_behind": { "x", "y", "entities" }, "input_left": { "x", "y", "entities" }, "input_right": { "x", "y", "entities" }, "inserters": [{ "name", "x", "y", "role" }], "existing_at_position", "can_place" }`

Inserter roles: `"picks_from_belt"`, `"drops_onto_belt"`

---

## Factory Diagnostics

### `FindUnpoweredEntities`
Find entities with no power or low power within a radius. Diagnose power distribution problems.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `50` | Search radius in tiles |

**Returns:** `{ "status", "radius", "unpowered_count", "entities": [{ "name", "type", "x", "y", "status" }] }`

---

### `FindIdleMachines`
Find machines that are idle (not working) within a radius. Filters out passive entities (belts, pipes, poles, walls). Use to find bottlenecks.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `50` | Search radius in tiles |

**Returns:** `{ "status", "radius", "idle_count", "machines": [{ "name", "type", "x", "y", "status_reason" }] }`

---

### `FindMissingInputs`
Check which inputs a furnace or assembler is missing. Inspects fuel, source/input inventories, and output fullness.

| Parameter | Type | Description |
|-----------|------|-------------|
| `x` | `double` | Machine X |
| `y` | `double` | Machine Y |

**Returns:** `{ "entity", "x", "y", "missing": [{ "slot", "issue", "have", "need" }] }`

---

## Utility

### `CountItemInWorld`
Count how many of an item exist across all nearby containers (chests, furnaces, assemblers) and the player's inventory. Returns a per-location breakdown.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `itemName` | `string` | required | e.g. `iron-plate`, `coal` |
| `radius` | `double` | `50` | Search radius |

---

## Batch Operations

Execute multiple operations in a single MCP call. All process targets sequentially and fail fast on the first error.

### `MineEntityMultiple`
Mine multiple entities in one call.

| Parameter | Type | Description |
|-----------|------|-------------|
| `targets` | `string` | JSON array: `[{"x":1,"y":2},{"x":3,"y":4}]` |

**Returns:** `{ "success", "status", "total", "succeeded", "failed", "results": [...] }`

---

### `InspectEntityMultiple`
Inspect multiple entities in one call.

| Parameter | Type | Description |
|-----------|------|-------------|
| `targets` | `string` | JSON array: `[{"x":1,"y":2},{"x":3,"y":4}]` |

**Returns:** `{ "success", "status", "total", "succeeded", "failed", "results": [...] }`

---

### `InsertItemsMultiple`
Insert items into multiple entities in one call. Each target specifies coordinates, item name, count, and optional inventory type.

| Parameter | Type | Description |
|-----------|------|-------------|
| `targets` | `string` | JSON array: `[{"x":1,"y":2,"item":"coal","count":5},{"x":3,"y":4,"item":"iron-ore","count":10,"inventoryType":"furnace_source"}]` |

**Returns:** `{ "success", "status", "total", "succeeded", "failed", "results": [...] }`

---

### `RefuelEntityMultiple`
Refuel multiple entities in one call. Walks to each entity and inserts fuel.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `targets` | `string` | required | JSON array: `[{"x":1,"y":2},{"x":3,"y":4,"fuelItem":"wood","count":10}]` |
| `defaultFuel` | `string` | `"coal"` | Fuel for targets that don't specify one |
| `defaultCount` | `int` | `5` | Count for targets that don't specify one |
| `walkTimeoutSeconds` | `double` | `30` | Walk timeout per entity |

**Returns:** `{ "success", "status", "total", "succeeded", "failed", "results": [...] }`

---

## MCP Resources

Read-only game state accessible without tool calls via `read_resource`:

| URI | Description |
|-----|-------------|
| `factorio://player/position` | Current player coordinates |
| `factorio://player/inventory` | Player inventory |
| `factorio://player/crafting-queue` | Current crafting queue |
| `factorio://research/status` | Research status and progress |
| `factorio://research/available` | Technologies available to research |
| `factorio://recipes/available` | All unlocked recipes |
| `factorio://energy/network` | Electric network stats |
| `factorio://game/tick` | Current game tick |

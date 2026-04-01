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

## Tips for AI Agent Prompt Engineering

- **Always check position** before walking. Use `GetPlayerPosition` to know where the player is, then calculate which direction and how long to walk.
- **Check distance before interacting**. Use `CheckDistance` to verify you're within build/reach range before calling `PlaceEntity` or `MineEntity`. Walk closer if out of range.
- **Check inventory before crafting**. Use `GetInventory` to verify the player has the required ingredients before calling `Craft`.
- **Poll the crafting queue**. After calling `Craft`, use `GetCraftingQueue` to check when items are finished before attempting to use them.
- **Scan before placing**. Use `GetNearbyEntities` to see what's around the player, and `GetPlayerPosition` to plan placement coordinates.
- **Check inventory before placing**. Use `GetInventory` to confirm you have the entity item before calling `PlaceEntity`.
- **Walking is physics-based**. The player accelerates and decelerates realistically. Short durations (0.5–1s) give fine-grained movement; longer durations (3–5s) cover more ground.
- **Directions are cardinal + diagonal**. All eight compass directions are supported: `north`, `south`, `east`, `west`, `northeast`, `northwest`, `southeast`, `southwest`.

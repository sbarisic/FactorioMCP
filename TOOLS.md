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

**Returns**: Player position after walking (serialized Lua table, e.g. `{x = 12.5, y = -3.75}`).

**Example prompt usage**:
> "Walk north for 3 seconds to get closer to the iron ore patch."

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

**Returns**: Serialized position (e.g. `{x = 12.5, y = -3.75}`).

**Example prompt usage**:
> "Check my current position before deciding which direction to walk."

---

## Inventory & Crafting Tools

### `GetInventory`

List all items and their counts in the player's main inventory.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: One line per item type in the format `item-name: count`, e.g.:
```
iron-plate: 50
copper-plate: 30
iron-gear-wheel: 10
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

**Returns**: Confirmation message (e.g. `"Queued 5 iron-gear-wheel"`). The count returned is the number actually queued, which may be less than requested if ingredients are insufficient.

**Example prompt usage**:
> "Craft 10 iron gear wheels so I can build an assembling machine."

---

### `GetCraftingQueue`

Get the player's current crafting queue showing what is being crafted and how many remain.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: One line per queued recipe in the format `recipe-name xcount`, e.g.:
```
iron-gear-wheel x5
electronic-circuit x3
```
Returns `"No items in crafting queue"` when the queue is empty.

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

**Returns**: Confirmation or error message:
- `"Placed stone-furnace at {x = 5, y = -2}"` — success
- `"Out of range: 12.3 tiles away (build distance: 6)"` — too far away
- `"Cannot place stone-furnace at {x = 5, y = -2}"` — position blocked
- `"No stone-furnace in inventory"` — item not available

**Example prompt usage**:
> "Place a stone furnace at coordinates 5, -2 facing north."

---

### `MineEntity`

Mine/remove an entity at the specified map coordinates. Validates proximity (must be within reach distance) before mining. Mined items are added to the player's inventory.

| Parameter | Type | Description |
|-----------|------|-------------|
| `x` | `double` | X coordinate of the entity to mine |
| `y` | `double` | Y coordinate of the entity to mine |

**Returns**: Confirmation or error message:
- `"Mined stone-furnace"` — success
- `"Out of range: 8.5 tiles away (reach distance: 6)"` — too far away
- `"No entity found at position"` — nothing to mine

**Example prompt usage**:
> "Mine the entity at coordinates 5, -2 to pick it up."

---

## World Scanning Tools

### `GetNearbyEntities`

Get a list of all entities near the player within a given radius.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `radius` | `double` | `10` | Search radius around the player in tiles |

**Returns**: One line per entity in the format `entity-name at {x = ..., y = ...}`, e.g.:
```
iron-ore at {x = 3.5, y = -1.5}
stone-furnace at {x = 5, y = -2}
transport-belt at {x = 6, y = -2}
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

**Returns**: Distance report with range status, e.g.:
- `"Distance: 4.2 tiles | Build: in range (6) | Reach: in range (6)"` — close enough
- `"Distance: 9.1 tiles | Build: OUT OF RANGE (6) | Reach: OUT OF RANGE (6)"` — too far

**Example prompt usage**:
> "Check if I'm close enough to coordinates 5, -2 before placing a furnace."

---

### `GetResearchStatus`

Get the current research status and progress for the player's force.

| Parameter | Type | Description |
|-----------|------|-------------|
| *(none)* | | |

**Returns**: Research status message:
- `"Researching: automation (45.2%)"` — research in progress
- `"No active research"` — nothing queued

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

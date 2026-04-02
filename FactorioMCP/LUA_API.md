# Factorio Lua API Reference

This project bundles a local copy of the **Factorio 2 Lua API** documentation (version **2.0.76**, API version 6) in the [`LuaAPI/`](LuaAPI/) folder. All RCON Lua scripts in this project have been audited against these docs.

> **Online version:** <https://lua-api.factorio.com/latest/>

---

## Folder Structure

```
LuaAPI/
├── index.html              — Landing page
├── classes.html            — Runtime class index
├── concepts.html           — Runtime concept index
├── defines.html            — All defines.* enumerations
├── events.html             — All game events (on_tick, on_console_chat, etc.)
├── prototypes.html         — Prototype data-stage index
├── types.html              — Prototype type index
├── runtime-api.json        — Machine-readable runtime API (classes, events, concepts)
├── prototype-api.json      — Machine-readable prototype API
├── classes/                — 148 runtime class pages (LuaPlayer, LuaEntity, etc.)
├── concepts/               — 420 runtime concept pages (MapPosition, ItemFilter, etc.)
├── prototypes/             — 278 prototype pages (ItemPrototype, EntityPrototype, etc.)
├── types/                  — 637 prototype type pages (Animation, Sound, etc.)
├── auxiliary/              — 13 supplementary pages (data lifecycle, migrations, etc.)
└── static/                 — CSS, JS, images
```

---

## Key Classes Used in This Project

These are the runtime classes most relevant to the RCON Lua scripts in `FactorioService.cs`, `EnergyService.cs`, and related services.

### Player & Control

| Class | Local Doc | Description |
|-------|-----------|-------------|
| **LuaPlayer** | [`classes/LuaPlayer.html`](LuaAPI/classes/LuaPlayer.html) | Player entity — position, inventory, crafting, walking state, build/reach distance |
| **LuaControl** | [`classes/LuaControl.html`](LuaAPI/classes/LuaControl.html) | Base class for LuaPlayer — `get_inventory()`, `get_main_inventory()`, `begin_crafting()`, `mine_entity()` |
| **LuaInventory** | [`classes/LuaInventory.html`](LuaAPI/classes/LuaInventory.html) | Inventory contents — `get_contents()`, `get_item_count()`, `insert()`, `remove()` |

### World & Entities

| Class | Local Doc | Description |
|-------|-----------|-------------|
| **LuaSurface** | [`classes/LuaSurface.html`](LuaAPI/classes/LuaSurface.html) | Game map surface — `find_entities_filtered()`, `can_place_entity()`, `create_entity()`, `find_tiles_filtered()` |
| **LuaEntity** | [`classes/LuaEntity.html`](LuaAPI/classes/LuaEntity.html) | Any in-game entity — position, direction, health, type, name, `get_inventory()`, burner, mining target, ghost properties |
| **LuaTile** | [`classes/LuaTile.html`](LuaAPI/classes/LuaTile.html) | Terrain tile — name, position, prototype |

### Research & Recipes

| Class | Local Doc | Description |
|-------|-----------|-------------|
| **LuaForce** | [`classes/LuaForce.html`](LuaAPI/classes/LuaForce.html) | Player force — `technologies`, `recipes`, `current_research`, `research_progress`, `add_research()` |
| **LuaTechnology** | [`classes/LuaTechnology.html`](LuaAPI/classes/LuaTechnology.html) | Technology node — `researched`, `enabled`, `prerequisites`, `effects`, `research_unit_ingredients` |
| **LuaRecipe** | [`classes/LuaRecipe.html`](LuaAPI/classes/LuaRecipe.html) | Crafting recipe — `ingredients`, `products`, `energy`, `category`, `enabled` |

### RCON & Game

| Class | Local Doc | Description |
|-------|-----------|-------------|
| **LuaRCON** | [`classes/LuaRCON.html`](LuaAPI/classes/LuaRCON.html) | `rcon.print()` — the only way to return data from RCON Lua commands |
| **LuaGameScript** | [`classes/LuaGameScript.html`](LuaAPI/classes/LuaGameScript.html) | Global `game` table — `connected_players`, `tick`, `print()`, `surfaces` |
| **LuaRecord** | [`classes/LuaRecord.html`](LuaAPI/classes/LuaRecord.html) | Blueprint/blueprint book record — `get_blueprint_entities()`, `set_blueprint_entities()`, blueprint string operations |

### Energy

| Class | Local Doc | Description |
|-------|-----------|-------------|
| **LuaFlowStatistics** | [`classes/LuaFlowStatistics.html`](LuaAPI/classes/LuaFlowStatistics.html) | Flow rate tracking — `get_flow_count()` with `defines.flow_precision_index` for production/consumption stats |

---

## Key Defines

The [`defines.html`](LuaAPI/defines.html) page documents all enumerations. The most commonly used in this project:

| Define | Usage |
|--------|-------|
| `defines.direction` | Entity facing: `north`, `south`, `east`, `west`, `northeast`, `northwest`, `southeast`, `southwest` |
| `defines.inventory` | Inventory slots: `fuel`, `furnace_source`, `furnace_result`, `chest`, `assembling_machine_input`, `assembling_machine_output` |
| `defines.events` | Event IDs: `on_tick`, `on_console_chat` |
| `defines.flow_precision_index` | Stat precision: `five_seconds`, `one_minute`, `ten_minutes`, `one_hour` |

---

## Key Events

The [`events.html`](LuaAPI/events.html) page documents all events. Events used in this project:

| Event | Usage |
|-------|-------|
| `on_tick` | Walking handler with stuck detection — registered via `script.on_event()` |
| `on_console_chat` | Chat message capture — stores messages in `storage.chat_log` |

---

## RCON-Specific Notes

- **`game.player` is `nil`** in RCON context. Use `game.connected_players[1]` instead.
- **`rcon.print()`** is the only mechanism to return data from an RCON Lua command. All service methods format JSON strings via Lua string concatenation and pass them to `rcon.print()`.
- **`/silent-command`** is used instead of `/c` to suppress chat echo of commands.
- **`storage.*`** persists data across RCON commands within the same game session (used for `walk_state`, `chat_log`).
- **`script.on_event()`** registers event handlers from RCON that persist for the session.

---

## Machine-Readable API

For tooling or code generation, the API is also available in JSON format:

- [`runtime-api.json`](LuaAPI/runtime-api.json) — All runtime classes, methods, attributes, events, and concepts
- [`prototype-api.json`](LuaAPI/prototype-api.json) — All prototype definitions and their properties

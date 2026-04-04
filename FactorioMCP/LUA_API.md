# Factorio Lua API Reference

> **Version:** 2.0.76 · API version 6  
> **Online docs:** <https://lua-api.factorio.com/latest/>  
> **Local copy:** [`LuaAPI/`](LuaAPI/) folder

This is a comprehensive, self-contained reference of all Lua API classes, methods,
attributes, defines, concepts, and events used in this project. Scripts are executed
via RCON using `/c <lua>` and return data through `rcon.print()`.

---

## Table of Contents

1. [RCON Context](#rcon-context)
2. [Global Objects](#global-objects)
3. [Classes](#classes)
   - [LuaGameScript](#luagamescript-game)
   - [LuaPlayer](#luaplayer)
   - [LuaControl](#luacontrol-base-class)
   - [LuaSurface](#luasurface)
   - [LuaEntity](#luaentity)
   - [LuaInventory](#luainventory)
   - [LuaForce](#luaforce)
   - [LuaTechnology](#luatechnology)
   - [LuaRecipe](#luarecipe)
   - [LuaFlowStatistics](#luaflowstatistics)
   - [LuaItemStack](#luaitemstack)
   - [LuaItem & LuaItemCommon](#luaitem--luaitemcommon)
   - [LuaRecord (Blueprint)](#luarecord-blueprint)
   - [LuaBurner](#luaburner)
   - [LuaTile](#luatile)
   - [LuaTransportLine](#luatransportline)
   - [LuaLogisticNetwork](#lualogisticnetwork)
   - [LuaLogisticCell](#lualogisticcell)
   - [LuaEquipment](#luaequipment)
   - [LuaEquipmentGrid](#luaequipmentgrid)
   - [LuaFluidBox](#luafluidbox)
   - [LuaCircuitNetwork](#luacircuitnetwork)
   - [LuaTrain](#luatrain)
   - [LuaRCON](#luarcon)
4. [Defines](#defines)
5. [Key Concepts / Types](#key-concepts--types)
6. [Events](#events)
7. [Common Patterns](#common-patterns)

---

## RCON Context

Scripts execute via `/c <lua>` through RCON. The only way to return data is
`rcon.print(string)`. All output must be serialised to a string (typically JSON).

```lua
-- Template pattern used throughout this project:
local p = game.connected_players[1]
local result = { x = p.position.x, y = p.position.y }
rcon.print(game.table_to_json(result))
```

**Important constraints:**
- Scripts run in a single tick — no coroutines or yielding
- Global state can be stored in `storage` (persists between RCON calls)
- Events can be registered with `script.on_event()` for tick-level logic
- `game.table_to_json()` / `game.json_to_table()` are the best serialisation helpers
- `serpent.dump()` is available for more complex serialisation

---

## Global Objects

| Object | Type | Description |
|--------|------|-------------|
| `game` | `LuaGameScript` | Main game object — access to players, surfaces, forces, etc. |
| `script` | `LuaBootstrap` | Register event handlers with `script.on_event()` |
| `rcon` | `LuaRCON` | Print output back to RCON caller with `rcon.print()` |
| `rendering` | `LuaRendering` | Draw shapes/text/sprites in the game world |
| `remote` | `LuaRemote` | Cross-mod remote interface calls |
| `commands` | `LuaCommandProcessor` | Register custom in-game console commands |
| `settings` | `LuaSettings` | Access mod settings |
| `prototypes` | `LuaPrototypes` | Read-only access to all prototypes |
| `helpers` | `LuaHelpers` | Utility and helper functions |
| `defines` | `defines` | All game constants/enumerations |
| `storage` | `table` | Persistent per-save mod storage |

**Global functions:**

| Function | Description |
|----------|-------------|
| `log(string)` | Write to factorio-current.log |
| `localised_print(LocalisedString)` | Print localised string to stdout |
| `table_size(table) → uint` | Count entries in a table |
| `serpent.dump(value) → string` | Serialise any value to a string |

---

## Classes

### LuaGameScript (`game`)

Main toplevel type, provides access to most of the API though its members.

#### Methods

| Signature | Description |
|-----------|-------------|
| `auto_save(name?: string)` → `void` | Instruct the game to perform an auto-save. |
| `ban_player(player: PlayerIdentification | string, reason?: string)` → `void` | Bans the given player from this multiplayer game. Does nothing if this is a single player game of if the player running this isn't an admin. |
| `check_consistency()` → `void` | Run internal consistency checks. Allegedly prints any errors it finds. |
| `create_force(force: string)` → `LuaForce` | Create a new force. |
| `create_inventory(gui_title?: LocalisedString, size: uint16)` → `LuaInventory` | Creates an inventory that is not owned by any game object. |
| `create_profiler(stopped?: boolean)` → `LuaProfiler` | Creates a LuaProfilerLuaProfiler, which is used for measuring script performance. |
| `create_random_generator(seed?: uint32)` → `LuaRandomGenerator` | Creates a deterministic standalone random generator with the given seed or if a seed is not provided the initial map seed is used. |
| `create_surface(name: string, settings?: MapGenSettings)` → `LuaSurface` | Create a new surface. |
| `delete_surface(surface: SurfaceIdentification)` → `boolean` | Deletes the given surface and all entities on it if possible. |
| `disable_replay()` → `void` | Disables replay saving for the current save file. Once done there's no way to re-enable replay saving for the save file without loading an o |
| `force_crc()` → `void` | Force a CRC check. Tells all peers to calculate their current CRC, which are then compared to each other. If a mismatch is detected, the gam |
| `get_entity_by_tag(tag: string)` → `LuaEntity` | Gets an entity by its name tagLuaEntity::name_tag. Entity name tags can also be set in the entity "extra settings" GUI in the map editor. |
| `get_entity_by_unit_number(unit_number: uint32)` → `LuaEntity` | Returns entity with a specified unit number or nil if entity with such number was not found or prototype does not have EntityPrototypeFlags: |
| `get_map_exchange_string()` → `string` | Gets the map exchange string for the map generation settings that were used to create this map. |
| `get_player(player: uint32 | string)` → `LuaPlayer` | Gets the given player or returns `nil` if no player is found. |
| `get_pollution_statistics(surface: SurfaceIdentification)` → `LuaFlowStatistics` | The pollution statistics for this the given surface. |
| `get_script_inventories(mod?: string)` → `dict[string, array[LuaInventory]]` | Gets the inventories created through LuaGameScript::create_inventoryLuaGameScript::create_inventory. |
| `get_surface(surface: uint32 | string)` → `LuaSurface` | Gets the given surface or returns `nil` if no surface is found. |
| `get_vehicles(force?: ForceID, has_passenger?: boolean, is_moving?: boolean, surface?: SurfaceIdentification, type?: EntityID | array[EntityID], unit_number?: uint32)` → `array[LuaEntity]` | Returns vehicles in game. |
| `is_demo()` → `boolean` | Is this the demo version of Factorio? |
| `is_multiplayer()` → `boolean` | Whether the save is loaded as a multiplayer map. |
| `kick_player(player: PlayerIdentification, reason?: string)` → `void` | Kicks the given player from this multiplayer game. Does nothing if this is a single player game or if the player running this isn't an admin |
| `merge_forces(destination: ForceID, source: ForceID)` → `void` | Marks two forces to be merged together. All players and entities in the source force will be reassigned to the target force. The source forc |
| `mute_player(player: PlayerIdentification)` → `void` | Mutes the given player. Does nothing if the player running this isn't an admin. |
| `play_sound(sound_specification: PlaySoundSpecification)` → `void` | Play a sound for every player in the game. |
| `print(message: LocalisedString, print_settings?: PrintSettings)` → `void` | Print text to the chat console all players. |
| `purge_player(player: PlayerIdentification)` → `void` | Purges the given players messages from the game. Does nothing if the player running this isn't an admin. |
| `regenerate_entity(entities: string | array[string])` → `void` | Regenerate autoplacement of some entities on all surfaces. This can be used to autoplace newly-added entities. |
| `reload_mods()` → `void` | Forces a reload of all mods. |
| `reload_script()` → `void` | Forces a reload of the scenario script from the original scenario location. |
| `remove_offline_players(players?: array[PlayerIdentification])` → `void` | Remove players who are currently not connected from the map. |
| `reset_game_state()` → `void` | Reset scenario state game_finished, player_won, etc.. |
| `reset_time_played()` → `void` | Resets the amount of time played for this map. |
| `save_atlas()` → `void` | Saves the current configuration of Atlas to a file. This will result in huge file containing all of the game graphics moved to as small spac |
| `server_save(name?: string)` → `void` | Instruct the server to save the map. Only actually saves when in multiplayer. |
| `set_game_state(can_continue?: boolean, game_finished?: boolean, next_level?: string, player_won?: boolean)` → `void` | Set scenario state. Any parameters not provided do not change the current state. |
| `set_lose_ending_info(bullet_points?: array[LocalisedString], final_message?: LocalisedString, image_path?: string, message?: LocalisedString, title: LocalisedString)` → `void` | Set losing ending information for the current scenario. |
| `set_wait_for_screenshots_to_finish()` → `void` | Forces the screenshot saving system to wait until all queued screenshots have been written to disk. |
| `set_win_ending_info(bullet_points?: array[LocalisedString], final_message?: LocalisedString, image_path?: string, message?: LocalisedString, title: LocalisedString)` → `void` | Set winning ending information for the current scenario. |
| `show_message_dialog(image?: string, point_to?: GuiArrowSpecification, style?: string, text: LocalisedString, wrapper_frame_style?: string)` → `void` | Show an in-game message dialog. |
| `take_screenshot(allow_in_replay?: boolean, anti_alias?: boolean, by_player?: PlayerIdentification, daytime?: double, force_render?: boolean, hide_clouds?: boolean, hide_fog?: boolean, path?: string, player?: PlayerIdentification, position?: MapPosition, quality?: int32, resolution?: TilePosition, show_cursor_building_preview?: boolean, show_entity_info?: boolean, show_gui?: boolean, surface?: SurfaceIdentification, water_tick?: uint32, zoom?: double)` → `void` | Take a screenshot of the game and save it to the `script-output` folder, located in the game's user data directoryhttps://wiki.factorio.com/ |
| `take_technology_screenshot(path?: string, player: PlayerIdentification, quality?: int32, selected_technology?: TechnologyID, skip_disabled?: boolean)` → `void` | Take a screenshot of the technology screen and save it to the `script-output` folder, located in the game's user data directoryhttps://wiki. |
| `unban_player(player: PlayerIdentification | string)` → `void` | Unbans the given player from this multiplayer game. Does nothing if this is a single player game of if the player running this isn't an admi |
| `unmute_player(player: PlayerIdentification)` → `void` | Unmutes the given player. Does nothing if the player running this isn't an admin. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `allow_debug_settings` | ? | `?` | Whether players who are not adminsLuaPlayer::admin can access all debug settings. Set this to false to disallow access to most deb |
| `allow_tip_activation` | ? | `?` | If the tips are allowed to be activated in this scenario, it is false by default. |
| `autosave_enabled` | ? | `?` | True by default. Can be used to disable autosaving. Make sure to turn it back on soon after. |
| `backer_names` | ? | `?` | Array of the names of all the backers that supported the game development early on. These are used as names for labs, locomotives, |
| `blueprints` | ? | `?` | Records contained in the "game blueprints" tab of the blueprint library. |
| `connected_players` | ? | `?` | The players that are currently online. |
| `console_command_used` | ? | `?` | Whether a console command has been used. |
| `default_map_gen_settings` | ? | `?` | The default map gen settings for this save. |
| `difficulty` | ? | `?` | Current scenario difficulty. |
| `difficulty_settings` | ? | `?` | The currently active set of difficulty settings. Even though this property is marked as read-only, the members of the dictionary t |
| `draw_resource_selection` | ? | `?` | True by default. Can be used to disable the highlighting of resource patches when they are hovered on the map. |
| `enemy_has_vision_on_land_mines` | ? | `?` | Determines if enemy land mines are completely invisible or not. |
| `finished` | ? | `?` | True while the victory screen is shown. |
| `finished_but_continuing` | ? | `?` | True after players finished the game and clicked "continue". |
| `forces` | ? | `?` | Get a table of all the forces that currently exist. This sparse table allows you to find forces by indexing it with either their ` |
| `map_settings` | ? | `?` | The currently active set of map settings. Even though this property is marked as read-only, the members of the dictionary that is  |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `permissions` | ? | `?` |  |
| `planets` | ? | `?` |  |
| `player` | ? | `?` | This property is only populated inside custom commandLuaCommandProcessor handlers and when writing Lua console commandshttps://wik |
| `players` | ? | `?` | Get a table of all the players that currently exist. This sparse table allows you to find players by indexing it with either their |
| `simulation` | ? | `?` | Simulation-related functions, or `nil` if the current game is not a simulation. |
| `speed` | ? | `?` | Speed to update the map at. 1.0 is normal speed -- 60 UPS. Minimum value is 0.01. |
| `surfaces` | ? | `?` | Get a table of all the surfaces that currently exist. This sparse table allows you to find surfaces by indexing it with either the |
| `technology_notifications_enabled` | ? | `?` | True by default. Can be used to prevent the game engine from printing certain messages. |
| `tick` | ? | `?` | Current map tick. |
| `tick_paused` | ? | `?` | If the tick has been paused. This means that entity update has been paused. |
| `ticks_played` | ? | `?` | The number of ticks since this game was created using either "new game" or "new game from scenario". Notably, this number progress |
| `ticks_to_run` | ? | `?` | The number of ticks to be run while the tick is paused. |
| `train_manager` | ? | `?` |  |
---

### LuaPlayer

A player in the game.

#### Methods

| Signature | Description |
|-----------|-------------|
| `activate_paste()` → `void` | Gets a copy of the currently selected blueprint in the clipboard queue into the player's cursor, as if the player activated Paste. |
| `add_alert(entity: LuaEntity, type: defines.alert_type)` → `void` | Adds an alert to this player for the given entity of the given alert type. |
| `add_custom_alert(entity: LuaEntity, icon: SignalID, message: LocalisedString, show_on_map: boolean)` → `void` | Adds a custom alert to this player. |
| `add_pin(always_visible?: boolean, entity?: LuaEntity, label?: string, player?: PlayerIdentification, position?: MapPosition, preview_distance?: uint16, surface?: SurfaceIdentification)` → `void` | Adds a pin to this player for the given pin specification. Either entity, player, or surface and position must be defined. |
| `add_recipe_notification(recipe: RecipeID)` → `void` | Adds the given recipe to the list of recipe notifications for this player. |
| `add_to_clipboard(blueprint: LuaItemStack)` → `void` | Adds the given blueprint to this player's clipboard queue. |
| `associate_character(character: LuaEntity)` → `void` | Associates a character with this player. |
| `build_from_cursor(build_mode?: defines.build_mode, direction?: defines.direction, flip_horizontal?: boolean, flip_vertical?: boolean, mirror?: boolean, position: MapPosition, skip_fog_of_war?: boolean, terrain_building_size?: uint32)` → `void` | Builds whatever is in the cursor on the surface the player is on. The cursor stack will automatically be reduced as if the player built norm |
| `can_build_from_cursor(build_mode?: defines.build_mode, direction?: defines.direction, flip_horizontal?: boolean, flip_vertical?: boolean, position: MapPosition, skip_fog_of_war?: boolean, terrain_building_size?: uint32)` → `boolean` | Checks if this player can build what ever is in the cursor on the surface the player is on. |
| `clear_console()` → `void` | Clear the chat console. |
| `clear_cursor()` → `boolean` | Invokes the "clear cursor" action on the player as if the user pressed it. |
| `clear_inventory_highlights()` → `void` | Clears the blinking of the inventory based on insertion of items |
| `clear_local_flying_texts()` → `void` | Clear any active flying texts for this player. |
| `clear_recipe_notification(recipe: RecipeID)` → `void` | Clears the given recipe from the list of recipe notifications for this player. |
| `clear_recipe_notifications()` → `void` | Clears all recipe notifications for this player. |
| `clear_selection()` → `void` | Clears the player's selection tool selection position. |
| `connect_to_server(address: string, description?: LocalisedString, name?: LocalisedString, password?: string)` → `void` | Asks the player if they would like to connect to the given server. |
| `create_character(character?: EntityWithQualityID)` → `boolean` | Creates and attaches a character entity to this player. |
| `create_local_flying_text(color?: Color, create_at_cursor?: boolean, position?: MapPosition, speed?: double, surface?: SurfaceIdentification, text: LocalisedString, time_to_live?: uint32)` → `void` | Spawn flying text that is only visible to this player. Either `position` or `create_at_cursor` are required. When `create_at_cursor` is `tru |
| `disable_alert(alert_type: defines.alert_type)` → `boolean` | Disables alerts for the given alert category. |
| `disable_recipe_groups()` → `void` | Disable recipe groups. |
| `disable_recipe_subgroups()` → `void` | Disable recipe subgroups. |
| `disassociate_character(character: LuaEntity)` → `void` | Disassociates a character from this player. This is functionally the same as setting LuaEntity::associated_playerLuaEntity::associated_playe |
| `drag_wire(position: MapPosition)` → `boolean` | Start/end wire dragging at the specified location, wire type is based on the cursor contents |
| `enable_alert(alert_type: defines.alert_type)` → `boolean` | Enables alerts for the given alert category. |
| `enable_recipe_groups()` → `void` | Enable recipe groups. |
| `enable_recipe_subgroups()` → `void` | Enable recipe subgroups. |
| `enter_space_platform(space_platform: LuaSpacePlatform)` → `boolean` | Enters the given space platform if possible. |
| `exit_cutscene()` → `void` | Exit the current cutscene. Errors if not in a cutscene. |
| `exit_remote_view()` → `void` | Exit remote view if possible. Exiting will fail if the player is in a rocket or in a platform. |
| `get_active_quick_bar_page(index: uint32)` → `uint8` | Gets which quick bar page is being used for the given screen page or `nil` if not known. |
| `get_alerts(entity?: LuaEntity, position?: MapPosition, prototype?: LuaEntityPrototype, surface?: SurfaceIdentification, type?: defines.alert_type)` → `dict[uint32, dict[defines.alert_type, array[Alert]]]` | Get all alerts matching the given filters, or all alerts if no filters are given. |
| `get_associated_characters()` → `array[LuaEntity]` | The characters associated with this player. |
| `get_goal_description()` → `LocalisedString` | Get the current goal description, as a localised string. |
| `get_infinity_inventory_filter(index: uint32)` → `InfinityInventoryFilter` | Gets the filter for this map editor infinity filters at the given index or `nil` if the filter index doesn't exist or is empty. |
| `get_quick_bar_slot(index: uint32)` → `ItemFilter` | Gets the quick bar filter for the given slot or `nil`. |
| `get_recipe_notifications()` → `array[LuaRecipePrototype]` | Get all recipes that currently have recipe notifications for this player. |
| `is_alert_enabled(alert_type: defines.alert_type)` → `boolean` | If the given alert type is currently enabled. |
| `is_alert_muted(alert_type: defines.alert_type)` → `boolean` | If the given alert type is currently muted. |
| `is_shortcut_available(prototype_name: string)` → `boolean` | Is a custom Lua shortcut currently available? |
| `is_shortcut_toggled(prototype_name: string)` → `boolean` | Is a custom Lua shortcut currently toggled? |
| `jump_to_cutscene_waypoint(waypoint_index: uint32)` → `void` | Jump to the specified cutscene waypoint. Only works when the player is viewing a cutscene. |
| `land_on_planet()` → `boolean` | Ejects this player from the current space platform and lands on the current planet. |
| `leave_space_platform()` → `void` | Ejects this player from the current space platform if in a platform. The player is left on the platform at the position of the hub. |
| `mute_alert(alert_type: defines.alert_type)` → `boolean` | Mutes alerts for the given alert category. |
| `pipette(allow_ghost?: boolean, id: PipetteID, quality?: QualityID)` → `boolean` | Invokes the "smart pipette" action on the player as if the user pressed it. |
| `pipette_entity(allow_ghost?: boolean, entity: EntityWithQualityID)` → `boolean` | Invokes the "smart pipette" action on the player as if the user pressed it. This method is deprecated in favor of LuaPlayer::pipetteLuaPlaye |
| `play_sound(sound_specification: PlaySoundSpecification)` → `void` | Play a sound for this player. |
| `print(message: LocalisedString, print_settings?: PrintSettings)` → `void` | Print text to the chat console. |
| `print_entity_statistics(entities?: array[EntityWithQualityID])` → `void` | Print entity statistics to the player's console. |
| `print_lua_object_statistics()` → `void` | Print LuaObject counts per mod. |
| `print_robot_jobs()` → `void` | Print construction robot job counts to the player's console. |
| `remove_alert(entity?: LuaEntity, icon?: SignalID, message?: LocalisedString, position?: MapPosition, prototype?: EntityID, surface?: SurfaceIdentification, type?: defines.alert_type)` → `void` | Removes all alerts matching the given filters or if an empty filters table is given all alerts are removed. |
| `request_translation(localised_string: LocalisedString)` → `uint32` | Requests a translation for the given localised string. If the request is successful, the on_string_translatedon_string_translated event will |
| `request_translations(localised_strings: array[LocalisedString])` → `array[uint32]` | Requests translation for the given set of localised strings. If the request is successful, a on_string_translatedon_string_translated event  |
| `set_active_quick_bar_page(page_index: uint32, screen_index: uint32)` → `void` | Sets which quick bar page is being used for the given screen page. |
| `set_controller(character?: LuaEntity, chart_mode_cutoff?: double, final_transition_time?: uint32, position?: MapPosition, start_position?: MapPosition, start_zoom?: double, surface?: SurfaceIdentification, type: defines.controllers, waypoints?: array[CutsceneWaypoint])` → `void` | Set the controller type of the player. |
| `set_ending_screen_data(file?: string, message: LocalisedString)` → `void` | Setup the screen to be shown when the game is finished. |
| `set_goal_description(only_update?: boolean, text?: LocalisedString)` → `void` | Set the text in the goal window top left. |
| `set_infinity_inventory_filter(filter: InfinityInventoryFilter | nil, index: uint32)` → `void` | Sets the filter for this map editor infinity filters at the given index. |
| `set_quick_bar_slot(filter: LuaItemStack | ItemWithQualityID | nil, index: uint32)` → `void` | Sets the quick bar filter for the given slot. If a LuaItemStackLuaItemStack is provided, the slot will be set to that particular item instan |
| `set_shortcut_available(available: boolean, prototype_name: string)` → `void` | Make a custom Lua shortcut available or unavailable. |
| `set_shortcut_toggled(prototype_name: string, toggled: boolean)` → `void` | Toggle or untoggle a custom Lua shortcut |
| `set_zoom_limits(controller_type: defines.controllers, zoom_limits: ZoomLimits)` → `void` | Sets the zoom limits for a specific controller type. To reset a controller's zoom limits to default, pass an empty table for `zoom_limits`. |
| `start_selection(position: MapPosition, selection_mode: defines.selection_mode)` → `void` | Starts selection with selection tool from the specified position. Does nothing if the player's cursor is not a selection tool. |
| `swap_characters(player: PlayerIdentification)` → `boolean` | Swaps this player's character with another player's character. |
| `toggle_map_editor()` → `void` | Toggles this player into or out of the map editor. Does nothing if this player isn't an admin or if the player doesn't have permission to us |
| `unlock_achievement(name: string)` → `void` | Unlock the achievements of the given player. This has any effect only when this is the local player, the achievement isn't unlocked so far a |
| `unmute_alert(alert_type: defines.alert_type)` → `boolean` | Unmutes alerts for the given alert category. |
| `use_from_cursor(position: MapPosition)` → `void` | Uses the current item in the cursor if it's a capsule or does nothing if not. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `admin` | ? | `?` | `true` if the player is an admin. |
| `afk_time` | ? | `?` | How many ticks since the last action of this player. |
| `auto_sort_main_inventory` | ? | `?` | If the main inventory will be auto sorted. |
| `blueprint_to_setup` | ? | `?` | The item stack containing a blueprint to be setup. |
| `blueprints` | ? | `?` | Records contained in the player's blueprint library. |
| `centered_on` | ? | `?` | The entity being centered on in remote view. |
| `character` | ? | `?` | The character attached to this player, if any. Returns `nil` when the player is disconnected see LuaPlayer::connectedLuaPlayer::co |
| `chat_color` | ? | `?` | The color used when this player talks in game. |
| `color` | ? | `?` | The color associated with the player. This will be used to tint the player's character as well as their buildings and vehicles. |
| `connected` | ? | `?` | `true` if the player is currently connected to the game. |
| `controller_type` | ? | `?` |  |
| `cursor_stack_temporary` | ? | `?` | Returns true if the current item stack in cursor will be destroyed after clearing the cursor. Manually putting it into inventory s |
| `cutscene_character` | ? | `?` | When in a cutscene; the character this player would be using once the cutscene is over, if any. Returns `nil` when the player is d |
| `display_density_scale` | ? | `?` | The display density scale for this player. The display density scale is the factor of LuaPlayer::display_scaleLuaPlayer::display_s |
| `display_resolution` | ? | `?` | The display resolution for this player. |
| `display_scale` | ? | `?` | The display scale for this player. |
| `drag_target` | ? | `?` | The wire drag target for this player, if any. |
| `entity_copy_source` | ? | `?` | The source entity used during entity settings copy-paste, if any. |
| `game_view_settings` | ? | `?` | The player's game view settings. |
| `gui` | ? | `?` |  |
| `hand_location` | ? | `?` | The original location of the item in the cursor, marked with a hand. `nil` if the cursor stack is empty. When writing, the specifi |
| `index` | ? | `?` | This player's index in LuaGameScript::playersLuaGameScript::players unique ID. It is assigned when a player is created, and remain |
| `infinity_inventory_filters` | ? | `?` | The filters for this map editor infinity inventory settings. |
| `input_method` | ? | `?` | The input method of the player, mouse and keyboard or game controller |
| `last_online` | ? | `?` | At what tick this player was last online. |
| `locale` | ? | `?` | The active locale for this player. |
| `map_view_settings` | ? | `?` | The player's map view settings. To write to this, use a table containing the fields that should be changed. |
| `minimap_enabled` | ? | `?` | `true` if the minimap is visible. |
| `mod_settings` | ? | `?` | The current per-player settings for the this player, indexed by prototype name. Returns the same structure as LuaSettings::get_pla |
| `name` | ? | `?` | The player's username. |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `online_time` | ? | `?` | How many ticks did this player spend playing this save all sessions combined. |
| `opened_self` | ? | `?` | `true` if the player opened itself. I.e. if they opened the character or god-controller GUI. |
| `permission_group` | ? | `?` | The permission group this player is part of, if any. |
| `physical_controller_type` | ? | `?` | The player's "physical" controller. When a player is in the remote controller, this specifies the controller they will return to.  |
| `physical_position` | ? | `?` | The current position of this player's physical controller. |
| `physical_surface` | ? | `?` | The surface this player's physical controller is on. |
| `physical_surface_index` | ? | `?` | Unique ID associated with the surface this player's physical controller is currently on. |
| `physical_vehicle` | ? | `?` | The current vehicle of this player's physical controller. |
| `remove_unfiltered_items` | ? | `?` | If items not included in this map editor infinity inventory filters should be removed. |
| `render_mode` | ? | `?` | The render mode of the player, like map or zoom to world. |
| `show_on_map` | ? | `?` | If `true`, circle and name of given player is rendered on the map/chart. |
| `spectator` | ? | `?` | If `true`, zoom-to-world noise effect will be disabled and environmental sounds will be based on zoom-to-world view instead of pos |
| `spidertron_remote_selection` | ? | `?` | All SpiderVehicles currently selected by the player, if they are holding a spidertron remote. |
| `stashed_controller_type` | ? | `?` | The stashed controller type, if any. This is mainly useful when a player is in the map editor. |
| `tag` | ? | `?` | The tag that is shown after the player in chat, on the map and above multiplayer selection rectangles. |
| `ticks_to_respawn` | ? | `?` | The number of ticks until this player will respawn. `nil` if this player is not waiting to respawn. |
| `undo_redo_stack` | ? | `?` | The undo and redo stack for this player. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
| `zoom` | ? | `?` | The current player controller's zoom level. Must be positive. The baseline zoom level is 1. Values greater than 1 will zoom in clo |
| `zoom_limits` | ? | `?` | The player's current controller's zoom limits. |
---

### LuaControl (Base Class)

Base class shared by `LuaPlayer`, `LuaEntity` (character), and `LuaVehicle`.

This is an abstract base class containing the common functionality between LuaPlayerLuaPlayer and entities see LuaEntityLuaEntity.

#### Methods

| Signature | Description |
|-----------|-------------|
| `begin_crafting(count: uint32, recipe: RecipeID, silent?: boolean)` → `uint32` | Begins crafting the given count of the given recipe. |
| `can_insert(items: ItemStackIdentification)` → `boolean` | Can at least some items be inserted? |
| `can_place_entity(direction?: defines.direction, name: EntityID, position: MapPosition)` → `boolean` | Checks if this character or player can build the given entity at the given location on the surface the character or player is on. |
| `can_reach_entity(entity: LuaEntity)` → `boolean` | Can a given entity be opened or accessed? |
| `cancel_crafting(count: uint32, index: uint32)` → `void` | Cancels the given amount of crafts at the given crafting queue position. If this causes any later crafts that depend on the cancelled one to |
| `clear_gui_arrow()` → `void` | Removes the arrow created by `set_gui_arrow`. |
| `clear_items_inside()` → `void` | Remove all items from this entity. |
| `clear_selected_entity()` → `void` | Unselect any selected entity. |
| `close_factoriopedia_gui()` → `void` | Closes the Factoriopedia GUI if it's open. |
| `disable_flashlight()` → `void` | Disable the flashlight. |
| `enable_flashlight()` → `void` | Enable the flashlight. |
| `get_craftable_count(recipe: RecipeID)` → `uint32` | Gets the count of the given recipe that can be crafted. |
| `get_inventory(inventory: defines.inventory)` → `LuaInventory` | Get an inventory belonging to this entity. This can be either the "main" inventory or some auxiliary one, like the module slots or logistic  |
| `get_inventory_name(inventory: defines.inventory)` → `string` | Get name of inventory. Names match keys of defines.inventorydefines.inventory. |
| `get_item_count(item?: ItemFilter)` → `uint32` | Get the number of all or some items in this entity. |
| `get_main_inventory()` → `LuaInventory` | Gets the main inventory for this character or player if this is a character or player. |
| `get_max_inventory_index()` → `defines.inventory` | The highest index of all inventories this entity can use. Allows iteration over all of them if desired. |
| `get_requester_point()` → `LuaLogisticPoint` | Gets the requester logistic point for this entity if it has one. |
| `has_items_inside()` → `boolean` | Does this entity have any item inside it? |
| `insert(items: ItemStackIdentification)` → `uint32` | Insert items into this entity. This works the same way as inserters or shift-clicking: the "best" inventory is chosen automatically. |
| `is_cursor_blueprint()` → `boolean` | Returns whether the player is holding a blueprint. This takes both blueprint items as well as blueprint records from the blueprint library i |
| `is_cursor_empty()` → `boolean` | Returns whether the player is holding something in the cursor. Takes into account items from the blueprint library, as well as items and gho |
| `is_flashlight_enabled()` → `boolean` | Is the flashlight enabled for the current controller. Only supported by defines.controllers.characterdefines.controllers.character and defin |
| `is_player()` → `boolean` | When `true` control adapter is a LuaPlayer object, `false` for entities including characters with players. |
| `mine_entity(entity: LuaEntity, force?: boolean)` → `boolean` | Mines the given entity as if this player or character mined it. |
| `mine_tile(tile: LuaTile)` → `boolean` | Mines the given tile as if this player or character mined it. |
| `open_factoriopedia_gui(prototype?: FactoriopediaID)` → `void` | Open the Factoriopedia GUI and select a given entry, if any valid ID is given. |
| `open_technology_gui(technology?: TechnologyID)` → `void` | Open the technology GUI and select a given technology. |
| `remove_item(items: ItemStackIdentification)` → `uint32` | Remove items from this entity. |
| `set_driving(driving: boolean, force?: boolean)` → `void` | Sets if this character or player is driving. Returns if the player or character is still driving. |
| `set_gui_arrow(margin: uint32, type: GuiArrowType)` → `void` | Create an arrow which points at this entity. This is used in the tutorial. For examples, see `control.lua` in the campaign missions. |
| `teleport(build_check_type?: defines.build_check_type, position: MapPosition, raise_teleported?: boolean, snap_to_grid?: boolean, surface?: SurfaceIdentification)` → `boolean` | Teleport the entity to a given position, possibly on another surface. |
| `update_selected_entity(position: MapPosition)` → `void` | Select an entity, as if by hovering the mouse above it. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `build_distance` | ? | `?` | The build distance of this character or max uint when not a character or player connected to a character. |
| `cargo_pod` | ? | `?` | The cargo pod the player is currently sitting in or the cargo pod attached to this rocket silo. |
| `character_additional_mining_categories` | ? | `?` | When called on a LuaPlayerLuaPlayer, it must be associated with a character see LuaPlayer::characterLuaPlayer::character. |
| `character_build_distance_bonus` | ? | `?` | When called on a LuaPlayerLuaPlayer, it must be associated with a character see LuaPlayer::characterLuaPlayer::character. |
| `character_crafting_speed_modifier` | ? | `?` | When called on a LuaPlayerLuaPlayer, it must be associated with a character see LuaPlayer::characterLuaPlayer::character. |
| `character_health_bonus` | ? | `?` | When called on a LuaPlayerLuaPlayer, it must be associated with a character see LuaPlayer::characterLuaPlayer::character. |
| `character_inventory_slots_bonus` | ? | `?` | When called on a LuaPlayerLuaPlayer, it must be associated with a character see LuaPlayer::characterLuaPlayer::character. |
| `character_item_drop_distance_bonus` | ? | `?` | When called on a LuaPlayerLuaPlayer, it must be associated with a character see LuaPlayer::characterLuaPlayer::character. |
| `character_item_pickup_distance_bonus` | ? | `?` | When called on a LuaPlayerLuaPlayer, it must be associated with a character see LuaPlayer::characterLuaPlayer::character. |
| `character_loot_pickup_distance_bonus` | ? | `?` | When called on a LuaPlayerLuaPlayer, it must be associated with a character see LuaPlayer::characterLuaPlayer::character. |
| `character_maximum_following_robot_count_bonus` | ? | `?` | When called on a LuaPlayerLuaPlayer, it must be associated with a character see LuaPlayer::characterLuaPlayer::character. |
| `character_mining_progress` | ? | `?` | The current mining progress between 0 and 1 of this character, or 0 if they aren't mining. |
| `character_mining_speed_modifier` | ? | `?` | When called on a LuaPlayerLuaPlayer, it must be associated with a character see LuaPlayer::characterLuaPlayer::character. |
| `character_reach_distance_bonus` | ? | `?` | When called on a LuaPlayerLuaPlayer, it must be associated with a character see LuaPlayer::characterLuaPlayer::character. |
| `character_resource_reach_distance_bonus` | ? | `?` | When called on a LuaPlayerLuaPlayer, it must be associated with a character see LuaPlayer::characterLuaPlayer::character. |
| `character_running_speed` | ? | `?` | The current movement speed of this character, including effects from exoskeletons, tiles, stickers and shooting. |
| `character_running_speed_modifier` | ? | `?` | Modifies the running speed of this character by the given value as a percentage. Setting the running modifier to `0.5` makes the c |
| `character_trash_slot_count_bonus` | ? | `?` | When called on a LuaPlayerLuaPlayer, it must be associated with a character see LuaPlayer::characterLuaPlayer::character. |
| `cheat_mode` | ? | `?` | When `true` hand crafting is free and instant. |
| `crafting_queue` | ? | `?` | The current crafting queue items. |
| `crafting_queue_progress` | ? | `?` | The crafting queue progress in the range `0-1`. `0` when no recipe is being crafted. |
| `crafting_queue_size` | ? | `?` | Size of the crafting queue. |
| `cursor_ghost` | ? | `?` | The ghost prototype in the player's cursor. |
| `cursor_record` | ? | `?` | The blueprint record in the player's cursor. |
| `cursor_stack` | ? | `?` | The player's cursor stack. `nil` if the player controller is a spectator. |
| `driving` | ? | `?` | `true` if the player is in a vehicle. Writing to this attribute puts the player in or out of a vehicle. |
| `drop_item_distance` | ? | `?` | The item drop distance of this character or max uint when not a character or player connected to a character. |
| `flight_height` | ? | `?` | The current flight height for this player or character entity. |
| `following_robots` | ? | `?` | The current combat robots following the character. |
| `force` | ? | `?` | The force of this entity. Reading will always give a LuaForceLuaForce, but it is possible to assign either stringstring, uint8uint |
| `force_index` | ? | `?` | Unique indexLuaForce::index ID associated with the force of this entity. |
| `hub` | ? | `?` | The space platform hub the player is currently sitting in. |
| `in_combat` | ? | `?` | Whether this character entity is in combat. |
| `is_flying` | ? | `?` | If this player or character entity is flying. |
| `item_pickup_distance` | ? | `?` | The item pickup distance of this character or max double when not a character or player connected to a character. |
| `loot_pickup_distance` | ? | `?` | The loot pickup distance of this character or max double when not a character or player connected to a character. |
| `mining_state` | ? | `?` | Current mining state. |
| `opened` | ? | `?` | The GUI the player currently has open. |
| `opened_gui_type` | ? | `?` |  |
| `picking_state` | ? | `?` | Current item-picking state. |
| `position` | ? | `?` | The current position of the entity. |
| `reach_distance` | ? | `?` | The reach distance of this character or max uint when not a character or player connected to a character. |
| `render_position` | ? | `?` | The current render position of the entity. |
| `repair_state` | ? | `?` | Current repair state. |
| `resource_reach_distance` | ? | `?` | The resource reach distance of this character or max double when not a character or player connected to a character. |
| `riding_state` | ? | `?` | Current riding state of this car, or of the car this player is riding in. |
| `selected` | ? | `?` | The currently selected entity. Assigning an entity will select it if is selectable, otherwise the selection is cleared. |
| `shooting_state` | ? | `?` | Current shooting state. |
| `surface` | ? | `?` | The surface this entity is currently on. |
| `surface_index` | ? | `?` | Unique indexLuaSurface::index ID associated with the surface this entity is currently on. |
| `vehicle` | ? | `?` | The vehicle the player is currently sitting in. |
| `walking_state` | ? | `?` | Current walking state of the player, or the spider-vehicle the character is driving. |
---

### LuaSurface

A "domain" of the world, such as a planet or space platform.

#### Methods

| Signature | Description |
|-----------|-------------|
| `add_script_area(area: ScriptArea)` → `uint32` | Adds the given script area. |
| `add_script_position(position: ScriptPosition)` → `uint32` | Adds the given script position. |
| `build_checkerboard(area: BoundingBox)` → `void` | Sets the given area to the checkerboard lab tiles. |
| `build_enemy_base(force?: ForceID, position: MapPosition, unit_count: uint32)` → `void` | Send a group to build a new base. |
| `calculate_tile_properties(positions: array[MapPosition], property_names: array[string])` → `dict[string, array[double]]` | Calculate values for a list of tile properties at a list of positions. Requests for unrecognized properties will be ignored, so this can als |
| `can_fast_replace(direction?: defines.direction, force?: ForceID, name: EntityID, position: MapPosition)` → `boolean` | If there exists an entity at the given location that can be fast-replaced with the given entity parameters. |
| `can_place_entity(build_check_type?: defines.build_check_type, direction?: defines.direction, force?: ForceID, forced?: boolean, inner_name?: string, name: EntityID, position: MapPosition)` → `boolean` | Check for collisions with terrain or other entities. |
| `cancel_deconstruct_area(area: BoundingBox, force: ForceID, item?: LuaItemStack, player?: PlayerIdentification, skip_fog_of_war?: boolean, super_forced?: boolean, undo_index?: uint32)` → `void` | Cancel a deconstruction order. |
| `cancel_upgrade_area(area: BoundingBox, force: ForceID, item: LuaItemStack, player?: PlayerIdentification, skip_fog_of_war?: boolean)` → `void` | Cancel a upgrade order. |
| `clear(ignore_characters?: boolean)` → `void` | Clears this surface deleting all entities and chunks on it. |
| `clear_hidden_tiles()` → `void` | Completely removes hidden and double hidden tiles data on this surface. |
| `clear_pollution()` → `void` | Clears all pollution on this surface. |
| `clear_territory_for_chunks(chunk_positions: array[ChunkPosition])` → `void` | Removes the chunk from the territory it is associated with if any and allows the map generator to potentially generate a new territory for t |
| `clone_area(clear_destination_decoratives?: boolean, clear_destination_entities?: boolean, clone_decoratives?: boolean, clone_entities?: boolean, clone_tiles?: boolean, create_build_effect_smoke?: boolean, destination_area: BoundingBox, destination_force?: ForceID, destination_surface?: SurfaceIdentification, expand_map?: boolean, source_area: BoundingBox)` → `void` | Clones the given area. |
| `clone_brush(clear_destination_decoratives?: boolean, clear_destination_entities?: boolean, clone_decoratives?: boolean, clone_entities?: boolean, clone_tiles?: boolean, create_build_effect_smoke?: boolean, destination_force?: LuaForce | string, destination_offset: TilePosition, destination_surface?: SurfaceIdentification, expand_map?: boolean, manual_collision_mode?: boolean, source_offset: TilePosition, source_positions: array[TilePosition])` → `void` | Clones the given area. |
| `clone_entities(create_build_effect_smoke?: boolean, destination_force?: ForceID, destination_offset: Vector, destination_surface?: SurfaceIdentification, entities: array[LuaEntity], snap_to_grid?: boolean)` → `void` | Clones the given entities. |
| `count_entities_filtered(filter: EntitySearchFilters)` → `uint32` | Count entities of given type or name in a given area. Works just like LuaSurface::find_entities_filteredLuaSurface::find_entities_filtered,  |
| `count_tiles_filtered(filter: TileSearchFilters)` → `uint32` | Count tiles of a given name in a given area. Works just like LuaSurface::find_tiles_filteredLuaSurface::find_tiles_filtered, except this onl |
| `create_decoratives(check_collision?: boolean, decoratives: array[Decorative])` → `void` | Adds the given decoratives to the surface. |
| `create_entities_from_blueprint_string(by_player?: PlayerIdentification, direction?: defines.direction, flip_horizontal?: boolean, flip_vertical?: boolean, force?: ForceID, position: MapPosition, string: string)` → `int32` | This method only works when used in simulations. |
| `create_entity(burner_fuel_inventory?: BlueprintInventoryWithFilters, cause?: LuaEntity | ForceID, character?: LuaEntity, create_build_effect_smoke?: boolean, direction?: defines.direction, fast_replace?: boolean, force?: ForceID, item?: LuaItemStack, mirror?: boolean, move_stuck_players?: boolean, name: EntityID, player?: PlayerIdentification, position: MapPosition, preserve_ghosts_and_corpses?: boolean, quality?: QualityID, raise_built?: boolean, register_plant?: boolean, snap_to_grid?: boolean, source?: LuaEntity | MapPosition, spawn_decorations?: boolean, spill?: boolean, target?: LuaEntity | MapPosition, undo_index?: uint32)` → `LuaEntity` | Create an entity on this surface. |
| `create_global_electric_network()` → `void` | Creates a global electric network for this surface, if one doesn't exist already. |
| `create_particle(frame_speed: float, height: float, movement: Vector, name: ParticleID, position: MapPosition, vertical_speed: float)` → `void` | Creates a particle at the given location |
| `create_segmented_unit(force?: ForceID, name: EntityID, quality?: QualityID, territory?: LuaTerritory)` → `LuaSegmentedUnit` | Create a segmented unit on the surface. This differs from creating an entity with type `"segmented-unit"` in that this method can create the |
| `create_territory(chunks: array[ChunkPosition], patrol_path?: array[MapPosition])` → `LuaTerritory` | Create a territory on the surface. |
| `create_trivial_smoke(name: TrivialSmokeID, position: MapPosition)` → `void` |  |
| `create_unit_group(force?: ForceID, position: MapPosition)` → `LuaCommandable` | Create a new unit group at a given position. |
| `deconstruct_area(area: BoundingBox, force: ForceID, item?: LuaItemStack, player?: PlayerIdentification, skip_fog_of_war?: boolean, super_forced?: boolean)` → `void` | Place a deconstruction request. |
| `decorative_prototype_collides(position: MapPosition, prototype: DecorativeID)` → `boolean` | Whether the given decorative prototype collides at the given position and direction. |
| `delete_chunk(chunk_position: ChunkPosition)` → `void` |  |
| `destroy_decoratives(area?: BoundingBox, collision_mask?: CollisionLayerID | array[CollisionLayerID] | dict[CollisionLayerID, True], exclude_soft?: boolean, from_layer?: string, invert?: boolean, limit?: uint32, name?: DecorativeID | array[DecorativeID], position?: TilePosition, to_layer?: string)` → `void` | Removes all decoratives from the given area. If no area and no position are given, then the entire surface is searched. |
| `destroy_global_electric_network()` → `void` | Destroys the global electric network for this surface, if it exists. |
| `edit_script_area(area: ScriptArea, id: uint32)` → `void` | Sets the given script area to the new values. |
| `edit_script_position(id: uint32, position: ScriptPosition)` → `void` | Sets the given script position to the new values. |
| `entity_prototype_collides(direction?: defines.direction, position: MapPosition, prototype: EntityID, use_map_generation_bounding_box: boolean)` → `boolean` | Whether the given entity prototype collides at the given position and direction. |
| `execute_lightning(name: EntityID, position: MapPosition)` → `void` | Creates lightning. If other entities which can be lightning targets are nearby, the final position will be adjusted. |
| `find_closest_logistic_network_by_position(force: ForceID, position: MapPosition)` → `LuaLogisticNetwork` | Find the logistic network with a cell closest to a given position. |
| `find_decoratives_filtered(area?: BoundingBox, collision_mask?: CollisionLayerID | array[CollisionLayerID] | dict[CollisionLayerID, True], exclude_soft?: boolean, from_layer?: string, invert?: boolean, limit?: uint32, name?: DecorativeID | array[DecorativeID], position?: TilePosition, to_layer?: string)` → `array[DecorativeResult]` | Find decoratives of a given name in a given area. |
| `find_enemy_units(center: MapPosition, force?: ForceID, radius: double)` → `array[LuaEntity]` | Find enemy units entities with type "unit" of a given force within an area. |
| `find_entities(area?: BoundingBox)` → `array[LuaEntity]` | Find entities in a given area. |
| `find_entities_filtered(filter: EntitySearchFilters)` → `array[LuaEntity]` | Find all entities of the given type or name in the given area. |
| `find_entity(entity: EntityWithQualityID, position: MapPosition)` → `LuaEntity` | Find an entity of the given name at the given position. This checks both the exact position and the bounding box of the entity. |
| `find_logistic_network_by_position(force: ForceID, position: MapPosition)` → `LuaLogisticNetwork` | Find the logistic network that covers a given position. |
| `find_logistic_networks_by_construction_area(force: ForceID, position: MapPosition)` → `array[LuaLogisticNetwork]` | Finds all of the logistics networks whose construction area intersects with the given position. |
| `find_nearest_enemy(force?: ForceID, max_distance: double, position: MapPosition)` → `LuaEntity` | Find the enemy military target military entityhttps://wiki.factorio.com/Military_units_and_structures closest to the given position. |
| `find_nearest_enemy_entity_with_owner(force?: ForceID, max_distance: double, position: MapPosition)` → `LuaEntity` | Find the enemy entity-with-owner closest to the given position. |
| `find_non_colliding_position(center: MapPosition, force_to_tile_center?: boolean, name: EntityID, precision: double, radius: double)` → `MapPosition` | Find a non-colliding position within a given radius. |
| `find_non_colliding_position_in_box(force_to_tile_center?: boolean, name: EntityID, precision: double, search_space: BoundingBox)` → `MapPosition` | Find a non-colliding position within a given rectangle. |
| `find_tiles_filtered(filter: TileSearchFilters)` → `array[LuaTile]` | Find all tiles of the given name in the given area. |
| `find_units(area: BoundingBox, condition: ForceCondition, force: ForceID)` → `array[LuaEntity]` | Find units entities with type "unit" of a given force and force condition within a given area. |
| `force_generate_chunk_requests()` → `void` | Blocks and generates all chunks that have been requested using all available threads. |
| `get_chunks()` → `LuaChunkIterator` | Get an iterator going over every chunk on this surface. |
| `get_closest(entities: array[LuaEntity], position: MapPosition)` → `LuaEntity` | Gets the closest entity in the list to this position. |
| `get_connected_tiles(area?: BoundingBox, include_diagonal?: boolean, position: TilePosition, tiles: array[TileID])` → `array[TilePosition]` | Gets all tiles of the given types that are connected horizontally or vertically to the given tile position including the given tile position |
| `get_default_cover_tile(force: ForceID, tile: TileID)` → `LuaTilePrototype` | Gets the cover tile for the given force and tile on this surface if one is set. |
| `get_double_hidden_tile(position: TilePosition)` → `string` | The double hidden tile name or `nil` if there isn't one for the given position. |
| `get_entities_with_force(chunk_position: ChunkPosition, force: ForceID)` → `array[LuaEntity]` | Returns all the military targets entities with force on this chunk for the given force. |
| `get_hidden_tile(position: TilePosition)` → `string` | The hidden tile name. |
| `get_map_exchange_string()` → `string` | Gets the map exchange string for the current map generation settings of this surface. |
| `get_pollution(position: MapPosition)` → `double` | Get the pollution for a given position. |
| `get_property(property: SurfacePropertyID)` → `double` | Gets the value of surface property on this surface. |
| `get_random_chunk()` → `ChunkPosition` | Gets a random generated chunk position or 0,0 if no chunks have been generated on this surface. |
| `get_resource_counts()` → `dict[string, uint32]` | Gets the resource amount of all resources on this surface |
| `get_script_area(key?: string | uint32)` → `ScriptArea` | Gets the first script area by name or id. |
| `get_script_areas(name?: string)` → `array[ScriptArea]` | Gets the script areas that match the given name or if no name is given all areas are returned. |
| `get_script_position(key?: string | uint32)` → `ScriptPosition` | Gets the first script position by name or id. |
| `get_script_positions(name?: string)` → `array[ScriptPosition]` | Gets the script positions that match the given name or if no name is given all positions are returned. |
| `get_segmented_units()` → `array[LuaSegmentedUnit]` | Get all segmented units that exist on the surface. |
| `get_starting_area_radius()` → `double` | Gets the starting area radius of this surface. |
| `get_territories()` → `array[LuaTerritory]` | Get all territories on the surface. |
| `get_territory_for_chunk(chunk_position: ChunkPosition)` → `LuaTerritory` | Get the territory that the given chunk is assigned to. If the chunk is not part of any territory or the territory for the chunk has not yet  |
| `get_tile(x: int32, y: int32)` → `LuaTile` | Get the tile at a given position. An alternative call signature for this method is passing it a single TilePositionTilePosition. |
| `get_total_pollution()` → `double` | Gets the total amount of pollution on the surface by iterating over all the chunks containing pollution. |
| `is_chunk_generated(chunk_position: ChunkPosition)` → `boolean` | Is a given chunk generated? |
| `play_sound(sound_specification: PlaySoundSpecification)` → `void` | Play a sound for every player on this surface. |
| `pollute(amount: double, prototype?: EntityID, source: MapPosition)` → `void` | Spawn pollution at the given position. |
| `print(message: LocalisedString, print_settings?: PrintSettings)` → `void` | Print text to the chat console of all players on this surface. |
| `regenerate_decorative(chunks?: array[ChunkPosition], decoratives?: string | array[string])` → `void` | Regenerate autoplacement of some decoratives on this surface. This can be used to autoplace newly-added decoratives. |
| `regenerate_entity(chunks?: array[ChunkPosition], entities?: string | array[string])` → `void` | Regenerate autoplacement of some entities on this surface. This can be used to autoplace newly-added entities. |
| `remove_script_area(id: uint32)` → `boolean` | Removes the given script area. |
| `remove_script_position(id: uint32)` → `boolean` | Removes the given script position. |
| `request_path(bounding_box: BoundingBox, can_open_gates?: boolean, collision_mask: CollisionMask, entity_to_ignore?: LuaEntity, force: ForceID, goal: MapPosition, max_attack_distance?: double, max_gap_size?: int32, path_resolution_modifier?: int32, pathfind_flags?: PathfinderFlags, radius?: double, start: MapPosition)` → `uint32` | Generates a path with the specified constraints as an array of PathfinderWaypointsPathfinderWaypoint using the unit pathfinding algorithm. T |
| `request_to_generate_chunks(position: MapPosition, radius?: uint32)` → `void` | Request that the game's map generator generate chunks at the given position for the given radius on this surface. If the radius is `0`, then |
| `set_chunk_generated_status(chunk_position: ChunkPosition, status: defines.chunk_generated_status)` → `void` | Set generated status of a chunk. Useful when copying chunks. |
| `set_default_cover_tile(force: ForceID, from_tile: TileID, to_tile: TileID | nil)` → `void` | Sets the cover tile for the given force and tile on this surface. |
| `set_double_hidden_tile(position: TilePosition, tile?: TileID)` → `void` | Set double hidden tile for the specified position. During normal gameplay, only non-mineableLuaTilePrototype::mineable_properties tiles can  |
| `set_hidden_tile(position: TilePosition, tile?: TileID)` → `void` | Set the hidden tile for the specified position. While during normal gameplay only non-mineableLuaTilePrototype::mineable_properties or found |
| `set_multi_command(command: Command, force?: ForceID, unit_count: uint32, unit_search_distance?: uint32)` → `uint32` | Give a command to multiple units. This will automatically select suitable units for the task. |
| `set_pollution(amount: double, position: MapPosition)` → `void` | Set the pollution for a given position. |
| `set_property(property: SurfacePropertyID, value: double)` → `void` | Sets the value of surface property on this surface. |
| `set_territory_for_chunks(chunk_positions: array[ChunkPosition], territory?: LuaTerritory)` → `void` | Removes the given chunks from their current territories and adds them to the given territory if provided. |
| `set_tiles(correct_tiles?: boolean, player?: PlayerIdentification, raise_event?: boolean, remove_colliding_decoratives?: boolean, remove_colliding_entities?: boolean | 'abort_on_collision', tiles: array[Tile], undo_index?: uint32)` → `void` | Set tiles at specified locations. Can automatically correct the edges around modified tiles. |
| `spill_inventory(allow_belts?: boolean, drop_full_stack?: boolean, enable_looted?: boolean, force?: ForceID, inventory: LuaInventory, max_radius?: double, position: MapPosition, use_start_position_on_failure?: boolean)` → `array[LuaEntity]` | Spill inventory on the ground centered at a given location. |
| `spill_item_stack(allow_belts?: boolean, drop_full_stack?: boolean, enable_looted?: boolean, force?: ForceID, max_radius?: double, position: MapPosition, stack: ItemStackIdentification, use_start_position_on_failure?: boolean)` → `array[LuaEntity]` | Spill items on the ground centered at a given location. |
| `upgrade_area(area: BoundingBox, force: ForceID, item: LuaItemStack, player?: PlayerIdentification, skip_fog_of_war?: boolean)` → `void` | Place an upgrade request. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `always_day` | ? | `?` | When set to true, the sun will always shine. |
| `brightness_visual_weights` | ? | `?` | Defines how surface daytime brightness influences each color channel of the current color lookup table LUT. |
| `darkness` | ? | `?` | Amount of darkness at the current time, as a number in range `0, 1`. |
| `dawn` | ? | `?` | The daytime when dawn starts. |
| `daytime` | ? | `?` | Current time of day, as a number in range `0, 1`. |
| `daytime_parameters` | ? | `?` | Parameters of daytime. Equivalent as reading duskLuaSurface::dusk, eveningLuaSurface::evening, morningLuaSurface::morning and dawn |
| `deletable` | ? | `?` | If this surface can be deleted. |
| `dusk` | ? | `?` | The daytime when dusk starts. |
| `evening` | ? | `?` | The daytime when evening starts. |
| `freeze_daytime` | ? | `?` | True if daytime is currently frozen. |
| `generate_with_lab_tiles` | ? | `?` | When set to true, new chunks will be generated with lab tiles, instead of using the surface's map generation settings. |
| `global_effect` | ? | `?` | Surface-wide effects applied to entities with effect receivers. `nil` if this surface is not using surface-wide effect source. |
| `global_electric_network_statistics` | ? | `?` | The global electric network statistics for this surface. |
| `has_global_electric_network` | ? | `?` | Whether this surface currently has a global electric network. |
| `ignore_surface_conditions` | ? | `?` | If surface condition checks should not be performed on this surface. |
| `index` | ? | `?` | This surface's index in LuaGameScript::surfacesLuaGameScript::surfaces unique ID. It is assigned when a surface is created, and re |
| `localised_name` | ? | `?` | Localised name of this surface. When set, will replace the internal surface name in places where a player sees surface name. |
| `map_gen_settings` | ? | `?` | The generation settings for this surface. These can be modified after surface generation, but note that this will not retroactivel |
| `min_brightness` | ? | `?` | The minimal brightness during the night. Defaults to `0.15`. This has an effect on both rendering and game mechanics such as biter |
| `morning` | ? | `?` | The daytime when morning starts. |
| `name` | ? | `?` | The name of this surface. Names are unique among surfaces. |
| `no_enemies_mode` | ? | `?` | Is no-enemies mode enabled on this surface? |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `peaceful_mode` | ? | `?` | Is peaceful mode enabled on this surface? |
| `planet` | ? | `?` | The planet associated with this surface, if there is one. |
| `platform` | ? | `?` |  |
| `pollutant_type` | ? | `?` | The type of pollutant enabled on the surface, or `nil` if no pollutant is enabled. |
| `pollution_statistics` | ? | `?` | The pollution statistics for this surface. |
| `show_clouds` | ? | `?` | If clouds are shown on this surface. If false, clouds are never shown. If true the player must also have clouds enabled in graphic |
| `solar_power_multiplier` | ? | `?` | The multiplier of solar power on this surface. Cannot be less than 0. |
| `ticks_per_day` | ? | `?` | The number of ticks per day for this surface. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
| `wind_orientation` | ? | `?` | Current wind direction. |
| `wind_orientation_change` | ? | `?` | Change in wind orientation per tick. |
| `wind_speed` | ? | `?` | Current wind speed in tiles per tick. |
---

### LuaEntity

The primary interface for interacting with entities through the Lua API.

#### Methods

| Signature | Description |
|-----------|-------------|
| `add_autopilot_destination(position: MapPosition)` → `void` | Adds the given position to this spidertron's autopilot's queue of destinations. |
| `add_market_item(offer: Offer)` → `void` | Offer a thing on the market. |
| `apply_upgrade()` → `LuaEntity, LuaEntity` | Upgrades this entity in place if it's marked to be upgraded. |
| `can_be_destroyed()` → `boolean` | Whether the entity can be destroyed |
| `can_set_inventory_filter(filter: ItemFilter, index: uint32, inventory_index: defines.inventory)` → `boolean` | The same as LuaInventory::can_set_filterLuaInventory::can_set_filter but also works for ghosts where the inventory is not available through  |
| `can_shoot(position: MapPosition, target: LuaEntity)` → `boolean` | Whether this character can shoot the given entity or position. |
| `can_wires_reach(entity: LuaEntity)` → `boolean` | Can wires reach between these entities. |
| `cancel_deconstruction(force: ForceID, player?: PlayerIdentification)` → `void` | Cancels deconstruction if it is scheduled, does nothing otherwise. |
| `cancel_upgrade(force: ForceID, player?: PlayerIdentification)` → `boolean` | Cancels upgrade if it is scheduled, does nothing otherwise. |
| `clear_fluid_inside()` → `void` | Remove all fluids from this entity. |
| `clear_market_items()` → `void` | Removes all offers from a market. |
| `clone(create_build_effect_smoke?: boolean, force?: ForceID, position: MapPosition, surface?: LuaSurface)` → `LuaEntity` | Clones this entity. |
| `connect_linked_belts(neighbour?: LuaEntity)` → `void` | Connects current linked belt with another one. |
| `connect_rolling_stock(direction: defines.rail_direction)` → `boolean` | Connects the rolling stock in the given direction. |
| `copy_settings(by_player?: PlayerIdentification, entity: LuaEntity)` → `ItemWithQualityCounts` | Copies settings from the given entity onto this entity. |
| `create_build_effect_smoke()` → `void` | Creates the same smoke that is created when you place a building by hand. |
| `create_cargo_pod(cargo_hatch?: LuaCargoHatch)` → `LuaEntity` | Creates a cargo pod if possible. |
| `damage(cause?: LuaEntity, damage: float, force: ForceID, source?: LuaEntity, type?: DamageTypeID)` → `float` | Damages the entity. |
| `deplete()` → `void` | Depletes and destroys this resource entity. |
| `destroy(do_cliff_correction?: boolean, player?: PlayerIdentification, raise_destroy?: boolean, undo_index?: uint32)` → `boolean` | Destroys the entity. |
| `die(cause?: LuaEntity, force?: ForceID)` → `boolean` | Immediately kills the entity. Does nothing if the entity doesn't have health. |
| `disconnect_linked_belts()` → `void` | Disconnects linked belt from its neighbour. |
| `disconnect_rolling_stock(direction: defines.rail_direction)` → `boolean` | Tries to disconnect this rolling stock in the given direction. |
| `force_finish_ascending()` → `void` | Take an ascending cargo pod and safely make it skip all animation and immediately switch surface. |
| `force_finish_descending()` → `void` | Take a descending cargo pod and safely make it arrive and deposit cargo. |
| `get_beacon_effect_receivers()` → `array[LuaEntity]` | Returns a table with all entities affected by this beacon |
| `get_beacons()` → `array[LuaEntity]` | Returns a table with all beacons affecting this effect receiver. Can only be used when the entity has an effect receiver AssemblingMachine,  |
| `get_beam_source()` → `BeamTarget` | Get the source of this beam. |
| `get_beam_target()` → `BeamTarget` | Get the target of this beam. |
| `get_burnt_result_inventory()` → `LuaInventory` | The burnt result inventory for this entity or `nil` if this entity doesn't have a burnt result inventory. |
| `get_cargo_bays()` → `array[LuaEntity]` | Gets the cargo bays connected to this cargo landing pad or space platform hub. |
| `get_child_signals()` → `array[LuaEntity]` | Returns all child signals. Child signals can be either RailSignal or RailChainSignal. Child signals are signals which are checked by this si |
| `get_circuit_network(wire_connector_id: defines.wire_connector_id)` → `LuaCircuitNetwork` |  |
| `get_connected_rail(rail_connection_direction: defines.rail_connection_direction, rail_direction: defines.rail_direction)` → `LuaEntity, defines.rail_direction, defines.rail_connection_direction` |  |
| `get_connected_rails()` → `array[LuaEntity]` | Get the rails that this signal is connected to. |
| `get_connected_rolling_stock(direction: defines.rail_direction)` → `LuaEntity, defines.rail_direction` | Gets rolling stock connected to the given end of this stock. |
| `get_control_behavior()` → `LuaControlBehavior` | Gets the control behavior of the entity if any. |
| `get_damage_to_be_taken()` → `float` | Returns the amount of damage to be taken by this entity. |
| `get_driver()` → `LuaEntity | LuaPlayer` | Gets the driver of this vehicle if any. |
| `get_electric_input_flow_limit(quality?: QualityID)` → `double` | The input flow limit for the electric energy source. `nil` if the entity doesn't have an electric energy source. |
| `get_electric_output_flow_limit(quality?: QualityID)` → `double` | The output flow limit for the electric energy source. `nil` if the entity doesn't have an electric energy source. |
| `get_filter(slot_index: uint32)` → `ItemFilter | EntityID | AsteroidChunkID` | Get the filter for a slot in an inserter, loader, mining drill, asteroid collector, or logistic storage container. The entity must allow fil |
| `get_fluid(index: uint32)` → `Fluid` | Gets fluid of the index-th fluid storage. This includes fluidbox and non-fluidbox fluid storages like fluid wagon contents. Refer to LuaEnti |
| `get_fluid_contents()` → `dict[string, FluidAmount]` | Get amounts of all fluids in this entity. |
| `get_fluid_count(fluid?: string)` → `double` | Get the amount of all or some fluid in this entity. |
| `get_fluid_source_fluid()` → `string` | Checks what is expected fluid to be produced from the offshore pump's source tile. It accounts for visible tile, hidden tile and double hidd |
| `get_fluid_source_tile()` → `TilePosition` | Gives TilePosition of a tile which this offshore pump uses to check what fluid should be produced. |
| `get_fuel_inventory()` → `LuaInventory` | The fuel inventory for this entity or `nil` if this entity doesn't have a fuel inventory. |
| `get_health_ratio()` → `float` | The health ratio of this entity between 1 and 0 for full health and no health respectively. |
| `get_heat_setting()` → `HeatSetting` | Gets the heat setting for this heat interface. |
| `get_inbound_signals()` → `array[LuaEntity]` | Returns all signals guarding entrance to a rail block this rail belongs to. |
| `get_infinity_container_filter(index: uint32)` → `InfinityInventoryFilter` | Gets the filter for this infinity container at the given index, or `nil` if the filter index doesn't exist or is empty. |
| `get_infinity_pipe_filter()` → `InfinityPipeFilter` | Gets the filter for this infinity pipe, or `nil` if the filter is empty. |
| `get_inventory_bar(inventory_index: defines.inventory)` → `uint32` | The same as LuaInventory::get_barLuaInventory::get_bar but also works for ghosts where the inventory is not available through LuaControl::ge |
| `get_inventory_filter(index: uint32, inventory_index: defines.inventory)` → `ItemFilter` | The same as LuaInventory::get_filterLuaInventory::get_filter but also works for ghosts where the inventory is not available through LuaContr |
| `get_inventory_size_override(inventory_index: defines.inventory)` → `uint16` | Gets the inventory size override of the selected inventory if size override was set using set_inventory_size_overrideLuaEntity::set_inventor |
| `get_item_insert_specification(position: MapPosition)` → `uint32, float` | Get an item insert specification onto a belt connectable: for a given map position provides into which line at what position item should be  |
| `get_line_item_position(index: defines.transport_line, position: float)` → `MapPosition` | Get a map position related to a position on a transport line. |
| `get_logistic_point(index?: defines.logistic_member_index)` → `LuaLogisticPoint | array[LuaLogisticPoint]` | Gets all the `LuaLogisticPoint`s that this entity owns. Optionally returns only the point specified by the index parameter. |
| `get_logistic_sections()` → `LuaLogisticSections` | Gives logistic sections of this entity if it uses logistic sections. |
| `get_market_items()` → `array[Offer]` | Get all offers in a market as an array. |
| `get_max_transport_line_index()` → `defines.transport_line` | Get the maximum transport line index of a belt or belt connectable entity. |
| `get_module_inventory()` → `LuaInventory` | Inventory for storing modules of this entity; `nil` if this entity has no module inventory. |
| `get_movement()` → `Vector` | Gets the combined movement vector direction and speed of this combat robot or asteroid. The entity moves by this vector each tick. |
| `get_or_create_control_behavior()` → `LuaControlBehavior` | Gets and or creates if needed the control behavior of the entity. |
| `get_outbound_signals()` → `array[LuaEntity]` | Returns all signals guarding exit from a rail block this rail belongs to. |
| `get_output_inventory()` → `LuaInventory` | Gets the entity's output inventory if it has one. |
| `get_parent_signals()` → `array[LuaEntity]` | Returns all parent signals. Parent signals are always RailChainSignal. Parent signals are those signals that are checking state of this sign |
| `get_passenger()` → `LuaEntity | LuaPlayer` | Gets the passenger of this car, spidertron, or cargo pod if any. |
| `get_priority_target(index: uint32)` → `LuaEntityPrototype` | Get the entity ID at the specified position in the turret's priority list. |
| `get_radius()` → `double` | The radius of this entity. The radius is defined as half the distance between the top left corner and bottom right corner of the collision b |
| `get_rail_end(direction: defines.rail_direction)` → `LuaRailEnd` | Gets a LuaRailEnd object for specified end of this rail |
| `get_rail_segment_end(direction: defines.rail_direction)` → `LuaEntity, defines.rail_direction` | Get the rail at the end of the rail segment this rail is in. |
| `get_rail_segment_length()` → `double` | Get the length of the rail segment this rail is in. |
| `get_rail_segment_overlaps()` → `array[LuaEntity]` | Get a rail from each rail segment that overlaps with this rail's rail segment. |
| `get_rail_segment_rails(direction: defines.rail_direction)` → `array[LuaEntity]` | Get all rails of a rail segment this rail is in |
| `get_rail_segment_signal(direction: defines.rail_direction, in_else_out: boolean)` → `LuaEntity` | Get the rail signal at the start/end of the rail segment this rail is in. |
| `get_rail_segment_stop(direction: defines.rail_direction)` → `LuaEntity` | Get train stop at the start/end of the rail segment this rail is in. |
| `get_recipe()` → `LuaRecipe, LuaQualityPrototype` | Current recipe being assembled by this machine, if any. |
| `get_signal(extra_wire_connector_id?: defines.wire_connector_id, signal: SignalID, wire_connector_id: defines.wire_connector_id)` → `int32` | Read a single signal from the selected wire connector |
| `get_signals(extra_wire_connector_id?: defines.wire_connector_id, wire_connector_id: defines.wire_connector_id)` → `array[Signal]` | Read all signals from the selected wire connector. |
| `get_spider_legs()` → `array[LuaEntity]` | Gets legs of given SpiderVehicle. |
| `get_stopped_train()` → `LuaTrain` | The train currently stopped at this train stop, if any. |
| `get_train_stop_trains()` → `array[LuaTrain]` | The trains scheduled to stop at this train stop. |
| `get_transport_line(index: defines.transport_line)` → `LuaTransportLine` | Get a transport line of a belt or belt connectable entity. |
| `get_upgrade_target()` → `LuaEntityPrototype, LuaQualityPrototype` | Returns the new entity prototype and its quality. |
| `get_wire_connector(or_create: boolean, wire_connector_id: defines.wire_connector_id)` → `LuaWireConnector` | Gets a single wire connector of this entity, if any. |
| `get_wire_connectors(or_create: boolean)` → `dict[defines.wire_connector_id, LuaWireConnector]` | Gets all wire connectors of this entity |
| `ghost_has_flag(flag: EntityPrototypeFlag)` → `boolean` | Same as LuaEntity::has_flagLuaEntity::has_flag, but targets the inner entity on a entity ghost. |
| `has_flag(flag: EntityPrototypeFlag)` → `boolean` | Test whether this entity's prototype has a certain flag set. |
| `insert_fluid(fluid: Fluid)` → `double` | Insert fluid into this entity. Fluidbox is chosen automatically. |
| `inventory_supports_bar(inventory_index: defines.inventory)` → `boolean` | The same as LuaInventory::supports_barLuaInventory::supports_bar but also works for ghosts where the inventory is not available through LuaC |
| `inventory_supports_filters(inventory_index: defines.inventory)` → `boolean` | The same as LuaInventory::supports_filtersLuaInventory::supports_filters but also works for ghosts where the inventory is not available thro |
| `is_closed()` → `boolean` |  |
| `is_closing()` → `boolean` |  |
| `is_connected_to_electric_network()` → `boolean` | Returns `true` if this entity produces or consumes electricity and is connected to an electric network that has at least one entity that can |
| `is_crafting()` → `boolean` | Returns whether a craft is currently in process. It does not indicate whether progress is currently being made, but whether a crafting proce |
| `is_inventory_filtered(inventory_index: defines.inventory)` → `boolean` | The same as LuaInventory::is_filteredLuaInventory::is_filtered but also works for ghosts where the inventory is not available through LuaCon |
| `is_opened()` → `boolean` |  |
| `is_opening()` → `boolean` |  |
| `is_rail_in_same_rail_block_as(other_rail: LuaEntity)` → `boolean` | Checks if this rail and other rail both belong to the same rail block. |
| `is_rail_in_same_rail_segment_as(other_rail: LuaEntity)` → `boolean` | Checks if this rail and other rail both belong to the same rail segment. |
| `is_registered_for_construction()` → `boolean` | Is this entity or tile ghost or item request proxy registered for construction? If false, it means a construction robot has been dispatched  |
| `is_registered_for_deconstruction(force: ForceID)` → `boolean` | Is this entity registered for deconstruction with this force? If false, it means a construction robot has been dispatched to deconstruct it, |
| `is_registered_for_repair()` → `boolean` | Is this entity registered for repair? If false, it means a construction robot has been dispatched to repair it, or it is not damaged. This i |
| `is_registered_for_upgrade()` → `boolean` | Is this entity registered for upgrade? If false, it means a construction robot has been dispatched to upgrade it, or it is not marked for up |
| `launch_rocket(character?: LuaEntity, destination?: CargoDestination)` → `boolean` |  |
| `mine(force?: boolean, ignore_minable?: boolean, inventory?: LuaInventory, raise_destroyed?: boolean)` → `boolean` | Mines this entity. |
| `order_deconstruction(force: ForceID, player?: PlayerIdentification, undo_index?: uint32)` → `boolean` | Sets the entity to be deconstructed by construction robots. |
| `order_upgrade(force: ForceID, player?: PlayerIdentification, target: EntityWithQualityID, undo_index?: uint32)` → `boolean` | Sets the entity to be upgraded by construction robots. |
| `play_note(instrument: uint32, note: uint32, stop_playing_sounds?: boolean)` → `boolean` | Plays a note with the given instrument and note. |
| `register_tree(tree: LuaEntity)` → `boolean` | Registers the given tree in this agricultural tower. |
| `release_from_spawner()` → `void` | Release the unit from the spawner which spawned it. This allows the spawner to continue spawning additional units. |
| `remove_fluid(amount: double, maximum_temperature?: double, minimum_temperature?: double, name: string, temperature?: double)` → `double` | Remove fluid from this entity. |
| `remove_market_item(offer: uint32)` → `boolean` | Remove an offer from a market. |
| `request_to_close(force: ForceID)` → `void` |  |
| `request_to_open(extra_time?: uint32, force: ForceID)` → `void` |  |
| `revive(overflow?: LuaInventory, raise_revive?: boolean)` → `dict[string, uint32], LuaEntity, LuaEntity` | Revive a ghost, which turns it from a ghost into a real entity or tile. |
| `rotate(by_player?: PlayerIdentification, reverse?: boolean)` → `boolean` | Rotates this entity as if the player rotated it. |
| `set_beam_source(source: LuaEntity | MapPosition)` → `void` | Set the source of this beam. |
| `set_beam_target(target: LuaEntity | MapPosition)` → `void` | Set the target of this beam. |
| `set_driver(driver: LuaEntity | PlayerIdentification | nil)` → `void` | Sets the driver of this vehicle. |
| `set_filter(filter?: ItemFilter | ItemWithQualityID | EntityID | AsteroidChunkID, index: uint32)` → `void` | Set the filter for a slot in an inserter ItemFilter, loader ItemFilter, mining drill EntityID, asteroid collector AsteroidChunkID or logisti |
| `set_fluid(fluid?: Fluid, index: uint32)` → `Fluid` | Sets fluid to the index-th fluid storage. This includes fluidbox and non-fluidbox fluid storages like fluid wagon contents. Refer to LuaEnti |
| `set_heat_setting(filter: HeatSetting)` → `void` | Sets the heat setting for this heat interface. |
| `set_infinity_container_filter(filter: InfinityInventoryFilter | nil, index: uint32)` → `void` | Sets the filter for this infinity container at the given index. |
| `set_infinity_pipe_filter(filter: InfinityPipeFilter | nil)` → `void` | Sets the filter for this infinity pipe. |
| `set_inventory_bar(bar?: uint32, inventory_index: defines.inventory)` → `void` | The same as LuaInventory::set_barLuaInventory::set_bar but also works for ghosts where the inventory is not available through LuaControl::ge |
| `set_inventory_filter(filter: ItemFilter | nil, index: uint32, inventory_index: defines.inventory)` → `boolean` | The same as LuaInventory::set_filterLuaInventory::set_filter but also works for ghosts where the inventory is not available through LuaContr |
| `set_inventory_size_override(inventory_index: defines.inventory, overflow?: LuaInventory, size_override: uint16 | nil)` → `void` | Sets inventory size override. When set, supported entity will ignore inventory size from prototype and will instead keep inventory size equa |
| `set_movement(direction: Vector, speed: double)` → `void` | Sets the movement direction and movement speed for this combat robot or asteroid. |
| `set_passenger(passenger: LuaEntity | PlayerIdentification | nil)` → `void` | Sets the passenger of this car, spidertron, or cargo pod. |
| `set_priority_target(entity_id?: EntityID, index: uint32)` → `void` | Set the entity ID name at the specified position in the turret's priority list. |
| `set_recipe(quality?: QualityID, recipe?: RecipeID)` → `ItemWithQualityCounts` | Sets the given recipe in this assembly machine. |
| `silent_revive(overflow?: LuaInventory, raise_revive?: boolean)` → `ItemWithQualityCounts, LuaEntity, LuaEntity` | Revives a ghost silently, so the revival makes no sound and no smoke is created. |
| `spawn_decorations()` → `void` | Triggers spawn_decoration actions defined in the entity prototype or does nothing if entity is not "turret" or "unit-spawner". |
| `start_fading_out()` → `void` | Only works if the entity is a speech-bubble, with an "effect" defined in its wrapper_flow_style. Starts animating the opacity of the speech  |
| `stop_spider()` → `void` | Sets the speedLuaEntity::speed of the given SpiderVehicle to zero. Notably does not clear its autopilot_destinationLuaEntity::autopilot_dest |
| `supports_backer_name()` → `boolean` | Whether this entity supports a backer name. |
| `to_be_deconstructed()` → `boolean` | Is this entity marked for deconstruction? |
| `to_be_upgraded()` → `boolean` | Is this entity marked for upgrade? |
| `toggle_equipment_movement_bonus()` → `void` | Toggle this entity's equipment movement bonus. Does nothing if the entity does not have an equipment grid. |
| `update_connections()` → `void` | Reconnect loader, beacon, cliff and mining drill connections to entities that might have been teleported out or in by the script. The game d |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `absorbed_pollution` | ? | `?` |  |
| `active` | ? | `?` | Deactivating an entity will stop all its operations car will stop moving, inserters will stop working, fish will stop moving etc. |
| `ai_settings` | ? | `?` | The ai settings of this unit. |
| `alert_parameters` | ? | `?` |  |
| `allow_dispatching_robots` | ? | `?` | Whether this character's personal roboports are allowed to dispatch robots. |
| `always_on` | ? | `?` | If the lamp is always on when not driven by control behavior. |
| `amount` | ? | `?` | Count of resource units contained. |
| `armed` | ? | `?` | Whether this land mine is armed. |
| `artillery_auto_targeting` | ? | `?` | If this artillery auto-targets enemies. |
| `associated_player` | ? | `?` | The player this character is associated with, if any. Set to `nil` to clear. |
| `attached_cargo_pod` | ? | `?` | The cargo pod attached to this rocket silo rocket if any. |
| `autopilot_destination` | ? | `?` | Destination of this spidertron's autopilot, if any. Writing `nil` clears all destinations. |
| `autopilot_destinations` | ? | `?` | The queued destination positions of spidertron's autopilot. |
| `backer_name` | ? | `?` | The backer name assigned to this entity. Entities that support backer names are labs, locomotives, radars, roboports, and train st |
| `base_damage_modifiers` | ? | `?` |  |
| `beacons_count` | ? | `?` | Number of beacons affecting this effect receiver. Can only be used when the entity has an effect receiver AssemblingMachine, Furna |
| `belt_neighbours` | ? | `?` | The belt connectable neighbours of this belt connectable entity. Only entities that input to or are outputs of this entity. Does n |
| `belt_shape` | ? | `?` | Gives what is the current shape of a transport-belt. |
| `belt_to_ground_type` | ? | `?` | Whether this underground belt goes into or out of the ground. |
| `bonus_damage_modifiers` | ? | `?` |  |
| `bonus_mining_progress` | ? | `?` | The bonus mining progress for this mining drill. Read yields a number in range 0, mining_target.prototype.mineable_properties.mini |
| `bonus_progress` | ? | `?` | The current productivity bonus progress, as a number in range `0, 1`. |
| `bounding_box` | ? | `?` | LuaEntityPrototype::collision_boxLuaEntityPrototype::collision_box around entity's given position and respecting the current entit |
| `burner` | ? | `?` | The burner energy source for this entity, if any. |
| `cargo_bay_connection_owner` | ? | `?` | The space platform hub or cargo landing pad this cargo bay is connected to if any. |
| `cargo_hatches` | ? | `?` | The cargo hatches owned by this entity if any. |
| `cargo_pod_destination` | ? | `?` | The destination of this cargo pod entity. |
| `cargo_pod_origin` | ? | `?` | The origin of this cargo pod entity. Must be a silo, hub or pad |
| `cargo_pod_state` | ? | `?` | The state of this cargo pod entity. |
| `chain_signal_state` | ? | `?` | The state of this chain signal. |
| `character_corpse_death_cause` | ? | `?` | The reason this character corpse character died. `""` if there is no reason. |
| `character_corpse_player_index` | ? | `?` | The player index associated with this character corpse. |
| `character_corpse_tick_of_death` | ? | `?` | The tick this character corpse died at. |
| `cliff_orientation` | ? | `?` | The orientation of this cliff. |
| `color` | ? | `?` | The color of this character, rolling stock, corpse, character corpse, train stop, simple-entity-with-owner, car, spider-vehicle, o |
| `combat_robot_owner` | ? | `?` | The owner of this combat robot, if any. |
| `combinator_description` | ? | `?` | The description on this combinator. |
| `commandable` | ? | `?` | Returns a LuaCommandable for this entity or nil if entity is not commandable. Units and SpiderUnits are commandable. |
| `connected_rail` | ? | `?` | The rail entity this train stop is connected to, if any. |
| `connected_rail_direction` | ? | `?` | Rail direction to which this train stop is binding. This returns a value even when no rails are present. |
| `consumption_bonus` | ? | `?` | The consumption bonus of this entity. |
| `consumption_modifier` | ? | `?` | Multiplies the energy consumption. |
| `copy_color_from_train_stop` | ? | `?` | If this rolling stock has 'copy color from train stop' enabled. |
| `corpse_expires` | ? | `?` | Whether this corpse will ever fade away. |
| `corpse_immune_to_entity_placement` | ? | `?` | If true, corpse won't be destroyed when entities are placed over it. If false, whether corpse will be removed or not depends on va |
| `crafting_progress` | ? | `?` | The current crafting progress, as a number in range `0, 1`. |
| `crafting_speed` | ? | `?` | The current crafting speed, including speed bonuses from modules and beacons. |
| `crane_destination` | ? | `?` | Destination of the crane of this entity. Throws when trying to set the destination out of range. |
| `crane_destination_3d` | ? | `?` | Destination of the crane of this entity in 3D. Throws when trying to set the destination out of range. |
| `crane_end_position_3d` | ? | `?` | Returns current position in 3D for the end of the crane of this entity. |
| `crane_grappler_destination` | ? | `?` | Will set destination for the grappler of crane of this entity. The crane grappler will start moving to reach the destination, but  |
| `crane_grappler_destination_3d` | ? | `?` | Will set destination in 3D for the grappler of crane of this entity. The crane grappler will start moving to reach the destination |
| `created_by_corpse` | ? | `?` | The corpse that caused this entity ghost to be created, if any. |
| `custom_status` | ? | `?` | A custom status for this entity that will be displayed in the GUI. |
| `damage_dealt` | ? | `?` | The damage dealt by this turret, artillery turret, or artillery wagon. |
| `destructible` | ? | `?` | If set to `false`, this entity can't be damaged and won't be attacked automatically. It can however still be mined. |
| `direction` | ? | `?` | The current direction this entity is facing. |
| `disabled_by_control_behavior` | ? | `?` | If the updatable entity is disabled by control behavior. |
| `disabled_by_recipe` | ? | `?` | If the assembling machine is disabled by recipe, e.g. due to AssemblingMachinePrototype::disabled_when_recipe_not_researchedAssemb |
| `disabled_by_script` | ? | `?` | If the updatable entity is disabled by script. |
| `display_panel_always_show` | ? | `?` |  |
| `display_panel_icon` | ? | `?` | Icon visible on the display panel. Can be written only when it is not set by control behavior. |
| `display_panel_show_in_chart` | ? | `?` |  |
| `display_panel_text` | ? | `?` | Text visible on the display panel. Can be written only when it is not set by control behavior. |
| `draw_data` | ? | `?` | Gives a draw data of the given entity if it supports such data. |
| `driver_is_gunner` | ? | `?` | Whether the driver of this car or spidertron is the gunner. If `false`, the passenger is the gunner. `nil` if this is neither a ca |
| `drop_position` | ? | `?` | Position where the entity puts its stuff. |
| `drop_target` | ? | `?` | The entity this entity is putting its items to. If there are multiple possible entities at the drop-off point, writing to this att |
| `effective_speed` | ? | `?` | The current speed of this unit in tiles per tick, taking into account any walking speed modifier given by the tile the unit is sta |
| `effectivity_modifier` | ? | `?` | Multiplies the acceleration the car can create for one unit of energy. Defaults to `1`. |
| `effects` | ? | `?` | The effects being applied to this entity, if any. For beacons, this is the effect the beacon is broadcasting. |
| `electric_buffer_size` | ? | `?` | The buffer size for the electric energy source. `nil` if the entity doesn't have an electric energy source. |
| `electric_drain` | ? | `?` | The electric drain for the electric energy source. `nil` if the entity doesn't have an electric energy source. |
| `electric_emissions_per_joule` | ? | `?` | The table of emissions of this energy source in `pollution/Joule`, indexed by pollutant type. `nil` if the entity doesn't have an  |
| `electric_network_id` | ? | `?` | Returns the id of the electric network that this entity is connected to, if any. |
| `electric_network_statistics` | ? | `?` | The electric network statistics for this electric pole. |
| `enable_logistics_while_moving` | ? | `?` | Whether equipment grid logistics are enabled while this vehicle is moving. |
| `energy` | ? | `?` | Energy stored in the entity's energy buffer energy stored in electrical devices etc.. Always 0 for entities that don't have the co |
| `energy_generated_last_tick` | ? | `?` | How much energy this generator generated in the last tick. |
| `entity_label` | ? | `?` | The label on this spider-vehicle entity, if any. `nil` if this is not a spider-vehicle. |
| `filter_slot_count` | ? | `?` | The number of filter slots this inserter, loader, mining drill, asteroid collector or logistic storage container has. 0 if not one |
| `fluidbox` | ? | `?` | Fluidboxes of this entity. |
| `fluids_count` | ? | `?` | Returns count of fluid storages. This includes fluid storages provided by fluidboxes but also covers other fluid storages like flu |
| `follow_offset` | ? | `?` | The follow offset of this spidertron, if any entity is being followed. This is randomized each time the follow entity is set. |
| `follow_target` | ? | `?` | The follow target of this spidertron, if any. |
| `friction_modifier` | ? | `?` | Multiplies the car friction rate. |
| `frozen` | ? | `?` | Whether the freezable entity is currently frozen. |
| `ghost_localised_description` | ? | `?` |  |
| `ghost_localised_name` | ? | `?` | Localised name of the entity or tile contained in this ghost. |
| `ghost_name` | ? | `?` | Name of the entity or tile contained in this ghost. |
| `ghost_prototype` | ? | `?` | The prototype of the entity or tile contained in this ghost. |
| `ghost_type` | ? | `?` | The prototype type of the entity or tile contained in this ghost. |
| `ghost_unit_number` | ? | `?` | The unit_numberLuaEntity::unit_number of the entity contained in this ghost. It is the same as the unit number of the EntityWithOw |
| `gps_tag` | ? | `?` | Returns a rich texthttps://wiki.factorio.com/Rich_text string containing this entity's position and surface name as a gps tag. Pri |
| `graphics_variation` | ? | `?` | The graphics variation for this entity. `nil` if this entity doesn't use graphics variations. |
| `grid` | ? | `?` | This entity's equipment grid, if any. |
| `health` | ? | `?` | The current health of the entity, if any. Health is automatically clamped to be between `0` and max health inclusive. Entities wit |
| `heat_neighbours` | ? | `?` | The entities connected to this entities heat buffer. |
| `held_stack` | ? | `?` | The item stack currently held in an inserter's hand. |
| `held_stack_position` | ? | `?` | Current position of the inserter's "hand". |
| `highlight_box_blink_interval` | ? | `?` | The blink interval of this highlight box entity. `0` indicates no blink. |
| `highlight_box_type` | ? | `?` | The highlight box type of this highlight box entity. |
| `ignore_unprioritised_targets` | ? | `?` | Whether this turret shoots at targets that are not on its priority list. |
| `infinity_container_filters` | ? | `?` | The filters for this infinity container. |
| `initial_amount` | ? | `?` | Count of initial resource units contained. `nil` if this is not an infinite resource. |
| `insert_plan` | ? | `?` | The insert plan for this ghost or item request proxy. |
| `inserter_filter_mode` | ? | `?` | The filter mode for this filter inserter. `nil` if this inserter doesn't use filters. |
| `inserter_spoil_priority` | ? | `?` | The spoil priority for this inserter. |
| `inserter_stack_size_override` | ? | `?` | Sets the stack size limit on this inserter. |
| `inserter_target_pickup_count` | ? | `?` | Returns the current target pickup count of the inserter. |
| `is_entity_with_health` | ? | `?` | If this entity is EntityWithHealth |
| `is_entity_with_owner` | ? | `?` | If this entity is EntityWithOwner |
| `is_freezable` | ? | `?` | Whether the entity is freezable and considered a FreezableEntity. |
| `is_headed_to_trains_front` | ? | `?` | If the rolling stock is facing train's front. |
| `is_military_target` | ? | `?` | Whether this entity is a MilitaryTarget. Can be written to if LuaEntityPrototype::allow_run_time_change_of_is_military_targetLuaEn |
| `is_updatable` | ? | `?` | Whether the entity is updatable and considered an UpdatableEntity. |
| `item_request_proxy` | ? | `?` | The first found item request proxy targeting this entity. |
| `item_requests` | ? | `?` | Items this ghost will request when revived or items this item request proxy is requesting. |
| `kills` | ? | `?` | The number of units killed by this turret, artillery turret, or artillery wagon. |
| `last_user` | ? | `?` | The last player that changed any setting on this entity. This includes building the entity, changing its color, or configuring its |
| `link_id` | ? | `?` | The link ID this linked container is using. |
| `linked_belt_neighbour` | ? | `?` | Neighbour to which this linked belt is connected to, if any. |
| `linked_belt_type` | ? | `?` | Type of linked belt. Changing type will also flip direction so the belt is out of the same side. |
| `loader_belt_stack_size_override` | ? | `?` | The belt stack size override for this loader. Set to `0` to disable. Writing this value requires LoaderPrototype::adjustable_belt_ |
| `loader_container` | ? | `?` | The container entity this loader is pointing at/pulling from depending on the LuaEntity::loader_typeLuaEntity::loader_type, if any |
| `loader_filter_mode` | ? | `?` | The filter mode for this loader. `nil` if this loader does not support filters. |
| `loader_type` | ? | `?` | Whether this loader gets items from or puts item into a container. |
| `localised_description` | ? | `?` |  |
| `localised_name` | ? | `?` | Localised name of the entity. |
| `logistic_cell` | ? | `?` | The logistic cell this entity is a part of. Will be `nil` if this entity is not a part of any logistic cell. |
| `logistic_network` | ? | `?` | The logistic network this entity is a part of, or `nil` if this entity is not a part of any logistic network. |
| `max_health` | ? | `?` | Max health of this entity. |
| `minable` | ? | `?` | Not minable entities can still be destroyed. |
| `minable_flag` | ? | `?` | Script controlled flag that allows entity to be mined. |
| `mining_area` | ? | `?` | Area in which this mining drill looks for resources to mine. |
| `mining_drill_filter_mode` | ? | `?` | The filter mode for this mining drill. `nil` if this mining drill doesn't have filters. |
| `mining_progress` | ? | `?` | The mining progress for this mining drill. Is a number in range 0, mining_target.prototype.mineable_properties.mining_time. `nil`  |
| `mining_target` | ? | `?` | The mining target, if any. |
| `mirroring` | ? | `?` | Whether the entity is currently mirrored. This state is referred to as `flipped` elsewhere, such as on the on_player_flipped_entit |
| `name` | ? | `?` | Name of the entity prototype. E.g. "inserter" or "fast-inserter". |
| `name_tag` | ? | `?` | Name tag of this entity. Returns `nil` if entity has no name tag. When name tag is already used by other entity, the name will be  |
| `neighbour_bonus` | ? | `?` | The current total neighbour bonus of this reactor. |
| `neighbours` | ? | `?` | A list of neighbours for certain types of entities. Applies to underground belts, walls, gates, reactors, heat pipes, cliffs, and  |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `operable` | ? | `?` | Player can't open gui of this entity and he can't quick insert/input stuff in to the entity when it is not operable. |
| `orientation` | ? | `?` | The smooth orientation of this entity. For turrets this is the orientation of the weapon. |
| `owned_plants` | ? | `?` | Plants registered by this agricultural tower. One plant can be registered in multiple agricultural towers. |
| `parameters` | ? | `?` |  |
| `pickup_from_left_lane` | ? | `?` | For inserters taking items from transport belt connectables, this determines whether the inserter is allowed to take items from th |
| `pickup_from_right_lane` | ? | `?` | For inserters taking items from transport belt connectables, this determines whether the inserter is allowed to take items from th |
| `pickup_position` | ? | `?` | Where the inserter will pick up items from. |
| `pickup_target` | ? | `?` | The entity this inserter will attempt to pick up items from. If there are multiple possible entities at the pick-up point, writing |
| `player` | ? | `?` | The player connected to this character, if any. |
| `pollution_bonus` | ? | `?` | The pollution bonus of this entity. |
| `power_production` | ? | `?` | The power production specific to the ElectricEnergyInterface entity type. |
| `power_switch_state` | ? | `?` | The state of this power switch. |
| `power_usage` | ? | `?` | The power usage specific to the ElectricEnergyInterface entity type. |
| `previous_recipe` | ? | `?` | The previous recipe this furnace was using, if any. |
| `priority_targets` | ? | `?` | The priority targets for this turret if any. |
| `procession_tick` | ? | `?` | how far into the current procession the cargo pod is. |
| `productivity_bonus` | ? | `?` | The productivity bonus of this entity. |
| `products_finished` | ? | `?` | The number of products this machine finished crafting in its lifetime. |
| `prototype` | ? | `?` | The entity prototype of this entity. |
| `proxy_target` | ? | `?` | The target entity for this item-request-proxy, if any. |
| `proxy_target_entity` | ? | `?` | Entity of which inventory is exposed by this ProxyContainer |
| `proxy_target_inventory` | ? | `?` | Inventory index of the inventory that is exposed by this ProxyContainer |
| `pump_rail_target` | ? | `?` | The rail target of this pump, if any. |
| `pumped_last_tick` | ? | `?` | The amount of fluid moved by this offshore pump or normal pump in the last tick. |
| `quality` | ? | `?` | The quality of this entity. |
| `radar_scan_progress` | ? | `?` | The current radar scan progress, as a number in range `0, 1`. |
| `rail_layer` | ? | `?` | Gets rail layer of a given signal |
| `rail_length` | ? | `?` | Length of this rail piece. |
| `recipe_locked` | ? | `?` | When locked; the recipe in this assembling machine can't be changed by the player. |
| `relative_turret_orientation` | ? | `?` | The relative orientation of the vehicle turret, artillery turret, artillery wagon. `nil` if this entity isn't a vehicle with a veh |
| `removal_plan` | ? | `?` | The removal plan for this item request proxy. |
| `remove_unfiltered_items` | ? | `?` | Whether items not included in this infinity container filters should be removed from the container. |
| `render_player` | ? | `?` | The player that this `simple-entity-with-owner`, `simple-entity-with-force`, or `highlight-box` is visible to. `nil` when this ent |
| `render_to_forces` | ? | `?` | The forces that this `simple-entity-with-owner` or `simple-entity-with-force` is visible to. `nil` or an empty array when this ent |
| `request_from_buffers` | ? | `?` | Whether this requester chest is set to also request from buffer chests. |
| `result_quality` | ? | `?` | The quality produced when this crafting machine finishes crafting. `nil` when crafting is not in progress. |
| `robot_order_queue` | ? | `?` | Get the current queue of robot orders. |
| `rocket` | ? | `?` | The rocket silo rocket this cargo pod is attached to, or rocket silo rocket attached to this rocket silo - if any. |
| `rocket_parts` | ? | `?` | Number of rocket parts in this rocket silo. |
| `rocket_silo_status` | ? | `?` | The status of this rocket silo entity. |
| `rotatable` | ? | `?` | When entity is not to be rotatable inserter, transport belt etc, it can't be rotated by player using the R key. |
| `secondary_bounding_box` | ? | `?` | The secondary bounding box of this entity or `nil` if it doesn't have one. This only exists for curved rails, and is automatically |
| `secondary_selection_box` | ? | `?` | The secondary selection box of this entity or `nil` if it doesn't have one. This only exists for curved rails, and is automaticall |
| `segmented_unit` | ? | `?` | The segmented unit object that the segment entity is a part of. |
| `selected_gun_index` | ? | `?` | Index of the currently selected weapon slot of this character, car, or spidertron. `nil` if this entity doesn't have guns. |
| `selection_box` | ? | `?` | LuaEntityPrototype::selection_boxLuaEntityPrototype::selection_box around entity's given position and respecting the current entit |
| `send_to_orbit_automatically` | ? | `?` | Whether this rocket silo is set to send items to orbit automatically. Only relevant if there is an item prototype with launch prod |
| `shooting_target` | ? | `?` | The shooting target for this turret, if any. Can't be set to `nil` via script. |
| `signal_state` | ? | `?` | The state of this rail signal. |
| `spawn_shift` | ? | `?` |  |
| `spawning_cooldown` | ? | `?` |  |
| `speed` | ? | `?` | The current speed if this is a car, rolling stock, projectile or spidertron, or the maximum speed if this is a unit. The speed is  |
| `speed_bonus` | ? | `?` | The speed bonus of this entity. |
| `splitter_filter` | ? | `?` | The filter for this splitter, if any is set. |
| `splitter_input_priority` | ? | `?` | The input priority for this splitter. |
| `splitter_output_priority` | ? | `?` | The output priority for this splitter. |
| `stack` | ? | `?` |  |
| `status` | ? | `?` | The status of this entity, if any. |
| `sticked_to` | ? | `?` | The entity this sticker is sticked to. |
| `sticker_vehicle_modifiers` | ? | `?` | The vehicle modifiers applied to this entity through the attached stickers. |
| `stickers` | ? | `?` | The sticker entities attached to this entity, if any. |
| `storage_filter` | ? | `?` | The storage filter for this logistic storage container. |
| `supports_direction` | ? | `?` | Whether the entity has direction. When it is false for this entity, it will always return north direction when asked for. |
| `tags` | ? | `?` | The tags associated with this entity ghost. `nil` if this is not an entity ghost or when the ghost has no tags. |
| `temperature` | ? | `?` | The temperature of this entity's heat energy source. `nil` if this entity does not use a heat energy source. |
| `tick_grown` | ? | `?` | The tick when this plant is fully grown. |
| `tick_of_last_attack` | ? | `?` | The last tick this character entity was attacked. |
| `tick_of_last_damage` | ? | `?` | The last tick this character entity was damaged. |
| `tile_height` | ? | `?` | Specifies the tiling size of the entity, is used to decide, if the center should be in the center of the tile odd tile size dimens |
| `tile_width` | ? | `?` | Specifies the tiling size of the entity, is used to decide, if the center should be in the center of the tile odd tile size dimens |
| `time_to_live` | ? | `?` | The ticks left before a combat robot, highlight box, smoke, or sticker entity is destroyed. |
| `time_to_next_effect` | ? | `?` | The ticks until the next trigger effect of this smoke-with-trigger. |
| `timeout` | ? | `?` | The timeout that's left on this landmine in ticks. It describes the time between the landmine being placed and it being armed. |
| `to_be_looted` | ? | `?` | Will this item entity be picked up automatically when the player walks over it? |
| `torso_orientation` | ? | `?` | The torso orientation of this spider vehicle. |
| `train` | ? | `?` | The train this rolling stock belongs to, if any. `nil` if this is not a rolling stock. |
| `train_stop_priority` | ? | `?` | Priority of this train stop. |
| `trains_count` | ? | `?` | Amount of trains related to this particular train stop. Includes train stopped at this train stop until it finds a path to next ta |
| `trains_in_block` | ? | `?` | The number of trains in this rail block for this rail entity. |
| `trains_limit` | ? | `?` | Amount of trains above which no new trains will be sent to this train stop. Writing nil will disable the limit will set a maximum  |
| `transitional_request_target` | ? | `?` | The space platform in orbit this rocket silo is automatically requesting items for. |
| `tree_color_index` | ? | `?` | Index of the tree color. |
| `tree_color_index_max` | ? | `?` | Maximum index of the tree colors. |
| `tree_gray_stage_index` | ? | `?` | Index of the tree gray stage |
| `tree_gray_stage_index_max` | ? | `?` | Maximum index of the tree gray stages. |
| `tree_stage_index` | ? | `?` | Index of the tree stage. |
| `tree_stage_index_max` | ? | `?` | Maximum index of the tree stages. |
| `type` | ? | `?` | The entity prototype type of this entity. |
| `unit_number` | ? | `?` | A unique number identifying this entity for the lifetime of the save. These are allocated sequentially, and not re-used until over |
| `units` | ? | `?` | The units associated with this spawner entity. |
| `use_filters` | ? | `?` | If set to 'true', this inserter will use filtering logic. |
| `use_transitional_requests` | ? | `?` | When true, the rocket silo will automatically request items for space platforms in orbit. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
| `valve_threshold_override` | ? | `?` | The threshold override of this valve, or `nil` if an override is not defined. |
| `vehicle_automatic_targeting_parameters` | ? | `?` | Read when this spidertron auto-targets enemies |
---

### LuaInventory

A storage of item stacks.

#### Methods

| Signature | Description |
|-----------|-------------|
| `can_insert(items: ItemStackIdentification)` → `boolean` | Can at least some items be inserted? |
| `can_set_filter(filter: ItemFilter, index: uint32)` → `boolean` | If the given inventory slot filter can be set to the given filter. |
| `clear()` → `void` | Clear this inventory of all items so that it becomes empty. |
| `count_empty_stacks(include_bar?: boolean, include_filtered?: boolean)` → `uint32` | Counts the number of empty stacks. |
| `destroy()` → `void` | Destroys this inventory. |
| `find_empty_stack(item?: ItemWithQualityID)` → `LuaItemStack, uint32` | Finds the first empty stack. Filtered slots are excluded unless a filter item is given. |
| `find_item_stack(item: ItemWithQualityID)` → `LuaItemStack, uint32` | Finds the first LuaItemStack in the inventory that matches the given item name. |
| `get_bar()` → `uint32` | Get the current bar. This is the index at which the red area starts. |
| `get_contents()` → `ItemWithQualityCounts` | Get counts of all items in this inventory. |
| `get_filter(index: uint32)` → `ItemFilter` | Gets the filter for the given item stack index. |
| `get_insertable_count(item: ItemWithQualityID)` → `uint32` | Gets the number of the given item that can be inserted into this inventory. |
| `get_item_count(item?: ItemWithQualityID)` → `uint32` | Get the number of all or some items in this inventory. |
| `get_item_count_filtered(filter: ItemFilter)` → `uint32` | Get the number of items in this inventory that match provided filter. |
| `get_item_quality_counts(item?: ItemID)` → `dict[string, uint32]` | Get the number of all or some items in this inventory, aggregated by quality. |
| `insert(items: ItemStackIdentification)` → `uint32` | Insert items into this inventory. |
| `is_empty()` → `boolean` | Does this inventory contain nothing? |
| `is_filtered()` → `boolean` | If this inventory supports filters and has at least 1 filter set. |
| `is_full()` → `boolean` | Is every stack in this inventory full? Ignores stacks blocked by the current bar. |
| `remove(items: ItemStackIdentification)` → `uint32` | Remove items from this inventory. |
| `resize(size: uint16)` → `void` | Resizes the inventory. |
| `set_bar(bar?: uint32)` → `void` | Set the current bar. |
| `set_filter(filter: ItemFilter | nil, index: uint32)` → `boolean` | Sets the filter for the given item stack index. |
| `sort_and_merge()` → `void` | Sorts and merges the items in this inventory. |
| `supports_bar()` → `boolean` | Does this inventory support a bar? Bar is the draggable red thing, found for example on chests, that limits the portion of the inventory tha |
| `supports_filters()` → `boolean` | If this inventory supports filters. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `entity_owner` | ? | `?` | The entity that owns this inventory, if any. |
| `equipment_owner` | ? | `?` | The equipment that owns this inventory, if any. |
| `index` | ? | `?` | The inventory index this inventory uses, if any. |
| `max_weight` | ? | `?` | Gives a maximum weight of items that can be inserted into this inventory. |
| `mod_owner` | ? | `?` | The mod that owns this inventory, if any. |
| `name` | ? | `?` | Name of this inventory, if any. Names match keys of defines.inventorydefines.inventory. |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `player_owner` | ? | `?` | The player that owns this inventory, if any. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
| `weight` | ? | `?` | Gives a total weight of all items currently in this inventory. |
---

### LuaForce

`LuaForce` encapsulates data local to each "force" or "faction" of the game.

#### Methods

| Signature | Description |
|-----------|-------------|
| `add_chart_tag(surface: SurfaceIdentification, tag: ChartTagSpec)` → `LuaCustomChartTag` | Adds a custom chart tag to the given surface and returns the new tag or `nil` if the given position isn't valid for a chart tag. |
| `add_research(technology: TechnologyID)` → `boolean` | Add this technology to the back of the research queue if the queue is enabled. Otherwise, set this technology to be researched now. |
| `cancel_charting(surface?: SurfaceIdentification)` → `void` | Cancels pending chart requests for the given surface or all surfaces. |
| `cancel_current_research()` → `void` | Stop the research currently in progress. This will remove any dependent technologies from the research queue. |
| `chart(area: BoundingBox, surface: SurfaceIdentification)` → `void` | Chart a portion of the map. The chart for the given area is refreshed; it creates chart for any parts of the given area that haven't been ch |
| `chart_all(surface?: SurfaceIdentification)` → `void` | Chart all generated chunks. |
| `clear_chart(surface?: SurfaceIdentification)` → `void` | Erases chart data for this force. |
| `copy_chart(destination_surface: SurfaceIdentification, source_force: ForceID, source_surface: SurfaceIdentification)` → `void` | Copies the given surface's chart from the given force to this force. |
| `copy_from(force: ForceID)` → `void` | Copies all of the given changeable values except charts from the given force to this force. |
| `create_logistic_group(name: string, type?: defines.logistic_group_type)` → `void` | Creates the given group if it doesn't already exist. |
| `create_space_platform(name?: string, planet: SpaceLocationID, starter_pack: ItemWithQualityID)` → `LuaSpacePlatform` | Creates a new space platform on this force. |
| `delete_logistic_group(name: string, type?: defines.logistic_group_type)` → `void` | Deletes the given logistic group if it exists. |
| `disable_all_prototypes()` → `void` | Disable all recipes and technologies. Only recipes and technologies enabled explicitly will be useable from this point. |
| `disable_research()` → `void` | Disable research for this force. |
| `enable_all_prototypes()` → `void` | Enables all recipes and technologies. The opposite of LuaForce::disable_all_prototypesLuaForce::disable_all_prototypes. |
| `enable_all_recipes()` → `void` | Unlock all recipes. |
| `enable_all_technologies()` → `void` | Unlock all technologies. |
| `enable_research()` → `void` | Enable research for this force. |
| `find_chart_tags(area?: BoundingBox, surface: SurfaceIdentification)` → `array[LuaCustomChartTag]` | Finds all custom chart tags within a given area on the given surface. If no area is given all custom chart tags on the surface are returned. |
| `find_logistic_network_by_position(position: MapPosition, surface: SurfaceIdentification)` → `LuaLogisticNetwork` |  |
| `get_ammo_damage_modifier(ammo: string)` → `double` |  |
| `get_cease_fire(other: ForceID)` → `boolean` | Is `other` force in this force's cease fire list? |
| `get_chunk_chart(chunk_position: ChunkPosition, surface: SurfaceIdentification)` → `string` | Gets the raw chart data for a given chunk as a binary string. |
| `get_entity_build_count_statistics(surface: SurfaceIdentification)` → `LuaFlowStatistics` | The entity build statistics for this force built and mined for the given surface. |
| `get_entity_count(name: EntityID)` → `uint32` | Count entities of given type. |
| `get_evolution_factor(surface?: SurfaceIdentification)` → `double` | Fetches the evolution factor of this force on the given surface. |
| `get_evolution_factor_by_killing_spawners(surface?: SurfaceIdentification)` → `double` | Fetches the spawner kill part of the evolution factor of this force on the given surface. |
| `get_evolution_factor_by_pollution(surface?: SurfaceIdentification)` → `double` | Fetches the pollution part of the evolution factor of this force on the given surface. |
| `get_evolution_factor_by_time(surface?: SurfaceIdentification)` → `double` | Fetches the time part of the evolution factor of this force on the given surface. |
| `get_fluid_production_statistics(surface: SurfaceIdentification)` → `LuaFlowStatistics` | The fluid production statistics for this force for the given surface. |
| `get_friend(other: ForceID)` → `boolean` | Is `other` force in this force's friends list. |
| `get_gun_speed_modifier(ammo: string)` → `double` |  |
| `get_hand_crafting_disabled_for_recipe(recipe: RecipeID)` → `boolean` | Gets if the given recipe is explicitly disabled from being hand crafted. |
| `get_item_launched(item: ItemID)` → `uint32` | Gets the count of a given item launched in rockets. |
| `get_item_production_statistics(surface: SurfaceIdentification)` → `LuaFlowStatistics` | The item production statistics for this force for the given surface. |
| `get_kill_count_statistics(surface: SurfaceIdentification)` → `LuaFlowStatistics` | The kill counter statistics for this force for the given surface. |
| `get_linked_inventory(link_id: uint32, prototype: EntityID)` → `LuaInventory` | Gets the linked inventory for the given prototype and link ID if it exists or `nil`. |
| `get_logistic_group(name: string, type?: defines.logistic_group_type)` → `LogisticGroup` | Gets the information about the given logistic group. |
| `get_logistic_groups(type?: defines.logistic_group_type)` → `array[string]` | Gets the names of the current logistic groups. |
| `get_spawn_position(surface: SurfaceIdentification)` → `MapPosition` |  |
| `get_surface_hidden(surface: SurfaceIdentification)` → `boolean` |  |
| `get_turret_attack_modifier(turret: EntityID)` → `double` |  |
| `is_chunk_charted(chunk_position: ChunkPosition, surface: SurfaceIdentification)` → `boolean` | Has a chunk been charted? |
| `is_chunk_requested_for_charting(chunk_position: ChunkPosition, surface: SurfaceIdentification)` → `boolean` | Has a chunk been requested for charting? |
| `is_chunk_visible(chunk_position: ChunkPosition, surface: SurfaceIdentification)` → `boolean` | Is the given chunk currently charted and visible not covered by fog of war on the map. |
| `is_enemy(other: ForceID)` → `boolean` | Is this force an enemy? This differs from `get_cease_fire` in that it is always false for neutral force. This is equivalent to checking the  |
| `is_friend(other: ForceID)` → `boolean` | Is this force a friend? This differs from `get_friend` in that it is always true for neutral force. This is equivalent to checking the `frie |
| `is_pathfinder_busy()` → `boolean` | Is pathfinder busy? When the pathfinder is busy, it won't accept any more pathfinding requests. |
| `is_quality_unlocked(quality: QualityID)` → `void` | Is the specified quality unlocked for this force? |
| `is_space_location_unlocked(name: SpaceLocationID)` → `void` | Is the specified planet unlocked for this force? |
| `is_space_platforms_unlocked()` → `boolean` | Are the space platforms unlocked? This basically just controls the availability of the space platforms button. |
| `kill_all_units()` → `void` | Kill all units and flush the pathfinder. |
| `lock_quality(quality: QualityID)` → `void` | Locks the quality to not be accessible to this force. |
| `lock_space_location(name: SpaceLocationID)` → `void` | Locks the planet to not be accessible to this force. |
| `lock_space_platforms()` → `void` | Locks the space platforms, which disables the space platforms button |
| `play_sound(sound_specification: PlaySoundSpecification)` → `void` | Play a sound for every player in this force. |
| `print(message: LocalisedString, print_settings?: PrintSettings)` → `void` | Print text to the chat console of all players on this force. |
| `rechart(surface?: SurfaceIdentification)` → `void` | Force a rechart of the whole chart. |
| `research_all_technologies(include_disabled_prototypes?: boolean)` → `void` | Research all technologies. |
| `reset()` → `void` | Reset everything. All technologies are set to not researched, all modifiers are set to default values. |
| `reset_evolution()` → `void` | Resets evolution for this force to zero. |
| `reset_recipes()` → `void` | Load the original version of all recipes from the prototypes. |
| `reset_technologies()` → `void` | Load the original versions of technologies from prototypes. Preserves research state of technologies. |
| `reset_technology_effects()` → `void` | Reapplies all possible research effects, including unlocked recipes. Any custom changes are lost. Preserves research state of technologies. |
| `script_trigger_research(technology: TechnologyID)` → `void` | Trigger the "scripted" research triggerResearchTrigger of a technology, researching it. Does nothing if the technology does not have a "scri |
| `set_ammo_damage_modifier(ammo: string, modifier: double)` → `void` |  |
| `set_cease_fire(cease_fire: boolean, other: ForceID)` → `void` | Add `other` force to this force's cease fire list. Forces on the cease fire list won't be targeted for attack. |
| `set_evolution_factor(factor: double, surface?: SurfaceIdentification)` → `void` | Sets the evolution factor of this force on the given surface. |
| `set_evolution_factor_by_killing_spawners(factor: double, surface?: SurfaceIdentification)` → `void` | Sets the spawner kill part of the evolution factor of this force on the given surface. |
| `set_evolution_factor_by_pollution(factor: double, surface?: SurfaceIdentification)` → `void` | Sets the pollution part of the evolution factor of this force on the given surface. |
| `set_evolution_factor_by_time(factor: double, surface?: SurfaceIdentification)` → `void` | Sets the time part of the evolution factor of this force on the given surface. |
| `set_friend(friend: boolean, other: ForceID)` → `void` | Add `other` force to this force's friends list. Friends have unrestricted access to buildings and turrets won't fire at them. |
| `set_gun_speed_modifier(ammo: string, modifier: double)` → `void` |  |
| `set_hand_crafting_disabled_for_recipe(hand_crafting_disabled: boolean, recipe: RecipeID)` → `void` | Sets if the given recipe can be hand-crafted. This is used to explicitly disable hand crafting a recipe - it won't allow hand-crafting other |
| `set_item_launched(count: uint32, item: ItemID)` → `void` | Sets the count of a given item launched in rockets. |
| `set_spawn_position(position: MapPosition, surface: SurfaceIdentification)` → `void` |  |
| `set_surface_hidden(hidden: boolean, surface: SurfaceIdentification)` → `void` |  |
| `set_turret_attack_modifier(modifier: double, turret: EntityID)` → `void` |  |
| `unchart_chunk(chunk_position: ChunkPosition, surface: SurfaceIdentification)` → `void` |  |
| `unlock_quality(quality: QualityID)` → `void` | Unlocks the quality to be accessible to this force. |
| `unlock_space_location(name: SpaceLocationID)` → `void` | Unlocks the planet to be accessible to this force. |
| `unlock_space_platforms()` → `void` | Unlocks the space platforms, which enables the space platforms button |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `ai_controllable` | ? | `?` | Enables some higher-level AI behaviour for this force. When set to `true`, biters belonging to this force will automatically expan |
| `artillery_range_modifier` | ? | `?` |  |
| `beacon_distribution_modifier` | ? | `?` |  |
| `belt_stack_size_bonus` | ? | `?` | Belt stack size bonus. |
| `bulk_inserter_capacity_bonus` | ? | `?` | Number of items that can be transferred by bulk inserters. When writing to this value, it must be >= 0 and <= 254. |
| `character_build_distance_bonus` | ? | `?` |  |
| `character_health_bonus` | ? | `?` |  |
| `character_inventory_slots_bonus` | ? | `?` | The number of additional inventory slots the character main inventory has. |
| `character_item_drop_distance_bonus` | ? | `?` |  |
| `character_item_pickup_distance_bonus` | ? | `?` |  |
| `character_logistic_requests` | ? | `?` | `true` if character requester logistics is enabled. |
| `character_loot_pickup_distance_bonus` | ? | `?` |  |
| `character_reach_distance_bonus` | ? | `?` |  |
| `character_resource_reach_distance_bonus` | ? | `?` |  |
| `character_running_speed_modifier` | ? | `?` | Modifies the running speed of all characters in this force by the given value as a percentage. Setting the running modifier to `0. |
| `character_trash_slot_count` | ? | `?` | Number of character trash slots. |
| `circuit_network_enabled` | ? | `?` |  |
| `cliff_deconstruction_enabled` | ? | `?` | When true, cliffs will be marked for deconstruction when trying to force-build things that collide. |
| `color` | ? | `?` | Effective color of this force. |
| `connected_players` | ? | `?` | The connected players belonging to this force. |
| `create_ghost_on_entity_death` | ? | `?` | When an entity dies, a ghost will be placed for automatic reconstruction. |
| `current_research` | ? | `?` | The currently ongoing technology research, if any. |
| `custom_color` | ? | `?` | Custom color for this force. If specified, will take priority over other sources of the force color. Writing `nil` clears custom c |
| `deconstruction_time_to_live` | ? | `?` | The time, in ticks, before a deconstruction order is removed. |
| `following_robots_lifetime_modifier` | ? | `?` | Additional lifetime for following robots. |
| `friendly_fire` | ? | `?` | If friendly fire is enabled for this force. |
| `index` | ? | `?` | This force's index in LuaGameScript::forcesLuaGameScript::forces unique ID. It is assigned when a force is created, and remains so |
| `inserter_stack_size_bonus` | ? | `?` | The inserter stack size bonus for non stack inserters |
| `items_launched` | ? | `?` | All of the items that have been launched in rockets. |
| `laboratory_productivity_bonus` | ? | `?` |  |
| `laboratory_speed_modifier` | ? | `?` |  |
| `logistic_networks` | ? | `?` | List of logistic networks, grouped by surface. |
| `manual_crafting_speed_modifier` | ? | `?` | Multiplier of the manual crafting speed. Default value is `0`. The actual crafting speed will be multiplied by `1 + manual_craftin |
| `manual_mining_speed_modifier` | ? | `?` | Multiplier of the manual mining speed. Default value is `0`. The actual mining speed will be multiplied by `1 + manual_mining_spee |
| `max_failed_attempts_per_tick_per_construction_queue` | ? | `?` |  |
| `max_successful_attempts_per_tick_per_construction_queue` | ? | `?` |  |
| `maximum_following_robot_count` | ? | `?` | Maximum number of follower robots. |
| `mining_drill_productivity_bonus` | ? | `?` |  |
| `mining_with_fluid` | ? | `?` |  |
| `name` | ? | `?` | Name of the force. |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `platforms` | ? | `?` | The space platforms that belong to this force mapped by their index value. |
| `players` | ? | `?` | Players belonging to this force. |
| `previous_research` | ? | `?` | The previous research, if any. |
| `rail_planner_allow_elevated_rails` | ? | `?` |  |
| `rail_support_on_deep_oil_ocean` | ? | `?` |  |
| `recipes` | ? | `?` | Recipes available to this force, indexed by `name`. |
| `research_enabled` | ? | `?` | Whether research is enabled for this force, see LuaForce::enable_researchLuaForce::enable_research and LuaForce::disable_researchL |
| `research_progress` | ? | `?` | Progress of current research, as a number in range `0, 1`. |
| `research_queue` | ? | `?` | The research queue of this force. The first technology in the array is the currently active one. Reading this attribute gives an a |
| `rockets_launched` | ? | `?` | The number of rockets launched. |
| `share_chart` | ? | `?` | If sharing chart data is enabled for this force. |
| `technologies` | ? | `?` | Technologies owned by this force, indexed by `name`. |
| `train_braking_force_bonus` | ? | `?` |  |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
| `vehicle_logistics` | ? | `?` | When true, cars/tanks that support logistics will be able to use them. |
| `worker_robots_battery_modifier` | ? | `?` |  |
| `worker_robots_speed_modifier` | ? | `?` |  |
| `worker_robots_storage_bonus` | ? | `?` |  |
---

### LuaTechnology

One research item.

#### Methods

| Signature | Description |
|-----------|-------------|
| `reload()` → `void` | Reload this technology from its prototype. |
| `research_recursive()` → `void` | Research this technology and all of its prerequisites recursively. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `enabled` | ? | `?` | Can this technology be researched? |
| `force` | ? | `?` | The force this technology belongs to. |
| `level` | ? | `?` | The current level of this technology. For level-based technology writing to this is the same as researching the technology to the  |
| `localised_description` | ? | `?` |  |
| `localised_name` | ? | `?` | Localised name of this technology. |
| `name` | ? | `?` | Name of this technology. |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `order` | ? | `?` | The string used to alphabetically sort these prototypes. It is a simple string that has no additional semantic meaning. |
| `prerequisites` | ? | `?` | Prerequisites of this technology. The result maps technology name to the LuaTechnologyLuaTechnology object. |
| `prototype` | ? | `?` | The prototype of this technology. |
| `research_unit_count` | ? | `?` | The number of research units required for this technology. |
| `research_unit_count_formula` | ? | `?` | The count formula, if this research has any. See TechnologyUnit::count_formulaTechnologyUnit::count_formula for details. |
| `research_unit_energy` | ? | `?` | Amount of energy required to finish a unit of research. |
| `research_unit_ingredients` | ? | `?` | The types of ingredients that labs will require to research this technology. |
| `researched` | ? | `?` | Has this technology been researched? Switching from `false` to `true` will trigger the technology advancement perks; switching fro |
| `saved_progress` | ? | `?` | Saved technology progress fraction as a value in range `0, 1`. 0 means there is no saved progress. |
| `successors` | ? | `?` | Successors of this technology, i.e. technologies which have this technology as a prerequisite. |
| `upgrade` | ? | `?` | Is this an upgrade-type research? |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
| `visible_when_disabled` | ? | `?` | If this technology will be visible in the research GUI even though it is disabled. |
---

### LuaRecipe

A crafting recipe.

#### Methods

| Signature | Description |
|-----------|-------------|
| `has_category(category: RecipeCategoryID)` → `boolean` | Checks if recipe has given category |
| `reload()` → `void` | Reload the recipe from the prototype. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `additional_categories` | ? | `?` | Additional categories of this recipe. |
| `category` | ? | `?` | Category of the recipe. |
| `enabled` | ? | `?` | Can the recipe be used? |
| `energy` | ? | `?` | Energy required to execute this recipe. This directly affects the crafting time: Recipe's energy is exactly its crafting time in s |
| `force` | ? | `?` | The force that owns this recipe. |
| `group` | ? | `?` | Group of this recipe. |
| `hidden` | ? | `?` | Is the recipe hidden? Hidden recipes don't show up in the crafting menu. |
| `hidden_from_flow_stats` | ? | `?` | Is the recipe hidden from flow statistics? |
| `ingredients` | ? | `?` | The ingredients to this recipe. |
| `localised_description` | ? | `?` |  |
| `localised_name` | ? | `?` | Localised name of the recipe. |
| `name` | ? | `?` | Name of the recipe. This can be different than the name of the result items as there could be more recipes to make the same item. |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `order` | ? | `?` | The string used to alphabetically sort these prototypes. It is a simple string that has no additional semantic meaning. |
| `productivity_bonus` | ? | `?` | The productivity bonus for this recipe. |
| `products` | ? | `?` | The results/products of this recipe. |
| `prototype` | ? | `?` | The prototype for this recipe. |
| `subgroup` | ? | `?` | Subgroup of this recipe. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
---

### LuaFlowStatistics

Encapsulates statistic data for different parts of the game.

#### Methods

| Signature | Description |
|-----------|-------------|
| `clear()` → `void` | Reset all the statistics data to 0. |
| `get_flow_count(category: string, count?: boolean, name: FlowStatisticsID, precision_index: defines.flow_precision_index, sample_index?: uint16)` → `double` | Gets the flow count value for the given time frame. If `sample_index` is not provided, then the value returned is the average across the pro |
| `get_input_count(id: FlowStatisticsID)` → `uint64 | double` | Gets the total input count for a given prototype. |
| `get_output_count(id: FlowStatisticsID)` → `uint64 | double` | Gets the total output count for a given prototype. |
| `get_storage_count(id: FlowStatisticsID)` → `uint64 | double` | Gets the total storage count for a given prototype. |
| `on_flow(count: float, id: FlowStatisticsID)` → `void` | Adds a value to this flow statistics. |
| `set_input_count(count: uint64 | double, id: FlowStatisticsID)` → `void` | Sets the total input count for a given prototype. |
| `set_output_count(count: uint64 | double, id: FlowStatisticsID)` → `void` | Sets the total output count for a given prototype. |
| `set_storage_count(count: uint64 | double, id: FlowStatisticsID)` → `void` | Sets the total storage count for a given prototype. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `force` | ? | `?` | The force these statistics belong to. `nil` for pollution statistics. |
| `input_counts` | ? | `?` | List of input counts indexed by prototype name. Represents the data that is shown on the left side of the GUI for the given statis |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `output_counts` | ? | `?` | List of output counts indexed by prototype name. Represents the data that is shown in the middle of the GUI for electric networks  |
| `storage_counts` | ? | `?` | List of storage counts indexed by prototype name. Represents the data that is shown on the right side of the GUI for electric netw |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
---

### LuaItemStack

A reference to an item and count owned by some external entity.

#### Methods

| Signature | Description |
|-----------|-------------|
| `add_ammo(amount: float)` → `void` | Add ammo to this ammo item. |
| `add_durability(amount: double)` → `void` | Add durability to this tool item. |
| `can_set_stack(stack?: ItemStackIdentification)` → `boolean` | Would a call to LuaItemStack::set_stackLuaItemStack::set_stack succeed? |
| `clear()` → `void` | Clear this item stack. |
| `create_grid()` → `LuaEquipmentGrid` | Creates the equipment grid for this item if it doesn't exist and this is an item-with-entity-data that supports equipment grids. |
| `drain_ammo(amount: float)` → `void` | Remove ammo from this ammo item. |
| `drain_durability(amount: double)` → `void` | Remove durability from this tool item. |
| `export_stack()` → `string` | Export a supported item blueprint, blueprint-book, deconstruction-planner, upgrade-planner, item-with-tags to a string. |
| `import_stack(data: string)` → `int32` | Import a supported item blueprint, blueprint-book, deconstruction-planner, upgrade-planner, item-with-tags from a string. |
| `set_stack(stack?: ItemStackIdentification)` → `boolean` | Set this item stack to another item stack. |
| `spoil()` → `void` | Spoils this item if the item can spoil. |
| `swap_stack(stack: LuaItemStack)` → `boolean` | Swaps this item stack with the given item stack if allowed. |
| `transfer_stack(amount?: uint32, stack: ItemStackIdentification)` → `boolean` | Transfers the given item stack into this item stack. |
| `use_capsule(entity: LuaEntity, target_position: MapPosition)` → `array[LuaEntity]` | Use the capsule item with the entity as the source, targeting the given position. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `count` | ? | `?` | Number of items in this stack. |
| `health` | ? | `?` | How much health the item has, as a number in range `0, 1`. |
| `is_module` | ? | `?` | If this is a module |
| `item` | ? | `?` | If the item has additional data, returns LuaItem pointing at the extra data, otherwise returns nil. |
| `name` | ? | `?` | Prototype name of the item held in this stack. |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `prototype` | ? | `?` | Prototype of the item held in this stack. |
| `quality` | ? | `?` | The quality of this item. |
| `spoil_percent` | ? | `?` | The percent spoiled this item is if it spoils. `0` in the case of the item not spoiling. |
| `spoil_tick` | ? | `?` | The tick this item spoils, or `0` if it does not spoil. When writing, setting to anything < the current game tick will spoil the i |
| `type` | ? | `?` | Type of the item prototype. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
| `valid_for_read` | ? | `?` | Is this valid for reading? Differs from the usual `valid` in that `valid` will be `true` even if the item stack is blank but the e |
---

### LuaItem & LuaItemCommon

`LuaItem` points at item extra-data (for items with entity data). `LuaItemCommon` is the mixin base for both `LuaItemStack` and `LuaItem` providing blueprint/deconstruction/upgrade operations.

#### LuaItem

A reference to an item with data.

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `item_stack` | ? | `?` | Object representing the item stack this item is located in right now. If its not possible to locate the item stack holding this it |
| `name` | ? | `?` | Name of the item prototype |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `prototype` | ? | `?` | Item prototype of this item |
| `quality` | ? | `?` | The quality of this item. |
| `type` | ? | `?` | Type of the item prototype |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
#### LuaItemCommon

Common methods related to usage of item with data.

#### Methods

| Signature | Description |
|-----------|-------------|
| `build_blueprint(build_mode?: defines.build_mode, by_player?: PlayerIdentification, direction?: defines.direction, force: ForceID, position: MapPosition, raise_built?: boolean, skip_fog_of_war?: boolean, surface: SurfaceIdentification)` → `array[LuaEntity]` | Build this blueprint at the given location. |
| `cancel_deconstruct_area(area: BoundingBox, by_player?: PlayerIdentification, force: ForceID, skip_fog_of_war?: boolean, super_forced?: boolean, surface: SurfaceIdentification)` → `void` | Cancel deconstruct the given area with this deconstruction item. |
| `clear_blueprint()` → `void` | Clears this blueprint item. |
| `clear_deconstruction_item()` → `void` | Clears all settings/filters on this deconstruction item resetting it to default values. |
| `clear_upgrade_item()` → `void` | Clears all settings/filters on this upgrade item resetting it to default values. |
| `create_blueprint(always_include_tiles?: boolean, area: BoundingBox, force: ForceID, include_entities?: boolean, include_fuel?: boolean, include_modules?: boolean, include_station_names?: boolean, include_trains?: boolean, surface: SurfaceIdentification)` → `dict[uint32, LuaEntity]` | Sets up this blueprint using the found blueprintable entities/tiles on the surface. |
| `deconstruct_area(area: BoundingBox, by_player?: PlayerIdentification, force: ForceID, skip_fog_of_war?: boolean, super_forced?: boolean, surface: SurfaceIdentification)` → `void` | Deconstruct the given area with this deconstruction item. |
| `get_blueprint_entities()` → `array[BlueprintEntity]` | The entities in this blueprint. |
| `get_blueprint_entity_count()` → `uint32` | Gets the number of entities in this blueprint item. |
| `get_blueprint_entity_tag(index: uint32, tag: string)` → `AnyBasic` | Gets the given tag on the given blueprint entity index in this blueprint item. |
| `get_blueprint_entity_tags(index: uint32)` → `Tags` | Gets the tags for the given blueprint entity index in this blueprint item. |
| `get_blueprint_tiles()` → `array[Tile]` | A list of the tiles in this blueprint. |
| `get_entity_filter(index: uint32)` → `ItemFilter` | Gets the entity filter at the given index for this deconstruction item. |
| `get_inventory(inventory: defines.inventory)` → `LuaInventory` | Access the inner inventory of an item. |
| `get_mapper(index: uint32, type: 'from' | 'to')` → `UpgradeMapperSource | UpgradeMapperDestination` | Gets the filter at the given index for this upgrade item. Note that sources `"from"` type that are undefined will read as `{type = "item"}`, |
| `get_tag(tag_name: string)` → `AnyBasic` | Gets the tag with the given name or returns `nil` if it doesn't exist. |
| `get_tile_filter(index: uint32)` → `string` | Gets the tile filter at the given index for this deconstruction item. |
| `is_blueprint_setup()` → `boolean` | Is this blueprint item setup? I.e. is it a non-empty blueprint? |
| `remove_tag(tag: string)` → `boolean` | Removes a tag with the given name. |
| `set_blueprint_entities(entities: array[BlueprintEntity])` → `void` | Set new entities to be a part of this blueprint. |
| `set_blueprint_entity_tag(index: uint32, tag: string, value: AnyBasic)` → `void` | Sets the given tag on the given blueprint entity index in this blueprint item. |
| `set_blueprint_entity_tags(index: uint32, tags: Tags)` → `void` | Sets the tags on the given blueprint entity index in this blueprint item. |
| `set_blueprint_tiles(tiles: array[Tile])` → `void` | Set specific tiles in this blueprint. |
| `set_entity_filter(filter: ItemFilter | nil, index: uint32)` → `boolean` | Sets the entity filter at the given index for this deconstruction item. |
| `set_mapper(index: uint32, mapper: UpgradeMapperSource | UpgradeMapperDestination | nil, type: 'from' | 'to')` → `void` | Sets the module filter at the given index for this upgrade item. |
| `set_tag(tag: AnyBasic, tag_name: string)` → `void` | Sets the tag with the given name and value. |
| `set_tile_filter(filter: string | LuaTilePrototype | LuaTile | nil, index: uint32)` → `boolean` | Sets the tile filter at the given index for this deconstruction item. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `active_index` | ? | `?` | The active blueprint index for this blueprint book. `nil` if this blueprint book is empty. |
| `allow_manual_label_change` | ? | `?` | Whether the label for this item can be manually changed. When false the label can only be changed through the API. |
| `ammo` | ? | `?` | Number of bullets left in the magazine. |
| `blueprint_absolute_snapping` | ? | `?` | If absolute snapping is enabled on this blueprint item. |
| `blueprint_description` | ? | `?` | The description for this blueprint or blueprint book |
| `blueprint_position_relative_to_grid` | ? | `?` | The offset from the absolute grid. `nil` if absolute snapping is not enabled. |
| `blueprint_snap_to_grid` | ? | `?` | The snapping grid size in this blueprint item. `nil` if snapping is not enabled. |
| `cost_to_build` | ? | `?` | List of raw materials required to build this blueprint. |
| `custom_description` | ? | `?` | The custom description this item-with-tags. This is shown over the normal item description if this is set to a non-empty value. |
| `default_icons` | ? | `?` | The default icons for a blueprint item. |
| `durability` | ? | `?` | Durability of the contained item. Automatically capped at the item's maximum durability. |
| `entity_auto_target_with_gunner` | ? | `?` | If this is an item with entity data, get the stored auto target with gunner state. |
| `entity_auto_target_without_gunner` | ? | `?` | If this is an item with entity data, get the stored auto target without gunner state. |
| `entity_color` | ? | `?` | If this is an item with entity data, get the stored entity color. |
| `entity_driver_is_gunner` | ? | `?` | If this is an item with entity data, get the stored driver is gunner state. |
| `entity_enable_logistics_while_moving` | ? | `?` | If this is an item with entity data, get the stored enable logistics while moving state. |
| `entity_filter_count` | ? | `?` | The number of entity filters this deconstruction item supports. |
| `entity_filter_mode` | ? | `?` | The blacklist/whitelist entity filter mode for this deconstruction item. |
| `entity_filters` | ? | `?` | The entity filters for this deconstruction item. The attribute is a sparse array with the keys representing the index of the filte |
| `entity_label` | ? | `?` | If this is an item with entity data, get the stored entity label. |
| `entity_logistic_sections` | ? | `?` | If this is an item with entity data, get the stored logistic filters. |
| `entity_logistics_enabled` | ? | `?` | If this is an item with entity data, get the stored vehicle logistics enabled state. |
| `entity_request_from_buffers` | ? | `?` | If this is an item with entity data, get the stored request from buffer state. |
| `grid` | ? | `?` | The equipment grid of this item, if any. |
| `is_ammo` | ? | `?` | If this is an ammo item. |
| `is_armor` | ? | `?` | If this is an armor item. |
| `is_blueprint` | ? | `?` | If this is a blueprint item. |
| `is_blueprint_book` | ? | `?` | If this is a blueprint book item. |
| `is_deconstruction_item` | ? | `?` | If this is a deconstruction tool item. |
| `is_item_with_entity_data` | ? | `?` | If this is an item with entity data item. |
| `is_item_with_inventory` | ? | `?` | If this is an item with inventory item. |
| `is_item_with_label` | ? | `?` | If this is an item with label item. |
| `is_item_with_tags` | ? | `?` | If this is an item with tags item. |
| `is_repair_tool` | ? | `?` | If this is a repair tool item. |
| `is_selection_tool` | ? | `?` | If this is a selection tool item. |
| `is_tool` | ? | `?` | If this is a tool item. |
| `is_upgrade_item` | ? | `?` | If this is a upgrade item. |
| `item_number` | ? | `?` | The unique identifier for this item, if any. Note that this ID stays the same no matter where the item is moved to. |
| `label` | ? | `?` | The current label for this item, if any. |
| `label_color` | ? | `?` | The current label color for this item, if any. |
| `mapper_count` | ? | `?` | The current count of mappers in the upgrade item. |
| `owner_location` | ? | `?` | The location of this item if it can be found. |
| `preview_icons` | ? | `?` | Icons of this blueprint item, blueprint book, deconstruction item or upgrade planner. An item that doesn't have icons returns `nil |
| `tags` | ? | `?` |  |
| `tile_filter_count` | ? | `?` | The number of tile filters this deconstruction item supports. |
| `tile_filter_mode` | ? | `?` | The blacklist/whitelist tile filter mode for this deconstruction item. |
| `tile_filters` | ? | `?` | The tile filters for this deconstruction item. The attribute is a sparse array with the keys representing the index of the filter. |
| `tile_selection_mode` | ? | `?` | The tile selection mode for this deconstruction item. |
| `trees_and_rocks_only` | ? | `?` | If this deconstruction item is set to allow trees and rocks only. |
---

### LuaRecord (Blueprint)

A reference to a record in the blueprint library.

#### Methods

| Signature | Description |
|-----------|-------------|
| `build_blueprint(build_mode?: defines.build_mode, by_player?: PlayerIdentification, direction?: defines.direction, force: ForceID, position: MapPosition, raise_built?: boolean, skip_fog_of_war?: boolean, surface: SurfaceIdentification)` → `array[LuaEntity]` | Build this blueprint at the given location. |
| `cancel_deconstruct_area(area: BoundingBox, by_player?: PlayerIdentification, force: ForceID, skip_fog_of_war?: boolean, super_forced?: boolean, surface: SurfaceIdentification)` → `void` | Cancel deconstruct the given area with this deconstruction planner. |
| `clear_blueprint()` → `void` | Clears this blueprint. |
| `clear_deconstruction_data()` → `void` | Clears all settings/filters on this deconstruction planner, resetting it to default values. |
| `clear_upgrade_data()` → `void` | Clears all settings/filters on this upgrade planner, resetting it to default values. |
| `create_blueprint(always_include_tiles?: boolean, area: BoundingBox, force: ForceID, include_entities?: boolean, include_fuel?: boolean, include_modules?: boolean, include_station_names?: boolean, include_trains?: boolean, surface: SurfaceIdentification)` → `dict[uint32, LuaEntity]` | Sets up this blueprint using the found blueprintable entities/tiles on the surface. |
| `deconstruct_area(area: BoundingBox, by_player?: PlayerIdentification, force: ForceID, skip_fog_of_war?: boolean, super_forced?: boolean, surface: SurfaceIdentification)` → `void` | Deconstruct the given area with this deconstruction planner. |
| `export_record()` → `string` | Exports this record to a string. |
| `get_active_index(player: PlayerIdentification)` → `uint32` | The active index of this BlueprintBookRecord. For records in "my blueprints", the result will be the same regardless of the player, but reco |
| `get_blueprint_entities()` → `array[BlueprintEntity]` | The entities in this blueprint. |
| `get_blueprint_entity_count()` → `uint32` | Gets the number of entities in this blueprint blueprint. |
| `get_blueprint_entity_tag(index: uint32, tag: string)` → `AnyBasic` | Gets the given tag on the given blueprint entity index in this blueprint. |
| `get_blueprint_entity_tags(index: uint32)` → `Tags` | Gets the tags for the given blueprint entity index in this blueprint. |
| `get_blueprint_tiles()` → `array[Tile]` | A list of the tiles in this blueprint. |
| `get_entity_filter(index: uint32)` → `ItemFilter` | Gets the entity filter at the given index for this deconstruction planner. |
| `get_mapper(index: uint32, type: 'from' | 'to')` → `UpgradeMapperSource | UpgradeMapperDestination` | Gets the filter at the given index for this upgrade item. Note that sources `"from"` type that are undefined will read as `{type = "item"}`, |
| `get_selected_record(player: PlayerIdentification)` → `LuaRecord` | Gets the currently selected record of the book for the given player. |
| `get_tile_filter(index: uint32)` → `string` | Gets the tile filter at the given index for this deconstruction planner. |
| `is_blueprint_setup()` → `boolean` | Is this blueprint setup? I.e. is it a non-empty blueprint? |
| `set_blueprint_entities(entities: array[BlueprintEntity])` → `void` | Set new entities to be a part of this blueprint. |
| `set_blueprint_entity_tag(index: uint32, tag: string, value: AnyBasic)` → `void` | Sets the given tag on the given blueprint entity index in this blueprint. |
| `set_blueprint_entity_tags(index: uint32, tags: Tags)` → `void` | Sets the tags on the given blueprint entity index in this blueprint. |
| `set_blueprint_tiles(tiles: array[Tile])` → `void` | Set specific tiles in this blueprint. |
| `set_entity_filter(filter: ItemFilter | nil, index: uint32)` → `boolean` | Sets the entity filter at the given index for this deconstruction planner. |
| `set_mapper(index: uint32, mapper: UpgradeMapperSource | UpgradeMapperDestination | nil, type: 'from' | 'to')` → `void` | Sets the module filter at the given index for this upgrade item. |
| `set_tile_filter(filter: string | LuaTilePrototype | LuaTile, index: uint32)` → `boolean` | Sets the tile filter at the given index for this deconstruction planner. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `blueprint_absolute_snapping` | ? | `?` | If absolute snapping is enabled on this blueprint. |
| `blueprint_description` | ? | `?` | The description for this blueprint or blueprint book. |
| `blueprint_position_relative_to_grid` | ? | `?` | The offset from the absolute grid. `nil` if absolute snapping is not enabled. |
| `blueprint_snap_to_grid` | ? | `?` | The snapping grid size in this blueprint. `nil` if snapping is not enabled. |
| `contents` | ? | `?` | The contents of this BlueprintBookRecord. This is sparse array - it may have gaps, so using `#` will not be reliable. Use LuaRecor |
| `contents_size` | ? | `?` | The highest populated index in the contents of this BlueprintBookRecord. |
| `cost_to_build` | ? | `?` | List of raw materials required to build this blueprint. |
| `default_icons` | ? | `?` | The default icons for a blueprint blueprint. |
| `entity_filter_count` | ? | `?` | The number of entity filters this deconstruction planner supports. |
| `entity_filter_mode` | ? | `?` | The blacklist/whitelist entity filter mode for this deconstruction planner. |
| `entity_filters` | ? | `?` | The entity filters for this deconstruction planner. The attribute is a sparse array with the keys representing the index of the fi |
| `is_blueprint_preview` | ? | `?` | Is this blueprint record a preview? A preview record must be synced by the player before entity and tile data can be read. This pr |
| `is_preview` | ? | `?` | Checks if this record is in a preview state. |
| `mapper_count` | ? | `?` | The current count of mappers in the upgrade item. |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `preview_icons` | ? | `?` | The preview icons for this record. |
| `tile_filter_count` | ? | `?` | The number of tile filters this deconstruction planner supports. |
| `tile_filter_mode` | ? | `?` | The blacklist/whitelist tile filter mode for this deconstruction planner. |
| `tile_filters` | ? | `?` | The tile filters for this deconstruction planner. The attribute is a sparse array with the keys representing the index of the filt |
| `tile_selection_mode` | ? | `?` | The tile selection mode for this deconstruction planner. |
| `trees_and_rocks_only` | ? | `?` | If this deconstruction planner, is set to allow trees and rocks only. |
| `type` | ? | `?` | The type of this blueprint record. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
| `valid_for_write` | ? | `?` | Is this record valid for writing? A record is invalid for write if it is a BlueprintRecord preview or if it is in the "My blueprin |
---

### LuaBurner

A reference to the burner energy source owned by a specific LuaEntityLuaEntity or LuaEquipmentLuaEquipment.

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `burnt_result_inventory` | ? | `?` | The burnt result inventory. |
| `currently_burning` | ? | `?` | The currently burning item. Writing `nil` will void the currently burning item without producing a LuaBurner::burnt_resultLuaBurne |
| `fuel_categories` | ? | `?` | The fuel categories this burner uses. |
| `heat` | ? | `?` | The current heat energy stored in this burner. |
| `heat_capacity` | ? | `?` | The maximum heat maximum energy that this burner can store. |
| `inventory` | ? | `?` | The fuel inventory. |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `owner` | ? | `?` | The owner of this burner energy source |
| `remaining_burning_fuel` | ? | `?` | The amount of energy left in the currently-burning fuel item. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
---

### LuaTile

A single "square" on the map.

#### Methods

| Signature | Description |
|-----------|-------------|
| `cancel_deconstruction(force: ForceID, player?: PlayerIdentification)` → `void` | Cancels deconstruction if it is scheduled, does nothing otherwise. |
| `collides_with(layer: CollisionLayerID)` → `boolean` | What type of things can collide with this tile? |
| `get_tile_ghosts(force?: ForceID)` → `array[LuaEntity]` | Gets all tile ghosts on this tile. |
| `has_tile_ghost(force?: ForceID)` → `boolean` | Does this tile have any tile ghosts on it. |
| `order_deconstruction(force: ForceID, player?: PlayerIdentification)` → `LuaEntity` | Orders deconstruction of this tile by the given force. |
| `to_be_deconstructed(force?: ForceID)` → `boolean` | Is this tile marked for deconstruction? |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `double_hidden_tile` | ? | `?` | The name of the LuaTilePrototypeLuaTilePrototype double hidden under this tile or `nil` if there is no double hidden tile. |
| `hidden_tile` | ? | `?` | The name of the LuaTilePrototypeLuaTilePrototype hidden under this tile, if any. |
| `name` | ? | `?` | Prototype name of this tile. E.g. `"sand-3"` or `"grass-2"`. |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `position` | ? | `?` | The position this tile references. |
| `prototype` | ? | `?` |  |
| `surface` | ? | `?` | The surface this tile is on. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
---

### LuaTransportLine

One line on a transport belt.

#### Methods

| Signature | Description |
|-----------|-------------|
| `can_insert_at(position: float)` → `boolean` | Can an item be inserted at a given position? |
| `can_insert_at_back()` → `boolean` | Can an item be inserted at the back of this line? |
| `clear()` → `void` | Remove all items from this transport line. |
| `force_insert_at(belt_stack_size?: uint8, items: ItemStackIdentification, position: float)` → `void` | Force insert item at a given position. Inserts item onto a transport line. If a position is out of range, it is clamped to a closest valid p |
| `get_contents()` → `ItemWithQualityCounts` | Get counts of all items on this line, similar to how LuaInventory::get_contentsLuaInventory::get_contents does. |
| `get_detailed_contents()` → `array[DetailedItemOnLine]` | Get detailed information of items on this line, such as their position. |
| `get_item_count(item?: ItemFilter)` → `uint32` | Count some or all items on this line, similar to how LuaInventory::get_item_countLuaInventory::get_item_count does. |
| `get_line_item_position(position: float)` → `MapPosition` | Get a map position related to a position on a transport line. |
| `insert_at(belt_stack_size?: uint8, items: ItemStackIdentification, position: float)` → `boolean` | Insert items at a given position. |
| `insert_at_back(belt_stack_size?: uint8, items: ItemStackIdentification)` → `boolean` | Insert items at the back of this line. |
| `line_equals(other: LuaTransportLine)` → `boolean` | Returns whether the associated internal transport line of this line is the same as the others associated internal transport line. |
| `remove_item(items: ItemStackIdentification)` → `uint32` | Remove some items from this line. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `input_lines` | ? | `?` | The transport lines that this transport line is fed by or an empty table if none. |
| `line_length` | ? | `?` | Length of the transport line. Items can be inserted at line position from 0 up to returned value |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `output_lines` | ? | `?` | The transport lines that this transport line outputs items to or an empty table if none. |
| `owner` | ? | `?` | The entity this transport line belongs to. |
| `total_segment_length` | ? | `?` | Total length of segment which consists of this line, all lines in front and lines in the back directly connected. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
---

### LuaLogisticNetwork

A single logistic network of a given force on a given surface.

#### Methods

| Signature | Description |
|-----------|-------------|
| `can_satisfy_request(count?: uint32, include_buffers?: boolean, item: ItemWithQualityID)` → `boolean` | Can the network satisfy a request for a given item and count. |
| `find_cell_closest_to(position: MapPosition)` → `LuaLogisticCell` | Find logistic cell closest to a given position. |
| `get_contents(member?: 'storage' | 'providers')` → `ItemWithQualityCounts` | Get item counts for the entire network, similar to how LuaInventory::get_contentsLuaInventory::get_contents does. |
| `get_item_count(item?: ItemWithQualityID, member?: 'storage' | 'providers')` → `int32` | Count given or all items in the network or given members. |
| `get_supply_counts(item: ItemWithQualityID)` → `LogisticsNetworkSupplyCounts` | Get the amount of items of the given type indexed by the storage member. |
| `get_supply_points(item: ItemWithQualityID)` → `LogisticsNetworkSupplyPoints` | Gets the logistic points with of the given type indexed by the storage member. |
| `insert(item: ItemStackIdentification, members?: 'storage' | 'storage-empty' | 'storage-empty-slot' | 'requester')` → `uint32` | Insert items into the logistic network. This will actually insert the items into some logistic chests. |
| `remove_item(item: ItemStackIdentification, members?: 'active-provider' | 'passive-provider' | 'buffer' | 'storage')` → `uint32` | Remove items from the logistic network. This will actually remove the items from some logistic chests. |
| `select_drop_point(members?: 'storage' | 'storage-empty' | 'storage-empty-slot' | 'requester', stack: ItemStackIdentification)` → `LuaLogisticPoint` | Find a logistic point to drop the specific item stack. |
| `select_pickup_point(include_buffers?: boolean, members?: 'active-provider' | 'passive-provider' | 'buffer' | 'storage', name: ItemWithQualityID, position?: MapPosition)` → `LuaLogisticPoint` | Find the 'best' logistic point with this item ID and from the given position or from given chest type. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `active_provider_points` | ? | `?` | All active provider points in this network. |
| `all_construction_robots` | ? | `?` | The total number of construction robots in the network idle and active + in roboports. |
| `all_logistic_robots` | ? | `?` | The total number of logistic robots in the network idle and active + in roboports. |
| `available_construction_robots` | ? | `?` | Number of construction robots available for a job. |
| `available_logistic_robots` | ? | `?` | Number of logistic robots available for a job. |
| `cells` | ? | `?` | All cells in this network. |
| `construction_robots` | ? | `?` | All construction robots in this logistic network. |
| `custom_name` | ? | `?` | The custom logistic network name set by the player or by script, if any. |
| `empty_provider_points` | ? | `?` | All things that have empty provider points in this network. |
| `empty_providers` | ? | `?` | All entities that have empty logistic provider points in this network. |
| `force` | ? | `?` | The force this logistic network belongs to. |
| `logistic_members` | ? | `?` | All other entities that have logistic points in this network inserters mostly. |
| `logistic_robots` | ? | `?` | All logistic robots in this logistic network. |
| `network_id` | ? | `?` | The unique logistic network ID. |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `passive_provider_points` | ? | `?` | All passive provider points in this network. |
| `provider_points` | ? | `?` | All things that have provider points in this network. |
| `providers` | ? | `?` | All entities that have logistic provider points in this network. |
| `requester_points` | ? | `?` | All things that have requester points in this network. |
| `requesters` | ? | `?` | All entities that have logistic requester points in this network. |
| `robot_limit` | ? | `?` | Maximum number of robots the network can work with. Currently only used for the personal roboport. |
| `robots` | ? | `?` | All robots in this logistic network. |
| `storage_points` | ? | `?` | All things that have storage points in this network. |
| `storages` | ? | `?` | All entities that have logistic storage points in this network. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
---

### LuaLogisticCell

Logistic cell of a particular LuaEntityLuaEntity.

#### Methods

| Signature | Description |
|-----------|-------------|
| `is_in_construction_range(position: MapPosition)` → `boolean` | Is a given position within the construction range of this cell? |
| `is_in_logistic_range(position: MapPosition)` → `boolean` | Is a given position within the logistic range of this cell? |
| `is_neighbour_with(other: LuaLogisticCell)` → `boolean` | Are two cells neighbours? |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `charge_approach_distance` | ? | `?` | Radius at which the robots hover when waiting to be charged. |
| `charging_robot_count` | ? | `?` | Number of robots currently charging. |
| `charging_robots` | ? | `?` | Robots currently being charged. |
| `construction_radius` | ? | `?` | Construction radius of this cell. |
| `logistic_network` | ? | `?` | The network that owns this cell, if any. |
| `logistic_radius` | ? | `?` | Logistic radius of this cell. |
| `logistics_connection_distance` | ? | `?` | Logistic connection distance of this cell. |
| `mobile` | ? | `?` | `true` if this is a mobile cell. The logistic cell created by roboport equipment considered is mobile. |
| `neighbours` | ? | `?` | Neighbouring cells. |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `owner` | ? | `?` | This cell's owner. |
| `stationed_construction_robot_count` | ? | `?` | Number of stationed construction robots in this cell. |
| `stationed_logistic_robot_count` | ? | `?` | Number of stationed logistic robots in this cell. |
| `to_charge_robot_count` | ? | `?` | Number of robots waiting to charge. |
| `to_charge_robots` | ? | `?` | Robots waiting to charge. |
| `transmitting` | ? | `?` | `true` if this cell is active. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
---

### LuaEquipment

An item in a LuaEquipmentGridLuaEquipmentGrid, for example a fusion reactor placed in one's power armor.

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `burner` | ? | `?` | The burner energy source for this equipment, if any. |
| `energy` | ? | `?` | Current available energy. |
| `generator_power` | ? | `?` | Energy generated per tick. |
| `ghost_name` | ? | `?` | Name of the equipment contained in this ghost |
| `ghost_prototype` | ? | `?` | The prototype of the equipment contained in this ghost. |
| `ghost_type` | ? | `?` | Type of the equipment contained in this ghost. |
| `inventory_bonus` | ? | `?` | Inventory size bonus. |
| `max_energy` | ? | `?` | Maximum amount of energy that can be stored in this equipment. |
| `max_shield` | ? | `?` | Maximum shield value. `0` if this equipment doesn't have a shield. |
| `max_solar_power` | ? | `?` | Maximum energy per tick crated by this equipment on the current surface. Actual generated energy varies depending on the daylight  |
| `movement_bonus` | ? | `?` | Movement speed bonus. |
| `name` | ? | `?` | Name of this equipment. |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `position` | ? | `?` | Position of this equipment in the equipment grid. |
| `prototype` | ? | `?` |  |
| `quality` | ? | `?` | Quality of this equipment. |
| `shape` | ? | `?` | Shape of this equipment. |
| `shield` | ? | `?` | Current shield value of the equipment. Can't be set higher than LuaEquipment::max_shieldLuaEquipment::max_shield. |
| `to_be_removed` | ? | `?` | If this equipment is marked to be removed. |
| `type` | ? | `?` | Type of this equipment. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
---

### LuaEquipmentGrid

An equipment grid is for example the inside of a power armor.

#### Methods

| Signature | Description |
|-----------|-------------|
| `can_move(equipment: LuaEquipment, position: EquipmentPosition)` → `boolean` | Check whether moving an equipment would succeed. |
| `cancel_removal(equipment: LuaEquipment)` → `boolean` | Cancels removal for the given equipment. |
| `clear(by_player?: PlayerIdentification)` → `void` | Clear all equipment from the grid, removing it without actually returning it. |
| `count(equipment?: EquipmentWithQualityID)` → `uint32` | Get the number of all or some equipment in this grid. |
| `find(equipment: EquipmentWithQualityID, search_ghosts?: boolean)` → `LuaEquipment` | Find equipment by name. |
| `get(position: EquipmentPosition)` → `LuaEquipment` | Find equipment in the Equipment Grid colliding with this position. |
| `get_contents()` → `array[EquipmentWithQualityCounts]` | Get counts of all equipment in this grid. |
| `get_generator_energy(quality?: QualityID)` → `double` | Total energy per tick generated by the equipment inside this grid. |
| `move(equipment: LuaEquipment, position: EquipmentPosition)` → `boolean` | Move an equipment within this grid. |
| `order_removal(equipment: LuaEquipment)` → `boolean` | Marks the given equipment for removal. If the given equipment is a ghost it is removed. |
| `put(by_player?: PlayerIdentification, ghost?: boolean, name: EquipmentID, position?: EquipmentPosition, quality?: QualityID)` → `LuaEquipment` | Insert an equipment into the grid. |
| `revive(equipment: LuaEquipment)` → `LuaEquipment` | Revives the given equipment ghost if possible. |
| `take(by_player?: PlayerIdentification, equipment?: LuaEquipment, position?: EquipmentPosition)` → `ItemWithQualityCount` | Remove an equipment from the grid. |
| `take_all(by_player?: PlayerIdentification)` → `ItemWithQualityCounts` | Remove all equipment from the grid. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `available_in_batteries` | ? | `?` | The total energy stored in all batteries in the equipment grid. |
| `battery_capacity` | ? | `?` | Total energy storage capacity of all batteries in the equipment grid. |
| `entity_owner` | ? | `?` | The entity that this equipment grid is owned by in some inventory or item stack. |
| `equipment` | ? | `?` | All the equipment in this grid. |
| `height` | ? | `?` | Height of the equipment grid. |
| `inhibit_movement_bonus` | ? | `?` | Whether this grid's equipment movement bonus is active. |
| `inventory_bonus` | ? | `?` | The total amount of inventory bonus this equipment grid gives. |
| `itemstack_owner` | ? | `?` | The item stack that this equipment grid is owned by. |
| `max_shield` | ? | `?` | The maximum amount of shield hitpoints this equipment grid has across all shield equipment. |
| `max_solar_energy` | ? | `?` | Maximum energy per tick that can be created by all solar panels in the equipment grid on the current surface. Actual generated ene |
| `movement_bonus` | ? | `?` | The total amount of movement bonus this equipment grid gives. |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `player_owner` | ? | `?` | The player that this equipment grid is owned by in some inventory or item stack. |
| `prototype` | ? | `?` |  |
| `shield` | ? | `?` | The amount of shield hitpoints this equipment grid currently has across all shield equipment. |
| `unique_id` | ? | `?` | Unique identifier of this equipment grid. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
| `width` | ? | `?` | Width of the equipment grid. |
---

### LuaFluidBox

An array of fluid boxes of an entity.

#### Methods

| Signature | Description |
|-----------|-------------|
| `add_linked_connection(other_entity: LuaEntity, other_linked_connection_id: uint32, this_linked_connection_id: uint32)` → `void` | Registers a linked connection between this entity and other entity. Because entity may have multiple fluidboxes, each with multiple connecti |
| `flush(fluid?: FluidID, index: uint32)` → `dict[string, FluidAmount]` | Flushes all fluid from this fluidbox and its fluid system. |
| `get_capacity(index: uint32)` → `double` | The capacity of the given fluidbox segment. |
| `get_connections(index: uint32)` → `array[LuaFluidBox]` | The fluidboxes to which the fluidbox at the given index is connected. |
| `get_filter(index: uint32)` → `FluidBoxFilter` | Get a fluid box filter |
| `get_fluid_segment_contents(index: uint32)` → `dict[string, uint32]` | Gets counts of all fluids in the fluid segment. May return `nil` for fluid wagon, fluid turret's internal buffer, or a fluidbox which does n |
| `get_fluid_segment_extent_bounding_box(index: uint32)` → `BoundingBox` | Gets the current extent bounding box of the fluid segment this fluid box belongs to. May return `nil` for fluid wagon, fluid turret's intern |
| `get_fluid_segment_id(index: uint32)` → `uint32` | Gets the unique ID of the fluid segment this fluid box belongs to. May return `nil` for fluid wagon, fluid turret's internal buffer or a flu |
| `get_linked_connection(this_linked_connection_id: uint32)` → `LuaEntity, uint32` | Returns other end of a linked connection. |
| `get_linked_connections()` → `array[FluidBoxConnectionRecord]` | Returns list of all linked connections registered for this entity. |
| `get_locked_fluid(index: uint32)` → `string` | Returns the fluid the fluidbox is locked onto |
| `get_pipe_connections(index: uint32)` → `array[PipeConnection]` | Get the fluid box's connections and associated data. |
| `get_prototype(index: uint32)` → `LuaFluidBoxPrototype | array[LuaFluidBoxPrototype]` | The prototype of this fluidbox index. If this is used on a fluidbox of a crafting machine which due to recipe was created by merging multipl |
| `remove_linked_connection(this_linked_connection_id: uint32)` → `void` | Removes linked connection record. If connected, other end will be also removed. |
| `set_filter(filter: FluidBoxFilterSpec | nil, index: uint32)` → `boolean` | Set a fluid box filter. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `owner` | ? | `?` | The entity that owns this fluidbox. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
---

### LuaCircuitNetwork

A circuit network associated with a given entity, connector, and wire type.

#### Methods

| Signature | Description |
|-----------|-------------|
| `get_signal(signal: SignalID)` → `int32` |  |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `connected_circuit_count` | ? | `?` | The number of circuits connected to this network. |
| `entity` | ? | `?` | The entity this circuit network reference is associated with. |
| `network_id` | ? | `?` | The circuit networks ID. |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `signals` | ? | `?` | The circuit network signals last tick. `nil` if there were no signals last tick. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
| `wire_connector_id` | ? | `?` | Wire connector ID on associated entity this network was gotten from. |
| `wire_type` | ? | `?` | The wire type this network is associated with. |
---

### LuaTrain

A train.

#### Methods

| Signature | Description |
|-----------|-------------|
| `clear_fluids_inside()` → `void` | Clears all fluids in this train. |
| `clear_items_inside()` → `void` | Clear all items in this train. |
| `get_contents()` → `ItemWithQualityCounts` | Get a mapping of the train's inventory. |
| `get_fluid_contents()` → `dict[string, FluidAmount]` | Gets a mapping of the train's fluid inventory. |
| `get_fluid_count(fluid?: string)` → `double` | Get the amount of a particular fluid stored in the train. |
| `get_item_count(item?: ItemFilter)` → `uint32` | Get the amount of a particular item stored in the train. |
| `get_rail_end(direction: defines.rail_direction)` → `LuaRailEnd` | Gets a LuaRailEnd object pointing away from the train at specified end of the train |
| `get_rails()` → `array[LuaEntity]` | Gets all rails under the train. |
| `get_schedule()` → `LuaSchedule` |  |
| `go_to_station(index: uint32)` → `void` | Go to the station specified by the index in the train's schedule. |
| `insert(stack: ItemStackIdentification)` → `void` | Insert a stack into the train. |
| `insert_fluid(fluid: Fluid)` → `double` | Inserts the given fluid into the first available location in this train. |
| `recalculate_path(force?: boolean)` → `boolean` | Checks if the path is invalid and tries to re-path if it isn't. |
| `remove_fluid(fluid: Fluid)` → `double` | Remove some fluid from the train. |
| `remove_item(stack: ItemStackIdentification)` → `uint32` | Remove some items from the train. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `back_end` | ? | `?` | Back end of the train: Rail and direction on that rail where the train will go when moving backward |
| `back_stock` | ? | `?` | The back stock of this train, if any. The back of the train is at the opposite end of the frontLuaTrain::front_stock. |
| `cargo_wagons` | ? | `?` | The cargo carriages the train contains. |
| `carriages` | ? | `?` | The rolling stocks this train is composed of, with the numbering starting at the frontLuaTrain::front_stock of the train. |
| `fluid_wagons` | ? | `?` | The fluid carriages the train contains. |
| `front_end` | ? | `?` | Front end of the train: Rail and direction on that rail where the train will go when moving forward |
| `front_stock` | ? | `?` | The front stock of this train, if any. The front of the train is in the direction that a majority of locomotives are pointing in.  |
| `group` | ? | `?` | The group this train belongs to. |
| `has_path` | ? | `?` | If this train has a path. |
| `id` | ? | `?` | The unique train ID. |
| `kill_count` | ? | `?` | The total number of kills by this train. |
| `killed_players` | ? | `?` | The players killed by this train. |
| `locomotives` | ? | `?` | Locomotives of the train. |
| `manual_mode` | ? | `?` | When `true`, the train is explicitly controlled by the player or script. When `false`, the train moves autonomously according to i |
| `max_backward_speed` | ? | `?` | Current max speed when moving backwards, depends on locomotive prototype and fuel. |
| `max_forward_speed` | ? | `?` | Current max speed when moving forward, depends on locomotive prototype and fuel. |
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
| `passengers` | ? | `?` | The player passengers on the train |
| `path` | ? | `?` | The path this train is using, if any. |
| `path_end_rail` | ? | `?` | The destination rail this train is currently pathing to, if any. |
| `path_end_stop` | ? | `?` | The destination train stop this train is currently pathing to, if any. |
| `riding_state` | ? | `?` | The riding state of this train. |
| `schedule` | ? | `?` | This train's current schedule, if any. Set to `nil` to clear. |
| `signal` | ? | `?` | The signal this train is arriving or waiting at, if any. |
| `speed` | ? | `?` | Current speed. |
| `state` | ? | `?` | This train's current state. |
| `station` | ? | `?` | The train stop this train is stopped at, if any. |
| `valid` | ? | `?` | Is this object valid? This Lua object holds a reference to an object within the game engine. It is possible that the game-engine o |
| `weight` | ? | `?` | The weight of this train. |
---

### LuaRCON

The global `rcon` object. `rcon.print()` is the **only** way to return data from RCON scripts.

An interface to send messages to the calling RCON interface through the global object named `rcon`.

#### Methods

| Signature | Description |
|-----------|-------------|
| `print(message: LocalisedString)` → `void` | Print text to the calling RCON interface if any. |

#### Attributes

| Attribute | Access | Type | Description |
|-----------|--------|------|-------------|
| `object_name` | ? | `?` | The class name of this object. Available even when `valid` is false. For LuaStruct objects it may also be suffixed with a dotted p |
---

## Defines

All game enumerations. Access via `defines.<category>.<value>`.

### defines.direction

Entity facing direction (16-directional in Factorio 2.x).

| Value | Description |
|-------|-------------|
| `defines.direction.east` |  |
| `defines.direction.eastnortheast` |  |
| `defines.direction.eastsoutheast` |  |
| `defines.direction.north` |  |
| `defines.direction.northeast` |  |
| `defines.direction.northnortheast` |  |
| `defines.direction.northnorthwest` |  |
| `defines.direction.northwest` |  |
| `defines.direction.south` |  |
| `defines.direction.southeast` |  |
| `defines.direction.southsoutheast` |  |
| `defines.direction.southsouthwest` |  |
| `defines.direction.southwest` |  |
| `defines.direction.west` |  |
| `defines.direction.westnorthwest` |  |
| `defines.direction.westsouthwest` |  |

### defines.inventory

Inventory slot types. Pass to `entity.get_inventory(defines.inventory.X)` or `player.get_inventory()`.

| Value | Description |
|-------|-------------|
| `defines.inventory.agricultural_tower_input` |  |
| `defines.inventory.agricultural_tower_output` |  |
| `defines.inventory.artillery_turret_ammo` |  |
| `defines.inventory.artillery_wagon_ammo` |  |
| `defines.inventory.assembling_machine_dump` | Used for ejected items, or items held by inserters that can't be inserted due the recipe being changed with the circuit  |
| `defines.inventory.assembling_machine_input` | Deprecated, replaced by `"crafter_input"`. |
| `defines.inventory.assembling_machine_modules` | Deprecated, replaced by `"crafter_modules"`. |
| `defines.inventory.assembling_machine_output` | Deprecated, replaced by `"crafter_output"`. |
| `defines.inventory.assembling_machine_trash` | Deprecated, replaced by `"crafter_trash"`. |
| `defines.inventory.asteroid_collector_arm` |  |
| `defines.inventory.asteroid_collector_output` |  |
| `defines.inventory.beacon_modules` |  |
| `defines.inventory.burnt_result` |  |
| `defines.inventory.car_ammo` |  |
| `defines.inventory.car_trash` |  |
| `defines.inventory.car_trunk` |  |
| `defines.inventory.cargo_landing_pad_main` |  |
| `defines.inventory.cargo_landing_pad_trash` |  |
| `defines.inventory.cargo_unit` | Inventory of cargo pod. |
| `defines.inventory.cargo_wagon` |  |
| `defines.inventory.character_ammo` |  |
| `defines.inventory.character_armor` |  |
| `defines.inventory.character_corpse` |  |
| `defines.inventory.character_guns` |  |
| `defines.inventory.character_main` |  |
| `defines.inventory.character_trash` |  |
| `defines.inventory.character_vehicle` |  |
| `defines.inventory.chest` |  |
| `defines.inventory.crafter_input` |  |
| `defines.inventory.crafter_modules` |  |
| `defines.inventory.crafter_output` |  |
| `defines.inventory.crafter_trash` | Used for spoil result items that do not fit into the recipe slots, and for items that are ejected when changing the reci |
| `defines.inventory.editor_ammo` |  |
| `defines.inventory.editor_armor` |  |
| `defines.inventory.editor_guns` |  |
| `defines.inventory.editor_main` |  |
| `defines.inventory.fuel` |  |
| `defines.inventory.furnace_modules` | Deprecated, replaced by `"crafter_modules"`. |
| `defines.inventory.furnace_result` | Deprecated, replaced by `"crafter_output"`. |
| `defines.inventory.furnace_source` | Deprecated, replaced by `"crafter_input"`. |
| `defines.inventory.furnace_trash` | Deprecated, replaced by `"crafter_trash"`. |
| `defines.inventory.god_main` |  |
| `defines.inventory.hub_main` |  |
| `defines.inventory.hub_trash` |  |
| `defines.inventory.item_main` |  |
| `defines.inventory.lab_input` |  |
| `defines.inventory.lab_modules` |  |
| `defines.inventory.lab_trash` |  |
| `defines.inventory.linked_container_main` |  |
| `defines.inventory.logistic_container_trash` |  |
| `defines.inventory.mining_drill_modules` |  |
| `defines.inventory.proxy_main` |  |
| `defines.inventory.roboport_material` |  |
| `defines.inventory.roboport_robot` |  |
| `defines.inventory.robot_cargo` |  |
| `defines.inventory.robot_repair` |  |
| `defines.inventory.rocket_silo_input` | Deprecated, replaced by `"crafter_input"`. |
| `defines.inventory.rocket_silo_modules` | Deprecated, replaced by `"crafter_modules"`. |
| `defines.inventory.rocket_silo_output` | Deprecated, replaced by `"crafter_output"`. |
| `defines.inventory.rocket_silo_rocket` |  |
| `defines.inventory.rocket_silo_trash` |  |
| `defines.inventory.spider_ammo` |  |
| `defines.inventory.spider_trash` |  |
| `defines.inventory.spider_trunk` |  |
| `defines.inventory.turret_ammo` |  |

### defines.flow_precision_index

Time window for `LuaFlowStatistics.get_flow_count()`.

| Value | Description |
|-------|-------------|
| `defines.flow_precision_index.fifty_hours` |  |
| `defines.flow_precision_index.five_seconds` |  |
| `defines.flow_precision_index.one_hour` |  |
| `defines.flow_precision_index.one_minute` |  |
| `defines.flow_precision_index.one_thousand_hours` |  |
| `defines.flow_precision_index.ten_hours` |  |
| `defines.flow_precision_index.ten_minutes` |  |
| `defines.flow_precision_index.two_hundred_fifty_hours` |  |

### defines.entity_status

Entity operational status. Read from `entity.status`.

| Value | Description |
|-------|-------------|
| `defines.entity_status.broken` | Only used if set through ContainerPrototype::default_statusContainerPrototype::default_status. |
| `defines.entity_status.cant_divide_segments` | Used by rail signals. |
| `defines.entity_status.charging` | Used by accumulators. |
| `defines.entity_status.closed_by_circuit_network` |  |
| `defines.entity_status.computing_navigation` | Used by asteroid collectors. |
| `defines.entity_status.destination_stop_full` | Used by trains. |
| `defines.entity_status.disabled` | Used by constant combinators: Combinator is turned off via switch in GUI. |
| `defines.entity_status.disabled_by_control_behavior` |  |
| `defines.entity_status.disabled_by_script` |  |
| `defines.entity_status.discharging` | Used by accumulators. |
| `defines.entity_status.fluid_ingredient_shortage` | Used by crafting machines. |
| `defines.entity_status.frozen` |  |
| `defines.entity_status.full_burnt_result_output` | Used by burner energy sources. |
| `defines.entity_status.full_output` | Used by crafting machines, boilers, burner energy sources and reactors: Reactor/burner has full burnt result inventory,  |
| `defines.entity_status.fully_charged` | Used by accumulators. |
| `defines.entity_status.ghost` | Used by ghosts. |
| `defines.entity_status.item_ingredient_shortage` | Used by crafting machines. |
| `defines.entity_status.launching_rocket` | Used by the rocket silo. |
| `defines.entity_status.low_input_fluid` | Used by boilers and fluid turrets: Boiler still has some fluid but is about to run out. |
| `defines.entity_status.low_power` |  |
| `defines.entity_status.low_temperature` | Used by heat energy sources. |
| `defines.entity_status.marked_for_deconstruction` |  |
| `defines.entity_status.missing_required_fluid` | Used by mining drills when the mining fluid is missing. |
| `defines.entity_status.missing_science_packs` | Used by labs. |
| `defines.entity_status.networks_connected` | Used by power switches. |
| `defines.entity_status.networks_disconnected` | Used by power switches. |
| `defines.entity_status.no_ammo` | Used by ammo turrets. |
| `defines.entity_status.no_filter` | Used by filter inserters. |
| `defines.entity_status.no_fuel` |  |
| `defines.entity_status.no_ingredients` | Used by furnaces. |
| `defines.entity_status.no_input_fluid` | Used by boilers, fluid turrets and fluid energy sources: Boiler has no fluid to work with. |
| `defines.entity_status.no_minable_resources` | Used by mining drills. |
| `defines.entity_status.no_modules_to_transmit` | Used by beacons. |
| `defines.entity_status.no_path` | Used by trains and space platform hubs. |
| `defines.entity_status.no_power` |  |
| `defines.entity_status.no_recipe` | Used by assembling machines. |
| `defines.entity_status.no_research_in_progress` | Used by labs. |
| `defines.entity_status.no_spot_seedable_by_inputs` | Used by agricultural towers. |
| `defines.entity_status.normal` |  |
| `defines.entity_status.not_connected_to_hub_or_pad` | Used by cargo bays. |
| `defines.entity_status.not_connected_to_rail` | Used by rail signals. |
| `defines.entity_status.not_enough_space_in_output` | Used by agricultural towers. |
| `defines.entity_status.not_enough_thrust` | Used by space platform hubs. |
| `defines.entity_status.not_plugged_in_electric_network` | Used by generators and solar panels. |
| `defines.entity_status.on_the_way` | Used by space platform hubs. |
| `defines.entity_status.opened_by_circuit_network` |  |
| `defines.entity_status.out_of_logistic_network` | Used by logistic containers. |
| `defines.entity_status.paused` | Used by space platform hubs. |
| `defines.entity_status.pipeline_overextended` | Used by pipes, pipes to ground and storage tanks. |
| `defines.entity_status.preparing_rocket_for_launch` | Used by the rocket silo. |
| `defines.entity_status.recharging_after_power_outage` | Used by roboports. |
| `defines.entity_status.recipe_is_parameter` | Used by assembling machines. |
| `defines.entity_status.recipe_not_researched` | Used by assembling machines. |
| `defines.entity_status.thrust_not_required` | Used by thrusters. |
| `defines.entity_status.turned_off_during_daytime` | Used by lamps. |
| `defines.entity_status.waiting_at_stop` | Used by trains. |
| `defines.entity_status.waiting_for_more_items` | Used by inserters when wait_for_full_hand is set. |
| `defines.entity_status.waiting_for_plants_to_grow` | Used by agricultural towers. |
| `defines.entity_status.waiting_for_rockets_to_arrive` | Used by space platform hubs. |
| `defines.entity_status.waiting_for_source_items` | Used by inserters. |
| `defines.entity_status.waiting_for_space_in_destination` | Used by inserters and mining drills. |
| `defines.entity_status.waiting_for_space_in_platform_hub` | Used by the rocket silo. |
| `defines.entity_status.waiting_for_target_to_be_built` | Used by inserters targeting entity ghosts. |
| `defines.entity_status.waiting_for_train` | Used by inserters targeting rails. |
| `defines.entity_status.waiting_in_orbit` | Used by space platform hubs. |
| `defines.entity_status.waiting_to_launch_rocket` | Used by the rocket silo. |
| `defines.entity_status.working` |  |

### defines.build_check_type

How to check placement validity in `surface.can_place_entity()`.

| Value | Description |
|-------|-------------|
| `defines.build_check_type.blueprint_ghost` |  |
| `defines.build_check_type.ghost_revive` |  |
| `defines.build_check_type.manual` |  |
| `defines.build_check_type.manual_ghost` |  |
| `defines.build_check_type.script` |  |
| `defines.build_check_type.script_ghost` |  |

### defines.logistic_mode

Logistic container operating mode.

| Value | Description |
|-------|-------------|
| `defines.logistic_mode.active_provider` |  |
| `defines.logistic_mode.buffer` |  |
| `defines.logistic_mode.none` |  |
| `defines.logistic_mode.passive_provider` |  |
| `defines.logistic_mode.requester` |  |
| `defines.logistic_mode.storage` |  |

### defines.wire_type

Circuit/wire connection type.

| Value | Description |
|-------|-------------|
| `defines.wire_type.copper` |  |
| `defines.wire_type.green` |  |
| `defines.wire_type.red` |  |

### defines.controllers

Player controller type. Read from `player.controller_type`.

| Value | Description |
|-------|-------------|
| `defines.controllers.character` | The controller controls a character. This is the default controller in freeplay. |
| `defines.controllers.cutscene` | The player can't interact with the world, and the camera pans around in a predefined manner. |
| `defines.controllers.editor` | The Editor Controller near ultimate power to do almost anything in the game. |
| `defines.controllers.ghost` | Can't interact with the world, can only observe. Used in the multiplayer waiting-to-respawn screen. |
| `defines.controllers.god` | The controller isn't tied to a character. This is the default controller in sandbox. |
| `defines.controllers.remote` | Can't move/change items but can build ghosts/change settings. |
| `defines.controllers.spectator` | Can't change anything in the world but can view anything. |

### defines.transport_line

Transport belt line index. Used with `entity.get_transport_line()`.

| Value | Description |
|-------|-------------|
| `defines.transport_line.left_line` |  |
| `defines.transport_line.left_split_line` |  |
| `defines.transport_line.left_underground_line` |  |
| `defines.transport_line.right_line` |  |
| `defines.transport_line.right_split_line` |  |
| `defines.transport_line.right_underground_line` |  |
| `defines.transport_line.secondary_left_line` |  |
| `defines.transport_line.secondary_left_split_line` |  |
| `defines.transport_line.secondary_right_line` |  |
| `defines.transport_line.secondary_right_split_line` |  |

### defines.deconstruction_item

Deconstruction planner sub-enumerations.

#### defines.deconstruction_item.entity_filter_mode

| Value | Description |
|-------|-------------|
| `defines.deconstruction_item.entity_filter_mode.blacklist` |  |
| `defines.deconstruction_item.entity_filter_mode.whitelist` |  |

#### defines.deconstruction_item.tile_filter_mode

| Value | Description |
|-------|-------------|
| `defines.deconstruction_item.tile_filter_mode.blacklist` |  |
| `defines.deconstruction_item.tile_filter_mode.whitelist` |  |

#### defines.deconstruction_item.tile_selection_mode

| Value | Description |
|-------|-------------|
| `defines.deconstruction_item.tile_selection_mode.always` |  |
| `defines.deconstruction_item.tile_selection_mode.never` |  |
| `defines.deconstruction_item.tile_selection_mode.normal` |  |
| `defines.deconstruction_item.tile_selection_mode.only` |  |

---

## Key Concepts / Types

These are the most important structural types used throughout the API.

### MapPosition

Coordinates on a surface, for example of an entity. MapPositions may be specified either as a dictionary with `x`, `y` as keys, or simply as an array with two e

**Table form:**

| Field | Required | Type |
|-------|----------|------|
| `x` | required | `double` |
| `y` | required | `double` |

**Tuple form:** `(double, double)`


### BoundingBox

Two positions, specifying the top-left and bottom-right corner of the box respectively. Like with MapPositionMapPosition, the names of the members may be omitte

**Table form:**

| Field | Required | Type |
|-------|----------|------|
| `left_top` | required | `MapPosition` |
| `orientation` | optional | `RealOrientation` |
| `right_bottom` | required | `MapPosition` |

**Tuple form:** `(MapPosition, MapPosition)`


### ChunkPosition

Coordinates of a chunk in a LuaSurfaceLuaSurface where each integer `x`/`y` represents a different chunk. This uses the same format as MapPositionMapPosition, m

**Table form:**

| Field | Required | Type |
|-------|----------|------|
| `x` | required | `int32` |
| `y` | required | `int32` |

**Tuple form:** `(int32, int32)`


### TilePosition

Coordinates of a tile on a LuaSurfaceLuaSurface where each integer `x`/`y` represents a different tile. This uses the same format as MapPositionMapPosition, exc

**Table form:**

| Field | Required | Type |
|-------|----------|------|
| `x` | required | `int32` |
| `y` | required | `int32` |

**Tuple form:** `(int32, int32)`


### Color

Red, green, blue and alpha values, all in range 0, 1 or all in range 0, 255 if any value is > 1. All values here are optional. Color channels default to `0`, th

**Table form:**

| Field | Required | Type |
|-------|----------|------|
| `a` | optional | `float` |
| `b` | optional | `float` |
| `g` | optional | `float` |
| `r` | optional | `float` |

**Tuple form:** `(float, float, float, float)`


### ItemFilter

An item filter may be specified in two ways, either as a string which is an item prototype name or as a table.

**Table form:**

| Field | Required | Type |
|-------|----------|------|
| `comparator` | optional | `ComparatorString` |
| `name` | optional | `ItemID` |
| `quality` | optional | `QualityID` |


### LocalisedString

Localised strings are a way to support translation of in-game text. They offer a language-independent code representation of the text that should be shown to pl


### Tags

A dictionary of string to the four basic Lua types: `string`, `boolean`, `number`, `table`. Note that the API returns tags as a simple table, meaning any modifi


### BlueprintEntity

The representation of an entity inside of a blueprint.

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `burner_fuel_inventory` | optional | `BlueprintInventoryWithFilters` | Used by entities with a burner energy source. |
| `direction` | optional | `defines.direction` | The direction the entity is facing. Only present for entities that can face in different directions  |
| `entity_number` | required | `uint32` | The entity's unique identifier in the blueprint. |
| `items` | optional | `array[BlueprintInsertPlan]` | The items that the entity will request when revived, if any. |
| `mirror` | optional | `boolean` | Whether this entity is mirrored. |
| `name` | required | `string` | The prototype name of the entity. |
| `position` | required | `MapPosition` | The position of the entity. |
| `quality` | optional | `string` | The prototype name of the entity's quality. |
| `tags` | optional | `Tags` | The entity tags of the entity, if there are any. |
| `wires` | optional | `array[BlueprintWire]` | Wires connected to this entity in the blueprint. |


### ItemWithCount

*A `{name: string, count: uint}` item quantity descriptor.*


### FluidBoxFilter

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `maximum_temperature` | required | `float` | The maximum temperature allowed into the fluidbox. |
| `minimum_temperature` | required | `float` | The minimum temperature allowed into the fluidbox. |
| `name` | required | `string` | Fluid prototype name of the filtered fluid. |


### PathfinderWaypoint

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `needs_destroy_to_reach` | required | `boolean` | `true` if the path from the previous waypoint to this one goes through an entity that must be destro |
| `position` | required | `MapPosition` | The position of the waypoint on its surface. |


### WalkToModifier

*Walking destination modifier with tolerance and snapping parameters.*


---

## Events

Register handlers with `script.on_event(defines.events.X, handler)`.

The handler receives a single event table argument.

### on_built_entity

Called when player builds something.

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `consumed_items` | required | `LuaInventory` | A temporary inventory containing all items that the game used to build the entity. This inventory is |
| `entity` | required | `LuaEntity` | The entity that was built. |
| `name` | required | `defines.events` | Identifier of the event |
| `player_index` | required | `uint32` | The player who did the building. |
| `tags` | optional | `Tags` | The tags associated with this entity if any. |
| `tick` | required | `uint32` | Tick the event was generated. |

### on_chunk_charted

Called when a chunk is charted or re-charted.

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `area` | required | `BoundingBox` | Area of the chunk. |
| `force` | required | `LuaForce` |  |
| `name` | required | `defines.events` | Identifier of the event |
| `position` | required | `ChunkPosition` |  |
| `surface_index` | required | `uint32` |  |
| `tick` | required | `uint32` | Tick the event was generated. |

### on_console_chat

Called when a message is sent to the in-game console, either by a player or through the server interface. This event only fires for plain messages, not for any commands including `/shout` or `/whisper

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `message` | required | `string` | The chat message that was sent. |
| `name` | required | `defines.events` | Identifier of the event |
| `player_index` | optional | `uint32` | The player doing the chatting, if any. |
| `tick` | required | `uint32` | Tick the event was generated. |

### on_entity_died

Called when an entity dies.

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `cause` | optional | `LuaEntity` | The entity that did the killing if available. |
| `damage_type` | optional | `LuaDamagePrototype` | The damage type if any. |
| `entity` | required | `LuaEntity` | The entity that died. |
| `force` | optional | `LuaForce` | The force that did the killing if any. |
| `loot` | required | `LuaInventory` | The loot generated by this entity if any. |
| `name` | required | `defines.events` | Identifier of the event |
| `tick` | required | `uint32` | Tick the event was generated. |

### on_gui_click

Called when LuaGuiElementLuaGuiElement is clicked.

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `alt` | required | `boolean` | If alt was pressed. |
| `button` | required | `defines.mouse_button_type` | The mouse button used if any. |
| `control` | required | `boolean` | If control was pressed. |
| `cursor_display_location` | required | `GuiLocation` | The display location of the player's cursor. |
| `element` | required | `LuaGuiElement` | The clicked element. |
| `name` | required | `defines.events` | Identifier of the event |
| `player_index` | required | `uint32` | The player who did the clicking. |
| `shift` | required | `boolean` | If shift was pressed. |
| `tick` | required | `uint32` | Tick the event was generated. |

### on_gui_confirmed

Called when a LuaGuiElementLuaGuiElement is confirmed, for example by pressing Enter in a textfield.

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `alt` | required | `boolean` | If alt was pressed. |
| `control` | required | `boolean` | If control was pressed. |
| `element` | required | `LuaGuiElement` | The confirmed element. |
| `name` | required | `defines.events` | Identifier of the event |
| `player_index` | required | `uint32` | The player who did the confirming. |
| `shift` | required | `boolean` | If shift was pressed. |
| `tick` | required | `uint32` | Tick the event was generated. |

### on_player_changed_position

Called when the tile position a player is located at changes.

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `name` | required | `defines.events` | Identifier of the event |
| `player_index` | required | `uint32` | The player. |
| `tick` | required | `uint32` | Tick the event was generated. |

### on_player_crafted_item

Called when the player finishes crafting an item. This event fires just before the results are inserted into the player's inventory, not when the crafting is queued see on_pre_player_crafted_itemon_pr

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `item_stack` | required | `LuaItemStack` | The item that has been crafted. |
| `name` | required | `defines.events` | Identifier of the event |
| `player_index` | required | `uint32` | The player doing the crafting. |
| `recipe` | required | `LuaRecipe` | The recipe used to craft this item. |
| `tick` | required | `uint32` | Tick the event was generated. |

### on_player_joined_game

Called after a player joins the game. This is not called when loading a save file in singleplayer, as the player doesn't actually leave the game, and the save is just on pause until they rejoin.

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `name` | required | `defines.events` | Identifier of the event |
| `player_index` | required | `uint32` |  |
| `tick` | required | `uint32` | Tick the event was generated. |

### on_player_left_game

Called after a player leaves the game. This is not called when closing a save file in singleplayer, as the player doesn't actually leave the game, and the save is just on pause until they rejoin.

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `name` | required | `defines.events` | Identifier of the event |
| `player_index` | required | `uint32` |  |
| `reason` | required | `defines.disconnect_reason` |  |
| `tick` | required | `uint32` | Tick the event was generated. |

### on_player_main_inventory_changed

Called after a players main inventory changed in some way.

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `name` | required | `defines.events` | Identifier of the event |
| `player_index` | required | `uint32` |  |
| `tick` | required | `uint32` | Tick the event was generated. |

### on_player_mined_entity

Called after the results of an entity being mined are collected just before the entity is destroyed. After this event any items in the buffer will be transferred into the player as if they came from m

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `buffer` | required | `LuaInventory` | The temporary inventory that holds the result of mining the entity. |
| `entity` | required | `LuaEntity` | The entity that has been mined. |
| `name` | required | `defines.events` | Identifier of the event |
| `player_index` | required | `uint32` | The index of the player doing the mining. |
| `tick` | required | `uint32` | Tick the event was generated. |

### on_pre_player_died

Called before a players dies.

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `cause` | optional | `LuaEntity` |  |
| `name` | required | `defines.events` | Identifier of the event |
| `player_index` | required | `uint32` |  |
| `tick` | required | `uint32` | Tick the event was generated. |

### on_research_finished

Called when a research finishes.

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `by_script` | required | `boolean` | If the technology was researched by script. |
| `name` | required | `defines.events` | Identifier of the event |
| `research` | required | `LuaTechnology` | The researched technology |
| `tick` | required | `uint32` | Tick the event was generated. |

### on_tick

It is fired once every tick. Since this event is fired every tick, its handler shouldn't include performance heavy code.

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `name` | required | `defines.events` | Identifier of the event |
| `tick` | required | `uint32` | Tick the event was generated. |

### All Events (reference)

<details>
<summary>Click to expand — all 219 events</summary>

| Event | Description |
|-------|-------------|
| `CustomInputEvent` | Called when a CustomInputPrototypeCustomInputPrototype is activated. |
| `on_achievement_gained` | Called when an achievement is gained. |
| `on_ai_command_completed` | Called when a unit/group completes a command. |
| `on_area_cloned` | Called when an area of the map is cloned. |
| `on_biter_base_built` | Called when a biter migration builds a base. |
| `on_brush_cloned` | Called when a set of positions on the map is cloned. |
| `on_build_base_arrived` | Called when a defines.command.build_basedefines.command.build_base command reaches its destination,  |
| `on_built_entity` | Called when player builds something. |
| `on_cancelled_deconstruction` | Called when the deconstruction of an entity is canceled. |
| `on_cancelled_upgrade` | Called when the upgrade of an entity is canceled. |
| `on_cargo_pod_delivered_cargo` | Called after a cargo pod has delivered its cargo. |
| `on_cargo_pod_finished_ascending` | Called when a cargo pod departs a surface. |
| `on_cargo_pod_finished_descending` | Called when a cargo pods lands on a surface, either at a station or on the ground. |
| `on_cargo_pod_started_ascending` | Called when a cargo pod departs from a space platform hub or by another method not attached to a roc |
| `on_character_corpse_expired` | Called when a character corpse expires due to timeout or all of the items being removed from it. |
| `on_chart_tag_added` | Called when a chart tag is created. |
| `on_chart_tag_modified` | Called when a chart tag is modified by a player or by script. |
| `on_chart_tag_removed` | Called just before a chart tag is deleted. |
| `on_chunk_charted` | Called when a chunk is charted or re-charted. |
| `on_chunk_deleted` | Called when one or more chunks are deleted using LuaSurface::delete_chunkLuaSurface::delete_chunk. |
| `on_chunk_generated` | Called when a chunk is generated. |
| `on_combat_robot_expired` | Called when a combat robot expires through a lack of energy, or timeout. |
| `on_console_chat` | Called when a message is sent to the in-game console, either by a player or through the server inter |
| `on_console_command` | Called when someone enters a command-like message regardless of it being a valid command. |
| `on_cutscene_cancelled` | Called when a cutscene is cancelled by the player or by script. |
| `on_cutscene_finished` | Called when a cutscene finishes naturally was not cancelled. |
| `on_cutscene_started` | Called when a cutscene starts. |
| `on_cutscene_waypoint_reached` | Called when a cutscene is playing, each time it reaches a waypoint in that cutscene. |
| `on_entity_cloned` | Called when an entity is cloned. The filter applies to the source entity. |
| `on_entity_color_changed` | Called after an entity has been recolored either by the player or through script. |
| `on_entity_damaged` | Called when an entity is damaged. This is not called when an entities health is set directly by anot |
| `on_entity_died` | Called when an entity dies. |
| `on_entity_logistic_slot_changed` | Called when one of an entity's logistic slots changes. |
| `on_entity_renamed` | Called after an entity has been renamed either by the player or through script. |
| `on_entity_settings_pasted` | Called after entity copy-paste is done. |
| `on_entity_spawned` | Called when an entity is spawned by a EnemySpawner |
| `on_equipment_inserted` | Called after equipment is inserted into an equipment grid. |
| `on_equipment_removed` | Called after equipment is removed from an equipment grid. |
| `on_force_cease_fire_changed` | Called when the a forces cease fire values change. |
| `on_force_created` | Called when a new force is created using `game.create_force` |
| `on_force_friends_changed` | Called when the a forces friends change. |
| `on_force_reset` | Called when LuaForce::resetLuaForce::reset is finished. |
| `on_forces_merged` | Called after two forces have been merged using `game.merge_forces`. |
| `on_forces_merging` | Called when two forces are about to be merged using `game.merge_forces`. |
| `on_game_created_from_scenario` | Called when a game is created from a scenario. This is fired for every mod, even when the scenario's |
| `on_gui_checked_state_changed` | Called when LuaGuiElementLuaGuiElement checked state is changed related to checkboxes and radio butt |
| `on_gui_click` | Called when LuaGuiElementLuaGuiElement is clicked. |
| `on_gui_closed` | Called when the player closes the GUI they have open. |
| `on_gui_confirmed` | Called when a LuaGuiElementLuaGuiElement is confirmed, for example by pressing Enter in a textfield. |
| `on_gui_elem_changed` | Called when LuaGuiElementLuaGuiElement element value is changed related to choose element buttons. |
| `on_gui_hover` | Called when LuaGuiElementLuaGuiElement is hovered by the mouse. |
| `on_gui_leave` | Called when the player's cursor leaves a LuaGuiElementLuaGuiElement that was previously hovered. |
| `on_gui_location_changed` | Called when LuaGuiElementLuaGuiElement element location is changed related to frames in `player.gui. |
| `on_gui_opened` | Called when the player opens a GUI. |
| `on_gui_selected_tab_changed` | Called when LuaGuiElementLuaGuiElement selected tab is changed related to tabbed-panes. |
| `on_gui_selection_state_changed` | Called when LuaGuiElementLuaGuiElement selection state is changed related to drop-downs and listboxe |
| `on_gui_switch_state_changed` | Called when LuaGuiElementLuaGuiElement switch state is changed related to switches. |
| `on_gui_text_changed` | Called when LuaGuiElementLuaGuiElement text is changed by the player. |
| `on_gui_value_changed` | Called when LuaGuiElementLuaGuiElement slider value is changed related to the slider element. |
| `on_land_mine_armed` | Called when a land mine is armed. |
| `on_lua_shortcut` | Called when a custom Lua shortcutShortcutPrototype is pressed. |
| `on_marked_for_deconstruction` | Called when an entity is marked for deconstruction with the Deconstruction planner or via script. |
| `on_marked_for_upgrade` | Called when an entity is marked for upgrade with the upgrade planner or via script. |
| `on_market_item_purchased` | Called after a player purchases some offer from a `market` entity. |
| `on_mod_item_opened` | Called when the player uses the 'Open item GUI' control on an item defined with the 'mod-openable' f |
| `on_multiplayer_init` | Called when LuaGameScript::is_multiplayerLuaGameScript::is_multiplayer changes to true. May also be  |
| `on_object_destroyed` | Called after an object is destroyed which was registered with LuaBootstrap::register_on_object_destr |
| `on_permission_group_added` | Called directly after a permission group is added. |
| `on_permission_group_deleted` | Called directly after a permission group is deleted. |
| `on_permission_group_edited` | Called directly after a permission group is edited in some way. |
| `on_permission_string_imported` | Called directly after a permission string is imported. |
| `on_picked_up_item` | Called when a player picks up an item. |
| `on_player_alt_reverse_selected_area` | Called after a player alt-reverse-selects an area with a selection-tool item. |
| `on_player_alt_selected_area` | Called after a player alt-selects an area with a selection-tool item. |
| `on_player_ammo_inventory_changed` | Called after a players ammo inventory changed in some way. |
| `on_player_armor_inventory_changed` | Called after a players armor inventory changed in some way. |
| `on_player_banned` | Called when a player is banned. |
| `on_player_built_tile` | Called after a player builds tiles. |
| `on_player_cancelled_crafting` | Called when a player cancels crafting. |
| `on_player_changed_force` | Called after a player changes forces. |
| `on_player_changed_position` | Called when the tile position a player is located at changes. |
| `on_player_changed_surface` | Called after a player changes surfaces. |
| `on_player_cheat_mode_disabled` | Called when cheat mode is disabled on a player. |
| `on_player_cheat_mode_enabled` | Called when cheat mode is enabled on a player. |
| `on_player_clicked_gps_tag` | Called when a player clicks a gps tag |
| `on_player_configured_blueprint` | Called when a player clicks the "confirm" button in the configure Blueprint GUI. |
| `on_player_controller_changed` | Called after a player changes controller types. |
| `on_player_crafted_item` | Called when the player finishes crafting an item. This event fires just before the results are inser |
| `on_player_created` | Called after the player was created. |
| `on_player_cursor_stack_changed` | Called after a player's cursor stackLuaControl::cursor_stack changed in some way. |
| `on_player_deconstructed_area` | Called when a player selects an area with a deconstruction planner. |
| `on_player_demoted` | Called when a player is demoted. |
| `on_player_died` | Called after a player dies. |
| `on_player_display_density_scale_changed` | Called when the display density scale changes for a given player. The display density scale is the s |
| `on_player_display_resolution_changed` | Called when the display resolution changes for a given player. |
| `on_player_display_scale_changed` | Called when the display scale changes for a given player. |
| `on_player_driving_changed_state` | Called when the player's driving state has changed, meaning a player has either entered or left a ve |
| `on_player_dropped_item` | Called when a player drops an item on the ground. |
| `on_player_dropped_item_into_entity` | Called when a player drops a single item into an entity. |
| `on_player_fast_transferred` | Called when a player fast-transfers something to or from an entity. |
| `on_player_flipped_entity` | Called when the player flips an entity. This event is only fired when the entity actually changes it |
| `on_player_flushed_fluid` | Called after player flushed fluid |
| `on_player_gun_inventory_changed` | Called after a players gun inventory changed in some way. |
| `on_player_input_method_changed` | Called when a player's input method changes. See LuaPlayer::input_methodLuaPlayer::input_method. |
| `on_player_joined_game` | Called after a player joins the game. This is not called when loading a save file in singleplayer, a |
| `on_player_kicked` | Called when a player is kicked. |
| `on_player_left_game` | Called after a player leaves the game. This is not called when closing a save file in singleplayer,  |
| `on_player_locale_changed` | Called when a player's active locale changes. See LuaPlayer::localeLuaPlayer::locale. |
| `on_player_main_inventory_changed` | Called after a players main inventory changed in some way. |
| `on_player_mined_entity` | Called after the results of an entity being mined are collected just before the entity is destroyed. |
| `on_player_mined_item` | Called when the player mines something. |
| `on_player_mined_tile` | Called after a player mines tiles. |
| `on_player_muted` | Called when a player is muted. |
| `on_player_pipette` | Called when a player invokes the "smart pipette" over an entity. |
| `on_player_placed_equipment` | Called after the player puts equipment in an equipment grid |
| `on_player_promoted` | Called when a player is promoted. |
| `on_player_removed` | Called when a player is removed deleted from the game. This is markedly different from a player temp |
| `on_player_removed_equipment` | Called after the player removes equipment from an equipment grid |
| `on_player_repaired_entity` | Called when a player repairs an entity. |
| `on_player_respawned` | Called after a player respawns. |
| `on_player_reverse_selected_area` | Called after a player reverse-selects an area with a selection-tool item. |
| `on_player_rotated_entity` | Called when the player rotates an entity. This event is only fired when the entity actually changes  |
| `on_player_selected_area` | Called after a player selects an area with a selection-tool item. |
| `on_player_set_quick_bar_slot` | Called when a player sets a quickbar slot to anything new value, or set to empty. |
| `on_player_setup_blueprint` | Called when a player selects an area with a blueprint. |
| `on_player_toggled_alt_mode` | Called when a player toggles alt mode, also known as "show entity info". |
| `on_player_toggled_map_editor` | Called when a player toggles the map editor on or off. |
| `on_player_trash_inventory_changed` | Called after a players trash inventory changed in some way. |
| `on_player_unbanned` | Called when a player is un-banned. |
| `on_player_unmuted` | Called when a player is unmuted. |
| `on_player_used_capsule` | Called when a player uses a capsule that results in some game action. |
| `on_player_used_spidertron_remote` | Called when a player uses spidertron remote to send all selected units to a given position |
| `on_post_entity_died` | Called after an entity dies. |
| `on_post_segmented_unit_died` | Called after a segmented unit dies. |
| `on_pre_build` | Called when players uses an item to build something. Called before on_built_entityon_built_entity. |
| `on_pre_chunk_deleted` | Called before one or more chunks are deleted using LuaSurface::delete_chunkLuaSurface::delete_chunk. |
| `on_pre_entity_settings_pasted` | Called before entity copy-paste is done. |
| `on_pre_ghost_deconstructed` | Called before a ghost entity is destroyed as a result of being marked for deconstruction. |
| `on_pre_ghost_upgraded` | Called before a ghost entity is upgraded. |
| `on_pre_permission_group_deleted` | Called directly before a permission group is deleted. |
| `on_pre_permission_string_imported` | Called directly before a permission string is imported. |
| `on_pre_player_crafted_item` | Called when a player queues something to be crafted. |
| `on_pre_player_died` | Called before a players dies. |
| `on_pre_player_left_game` | Called before a player leaves the game. |
| `on_pre_player_mined_item` | Called when the player completes a mining action, but before the entity is potentially removed from  |
| `on_pre_player_removed` | Called before a player is removed deleted from the game. This is markedly different from a player te |
| `on_pre_player_toggled_map_editor` | Called before a player toggles the map editor on or off. |
| `on_pre_robot_exploded_cliff` | Called directly before a robot explodes cliffs. |
| `on_pre_scenario_finished` | Called just before the scenario finishes. |
| `on_pre_script_inventory_resized` | Called just before a script inventory is resized. |
| `on_pre_surface_cleared` | Called just before a surface is cleared all entities removed and all chunks deleted. |
| `on_pre_surface_deleted` | Called just before a surface is deleted. |
| `on_redo_applied` | Called when the player triggers "redo". |
| `on_research_cancelled` | Called when research is cancelled. |
| `on_research_finished` | Called when a research finishes. |
| `on_research_moved` | Called when research is moved forwards or backwards in the research queue. |
| `on_research_queued` | Called when research is queued. |
| `on_research_reversed` | Called when a research is reversed unresearched. |
| `on_research_started` | Called when a technology research starts. |
| `on_resource_depleted` | Called when a resource entity reaches 0 or its minimum yield for infinite resources. |
| `on_robot_built_entity` | Called when a construction robot builds an entity. |
| `on_robot_built_tile` | Called after a robot builds tiles. |
| `on_robot_exploded_cliff` | Called directly after a robot explodes cliffs. |
| `on_robot_mined` | Called when a robot mines an entity. |
| `on_robot_mined_entity` | Called after the results of an entity being mined are collected just before the entity is destroyed. |
| `on_robot_mined_tile` | Called after a robot mines tiles. |
| `on_robot_pre_mined` | Called before a robot mines an entity. |
| `on_rocket_launch_ordered` | Called when a rocket silo is ordered to be launched. |
| `on_rocket_launched` | Called when a rocket finishes ascending. Triggers listening for finished rocket launch past 2.0 have |
| `on_runtime_mod_setting_changed` | Called when a runtime mod setting is changed by a player. |
| `on_script_inventory_resized` | Called just after a script inventory is resized. |
| `on_script_path_request_finished` | Called when a LuaSurface::request_pathLuaSurface::request_path call completes. |
| `on_script_trigger_effect` | Called when a script trigger effect is triggered. |
| `on_sector_scanned` | Called when an entity of type `radar` finishes scanning a sector. |
| `on_segment_entity_created` | Called when an individual segment of a SegmentedUnit is created. |
| `on_segmented_unit_created` | Called when a segmented unit is created for any reason. |
| `on_segmented_unit_damaged` | Called when a segmented unit is damaged. This is not called when a segmented unit's health is set di |
| `on_segmented_unit_died` | Called when a segmented unit dies. |
| `on_selected_entity_changed` | Called after the selected entity changes for a given player. |
| `on_singleplayer_init` | Called when LuaGameScript::is_multiplayerLuaGameScript::is_multiplayer changes to false. May also be |
| `on_space_platform_built_entity` | Called when a space platform builds an entity. |
| `on_space_platform_built_tile` | Called after a space platform builds tiles. |
| `on_space_platform_changed_state` | Called when a space platform changes state |
| `on_space_platform_mined_entity` | Called after the results of an entity being mined are collected just before the entity is destroyed. |
| `on_space_platform_mined_item` | Called when a platform mines an entity. |
| `on_space_platform_mined_tile` | Called after a platform mines tiles. |
| `on_space_platform_pre_mined` | Called before a platform mines an entity. |
| `on_spider_command_completed` | Called when a spider finishes moving to its autopilot position. |
| `on_string_translated` | Called when a translation request generated through LuaPlayer::request_translationLuaPlayer::request |
| `on_surface_cleared` | Called just after a surface is cleared all entities removed and all chunks deleted. |
| `on_surface_created` | Called when a surface is created. |
| `on_surface_deleted` | Called after a surface is deleted. |
| `on_surface_imported` | Called after a surface is imported via the map editor. |
| `on_surface_renamed` | Called when a surface is renamed. |
| `on_technology_effects_reset` | Called when LuaForce::reset_technology_effectsLuaForce::reset_technology_effects is finished. |
| `on_territory_created` | Called when a territory is created for any reason. |
| `on_territory_destroyed` | Called when a territory is destroyed from a surface. |
| `on_tick` | It is fired once every tick. Since this event is fired every tick, its handler shouldn't include per |
| `on_tower_mined_plant` | Called after the results of an entity being mined are collected just before the entity is destroyed. |
| `on_tower_planted_seed` | Called before an agricultural tower plants a seed. |
| `on_tower_pre_mined_plant` | Called before an agricultural tower mines a plant. |
| `on_train_changed_state` | Called when a train changes state started to stopped and vice versa |
| `on_train_created` | Called when a new train is created either through disconnecting/connecting an existing one or buildi |
| `on_train_schedule_changed` | Called when a trains schedule is changed either by the player or through script. |
| `on_trigger_created_entity` | Called when an entity with a trigger prototype such as capsules create an entity AND that trigger pr |
| `on_trigger_fired_artillery` | Called when an entity with a trigger prototype such as capsules fire an artillery projectile AND tha |
| `on_udp_packet_received` | Called when new packets are processed by LuaHelpers::recv_udpLuaHelpers::recv_udp. |
| `on_undo_applied` | Called when the player triggers "undo". |
| `on_unit_added_to_group` | Called when a unit is added to a unit group. |
| `on_unit_group_created` | Called when a new unit group is created, before any members are added to it. |
| `on_unit_group_finished_gathering` | Called when a unit group finishes gathering and starts executing its command. |
| `on_unit_removed_from_group` | Called when a unit is removed from a unit group. |
| `on_worker_robot_expired` | Called when a worker construction or logistic robot expires through a lack of energy. |
| `script_raised_built` | A static event mods can use to tell other mods they built something by script. This event is only ra |
| `script_raised_destroy` | A static event mods can use to tell other mods they destroyed something by script. This event is onl |
| `script_raised_destroy_segmented_unit` | A static event that mods can use to tell other mods they destroyed a segmented unit by script. This  |
| `script_raised_revive` | A static event mods can use to tell other mods they revived something by script. This event is only  |
| `script_raised_set_tiles` | A static event mods can use to tell other mods they changed tiles on a surface by script. This event |
| `script_raised_teleported` | A static event mods can use to tell other mods they teleported something by script. This event is on |

</details>

---

## Common Patterns

Practical Lua examples used throughout this project's RCON scripts.

### Get Player Position

```lua
local p = game.connected_players[1]
local pos = p.position
rcon.print(game.table_to_json({x = pos.x, y = pos.y}))
```

### Find Entities Near a Position

```lua
local player = game.connected_players[1]
local surface = player.surface
local pos = player.position
local entities = surface.find_entities_filtered{
  position = pos,
  radius = 32,
  type = "resource"  -- optional: filter by type
}
local result = {}
for _, e in pairs(entities) do
  table.insert(result, {
    name = e.name,
    type = e.type,
    x = e.position.x,
    y = e.position.y
  })
end
rcon.print(game.table_to_json(result))
```

### Get Player Inventory Contents

```lua
local player = game.connected_players[1]
local inv = player.get_main_inventory()
local contents = inv.get_contents()
rcon.print(game.table_to_json(contents))
```

### Insert Items into Inventory

```lua
local player = game.connected_players[1]
local inv = player.get_main_inventory()
local inserted = inv.insert{name = "iron-plate", count = 50}
rcon.print(tostring(inserted))
```

### Get/Set Research

```lua
local force = game.connected_players[1].force
-- Queue a technology for research
local ok = force.add_research("automation")
-- Get current research progress
local current = force.current_research
if current then
  rcon.print(game.table_to_json({
    name = current.name,
    progress = force.research_progress
  }))
end
-- Get all technologies
local techs = {}
for name, tech in pairs(force.technologies) do
  table.insert(techs, {name=name, researched=tech.researched, enabled=tech.enabled})
end
rcon.print(game.table_to_json(techs))
```

### Walk Player to Position (Event-based)

```lua
-- Register walking handler
script.on_event(defines.events.on_tick, function(event)
  local player = game.connected_players[1]
  local target = storage.walk_target  -- {x=100, y=200}
  if not target then return end
  local pos = player.position
  local dx = target.x - pos.x
  local dy = target.y - pos.y
  local dist = math.sqrt(dx*dx + dy*dy)
  if dist < 0.5 then
    player.walking_state = {walking=false, direction=defines.direction.north}
    storage.walk_target = nil
    return
  end
  -- Calculate 8-directional movement
  local angle = math.atan2(dy, dx)
  -- ... map angle to defines.direction value ...
  player.walking_state = {walking=true, direction=some_direction}
end)
```

### Place an Entity

```lua
local player = game.connected_players[1]
local surface = player.surface
local pos = {x=10, y=20}
local name = "iron-chest"
local dir = defines.direction.north
if surface.can_place_entity{name=name, position=pos, force=player.force, direction=dir} then
  local entity = surface.create_entity{
    name = name,
    position = pos,
    direction = dir,
    force = player.force
  }
  rcon.print(game.table_to_json({success=true, unit_number=entity.unit_number}))
else
  rcon.print('{"success":false,"error":"cannot_place"}')
end
```

### Mine an Entity

```lua
local player = game.connected_players[1]
local surface = player.surface
-- Find entity at position
local entities = surface.find_entities_filtered{position={x=10,y=20}, radius=1}
if #entities > 0 then
  local entity = entities[1]
  if player.can_reach_entity(entity) then
    player.mine_entity(entity, true)  -- true = raise event
    rcon.print('{"success":true}')
  else
    rcon.print('{"success":false,"error":"out_of_reach"}')
  end
end
```

### Check Energy Production Statistics

```lua
local force = game.connected_players[1].force
local surface = game.surfaces["nauvis"]
local stats = force.get_electric_network_stats(surface)
-- Production in the last 5 seconds
local produced = stats.get_flow_count{
  name = "steam",
  input = true,
  precision_index = defines.flow_precision_index.five_seconds
}
rcon.print(tostring(produced))
```

### Blueprint Operations

```lua
local player = game.connected_players[1]
local surface = player.surface
-- Create a blueprint record (in game blueprints shelf)
local record = game.create_blueprint{
  player = player,
  surface = surface,
  area = {{x=-5, y=-5}, {x=5, y=5}},
  always_include_tiles = false
}
if record then
  local entities = record.get_blueprint_entities()
  rcon.print(game.table_to_json({entity_count = #(entities or {})}))
end
```

### Get Entity Burner Fuel

```lua
local entities = game.surfaces["nauvis"].find_entities_filtered{type="furnace"}
for _, e in pairs(entities) do
  if e.burner then
    local b = e.burner
    rcon.print(game.table_to_json({
      entity = e.name,
      heat = b.heat,
      heat_capacity = b.heat_capacity,
      remaining_fuel = b.remaining_burning_fuel,
      currently_burning = b.currently_burning and b.currently_burning.name or nil
    }))
  end
end
```

### Check Crafting Queue

```lua
local player = game.connected_players[1]
-- Begin crafting
local queued = player.begin_crafting{count=5, recipe="electronic-circuit"}
-- Get craftable count  
local craftable = player.get_craftable_count("electronic-circuit")
rcon.print(game.table_to_json({queued=queued, craftable=craftable}))
```

### Send Chat Message

```lua
game.print("[MCP] Hello from script!")
-- Or to a specific player:
game.connected_players[1].print("Private message")
```

### Access Recipes

```lua
local force = game.connected_players[1].force
local recipe = force.recipes["iron-gear-wheel"]
if recipe and recipe.enabled then
  local ingredients = {}
  for _, ing in pairs(recipe.ingredients) do
    table.insert(ingredients, {name=ing.name, amount=ing.amount, type=ing.type})
  end
  rcon.print(game.table_to_json({
    name = recipe.name,
    energy = recipe.energy,
    ingredients = ingredients
  }))
end
```

### Pathfinding / Request Path

```lua
local player = game.connected_players[1]
local surface = player.surface
local request_id = surface.request_path{
  bounding_box = {{-0.3, -0.3}, {0.3, 0.3}},
  collision_mask = player.character.prototype.collision_mask,
  start = player.position,
  goal = {x=50, y=50},
  force = player.force,
  radius = 1
}
-- Path result arrives via on_script_path_request_finished event
rcon.print(game.table_to_json({request_id=request_id}))
```

### List All Surfaces

```lua
local surfaces = {}
for name, surface in pairs(game.surfaces) do
  table.insert(surfaces, {name=name, index=surface.index})
end
rcon.print(game.table_to_json(surfaces))
```

### Get Tiles in Area

```lua
local surface = game.surfaces["nauvis"]
local tiles = surface.find_tiles_filtered{
  area = {{x=-10, y=-10}, {x=10, y=10}}
}
local tile_info = {}
for _, t in pairs(tiles) do
  table.insert(tile_info, {name=t.name, x=t.position.x, y=t.position.y})
end
rcon.print(game.table_to_json(tile_info))
```

---

## LuaBootstrap (script)

The global `script` object for event and mod lifecycle management.

| Method | Signature | Description |
|--------|-----------|-------------|
| `on_event` | `on_event(event: defines.events \| array[defines.events] \| string, handler: function \| nil, filters?: EventFilter)` | Register/unregister an event handler |
| `on_init` | `on_init(handler: function \| nil)` | Called once when a new game is created |
| `on_load` | `on_load(handler: function \| nil)` | Called when game is loaded |
| `on_configuration_changed` | `on_configuration_changed(handler: function \| nil)` | Called when mod configuration changes |
| `on_nth_tick` | `on_nth_tick(tick: uint32 \| dict \| nil, handler: function \| nil)` | Register handler for every Nth tick |
| `register_on_entity_destroyed` | `register_on_entity_destroyed(entity: LuaEntity) → uint64` | Get notified when entity is destroyed |
| `get_event_handler` | `get_event_handler(event: defines.events) → function` | Get registered handler for event |
| `get_event_order` | `get_event_order() → string` | Get current event order string |
| `raise_event` | `raise_event(event: defines.events, data: table)` | Raise event for all mods |
| `raise_bespoke_event` | `raise_bespoke_event(name: string, data: table)` | Raise a custom named event |
| `mod_name` | `[R] string` | This mod's name |
| `active_mods` | `[R] dict[string, string]` | Map of modname → version for all active mods |
| `object_name` | `[R] string` | Always `"LuaBootstrap"` |

---

*Generated from `LuaAPI/runtime-api.json` v2.0.76. For full details see the [online docs](https://lua-api.factorio.com/latest/) or browse [`LuaAPI/classes/`](LuaAPI/classes/).*

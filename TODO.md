# TODO: MCP Functions to Remove or Rework

Analysis of which MCP tools should be removed because they are too low-level, confuse the AI, or overlap with better alternatives.

---

## High Priority — Remove These Tools

### `InitializeChatListener` (ChatTools.cs)
**Reason: Confuses the AI — infrastructure concern, not a player action**
- Auto-called at startup by `RconConnectionService` (line 62 in `RconConnectionService.cs`)
- The AI has no reason to call this; it's already initialized before the AI connects
- If the listener is lost after game reload, the server should re-initialize automatically, not rely on the AI to know about it
- Exposing it leads to wasted calls or confusion about whether chat is "working"

### `GetBeltDirectionHelp` (BeltTools.cs)
**Reason: Static reference text, not a tool**
- Returns a hardcoded JSON document explaining belt direction mechanics
- This is documentation, not an action — it belongs in the system prompt (`PROMPT.md`) or in the `Description` attribute of `PlanBeltRoute`
- Having it as a callable tool teaches the AI to "ask for help" instead of just knowing the rules
- Move the content into `PROMPT.md` under the belt placement section

### `EstimateTravelTime` (UtilityTools.cs)
**Reason: Low-level, misleading output**
- Returns straight-line distance divided by movement speed — a trivial calculation
- Does NOT account for obstacles, water, pathfinding detours, or terrain
- The AI gets a falsely precise estimate that doesn't match actual travel
- Better approach: just call `WalkToPosition` and observe the real result
- If the AI needs to decide whether to walk, `CheckDistance` already tells it if it's in range

### `ScanTiles` (WorldTools.cs)
**Reason: Low-value, rarely actionable for AI**
- Returns terrain type distribution (grass, sand, water, dirt counts)
- The AI almost never needs to know "there are 15 grass tiles nearby"
- The one useful case (water detection) is better served by `FindBuildableArea` which directly checks if an area is clear for building
- Adds noise to the tool list without informing meaningful decisions

### `GetGameTick` (WaitTools.cs)
**Reason: Too low-level — raw tick numbers are meaningless to the AI**
- Returns the current Factorio game tick (an integer)
- The AI has no frame of reference for what tick 847293 means
- All useful timing is already handled by purpose-built wait tools (`WaitForCrafting`, `WaitForEntityStatus`, `WaitForEntityInventory`)
- If elapsed time measurement is needed, the wait tools already report duration

### `GetAvailableSlots` (EntityTools.cs)
**Reason: Too low-level — internal detail of placement workflow**
- Reports which tiles around an entity are free for adjacent placement
- This is an implementation detail that `PlaceInserter` and `InsertBetween` handle automatically
- The AI shouldn't need to manually query slot availability — the placement tools already validate positions
- Adds decision-making complexity without meaningful benefit

---

## Medium Priority — Consider Removing or Reworking

### `WaitForTicks` (WaitTools.cs)
**Reason: Low-level timing primitive; task-oriented waits are better**
- Waits for N game ticks (60 = 1 second at normal speed)
- The AI must know the tick-to-seconds conversion and guess how long things take
- Task-oriented alternatives are always better:
  - Smelting? Use `WaitForEntityInventory` to wait for output items
  - Crafting? Use `WaitForCrafting`
  - Entity startup? Use `WaitForEntityStatus`
- **Keep only if** there are genuine cases where no task-oriented wait exists

### `StopWalking` (MovementTools.cs)
**Reason: Edge case that confuses tool selection**
- Stops the player from walking mid-path
- `WalkToPosition` already handles stuck detection and timeouts
- The AI would need to be in a multi-step async workflow to use this, which is rare
- Creates confusion: "should I stop walking before doing something else?"
- **Keep only if** there are real scenarios where the AI needs to abort a walk

### `WhatAmILookingAt` (PerceptionTools.cs)
**Reason: Niche directional scan with limited AI utility**
- Performs a cone-shaped raycast in a compass direction
- `SummarizeArea` provides a more comprehensive omnidirectional overview
- The directional use case ("what's north of me?") rarely drives AI decisions
- Could be merged into `SummarizeArea` with an optional direction filter parameter
- **Keep only if** directional awareness is important for combat or exploration

### `ClearBuildingMemory` (BuildingTools.cs)
**Reason: Destructive operation the AI should rarely use**
- Wipes all tracked buildings from memory (not from the game world)
- No undo; the AI loses all spatial knowledge of its factory
- `ValidateBuildingMemory` is the safe alternative (prunes only stale entries)
- Risk: AI calls this thinking it's "cleaning up" and loses critical building locations
- **Rework**: Add a confirmation parameter or rename to make the destructive nature clearer

### `ExecuteLua` (LuaTools.cs)
**Reason: Dangerous escape hatch that bypasses safety constraints**
- Executes arbitrary Lua code on the Factorio server with no sandboxing
- Can corrupt game state, crash the server, or cause data loss
- Encourages the AI to bypass purpose-built tools with ad-hoc Lua
- The tool description includes warnings, but AI models tend to use the "easiest" path
- **Rework**: Consider gating behind an opt-in flag or requiring structured Lua templates instead of freeform code

### `GetReachableEntities` (UtilityTools.cs)
**Reason: Overlaps with `GetNearbyEntities` using a small radius**
- Returns entities within the player's reach/build range
- `GetNearbyEntities(radius: 6)` returns nearly the same result
- Having both creates confusion about which to call
- **Rework**: Add a `withinReach` boolean parameter to `GetNearbyEntities` instead

### `DropItems` (InventoryTools.cs)
**Reason: Niche edge case that confuses inventory management**
- Drops items on the ground at the player's position
- The AI rarely needs to discard items; it should craft, store, or use them
- Creates confusion: "should I drop excess items or find a chest?"
- Risk of items being lost on the ground with no way to track them
- **Keep only if** inventory-full situations genuinely require ground dropping as a strategy

---

## Low Priority — Minor Improvements

### `PlaceInserter` vs `InsertBetween` (EntityTools.cs)
**Not removal, but documentation improvement needed**
- `PlaceInserter`: Place inserter adjacent to ONE entity, specifying side + inbound/outbound
- `InsertBetween`: Place inserter between TWO entities automatically
- Both are useful but the AI may struggle to choose between them
- **Fix**: Add clearer descriptions explaining when to use each (single-target setup vs. connecting two machines)

### `PlanCraft` vs `CheckCraftFeasibility` (RecipeTools.cs)
**Not removal, but naming/description improvement needed**
- `CheckCraftFeasibility`: "Can I craft this right now with current inventory?"
- `PlanCraft`: "What's the full recipe tree to eventually craft this?"
- Names sound similar but answer different questions
- **Fix**: Rename `PlanCraft` to something like `GetCraftingTree` or `ExpandRecipeChain`

### `GetPowerNetworkTopology` (EnergyTools.cs)
**Not removal, but potential confusion with `GetElectricNetwork`**
- `GetElectricNetwork`: Power balance (production vs. consumption)
- `GetPowerNetworkTopology`: Pole connectivity graph
- Both return "electric network" info but answer different questions
- **Fix**: Clarify in descriptions — "power balance" vs. "pole wiring/coverage"

### `GetInventory` vs `GetInventorySummary` (InventoryTools.cs)
**Both are useful, but the distinction is subtle**
- `GetInventory`: Full detailed listing with slot info
- `GetInventorySummary`: Condensed name:count pairs (token-efficient)
- **Fix**: Make `GetInventorySummary` the default recommendation and note `GetInventory` is for detailed inspection

---

## Tools That Are Fine — Keep As-Is

These were analyzed and confirmed to be well-designed, non-overlapping, and useful:

- **All NavigationTools** (`MoveToEntity`, `MoveToResource`, `MoveToBuilding`) — excellent high-level convenience
- **All BatchTools** (`MineEntityMultiple`, `InspectEntityMultiple`, etc.) — critical for LLM efficiency
- **All GoalTools** — well-structured planning system
- **All ResearchTools** — clear and necessary
- **All TaskTools** (`GatherResource`, `RefuelEntity`, `Smelt`) — excellent high-level composition
- **Core WaitTools** (`WaitForCrafting`, `WaitForPosition`, `WaitForItemCount`, `WaitForEntityStatus`, `WaitForEntityInventory`) — essential synchronization
- **Core WorldTools** (`GetNearbyEntities`, `CheckDistance`, `ScanResources`) — fundamental awareness
- **Core EntityTools** (`PlaceEntity`, `PlaceEntitySmart`, `MineEntity`, `MineResource`, `RotateEntity`, `PreviewInserterPlacement`, `InsertBetween`) — well-designed
- **All TargetTools** (`FindNearest`, `FindBestResourcePatch`, `GetClosestBuildingOfType`) — complementary search tools
- **All BuildingTools** (except `ClearBuildingMemory`) — well-structured memory system
- **All InteractionTools** (`InsertItems`, `RemoveItems`, `InspectEntity`, `PickupItems`) — essential
- **Core InventoryTools** (`GetInventory`, `Craft`, `GetCraftingQueue`, `TransferAllItems`, `GetEntityInventory`, `EnsureItem`) — necessary
- **All FlowTools** (`GetFlowGraph`, `TraceItemFlow`, `PreviewBeltPlacement`) — advanced but justified
- **All EnergyTools** — complementary (balance vs. topology vs. per-entity)
- **All LogisticsTools** — clear and distinct
- **All TrainTools** — well-scoped
- **All CombatTools** — well-scoped
- **All BlueprintTools** — well-designed
- **StatusTools** (`GetFactoryStatus`) — excellent comprehensive snapshot
- **VisionTools** (`TakeScreenshot`) — unique capability
- **BeltTools** (`PlanBeltRoute`) — useful planning tool
- **ChatTools** (`GetChatMessages`, `SendChatMessage`) — essential communication
- **LuaTools** (`ReconnectRcon`) — necessary recovery tool

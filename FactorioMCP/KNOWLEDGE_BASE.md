# Factorio Knowledge Base

This document contains important game mechanics and patterns learned through testing and gameplay.

---

## Building Mechanics

### Burner Mining Drills

**Output Behavior:**
- Burner mining drills place mined resources directly onto the **output tile position**
- The output tile is on the side the drill is facing (e.g., south-facing drill outputs to its south side)
- Resources are placed as items on the ground at that tile position
- **You CANNOT place an inserter directly between a drill and a furnace** - the drill needs to output to a belt or container first
- The drill will show status "waiting_for_space_in_destination" if its output tile is blocked

**Correct Patterns:**
1. **Drill ? Belt ? Inserter ? Furnace**
   - Place drill on ore patch
   - Place a transport belt tile at the drill's output position (the tile it faces)
   - Place inserter to pick up from the belt and insert into furnace

2. **Drill ? Chest ? Inserter ? Furnace**
   - Place drill on ore patch
   - Place a chest at the drill's output position
   - Place inserter to pick up from chest and insert into furnace

**Incorrect Pattern:**
- ? Drill ? Inserter ? Furnace (inserter between them)
- Why it fails: The drill drops ore as ground items onto a tile. If an inserter occupies that tile, the inserter cannot pick up the ore being dropped there - it's looking for items at its pickup position, but the drill is dropping items "on" the inserter entity itself

### Entity Sizing
- Burner mining drills are **2×2** entities
- Stone furnaces are **2×2** entities
- Inserters are **1×1** entities
- Transport belts are **1×1** tiles

### Placement Rules
- **Resources (ore) do NOT block building placement** - you can build entities directly on top of ore patches
- **The player character BLOCKS placement** - if you're standing where you want to build, the placement will fail with "invalid_position"
- **Solution:** Walk away from the target position before placing entities
- Entities can only be placed if their collision box doesn't overlap with other entities (except resources)

---

## Spatial Planning

### Drill Placement
- Drills must be placed on resource patches (ore, coal, stone, etc.)
- The drill's output tile must be free (no entity blocking it initially)
- Leave space for the output mechanism (belt or chest)

### Common Layout
```
[Ore] [Ore] [Ore]
[Ore] [Drill] [Ore]    (2×2, facing east ?)
[Ore] [Ore] [Ore]
       ?
    [Belt] ? [Inserter] ? [Furnace]
```

---

## Lessons Learned

### 2026-04-04: Drill Output Mechanics
- **Attempted:** Placing inserters directly between drills and furnaces
- **Result:** Drills showed "waiting_for_space_in_destination", furnaces showed "no_ingredients"
- **Cause:** Inserters cannot pick up items being dropped onto their own tile position
- **Solution:** Always place a belt or chest at the drill's output tile, then use an inserter to transfer from there

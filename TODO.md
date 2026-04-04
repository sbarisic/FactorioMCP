# TODO — Bug List

## BUG-1: `int.Parse()` outside try-catch in `RconConnectionService` crashes app on bad config
**File:** `FactorioMCP/Services/RconConnectionService.cs` — line 26  
**Severity:** High  

`int.Parse(configuration["FACTORIO_RCON_PORT"] ?? "27015")` executes before the retry
`try/catch` block that starts on line 32. If `FACTORIO_RCON_PORT` is set to any non-numeric
value the app throws an unhandled `FormatException` on startup instead of logging a useful
error or falling back to the default port.

**Fix:** Use `int.TryParse()` with a fallback, or move the port parsing inside the try block.

---

## BUG-2: Nullable `_rcon` used without null-check in `BuildingMemoryService.ValidateBuildingsAsync`
**File:** `FactorioMCP/Services/BuildingMemoryService.cs` — line 495  
**Severity:** High  

`_rcon` is declared `private readonly RconClient? _rcon` (line 37) and the internal
constructor (line 47) explicitly allows `rcon = null`. However,
`ValidateBuildingsAsync` calls `await _rcon.ExecuteLuaAsync(...)` unconditionally,
so any code path that reaches validation with a null `_rcon` will throw a
`NullReferenceException`.

**Fix:** Guard with `if (_rcon is null) return Respond(new { Status = "error", Error = "no_rcon" });`
at the top of the method.

---

## BUG-3: Any RCON error response during building validation silently deletes all tracked buildings
**File:** `FactorioMCP/Services/BuildingMemoryService.cs` — lines 561–574 (`ParseValidationResponse`)  
**Severity:** Critical  

`ParseValidationResponse` treats an empty/whitespace response as "all false" and returns
an all-false array (line 563–564). If the RCON call returns a Lua error string such as
`LuaError: attempt to index …` the response is not empty/whitespace, so the code calls
`Split(',')` on it. The first element of the split will not equal `"1"`, so `results[0]`
becomes `false`. All remaining indices are also `false` (default value). The caller then
removes every building with a `false` result, causing **complete data loss** of all tracked
buildings on any transient RCON / Lua error.

**Fix:** Validate that each part of the response is strictly `"0"` or `"1"` before treating
it as a valid result. Return an error (rather than all-false) when the response does not
match the expected CSV format.

---

## BUG-4: Item removed from player inventory before `create_entity` return value is checked
**File:** `FactorioMCP/Services/FactorioService.Entity.cs` — lines 43–44  
**Severity:** Medium  

In `PlaceEntityAsync` the Lua script calls `player.remove_item{…}` on line 43 and
immediately follows with `surface.create_entity{…}` on line 44 without storing or
checking the return value. `surface.create_entity` returns `nil` on failure. If it fails
(e.g. another entity placed the same tile between the `can_place_entity` check and the
actual placement), the item is already removed from inventory but no entity is created —
the item is permanently lost with no error reported to the caller.

**Fix:** Store the result of `create_entity` and, if it is `nil`, restore the item via
`player.insert{…}` and return an error JSON.

---

## BUG-5: Unchecked `GetProperty()` calls in `MiningService.MineResourceAsync` throw on malformed JSON
**File:** `FactorioMCP/Services/MiningService.cs` — lines 151–152, 170–171, 175, 184  
**Severity:** Medium  

After the `success` property is checked with `TryGetProperty`, the code accesses several
other properties directly with `GetProperty(…)`:

```
var entityName = startRoot.GetProperty("entity").GetString()!;   // line 151
var initialAmount = startRoot.GetProperty("amount").GetInt32();   // line 152
totalMined = statusRoot.GetProperty("mined").GetInt32();          // line 170
depleted   = statusRoot.GetProperty("depleted").GetBoolean();     // line 171
remaining  = statusRoot.GetProperty("remaining").GetInt32();      // line 175
var isMining = statusRoot.GetProperty("is_mining").GetBoolean();  // line 184
```

`GetProperty` throws `KeyNotFoundException` if the property is absent, and
`GetString()!` suppresses the null warning while the underlying call can return null
(causing `InvalidOperationException`) if the JSON value is `null`. Any Lua-side error
or partial response crashes the entire `MineResourceAsync` method with an unhandled
exception instead of returning a structured error string.

**Fix:** Use `TryGetProperty` for each access, or wrap the block in a try/catch and
return a structured error JSON on failure.

---

## BUG-6: Broken XML documentation comment on `InsertItemsAsync`
**File:** `FactorioMCP/Services/FactorioService.Entity.cs` — lines 147–149  
**Severity:** Low  

The XML doc block for `InsertItemsAsync` is missing its opening `<summary>` tag:

```csharp
    /// Supports specifying the target inventory slot (fuel, input, output, etc.).
    /// Validates proximity before interacting.
    /// </summary>
```

The `<summary>` open tag is absent, making this malformed XML. Build tooling that
generates API docs (e.g. `dotnet doc`, IDE tooltips) will either omit or corrupt the
documentation for this method.

**Fix:** Add the missing `/// <summary>` line before the description text.

---

## BUG-7: `WaitForCraftingAsync` empty-queue check fails if Lua returns whitespace inside the array
**File:** `FactorioMCP/Services/FactorioService.Wait.cs` — line 24  
**Severity:** Low  

The crafting-complete check is:

```csharp
if (TryParseJsonArray(result, "queue", out var queue) && queue == "[]")
```

`queue` is the raw extracted substring. If the Lua script ever returns `{"queue":[ ]}`
(with any whitespace inside the brackets) the string equality `queue == "[]"` fails and
the poll loop never terminates until timeout, even though the queue is actually empty.

**Fix:** Trim the extracted value or compare after normalising whitespace, e.g.
`queue.Trim() == "[]"` or check `queue.Length == 2` (i.e. only `[` and `]`).

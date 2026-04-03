# Contributing to FactorioMCP

## Complexity Points (CPX)

Tasks are rated on a 1–5 scale:

| CPX | Scope |
|-----|-------|
| 1 | Single file component |
| 2 | Single file component, possible small changes in other files |
| 3 | Single file component with multiple dependencies, no architecture changes |
| 4 | Multi file component with significant logic, possible minor architecture changes |
| 5 | Large feature spanning multiple components and subsystems, major architecture changes |

## TODO Workflow

1. **Triage first** — Handle the Uncategorized section in `TODO.md`. If similar issues already exist, increase their priority instead of adding duplicates. Categorize all at once.
2. **Bugs next** — When Uncategorized is empty, fix Active Bugs first.
3. **Then by priority and complexity** — High priority takes precedence, then lower CPX points within the same priority level.
4. **Archive completed work** — Move completed items to `DONE.md`, consolidating and shortening descriptions where possible.

## Coding Conventions

- This is for **Factorio 2**, not Factorio 1. Some API calls and behaviors may differ. See [`LUA_API.md`](LUA_API.md) for the bundled reference.
- When implementing or modifying Lua scripts, reference [`LUA_API.md`](LUA_API.md) and the bundled `LuaAPI/` HTML docs to verify correct API calls, parameter names, and return types.
- Keep files below 1000 lines. Split using partial classes or multiple smaller classes with single responsibilities.
- Problem solutions should be optimized, performant, and well thought out — avoid quick fixes.
- Do not be afraid to break backwards compatibility if new changes simplify or improve the project.
- Try to edit files and use tools WITHOUT PowerShell where possible — shell scripts get stuck and then manually terminate.

## Environment

- Factorio installation folder: `E:\Games\Factorio2`

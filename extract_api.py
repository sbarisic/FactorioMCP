"""
Extract Factorio Lua API into LUA_API.md
Parses runtime-api.json and prototype-api.json and generates a comprehensive
markdown reference with all classes, methods, attributes, events, concepts,
global objects, defines, and prototypes.
"""
import json
import re

OUT = r'E:\Projects\FactorioMCP\FactorioMCP\LUA_API.md'
RUNTIME_JSON = r'E:\Projects\FactorioMCP\FactorioMCP\LuaAPI\runtime-api.json'
PROTO_JSON   = r'E:\Projects\FactorioMCP\FactorioMCP\LuaAPI\prototype-api.json'

print("Loading JSON files...")
runtime   = json.loads(open(RUNTIME_JSON, encoding='utf-8').read())
prototype = json.loads(open(PROTO_JSON,   encoding='utf-8').read())
print(f"  Runtime:   {len(runtime['classes'])} classes, {len(runtime['events'])} events, {len(runtime['concepts'])} concepts")
print(f"  Prototype: {len(prototype['prototypes'])} prototypes, {len(prototype['types'])} types")


# ── helpers ───────────────────────────────────────────────────────────────────

def clean(text):
    """Strip markdown links of the form [text](link) → text, collapse whitespace."""
    if not text:
        return ""
    text = re.sub(r'\[([^\]]+)\]\([^)]+\)', r'\1', text)  # remove hyperlinks
    text = re.sub(r'\s+', ' ', text.strip())
    # truncate very long descriptions for readability
    if len(text) > 250:
        text = text[:247] + "..."
    return text

def fmt_type(t):
    if t is None:
        return ""
    if isinstance(t, str):
        return t
    if isinstance(t, dict):
        kind = t.get('complex_type', '')
        if kind == 'array':
            return f"array[{fmt_type(t.get('value','?'))}]"
        if kind == 'dictionary':
            return f"dict[{fmt_type(t.get('key','?'))} → {fmt_type(t.get('value','?'))}]"
        if kind == 'union':
            opts = [fmt_type(o) for o in t.get('options', [])]
            return " | ".join(opts[:4]) + (" | ..." if len(opts) > 4 else "")
        if kind == 'literal':
            return repr(t.get('value', ''))
        if kind == 'tuple':
            return f"({', '.join(fmt_type(v) for v in t.get('values',[]))})"
        if kind == 'table':
            return "table"
        if kind == 'function':
            return "function"
        if kind == 'LuaStruct':
            return "struct"
        return kind or str(t)
    return str(t)

def fmt_params(params):
    parts = []
    for p in params:
        ptype = fmt_type(p.get('type'))
        opt   = "?" if p.get('optional') else ""
        parts.append(f"`{p['name']}{opt}`: {ptype}")
    return ", ".join(parts) if parts else "—"

def fmt_returns(rvs):
    if not rvs:
        return "void"
    parts = [fmt_type(r.get('type')) for r in rvs]
    return ", ".join(p for p in parts if p)

lines = []
w = lines.append


# ── Header ────────────────────────────────────────────────────────────────────
api_ver  = runtime.get('application_version', '?')
api_num  = runtime.get('api_version', '?')

w(f"# Factorio Lua API Reference")
w(f"")
w(f"> **Factorio version:** {api_ver}  |  **API version:** {api_num}  |  **Stage:** {runtime.get('stage','runtime')}")
w(f"> Auto-generated from `runtime-api.json` and `prototype-api.json`.")
w(f"> Online: <https://lua-api.factorio.com/latest/>")
w(f"")
w("---")
w("")

# ── Table of Contents ─────────────────────────────────────────────────────────
w("## Table of Contents")
w("")
w("1. [Global Objects & Functions](#1-global-objects--functions)")
w("2. [Runtime Classes](#2-runtime-classes)")
w("3. [Events](#3-events)")
w("4. [Concepts](#4-concepts)")
w("5. [Defines](#5-defines)")
w("6. [Prototypes](#6-prototypes)")
w("7. [Prototype Types (Summary)](#7-prototype-types-summary)")
w("8. [RCON-Specific Notes](#8-rcon-specific-notes)")
w("")
w("---")
w("")

# ── Section 1: Global Objects & Functions ────────────────────────────────────
w("## 1. Global Objects & Functions")
w("")
w("These are the top-level Lua globals available in the **runtime (control)** stage.")
w("")

w("### Global Objects")
w("")
w("| Name | Type | Description |")
w("|------|------|-------------|")
for g in runtime.get('global_objects', []):
    desc = clean(g.get('description', ''))
    w(f"| `{g['name']}` | `{g.get('type','')}` | {desc} |")
w("")

w("### Global Functions")
w("")
w("| Function | Parameters | Returns | Description |")
w("|----------|------------|---------|-------------|")
for gf in runtime.get('global_functions', []):
    params = fmt_params(gf.get('parameters', []))
    ret    = fmt_returns(gf.get('return_values', []))
    desc   = clean(gf.get('description', ''))
    w(f"| `{gf['name']}` | {params} | {ret} | {desc} |")
w("")
w("---")
w("")


# ── Section 2: Runtime Classes ────────────────────────────────────────────────
w("## 2. Runtime Classes")
w("")
w(f"There are **{len(runtime['classes'])}** runtime classes. Abstract base classes are marked *(abstract)*.")
w("")

for cls in sorted(runtime['classes'], key=lambda c: c['name']):
    abstract_tag = " *(abstract)*" if cls.get('abstract') else ""
    desc = clean(cls.get('description', ''))
    w(f"### `{cls['name']}`{abstract_tag}")
    w("")
    if desc:
        w(desc)
        w("")

    # Methods
    methods = cls.get('methods', [])
    if methods:
        w("**Methods:**")
        w("")
        w("| Method | Parameters | Returns | Description |")
        w("|--------|------------|---------|-------------|")
        for m in sorted(methods, key=lambda x: x['name']):
            params = fmt_params(m.get('parameters', []))
            ret    = fmt_returns(m.get('return_values', []))
            desc_m = clean(m.get('description', ''))
            w(f"| `{m['name']}()` | {params} | {ret} | {desc_m} |")
        w("")

    # Attributes
    attrs = cls.get('attributes', [])
    if attrs:
        w("**Attributes:**")
        w("")
        w("| Attribute | Type | RW | Description |")
        w("|-----------|------|----|-------------|")
        for a in sorted(attrs, key=lambda x: x['name']):
            rt = fmt_type(a.get('read_type'))
            wt = fmt_type(a.get('write_type'))
            if rt and wt and rt != wt:
                t_str = f"R:`{rt}` W:`{wt}`"
            elif rt:
                t_str = f"`{rt}`"
            elif wt:
                t_str = f"`{wt}`"
            else:
                t_str = ""
            # R/W flags
            has_read  = a.get('read_type')  is not None
            has_write = a.get('write_type') is not None
            rw = ("R" if has_read else "") + ("W" if has_write else "")
            rw = rw if rw else "?"
            desc_a = clean(a.get('description', ''))
            w(f"| `{a['name']}` | {t_str} | {rw} | {desc_a} |")
        w("")

    # Operators (call, index, length)
    ops = cls.get('operators', [])
    if ops:
        w("**Operators:**")
        w("")
        w("| Operator | Type | Description |")
        w("|----------|------|-------------|")
        for op in ops:
            desc_op = clean(op.get('description', ''))
            w(f"| `{op.get('name','?')}` | {fmt_type(op.get('read_type') or op.get('type'))} | {desc_op} |")
        w("")

    w("---")
    w("")


# ── Section 3: Events ─────────────────────────────────────────────────────────
w("## 3. Events")
w("")
w(f"There are **{len(runtime['events'])}** events. Register handlers with `script.on_event(defines.events.EVENT_NAME, handler)`.")
w("")
w("| Event | Data Fields | Description |")
w("|-------|-------------|-------------|")
for ev in sorted(runtime['events'], key=lambda e: e['name']):
    data_fields = ", ".join(f"`{d['name']}`" for d in ev.get('data', []))
    desc = clean(ev.get('description', ''))
    w(f"| `{ev['name']}` | {data_fields or '—'} | {desc} |")
w("")
w("---")
w("")

# ── Section 4: Concepts ───────────────────────────────────────────────────────
w("## 4. Concepts")
w("")
w(f"There are **{len(runtime['concepts'])}** runtime concepts (structured data types, unions, etc.).")
w("")
w("| Concept | Type Shape | Description |")
w("|---------|------------|-------------|")
for c in sorted(runtime['concepts'], key=lambda x: x['name']):
    type_shape = fmt_type(c.get('type'))
    desc = clean(c.get('description', ''))
    w(f"| `{c['name']}` | {type_shape} | {desc} |")
w("")
w("---")
w("")

# ── Section 5: Defines ────────────────────────────────────────────────────────
w("## 5. Defines")
w("")
w("All `defines.*` enumerations available at runtime.")
w("")

def render_define(d, prefix="defines"):
    full_name = f"{prefix}.{d['name']}"
    desc = clean(d.get('description', ''))
    if 'values' in d:
        values = [v['name'] for v in d['values']]
        values_str = ", ".join(f"`{v}`" for v in values[:12])
        if len(values) > 12:
            values_str += f" *...+{len(values)-12} more*"
        w(f"### `{full_name}`")
        w("")
        if desc:
            w(desc)
            w("")
        w(f"Values: {values_str}")
        w("")
    elif 'subkeys' in d:
        w(f"### `{full_name}` *(namespace)*")
        w("")
        if desc:
            w(desc)
            w("")
        for sub in d['subkeys']:
            render_define(sub, full_name)

for d in sorted(runtime.get('defines', []), key=lambda x: x['name']):
    render_define(d)

w("---")
w("")


# ── Section 6: Prototypes ─────────────────────────────────────────────────────
w("## 6. Prototypes")
w("")
w(f"There are **{len(prototype['prototypes'])}** data-stage prototypes. These define entity/item/recipe types in `data.extend({{...}})`.")
w("")

for p in sorted(prototype['prototypes'], key=lambda x: x['name']):
    desc = clean(p.get('description', ''))
    parent = p.get('parent')
    parent_str = f" *(extends `{parent}`)*" if parent else ""
    abstract_tag = " *(abstract)*" if p.get('abstract') else ""
    w(f"### `{p['name']}`{parent_str}{abstract_tag}")
    w("")
    if desc:
        w(desc)
        w("")

    props = p.get('properties', [])
    if props:
        w("**Properties:**")
        w("")
        w("| Property | Type | Required | Description |")
        w("|----------|------|----------|-------------|")
        for prop in sorted(props, key=lambda x: (not x.get('required', False), x['name'])):
            ptype   = fmt_type(prop.get('type'))
            req     = "✓" if prop.get('required') else ""
            desc_p  = clean(prop.get('description', ''))
            default = prop.get('default')
            default_str = f" *(default: `{default}`)*" if default is not None else ""
            w(f"| `{prop['name']}` | `{ptype}` | {req} | {desc_p}{default_str} |")
        w("")

    w("---")
    w("")


# ── Section 7: Prototype Types Summary ───────────────────────────────────────
w("## 7. Prototype Types (Summary)")
w("")
w(f"There are **{len(prototype['types'])}** named prototype types. These are reusable type definitions used as property values in prototypes.")
w("")
w("| Type | Shape | Description |")
w("|------|-------|-------------|")
for t in sorted(prototype['types'], key=lambda x: x['name']):
    shape = fmt_type(t.get('type'))
    desc  = clean(t.get('description', ''))
    w(f"| `{t['name']}` | {shape} | {desc} |")
w("")
w("---")
w("")

# ── Section 8: RCON Notes ─────────────────────────────────────────────────────
w("## 8. RCON-Specific Notes")
w("")
w("These notes apply to Lua executed via RCON (`/silent-command`) in this project:")
w("")
w("- **`game.player` is `nil`** in RCON context — use `game.connected_players[1]` instead.")
w("- **`rcon.print(value)`** is the *only* mechanism to return data from an RCON command. All service methods format a JSON string and pass it here.")
w("- **`/silent-command`** suppresses chat echo; use instead of `/c`.")
w("- **`storage.*`** persists data across RCON calls within the same game session (used for `walk_state`, `chat_log`).")
w("- **`script.on_event()`** registers persistent event handlers from RCON (e.g. `on_tick` for walking, `on_console_chat` for chat capture).")
w("- **Serialisation:** Lua tables must be serialised manually via string concatenation (no `json` library in vanilla Factorio Lua).")
w("")
w("### Common RCON Patterns")
w("")
w("```lua")
w("-- Get player position")
w("local p = game.connected_players[1]")
w("rcon.print('{\"x\":' .. p.position.x .. ',\"y\":' .. p.position.y .. '}')")
w("")
w("-- Find entities near player")
w("local ents = game.connected_players[1].surface.find_entities_filtered{")
w("  position = game.connected_players[1].position,")
w("  radius   = 20")
w("}")
w("")
w("-- Walk handler (persists via on_tick)")
w("script.on_event(defines.events.on_tick, function(e)")
w("  -- movement logic here")
w("end)")
w("")
w("-- Store data across RCON calls")
w("storage.my_key = 'some_value'")
w("```")
w("")
w("---")
w("")
w("*Generated by `extract_api.py` from `runtime-api.json` (Factorio 2 API) and `prototype-api.json`.*")
w("")

# ── Write file ────────────────────────────────────────────────────────────────
print("Writing LUA_API.md...")
content = "\n".join(lines)
with open(OUT, 'w', encoding='utf-8') as f:
    f.write(content)

line_count = content.count('\n') + 1
size_kb = len(content.encode('utf-8')) / 1024
print(f"Done! {line_count:,} lines, {size_kb:.1f} KB → {OUT}")


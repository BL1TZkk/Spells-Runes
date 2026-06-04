# SpellsAndRunes — Session Changes

## HUD Scaling (`src/HUD/HudFlux.cs`, `src/HUD/HudCastBar.cs`)

Both HUD elements now have constant visual size proportional to screen resolution, independent of GUI scale setting.

- `_drawScale = clamp(FrameHeight / 1080, 0.60, 1.40)` — physical size based on resolution
- `_vds = _drawScale / _sc` — virtual px scale for bounds
- `ctx.Scale(_drawScale, _drawScale)` in Cairo draw (HudElement is NOT pre-scaled by VS)
- HudFlux position tracks hotbar in physical px, clamped to screen edge

## Radial Menu (`src/HUD/HudRadialMenu.cs`)

**Click detection fix:**
- `Local(mx, my)` returns physical canvas offset via `el.Bounds.absX/Y`
- `ctx.Scale(sc, sc)` in `DrawRadial` centers the wheel visually at any GUI scale
- Both `UpdateMouse` and `OnMouseDown` divide `Local()` result by `sc` to convert to draw-space before passing to `SlotAtPos`

**Sprint while R is held:**
- `DialogType.Dialog` must stay (needed for `PrefersUngrabbedMouse` + cursor + click handling)
- Sprint forwarded via a 16ms tick registered on `Open()`, unregistered on `Close()`
- Uses `GetHotKeyByCode("sprint")` with `GlKeys.LShift` fallback

## Spellbook GUI Performance (`src/GUI/GuiDialogSpellbook.cs`)

**Problem:** 30fps forced redraws + unconditional `Redraw()` on every `OnMouseMove` → 100-200 Cairo redraws/s. Each redraw included ~276 `ctx.Stroke()` calls for the leather grain.

**Fixes:**
- Tick rate: 30fps → 20fps
- `OnMouseMove` no longer calls `Redraw()` directly (tick handles hover updates)
- Leather grain cached to `ImageSurface` on first draw, blit-ted each frame (276 strokes → 1 paint)
- `ComposeDialog` guarded by frame dimensions — no double-compose on first open
- `_elemSpells[4]` cache — LINQ `.Where().ToList()` per element cached, rebuilt only on compose
- `TSizeCached(ctx, text, fontSize)` — text measurements cached in `Dictionary`, hot path in `DrawSpellNode`
- Lore (tab 2) and Runes (tab 3) excluded from tick redraws (no `_t` animations)
- Pan and drag call `Redraw()` directly for immediate response

## Build Fix (`SpellsAndRunes.csproj`)

- `VSSurvivalMod.dll` HintPath fixed from `C:\Games\Vintagestory\Mods\` to `D:\Vintagestory\Mods\`

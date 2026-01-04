# SYSTEM CONTEXT — GOOGLE ANTIGRAVITY

This document is a **single source of truth** for understanding the technical limitations, APIs, and architectural constraints of **Vintage Story 1.21.5 modding**, specifically related to **World Map integration and land claims visualization**.

Your role:

- Do NOT hallucinate undocumented APIs
- Assume only **public Modding API access** unless explicitly stated
- Treat internal game assemblies as **restricted**

---

# PROJECT CONTEXT — LANDBARON MAP INTEGRATION

## Goal

Display claimed land areas on the **World Map (M)** using color-coded overlays:

- GREEN → Owned by player
- RED → Owned by others
- YELLOW → For sale

With:

- Viewport culling
- Tooltips on hover
- Client-side rendering only

---

# TARGET GAME VERSION

- **Vintage Story 1.21.5**
- API compatibility: 1.21.x

---

# VERIFIED PUBLIC DOCUMENTATION

## 1. Official Modding API

**API Reference (Public Only):**
<https://apidocs.vintagestory.at/>

Contains:

- All public interfaces
- All supported namespaces
- Server & Client APIs

Does NOT contain:

- Map rendering APIs
- GUI Map APIs
- World Map layers

---

## 2. Official Modding Overview

<https://www.vintagestory.at/features/modding.html>

Confirms:

- Not all internal systems are exposed
- GUI-heavy systems are often internal-only
- Mods must rely on documented APIs

---

# MAP-RELATED API AVAILABILITY MATRIX

| Component | Public API | Notes |
|---------|-----------|------|
| MapLayer | NO | Internal class |
| WorldMapManager | NO | Not exposed |
| GuiElementMap | NO | Internal GUI |
| World Map rendering | NO | Restricted |
| Land claims data | YES | Via IWorldManagerAPI |

---

# CLAIM DATA — WHAT IS ACCESSIBLE

## API: IWorldManagerAPI

Namespace:

```
Vintagestory.API.Server
```

Public property:

```
List<LandClaim> LandClaims
```

This allows:

- Enumerating all claims
- Reading owner, area, and block positions
- Syncing data to clients

---

## Example (Valid Public API Usage)

```csharp
foreach (var claim in api.WorldManager.LandClaims)
{
    api.Logger.Notification($"Claim owner: {claim.OwnerName}");
}
```

This is valid and supported.

---

# IMPLEMENTATION PLAN — STATUS REVIEW

## Original Design (Conceptually Correct)

- Create `LandClaimMapLayer : MapLayer`
- Register via `WorldMapManager.RegisterMapLayer`
- Draw rectangles per chunk
- Use viewport culling
- Provide hover tooltips

## Technical Blocker

The following required classes are NOT present in public assemblies:

- MapLayer
- WorldMapManager
- GuiElementMap

They exist only in:

- `Vintagestory.exe`
- `Vintagestory.Client.dll`

These assemblies are NOT referenced by mods.

---

# ROOT CAUSE ANALYSIS

The Vintage Story Modding API intentionally restricts:

- Direct World Map rendering
- Internal GUI access
- Map layer manipulation

Reason:

- Stability
- Security
- Mod compatibility

Conclusion:
> **World Map overlays are not officially supported by the public API in 1.21.5**

---

# EXISTING MAP MODS (NON-PUBLIC API USAGE)

The following mods render map data by bypassing the public API:

- GiMap — <https://mods.vintagestory.at/gimap>
- MinimalMapping — <https://mods.vintagestory.at/minimalmapping>

They likely use:

- Reflection
- Internal type loading
- Assembly hooks

---

# REFLECTION — ADVANCED (UNSUPPORTED)

Possible but unsafe approach:

```csharp
Type mapLayerType = Type.GetType(
    "Vintagestory.Client.GUI.MapLayer, Vintagestory.Client"
);
```

Risks:

- Breaks on game updates
- Not supported by API
- High maintenance cost

---

# WHAT IS POSSIBLE TODAY (PUBLIC API)

## Supported

- Read land claims
- Sync claim data
- Custom GUIs
- External or alternative maps

## NOT Supported

- World Map overlays
- Map tooltips
- MapLayer registration

---

# RECOMMENDED ARCHITECTURAL ALTERNATIVES

1. Custom in-game map UI (not World Map)
2. External web-based map
3. Integration with existing map mods
4. Reflection-based solution (last resort)

---

# FINAL DIRECTIVE FOR GEMINI 3 HIGH

- Do NOT attempt to implement MapLayer via public API
- Do NOT assume undocumented classes exist
- Treat map overlay rendering as **blocked**
- Focus on alternative architectures unless reflection is explicitly requested

---

# END OF CONTEXT

# Implementation Plan - Land Baron v2.5.0

This document outlines the roadmap for the next set of features for the **Land Baron** mod.

## User Review Required
> [!IMPORTANT]
> **PLAYERS DATA:** DONT CHANGE IT! IN THIS PROCESS OF UPDATING THE MOD, PLAYERS SHOULD NOT LOSE THEIR DATA!

## Goal Description
We are enhancing the Economy and Land Management systems to improve player interaction and Quality of Life (QoL).
1.  **Economy:** Allow direct peer-to-peer transfers without physical items.
2.  **Land Management:** Simplify permission sharing with a "Global Trust" system.
3.  **UX/UI:** Reduce chat spam by moving territory notifications to a sleek, non-intrusive HUD.

## User Review Required
> [!IMPORTANT]
> **HUD Implementation:** functionality relies on `HudElement`. We will position it slightly above the hotbar. This allows for a cleaner interface but requires Client-Side rendering logic.

## Proposed Changes

### [Source Code]
modification of `src/LandBaronSystem.cs` to include new commands and the HUD class.

#### [MODIFY] [LandBaronSystem.cs](file:///g:/GitHub/Vibecoding/VintageStoryMODS/0.%20Mod%20Creation/LandBaron/LandBaron_v2.5.0/src/LandBaronSystem.cs)

**1. Bank System Updates (`/transferir`)**
*   **Logic:**
    *   Check if Target Player is Online.
    *   Check Source Balance.
    *   Atomic Transaction: Deduct from Source -> Add to Target.
    *   Feedback: Send success message to both players.
*   **Command:** `/transferir [player] [amount]`

**2. Land Permission Updates (`/terreno add`)**
*   **Logic:**
    *   Resolve Target Player UID.
    *   Iterate all claims where `OwnerUID == CurrentPlayer`.
    *   Add Target UID to `AllowedUIDs` if not present.
    *   Save changes.
*   **Command:** `/terreno add [player]`

**3. HUD Implementation (New Class `TerritoryHud`)**
*   **Logic:**
    *   Inherit from `HudElement`.
    *   Render text using `Cairo` context.
    *   Trigger "Fade In" when entering a new chunk.
    *   Auto-hide after ~3-5 seconds.
    *   Replace `capi.ShowChatMessage` with `hud.ShowNotification()`.

```csharp
// Mockup of the HUD Class structure
public class TerritoryHud : HudElement
{
    // Compose the GUI
    // Render the text (Owner Name, Sale Status)
    // Handle FadeOut logic
}
```

## Verification Plan

### Automated Tests
*   N/A (Visual/Gameplay Mod)

### Manual Verification
1.  **Transfer:**
    *   Join with 2 clients (or use a friend).
    *   Run `/transferir Friend 50`.
    *   Verify sender balance decreases and receiver increases.
    *   Verify error if offline or insufficient funds.
2.  **Permissions:**
    *   Owner runs `/terreno add Friend`.
    *   Friend tries to break block in Owner's land.
    *   Verify success.
3.  **HUD:**
    *   Walk between claimed chunks.
    *   Verify HUD appears above hotbar.
    *   Verify HUD fades out / disappears.
    *   Verify NO chat spam occurs.

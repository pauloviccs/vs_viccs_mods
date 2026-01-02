# Land Baron v2.5.0 - Walkthrough

## Summary
Successfully implemented the Money Transfer and Global Land Permission features. The Territory HUD feature was attempted but reverted to the classic Chat Notification system due to compilation issues with the Cairo dependencies in the current environment.

## Features Verification

### 1. Money Transfer (`/transferir`)
Allows players to send money to other **online** players.

**Steps:**
1. Ensure two players are online (e.g., You and a friend/alt).
2. Check your balance: `/saldo`.
3. Execute: `/transferir [TargetPlayerName] [Amount]`
   - Example: `/transferir PlayerTwo 50`
4. **Verify:**
   - Both players receive a chat notification.
   - Using `/saldo` confirms the amount was deducted from sender and added to recipient.
   - "Cash Register" sound plays for both.

### 2. Global Land Permissions (`/terreno add`)
Grants build/break access to a player across **ALL** of your owned land claims.

**Steps:**
1. Own at least one chunk of land (`/terreno comprar`).
2. Execute: `/terreno add [TargetPlayerName]`
   - Example: `/terreno add PlayerTwo`
3. **Verify:**
   - Chat confirms: "Permissão concedida a PlayerTwo em X terrenos."
   - The target player can now place/break blocks in your claimed chunks.

### 3. Territory Notification
**Note:** The HUD implementation was disabled. The mod uses the improved Chat Notification system.

**Steps:**
1. Walk from Wilderness into a Claimed Chunk.
2. **Verify:**
   - A chat message appears: `[Território] 🏰 OwnerName`
   - If for sale, it shows price.
   - It shows "Empire Size" (total chunks owned).
   - This message appears only when causing a *change* in territory status (entering/leaving), preventing spam when standing still, but will appear when crossing borders.

## Compilation
The mod compiles successfully with `Exit code 0`.
Path: `bin/Release/LandBaron.dll` (or similar).

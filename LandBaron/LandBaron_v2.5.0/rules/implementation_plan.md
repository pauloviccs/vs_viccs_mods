# Implementar HUD de Território

O objetivo é restaurar a classe `TerritoryHud` (que estava comentada), descomentando-a e aprimorando-a para exibir informações do território (dono, preço de venda) e o saldo do jogador de forma visual, substituindo o spam no chat.

## User Review Required
>
> [!IMPORTANT]
> Vou remover as mensagens de chat que anunciam o status do território e substituí-las pelo HUD.

## Proposed Changes

### src

#### [MODIFY] [LandBaronSystem.cs](file:///g:/GitHub/Vibecoding/VintageStoryMODS/0.%20Mod%20Creation/LandBaron/LandBaron_v2.5.0/src/LandBaronSystem.cs)

- Descomentar a classe `TerritoryHud`.
- Atualizar `TerritoryHud` para incluir:
  - Um estilo visual distinto.
  - Exibição do saldo do jogador (obtido de `WatchedAttributes`).
  - Lógica para atualizar/abrir ao entrar em um novo chunk.
  - Garantir persistência (ou tempo de exibição longo) conforme solicitado.
- Atualizar `LandBaronSystem` para:
  - Inicializar `TerritoryHud` no `StartClientSide`.
  - Chamar `hud.ShowNotification(...)` no `ClientTick` em vez de `capi.ShowChatMessage`.
  - Remover as chamadas de `capi.ShowChatMessage` para informações de território.

## Verification Plan

### Automated Tests

- Nenhum teste automatizado disponível para GUI.

### Manual Verification

- Entrar no jogo.
- Caminhar entre chunks com dono e sem dono.
- Verificar se o HUD aparece com as informações corretas (Dono, Preço, Meu Saldo).
- Verificar se o HUD atualiza ou desaparece conforme apropriado.
- Verificar a ausência de spam no chat.

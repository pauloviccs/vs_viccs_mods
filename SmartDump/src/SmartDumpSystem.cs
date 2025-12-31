using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace SmartDump
{
    public class SmartDumpSystem : ModSystem
    {
        private ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            api.Logger.Notification("[SmartDump] Mod de Despejo Inteligente carregado.");

            api.ChatCommands.Create("dump")
                .WithDescription("Guarda itens do inventario em baus proximos que ja tenham o mesmo item.")
                .WithAlias("despejar")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(OnDumpCommand);
        }

        private TextCommandResult OnDumpCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Comando apenas para jogadores.");

            // 1. Definição da Área de Busca (Raio de 5 blocos)
            BlockPos center = player.Entity.Pos.AsBlockPos;
            int radius = 5;
            
            List<BlockEntityContainer> nearbyContainers = new List<BlockEntityContainer>();

            BlockPos minPos = center.AddCopy(-radius, -radius, -radius);
            BlockPos maxPos = center.AddCopy(radius, radius, radius);

            // Escaneia a área procurando containers
            sapi.World.BlockAccessor.WalkBlocks(
                minPos, maxPos,
                (block, x, y, z) =>
                {
                    BlockPos pos = new BlockPos(x, y, z);
                    BlockEntity be = sapi.World.BlockAccessor.GetBlockEntity(pos);
                    
                    if (be is BlockEntityContainer container)
                    {
                        // CORREÇÃO DEFINITIVA: Removemos o CheckOpen.
                        // Se é um container e está perto, adicionamos à lista.
                        nearbyContainers.Add(container);
                    }
                }
            );

            if (nearbyContainers.Count == 0)
                return TextCommandResult.Error("Nenhum bau encontrado nas proximidades (Raio 5).");

            // 2. Coleta Inventários do Jogador
            List<IInventory> playerInventories = new List<IInventory>();
            
            // Usa strings literais para evitar erros de constantes
            var backpack = player.InventoryManager.GetOwnInventory("backpack");
            var hotbar = player.InventoryManager.GetOwnInventory("hotbar");

            if (backpack != null) playerInventories.Add(backpack);
            if (hotbar != null) playerInventories.Add(hotbar);

            int itemsMovedCount = 0;
            int totalStacksMoved = 0;

            // 3. Execução da Lógica "Smart Dump"
            foreach (var pInv in playerInventories)
            {
                // Itera sobre cada slot do jogador
                foreach (var playerSlot in pInv)
                {
                    // Ignora slots vazios ou o item que está segurando na mão ativa
                    if (playerSlot.Empty) continue;
                    if (playerSlot == player.InventoryManager.ActiveHotbarSlot && !playerSlot.Empty) continue; 

                    ItemStack pStack = playerSlot.Itemstack;
                    if (pStack == null) continue;

                    // Para este item, procura em TODOS os baús próximos
                    foreach (var container in nearbyContainers)
                    {
                        IInventory chestInv = container.Inventory;

                        // Varre os slots do baú
                        foreach (var chestSlot in chestInv)
                        {
                            // REGRA DE OURO: Só interage se o slot do baú NÃO estiver vazio
                            // E se contiver EXATAMENTE o mesmo item
                            if (chestSlot.Empty) continue;

                            if (chestSlot.Itemstack.Equals(sapi.World, pStack, GlobalConstants.IgnoredStackAttributes))
                            {
                                // SAFELOCK: Verificação de Integridade Matemática (Snapshot antes de mover)
                                int initialTotal = pStack.StackSize + chestSlot.StackSize;

                                // Tenta mover do Jogador -> Baú
                                int moved = playerSlot.TryPutInto(sapi.World, chestSlot);

                                if (moved > 0)
                                {
                                    // Verificação pós-movimento
                                    int finalTotal = (playerSlot.Empty ? 0 : playerSlot.StackSize) + chestSlot.StackSize;
                                    
                                    if (initialTotal != finalTotal)
                                    {
                                        sapi.Logger.Error($"[SmartDump] ERRO DE INTEGRIDADE: {pStack.GetName()}. Iniciou com {initialTotal}, terminou com {finalTotal}.");
                                        // Nota: O dano já estaria feito aqui, mas TryPutInto é atômico na API.
                                        // O log serve para debug se algo muito estranho acontecer.
                                    }

                                    itemsMovedCount += moved;
                                    chestSlot.MarkDirty();
                                    playerSlot.MarkDirty();

                                    // Se o stack do jogador acabou, para de procurar baús para este item
                                    if (playerSlot.Empty) break;
                                }
                            }
                        }

                        if (playerSlot.Empty) break; // Item acabou, próximo item do inventário
                    }
                    
                    if (playerSlot.Empty) totalStacksMoved++;
                }
            }

            if (itemsMovedCount > 0)
            {
                // Som de sucesso
                sapi.World.PlaySoundAt(new AssetLocation("game:sounds/player/clothrepair"), player.Entity, null, true, 32, 1f);
                return TextCommandResult.Success($"Despejo inteligente concluido: {itemsMovedCount} itens transferidos.");
            }
            else
            {
                return TextCommandResult.Success("Nenhum item correspondente encontrado nos baus proximos.");
            }
        }
    }
}
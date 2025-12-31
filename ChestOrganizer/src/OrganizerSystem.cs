using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent; // <--- CRUCIAL: Onde vive o BlockEntityContainer

namespace SimpleOrganizer
{
    public class OrganizerSystem : ModSystem
    {
        private ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            api.Logger.Notification("[ChestOrganizer] Mod de Organizacao carregado.");

            api.ChatCommands.Create("organizar")
                .WithDescription("Organiza o bau ou container que voce esta olhando.")
                .WithAlias("sort")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(OnSortCommand);
        }

        private TextCommandResult OnSortCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Comando apenas para jogadores.");

            // 1. Identificar o Alvo (APENAS BLOCOS)
            IInventory targetInv = null;
            string targetName = "";

            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null)
            {
                return TextCommandResult.Error("Olhe para um Bau ou Container para organizar.");
            }

            BlockEntity be = sapi.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            
            // Verifica se o bloco tem inventário (BlockEntityContainer precisa de Vintagestory.GameContent)
            if (be is BlockEntityContainer container)
            {
                targetInv = container.Inventory;
                targetName = "do Container";
            }
            else
            {
                return TextCommandResult.Error("Este bloco nao e um container organizavel.");
            }

            if (targetInv == null || targetInv.Empty) 
                return TextCommandResult.Error("O container esta vazio ou invalido.");

            // 2. Executar Organização Segura
            bool success = SortInventorySafe(targetInv);

            if (success)
            {
                sapi.World.PlaySoundAt(new AssetLocation("game:sounds/player/clothrepair"), player.Entity, null, true, 32, 1f);
                return TextCommandResult.Success($"Organizacao {targetName} concluida.");
            }
            else
            {
                return TextCommandResult.Error("ERRO CRITICO: Contagem de itens nao bateu. Operacao cancelada.");
            }
        }

        private bool SortInventorySafe(IInventory inventory)
        {
            // --- FASE 1: Extração (Simulação) ---
            List<ItemStack> rawItems = new List<ItemStack>();
            int originalTotalStackSize = 0; 

            foreach (var slot in inventory)
            {
                if (!slot.Empty)
                {
                    rawItems.Add(slot.Itemstack.Clone());
                    originalTotalStackSize += slot.StackSize;
                }
            }

            if (rawItems.Count == 0) return true; // Já estava vazio

            // --- FASE 2: Merge (Agrupamento) ---
            List<ItemStack> mergedItems = new List<ItemStack>();

            foreach (var stack in rawItems)
            {
                // Tenta juntar com itens já listados
                foreach (var existing in mergedItems)
                {
                    if (existing.Equals(sapi.World, stack, GlobalConstants.IgnoredStackAttributes))
                    {
                        int maxStack = existing.Collectible.MaxStackSize;
                        int space = maxStack - existing.StackSize;
                        
                        if (space > 0)
                        {
                            int move = Math.Min(space, stack.StackSize);
                            existing.StackSize += move;
                            stack.StackSize -= move;
                        }
                    }
                    
                    if (stack.StackSize <= 0) break;
                }

                if (stack.StackSize > 0)
                {
                    mergedItems.Add(stack);
                }
            }

            // --- FASE 3: Ordenação ---
            var sortedItems = mergedItems.OrderBy(stack => GetCategoryWeight(stack)) // Tipo
                                         .ThenBy(stack => stack.GetName())           // Nome
                                         .ThenByDescending(stack => stack.StackSize) // Quantidade
                                         .ToList();

            // --- FASE 4: FAILSAFE (Verificação de Integridade) ---
            int finalTotalStackSize = sortedItems.Sum(i => i.StackSize);
            
            if (finalTotalStackSize != originalTotalStackSize)
            {
                sapi.Logger.Error($"[ChestOrganizer] ABORTANDO: Inconsistencia detectada. Original: {originalTotalStackSize}, Novo: {finalTotalStackSize}");
                return false;
            }

            // --- FASE 5: Aplicação (Commit) ---
            
            // 1. Limpa o inventário
            foreach (var slot in inventory)
            {
                if (!slot.Empty)
                {
                    slot.Itemstack = null;
                    slot.MarkDirty();
                }
            }

            // 2. Preenche ordenado
            int slotIndex = 0;
            foreach (var stack in sortedItems)
            {
                DummySlot tempSlot = new DummySlot(stack);

                while (slotIndex < inventory.Count)
                {
                    if (inventory[slotIndex].CanHold(tempSlot))
                    {
                        inventory[slotIndex].Itemstack = stack;
                        inventory[slotIndex].MarkDirty();
                        slotIndex++; 
                        break; 
                    }
                    slotIndex++;
                }
            }

            return true;
        }

        private int GetCategoryWeight(ItemStack stack)
        {
            if (stack.Collectible.Tool != null) return 1;
            if (stack.Collectible.Attributes != null && stack.Collectible.Attributes["clothes"].Exists) return 2;
            if (stack.Collectible.NutritionProps != null) return 3;
            if (stack.Class == EnumItemClass.Block) return 4;
            return 5;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Config; // <--- FALTAVA ESTA LINHA

namespace InfiniteHands
{
    public class InfiniteHandsSystem : ModSystem
    {
        private ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            // O evento DidPlaceBlock acontece APÓS o bloco ser colocado e o item consumido
            api.Event.DidPlaceBlock += OnDidPlaceBlock;
            
            api.Logger.Notification("[InfiniteHands] Carregado: Construção contínua ativada.");
        }

        private void OnDidPlaceBlock(IServerPlayer player, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            // Se estiver no criativo, não precisa recarregar
            if (player.WorldData.CurrentGameMode == EnumGameMode.Creative) return;

            // Pega o slot da mão ativa
            ItemSlot activeSlot = player.InventoryManager.ActiveHotbarSlot;

            // Só recarregamos se o slot estiver vazio AGORA (acabou o item)
            if (activeSlot.Empty) 
            {
                if (withItemStack == null) return;

                string targetCode = withItemStack.Collectible.Code.ToString();
                
                // Tenta recarregar
                if (RefillHand(player, activeSlot, targetCode))
                {
                    // Toque Cozy: Som de Pop
                    sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/pop"), 
                        player.Entity, null, true, 32, 0.5f);
                }
            }
        }

        private bool RefillHand(IServerPlayer player, ItemSlot handSlot, string targetCode)
        {
            // Acessa o inventário principal (Mochilas + Hotbar)
            // GlobalConstants agora será reconhecido
            IInventory inventory = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
            
            if (inventory == null) return false;

            // Varre todos os slots
            foreach (var slot in inventory)
            {
                if (slot.Empty) continue;

                // Achou o mesmo item?
                if (slot.Itemstack.Collectible.Code.ToString() == targetCode)
                {
                    // Transfere para a mão
                    int moved = slot.TryPutInto(player.Entity.World, handSlot);

                    if (moved > 0)
                    {
                        slot.MarkDirty();
                        handSlot.MarkDirty();
                        return true; 
                    }
                }
            }

            return false; 
        }
    }
}
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using AnimalTransport.Logic;
using System.IO;

namespace AnimalTransport.Patches
{
    [HarmonyPatch(typeof(Entity), "OnInteract")]
    public class EntityInteractPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Entity __instance, EntityAgent byEntity, ItemSlot slot, int mode)
        {
            // Validações de segurança
            if (mode != 0 || __instance.World.Side != EnumAppSide.Server) return true;
            
            var player = byEntity as EntityPlayer;
            if (player == null || !player.Controls.Sneak) return true;

            // Segurança extra para evitar null reference no slot
            if (slot.Itemstack == null) return true;

            ItemStack stack = slot.Itemstack;
            if (!TransportHelper.IsValidContainer(stack)) return true;
            if (TransportHelper.HasAnimal(stack)) return true;

            // Se for muito grande, deixa o jogo rodar normal (retorna true)
            if (!TransportHelper.IsCatchable(__instance)) return true;

            // --- LÓGICA DE CAPTURA ---

            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(ms))
                {
                    // Salva a entidade em bytes
                    __instance.ToBytes(writer, false);
                    
                    // Armazena no item
                    stack.Attributes.SetBytes("capturedEntityData", ms.ToArray());
                    stack.Attributes.SetString("capturedEntityClass", __instance.Class);
                    stack.Attributes.SetString("capturedEntityCode", __instance.Code.ToString());
                    
                    // FIX: Usamos GetName() direto. Ele já pega o nome customizado se houver.
                    string realName = __instance.GetName(); 
                    stack.Attributes.SetString("capturedEntityName", realName);
                }
            }

            // Atualiza UI do Item
            string animalName = stack.Attributes.GetString("capturedEntityName");
            stack.Attributes.SetString("gui-nametag", $"{stack.Collectible.GetHeldItemName(stack)} ({animalName})");
            slot.MarkDirty();

            // Feedback
            __instance.World.PlaySoundAt(new AssetLocation("game:sounds/effect/squish1"), __instance);
            __instance.Die(EnumDespawnReason.PickedUp);

            // Retorna false para CANCELAR a interação vanilla
            return false; 
        }
    }
}
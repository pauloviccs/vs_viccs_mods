using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using AnimalTransport.Logic;
using System.IO;

namespace AnimalTransport.Patches
{
    // FIX FINAL: Mudamos o alvo para 'CollectibleObject'. 
    // Isso existe com certeza e engloba ItemBlock, resolvendo o erro de build.
    [HarmonyPatch(typeof(CollectibleObject), "OnHeldInteractStart")]
    public class ItemBlockInteractPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, ref EnumHandHandling handling)
        {
            // --- GUARDA DE SEGURANÇA ---
            // Se não for uma das nossas cestas, retorna true (deixa o jogo rodar normal)
            if (slot.Itemstack == null) return true;
            if (!TransportHelper.IsValidContainer(slot.Itemstack)) return true;
            if (!TransportHelper.HasAnimal(slot.Itemstack)) return true;

            // --- LÓGICA DO MOD ---
            // Se chegou aqui, é uma cesta com animal. INTERCEPTA.
            handling = EnumHandHandling.PreventDefault;
            
            if (blockSel == null) return false;
            
            IWorldAccessor world = byEntity.World;
            if (world.Side == EnumAppSide.Client) return false;

            try 
            {
                byte[] data = slot.Itemstack.Attributes.GetBytes("capturedEntityData");
                string entityCode = slot.Itemstack.Attributes.GetString("capturedEntityCode");

                if (data != null)
                {
                    EntityProperties type = world.GetEntityType(new AssetLocation(entityCode));
                    if (type != null)
                    {
                        Entity newEntity = world.ClassRegistry.CreateEntity(type);

                        using (MemoryStream ms = new MemoryStream(data))
                        {
                            using (BinaryReader reader = new BinaryReader(ms))
                            {
                                newEntity.FromBytes(reader, false);
                            }
                        }

                        // Spawna levemente acima do bloco
                        newEntity.ServerPos.SetPos(blockSel.Position.ToVec3d().Add(0.5, 1.1, 0.5));
                        newEntity.Pos.SetFrom(newEntity.ServerPos);
                        world.SpawnEntity(newEntity);
                        
                        world.PlaySoundAt(new AssetLocation("game:sounds/effect/squish2"), newEntity);
                    }
                }
            }
            catch (System.Exception ex)
            {
                world.Logger.Error("AnimalTransport: Erro crítico ao soltar. " + ex.Message);
            }

            // Limpa a cesta
            slot.Itemstack.Attributes.RemoveAttribute("capturedEntityData");
            slot.Itemstack.Attributes.RemoveAttribute("capturedEntityClass");
            slot.Itemstack.Attributes.RemoveAttribute("capturedEntityCode");
            slot.Itemstack.Attributes.RemoveAttribute("capturedEntityName");
            slot.Itemstack.Attributes.RemoveAttribute("gui-nametag");
            
            slot.MarkDirty();

            return false; // Cancela a ação original (não coloca o bloco)
        }
    }
}
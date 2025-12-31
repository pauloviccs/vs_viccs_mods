using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace AnimalTransport.Patches
{
    [HarmonyPatch(typeof(Entity), "OnInteract")]
    public class EntityInteractPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Entity __instance, EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, int mode, ref EnumHandling consumed)
        {
            // If we are on the server or client, and the entity interacting (player) is holding our item
            if (slot != null && slot.Itemstack != null && slot.Itemstack.Collectible != null)
            {
                // Check if the item has our behavior
                if (slot.Itemstack.Collectible.HasBehavior<CollectibleBehaviorEntityCatch>())
                {
                    // Check if the item is NOT full (i.e. we are trying to capture)
                    bool isFull = slot.Itemstack.Attributes.HasAttribute("capturedEntity");
                    if (!isFull)
                    {
                        // We want to capture.
                        // Retrieve the behavior to check if we SHOULD capture (e.g. size check handled in behavior, but interaction blocking happens here)
                        // Actually, simplest is: If holding empty basket -> Block Entity Interact -> Let Item Interact run.
                        
                        // BUT, we must ensure we don't block interaction if we are just creating the behavior instance or something generic.
                        // The behavior exists on this item.
                        
                        // Force the handling to be done by the item, not the entity.
                        // Returning false in prefix skips the original method.
                        return false; 
                    }
                }
            }

            return true; // Continue original execution
        }
    }
}

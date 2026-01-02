using HarmonyLib;
using Vintagestory.API.Common;
using AnimalTransport.Logic;

namespace AnimalTransport.Patches
{
    [HarmonyPatch(typeof(CollectibleObject), "GetColorPreset")]
    public class ColorPresetPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ItemSlot slot, ref int __result)
        {
            if (TransportHelper.IsValidContainer(slot.Itemstack) && TransportHelper.HasAnimal(slot.Itemstack))
            {
                // Tinge de vermelho (Formato ARGB Int)
                // Alpha: 255, R: 255, G: 150, B: 150
                __result = (255 << 24) | (255 << 16) | (150 << 8) | 150;
            }
        }
    }
}
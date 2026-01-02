using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace AnimalTransport.Logic
{
    public static class TransportHelper
    {
        // AJUSTE FINO: Tamanho máximo da hitbox para ser considerado "pequeno"
        private const float MAX_WIDTH = 1.0f;
        private const float MAX_HEIGHT = 1.2f; 

        public static bool IsCatchable(Entity entity)
        {
            if (entity == null || !entity.Alive) return false;
            if (entity is EntityPlayer || entity is EntityItem) return false;

            // Lógica Dinâmica: Se couber na caixa, entra.
            Cuboidf box = entity.SelectionBox;
            if (box.Width > MAX_WIDTH || box.Height > MAX_HEIGHT) return false;

            return true;
        }

        public static bool IsValidContainer(ItemStack stack)
        {
            if (stack == null) return false;
            // Aceita qualquer item que tenha "basket" ou "pot" no código (ex: reedbasket)
            return stack.Collectible.Code.Path.Contains("basket") || 
                   stack.Collectible.Code.Path.Contains("pot");
        }

        public static bool HasAnimal(ItemStack stack)
        {
            return stack.Attributes.HasAttribute("capturedEntityData");
        }
    }
}
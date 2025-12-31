using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;

namespace PassiveForest
{
    public class ReforestSystem : ModSystem
    {
        private ICoreServerAPI? sapi;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            // Escuta global: Sempre que ALGO deixar de existir no servidor.
            api.Event.OnEntityDespawn += OnEntityDespawn;
        }

        private void OnEntityDespawn(Entity entity, EntityDespawnData reason)
        {
            // FILTRO 1: Só nos importamos se morreu de velhice (Timer acabou)
            if (reason.Reason != EnumDespawnReason.Expire) return;

            // FILTRO 2: É um item dropado?
            if (entity is EntityItem itemEntity)
            {
                ItemStack? stack = itemEntity.Itemstack;
                if (stack == null) return;

                // FILTRO 3: É uma muda de árvore (sapling)?
                // Os códigos de sapling no VS geralmente são "sapling-carvalho-free", etc.
                string itemCode = stack.Collectible.Code.Path;
                if (itemCode.StartsWith("sapling"))
                {
                    TryPlantSapling(itemEntity, stack);
                }
            }
        }

        private void TryPlantSapling(EntityItem entity, ItemStack stack)
        {
            if (sapi == null) return;

            // Pega a posição exata onde o item "morreu"
            BlockPos pos = entity.ServerPos.XYZ.AsBlockPos;

            // O item pode estar levemente afundado visualmente, garantimos que estamos no bloco de ar
            Block blockAtPos = sapi.World.BlockAccessor.GetBlock(pos);

            // Regra 1: O espaço precisa estar vazio (Ar ou Água rasa se for Mangue, mas vamos focar no Ar/Grama baixa)
            if (!IsReplaceable(blockAtPos)) return;

            // Regra 2: O bloco ABAIXO precisa ser solo
            Block blockBelow = sapi.World.BlockAccessor.GetBlock(pos.DownCopy());
            if (!IsSoil(blockBelow)) return;

            // MÁGICA: Transforma o Item em Bloco
            // Precisamos achar o Block correspondente ao Item.
            // Em VS, items e blocos compartilham códigos, mas precisamos garantir.
            Block? saplingBlock = sapi.World.GetBlock(stack.Collectible.Code);

            if (saplingBlock != null)
            {
                // Coloca o bloco no mundo
                sapi.World.BlockAccessor.SetBlock(saplingBlock.BlockId, pos);
                
                // Toca um som sutil de "folhas" para dar feedback se tiver alguém perto
                sapi.World.PlaySoundAt(new AssetLocation("game:sounds/block/plant"), pos.X, pos.Y, pos.Z);
                
                // Log opcional para debug (pode remover depois)
                // sapi.Logger.Notification($"Muda plantada automaticamente em {pos}");
            }
        }

        // Helpers para detecção de terreno
        private bool IsReplaceable(Block block)
        {
            // ID 0 é ar. Blocos com Replaceable > 6000 (como grama alta) podem ser substituídos.
            return block.BlockId == 0 || block.Replaceable > 6000;
        }

        private bool IsSoil(Block block)
        {
            // Verifica a fertilidade ou se é do tipo "Soil" / "Grass"
            return block.Fertility > 0 || block.Code.Path.Contains("soil") || block.Code.Path.Contains("grass");
        }
    }
}
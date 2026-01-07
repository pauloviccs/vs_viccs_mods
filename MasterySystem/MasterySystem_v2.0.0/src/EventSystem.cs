using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace MasteryTitles
{
    public class EventSystem : ModSystem
    {
        private ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.BreakBlock += OnBlockBreak;
            api.Event.OnEntityDeath += OnEntityDeath;
        }

        private void OnBlockBreak(IServerPlayer player, BlockSelection blockSel, ref float dropQuantityMultiplier, ref EnumHandling handling)
        {
            if (player == null) return;
            Block block = sapi.World.BlockAccessor.GetBlock(blockSel.Position);
            string code = block.Code.Path;

            if (code.Contains("rock") || code.Contains("ore")) TryTriggerEvent(player, MasteryType.Mining);
            else if (code.Contains("log")) TryTriggerEvent(player, MasteryType.Lumbering);
            else if (block.BlockMaterial == EnumBlockMaterial.Plant || code.Contains("crop")) TryTriggerEvent(player, MasteryType.Farming);
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (damageSource != null && damageSource.SourceEntity is EntityPlayer entityPlayer)
            {
                IServerPlayer player = entityPlayer.Player as IServerPlayer;
                if (player != null) TryTriggerEvent(player, MasteryType.Combat);
            }
        }

        private void TryTriggerEvent(IServerPlayer player, MasteryType type)
        {
            // 0.5% Chance (1 in 200) to avoid spam
            if (sapi.World.Rand.NextDouble() > 0.005) return;

            // Check if player has mastery data (optional, maybe only for high levels?)
            // Assuming events are for everyone to make it fun.

            switch(type)
            {
                case MasteryType.Mining:
                    TriggerMiningEvent(player);
                    break;
                case MasteryType.Lumbering:
                    TriggerLumberingEvent(player);
                    break;
                case MasteryType.Farming:
                    TriggerFarmingEvent(player);
                    break;
                case MasteryType.Combat:
                    TriggerCombatEvent(player);
                    break;
            }
        }

        private void TriggerMiningEvent(IServerPlayer player)
        {
            // Spawn Drifter as Golem
            EntityProperties type = sapi.World.GetEntityType(new AssetLocation("game:drifter-deep"));
            if (type == null) return;

            Entity entity = sapi.World.ClassRegistry.CreateEntity(type);
            entity.ServerPos.SetPos(player.Entity.Pos.XYZ.Add(2, 0, 2));
            
            // Buff it
            entity.Stats.Set("maxhealthExtraPoints", "event", 50f, true);
            entity.WatchedAttributes.SetString("event_type", "mastery_golem"); // Tag for drops if needed

            sapi.World.SpawnEntity(entity);
            sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/toolbreak"), player.Entity);
            player.SendMessage(0, "** UM GOLEM DE PEDRA DESPERTA! **", EnumChatType.Notification);
        }

        private void TriggerLumberingEvent(IServerPlayer player)
        {
            // Drop extra goodies
             player.SendMessage(0, "** voce encontrou um espirito da floresta! (Presente recebido) **", EnumChatType.Notification);
             ItemStack stack = new ItemStack(sapi.World.GetItem(new AssetLocation("game:gear-rusty")), 1);
             if (stack.Item != null) sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
        }

        private void TriggerFarmingEvent(IServerPlayer player)
        {
            // Insta-grow around
            BlockPos center = player.Entity.Pos.AsBlockPos;
            sapi.World.BlockAccessor.WalkBlocks(center.AddCopy(-5, -1, -5), center.AddCopy(5, 1, 5), (block, x, y, z) => {
                 BlockPos pos = new BlockPos(x, y, z, 0);
                 if (block.CropProps != null)
                 {
                     // Force grow a bit
                     // Simplification: just particles
                     sapi.World.SpawnParticles(5, ColorUtil.ToRgba(255, 0, 255, 0), pos.ToVec3d(), pos.ToVec3d().Add(1,1,1), new Vec3f(), new Vec3f(), 1f, 1f);
                 }
            });
            player.SendMessage(0, "** Chuva Abençoada! (Visual) **", EnumChatType.Notification);
        }

        private void TriggerCombatEvent(IServerPlayer player)
        {
            // Spawn Mini Boss
             EntityProperties type = sapi.World.GetEntityType(new AssetLocation("game:wolf-male"));
             if (type == null) return;
             
             Entity entity = sapi.World.ClassRegistry.CreateEntity(type);
             entity.ServerPos.SetPos(player.Entity.Pos.XYZ.Add(3, 0, 0));
             entity.Stats.Set("maxhealthExtraPoints", "event", 100f, true); // Tanky wolf
             
             sapi.World.SpawnEntity(entity);
             player.SendMessage(0, "** UM LOBO ALPHA APARECEU! **", EnumChatType.Notification);
        }
    }
}

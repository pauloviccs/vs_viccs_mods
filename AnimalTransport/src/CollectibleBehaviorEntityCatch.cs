using System;
using System.Text;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace AnimalTransport
{
    public class CollectibleBehaviorEntityCatch : CollectibleBehavior
    {
        public CollectibleBehaviorEntityCatch(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling, ref EnumHandling clientHandling)
        {
            bool isFull = slot.Itemstack.Attributes.HasAttribute("capturedEntity");

            if (!isFull)
            {
                // --- CAPTURE MODE ---
                if (entitySel == null || entitySel.Entity == null) return;
                
                // Prevent capturing players
                if (entitySel.Entity is EntityPlayer) return;

                // 1. Claim the interaction immediately to stop other behaviors (like "Too Wild" checks from vanilla/mods)
                handling = EnumHandHandling.PreventDefault;
                clientHandling = EnumHandling.PreventDefault;

                // 2. Client side just needs to know "we are handling this", so we stop here.
                // We let the server decide if it's valid or not (size check, etc).
                if (byEntity.World.Side == EnumAppSide.Client) return;

                // --- SERVER LOGIC BELOW ---
                ICoreServerAPI sapi = byEntity.World.Api as ICoreServerAPI;
                if (sapi == null) return;

                Entity target = entitySel.Entity;

                double maxDim = 1.1;
                if (target.SelectionBox.YSize > maxDim || target.SelectionBox.XSize > maxDim || target.SelectionBox.ZSize > maxDim)
                {
                   if (byEntity is EntityPlayer player)
                    {
                        sapi.SendIngameError(player as IServerPlayer, "toobig", "This animal is too big to fit in the basket!");
                    }
                    return;
                }

                ITreeAttribute entityTree = new TreeAttribute();
                
                // REFLECTION CALL FOR ToAttribute
                CallToAttribute(target, entityTree);

                string entityClass = target.Code.ToString();

                slot.Itemstack.Attributes["capturedEntity"] = entityTree; 
                slot.Itemstack.Attributes.SetString("entityClass", entityClass);
                slot.Itemstack.Attributes.SetString("entityName", target.GetName());

                target.Die(EnumDespawnReason.PickedUp);

                slot.MarkDirty();
                byEntity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/squish1"), byEntity.Pos.X, byEntity.Pos.Y, byEntity.Pos.Z);

                if (byEntity is EntityPlayer playerEntity && playerEntity.Player is IServerPlayer serverPlayer)
                {
                   sapi.SendMessage(serverPlayer, GlobalConstants.GeneralChatGroup, $"{serverPlayer.PlayerName}: capturou {target.GetName()}", EnumChatType.Notification);
                }
            }
            else
            {
                // --- RELEASE MODE ---
                // If we are looking at a block, we want to release.
                if (blockSel == null) return;

                // Claim interaction to prevent block placement (since baskets/chests are blocks)
                handling = EnumHandHandling.PreventDefault;
                clientHandling = EnumHandling.PreventDefault;

                if (byEntity.World.Side == EnumAppSide.Client) return;

                // --- SERVER LOGIC ---
                ICoreServerAPI sapi = byEntity.World.Api as ICoreServerAPI;
                if (sapi == null) return;

                ITreeAttribute entityTree = slot.Itemstack.Attributes.GetTreeAttribute("capturedEntity");
                string entityClassCode = slot.Itemstack.Attributes.GetString("entityClass");

                if (entityTree == null || string.IsNullOrEmpty(entityClassCode))
                {
                     // Corrupted state, clean up
                     slot.Itemstack.Attributes.RemoveAttribute("capturedEntity");
                     slot.Itemstack.Attributes.RemoveAttribute("entityClass");
                     slot.Itemstack.Attributes.RemoveAttribute("entityName");
                     slot.MarkDirty();
                     return;
                }

                BlockPos pos = blockSel.Position;
                Vec3d spawnPos = new Vec3d(pos.X + 0.5, pos.Y + 1.1, pos.Z + 0.5);

                AssetLocation code = new AssetLocation(entityClassCode);
                EntityProperties type = sapi.World.GetEntityType(code);
                if (type == null) return;

                Entity newEntity = sapi.World.ClassRegistry.CreateEntity(type);
                if (newEntity == null) return;

                // REFLECTION CALL FOR FromAttribute
                CallFromAttribute(newEntity, entityTree, byEntity.World);

                newEntity.ServerPos.SetPos(spawnPos);
                newEntity.Pos.SetPos(spawnPos);

                sapi.World.SpawnEntity(newEntity);

                slot.Itemstack.Attributes.RemoveAttribute("capturedEntity");
                slot.Itemstack.Attributes.RemoveAttribute("entityClass");
                slot.Itemstack.Attributes.RemoveAttribute("entityName");

                slot.MarkDirty();
                byEntity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/squish2"), byEntity.Pos.X, byEntity.Pos.Y, byEntity.Pos.Z);
            }
        }

        private void CallToAttribute(Entity entity, ITreeAttribute tree)
        {
            try
            {
                // Try to find ToAttribute(ITreeAttribute, bool)
                MethodInfo method = entity.GetType().GetMethod("ToAttribute");
                if (method != null)
                {
                     method.Invoke(entity, new object[] { tree, false });
                }
                else
                {
                    // Fallback: Just save basic stuff we know
                     tree["code"] = new StringAttribute(entity.Code.ToString());
                     tree["attributes"] = entity.Attributes; // Save dynamic attributes
                    // Manual fallback for Position
                     tree.SetDouble("posX", entity.Pos.X);
                     tree.SetDouble("posY", entity.Pos.Y);
                     tree.SetDouble("posZ", entity.Pos.Z);
                }
            }
            catch (Exception)
            {
                // Fail silently, at least we have the tree object
            }
        }

        private void CallFromAttribute(Entity entity, ITreeAttribute tree, IWorldAccessor world)
        {
            try
            {
                // Try to find FromAttribute(ITreeAttribute, long)
                // Note: The second argument type varies by version, trying reasonable guesses or generic invoke
                MethodInfo method = entity.GetType().GetMethod("FromAttribute");
                if (method != null)
                {
                    // Try to invoke with (tree, TotalDays)
                     method.Invoke(entity, new object[] { tree, (long)world.Calendar.TotalDays });
                }
            }
            catch (Exception)
            {
                // Fail silently
            }
        }

        public override void GetHeldItemName(StringBuilder sb, ItemStack itemStack)
        {
            if (itemStack.Attributes.HasAttribute("entityName"))
            {
                string animal = itemStack.Attributes.GetString("entityName");
                sb.Append(" (" + animal + ")");
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (inSlot.Itemstack.Attributes.HasAttribute("entityName"))
            {
                string animal = inSlot.Itemstack.Attributes.GetString("entityName");
                dsc.AppendLine("\nContains: " + animal);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config; 
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util; 

namespace GraveMod
{
    public class GraveData
    {
        public string OwnerUID;
        public string OwnerName;
        public long CreationTime;
    }

    public class GraveSystem : ModSystem
    {
        private ICoreServerAPI sapi;
        
        private Dictionary<string, GraveData> graves = new Dictionary<string, GraveData>();
        
        private const long PROTECTION_TIME_MS = 15 * 60 * 1000;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            
            api.Event.PlayerDeath += OnPlayerDeath;
            api.Event.DidUseBlock += OnDidUseBlock;
            
            api.Event.GameWorldSave += OnSaveGame;
            api.Event.SaveGameLoaded += OnLoadGame;
            
            api.Event.BreakBlock += OnBreakBlock;

            api.Event.RegisterGameTickListener(OnGraveCleanupTick, 1000);
        }

        private void OnGraveCleanupTick(float dt)
        {
            List<string> gravesToRemove = new List<string>();

            foreach (var kvp in graves)
            {
                string[] parts = kvp.Key.Split(',');
                if (parts.Length != 3) continue;
                
                BlockPos pos = new BlockPos(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));

                BlockEntity be = sapi.World.BlockAccessor.GetBlockEntity(pos);

                if (be == null) 
                {
                    if (sapi.World.BlockAccessor.GetChunkAtBlockPos(pos) == null) continue;
                    gravesToRemove.Add(kvp.Key);
                    continue;
                }

                if (be is IBlockEntityContainer container)
                {
                    if (container.Inventory.Empty)
                    {
                        sapi.World.BlockAccessor.SetBlock(0, pos);
                        gravesToRemove.Add(kvp.Key);
                        sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/squish1"), pos.X, pos.Y, pos.Z);
                    }
                }
            }

            foreach (string key in gravesToRemove)
            {
                graves.Remove(key);
            }
        }

        private void OnSaveGame()
        {
            sapi.WorldManager.SaveGame.StoreData("GraveModData", SerializerUtil.Serialize(graves));
        }

        private void OnLoadGame()
        {
            byte[] data = sapi.WorldManager.SaveGame.GetData("GraveModData");
            if (data != null) graves = SerializerUtil.Deserialize<Dictionary<string, GraveData>>(data);
        }

        private void OnPlayerDeath(IServerPlayer player, DamageSource damageSource)
        {
            BlockPos deathPos = player.Entity.Pos.AsBlockPos;
            if (deathPos.Y < 1) deathPos.Y = 1;

            while (sapi.World.BlockAccessor.GetBlock(deathPos).BlockMaterial == EnumBlockMaterial.Mantle && deathPos.Y < sapi.World.BlockAccessor.MapSizeY)
            {
                deathPos.Y++;
            }

            Block chestBlock = sapi.World.GetBlock(new AssetLocation("game:chest-east"));
            sapi.World.BlockAccessor.SetBlock(chestBlock.Id, deathPos);

            BlockEntity be = sapi.World.BlockAccessor.GetBlockEntity(deathPos);
            
            if (be is IBlockEntityContainer chest)
            {
                string posKey = $"{deathPos.X},{deathPos.Y},{deathPos.Z}"; 
                
                graves[posKey] = new GraveData() 
                { 
                    OwnerUID = player.PlayerUID, 
                    OwnerName = player.PlayerName, 
                    CreationTime = sapi.World.ElapsedMilliseconds 
                };

                TransferEverySingleItem(player, chest.Inventory, deathPos);
                
                be.MarkDirty(true);
            }

            sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, $"<strong>Túmulo criado em: {deathPos.X}, {deathPos.Y}, {deathPos.Z}</strong>", EnumChatType.Notification);
        }

        private void TransferEverySingleItem(IServerPlayer player, IInventory targetInv, BlockPos pos)
        {
            // 1. Itera sobre inventários principais
            foreach (var inventory in player.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == "creative") continue;
                MoveItemsFromInventory(inventory, targetInv, pos);
            }

            // 2. CORREÇÃO: Pega o item do cursor acessando o inventário "mouse"
            IInventory mouseInv = player.InventoryManager.GetOwnInventory("mouse");
            if (mouseInv != null && !mouseInv[0].Empty)
            {
                // O inventário do mouse tem apenas 1 slot (índice 0)
                ItemStack mouseStack = mouseInv[0].TakeOutWhole();
                ItemStack remainder = AddItemToInventory(targetInv, mouseStack);
                
                if (remainder != null && remainder.StackSize > 0)
                {
                    sapi.World.SpawnItemEntity(remainder, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                mouseInv[0].MarkDirty();
            }
        }

        private void MoveItemsFromInventory(IInventory source, IInventory target, BlockPos pos)
        {
            foreach (var slot in source)
            {
                if (!slot.Empty)
                {
                    ItemStack stack = slot.TakeOutWhole();
                    ItemStack remainder = AddItemToInventory(target, stack);

                    if (remainder != null && remainder.StackSize > 0)
                    {
                        sapi.World.SpawnItemEntity(remainder, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                    slot.MarkDirty();
                }
            }
        }

        private ItemStack AddItemToInventory(IInventory inv, ItemStack stack)
        {
            // Tentar empilhar
            foreach (var slot in inv)
            {
                if (stack.StackSize <= 0) break;
                
                if (!slot.Empty && slot.Itemstack.Equals(sapi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    int space = slot.MaxSlotStackSize - slot.Itemstack.StackSize;
                    int toAdd = Math.Min(space, stack.StackSize);
                    if (toAdd > 0)
                    {
                        slot.Itemstack.StackSize += toAdd;
                        stack.StackSize -= toAdd;
                        slot.MarkDirty();
                    }
                }
            }

            // Tentar slots vazios
            if (stack.StackSize > 0)
            {
                foreach (var slot in inv)
                {
                    if (stack.StackSize <= 0) break;

                    if (slot.Empty)
                    {
                        int toAdd = Math.Min(slot.MaxSlotStackSize, stack.StackSize);
                        slot.Itemstack = stack.Clone();
                        slot.Itemstack.StackSize = toAdd;
                        stack.StackSize -= toAdd;
                        slot.MarkDirty();
                    }
                }
            }
            
            return (stack.StackSize > 0) ? stack : null;
        }

        private void OnDidUseBlock(IServerPlayer player, BlockSelection blockSel)
        {
            if (blockSel == null) return;
            string posKey = $"{blockSel.Position.X},{blockSel.Position.Y},{blockSel.Position.Z}";

            if (graves.TryGetValue(posKey, out GraveData data))
            {
                if (player.PlayerUID == data.OwnerUID) return;

                long timeDiff = sapi.World.ElapsedMilliseconds - data.CreationTime;
                if (timeDiff < PROTECTION_TIME_MS)
                {
                    int minutesLeft = (int)((PROTECTION_TIME_MS - timeDiff) / 1000 / 60);
                    
                    BlockEntity be = sapi.World.BlockAccessor.GetBlockEntity(blockSel.Position);
                    if (be is IBlockEntityContainer container)
                    {
                        player.InventoryManager.CloseInventory(container.Inventory);
                    }
                    
                    player.SendMessage(GlobalConstants.GeneralChatGroup, $"Túmulo de {data.OwnerName}. Protegido por {minutesLeft} min.", EnumChatType.Notification);
                }
            }
        }

        private void OnBreakBlock(IServerPlayer player, BlockSelection blockSel, ref float dropQuantityMultiplier, ref EnumHandling handling)
        {
            if (blockSel == null) return;
            string posKey = $"{blockSel.Position.X},{blockSel.Position.Y},{blockSel.Position.Z}";
            
            if (graves.ContainsKey(posKey))
            {
                graves.Remove(posKey);
            }
        }
    }
}
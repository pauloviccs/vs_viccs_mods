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
            // 1. Coletar TODOS os itens do jogador primeiro
            List<ItemStack> allItems = CollectAllItems(player);

            if (allItems.Count == 0) return; // Nada para guardar

            BlockPos startPos = player.Entity.Pos.AsBlockPos;
            Block chestBlock = sapi.World.GetBlock(new AssetLocation("game:chest-east"));
            
            // HashSet para evitar colocar graves na mesma posição no mesmo tick (caso bizarro)
            HashSet<BlockPos> usedPositions = new HashSet<BlockPos>();

            // 2. Loop para criar quantos baús forem necessários
            while (allItems.Count > 0)
            {
                // Encontrar posição válida mais próxima
                BlockPos gravePos = FindValidGravePosition(startPos, usedPositions, chestBlock);
                
                if (gravePos == null)
                {
                    // Fallback extremo: se não achar lugar, dropa o resto no chão onde morreu
                    foreach (var stack in allItems)
                    {
                        sapi.World.SpawnItemEntity(stack, startPos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                    sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, "Não foi possível encontrar espaço seguro para todos os itens. Alguns foram dropados.", EnumChatType.Notification);
                    break;
                }

                usedPositions.Add(gravePos);

                // Colocar o bloco
                sapi.World.BlockAccessor.SetBlock(chestBlock.Id, gravePos);
                
                BlockEntity be = sapi.World.BlockAccessor.GetBlockEntity(gravePos);
                if (be is IBlockEntityContainer chest)
                {
                    // Preencher este baú
                    allItems = FillChest(chest.Inventory, allItems);

                    // Registrar dados do túmulo
                    string posKey = $"{gravePos.X},{gravePos.Y},{gravePos.Z}";
                    graves[posKey] = new GraveData()
                    {
                        OwnerUID = player.PlayerUID,
                        OwnerName = player.PlayerName,
                        CreationTime = sapi.World.ElapsedMilliseconds
                    };
                    
                    be.MarkDirty(true);
                    sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, $"<strong>Túmulo criado em: {gravePos.X}, {gravePos.Y}, {gravePos.Z}</strong>", EnumChatType.Notification);
                }
                else
                {
                    // Se falhar ao pegar o BE, tenta próxima posição (segurança)
                    continue; 
                }
            }
        }

        private List<ItemStack> CollectAllItems(IServerPlayer player)
        {
            List<ItemStack> items = new List<ItemStack>();

            // Inventários principais
            foreach (var inventory in player.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == "creative") continue;

                foreach (var slot in inventory)
                {
                    if (!slot.Empty)
                    {
                        items.Add(slot.TakeOutWhole());
                        slot.MarkDirty();
                    }
                }
            }

            // Mouse cursor
            IInventory mouseInv = player.InventoryManager.GetOwnInventory("mouse");
            if (mouseInv != null && !mouseInv[0].Empty)
            {
                items.Add(mouseInv[0].TakeOutWhole());
                mouseInv[0].MarkDirty();
            }

            return items;
        }

        private List<ItemStack> FillChest(IInventory chestInv, List<ItemStack> itemsToDistribute)
        {
            List<ItemStack> remaining = new List<ItemStack>();

            foreach (var stack in itemsToDistribute)
            {
                ItemStack leftOver = AddItemToInventory(chestInv, stack);
                if (leftOver != null && leftOver.StackSize > 0)
                {
                    remaining.Add(leftOver);
                }
            }
            
            return remaining;
        }

        private BlockPos FindValidGravePosition(BlockPos startPos, HashSet<BlockPos> exclude, Block chestBlock)
        {
            // Busca em espiral ou cubo expandido perto da morte
            // Prioridade: O próprio local -> Acima -> Ao redor
            
            // 1. Tentar o local exato (se seguro)
            if (IsSpotSafe(startPos, exclude, chestBlock)) return startPos.Copy();

            // 2. Tentar subir até encontrar ar (para não ficar enterrado na pedra)
            BlockPos upPos = startPos.Copy();
            for (int i = 0; i < 5; i++)
            {
                upPos.Up();
                if (IsSpotSafe(upPos, exclude, chestBlock)) return upPos;
            }

            // 3. Busca em raio (Cubo 5x5x5)
            int radius = 3;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -1; dy <= radius; dy++) // Prefere subir a descer muito
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        BlockPos testPos = startPos.AddCopy(dx, dy, dz);
                        if (IsSpotSafe(testPos, exclude, chestBlock)) return testPos;
                    }
                }
            }

            return null;
        }

        private bool IsSpotSafe(BlockPos pos, HashSet<BlockPos> exclude, Block chestBlock)
        {
            if (exclude.Contains(pos)) return false;
            if (pos.Y < 1 || pos.Y >= sapi.World.BlockAccessor.MapSizeY) return false;

            Block existingBlock = sapi.World.BlockAccessor.GetBlock(pos);

            // Verifica se o bloco atual pode ser substituído pelo baú
            // Use IsReplacableBy se disponível, ou lógica manual
            if (existingBlock.IsReplacableBy(chestBlock)) return true;
            
            // Manual check for common replaceable blocks if IsReplacableBy isn't enough
            if (existingBlock.Id == 0) return true; // Air
            if (existingBlock.BlockMaterial == EnumBlockMaterial.Plant) return true;
            if (existingBlock.BlockMaterial == EnumBlockMaterial.Snow) return true;
            if (existingBlock.Code.Path.Contains("water")) return true; // Replace water is usually ok for graves

            return false;
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
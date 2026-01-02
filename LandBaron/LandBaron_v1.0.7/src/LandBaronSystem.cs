using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Newtonsoft.Json; // <--- AQUI ESTÁ A MÁGICA

namespace LandBaron
{
    // --- CLASSES DE DADOS ---
    public class ModConfig
    {
        public string CurrencyItemCode = "game:gear-rusty"; 
        public int BaseChunkCost = 10; 
        public float CostMultiplier = 1.5f; 
    }

    public class LandClaim
    {
        public string OwnerUID;
        public string OwnerName;
        public List<string> AllowedUIDs = new List<string>(); 
        public bool PublicAccess = false; 
        public int SalePrice = -1; 
    }

    public class LandBaronSystem : ModSystem
    {
        private ICoreServerAPI sapi;
        private ModConfig config;

        // DADOS PERSISTENTES
        private Dictionary<string, LandClaim> claims = new Dictionary<string, LandClaim>();
        private HashSet<string> bankBlocks = new HashSet<string>();
        private Dictionary<string, int> bankAccounts = new Dictionary<string, int>();

        // DADOS TEMPORÁRIOS (RADAR)
        private Dictionary<string, string> playerLastChunk = new Dictionary<string, string>();

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;

            try {
                config = api.LoadModConfig<ModConfig>("landbaron_config.json");
                if (config == null) { config = new ModConfig(); api.StoreModConfig(config, "landbaron_config.json"); }
            } catch { config = new ModConfig(); }

            api.Event.SaveGameLoaded += OnLoadGame;
            api.Event.GameWorldSave += OnSaveGame;
            
            api.Event.PlayerJoin += OnPlayerJoin; 
            api.Event.PlayerLeave += OnPlayerLeave;

            api.Event.CanUseBlock += OnCanUseBlock; 
            api.Event.BreakBlock += OnBreakBlock; 
            api.Event.DidPlaceBlock += OnDidPlaceBlock; 

            api.Event.RegisterGameTickListener(OnVacuumTick, 250); 
            api.Event.RegisterGameTickListener(OnMovementTick, 1000); 

            RegisterCommands();
        }

        // --- PERSISTÊNCIA CORRIGIDA (Newtonsoft Direto) ---
        private void OnSaveGame()
        {
            // Serializa usando Newtonsoft direto
            sapi.WorldManager.SaveGame.StoreData("LandBaron_Claims", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(claims)));
            sapi.WorldManager.SaveGame.StoreData("LandBaron_Banks", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(bankBlocks)));
            sapi.WorldManager.SaveGame.StoreData("LandBaron_Accounts", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(bankAccounts)));
        }

        private void OnLoadGame()
        {
            try 
            {
                byte[] dataClaims = sapi.WorldManager.SaveGame.GetData("LandBaron_Claims");
                if (dataClaims != null) 
                {
                    string json = Encoding.UTF8.GetString(dataClaims);
                    // Deserializa usando Newtonsoft direto (1 argumento, sem erro)
                    claims = JsonConvert.DeserializeObject<Dictionary<string, LandClaim>>(json) ?? new Dictionary<string, LandClaim>();
                }

                byte[] dataBanks = sapi.WorldManager.SaveGame.GetData("LandBaron_Banks");
                if (dataBanks != null) 
                {
                    string json = Encoding.UTF8.GetString(dataBanks);
                    bankBlocks = JsonConvert.DeserializeObject<HashSet<string>>(json) ?? new HashSet<string>();
                }

                byte[] dataAcc = sapi.WorldManager.SaveGame.GetData("LandBaron_Accounts");
                if (dataAcc != null) 
                {
                    string json = Encoding.UTF8.GetString(dataAcc);
                    bankAccounts = JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[LandBaron] Erro ao carregar dados: {ex.Message}. Iniciando banco de dados vazio.");
                claims = new Dictionary<string, LandClaim>();
                bankBlocks = new HashSet<string>();
                bankAccounts = new Dictionary<string, int>();
            }
        }

        private void OnPlayerJoin(IServerPlayer player)
        {
            UpdateVisualBalance(player);
            playerLastChunk[player.PlayerUID] = ""; 
        }

        private void OnPlayerLeave(IServerPlayer player)
        {
            if (playerLastChunk.ContainsKey(player.PlayerUID))
                playerLastChunk.Remove(player.PlayerUID);
        }

        // --- RADAR DE FRONTEIRA ---
        private void OnMovementTick(float dt)
        {
            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity == null) continue;

                string currentKey = GetChunkKey(player.Entity.Pos.AsBlockPos);
                
                if (!playerLastChunk.TryGetValue(player.PlayerUID, out string lastKey) || lastKey != currentKey)
                {
                    playerLastChunk[player.PlayerUID] = currentKey;
                    NotifyEntry(player, currentKey);
                }
            }
        }

        private void NotifyEntry(IServerPlayer player, string chunkKey)
        {
            if (claims.TryGetValue(chunkKey, out LandClaim claim))
            {
                int totalOwned = claims.Values.Count(c => c.OwnerUID == claim.OwnerUID);
                string saleStatus = claim.SalePrice > -1 ? $" | 💲 À VENDA: {claim.SalePrice}" : "";
                string subText = $"📊 Império: {totalOwned} terrenos{saleStatus}";
                
                player.SendMessage(GlobalConstants.GeneralChatGroup, $"🏰 **Terras de {claim.OwnerName}**", EnumChatType.Notification);
                player.SendMessage(GlobalConstants.GeneralChatGroup, subText, EnumChatType.OwnMessage);
            }
        }

        // --- HELPER FINANCEIRO ---
        private int GetBalance(string uid)
        {
            if (bankAccounts.TryGetValue(uid, out int bal)) return bal;
            return 0;
        }

        private void AddBalance(string uid, int amount)
        {
            if (!bankAccounts.ContainsKey(uid)) bankAccounts[uid] = 0;
            bankAccounts[uid] += amount;
            
            var player = sapi.World.PlayerByUid(uid) as IServerPlayer;
            if (player != null) UpdateVisualBalance(player);
        }

        private bool TrySpend(string uid, int amount)
        {
            int current = GetBalance(uid);
            if (current >= amount)
            {
                bankAccounts[uid] = current - amount;
                var player = sapi.World.PlayerByUid(uid) as IServerPlayer;
                if (player != null) UpdateVisualBalance(player);
                return true;
            }
            return false;
        }

        private void UpdateVisualBalance(IServerPlayer player)
        {
            int bal = GetBalance(player.PlayerUID);
            player.Entity.WatchedAttributes.SetInt("landBaron_balance", bal);
            player.Entity.WatchedAttributes.MarkPathDirty("landBaron_balance");
        }

        // --- SISTEMA VACUUM ---
        private void OnVacuumTick(float dt)
        {
            if (bankBlocks.Count == 0 || sapi.World.AllOnlinePlayers.Length == 0) return;

            foreach (var entity in sapi.World.LoadedEntities.Values)
            {
                if (entity is EntityItem itemEntity && itemEntity.Alive)
                {
                    ItemStack stack = itemEntity.Itemstack;
                    if (stack == null || stack.Collectible == null) continue;
                    
                    if (stack.Collectible.Code.ToString().Contains(config.CurrencyItemCode))
                    {
                        BlockPos pos = itemEntity.Pos.AsBlockPos;
                        string key = pos.ToString();
                        string keyBelow = pos.DownCopy().ToString();

                        if (bankBlocks.Contains(key) || bankBlocks.Contains(keyBelow))
                        {
                            AbsorbItem(itemEntity);
                        }
                    }
                }
            }
        }

        private void AbsorbItem(EntityItem itemEntity)
        {
            IPlayer nearestPlayer = null;
            double minDistance = 5.0; 

            foreach (IPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity == null) continue;
                double dist = player.Entity.Pos.DistanceTo(itemEntity.Pos.XYZ);
                if (dist < minDistance) { minDistance = dist; nearestPlayer = player; }
            }
            
            if (nearestPlayer != null)
            {
                int amount = itemEntity.Itemstack.StackSize;
                AddBalance(nearestPlayer.PlayerUID, amount); 

                ((IServerPlayer)nearestPlayer).SendMessage(GlobalConstants.GeneralChatGroup, $"[Banco] Depositado: {amount}. Novo Saldo: {GetBalance(nearestPlayer.PlayerUID)}", EnumChatType.Notification);
                
                sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/cashregister"), nearestPlayer.Entity, null, true, 32);
                
                SimpleParticleProperties particles = new SimpleParticleProperties(
                    5f, 10f, ColorUtil.ToRgba(255, 100, 255, 100), new Vec3d(), new Vec3d(),
                    new Vec3f(-0.2f, 0.5f, -0.2f), new Vec3f(0.2f, 1f, 0.2f), 1f, 0f, 0.5f, 0.5f, EnumParticleModel.Cube
                );
                particles.MinPos = itemEntity.Pos.XYZ;
                sapi.World.SpawnParticles(particles);

                itemEntity.Die();
            }
        }

        // --- SISTEMA INTERAÇÃO ---
        private bool OnCanUseBlock(IServerPlayer player, BlockSelection blockSel)
        {
            if (blockSel == null) return true;
            string posKey = blockSel.Position.ToString();

            if (bankBlocks.Contains(posKey))
            {
                if (player.Entity.Controls.Sneak && player.InventoryManager.ActiveHotbarSlot.Empty)
                {
                     PerformWithdraw(player, 64);
                }
                else
                {
                    CheckBalance(player);
                }
                return false; 
            }

            if (!HasPermission(player, blockSel.Position, true))
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "Você não tem permissão para interagir aqui.", EnumChatType.Notification);
                return false;
            }

            return true;
        }

        private void CheckBalance(IServerPlayer player)
        {
            int balance = GetBalance(player.PlayerUID);
            player.SendMessage(GlobalConstants.GeneralChatGroup, $"[Banco] Seu Saldo: {balance} moedas.", EnumChatType.Notification);
        }

        private void PerformWithdraw(IServerPlayer player, int requestedAmount)
        {
            int balance = GetBalance(player.PlayerUID);
            if (balance <= 0)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "[Banco] Saldo insuficiente.", EnumChatType.Notification);
                return;
            }

            var item = sapi.World.GetItem(new AssetLocation(config.CurrencyItemCode));
            if (item == null) 
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "[Erro] Moeda configurada não existe.", EnumChatType.Notification);
                return;
            }

            int toWithdraw = Math.Min(balance, requestedAmount);

            if (TrySpend(player.PlayerUID, toWithdraw))
            {
                ItemStack moneyStack = new ItemStack(item, toWithdraw);
                
                if (!player.InventoryManager.TryGiveItemstack(moneyStack))
                {
                    sapi.World.SpawnItemEntity(moneyStack, player.Entity.Pos.XYZ);
                    player.SendMessage(GlobalConstants.GeneralChatGroup, $"[Banco] Inventário cheio! Saque de {toWithdraw} jogado no chão.", EnumChatType.Notification);
                }
                else
                {
                    player.SendMessage(GlobalConstants.GeneralChatGroup, $"[Banco] Saque: {toWithdraw}. Restante: {GetBalance(player.PlayerUID)}", EnumChatType.Notification);
                }
                
                sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/cashregister"), player.Entity, null, true, 32);
            }
        }

        // --- PROTEÇÃO ---
        private void OnBreakBlock(IServerPlayer player, BlockSelection blockSel, ref float dropQuantityMultiplier, ref EnumHandling handling)
        {
            if (blockSel == null) return;
            string posKey = blockSel.Position.ToString();

            if (bankBlocks.Contains(posKey))
            {
                if (player.Role.Code == "admin")
                {
                    bankBlocks.Remove(posKey);
                    player.SendMessage(GlobalConstants.GeneralChatGroup, "[Admin] Banco removido.", EnumChatType.Notification);
                }
                else
                {
                    player.SendMessage(GlobalConstants.GeneralChatGroup, "Propriedade do Banco Federal!", EnumChatType.Notification);
                    handling = EnumHandling.PreventDefault;
                    return;
                }
            }

            if (!HasPermission(player, blockSel.Position, false))
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "Esta terra pertence a outra pessoa.", EnumChatType.Notification);
                handling = EnumHandling.PreventDefault;
            }
        }

        private void OnDidPlaceBlock(IServerPlayer player, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            if (blockSel == null) return;

            if (!HasPermission(player, blockSel.Position, false))
            {
                sapi.World.BlockAccessor.SetBlock(0, blockSel.Position);
                
                if (player.WorldData.CurrentGameMode != EnumGameMode.Creative && withItemStack != null)
                {
                    ItemStack returnStack = withItemStack.Clone();
                    returnStack.StackSize = 1; 
                    if (!player.InventoryManager.TryGiveItemstack(returnStack))
                    {
                        sapi.World.SpawnItemEntity(returnStack, player.Entity.Pos.XYZ);
                    }
                }
                player.SendMessage(GlobalConstants.GeneralChatGroup, "Você não pode construir aqui.", EnumChatType.Notification);
            }
        }

        private bool HasPermission(IServerPlayer player, BlockPos pos, bool isInteraction)
        {
            if (player.Role.Code == "admin") return true;

            int chunkX = pos.X / 32;
            int chunkZ = pos.Z / 32;
            string key = $"{chunkX},{chunkZ}";

            if (claims.TryGetValue(key, out LandClaim claim))
            {
                if (claim.OwnerUID == player.PlayerUID) return true;
                if (claim.AllowedUIDs.Contains(player.PlayerUID)) return true;
                if (claim.PublicAccess && isInteraction) return true;
                return false; 
            }
            return true; 
        }

        // --- COMANDOS ---
        private void RegisterCommands()
        {
            var parsers = sapi.ChatCommands.Parsers;

            // Banco Criar
            sapi.ChatCommands.Create("banco")
                .RequiresPrivilege(Privilege.controlserver) 
                .BeginSubCommand("criar")
                    .HandleWith((args) => {
                        var player = args.Caller.Player as IServerPlayer;
                        var blockSel = player.CurrentBlockSelection;
                        if (blockSel == null) return TextCommandResult.Error("Olhe para um bloco.");
                        string posKey = blockSel.Position.ToString();
                        bankBlocks.Add(posKey);
                        sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/latch"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, null, true, 32);
                        return TextCommandResult.Success("Caixa Eletrônico criado!");
                    })
                .EndSubCommand();

            // Saldo
            sapi.ChatCommands.Create("saldo")
                .WithDescription("Verifica seu saldo")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith((args) => {
                    var player = args.Caller.Player as IServerPlayer;
                    CheckBalance(player);
                    return TextCommandResult.Success();
                });

            // Sacar
            sapi.ChatCommands.Create("sacar")
                .WithDescription("Saca dinheiro")
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(parsers.Int("quantidade"))
                .HandleWith((args) => {
                    var player = args.Caller.Player as IServerPlayer;
                    int amount = (int)args.Parsers[0].GetValue();
                    if (amount <= 0) return TextCommandResult.Error("Valor invalido.");
                    
                    PerformWithdraw(player, amount);
                    return TextCommandResult.Success();
                });

            // Depositar
            sapi.ChatCommands.Create("depositar")
                .WithDescription("Deposita o item da mao")
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(parsers.Int("quantidade"))
                .HandleWith((args) => {
                    var player = args.Caller.Player as IServerPlayer;
                    int amount = (int)args.Parsers[0].GetValue();
                    if (amount <= 0) return TextCommandResult.Error("Valor invalido.");
                    
                    ItemSlot activeSlot = player.InventoryManager.ActiveHotbarSlot;
                    if (activeSlot.Empty) return TextCommandResult.Error("Segure o dinheiro na mao para depositar.");

                    if (!activeSlot.Itemstack.Collectible.Code.ToString().Contains(config.CurrencyItemCode))
                        return TextCommandResult.Error($"Item invalido. O banco aceita apenas: {config.CurrencyItemCode}");

                    int toDeposit = Math.Min(amount, activeSlot.StackSize);

                    activeSlot.TakeOut(toDeposit);
                    activeSlot.MarkDirty(); 

                    AddBalance(player.PlayerUID, toDeposit);
                    
                    sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/cashregister"), player.Entity, null, true, 32);
                    return TextCommandResult.Success($"Depositado: {toDeposit}. Novo Saldo: {GetBalance(player.PlayerUID)}");
                });

            // Terreno
            var terrenoCmd = sapi.ChatCommands.Create("terreno")
                .WithDescription("Sistema de Terras")
                .RequiresPrivilege(Privilege.chat); 

            terrenoCmd.BeginSubCommand("comprar").HandleWith(CmdComprar).EndSubCommand();
            terrenoCmd.BeginSubCommand("vender").WithArgs(parsers.Int("preco")).HandleWith(CmdVender).EndSubCommand();
            terrenoCmd.BeginSubCommand("transferir").WithArgs(parsers.Word("jogador")).HandleWith(CmdTransferir).EndSubCommand();
            terrenoCmd.BeginSubCommand("abandonar").HandleWith(CmdAbandonar).EndSubCommand();
            terrenoCmd.BeginSubCommand("cancelarvenda").HandleWith(CmdCancelarVenda).EndSubCommand();
            terrenoCmd.BeginSubCommand("ver").HandleWith(CmdVer).EndSubCommand();
            terrenoCmd.BeginSubCommand("amigo").WithArgs(parsers.Word("nome")).HandleWith(CmdAmigo).EndSubCommand();
        }

        private TextCommandResult CmdComprar(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            string key = GetChunkKey(player.Entity.Pos.AsBlockPos);

            if (claims.TryGetValue(key, out LandClaim claim))
            {
                if (claim.SalePrice > -1)
                {
                    if (claim.OwnerUID == player.PlayerUID) return TextCommandResult.Error("Você já é o dono.");

                    int price = claim.SalePrice;
                    if (!TrySpend(player.PlayerUID, price)) return TextCommandResult.Error($"Saldo insuficiente. Preço: {price}");

                    AddBalance(claim.OwnerUID, price);
                    
                    string oldOwnerName = claim.OwnerName;
                    claim.OwnerUID = player.PlayerUID;
                    claim.OwnerName = player.PlayerName;
                    claim.SalePrice = -1;
                    claim.AllowedUIDs.Clear();

                    ShowChunkBorders(player);
                    return TextCommandResult.Success($"Você comprou as terras de {oldOwnerName} por {price} moedas!");
                }
                else
                {
                    return TextCommandResult.Error($"Esta terra pertence a {claim.OwnerName} e NÃO está à venda.");
                }
            }
            else
            {
                int ownedCount = claims.Values.Count(c => c.OwnerUID == player.PlayerUID);
                int cost = (int)(config.BaseChunkCost * Math.Pow(config.CostMultiplier, ownedCount));

                if (!TrySpend(player.PlayerUID, cost)) return TextCommandResult.Error($"Saldo insuficiente. Custo: {cost}");
                
                claims[key] = new LandClaim() { OwnerUID = player.PlayerUID, OwnerName = player.PlayerName };
                ShowChunkBorders(player);
                return TextCommandResult.Success($"Terra comprada por {cost} moedas!");
            }
        }

        private TextCommandResult CmdVender(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            string key = GetChunkKey(player.Entity.Pos.AsBlockPos);
            int price = (int)args.Parsers[0].GetValue();

            if (price <= 0) return TextCommandResult.Error("O preço deve ser maior que zero.");

            if (claims.TryGetValue(key, out LandClaim claim))
            {
                if (claim.OwnerUID != player.PlayerUID) return TextCommandResult.Error("Esta terra não é sua.");
                claim.SalePrice = price;
                ShowChunkBorders(player);
                return TextCommandResult.Success($"Terra colocada à venda por {price} moedas! Qualquer um pode comprar agora.");
            }
            return TextCommandResult.Error("Você precisa comprar a terra antes de vender.");
        }

        private TextCommandResult CmdCancelarVenda(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            string key = GetChunkKey(player.Entity.Pos.AsBlockPos);
            if (claims.TryGetValue(key, out LandClaim claim))
            {
                if (claim.OwnerUID != player.PlayerUID) return TextCommandResult.Error("Esta terra não é sua.");
                claim.SalePrice = -1;
                return TextCommandResult.Success("Venda cancelada.");
            }
            return TextCommandResult.Error("Esta terra não tem dono.");
        }

        private TextCommandResult CmdTransferir(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            string targetName = args.Parsers[0].GetValue() as string;
            string key = GetChunkKey(player.Entity.Pos.AsBlockPos);

            if (claims.TryGetValue(key, out LandClaim claim))
            {
                if (claim.OwnerUID != player.PlayerUID) return TextCommandResult.Error("Esta terra não é sua.");
                
                IPlayer target = sapi.World.AllPlayers.FirstOrDefault(p => p.PlayerName.ToLower() == targetName.ToLower());
                if (target == null) return TextCommandResult.Error("Jogador não encontrado.");

                claim.OwnerUID = target.PlayerUID;
                claim.OwnerName = target.PlayerName;
                claim.SalePrice = -1;
                claim.AllowedUIDs.Clear();

                return TextCommandResult.Success($"Terra transferida para {target.PlayerName} com sucesso!");
            }
            return TextCommandResult.Error("Você não possui terras aqui.");
        }

        private TextCommandResult CmdAbandonar(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            string key = GetChunkKey(player.Entity.Pos.AsBlockPos);

            if (claims.TryGetValue(key, out LandClaim claim))
            {
                if (claim.OwnerUID != player.PlayerUID) return TextCommandResult.Error("Esta terra não é sua.");
                claims.Remove(key);
                ShowChunkBorders(player);
                return TextCommandResult.Success("Terra abandonada.");
            }
            return TextCommandResult.Error("Você não possui terras aqui.");
        }

        private TextCommandResult CmdVer(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            string key = GetChunkKey(player.Entity.Pos.AsBlockPos);
            ShowChunkBorders(player);

            if (claims.TryGetValue(key, out LandClaim claim))
            {
                string status = claim.SalePrice > -1 ? $"[À VENDA: {claim.SalePrice}$]" : "[Não está à venda]";
                return TextCommandResult.Success($"Dono: {claim.OwnerName} {status} | Público: {claim.PublicAccess}");
            }
            return TextCommandResult.Success("Terra Selvagem (Sem Dono).");
        }

        private TextCommandResult CmdAmigo(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            string friendName = args.Parsers[0].GetValue() as string;
            string key = GetChunkKey(player.Entity.Pos.AsBlockPos);
            
            IPlayer friend = sapi.World.AllPlayers.FirstOrDefault(p => p.PlayerName.ToLower() == friendName.ToLower());
            if (friend == null) return TextCommandResult.Error("Jogador não encontrado.");

            if (claims.TryGetValue(key, out LandClaim claim))
            {
                if (claim.OwnerUID != player.PlayerUID) return TextCommandResult.Error("Esta terra não é sua.");
                
                if (!claim.AllowedUIDs.Contains(friend.PlayerUID))
                {
                    claim.AllowedUIDs.Add(friend.PlayerUID);
                    return TextCommandResult.Success($"{friendName} adicionado!");
                }
                else
                {
                    claim.AllowedUIDs.Remove(friend.PlayerUID);
                    return TextCommandResult.Success($"{friendName} removido.");
                }
            }
            return TextCommandResult.Error("Você não está em um terreno seu.");
        }

        private string GetChunkKey(BlockPos pos)
        {
            return $"{pos.X / 32},{pos.Z / 32}";
        }

        private void ShowChunkBorders(IServerPlayer player)
        {
            int chunkX = player.Entity.Pos.AsBlockPos.X / 32;
            int chunkZ = player.Entity.Pos.AsBlockPos.Z / 32;
            int startX = chunkX * 32;
            int startZ = chunkZ * 32;
            int y = player.Entity.Pos.AsBlockPos.Y;

            SimpleParticleProperties particles = new SimpleParticleProperties(
                3f, 5f, 
                ColorUtil.ToRgba(200, 255, 215, 0), 
                new Vec3d(), new Vec3d(), 
                new Vec3f(-0.1f, -0.1f, -0.1f), new Vec3f(0.1f, 0.5f, 0.1f), 
                10.0f, 
                0f, 
                0.5f, 1.0f, 
                EnumParticleModel.Cube
            );
            particles.SelfPropelled = true; 

            for (int i = 0; i <= 32; i += 2)
            {
                SpawnP(startX + i, y, startZ, particles);      
                SpawnP(startX + i, y, startZ + 32, particles); 
                SpawnP(startX, y, startZ + i, particles);      
                SpawnP(startX + 32, y, startZ + i, particles); 
            }
        }

        private void SpawnP(int x, int y, int z, SimpleParticleProperties p)
        {
            p.MinPos = new Vec3d(x, y, z);
            sapi.World.SpawnParticles(p);
        }
    }
}
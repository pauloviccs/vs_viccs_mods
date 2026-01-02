using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Newtonsoft.Json;
using ProtoBuf;

namespace LandBaron
{
    // --- DADOS E REDE ---
    public class ModConfig
    {
        public string CurrencyItemCode = "game:gear-rusty";
        public int BaseChunkCost = 10;
        public float CostMultiplier = 1.5f;
    }

    [ProtoContract]
    public class LandClaim
    {
        [ProtoMember(1)] public string OwnerUID;
        [ProtoMember(2)] public string OwnerName;
        [ProtoMember(3)] public List<string> AllowedUIDs = new List<string>();
        [ProtoMember(4)] public bool PublicAccess = false;
        [ProtoMember(5)] public int SalePrice = -1;
    }

    [ProtoContract]
    public class SyncClaimsPacket
    {
        [ProtoMember(1)] public Dictionary<string, LandClaim> Claims;
    }

    [ProtoContract]
    public class PlaySoundPacket
    {
        [ProtoMember(1)] public string SoundName;
    }

    [ProtoContract]
    public class HighlightChunkPacket
    {
        [ProtoMember(1)] public bool Show;
    }

    public class LandBaronSystem : ModSystem
    {
        // APIs
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;
        private IServerNetworkChannel serverChannel;
        private IClientNetworkChannel clientChannel;

        // Dados
        private ModConfig config;
        private Dictionary<string, LandClaim> claims = new Dictionary<string, LandClaim>();
        private HashSet<string> bankBlocks = new HashSet<string>();
        private Dictionary<string, int> bankAccounts = new Dictionary<string, int>();

        // Cliente Vars
        private string lastChunkKeyClient = "";
        private bool showBorders = false;
        private long borderTimer = 0;
        
        // private TerritoryHud hud;

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel("landbaron")
                .RegisterMessageType<SyncClaimsPacket>()
                .RegisterMessageType<PlaySoundPacket>()
                .RegisterMessageType<HighlightChunkPacket>();
        }

        // =========================================================================================
        //                                      LADO DO SERVIDOR
        // =========================================================================================
        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            serverChannel = api.Network.GetChannel("landbaron");

            try {
                config = api.LoadModConfig<ModConfig>("landbaron_config.json");
                if (config == null) { config = new ModConfig(); api.StoreModConfig(config, "landbaron_config.json"); }
            } catch { config = new ModConfig(); }

            api.Event.SaveGameLoaded += OnLoadGame;
            api.Event.GameWorldSave += OnSaveGame;
            api.Event.PlayerJoin += OnPlayerJoinServer;
            
            api.Event.CanUseBlock += OnCanUseBlock;
            api.Event.BreakBlock += OnBreakBlock;
            api.Event.DidPlaceBlock += OnDidPlaceBlock;
            api.Event.RegisterGameTickListener(OnVacuumTick, 250);

            RegisterCommands();
        }

        private void OnPlayerJoinServer(IServerPlayer player)
        {
            UpdateVisualBalance(player);
            if (claims == null) claims = new Dictionary<string, LandClaim>();
            serverChannel.SendPacket(new SyncClaimsPacket() { Claims = claims }, player);
        }

        private void BroadcastClaims()
        {
            if (claims == null) claims = new Dictionary<string, LandClaim>();
            serverChannel.BroadcastPacket(new SyncClaimsPacket() { Claims = claims });
        }

        private void SendSound(IServerPlayer player, string sound)
        {
            serverChannel.SendPacket(new PlaySoundPacket() { SoundName = sound }, player);
        }

        // --- Persistência ---
        private void OnSaveGame()
        {
            sapi.WorldManager.SaveGame.StoreData("LandBaron_Claims", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(claims)));
            sapi.WorldManager.SaveGame.StoreData("LandBaron_Banks", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(bankBlocks)));
            sapi.WorldManager.SaveGame.StoreData("LandBaron_Accounts", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(bankAccounts)));
        }

        private void OnLoadGame()
        {
            try {
                var cData = sapi.WorldManager.SaveGame.GetData("LandBaron_Claims");
                if (cData != null) claims = JsonConvert.DeserializeObject<Dictionary<string, LandClaim>>(Encoding.UTF8.GetString(cData)) ?? new Dictionary<string, LandClaim>();
                
                var bData = sapi.WorldManager.SaveGame.GetData("LandBaron_Banks");
                if (bData != null) bankBlocks = JsonConvert.DeserializeObject<HashSet<string>>(Encoding.UTF8.GetString(bData)) ?? new HashSet<string>();

                var aData = sapi.WorldManager.SaveGame.GetData("LandBaron_Accounts");
                if (aData != null) bankAccounts = JsonConvert.DeserializeObject<Dictionary<string, int>>(Encoding.UTF8.GetString(aData)) ?? new Dictionary<string, int>();
            } catch {
                claims = new Dictionary<string, LandClaim>();
                bankBlocks = new HashSet<string>();
                bankAccounts = new Dictionary<string, int>();
            }
        }

        // --- Comandos ---
        private void RegisterCommands()
        {
             var parsers = sapi.ChatCommands.Parsers;
             
             // COMANDOS DE BANCO
             sapi.ChatCommands.Create("banco").RequiresPrivilege(Privilege.controlserver).BeginSubCommand("criar").HandleWith(a => {
                 bankBlocks.Add(((IServerPlayer)a.Caller.Player).CurrentBlockSelection.Position.ToString());
                 return TextCommandResult.Success("Banco criado");
             }).EndSubCommand();

             sapi.ChatCommands.Create("saldo").RequiresPrivilege(Privilege.chat).HandleWith(args => {
                 var p = args.Caller.Player as IServerPlayer;
                 CheckBalance(p);
                 return TextCommandResult.Success();
             });

             sapi.ChatCommands.Create("depositar").WithArgs(parsers.Int("qtd")).RequiresPrivilege(Privilege.chat).HandleWith(args => {
                 var p = args.Caller.Player as IServerPlayer;
                 int amount = (int)args.Parsers[0].GetValue();
                 if (amount <= 0) return TextCommandResult.Error("Valor inválido.");

                 ItemSlot activeSlot = p.InventoryManager.ActiveHotbarSlot;
                 if (activeSlot.Empty) return TextCommandResult.Error("Segure o dinheiro na mão.");
                 if (!activeSlot.Itemstack.Collectible.Code.ToString().Contains(config.CurrencyItemCode)) 
                     return TextCommandResult.Error("Item inválido.");

                 int toDeposit = Math.Min(amount, activeSlot.StackSize);
                 activeSlot.TakeOut(toDeposit);
                 activeSlot.MarkDirty();
                 AddBalance(p.PlayerUID, toDeposit);
                 SendSound(p, "game:sounds/effect/cashregister");
                 return TextCommandResult.Success($"Depositado: {toDeposit}. Novo saldo: {GetBalance(p.PlayerUID)}");
             });

             sapi.ChatCommands.Create("sacar").WithArgs(parsers.Int("qtd")).RequiresPrivilege(Privilege.chat).HandleWith(args => {
                 var p = args.Caller.Player as IServerPlayer;
                 int amount = (int)args.Parsers[0].GetValue();
                 if (amount <= 0) return TextCommandResult.Error("Valor inválido.");
                 PerformWithdraw(p, amount);
                 return TextCommandResult.Success();
             });
             
             // NOVO: TRANSFERIR
             sapi.ChatCommands.Create("transferir")
                .WithArgs(parsers.Word("jogador"), parsers.Int("qtd"))
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => {
                    var p = args.Caller.Player as IServerPlayer;
                    string targetName = (string)args.Parsers[0].GetValue();
                    int amount = (int)args.Parsers[1].GetValue();
                    
                    if (amount <= 0) return TextCommandResult.Error("Valor inválido.");
                    
                    IServerPlayer target = sapi.World.AllOnlinePlayers
                        .FirstOrDefault(x => x.PlayerName.ToLower() == targetName.ToLower()) as IServerPlayer;
                        
                    if (target == null) return TextCommandResult.Error("Jogador não encontrado ou offline.");
                    if (target.PlayerUID == p.PlayerUID) return TextCommandResult.Error("Não pode transferir para si mesmo.");

                    if (!TrySpend(p.PlayerUID, amount)) {
                         return TextCommandResult.Error($"Saldo insuficiente na conta.");
                    }
                    
                    AddBalance(target.PlayerUID, amount);
                    
                    p.SendMessage(GlobalConstants.GeneralChatGroup, $"Você transferiu {amount} para {target.PlayerName}.", EnumChatType.Notification);
                    target.SendMessage(GlobalConstants.GeneralChatGroup, $"Você recebeu {amount} de {p.PlayerName}.", EnumChatType.Notification);
                    
                    SendSound(p, "game:sounds/effect/cashregister");
                    SendSound(target, "game:sounds/effect/cashregister");
                    
                    return TextCommandResult.Success();
                });

             // COMANDOS DE TERRENO
             var t = sapi.ChatCommands.Create("terreno").RequiresPrivilege(Privilege.chat);
             t.BeginSubCommand("comprar").HandleWith(CmdComprar).EndSubCommand();
             t.BeginSubCommand("vender").WithArgs(parsers.Int("p")).HandleWith(CmdVender).EndSubCommand();
             t.BeginSubCommand("abandonar").HandleWith(CmdAbandonar).EndSubCommand();
             
             // NOVO: ADD PERMISSAO
             t.BeginSubCommand("add").WithArgs(parsers.Word("jogador")).HandleWith(args => {
                var player = args.Caller.Player as IServerPlayer;
                string targetName = (string)args.Parsers[0].GetValue();
                
                var target = sapi.World.AllOnlinePlayers.FirstOrDefault(x => x.PlayerName.ToLower() == targetName.ToLower());
                if (target == null) return TextCommandResult.Error("Jogador precisa estar online.");
                
                string targetUID = target.PlayerUID;
                if (targetUID == player.PlayerUID) return TextCommandResult.Error("Você já é o dono.");

                int count = 0;
                foreach(var kvp in claims) {
                    if (kvp.Value.OwnerUID == player.PlayerUID) {
                        if (!kvp.Value.AllowedUIDs.Contains(targetUID)) {
                            kvp.Value.AllowedUIDs.Add(targetUID);
                            count++;
                        }
                    }
                }
                
                BroadcastClaims();
                return TextCommandResult.Success($"Permissão concedida a {targetName} em {count} terrenos.");
             }).EndSubCommand();

             t.BeginSubCommand("ver").HandleWith((args) => {
                 var player = args.Caller.Player as IServerPlayer;
                 string key = GetChunkKey(player.Entity.Pos.AsBlockPos);
                 
                 if (claims.ContainsKey(key))
                 {
                     serverChannel.SendPacket(new HighlightChunkPacket() { Show = true }, player);
                     return TextCommandResult.Success("Visualizando fronteiras por 60 segundos.");
                 }
                 return TextCommandResult.Error("Este terreno não tem dono.");
             }).EndSubCommand();
        }

        private TextCommandResult CmdComprar(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            string key = GetChunkKey(player.Entity.Pos.AsBlockPos);

            // ALERTA DE COMPRA FALHA
            if (claims.ContainsKey(key) && claims[key].SalePrice == -1) 
            {
                // Mensagem específica solicitada
                return TextCommandResult.Error("Terra já tem dono e não está à venda!");
            }

            int cost = 0;
            if (claims.ContainsKey(key)) 
            {
                if (claims[key].OwnerUID == player.PlayerUID) return TextCommandResult.Error("Já é seu.");
                cost = claims[key].SalePrice;
                if (!TrySpend(player.PlayerUID, cost)) return TextCommandResult.Error($"Saldo insuficiente. Preço: {cost}");
                AddBalance(claims[key].OwnerUID, cost); 
            }
            else 
            {
                int ownedCount = claims.Values.Count(c => c.OwnerUID == player.PlayerUID);
                cost = (int)(config.BaseChunkCost * Math.Pow(config.CostMultiplier, ownedCount));
                if (!TrySpend(player.PlayerUID, cost)) return TextCommandResult.Error($"Saldo insuficiente. Custo: {cost}");
            }

            claims[key] = new LandClaim() { OwnerUID = player.PlayerUID, OwnerName = player.PlayerName };
            BroadcastClaims();
            SendSound(player, "game:sounds/effect/cashregister");
            
            return TextCommandResult.Success($"Terra comprada por {cost} moedas!");
        }

        private TextCommandResult CmdVender(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            string key = GetChunkKey(player.Entity.Pos.AsBlockPos);
            int price = (int)args.Parsers[0].GetValue();

            if (!claims.ContainsKey(key) || claims[key].OwnerUID != player.PlayerUID) 
                return TextCommandResult.Error("Esta terra não é sua.");

            claims[key].SalePrice = price;
            BroadcastClaims();
            SendSound(player, "game:sounds/effect/latch");
            return TextCommandResult.Success($"À venda por {price} moedas.");
        }

        private TextCommandResult CmdAbandonar(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            string key = GetChunkKey(player.Entity.Pos.AsBlockPos);

            if (!claims.ContainsKey(key) || claims[key].OwnerUID != player.PlayerUID) 
                return TextCommandResult.Error("Esta terra não é sua.");

            claims.Remove(key);
            BroadcastClaims();
            SendSound(player, "game:sounds/effect/squish1");
            return TextCommandResult.Success("Terra abandonada.");
        }
        
        // --- Helper Financeiro e Vacuum ---
        private int GetBalance(string uid) { return bankAccounts.ContainsKey(uid) ? bankAccounts[uid] : 0; }
        private void AddBalance(string uid, int amount) {
            if (!bankAccounts.ContainsKey(uid)) bankAccounts[uid] = 0;
            bankAccounts[uid] += amount;
            var p = sapi.World.PlayerByUid(uid) as IServerPlayer;
            if (p != null) UpdateVisualBalance(p);
        }
        private bool TrySpend(string uid, int amount) {
            int cur = GetBalance(uid);
            if (cur >= amount) { bankAccounts[uid] = cur - amount; 
                var p = sapi.World.PlayerByUid(uid) as IServerPlayer; 
                if (p != null) UpdateVisualBalance(p); return true; 
            } return false;
        }
        private void UpdateVisualBalance(IServerPlayer player) {
            player.Entity.WatchedAttributes.SetInt("landBaron_balance", GetBalance(player.PlayerUID));
            player.Entity.WatchedAttributes.MarkPathDirty("landBaron_balance");
        }

        private void CheckBalance(IServerPlayer player) {
            player.SendMessage(GlobalConstants.GeneralChatGroup, $"[Banco] Saldo: {GetBalance(player.PlayerUID)}", EnumChatType.Notification);
        }

        private void PerformWithdraw(IServerPlayer player, int amount) {
            int bal = GetBalance(player.PlayerUID);
            if (bal < amount) {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "[Banco] Saldo insuficiente.", EnumChatType.Notification);
                return;
            }
            var item = sapi.World.GetItem(new AssetLocation(config.CurrencyItemCode));
            if (item == null) return;

            if (TrySpend(player.PlayerUID, amount)) {
                ItemStack stack = new ItemStack(item, amount);
                if (!player.InventoryManager.TryGiveItemstack(stack)) sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
                player.SendMessage(GlobalConstants.GeneralChatGroup, $"[Banco] Saque: {amount}. Restante: {GetBalance(player.PlayerUID)}", EnumChatType.Notification);
                SendSound(player, "game:sounds/effect/cashregister");
            }
        }
        
        private void OnVacuumTick(float dt) {
            if (bankBlocks.Count == 0 || sapi.World.AllOnlinePlayers.Length == 0) return;
            foreach (var entity in sapi.World.LoadedEntities.Values) {
                if (entity is EntityItem itemEntity && itemEntity.Alive) {
                    ItemStack stack = itemEntity.Itemstack;
                    if (stack?.Collectible?.Code?.ToString().Contains(config.CurrencyItemCode) == true) {
                        BlockPos pos = itemEntity.Pos.AsBlockPos;
                        string key = pos.ToString(); string keyBelow = pos.DownCopy().ToString();
                        
                        if (bankBlocks.Contains(key) || bankBlocks.Contains(keyBelow)) {
                             IPlayer p = null;
                             double bestDist = 5.0; 
                             foreach (var player in sapi.World.AllOnlinePlayers) {
                                 if (player.Entity == null) continue;
                                 double dist = player.Entity.Pos.DistanceTo(itemEntity.Pos.XYZ);
                                 if (dist < bestDist) { bestDist = dist; p = player; }
                             }
                             if (p != null) {
                                 AddBalance(p.PlayerUID, stack.StackSize);
                                 sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/cashregister"), p.Entity, null, true, 32);
                                 itemEntity.Die();
                             }
                        }
                    }
                }
            }
        }
        
        private bool OnCanUseBlock(IServerPlayer player, BlockSelection blockSel) {
            if (bankBlocks.Contains(blockSel.Position.ToString())) {
                SendSound(player, "game:sounds/effect/cashregister"); 
                CheckBalance(player);
                return false; 
            }
            return CheckAccess(player, blockSel.Position, true);
        }
        private void OnBreakBlock(IServerPlayer player, BlockSelection blockSel, ref float drop, ref EnumHandling handling) {
             if (bankBlocks.Contains(blockSel.Position.ToString()) && player.Role.Code != "admin") { handling = EnumHandling.PreventDefault; return; }
             if (!CheckAccess(player, blockSel.Position, false)) handling = EnumHandling.PreventDefault;
        }
        private void OnDidPlaceBlock(IServerPlayer player, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack) {
             if (!CheckAccess(player, blockSel.Position, false)) {
                 sapi.World.BlockAccessor.SetBlock(0, blockSel.Position);
                 if (player.WorldData.CurrentGameMode != EnumGameMode.Creative && withItemStack != null) {
                     ItemStack ret = withItemStack.Clone(); ret.StackSize = 1;
                     if (!player.InventoryManager.TryGiveItemstack(ret)) sapi.World.SpawnItemEntity(ret, player.Entity.Pos.XYZ);
                 }
             }
        }
        private bool CheckAccess(IServerPlayer player, BlockPos pos, bool interact) {
            if (player.Role.Code == "admin") return true;
            string key = GetChunkKey(pos);
            if (claims.TryGetValue(key, out LandClaim c)) {
                if (c.OwnerUID == player.PlayerUID || c.AllowedUIDs.Contains(player.PlayerUID) || (interact && c.PublicAccess)) return true;
                return false;
            }
            return true;
        }
        private string GetChunkKey(BlockPos pos) { return $"{pos.X / 32},{pos.Z / 32}"; }

        // =========================================================================================
        //                                      LADO DO CLIENTE
        // =========================================================================================
        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            clientChannel = api.Network.GetChannel("landbaron")
                .SetMessageHandler<SyncClaimsPacket>(OnPacketSync)
                .SetMessageHandler<PlaySoundPacket>(OnPacketSound)
                .SetMessageHandler<HighlightChunkPacket>((p) => {
                    showBorders = p.Show;
                    // ATUALIZAÇÃO: 60 SEGUNDOS
                    borderTimer = api.ElapsedMilliseconds + 60000;
                });

            api.Event.RegisterGameTickListener(ClientTick, 200); 
            
            // hud = new TerritoryHud(api);
        }

        private void OnPacketSync(SyncClaimsPacket packet)
        {
            if (packet == null || packet.Claims == null) return;

            if (claims == null) claims = new Dictionary<string, LandClaim>();
            claims.Clear();
            foreach (var kvp in packet.Claims) claims[kvp.Key] = kvp.Value;
        }

        private void OnPacketSound(PlaySoundPacket packet)
        {
            capi.World.PlaySoundAt(new AssetLocation(packet.SoundName), capi.World.Player.Entity, null, true, 32, 1f);
        }

        private void ClientTick(float dt)
        {
            if (capi.World.Player == null) return;
            var pos = capi.World.Player.Entity.Pos.AsBlockPos;
            string currentKey = GetChunkKey(pos);

            if (currentKey != lastChunkKeyClient)
            {
                lastChunkKeyClient = currentKey;
                if (claims.TryGetValue(currentKey, out LandClaim claim))
                {
                    int totalLands = claims.Values.Count(c => c.OwnerUID == claim.OwnerUID);
                    // hud.ShowNotification(claim, totalLands, capi.World.Player.PlayerUID);

                    string statusVenda = claim.SalePrice > -1 ? $" | 💲 À VENDA: {claim.SalePrice}" : "";
                    
                    string cor = claim.OwnerUID == capi.World.Player.PlayerUID ? "#55FF55" : "#FF5555";
                    if (claim.SalePrice > -1) cor = "#FFFF55";

                    capi.ShowChatMessage($"<font color='{cor}'>[Território]</font> 🏰 **{claim.OwnerName}**{statusVenda}");
                    capi.ShowChatMessage($"<font color='#AAAAAA'>📊 Império: {totalLands} terrenos conectados.</font>");
                }
            }

            if (showBorders && capi.ElapsedMilliseconds < borderTimer)
            {
                if (claims.ContainsKey(currentKey)) DrawChunkBorders(pos, claims[currentKey]);
                else showBorders = false;
            }
            else
            {
                showBorders = false;
            }
        }

        private void DrawChunkBorders(BlockPos pos, LandClaim claim)
        {
            int chunkX = pos.X / 32;
            int chunkZ = pos.Z / 32;
            double startX = chunkX * 32;
            double startZ = chunkZ * 32;
            double y = pos.Y; 

            int color = ColorUtil.ToRgba(255, 255, 0, 0); 
            if (claim.OwnerUID == capi.World.Player.PlayerUID) color = ColorUtil.ToRgba(255, 0, 255, 0); 
            if (claim.SalePrice > -1) color = ColorUtil.ToRgba(255, 255, 255, 0); 

            SimpleParticleProperties p = new SimpleParticleProperties(
                1f, 1f, color, new Vec3d(), new Vec3d(), new Vec3f(), new Vec3f(), 
                2.0f, 0f, 0.5f, 1.5f, EnumParticleModel.Cube
            );
            
            p.VertexFlags = 255; 
            p.SelfPropelled = true; 
            p.MinVelocity = new Vec3f(0, 0.1f, 0); 
            p.GravityEffect = -0.05f; 
            p.WithTerrainCollision = false;

            for (int i = 0; i <= 32; i+=2) 
            {
                capi.World.SpawnParticles(p); p.MinPos = new Vec3d(startX + i, y, startZ);
                capi.World.SpawnParticles(p); p.MinPos = new Vec3d(startX + i, y, startZ + 32);
                capi.World.SpawnParticles(p); p.MinPos = new Vec3d(startX, y, startZ + i);
                capi.World.SpawnParticles(p); p.MinPos = new Vec3d(startX + 32, y, startZ + i);
            }
        }

    }

    /*
    public class TerritoryHud : HudElement
    {
        private long fadeOutTime;
        private bool isVisible;

        public TerritoryHud(ICoreClientAPI capi) : base(capi)
        {
        }

        public void ShowNotification(LandClaim claim, int lands, string myUID)
        {
            string cor = claim.OwnerUID == myUID ? "#55FF55" : "#FF5555";
            if (claim.SalePrice > -1) cor = "#FFFF55";

            string text = $"<font color='{cor}'>🏰 {claim.OwnerName}</font>";
            if (claim.SalePrice > -1) text += $" <font color='#DDDDDD'>| 💲 {claim.SalePrice}</font>";
            text += $"\n<font color='#AAAAAA'>📊 Império: {lands} Chunks</font>";
            
            ElementBounds textBounds = ElementBounds.Fixed(0, 0, 400, 50);
            ElementBounds dialogBounds = ElementBounds.Fixed(EnumDialogArea.CenterBottom, 0, -100, 400, 50);
            
            Composers["territoryhud"] = capi.Gui.CreateCompo("territoryhud", dialogBounds.FlatCopy().WithFixedAlignment(EnumDialogArea.CenterBottom))
                //.AddDynamicText(text, CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center), textBounds, "lbl")
                .AddShadedDialogBG(dialogBounds)
                .Compose();
            
            TryOpen();
            isVisible = true;
            fadeOutTime = capi.ElapsedMilliseconds + 5000;
            
            // Force update text immediately
            // Composers["territoryhud"].GetDynamicText("lbl").SetNewText(text);
        }

        public override void OnRenderGUI(float deltaTime)
        {
            if (isVisible)
            {
                 base.OnRenderGUI(deltaTime);
                 if (capi.ElapsedMilliseconds > fadeOutTime)
                 {
                     isVisible = false;
                     TryClose();
                 }
            }
        }
        
        public override bool Focusable => false;
    }
    */
}
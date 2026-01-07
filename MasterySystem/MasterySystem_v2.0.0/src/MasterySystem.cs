using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;

using Vintagestory.API.Datastructures;

namespace MasteryTitles
{
    public enum MasteryType { Mining, Lumbering, Farming, Combat }

    public class PlayerMasteryData
    {
        public Dictionary<MasteryType, int> Experience = new Dictionary<MasteryType, int>();
        public string ActiveTitle = "";

        public PlayerMasteryData()
        {
            foreach (MasteryType type in Enum.GetValues(typeof(MasteryType)))
            {
                Experience[type] = 0;
            }
        }
    }

    public class MasterySystem : ModSystem
    {
        private ICoreServerAPI sapi;
        public Dictionary<string, PlayerMasteryData> masteryCache = new Dictionary<string, PlayerMasteryData>();
        public MasteryConfig Config;

        // Níveis de XP (Quantidade de ações)
        public const int LVL_1 = 100;   
        public const int LVL_2 = 1000;  
        public const int LVL_3 = 5000;

        // Evento para outros sistemas
        public event Action<IServerPlayer, MasteryType, int> PlayerLeveledUp;  

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            
            try 
            {
                Config = api.LoadModConfig<MasteryConfig>("MasteryConfig.json"); 
                if (Config == null)
                {
                    Config = new MasteryConfig();
                    api.StoreModConfig(Config, "MasteryConfig.json");
                }
            } 
            catch 
            {
                Config = new MasteryConfig();
                api.StoreModConfig(Config, "MasteryConfig.json");
            }

            api.Event.PlayerJoin += OnPlayerJoin;
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
            api.Event.BreakBlock += OnBlockBreak;
            api.Event.OnEntityDeath += OnEntityDeath; 
            api.Event.PlayerChat += OnPlayerChat;

            // Debug Commands
            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands.Create("mastery")
                .WithDescription("Mastery System Debug Tools")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("setxp")
                    .WithArgs(parsers.Word("player"), parsers.Word("profession"), parsers.Int("amount"))
                    .HandleWith(OnCmdSetXP)
                .EndSubCommand()
                .BeginSubCommand("addxp")
                    .WithArgs(parsers.Word("player"), parsers.Word("profession"), parsers.Int("amount"))
                    .HandleWith(OnCmdAddXP)
                .EndSubCommand()
                .BeginSubCommand("reset")
                    .WithArgs(parsers.Word("player"))
                    .HandleWith(OnCmdReset)
                .EndSubCommand()
                .BeginSubCommand("resetcd")
                    .WithArgs(parsers.Word("player"))
                    .HandleWith(OnCmdResetCD)
                .EndSubCommand();
        }

        private TextCommandResult OnCmdSetXP(TextCommandCallingArgs args)
        {
            string playerName = args[0] as string;
            IServerPlayer player = sapi.World.AllOnlinePlayers.FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.InvariantCultureIgnoreCase)) as IServerPlayer;
            string profStr = args[1] as string;
            int amount = (int)args[2];

            if (player == null) return TextCommandResult.Error("Player required.");
            if (!Enum.TryParse<MasteryType>(profStr, true, out var type)) return TextCommandResult.Error("Invalid profession (Mining, Lumbering, Farming, Combat).");
            
            if (masteryCache.TryGetValue(player.PlayerUID, out var data))
            {
                int oldXp = data.Experience.ContainsKey(type) ? data.Experience[type] : 0;
                data.Experience[type] = amount;
                
                int oldLevel = GetLevel(oldXp);
                int newLevel = GetLevel(amount);
                
                if (newLevel > oldLevel)
                {
                     TriggerLevelUpEffects(player, type, newLevel);
                     PlayerLeveledUp?.Invoke(player, type, newLevel);
                }
                
                RecalcularBuffsETitulo(player, data);
                SalvarDados(player);
                
                return TextCommandResult.Success($"Set {player.PlayerName} {type} XP to {amount} (Lvl {newLevel})");
            }
            return TextCommandResult.Error("Player data not found.");
        }

        private TextCommandResult OnCmdAddXP(TextCommandCallingArgs args)
        {
            string playerName = args[0] as string;
            IServerPlayer player = sapi.World.AllOnlinePlayers.FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.InvariantCultureIgnoreCase)) as IServerPlayer;
            string profStr = args[1] as string;
            int amount = (int)args[2];

            if (player == null) return TextCommandResult.Error("Player required.");
            if (!Enum.TryParse<MasteryType>(profStr, true, out var type)) return TextCommandResult.Error("Invalid profession (Mining, Lumbering, Farming, Combat).");
            
            GiveXP(player, type, amount);
            return TextCommandResult.Success($"Added {amount} XP to {player.PlayerName} {type}");
        }

        private TextCommandResult OnCmdReset(TextCommandCallingArgs args)
        {
            string playerName = args[0] as string;
            IServerPlayer player = sapi.World.AllOnlinePlayers.FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.InvariantCultureIgnoreCase)) as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Player required or not online.");
            
            if (masteryCache.ContainsKey(player.PlayerUID))
            {
                masteryCache[player.PlayerUID] = new PlayerMasteryData();
            }
            // Force reset watched attributes
            ITreeAttribute tree = player.Entity.WatchedAttributes.GetOrAddTreeAttribute("mastery");
            tree.RemoveAttribute("masteryData"); // Clear data
            
            // Clear other related attributes just in case
            player.Entity.WatchedAttributes.MarkPathDirty("mastery");
            
            SalvarDados(player);
            SalvarDados(player);
            return TextCommandResult.Success($"Reset mastery data for {player.PlayerName}");
        }

        private TextCommandResult OnCmdResetCD(TextCommandCallingArgs args)
        {
            string playerName = args[0] as string;
            IServerPlayer player = sapi.World.AllOnlinePlayers.FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.InvariantCultureIgnoreCase)) as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Player required or not online.");

            ITreeAttribute persistence = player.Entity.WatchedAttributes.GetOrAddTreeAttribute("mastery_abilities");
            persistence.SetLong("last_used_ms", 0);
            persistence.SetLong("active_until_ms", 0);
            player.Entity.WatchedAttributes.MarkPathDirty("mastery_abilities");
            
            // Also clear stats if stuck
            player.Entity.Stats.Remove("miningSpeedMul", "ability");
            player.Entity.Stats.Remove("walkspeed", "ability");
            
            return TextCommandResult.Success($"Reset ability cooldown for {player.PlayerName}");
        }

        // --- LOAD / SAVE ---
        private void OnPlayerJoin(IServerPlayer player)
        {
            var data = new PlayerMasteryData();
            if (player.Entity.WatchedAttributes.HasAttribute("masteryData"))
            {
                var tree = player.Entity.WatchedAttributes.GetTreeAttribute("masteryData");
                data.ActiveTitle = tree.GetString("activeTitle");
                foreach (MasteryType type in Enum.GetValues(typeof(MasteryType)))
                {
                    data.Experience[type] = tree.GetInt(type.ToString());
                }
            }
            masteryCache[player.PlayerUID] = data;
            RecalcularBuffsETitulo(player, data);
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            SalvarDados(player);
            masteryCache.Remove(player.PlayerUID);
        }

        private void SalvarDados(IServerPlayer player)
        {
            if (masteryCache.TryGetValue(player.PlayerUID, out var data))
            {
                ITreeAttribute tree = new TreeAttribute();
                tree.SetString("activeTitle", data.ActiveTitle);
                foreach (var kvp in data.Experience)
                {
                    tree.SetInt(kvp.Key.ToString(), kvp.Value);
                }
                player.Entity.WatchedAttributes.SetAttribute("masteryData", tree);
                player.Entity.WatchedAttributes.MarkPathDirty("masteryData");
            }
        }

        // --- AÇÕES (XP) ---
        private void OnBlockBreak(IServerPlayer player, BlockSelection blockSel, ref float dropQuantityMultiplier, ref EnumHandling handling)
        {
            if (player == null) return;
            Block block = sapi.World.BlockAccessor.GetBlock(blockSel.Position);
            string code = block.Code.Path;

            if (code.Contains("rock") || code.Contains("ore")) GiveXP(player, MasteryType.Mining, 1);
            else if (code.Contains("log")) GiveXP(player, MasteryType.Lumbering, 1);
            else if (block.BlockMaterial == EnumBlockMaterial.Plant || code.Contains("crop")) GiveXP(player, MasteryType.Farming, 1);
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (damageSource != null && damageSource.SourceEntity is EntityPlayer entityPlayer)
            {
                IServerPlayer player = entityPlayer.Player as IServerPlayer;
                if (player != null) GiveXP(player, MasteryType.Combat, 1);
            }
        }

        // --- LEVEL UP ---
        public void GiveXP(IServerPlayer player, MasteryType type, int amount)
        {
            if (!masteryCache.TryGetValue(player.PlayerUID, out var data)) return;

            int oldXp = data.Experience[type];
            data.Experience[type] += amount;
            CheckLevelChange(player, type, oldXp, data.Experience[type]);
        }

        private void CheckLevelChange(IServerPlayer player, MasteryType type, int oldXp, int newXp)
        {
            if (!masteryCache.TryGetValue(player.PlayerUID, out var data)) return;

            int oldLevel = GetLevel(oldXp);
            int newLevel = GetLevel(newXp);

            if (newLevel > oldLevel)
            {
                TriggerLevelUpEffects(player, type, newLevel);
                RecalcularBuffsETitulo(player, data);
                SalvarDados(player);
                
                PlayerLeveledUp?.Invoke(player, type, newLevel);
            }
            else if (newLevel != oldLevel) // Level down or just change
            {
                RecalcularBuffsETitulo(player, data);
                SalvarDados(player);
            }
        }

        public int GetLevel(int xp)
        {
            if (xp >= LVL_3) return 3;
            if (xp >= LVL_2) return 2;
            if (xp >= LVL_1) return 1;
            return 0;
        }

        private string GetTitleName(MasteryType type, int level)
        {
            string prefix = level == 1 ? "[Aprendiz]" : level == 2 ? "[Experiente]" : level == 3 ? "[Mestre]" : "";
            string suffix = type == MasteryType.Mining ? "Minerador" : type == MasteryType.Lumbering ? "Lenhador" : type == MasteryType.Farming ? "Agricultor" : "Guerreiro";
            return $"{prefix} {suffix}";
        }

        private void TriggerLevelUpEffects(IServerPlayer player, MasteryType type, int newLevel)
        {
             // Partículas Douradas
             player.Entity.World.SpawnParticles(
                50, 
                ColorUtil.ToRgba(255, 255, 215, 0), // Dourado
                player.Entity.Pos.XYZ.Add(0, 1, 0), 
                player.Entity.Pos.XYZ.Add(0.5, 2, 0.5), 
                new Vec3f(-0.5f, 0.5f, -0.5f), 
                new Vec3f(0.5f, 1f, 0.5f), 
                2f, 
                1f,
                0.5f,
                EnumParticleModel.Cube
            );

            // Som Epico
            player.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/latch"), player.Entity, null, true, 32, 1f);

            // Mensagem
            string titleName = GetTitleName(type, newLevel);
            sapi.SendMessage(player, 0, $"<strong><font color=\"#00FF00\">LEVEL UP! Agora voce eh {titleName}!</font></strong>", EnumChatType.Notification);
        }

        private void RecalcularBuffsETitulo(IServerPlayer player, PlayerMasteryData data)
        {
            if (data.Experience.Count == 0) return;

            var highestSkill = data.Experience.OrderByDescending(x => x.Value).First();
            int level = GetLevel(highestSkill.Value);
            
            // Title logic with override support
            string overrideTitle = player.Entity.WatchedAttributes.GetString("selected_title_override");
            if (!string.IsNullOrEmpty(overrideTitle))
            {
                data.ActiveTitle = overrideTitle;
            }
            else
            {
                data.ActiveTitle = level > 0 ? GetTitleName(highestSkill.Key, level) : "";
            }

            float miningSpeed = 0f;
            float hungerRate = 1f; 
            float hpBonus = 0f;

            int miningLvl = GetLevel(data.Experience[MasteryType.Mining]);
            if (miningLvl >= 1) miningSpeed += 0.1f;
            if (miningLvl >= 3) miningSpeed += 0.2f;

            int lumberLvl = GetLevel(data.Experience[MasteryType.Lumbering]);
            if (lumberLvl >= 1) miningSpeed += 0.05f;

            int farmLvl = GetLevel(data.Experience[MasteryType.Farming]);
            if (farmLvl >= 1) hungerRate -= 0.05f;
            if (farmLvl >= 3) hungerRate -= 0.15f;

            int combatLvl = GetLevel(data.Experience[MasteryType.Combat]);
            if (combatLvl >= 1) hpBonus += 2f;
            if (combatLvl >= 3) hpBonus += 9f;

            player.Entity.Stats.Set("miningSpeedMultiplier", "mastery", miningSpeed, true);
            player.Entity.Stats.Set("hungerrate", "mastery", hungerRate, true);
            player.Entity.Stats.Set("maxhealthExtraPoints", "mastery", hpBonus, true);
        }

        // --- CHAT ---
        private void OnPlayerChat(IServerPlayer player, int channelId, ref string message, ref string data, BoolRef consumed)
        {
            if (channelId != 0) return;

            if (masteryCache.TryGetValue(player.PlayerUID, out var mData))
            {
                if (!string.IsNullOrEmpty(mData.ActiveTitle))
                {
                    // CORREÇÃO: 'value' deve ser minúsculo
                    consumed.value = true;

                    string formattedMsg = string.Format("<font color=\"#FFD700\">{0}</font> <strong>{1}:</strong> {2}", 
                        mData.ActiveTitle, 
                        player.PlayerName, 
                        message);

                    sapi.SendMessageToGroup(0, formattedMsg, EnumChatType.Notification);
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
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
        private Dictionary<string, PlayerMasteryData> masteryCache = new Dictionary<string, PlayerMasteryData>();

        // Níveis de XP (Quantidade de ações)
        private const int LVL_1 = 100;   
        private const int LVL_2 = 1000;  
        private const int LVL_3 = 5000;  

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;

            api.Event.PlayerJoin += OnPlayerJoin;
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
            api.Event.BreakBlock += OnBlockBreak;
            api.Event.OnEntityDeath += OnEntityDeath; 
            api.Event.PlayerChat += OnPlayerChat;
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
        private void GiveXP(IServerPlayer player, MasteryType type, int amount)
        {
            if (!masteryCache.TryGetValue(player.PlayerUID, out var data)) return;

            int oldLevel = GetLevel(data.Experience[type]);
            data.Experience[type] += amount;
            int newLevel = GetLevel(data.Experience[type]);

            if (newLevel > oldLevel)
            {
                string titleName = GetTitleName(type, newLevel);
                sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, $"<strong><font color=\"#00FF00\">LEVEL UP! Agora voce eh {titleName}!</font></strong>", EnumChatType.Notification);
                sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/latch"), player.Entity);
                RecalcularBuffsETitulo(player, data);
                SalvarDados(player);
            }
        }

        private int GetLevel(int xp)
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

        private void RecalcularBuffsETitulo(IServerPlayer player, PlayerMasteryData data)
        {
            if (data.Experience.Count == 0) return;

            var highestSkill = data.Experience.OrderByDescending(x => x.Value).First();
            int level = GetLevel(highestSkill.Value);
            data.ActiveTitle = level > 0 ? GetTitleName(highestSkill.Key, level) : "";

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
            if (channelId != GlobalConstants.GeneralChatGroup) return;

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

                    sapi.SendMessageToGroup(GlobalConstants.GeneralChatGroup, formattedMsg, EnumChatType.Notification);
                }
            }
        }
    }
}
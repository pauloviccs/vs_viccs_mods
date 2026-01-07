using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;

namespace MasteryTitles
{
    public class AchievementSystem : ModSystem
    {
        private ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.BreakBlock += OnBlockBreak;
            api.Event.OnEntityDeath += OnEntityDeath;
            
            api.ChatCommands.Create("mtitle")
                .WithDescription("Set your mastery title")
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(api.ChatCommands.Parsers.Word("title"))
                .HandleWith(OnTitleCommand);
        }

        private TextCommandResult OnTitleCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            string titleReq = args[0] as string;
            
            ITreeAttribute achTree = player.Entity.WatchedAttributes.GetTreeAttribute("achievements");
            if (achTree == null) return TextCommandResult.Error("You have no achievements yet.");

            // Check if unlocked
            // Simple mapping for now
            bool unlocked = false;
            string formalTitle = "";

            if (titleReq.ToLower() == "obcecado" && achTree.GetInt("rock_broken") >= 1000) { unlocked = true; formalTitle = "[Obcecado]"; }
            if (titleReq.ToLower() == "noturno" && achTree.GetInt("night_chopped") >= 100) { unlocked = true; formalTitle = "[Noturno]"; }
            if (titleReq.ToLower() == "persistente" && achTree.GetInt("crops_farmed") >= 500) { unlocked = true; formalTitle = "[Persistente]"; }
            if (titleReq.ToLower() == "cacador" && achTree.GetInt("mobs_killed") >= 50) { unlocked = true; formalTitle = "[Caçador]"; }

            if (unlocked)
            {
                // Update Mastery Data active title
                var mastery = sapi.ModLoader.GetModSystem<MasterySystem>();
                if (mastery != null && mastery.masteryCache.TryGetValue(player.PlayerUID, out var data))
                {
                    data.ActiveTitle = formalTitle + " " + player.PlayerName; // Or just prefix? 
                    // Wait, ActiveTitle usually includes the profession. 
                    // Let's just set the prefix.
                    // Actually MasterySystem rewrites ActiveTitle on Level Up/Join.
                    // We need a persistent "Selected Title" override.
                    player.Entity.WatchedAttributes.SetString("selected_title_override", formalTitle);
                    
                    // Force update
                    data.ActiveTitle = formalTitle; // Temporary until next recalc
                    mastery.masteryCache[player.PlayerUID] = data; // Ensure cache update
                }
                return TextCommandResult.Success($"Title set to {formalTitle}");
            }

            return TextCommandResult.Error("Title locked or invalid. (Requirements: 1000 Rock, 100 Night Chop, 500 Crop, 50 Kill)");
        }

        private void OnBlockBreak(IServerPlayer player, BlockSelection blockSel, ref float dropQuantityMultiplier, ref EnumHandling handling)
        {
            if (player == null) return;
            Block block = sapi.World.BlockAccessor.GetBlock(blockSel.Position);
            string code = block.Code.Path;

            ITreeAttribute tree = player.Entity.WatchedAttributes.GetOrAddTreeAttribute("achievements");

            if (code.Contains("rock"))
            {
                int current = tree.GetInt("rock_broken");
                tree.SetInt("rock_broken", current + 1);
                if (current + 1 == 1000) NotifyUnlock(player, "[Obcecado]");
            }
            else if (code.Contains("log"))
            {
                // Check time for "Noturno"
                float hour = sapi.World.Calendar.HourOfDay;
                if (hour < 6 || hour > 20)
                {
                    int current = tree.GetInt("night_chopped");
                    tree.SetInt("night_chopped", current + 1);
                    if (current + 1 == 100) NotifyUnlock(player, "[Noturno]");
                }
            }
            else if (code.Contains("crop"))
            {
                int current = tree.GetInt("crops_farmed");
                tree.SetInt("crops_farmed", current + 1);
                if (current + 1 == 500) NotifyUnlock(player, "[Persistente]");
            }
            
            player.Entity.WatchedAttributes.MarkPathDirty("achievements");
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
             if (damageSource != null && damageSource.SourceEntity is EntityPlayer entityPlayer)
            {
                IServerPlayer player = entityPlayer.Player as IServerPlayer;
                ITreeAttribute tree = player.Entity.WatchedAttributes.GetOrAddTreeAttribute("achievements");
                
                int current = tree.GetInt("mobs_killed");
                tree.SetInt("mobs_killed", current + 1);
                if (current + 1 == 50) NotifyUnlock(player, "[Caçador]");
                
                player.Entity.WatchedAttributes.MarkPathDirty("achievements");
            }
        }

        private void NotifyUnlock(IServerPlayer player, string title)
        {
            player.SendMessage(0, $"<strong><font color=\"#FFD700\">ACHIEVEMENT UNLOCKED: {title}</font></strong>", EnumChatType.Notification);
            player.SendMessage(0, $"Use /mtitle {title.Replace("[","").Replace("]","")} to equip.", EnumChatType.Notification);
            sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/latch"), player.Entity);
        }
    }
}

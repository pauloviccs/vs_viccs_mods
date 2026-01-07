using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace MasteryTitles
{
    public class AbilitySystem : ModSystem
    {
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private IClientNetworkChannel clientChannel;
        private IServerNetworkChannel serverChannel;

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel("masteryability")
                .RegisterMessageType<AbilityPacket>();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            clientChannel = api.Network.GetChannel("masteryability");

            // Register Hotkey 'V'
            api.Input.RegisterHotKey("mastery_ability", "Habilidade Mastery", GlKeys.V);
            api.Input.SetHotKeyHandler("mastery_ability", OnAbilityKeyPressed);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            serverChannel = api.Network.GetChannel("masteryability");
            serverChannel.SetMessageHandler<AbilityPacket>(OnAbilityPacketReceived);

            api.Event.BreakBlock += OnBlockBreak;
        }

        private bool OnAbilityKeyPressed(KeyCombination key)
        {
            clientChannel.SendPacket(new AbilityPacket());
            return true;
        }

        private void OnAbilityPacketReceived(IServerPlayer player, AbilityPacket packet)
        {
            var masterySystem = sapi.ModLoader.GetModSystem<MasterySystem>();
            if (masterySystem == null || !masterySystem.masteryCache.TryGetValue(player.PlayerUID, out var data)) return;
            
            var config = masterySystem.Config;

            ITreeAttribute persistence = player.Entity.WatchedAttributes.GetOrAddTreeAttribute("mastery_abilities");
            long lastUsed = persistence.GetLong("last_used_ms");
            long activeUntil = persistence.GetLong("active_until_ms");
            long now = sapi.World.ElapsedMilliseconds;
            
            // Check if already active
            if (now < activeUntil)
            {
                player.SendMessage(0, "Habilidade ja esta ativa!", EnumChatType.Notification);
                return;
            }

            long cooldownMs = config.AbilityCooldownMinutes * 60 * 1000; 

            // Check Cooldown (starts AFTER active duration)
            if (now - lastUsed < cooldownMs)
            {
                float remainingSeconds = (cooldownMs - (now - lastUsed)) / 1000f;
                float remainingMins = remainingSeconds / 60f;
                player.SendMessage(0, $"Habilidade em recarga! {remainingMins:F1} min restantes.", EnumChatType.Notification);
                return;
            }

            if (ExecuteAbility(player, data, config))
            {
                 // Set Cooldown Start to NOW + DURATION? 
                 // User said: "depois ela é desativada, e entra em cooldown".
                 // So "Last Used" should be set to "Now + Duration".
                 // But strictly, last_used is usually "when clicked".
                 // Let's set last_used = Now + Duration.
                 // So when (Now + Duration) passes, the check (Now - (LastUsed)) will be negative? No.
                 // Let's redefine: Cooldown Check = (Now > ActiveUntil + Cooldown).
                 // Simpler: Set "last_used" to (Now + Duration).
                 
                 long durationMs = config.AbilityDurationSeconds * 1000;
                 persistence.SetLong("active_until_ms", now + durationMs);
                 persistence.SetLong("last_used_ms", now + durationMs); // Cooldown starts after duration
                 
                 player.Entity.WatchedAttributes.MarkPathDirty("mastery_abilities");
                 
                 // Schedule cleanup if needed (stats)
                 player.Entity.World.RegisterCallback((dt) => {
                     player.Entity.Stats.Remove("miningSpeedMul", "ability");
                     player.Entity.Stats.Remove("walkspeed", "ability");
                     player.SendMessage(0, "Habilidade desativada.", EnumChatType.Notification);
                 }, (int)durationMs);
            }
        }

        private bool ExecuteAbility(IServerPlayer player, PlayerMasteryData data, MasteryConfig config)
        {
            if (data.Experience.Count == 0) return false;
            
            var highest = data.Experience.OrderByDescending(x => x.Value).First();
            int level = sapi.ModLoader.GetModSystem<MasterySystem>().GetLevel(highest.Value);

            if (level < 3) 
            {
                player.SendMessage(0, "Voce precisa ser Mestre (Nivel 3) para usar habilidades!", EnumChatType.Notification);
                return false;
            }

            switch (highest.Key)
            {
                case MasteryType.Mining:
                    return CastMiningAbility(player, config);
                case MasteryType.Lumbering:
                    return CastLumberingAbility(player, config);
                case MasteryType.Farming:
                    return CastFarmingAbility(player, config);
                case MasteryType.Combat:
                    return CastCombatAbility(player, config);
                default:
                    return false;
            }
        }

        // --- MINER ---
        private bool CastMiningAbility(IServerPlayer player, MasteryConfig config)
        {
            // "Furia do Minerador": 30s of Speed + Blast
            player.Entity.Stats.Set("miningSpeedMul", "ability", config.MiningSpeedMultiplier, true);

            sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/toolbreak"), player.Entity, null, true, 32, 0.5f);
            player.SendMessage(0, $"** MINERADOR: FURIA ATIVADA ({config.AbilityDurationSeconds}s) **", EnumChatType.Notification);
            return true;
        }

        private void OnBlockBreak(IServerPlayer player, BlockSelection blockSel, ref float dropQuantityMultiplier, ref EnumHandling handling)
        {
            ITreeAttribute tree = player.Entity.WatchedAttributes.GetTreeAttribute("mastery_abilities");
            if (tree == null) return;
            
            long activeUntil = tree.GetLong("active_until_ms");
            long now = sapi.World.ElapsedMilliseconds;

            if (now < activeUntil)
            {
                // 3x3 Blast Logic
                sapi.World.SpawnParticles(10, ColorUtil.ToRgba(255, 150, 150, 150), blockSel.Position.ToVec3d(), blockSel.Position.ToVec3d().Add(1, 1, 1), new Vec3f(), new Vec3f(), 1f, 1f, 0.5f, EnumParticleModel.Cube);
                sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/toolbreak"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, null, true, 32, 1f);

                // Break neighbors (3x3 blast)
                BlockPos center = blockSel.Position;
                sapi.World.BlockAccessor.WalkBlocks(center.AddCopy(-1, -1, -1), center.AddCopy(1, 1, 1), (block, x, y, z) => 
                {
                    BlockPos p = new BlockPos(x, y, z, 0);
                    if (p.Equals(center)) return; // Already broken by player
                    
                    // Only break stone/ore/dirt
                    if (block.Code.Path.Contains("rock") || block.Code.Path.Contains("ore") || block.Code.Path.Contains("soil"))
                    {
                        sapi.World.BlockAccessor.BreakBlock(p, player);
                    }
                });
            }
        }

        // --- LUMBERJACK ---
        private bool CastLumberingAbility(IServerPlayer player, MasteryConfig config)
        {
            // "Corte Limpo": Break trees around
            BlockPos center = player.Entity.Pos.AsBlockPos;
            int radius = 10;
            int brokenCount = 0;
            int maxBroken = 64; // Limit to prevent lag

            sapi.World.BlockAccessor.WalkBlocks(center.AddCopy(-radius, -5, -radius), center.AddCopy(radius, 10, radius), (block, x, y, z) =>
            {
                BlockPos pos = new BlockPos(x, y, z, 0);
                if (brokenCount >= maxBroken) return;
                if (block.Code.Path.Contains("log"))
                {
                    sapi.World.BlockAccessor.BreakBlock(pos, player);
                    brokenCount++;
                }
            });

            if (brokenCount > 0)
            {
                sapi.World.PlaySoundAt(new AssetLocation("game:sounds/block/chop2"), player.Entity);
                player.SendMessage(0, $"** LENHADOR: {brokenCount} TRONCOS CORTADOS **", EnumChatType.Notification);
                return true;
            }
            
            player.SendMessage(0, "Nenhuma arvore encontrada por perto.", EnumChatType.Notification);
            return false;
        }

        // --- FARMER ---
        private bool CastFarmingAbility(IServerPlayer player, MasteryConfig config)
        {
            // "Bênção da Colheita": Grow crops
            BlockPos center = player.Entity.Pos.AsBlockPos;
            int radius = 10;
            int grownCount = 0;

            sapi.World.BlockAccessor.WalkBlocks(center.AddCopy(-radius, -2, -radius), center.AddCopy(radius, 2, radius), (block, x, y, z) =>
            {
                 BlockPos pos = new BlockPos(x, y, z, 0);
                 if (block.CropProps != null)
                 {
                     // Use CropProps to detect crops instead of BlockCrop class cast
                    // Basicamente tenta crescer para o estágio máximo
                    // Assumindo que TryGrowCrop avança o estágio
                    // Mas BlockCrop não tem TryGrow publico facil as vezes.
                    // Vamos tentar setar o block para o proximo estagio.
                    
                    // Hack: Pegar o max stage e setar? Ou usar behaviors?
                    // Vamos tentar simular um tick de crescimento forçado várias vezes.
                    
                    // Simple hack: Check code variant "stage".
                   
                   string stageStr = block.Variant["stage"];
                   if (int.TryParse(stageStr, out int currentStage))
                   {
                        // Max stage is usually 7 or 8 depending on crop. Let's add 5 stages.
                        int newStage = Math.Min(currentStage + 5, block.CropProps.GrowthStages); // Default logic
                        if (newStage > currentStage)
                        {
                            AssetLocation newCode = block.CodeWithVariant("stage", newStage.ToString());
                            Block newBlock = sapi.World.GetBlock(newCode);
                            if (newBlock != null)
                            {
                                sapi.World.BlockAccessor.SetBlock(newBlock.BlockId, pos);
                                // Green particles for growth
                                sapi.World.SpawnParticles(5, ColorUtil.ToRgba(255, 50, 200, 50), pos.ToVec3d(), pos.ToVec3d().Add(1, 1, 1), new Vec3f(), new Vec3f(), 1f, 1f, 0.5f, EnumParticleModel.Cube);
                                grownCount++;
                            }
                        }
                   }
                }
            });

            if (grownCount > 0)
            {
                sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/latch"), player.Entity);
                player.SendMessage(0, $"** AGRICULTOR: {grownCount} PLANTAS CRESCERAM **", EnumChatType.Notification);
                return true;
            }

            player.SendMessage(0, "Nenhuma plantacao encontrada.", EnumChatType.Notification);
            return false;
        }

        // --- WARRIOR ---
        private bool CastCombatAbility(IServerPlayer player, MasteryConfig config)
        {
            // "Investida Brutal": Speed + Shockwave
            
            // 1. Stat Buff
            player.Entity.Stats.Set("walkspeed", "ability", 0.5f, true); // +50% Speed
            // Duration handled by common callback in OnAbilityPacketReceived
            
            // 2. Shockwave (Instant)
            var entities = player.Entity.World.GetEntitiesAround(player.Entity.Pos.XYZ, 10, 5, e => e.IsInteractable);
            int hitCount = 0;
            foreach (var e in entities)
            {
                if (e is EntityPlayer) continue; // Don't hit players (unless pvp?)
                
                DamageSource src = new DamageSource();
                src.Source = EnumDamageSource.Entity;
                src.SourceEntity = player.Entity;
                src.Type = EnumDamageType.BluntAttack;

                e.ReceiveDamage(src, 10f); // 10 dmg flat
                
                // Knockback
                Vec3d push = e.Pos.XYZ.Sub(player.Entity.Pos.XYZ).Normalize().Mul(0.5);
                e.SidedPos.Motion.Add(push.X, 0.2, push.Z);
                
                hitCount++;
            }

            sapi.World.SpawnParticles(20, ColorUtil.ToRgba(255, 200, 50, 50), player.Entity.Pos.XYZ.Add(-2,0,-2), player.Entity.Pos.XYZ.Add(2,2,2), new Vec3f(-1,0,-1), new Vec3f(1,1,1), 2f, 1f, 1f, EnumParticleModel.Quad);
            sapi.World.PlaySoundAt(new AssetLocation("game:sounds/player/strike"), player.Entity);
             player.SendMessage(0, $"** GUERREIRO: INVESTIDA! ({hitCount} inimigos atingidos) **", EnumChatType.Notification);
            return true;
        }
    }

    [ProtoContract]
    public class AbilityPacket
    {
    }
}

using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Config;

namespace AnimalTransport
{
    public class AnimalTransportSystem : ModSystem
    {
        private ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            api.Logger.Notification("[AnimalTransport] Inicializando sistema Server-Side...");

            // 1. INJEÇÃO DE BEHAVIOR (Cestas e Baús)
            foreach (var coll in api.World.Collectibles)
            {
                if (coll.Code == null) continue;
                
                // Verifica se é um item de transporte válido
                if (coll.Code.Path.Contains("basket-reed") || coll.Code.Path.Contains("chest-"))
                {
                    List<CollectibleBehavior> behaviors = coll.CollectibleBehaviors == null ? 
                        new List<CollectibleBehavior>() : coll.CollectibleBehaviors.ToList();
                    
                    behaviors.Add(new ServerEntityCatchBehavior(coll));
                    coll.CollectibleBehaviors = behaviors.ToArray();
                }
            }

            // 2. EVENTO DE PICKUP (Notificação ao pegar do chão)
            api.Event.OnEntityDespawn += OnEntityDespawn;
        }

        // Detecta quando um item é pego do chão (PickedUp)
        private void OnEntityDespawn(Entity entity, EntityDespawnData reason)
        {
            if (reason == null || reason.Reason != EnumDespawnReason.PickedUp) return;
            if (!(entity is EntityItem entityItem)) return;

            ItemStack stack = entityItem.Itemstack;
            if (stack == null || !stack.Attributes.HasAttribute("capturedEntityData")) return;

            // Recupera dados salvos
            string animalName = stack.Attributes.GetString("entityName");
            string capturer = stack.Attributes.GetString("capturerName");
            string status = stack.Attributes.GetString("healthStatus");

            if (string.IsNullOrEmpty(capturer)) capturer = "Desconhecido";
            if (string.IsNullOrEmpty(status)) status = "?/?";

            // Encontra quem pegou (Player mais próximo em 5 blocos)
            IPlayer[] players = sapi.World.GetPlayersAround(entity.ServerPos.XYZ, 5, 5);
            if (players != null && players.Length > 0)
            {
                // Cast seguro para IServerPlayer
                IServerPlayer player = players[0] as IServerPlayer;
                
                if (player != null)
                {
                    string msg = $"<strong>[AnimalTransport]</strong> Você obteve: <strong>{animalName}</strong> (Status: {status}). Capturado por: {capturer}.";
                    player.SendMessage(GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
                }
            }
        }
    }

    // Behavior customizado que roda APENAS no servidor
    public class ServerEntityCatchBehavior : CollectibleBehavior
    {
        public ServerEntityCatchBehavior(CollectibleObject coll) : base(coll) { }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            // Garante que é Server-Side e um Jogador
            if (byEntity.World.Side != EnumAppSide.Server) return;
            if (!(byEntity is EntityPlayer playerEntity)) return;
            
            IServerPlayer byPlayer = byEntity.World.PlayerByUid(playerEntity.PlayerUID) as IServerPlayer;
            if (byPlayer == null) return;

            // --- LÓGICA DE LIBERAÇÃO (RELEASE) ---
            if (slot.Itemstack.Attributes.HasAttribute("capturedEntityData"))
            {
                if (blockSel != null)
                {
                    handHandling = EnumHandHandling.PreventDefault;
                    handling = EnumHandling.PreventDefault; // IMPEDE colocar o bloco no chão

                    ReleaseAnimal(slot, byEntity, blockSel.Position.AddCopy(blockSel.Face));
                }
                return;
            }

            // --- LÓGICA DE CAPTURA (CATCH) ---
            if (entitySel != null && entitySel.Entity != null)
            {
                handHandling = EnumHandHandling.PreventDefault;
                handling = EnumHandling.PreventDefault; // IMPEDE interação vanilla

                CaptureAnimal(slot, byEntity, entitySel.Entity, byPlayer);
            }
        }

        private void CaptureAnimal(ItemSlot slot, EntityAgent byEntity, Entity target, IServerPlayer player)
        {
            // Valida tamanho
            Cuboidf collisionBox = target.CollisionBox;
            if (collisionBox.Length > 1.2 || collisionBox.Width > 1.2 || collisionBox.Height > 1.2) {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "[AnimalTransport] Animal muito grande!", EnumChatType.Notification);
                return;
            }

            try {
                ITreeAttribute captureTree = new TreeAttribute();
                MethodInfo toAttrMethod = target.GetType().GetMethod("ToAttribute", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                if (toAttrMethod != null) {
                    toAttrMethod.Invoke(target, new object[] { captureTree, true });
                    
                    // Salva dados técnicos
                    slot.Itemstack.Attributes["capturedEntityData"] = captureTree;
                    slot.Itemstack.Attributes.SetString("entityClass", target.Code.ToString());
                    
                    // Salva dados informativos
                    string name = target.GetName();
                    slot.Itemstack.Attributes.SetString("entityName", name);
                    slot.Itemstack.Attributes.SetString("capturerName", player.PlayerName);

                    // CORREÇÃO: Lê a vida direto dos atributos observados (sem depender de EntityBehaviorHealth)
                    float currentHealth = target.WatchedAttributes.GetFloat("health", 0);
                    float maxHealth = target.WatchedAttributes.GetFloat("maxhealth", 1);
                    // Se maxHealth for 0 ou 1 estranho, tenta pegar do attributesTree se disponível, ou assume padrão
                    if (maxHealth <= 1 && currentHealth > 1) maxHealth = 20; // Fallback visual
                    
                    string healthStatus = $"{currentHealth:0.#}/{maxHealth:0.#}";
                    slot.Itemstack.Attributes.SetString("healthStatus", healthStatus);

                    slot.MarkDirty();
                    
                    target.Die(EnumDespawnReason.PickedUp);
                    byEntity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/squish1"), byEntity, null, true, 32, 1f);

                    player.SendMessage(GlobalConstants.GeneralChatGroup, $"[AnimalTransport] Capturado: {name} (Vida: {healthStatus})", EnumChatType.Notification);
                }
            } catch (Exception e) {
                byEntity.World.Logger.Error("[AnimalTransport] Erro Capture: " + e.Message);
            }
        }

        private void ReleaseAnimal(ItemSlot slot, EntityAgent byEntity, BlockPos pos)
        {
             try {
                ITreeAttribute data = slot.Itemstack.Attributes.GetTreeAttribute("capturedEntityData");
                string cls = slot.Itemstack.Attributes.GetString("entityClass");
                
                Entity newEntity = byEntity.World.ClassRegistry.CreateEntity(new AssetLocation(cls));
                if (newEntity != null) {
                     MethodInfo fromAttrMethod = newEntity.GetType().GetMethod("FromAttribute", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                     
                     if (fromAttrMethod != null) {
                        fromAttrMethod.Invoke(newEntity, new object[] { data, byEntity.World });
                        
                        newEntity.ServerPos.SetPos(pos.ToVec3d().Add(0.5, 0, 0.5));
                        newEntity.Pos.SetPos(newEntity.ServerPos);
                        
                        byEntity.World.SpawnEntity(newEntity);

                        slot.Itemstack.Attributes.RemoveAttribute("capturedEntityData");
                        slot.Itemstack.Attributes.RemoveAttribute("entityClass");
                        slot.Itemstack.Attributes.RemoveAttribute("entityName");
                        slot.Itemstack.Attributes.RemoveAttribute("capturerName");
                        slot.Itemstack.Attributes.RemoveAttribute("healthStatus");
                        slot.MarkDirty();

                        byEntity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/squish2"), byEntity, null, true, 32, 1f);
                     }
                }
             } catch (Exception e) {
                 byEntity.World.Logger.Error("[AnimalTransport] Erro Release: " + e.Message);
             }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;

namespace LotteryMod
{
    public class LotterySystem : ModSystem
    {
        private ICoreServerAPI sapi;
        private Random rand = new Random();

        // Listas de prêmios
        private List<CollectibleObject> foodPool = new List<CollectibleObject>();
        private List<CollectibleObject> currencyPool = new List<CollectibleObject>();
        private List<CollectibleObject> jackpotPool = new List<CollectibleObject>();

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;

            // LOG DE DEBUG: Se isso não aparecer no console, o mod não carregou.
            api.Logger.Notification("[LotteryMod] Sistema de Apostas INICIADO. Registrando comandos...");

            // Carrega os itens apenas quando o jogo estiver rodando (para garantir que os itens existem)
            api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, OnRunGame);

            // Registro do comando
            api.ChatCommands.Create("apostar")
                .WithDescription("Segure um stack de itens e digite para apostar.")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(OnBetCommand);
        }

        private void OnRunGame()
        {
            foodPool.Clear();
            currencyPool.Clear();
            jackpotPool.Clear();

            // Popula as listas de prêmios
            foreach (var collectible in sapi.World.Collectibles)
            {
                if (collectible == null || collectible.Code == null) continue;
                if (collectible.CreativeInventoryTabs == null || collectible.CreativeInventoryTabs.Length == 0) continue;

                // 1. Pool de Comida
                if (collectible.NutritionProps != null)
                {
                    foodPool.Add(collectible);
                }

                // 2. Pool de Moeda (Pepitas e Engrenagens)
                string path = collectible.Code.Path;
                if (path.Contains("nugget") || path.Contains("gear-temporal"))
                {
                    currencyPool.Add(collectible);
                }

                // 3. Jackpot (Qualquer item válido)
                jackpotPool.Add(collectible);
            }

            sapi.Logger.Notification($"[LotteryMod] Tabelas carregadas: {foodPool.Count} comidas, {currencyPool.Count} moedas.");
        }

        private TextCommandResult OnBetCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            
            // Segurança extra: verifica se inventory manager existe
            if (player == null || player.InventoryManager == null) return TextCommandResult.Error("Erro ao acessar inventário.");

            ItemSlot activeSlot = player.InventoryManager.ActiveHotbarSlot;

            // 1. Validação: Mão vazia?
            if (activeSlot.Empty)
            {
                return TextCommandResult.Error("Segure um stack de itens na mao para apostar (ex: terra, cascalho).");
            }

            // 2. Consome o item
            int amountBet = activeSlot.StackSize;
            string betItemName = activeSlot.Itemstack.GetName();
            
            // Remove o item da mão
            activeSlot.TakeOutWhole();
            activeSlot.MarkDirty(); 

            // 3. Rola a sorte (0.0 a 100.0)
            double roll = rand.NextDouble() * 100.0;
            
            // Faixas de Probabilidade:
            // 00.0 - 88.5 : Perdeu (88.5%)
            // 88.5 - 97.5 : Comida (9.0%)
            // 97.5 - 99.0 : Moeda (1.5%)
            // 99.0 - 100.0: Jackpot (1.0%)

            if (roll < 88.5)
            {
                // PERDEU
                player.SendMessage(GlobalConstants.GeneralChatGroup, $"[Cassino] Você apostou {amountBet}x {betItemName} e perdeu tudo.", EnumChatType.Notification);
                sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/toolbreak"), player.Entity);
                return TextCommandResult.Success("");
            }
            else if (roll < 97.5)
            {
                GiveRandomReward(player, foodPool, "Prêmio Saboroso", 1, 5);
            }
            else if (roll < 99.0)
            {
                GiveRandomReward(player, currencyPool, "Prêmio Brilhante", 1, 3);
            }
            else
            {
                // JACKPOT
                GiveRandomReward(player, jackpotPool, "JACKPOT LENDÁRIO!!", 1, 1);
                sapi.SendMessageToGroup(GlobalConstants.GeneralChatGroup, $"<strong>O JOGADOR {player.PlayerName.ToUpper()} ACERTOU O 1% NA LOTERIA!</strong>", EnumChatType.Notification);
            }

            return TextCommandResult.Success("");
        }

        private void GiveRandomReward(IServerPlayer player, List<CollectibleObject> pool, string tierName, int minAmount, int maxAmount)
        {
            if (pool.Count == 0) return;

            CollectibleObject reward = pool[rand.Next(pool.Count)];
            int amount = rand.Next(minAmount, maxAmount + 1);
            
            ItemStack stack = new ItemStack(reward, amount);

            if (!player.Entity.TryGiveItemStack(stack))
            {
                sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
            }

            player.SendMessage(GlobalConstants.GeneralChatGroup, $"<strong>[{tierName}]</strong> Você ganhou {amount}x {stack.GetName()}!", EnumChatType.Notification);
            sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/cashregister"), player.Entity);
        }
    }
}
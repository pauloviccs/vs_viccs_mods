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

        // Caches para as listas de prêmios (carregados na inicialização para não lagar o comando)
        private List<CollectibleObject> foodPool = new List<CollectibleObject>();
        private List<CollectibleObject> currencyPool = new List<CollectibleObject>();
        private List<CollectibleObject> jackpotPool = new List<CollectibleObject>();

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;

            // Carrega os itens quando os assets estiverem prontos
            api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, OnRunGame);

            api.ChatCommands.Create("apostar")
                .WithDescription("Consome o stack na mao para tentar a sorte.")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(OnBetCommand);
        }

        private void OnRunGame()
        {
            // Popula as listas de prêmios
            foreach (var collectible in sapi.World.Collectibles)
            {
                if (collectible == null || collectible.Code == null) continue;

                // FILTRO DE SEGURANÇA: Só aceita itens que aparecem no menu criativo (itens "reais")
                if (collectible.CreativeInventoryTabs == null || collectible.CreativeInventoryTabs.Length == 0) continue;

                // 1. Pool de Comida
                if (collectible.NutritionProps != null)
                {
                    foodPool.Add(collectible);
                }

                // 2. Pool de Moeda (Pepitas e Engrenagens)
                // Procura por "nugget" ou "gear-temporal" no código do item
                string path = collectible.Code.Path;
                if (path.Contains("nugget") || path.Contains("gear-temporal"))
                {
                    currencyPool.Add(collectible);
                }

                // 3. Jackpot (Tudo)
                jackpotPool.Add(collectible);
            }

            sapi.Logger.Event($"[Lottery] Carregado: {foodPool.Count} comidas, {currencyPool.Count} moedas, {jackpotPool.Count} itens totais.");
        }

        private TextCommandResult OnBetCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            ItemSlot activeSlot = player.InventoryManager.ActiveHotbarSlot;

            // 1. Validação: Mão vazia?
            if (activeSlot.Empty)
            {
                return TextCommandResult.Error("Segure um stack de itens na mao para apostar (ex: terra, cascalho).");
            }

            // 2. O Preço: Consome TODO o stack
            int amountBet = activeSlot.StackSize;
            string betItemName = activeSlot.Itemstack.GetName();
            activeSlot.TakeOutWhole();
            activeSlot.MarkDirty();

            // 3. A Roleta (0.0 a 100.0)
            double roll = rand.NextDouble() * 100.0;
            
            // Lógica de Faixas de Probabilidade
            // 0 a 88.5 (88.5%) -> Perdeu
            // 88.5 a 97.5 (9%) -> Comida
            // 97.5 a 99.0 (1.5%) -> Moeda
            // 99.0 a 100.0 (1%) -> Jackpot

            if (roll < 88.5)
            {
                // PERDEU
                player.SendMessage(GlobalConstants.GeneralChatGroup, $"Você apostou {amountBet}x {betItemName} e... não deu nada. A casa agradece.", EnumChatType.Notification);
                sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/toolbreak"), player.Entity);
                return TextCommandResult.Success("");
            }
            else if (roll < 97.5)
            {
                // GANHOU COMIDA
                GiveRandomReward(player, foodPool, "Prêmio Saboroso", 1, 5); // 1 a 5 comidas
            }
            else if (roll < 99.0)
            {
                // GANHOU MOEDA
                GiveRandomReward(player, currencyPool, "Prêmio Brilhante", 1, 3);
            }
            else
            {
                // JACKPOT
                GiveRandomReward(player, jackpotPool, "JACKPOT LENDÁRIO!!", 1, 1);
                
                // Avisa o servidor todo no caso de Jackpot
                sapi.SendMessageToGroup(GlobalConstants.GeneralChatGroup, $"<strong>{player.PlayerName} ACERTOU O 1% NA LOTERIA!</strong>", EnumChatType.Notification);
            }

            return TextCommandResult.Success("");
        }

        private void GiveRandomReward(IServerPlayer player, List<CollectibleObject> pool, string tierName, int minAmount, int maxAmount)
        {
            if (pool.Count == 0) return;

            // Escolhe item aleatório da lista
            CollectibleObject reward = pool[rand.Next(pool.Count)];
            int amount = rand.Next(minAmount, maxAmount + 1);
            
            ItemStack stack = new ItemStack(reward, amount);

            // Tenta dar ao jogador
            if (!player.Entity.TryGiveItemStack(stack))
            {
                // Se inventário cheio, dropa no chão
                sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
            }

            // Feedback
            player.SendMessage(GlobalConstants.GeneralChatGroup, $"<strong>[{tierName}]</strong> Você ganhou {amount}x {stack.GetName()}!", EnumChatType.Notification);
            sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/cashregister"), player.Entity);
        }
    }
}
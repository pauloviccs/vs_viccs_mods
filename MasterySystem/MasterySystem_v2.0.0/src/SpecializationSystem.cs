using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;
using ProtoBuf;

namespace MasteryTitles
{
    public class SpecializationSystem : ModSystem
    {
         private ICoreClientAPI capi;
         private ICoreServerAPI sapi;
         private IClientNetworkChannel clientChannel;
         private IServerNetworkChannel serverChannel;

         public override void Start(ICoreAPI api)
         {
             api.Network.RegisterChannel("masteryspec")
                 .RegisterMessageType<OpenSpecGuiPacket>()
                 .RegisterMessageType<ChooseSpecPacket>();
         }

         public override void StartServerSide(ICoreServerAPI api)
         {
             sapi = api;
             serverChannel = api.Network.GetChannel("masteryspec");
             serverChannel.SetMessageHandler<ChooseSpecPacket>(OnSpecChosen);

             var mastery = api.ModLoader.GetModSystem<MasterySystem>();
             if (mastery != null)
             {
                 mastery.PlayerLeveledUp += OnPlayerLeveledUp;
             }

             api.Event.BreakBlock += OnBlockBreak;
         }

         public override void StartClientSide(ICoreClientAPI api)
         {
             capi = api;
             clientChannel = api.Network.GetChannel("masteryspec");
             clientChannel.SetMessageHandler<OpenSpecGuiPacket>(OnOpenGuiRequest);
         }

         private void OnPlayerLeveledUp(IServerPlayer player, MasteryType type, int level)
         {
             if (level == 2)
             {
                 serverChannel.SendPacket(new OpenSpecGuiPacket() { MasteryType = type }, player);
             }
         }

         private void OnOpenGuiRequest(OpenSpecGuiPacket packet)
         {
             new SpecializationGui(capi, packet.MasteryType, clientChannel).TryOpen();
         }

         private void OnSpecChosen(IServerPlayer player, ChooseSpecPacket packet)
         {
             ITreeAttribute tree = player.Entity.WatchedAttributes.GetOrAddTreeAttribute("mastery_specs");
             tree.SetString(packet.MasteryType.ToString(), packet.ChoiceId);
             player.Entity.WatchedAttributes.MarkPathDirty("mastery_specs");
             
             ApplySpecBuffs(player, packet.MasteryType, packet.ChoiceId);
             
             player.SendMessage(0, $"Especialização escolhida: {packet.ChoiceId}", EnumChatType.Notification);
         }

         public void ApplySpecBuffs(IServerPlayer player, MasteryType type, string specId)
         {
             // Static Buffs
             if (specId == "excavator")
                 player.Entity.Stats.Set("miningSpeedMultiplier", "spec", 0.3f, true);
             
             if (specId == "lumber_velocity")
                  player.Entity.Stats.Set("miningSpeedMultiplier", "spec", 0.3f, true); // Works for axes too usually
             
             if (specId == "berserker")
             {
                 player.Entity.Stats.Set("meleeWeaponsDamagePercent", "spec", 0.45f, true);
                 player.Entity.Stats.Set("damageResistance", "spec", -0.1f, true); // Glass cannon
             }

             if (specId == "tank")
             {
                 player.Entity.Stats.Set("maxhealthExtraPoints", "spec", 10f, true);
                 player.Entity.Stats.Set("damageResistance", "spec", 0.2f, true);
                 player.Entity.Stats.Set("walkspeed", "spec", -0.1f, true);
             }
         }

         private void OnBlockBreak(IServerPlayer player, BlockSelection blockSel, ref float dropQuantityMultiplier, ref EnumHandling handling)
         {
             ITreeAttribute tree = player.Entity.WatchedAttributes.GetTreeAttribute("mastery_specs");
             if (tree == null) return;

             Block block = sapi.World.BlockAccessor.GetBlock(blockSel.Position);
             string code = block.Code.Path;

             // Geologist: Double Ores
             if (code.Contains("ore") || code.Contains("quartz"))
             {
                 if (tree.GetString("Mining") == "geologist") dropQuantityMultiplier *= 2;
             }
             
             // Forester: Double Wood (Chance)
             if (code.Contains("log"))
             {
                 if (tree.GetString("Lumbering") == "forester" && sapi.World.Rand.NextDouble() < 0.2) 
                     dropQuantityMultiplier *= 2;
             }

             // Farmer: More Crops
             if (block.BlockMaterial == EnumBlockMaterial.Plant || code.Contains("crop"))
             {
                 if (tree.GetString("Farming") == "farmer") dropQuantityMultiplier *= 1.5f;
             }
         }
    }

    public class SpecializationGui : GuiDialog
    {
        private MasteryType type;
        private IClientNetworkChannel channel;

        public SpecializationGui(ICoreClientAPI capi, MasteryType type, IClientNetworkChannel channel) : base(capi)
        {
            this.type = type;
            this.channel = channel;
            SetupDialog();
        }

        public override string ToggleKeyCombinationCode => null;

        private void SetupDialog()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            ElementBounds textBounds = ElementBounds.Fixed(0, 30, 400, 30);
            ElementBounds btn1Bounds = ElementBounds.Fixed(0, 80, 190, 40);
            ElementBounds btn2Bounds = btn1Bounds.RightCopy(20);
            
            bgBounds.WithChildren(textBounds, btn1Bounds, btn2Bounds);

            GetOptions(type, out string opt1, out string opt2);

            SingleComposer = capi.Gui.CreateCompo("specgui", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Especialização Mastery", OnClose)
                .AddStaticText("Escolha seu caminho para sempre:", CairoFont.WhiteSmallText(), textBounds)
                .AddButton(opt1, () => SendChoice(GetChoiceId(type, 1)), btn1Bounds)
                .AddButton(opt2, () => SendChoice(GetChoiceId(type, 2)), btn2Bounds)
                .Compose();
        }
        
        private void GetOptions(MasteryType type, out string o1, out string o2)
        {
             if (type == MasteryType.Mining) { o1 = "Escavador"; o2 = "Geólogo"; }
             else if (type == MasteryType.Lumbering) { o1 = "Veloz"; o2 = "Silvicultor"; }
             else if (type == MasteryType.Farming) { o1 = "Fazendeiro"; o2 = "Herbalista"; }
             else { o1 = "Berserker"; o2 = "Tanque"; }
        }

        private string GetChoiceId(MasteryType type, int idx)
        {
            if (type == MasteryType.Mining) return idx == 1 ? "excavator" : "geologist";
            if (type == MasteryType.Lumbering) return idx == 1 ? "lumber_velocity" : "forester";
            if (type == MasteryType.Farming) return idx == 1 ? "farmer" : "herbalist";
            return idx == 1 ? "berserker" : "tank";
        }

        private bool SendChoice(string choiceId)
        {
            channel.SendPacket(new ChooseSpecPacket() { MasteryType = type, ChoiceId = choiceId });
            TryClose();
            return true;
        }

        private void OnClose() { TryClose(); }
    }

    [ProtoContract]
    public class OpenSpecGuiPacket
    {
        [ProtoMember(1)]
        public MasteryType MasteryType;
    }

    [ProtoContract]
    public class ChooseSpecPacket
    {
        [ProtoMember(1)]
        public MasteryType MasteryType;
        [ProtoMember(2)]
        public string ChoiceId;
    }
}

using Vintagestory.API.Common;
using HarmonyLib;

namespace AnimalTransport
{
    public class AnimalTransportModSystem : ModSystem
    {
        private Harmony harmony;

        public override void Start(ICoreAPI api)
        {
            // Carrega os patches quando o mod inicia
            harmony = new Harmony("com.viccs.animaltransport");
            harmony.PatchAll(); 
            api.Logger.Notification("Animal Transport: Patches carregados.");
        }

        public override void Dispose()
        {
            // Limpa a bagunça ao sair
            harmony?.UnpatchAll("com.viccs.animaltransport");
        }
    }
}
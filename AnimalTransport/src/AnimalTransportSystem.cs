using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using HarmonyLib;
using System.Reflection;

namespace AnimalTransport
{
    public class AnimalTransportSystem : ModSystem
    {
        private Harmony harmony;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterCollectibleBehaviorClass("AnimalTransport.CollectibleBehaviorEntityCatch", typeof(CollectibleBehaviorEntityCatch));

            if (api.Side == EnumAppSide.Client || api.Side == EnumAppSide.Server)
            {
                harmony = new Harmony("animaltransport.fixes");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
        }

        public override void Dispose()
        {
            if (harmony != null)
            {
                harmony.UnpatchAll("animaltransport.fixes");
            }
            base.Dispose();
        }
    }
}

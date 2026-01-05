using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LandBaron
{
    public class ChunkWireframeRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        public bool Enabled { get; set; } = false;

        public ChunkWireframeRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;
            capi.Event.RegisterRenderer(this, EnumRenderStage.AfterFinalComposition, "landbaron-chunkwireframe");
        }

        public double RenderOrder => 1.0;

        public int RenderRange => 9999;

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (!Enabled || capi.World.Player == null) return;

            // Pega a posição do jogador
            EntityPlayer entity = capi.World.Player.Entity;
            BlockPos playerPos = entity.Pos.AsBlockPos;

            // Calcula o início do Chunk (0,0,0 relativo ao chunk)
            int chunkX = (playerPos.X / 32) * 32;
            int chunkY = (playerPos.Y / 32) * 32;
            int chunkZ = (playerPos.Z / 32) * 32;

            int color = ColorUtil.ToRgba(255, 0, 255, 0); // Verde

            // Desenha as 12 arestas do cubo 32x32x32
            // Base
            DrawLine(chunkX, chunkY, chunkZ, chunkX + 32, chunkY, chunkZ, color);
            DrawLine(chunkX, chunkY, chunkZ, chunkX, chunkY, chunkZ + 32, color);
            DrawLine(chunkX + 32, chunkY, chunkZ, chunkX + 32, chunkY, chunkZ + 32, color);
            DrawLine(chunkX, chunkY, chunkZ + 32, chunkX + 32, chunkY, chunkZ + 32, color);

            // Topo
            DrawLine(chunkX, chunkY + 32, chunkZ, chunkX + 32, chunkY + 32, chunkZ, color);
            DrawLine(chunkX, chunkY + 32, chunkZ, chunkX, chunkY + 32, chunkZ + 32, color);
            DrawLine(chunkX + 32, chunkY + 32, chunkZ, chunkX + 32, chunkY + 32, chunkZ + 32, color);
            DrawLine(chunkX, chunkY + 32, chunkZ + 32, chunkX + 32, chunkY + 32, chunkZ + 32, color);

            // Colunas
            DrawLine(chunkX, chunkY, chunkZ, chunkX, chunkY + 32, chunkZ, color);
            DrawLine(chunkX + 32, chunkY, chunkZ, chunkX + 32, chunkY + 32, chunkZ, color);
            DrawLine(chunkX, chunkY, chunkZ + 32, chunkX, chunkY + 32, chunkZ + 32, color);
            DrawLine(chunkX + 32, chunkY, chunkZ + 32, chunkX + 32, chunkY + 32, chunkZ + 32, color);
        }

        private void DrawLine(double x1, double y1, double z1, double x2, double y2, double z2, int color)
        {
            BlockPos origin = new BlockPos((int)x1, (int)y1, (int)z1);
            capi.Render.RenderLine(
                origin, 
                (float)(x1 - origin.X), (float)(y1 - origin.Y), (float)(z1 - origin.Z),
                (float)(x2 - origin.X), (float)(y2 - origin.Y), (float)(z2 - origin.Z),
                color
            );
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.AfterFinalComposition);
        }
    }
}

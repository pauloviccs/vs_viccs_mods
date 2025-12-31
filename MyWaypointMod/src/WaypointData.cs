using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace SimpleWaypoints
{
    [ProtoContract]
    public class PlayerWaypointData
    {
        [ProtoMember(1)]
        public List<WaypointEntry> Waypoints { get; set; } = new List<WaypointEntry>();
    }

    [ProtoContract]
    public class WaypointEntry
    {
        [ProtoMember(1)]
        public string Name { get; set; } = ""; // Inicializado para evitar null warning
        
        [ProtoMember(2)]
        public Vec3d Position { get; set; } = new Vec3d(); // Inicializado
    }
}
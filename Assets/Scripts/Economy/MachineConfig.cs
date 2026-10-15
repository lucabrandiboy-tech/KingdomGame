using UnityEngine;

namespace KingdomGame.Economy
{
    public enum MachineKind
    {
        /// <summary>Turns iron and wood from storage into steel, straight into storage (Medieval, no power).</summary>
        Smelter,
        /// <summary>Burns wood from storage; machines within its reach work while it burns.</summary>
        Generator,
        /// <summary>Cuts the trees within its reach (needs power); the wood goes out on conveyors.</summary>
        Sawmill,
        /// <summary>Digs stone out of the ground (needs power); the stone goes out on conveyors.</summary>
        Quarry,
        /// <summary>A piece of belt: carries what machines next to it make to a storehouse or chest it touches.</summary>
        Conveyor,
        /// <summary>An electric light: shines only while a generator's power reaches it.</summary>
        Lamp,
        /// <summary>Keeps young trees planted round it, up to <see cref="MachineConfig.output"/> trees within its reach.</summary>
        TreeFarm,
        /// <summary>A crafting table: a Crafter villager works at it, turning <see cref="MachineConfig.inputs"/> into the age's part.</summary>
        Workbench,
        /// <summary>Makes <see cref="MachineConfig.makes"/> by itself (from <see cref="MachineConfig.inputs"/> if any): farms, kilns, drills, auto-crafters.</summary>
        Producer,
    }

    /// <summary>What a machine does by itself, and how fast. Set on a Building Config (Machines).</summary>
    [CreateAssetMenu(menuName = "KingdomGame/Economy/Machine", fileName = "Machine")]
    public sealed class MachineConfig : ScriptableObject
    {
        public MachineKind kind = MachineKind.Sawmill;
        [Tooltip("Seconds per round of work (one batch made, or one load of fuel burnt).")]
        [Min(0.5f)] public float workSeconds = 6f;
        [Tooltip("How much each round makes (wood, stone or steel).")]
        [Min(1)] public int output = 5;
        [Tooltip("Sawmill: how far away it cuts trees. Generator: how far its power reaches. In tiles. Lamp: how big its light is (1 = a brazier's).")]
        [Min(1)] public float reach = 8f;
        [Tooltip("How much it keeps before it stops (when nothing takes its output away).")]
        [Min(1)] public int holds = 60;
        [Tooltip("Generator: wood burnt each round. Smelter: wood used for each batch.")]
        [Min(0)] public int woodIn = 1;
        [Tooltip("Generator: coal burnt each round (it burns wood instead when there's no coal, and wood lasts a third as long).")]
        [Min(0)] public int coalIn = 1;
        [Tooltip("Smelter: iron used for each batch.")]
        [Min(0)] public int ironIn = 2;
        [Tooltip("Sawmills and quarries: only works within a burning generator's reach (Iron Age water and treadwheel ones don't need it).")]
        public bool needsPower = true;
        [Tooltip("Tree farm: what it plants (a tree that grows back when it's cut).")]
        public ResourceNodeConfig plant;
        [Tooltip("Tree farm: seconds until a young tree it plants is grown.")]
        [Min(0)] public float growSeconds = 180f;
        [Header("Crafting tables and producers")]
        [Tooltip("What it makes.")]
        public ResourceType makes = ResourceType.Wood;
        [Tooltip("What each batch uses up, from storage.")]
        public ResourceAmount[] inputs = new ResourceAmount[0];
        [Tooltip("Crafting tables and auto-crafters stop when storage has this many of what they make.")]
        [Min(0)] public int keep = 40;
        [Tooltip("Producers: send what's made out on conveyor belts (like a quarry) instead of straight into storage.")]
        public bool toBelts;
        [Tooltip("How fast things ride the conveyor belts, in tiles a second.")]
        [Min(0.2f)] public float beltSpeed = 2f;
    }
}

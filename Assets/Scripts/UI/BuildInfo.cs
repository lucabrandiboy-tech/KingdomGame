using System.Text;
using KingdomGame.Building;
using KingdomGame.Defenses;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>The box that shows over the build bar when the mouse is on something: what it costs and what it does.</summary>
    public static class BuildInfo
    {
        static readonly StringBuilder text = new StringBuilder(256);

        public static string Describe(BuildingConfig b)
        {
            if (b == null) return "";
            text.Clear();
            text.Append("<b>").Append(b.displayName).Append("</b>\n");
            text.Append("Costs: ").Append(b.CostText()).Append('\n');
            var size = b.size;
            text.Append($"{size.x} x {size.y} tile{(size.x * size.y > 1 ? "s" : "")}");
            if (b.buildSeconds > 0f) text.Append($", {b.buildSeconds:0.#} s of building");
            if (b.health > 0f) text.Append($", health {b.health:0}");
            text.Append('\n');

            if (b.defense != null) Defense(b.defense);
            if (b.machine != null) Machine(b.machine);
            if (b.boost != BoostKind.None)
                text.Append($"+{b.boostPercent:0.#}% {BoostName(b.boost)} for the kingdom (each kind counts once)\n");
            if (b.sleeps > 0) text.Append($"Homes for {b.sleeps} villager{(b.sleeps > 1 ? "s" : "")}\n");
            if (b.lightSize > 0f) text.Append("Gives light at night\n");
            if (b.blocksMovement) text.Append("Blocks the way (zombies too)\n");
            else if (b.kind == BuildingKind.Furniture) text.Append("Can be walked through\n");
            return text.ToString().TrimEnd('\n');
        }

        static void Defense(DefenseConfig d)
        {
            switch (d.kind)
            {
                case DefenseKind.Tower:
                    float hit = d.weapon != null ? d.weapon.hitDamage : 0f;
                    text.Append($"Tower: reach {d.range:0.#} tiles, {hit:0} damage a shot, {d.shotsPerSecond:0.##} shots a second\n");
                    text.Append(d.needsGuard ? "Needs a guard on it (Guard job)" : "Shoots by itself");
                    if (d.pierce > 0) text.Append($", goes through {d.pierce + 1} zombies");
                    if (d.lobbed) text.Append(", lobs over walls");
                    if (d.minRange > 0f) text.Append($", not closer than {d.minRange:0.#} tiles");
                    text.Append('\n');
                    break;
                case DefenseKind.Trap:
                    text.Append($"Trap: {d.damage:0} damage, holds a zombie {d.holdSeconds:0.#} s, ready again after {d.rearmSeconds:0} s\n");
                    break;
                case DefenseKind.Area:
                    text.Append($"Slows zombies to {d.slowTo * 100f:0}% and hurts them {d.damagePerSecond:0.#} a second\n");
                    break;
                case DefenseKind.Mine:
                    text.Append($"Mine: blows up under the first zombie ({d.damage:0} damage)\n");
                    break;
                case DefenseKind.Barrier:
                    text.Append(d.thorns > 0f ? $"Barrier: zombies hitting it take {d.thorns:0} damage\n" : "Barrier: zombies have to break it\n");
                    break;
                case DefenseKind.Cloud:
                    text.Append($"Cloud trap: {d.cloudDamagePerSecond:0} damage a second for {d.cloudSeconds:0} s, {d.cloudRadius:0.#} tiles round\n");
                    break;
            }
        }

        static void Machine(MachineConfig m)
        {
            switch (m.kind)
            {
                case MachineKind.Generator:
                    text.Append($"Generator: powers machines and lamps within {m.reach:0} tiles; burns coal, or wood (which goes 3 times as fast)\n");
                    break;
                case MachineKind.Conveyor:
                    text.Append($"Conveyor belt: carries things along at {m.beltSpeed:0.#} tiles a second\n");
                    break;
                case MachineKind.Lamp:
                    text.Append("Electric light\n");
                    break;
                case MachineKind.TreeFarm:
                    text.Append($"Plants trees within {m.reach:0} tiles; they grow back after they're cut\n");
                    break;
                case MachineKind.Sawmill:
                    text.Append($"Cuts trees within {m.reach:0} tiles by itself: {m.output} wood every {m.workSeconds:0.#} s\n");
                    break;
                case MachineKind.Quarry:
                    text.Append($"Digs {m.output} stone every {m.workSeconds:0.#} s by itself\n");
                    break;
                case MachineKind.Smelter:
                    text.Append($"Smelts {m.ironIn} iron with {m.woodIn} wood into {m.output} steel every {m.workSeconds:0.#} s\n");
                    break;
                case MachineKind.Workbench:
                    text.Append($"Crafting table: a Crafter makes {ResourceText.Name(m.makes)} here ({m.output} a batch{Inputs(m)})\n");
                    break;
                default:
                    text.Append($"Makes {m.output} {ResourceText.Name(m.makes)} every {m.workSeconds:0.#} s by itself{Inputs(m)}\n");
                    break;
            }
            // (only these kinds look for power: see Machine.Work)
            bool usesPower = m.kind == MachineKind.Producer || m.kind == MachineKind.Sawmill || m.kind == MachineKind.Quarry || m.kind == MachineKind.Lamp;
            if (m.needsPower && usesPower) text.Append("Needs power (a generator nearby)\n");
        }

        static string Inputs(MachineConfig m)
        {
            if (m.inputs == null || m.inputs.Length == 0) return "";
            var from = new StringBuilder(", from ");
            for (int i = 0; i < m.inputs.Length; i++)
            {
                if (i > 0) from.Append(i == m.inputs.Length - 1 ? " and " : ", ");
                from.Append($"{m.inputs[i].amount} {ResourceText.Name(m.inputs[i].type)}");
            }
            return from.ToString();
        }

        static string BoostName(BoostKind k) =>
            k == BoostKind.Speed ? "walking speed" : k == BoostKind.Gathering ? "gathering" : k == BoostKind.Crafting ? "crafting"
            : k == BoostKind.Building ? "building" : k == BoostKind.Research ? "research speed" : "";
    }
}

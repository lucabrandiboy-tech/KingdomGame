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
            if (b.water) text.Append("Villagers fetch water here to put out fires\n");
            if (b.flammable) text.Append("Burns: it can catch fire\n");
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
                    text.Append($"Generator: {Factory.Watts(m.powerOutput)} for what's within {m.reach:0} tiles and every power pole wired to it; burns coal, or wood (a third as long), only for what's used\n");
                    break;
                case MachineKind.PowerPole:
                    text.Append($"Power pole: wires to poles within {m.wireReach:0.#} tiles and powers a {m.supplyArea:0} x {m.supplyArea:0} square round it\n");
                    break;
                case MachineKind.Accumulator:
                    text.Append($"Accumulator: stores {m.storeMJ:0.#} MJ of spare power and gives it back (up to {Factory.Watts(m.powerOutput)}) when the network is short\n");
                    break;
                case MachineKind.Solar:
                    text.Append($"Solar panel: {Factory.Watts(m.powerOutput)} in full sun, none at night, no fuel\n");
                    break;
                case MachineKind.Conveyor:
                    text.Append($"Conveyor belt: carries things along at {m.beltSpeed:0.#} tiles a second\n");
                    break;
                case MachineKind.Lamp:
                    text.Append($"Electric light ({Factory.Watts(m.powerUse)})\n");
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
                case MachineKind.Arm:
                    text.Append($"Arm: picks things up behind it and puts them down in front, a swing every {m.swingSeconds * 2f:0.##} s; E beside it: choose what it moves\n");
                    text.Append(m.burner ? "Burns wood or coal (it takes its own from where it picks up)\n" : "Electric\n");
                    break;
                case MachineKind.Furnace:
                    text.Append($"Furnace: smelts what arms put in it (iron ore into iron, copper ore into copper, iron into steel) at {m.craftSpeed:0.##}x speed\n");
                    text.Append(m.burner ? "Burns wood or coal that arms put in it\n" : "Electric\n");
                    break;
                case MachineKind.Assembler:
                    text.Append($"Assembler: makes the recipe you choose (E beside it) from what arms put in it, at {m.craftSpeed:0.##}x speed\n");
                    text.Append(m.burner ? "Burns wood or coal that arms put in it\n" : "Electric\n");
                    break;
                case MachineKind.Drill:
                    text.Append($"Drill: digs the ore field under it{(m.mineReach > 0 ? $" and {m.mineReach} tile{(m.mineReach == 1 ? "" : "s")} round it" : "")}, " +
                                $"{60f / Mathf.Max(0.05f, m.mineSeconds):0.#} a minute, out of its chute onto a belt or into a chest or furnace\n");
                    text.Append(m.burner ? "Burns wood or coal that arms (or another drill) put in it\n" : "Electric\n");
                    break;
                default:
                    text.Append($"Makes {m.output} {ResourceText.Name(m.makes)} every {m.workSeconds:0.#} s by itself{Inputs(m)}\n");
                    break;
            }
            // (only these kinds look for power: see Machine.Work)
            bool usesPower = m.kind == MachineKind.Producer || m.kind == MachineKind.Sawmill || m.kind == MachineKind.Quarry || m.kind == MachineKind.Lamp
                || (!m.burner && (m.kind == MachineKind.Arm || m.kind == MachineKind.Furnace || m.kind == MachineKind.Assembler || m.kind == MachineKind.Drill));
            if ((m.needsPower || m.kind == MachineKind.Lamp) && usesPower)
                text.Append("Needs electricity: a generator's reach or a power pole's area\n");
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

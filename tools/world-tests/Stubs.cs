using System;

// The rest of what NatureMap.cs and ResourceNodeConfig.cs need from Unity, as plain C# stand-ins.
namespace UnityEngine
{
    public class Object
    {
        public static int Destroyed;
        public static void Destroy(Object o) => Destroyed++;
    }

    public class ScriptableObject : Object
    {
        public string name;
    }

    /// <summary>The tests set the clock and frame by hand.</summary>
    public static class Time
    {
        public static float time;
        public static int frameCount;
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string menuName, fileName;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string text) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MinAttribute : Attribute
    {
        public MinAttribute(float min) { }
    }
}

namespace KingdomGame.Economy
{
    /// <summary>
    /// Stands in for the real object (a MonoBehaviour): just what the map asks of a shown tree,
    /// and a state it writes back, like the real one does before it goes or the game saves.
    /// </summary>
    public sealed class ResourceNode : UnityEngine.Object
    {
        public ResourceNodeConfig Config;
        public int Left;
        public bool Unbound;
        NatureMap map;
        UnityEngine.Vector2Int cell;

        public bool Empty => Left <= 0;
        public UnityEngine.Object gameObject => this;

        public void Bind(NatureMap nature, UnityEngine.Vector2Int at)
        {
            map = nature;
            cell = at;
            map.Attach(cell, this);
            map.StateOf(cell, out Left, out _, out _, out _);
        }

        public void Unbind()
        {
            map?.Detach(cell, this);
            map = null;
            Unbound = true;
        }

        public void WriteBack() => map?.Record(cell, Left, false, 0f, 0f, false);
    }
}

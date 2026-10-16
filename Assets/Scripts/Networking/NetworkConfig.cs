using UnityEngine;

namespace KingdomGame.Networking
{
    /// <summary>
    /// Settings for playing online. The server's can also be set when starting it from the
    /// command line (-port, -maxplayers, -servername), which wins over these. A dedicated
    /// server keeps its world in a file (-world name picks another one, -newworld starts over).
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Network Config", fileName = "NetworkConfig")]
    public sealed class NetworkConfig : ScriptableObject
    {
        [Header("Server")]
        [Tooltip("The UDP port the server listens on. Friends joining over the internet need it forwarded on your router (see docs/ONLINE-TEST.md).")]
        [Range(1024, 65535)] public int port = Protocol.DefaultPort;
        [Tooltip("Most players on at once.")]
        [Range(1, 200)] public int maxPlayers = 16;
        [Tooltip("The name players see.")]
        public string serverName = "Kingdom server";
        [Tooltip("0 = a new random world for each new world. Any other number always makes the same world.")]
        public int seed = 0;
        [Tooltip("Tiles between players' kingdoms (throne to throne). Keep it at least the Kingdom Config's Lands Size, so kingdoms' lands don't overlap.")]
        [Range(24, 4000)] public int kingdomSpacing = 560;
        [Tooltip("Frames a second the server runs at. It has no screen to draw, so it doesn't need many.")]
        [Range(10, 120)] public int serverFrameRate = 30;

        [Header("Sending")]
        [Tooltip("Players are told about others within this many tiles.")]
        [Range(16, 256)] public float interestRadius = 64f;
        [Tooltip("How many times a second the server tells players where the others are.")]
        [Range(5, 60)] public float snapshotRate = 15f;
        [Tooltip("How many times a second a player's game tells the server where they are.")]
        [Range(5, 60)] public float sendRate = 15f;
        [Tooltip("Moves faster than the ruler's walking speed times this are put back (a little slack for lag).")]
        [Range(1f, 4f)] public float speedSlack = 1.6f;

        [Header("Playing together")]
        [Tooltip("Your zombies within this many tiles of another player are shown in their game (they can shoot them, and be bitten).")]
        [Range(16, 96)] public float shareZombiesWithin = 40f;
        [Tooltip("Seconds the ride to a friend's kingdom (or home) takes (O: the online panel).")]
        [Range(0, 60)] public float rideSeconds = 6f;
        [Tooltip("Seconds a trade offer waits for an answer before it's turned down by itself.")]
        [Range(10, 300)] public float tradeSeconds = 60f;
    }
}

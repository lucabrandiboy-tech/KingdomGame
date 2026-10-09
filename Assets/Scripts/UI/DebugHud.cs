using System;
using System.Text;
using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>
    /// Top-left text box with frame rate and destruction numbers, plus help text.
    /// FPS is the average over the last second; "worst frame" is the slowest
    /// single frame in that second (what you feel as a stutter).
    /// </summary>
    public sealed class DebugHud : MonoBehaviour
    {
        [TextArea] public string help = "";
        /// <summary>Extra lines added by other scripts (the stress test uses it).</summary>
        public Func<string> extraLines;
        public bool visible = true;
        [Tooltip("Key that shows and hides this box.")]
        public KeyCode toggleKey = KeyCode.H;

        const int History = 512;
        readonly float[] frameTimes = new float[History];
        int next, filled;
        readonly StringBuilder text = new StringBuilder(512);
        GUIStyle style;

        public float AverageFps { get; private set; }
        public float WorstFrameMs { get; private set; }

        void Update()
        {
            if (GameInput.Pressed(toggleKey)) visible = !visible;

            frameTimes[next] = Time.unscaledDeltaTime;
            next = (next + 1) % History;
            filled = Mathf.Min(filled + 1, History);

            float total = 0, worst = 0;
            int frames = 0;
            for (int i = 1; i <= filled && total < 1f; i++)
            {
                float t = frameTimes[(next - i + History) % History];
                total += t;
                worst = Mathf.Max(worst, t);
                frames++;
            }
            AverageFps = total > 0 ? frames / total : 0;
            WorstFrameMs = worst * 1000f;
        }

        void OnGUI()
        {
            if (!visible) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, wordWrap = false };
            style.fontSize = Mathf.Max(12, Screen.height / 54);

            var world = PixelWorld.Current;
            text.Clear();
            text.Append($"<b>{AverageFps:0} FPS</b>   worst frame {WorstFrameMs:0.0} ms\n");
            if (world != null)
            {
                text.Append($"Chunks: {world.ChunkCount} ({world.DamagedChunkCount} damaged)   Pixels destroyed: {world.PixelsDestroyed:N0}\n");
                text.Append($"Flying pixels: {world.DebrisCount} / {world.Settings.maxDebris}   Rubble tiles: {world.RubbleTileCount}\n");
            }
            if (extraLines != null) text.Append(extraLines()).Append('\n');
            if (!string.IsNullOrEmpty(help)) text.Append(help).Append('\n');
            text.Append($"{toggleKey}: hide this box");

            var content = new GUIContent(text.ToString());
            var size = style.CalcSize(content);
            GUI.Box(new Rect(10, 10, size.x + 8, size.y + 4), content, style);
        }
    }
}

using System;
using System.Collections.Generic;
using KingdomGame.Character;
using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>
    /// Chat when playing online: Enter opens a line to type in (the ruler stands still
    /// meanwhile), Enter sends it, Esc cancels. The last few messages show bottom left
    /// for a while; all of them while typing.
    /// </summary>
    public sealed class ChatBox : MonoBehaviour
    {
        const int Keep = 40, Shown = 8;
        const float ShowSeconds = 12f;

        readonly List<(string Text, float At)> lines = new List<(string, float)>();
        bool typing;
        string draft = "";
        int openedFrame = -1;
        GUIStyle style, field;

        /// <summary>Sends what was typed.</summary>
        public Action<string> Send { get; set; }
        public RulerController Ruler { get; set; }
        public bool Typing => typing;

        /// <summary>A message from someone.</summary>
        public void Add(string from, string text) => Line($"<b><color=#e8c27a>{from}:</color></b> {text}");

        /// <summary>Something that happened (someone joined or left).</summary>
        public void Notice(string text) => Line($"<color=#a8c8a0>{text}</color>");

        void Line(string text)
        {
            lines.Add((text, Time.unscaledTime));
            if (lines.Count > Keep) lines.RemoveAt(0);
        }

        void Update()
        {
            if (!typing && !UiFocus.PanelOpen && GameInput.Pressed(KeyCode.Return)) Open();
        }

        void Open()
        {
            typing = true;
            draft = "";
            openedFrame = Time.frameCount;
            UiFocus.PanelOpen = true;
            UiFocus.Typing = true;
            if (Ruler != null) Ruler.Typing = true;
        }

        void Close()
        {
            typing = false;
            UiFocus.PanelOpen = false;
            UiFocus.Typing = false;
            if (Ruler != null) Ruler.Typing = false;
        }

        void OnDestroy()
        {
            if (typing) Close();
        }

        void OnGUI()
        {
            int font = Mathf.Max(12, Screen.height / 52);
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, alignment = TextAnchor.LowerLeft };
                field = new GUIStyle(GUI.skin.textField);
            }
            style.fontSize = field.fontSize = font;

            float width = Mathf.Min(Screen.width * 0.45f, font * 34f);
            float x = font, bottom = Screen.height * 0.72f;
            if (typing)
            {
                var e = Event.current;
                if (e.type == EventType.KeyDown && Time.frameCount != openedFrame)
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    {
                        string text = draft.Trim();
                        if (text.Length > 0) Send?.Invoke(text);
                        Close();
                        e.Use();
                    }
                    else if (e.keyCode == KeyCode.Escape)
                    {
                        Close();
                        e.Use();
                    }
                }
            }
            if (typing)
            {
                GUI.SetNextControlName("chat");
                draft = GUI.TextField(new Rect(x, bottom, width, font * 1.8f), draft, Networking.Protocol.MaxChatLength, field);
                GUI.FocusControl("chat");
            }

            // The messages, newest at the bottom.
            float y = bottom - font * 0.3f;
            int shown = 0;
            for (int i = lines.Count - 1; i >= 0 && shown < (typing ? Keep : Shown); i--)
            {
                if (!typing && Time.unscaledTime - lines[i].At > ShowSeconds) break;
                var content = new GUIContent(lines[i].Text);
                float h = style.CalcHeight(content, width);
                y -= h;
                if (y < font * 4f) break;
                var old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.45f);
                GUI.DrawTexture(new Rect(x - 4f, y, width + 8f, h), Texture2D.whiteTexture);
                GUI.color = old;
                GUI.Label(new Rect(x, y, width, h), content, style);
                shown++;
            }
            if (typing) GUI.Label(new Rect(x, bottom + font * 1.9f, width, font * 1.4f), "<color=#8f877a>Enter: send   Esc: cancel</color>", style);
        }
    }
}

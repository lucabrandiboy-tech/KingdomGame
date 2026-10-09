using UnityEngine;

namespace KingdomGame
{
    /// <summary>How the camera moves and zooms, and the look of shadows and glowing things.</summary>
    [CreateAssetMenu(menuName = "KingdomGame/View Config", fileName = "ViewConfig")]
    public sealed class ViewConfig : ScriptableObject
    {
        [Header("Camera zoom (half the screen height, in tiles)")]
        [Tooltip("Zoom while walking around: 12 shows 24 tiles from top to bottom. Bigger = see more.")]
        [Min(1)] public float walkZoom = 12f;
        [Min(1)] public float walkZoomMin = 6f;
        [Min(1)] public float walkZoomMax = 18f;
        [Tooltip("Zoom when you sit on the throne.")]
        [Min(1)] public float throneZoom = 24f;
        [Min(1)] public float throneZoomMin = 8f;
        [Min(1)] public float throneZoomMax = 44f;
        [Tooltip("How much one click of the mouse wheel zooms.")]
        [Range(1.01f, 2f)] public float zoomStep = 1.12f;
        [Tooltip("How quickly the zoom catches up.")]
        [Min(0.1f)] public float zoomSharpness = 10f;

        [Header("Camera movement")]
        [Tooltip("How quickly the camera catches up with the ruler.")]
        [Min(0.1f)] public float followSharpness = 9f;
        [Tooltip("Throne view: how fast WASD moves the view, in screen heights per second.")]
        [Min(0.05f)] public float panSpeed = 0.9f;
        [Tooltip("The most the camera can shake, in tiles.")]
        [Min(0)] public float maxShake = 0.35f;

        [Header("Look")]
        [Tooltip("A material that ignores lighting (Sprite-Unlit-Default), for fire, sparks and shots, so they glow at night.")]
        public Material glowMaterial;
        [Tooltip("How dark shadows are.")]
        [Range(0, 1)] public float shadowOpacity = 0.45f;
        [Tooltip("Which way shadows fall, per tile of height (away from the light, which comes from the top-left).")]
        public Vector2 shadowDirection = new Vector2(0.55f, -0.65f);
        [Tooltip("How see-through tree leaves get when the ruler walks under them.")]
        [Range(0, 1)] public float canopyFadeAlpha = 0.35f;
        [Tooltip("How see-through tree leaves are in the throne view, so you can see what's under them.")]
        [Range(0, 1)] public float throneCanopyAlpha = 0.6f;
        [Tooltip("Color outside the map.")]
        public Color backgroundColor = new Color(0.04f, 0.05f, 0.04f);
    }
}

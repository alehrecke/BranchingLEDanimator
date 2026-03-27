using UnityEngine;
using System.Collections.Generic;
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Simple wave animation that works directly in LED space.
    /// Demonstrates Option D LED-based animation system.
    /// 
    /// Parameters like waveWidth and waveSpeed are in LED units,
    /// meaning "waveWidth = 5" will always light up exactly 5 LEDs,
    /// regardless of how many nodes are in the original polyline.
    /// </summary>
    [CreateAssetMenu(fileName = "LEDWaveAnimation", menuName = "LED Animations/LED-Based/LED Wave")]
    public class LEDWaveAnimation : LEDAnimationType
    {
        [Header("LED Wave Settings")]
        [Tooltip("Width of the wave in LEDs (exact pixel count)")]
        [Range(1, 50)]
        public int waveWidthLEDs = 8;
        
        [Tooltip("Speed of wave propagation (LEDs per second)")]
        [Range(1f, 100f)]
        public float waveSpeedLEDs = 20f;
        
        [Tooltip("Wave travels from endpoints toward center")]
        public bool fromEndpoints = true;
        
        [Tooltip("Multiple waves at once")]
        [Range(1, 5)]
        public int waveCount = 1;
        
        [Tooltip("Spacing between multiple waves (in LEDs)")]
        public int waveSpacing = 30;
        
        [Header("Wave Shape")]
        public AnimationCurve waveShape = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        /// <summary>
        /// LED mode disabled - node mode fallback works more reliably
        /// TODO: Debug LED mode mapping issues before re-enabling
        /// </summary>
        public override bool SupportsLEDMode => false;
        
        /// <summary>
        /// Calculate colors directly for each LED
        /// </summary>
        public override Color[] CalculateLEDColors(MappedGraph mappedGraph, float time, int frame)
        {
            if (mappedGraph == null || !mappedGraph.IsValid)
                return null;
            
            Color[] colors = new Color[mappedGraph.TotalLEDCount];
            
            // Initialize with inactive color
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            if (mappedGraph.StripCount == 0)
                return colors;
            
            float animTime = time * speed;
            
            // Process each strip independently
            for (int stripIdx = 0; stripIdx < mappedGraph.StripCount; stripIdx++)
            {
                int stripStart = mappedGraph.StripStartIndices[stripIdx];
                int stripLength = mappedGraph.StripLEDCounts[stripIdx];
                
                // Calculate wave position(s) for this strip
                for (int w = 0; w < waveCount; w++)
                {
                    float waveOffset = w * waveSpacing;
                    float wavePos = (animTime * waveSpeedLEDs + waveOffset) % (stripLength * 2);
                    
                    // Bounce wave back at end of strip
                    if (wavePos > stripLength)
                    {
                        wavePos = stripLength * 2 - wavePos;
                    }
                    
                    // If from endpoints, start from both ends
                    if (fromEndpoints)
                    {
                        ApplyWaveToStrip(colors, stripStart, stripLength, wavePos, false);
                        ApplyWaveToStrip(colors, stripStart, stripLength, stripLength - wavePos, false);
                    }
                    else
                    {
                        ApplyWaveToStrip(colors, stripStart, stripLength, wavePos, false);
                    }
                }
            }
            
            return colors;
        }
        
        private void ApplyWaveToStrip(Color[] colors, int stripStart, int stripLength, float waveCenter, bool reverse)
        {
            for (int i = 0; i < stripLength; i++)
            {
                int ledIdx = stripStart + i;
                float distance = Mathf.Abs(i - waveCenter);
                
                if (distance < waveWidthLEDs)
                {
                    float normalizedDist = distance / waveWidthLEDs;
                    float intensity = waveShape.Evaluate(1f - normalizedDist);
                    
                    // Blend with existing color (additive for overlapping waves)
                    Color waveColor = Color.Lerp(inactiveColor, primaryColor, intensity);
                    colors[ledIdx] = MaxColor(colors[ledIdx], waveColor);
                }
            }
        }
        
        private Color MaxColor(Color a, Color b)
        {
            return new Color(
                Mathf.Max(a.r, b.r),
                Mathf.Max(a.g, b.g),
                Mathf.Max(a.b, b.b),
                Mathf.Max(a.a, b.a)
            );
        }
        
        /// <summary>
        /// Fallback node-based calculation (required by base class)
        /// </summary>
        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            // This is only called if LED mode isn't available
            // Provide a simple fallback
            Color[] colors = new Color[nodePositions.Count];
            float progress = GetAnimationProgress(time);
            
            for (int i = 0; i < colors.Length; i++)
            {
                float t = (float)i / nodePositions.Count;
                float wave = Mathf.Sin((t + progress) * Mathf.PI * 2f * waveCount);
                wave = (wave + 1f) / 2f; // Normalize to 0-1
                colors[i] = Color.Lerp(inactiveColor, primaryColor, wave);
            }
            
            return colors;
        }
    }
}

using UnityEngine;
using System.Collections.Generic;
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Base class for different LED animation types
    /// </summary>
    public abstract class LEDAnimationType : ScriptableObject
    {
        [Header("Animation Settings")]
        public string animationName = "Base Animation";
        public float speed = 1f;
        public Color primaryColor = Color.red;
        public Color secondaryColor = Color.blue;
        public Color inactiveColor = Color.black;
        
        [Header("Color Enhancement")]
        [Range(0f, 2f)]
        public float brightness = 1f;
        [Range(0f, 2f)]
        public float saturation = 1f;
        [Tooltip("Apply brightness/saturation to all generated colors before sending to ESP32")]
        public bool enhanceColors = true;
        
        [Header("Timing")]
        public float duration = 10f; // Duration in seconds for one full cycle
        public bool loop = true;
        
        /// <summary>
        /// Set by LEDAnimationSystem during initialization. Used by interactive animations
        /// to filter player events so they only react to their own graph.
        /// </summary>
        [System.NonSerialized]
        public LEDGraphManager OwnerGraphManager;
        
        /// <summary>
        /// Calculate colors for all nodes at a given time
        /// </summary>
        /// <param name="nodePositions">Positions of all LED nodes</param>
        /// <param name="edgeConnections">Connections between nodes</param>
        /// <param name="sourceNodes">Source node indices</param>
        /// <param name="time">Current animation time</param>
        /// <param name="frame">Current frame number</param>
        /// <returns>Array of colors for each node</returns>
        public Color[] CalculateNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame
        )
        {
            // Get base colors from derived class
            Color[] baseColors = CalculateBaseNodeColors(nodePositions, edgeConnections, sourceNodes, time, frame);
            
            // Apply color enhancement if enabled
            if (enhanceColors && (brightness != 1f || saturation != 1f))
            {
                for (int i = 0; i < baseColors.Length; i++)
                {
                    baseColors[i] = EnhanceColor(baseColors[i]);
                }
            }
            
            return baseColors;
        }
        
        /// <summary>
        /// Calculate base colors for all nodes (implemented by derived classes)
        /// </summary>
        protected abstract Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame
        );
        
        #region LED-Based Animation Support (Option D)
        
        /// <summary>
        /// Whether this animation supports direct LED-based rendering.
        /// Override to return true in animations that implement CalculateLEDColors.
        /// </summary>
        public virtual bool SupportsLEDMode => false;
        
        /// <summary>
        /// Calculate colors directly for each LED in the mapped graph.
        /// This bypasses node-based calculation for pixel-perfect hardware control.
        /// 
        /// Override this in animations that want to work in LED-space.
        /// Default implementation falls back to node-based with interpolation hint.
        /// </summary>
        /// <param name="mappedGraph">The LED graph with positions and topology</param>
        /// <param name="time">Current animation time</param>
        /// <param name="frame">Current frame number</param>
        /// <returns>Array of colors, one per LED (indexed by global LED index)</returns>
        public virtual Color[] CalculateLEDColors(
            MappedGraph mappedGraph,
            float time,
            int frame
        )
        {
            // Default implementation: return null to signal "use node-based"
            // Animations that support LED mode should override this
            return null;
        }
        
        /// <summary>
        /// Calculate LED colors with enhancement applied.
        /// Called by the animation system for LED-mode animations.
        /// </summary>
        public Color[] CalculateEnhancedLEDColors(
            MappedGraph mappedGraph,
            float time,
            int frame
        )
        {
            Color[] baseColors = CalculateLEDColors(mappedGraph, time, frame);
            
            if (baseColors == null) return null;
            
            // Apply color enhancement if enabled
            if (enhanceColors && (brightness != 1f || saturation != 1f))
            {
                for (int i = 0; i < baseColors.Length; i++)
                {
                    baseColors[i] = EnhanceColor(baseColors[i]);
                }
            }
            
            return baseColors;
        }
        
        #endregion
        
        /// <summary>
        /// Get animation progress (0-1) based on time
        /// </summary>
        protected float GetAnimationProgress(float time)
        {
            if (duration <= 0) return 0f;
            
            float progress = (time * speed) / duration;
            
            if (loop)
            {
                return progress % 1f;
            }
            else
            {
                return Mathf.Clamp01(progress);
            }
        }
        
        /// <summary>
        /// Calculate distance from any source node
        /// </summary>
        protected float GetDistanceFromNearestSource(Vector3 position, List<Vector3> nodePositions, List<int> sourceNodes)
        {
            float minDistance = float.MaxValue;
            
            foreach (int sourceIndex in sourceNodes)
            {
                if (sourceIndex < nodePositions.Count)
                {
                    float distance = Vector3.Distance(position, nodePositions[sourceIndex]);
                    minDistance = Mathf.Min(minDistance, distance);
                }
            }
            
            return minDistance == float.MaxValue ? 0f : minDistance;
        }
        
        /// <summary>
        /// Blend two colors based on intensity
        /// </summary>
        protected Color BlendColors(Color colorA, Color colorB, float blend)
        {
            return Color.Lerp(colorA, colorB, Mathf.Clamp01(blend));
        }
        
        /// <summary>
        /// Create a wave effect based on distance and time
        /// </summary>
        protected float CalculateWaveIntensity(float distance, float time, float waveSpeed = 50f, float waveWidth = 20f)
        {
            float wavePosition = time * waveSpeed;
            float distanceFromWave = Mathf.Abs(distance - wavePosition);
            
            if (distanceFromWave <= waveWidth)
            {
                return 1f - (distanceFromWave / waveWidth);
            }
            
            return 0f;
        }
        
        /// <summary>
        /// Enhance color with brightness and saturation adjustments
        /// </summary>
        protected Color EnhanceColor(Color originalColor)
        {
            // Skip enhancement for completely black colors (inactive)
            if (originalColor.r == 0f && originalColor.g == 0f && originalColor.b == 0f)
                return originalColor;
            
            // Convert to HSV for better saturation control
            Color.RGBToHSV(originalColor, out float h, out float s, out float v);
            
            // Apply saturation and brightness
            s = Mathf.Clamp01(s * saturation);
            v = Mathf.Clamp01(v * brightness);
            
            // Convert back to RGB
            Color enhancedColor = Color.HSVToRGB(h, s, v);
            enhancedColor.a = originalColor.a; // Preserve alpha
            
            return enhancedColor;
        }
    }
}

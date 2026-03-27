using UnityEngine;
using System.Collections.Generic;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Pulsing animation that breathes from source points
    /// </summary>
    [CreateAssetMenu(fileName = "PulseAnimation", menuName = "LED Animations/Pulse Animation")]
    public class PulseAnimation : LEDAnimationType
    {
        [Header("Pulse Settings")]
        public float pulseRadius = 100f;
        public AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public bool synchronizedPulse = true;
        public float phaseOffset = 0f;
        
                protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            Color[] colors = new Color[nodePositions.Count];
            float animTime = time * speed;
            
            // Initialize all as inactive
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            // Calculate pulse effects from each source
            for (int sourceIdx = 0; sourceIdx < sourceNodes.Count; sourceIdx++)
            {
                int sourceIndex = sourceNodes[sourceIdx];
                if (sourceIndex >= nodePositions.Count) continue;
                
                Vector3 sourcePos = nodePositions[sourceIndex];
                
                // Calculate phase for this source
                float sourcePhase = synchronizedPulse ? 0f : (sourceIdx * phaseOffset);
                float pulseTime = (animTime + sourcePhase) % duration;
                float pulseProgress = pulseTime / duration;
                
                // Get pulse intensity from curve
                float pulseIntensity = pulseCurve.Evaluate(pulseProgress);
                
                // Apply pulse to all nodes within radius
                for (int i = 0; i < nodePositions.Count; i++)
                {
                    float distance = Vector3.Distance(sourcePos, nodePositions[i]);
                    
                    if (distance <= pulseRadius)
                    {
                        // Calculate falloff based on distance
                        float distanceFalloff = 1f - (distance / pulseRadius);
                        float finalIntensity = pulseIntensity * distanceFalloff;
                        
                        Color pulseColor = BlendColors(inactiveColor, primaryColor, finalIntensity);
                        
                        // Use additive blending for overlapping pulses
                        colors[i] = BlendColors(colors[i], pulseColor, finalIntensity);
                    }
                }
            }
            
            return colors;
        }
    }
}

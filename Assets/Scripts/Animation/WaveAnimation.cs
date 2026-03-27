using UnityEngine;
using System.Collections.Generic;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Wave propagation animation from source points
    /// </summary>
    [CreateAssetMenu(fileName = "WaveAnimation", menuName = "LED Animations/Wave Animation")]
    public class WaveAnimation : LEDAnimationType
    {
        [Header("Wave Settings")]
        public float waveSpeed = 50f;
        public float waveWidth = 20f;
        public bool multipleWaves = true;
        public float waveSeparation = 100f;
        
                protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            Color[] colors = new Color[nodePositions.Count];
            
            // Handle looping - restart time cycle when duration is reached
            float animTime = time * speed;
            if (loop && duration > 0)
            {
                animTime = animTime % duration;
            }
            
            // Initialize all as inactive
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            // Calculate wave effects from each source
            foreach (int sourceIndex in sourceNodes)
            {
                if (sourceIndex >= nodePositions.Count) continue;
                
                Vector3 sourcePos = nodePositions[sourceIndex];
                
                // Always show source nodes
                colors[sourceIndex] = primaryColor;
                
                // Calculate waves
                if (multipleWaves)
                {
                    // Multiple waves with separation - loop continuously
                    float maxWaveOffset = duration * waveSpeed;
                    for (float waveOffset = 0; waveOffset < maxWaveOffset; waveOffset += waveSeparation)
                    {
                        float wavePosition = (animTime * waveSpeed) - waveOffset;
                        
                        // Allow waves to loop by using modulo
                        if (loop && wavePosition < 0)
                        {
                            wavePosition += maxWaveOffset;
                        }
                        
                        if (wavePosition > 0 && wavePosition < maxWaveOffset)
                        {
                            ApplyWaveToNodes(colors, nodePositions, sourcePos, wavePosition);
                        }
                    }
                }
                else
                {
                    // Single continuous wave - loop when it reaches the edge
                    float wavePosition = (animTime * waveSpeed) % (duration * waveSpeed);
                    ApplyWaveToNodes(colors, nodePositions, sourcePos, wavePosition);
                }
            }
            
            return colors;
        }
        
        private void ApplyWaveToNodes(Color[] colors, List<Vector3> nodePositions, Vector3 sourcePos, float wavePosition)
        {
            for (int i = 0; i < nodePositions.Count; i++)
            {
                float distance = Vector3.Distance(sourcePos, nodePositions[i]);
                float intensity = CalculateWaveIntensity(distance, wavePosition / waveSpeed, waveSpeed, waveWidth);
                
                if (intensity > 0)
                {
                    Color waveColor = BlendColors(inactiveColor, primaryColor, intensity);
                    // Use additive blending for overlapping waves
                    colors[i] = Color.Lerp(colors[i], waveColor, intensity);
                }
            }
        }
    }
}

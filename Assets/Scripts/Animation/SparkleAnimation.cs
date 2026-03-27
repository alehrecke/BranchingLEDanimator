using UnityEngine;
using System.Collections.Generic;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Random sparkle animation across the LED network
    /// </summary>
    [CreateAssetMenu(fileName = "SparkleAnimation", menuName = "LED Animations/Sparkle Animation")]
    public class SparkleAnimation : LEDAnimationType
    {
        [Header("Sparkle Settings")]
        public int maxActiveSparkles = 20;
        public float sparkleLifetime = 1f;
        public float sparkleIntensity = 1f;
        public bool colorVariation = true;
        public int randomSeed = 42;
        
        private Dictionary<int, SparkleData> activeSparkles = new Dictionary<int, SparkleData>();
        private System.Random random;
        private float lastUpdateTime = -1f;
        
        private struct SparkleData
        {
            public float startTime;
            public Color color;
            public float intensity;
        }
        
                protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            // Initialize random if needed
            if (random == null || lastUpdateTime < 0f)
            {
                random = new System.Random(randomSeed);
                lastUpdateTime = time;
            }
            
            Color[] colors = new Color[nodePositions.Count];
            float animTime = time * speed;
            
            // Initialize all as inactive
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            // Update sparkles
            UpdateSparkles(animTime, nodePositions.Count);
            
            // Apply sparkle colors
            foreach (var kvp in activeSparkles)
            {
                int nodeIndex = kvp.Key;
                SparkleData sparkle = kvp.Value;
                
                if (nodeIndex < colors.Length)
                {
                    float age = animTime - sparkle.startTime;
                    float lifeProgress = age / sparkleLifetime;
                    
                    if (lifeProgress <= 1f)
                    {
                        // Use a curve for sparkle intensity over lifetime
                        float intensityCurve = Mathf.Sin(lifeProgress * Mathf.PI); // Bell curve
                        float finalIntensity = sparkle.intensity * intensityCurve * sparkleIntensity;
                        
                        colors[nodeIndex] = BlendColors(inactiveColor, sparkle.color, finalIntensity);
                    }
                }
            }
            
            lastUpdateTime = animTime;
            return colors;
        }
        
        private void UpdateSparkles(float currentTime, int nodeCount)
        {
            // Remove expired sparkles
            var expiredSparkles = new List<int>();
            foreach (var kvp in activeSparkles)
            {
                float age = currentTime - kvp.Value.startTime;
                if (age > sparkleLifetime)
                {
                    expiredSparkles.Add(kvp.Key);
                }
            }
            
            foreach (int expired in expiredSparkles)
            {
                activeSparkles.Remove(expired);
            }
            
            // Add new sparkles randomly
            while (activeSparkles.Count < maxActiveSparkles && nodeCount > 0)
            {
                int randomNode = random.Next(nodeCount);
                
                // Don't add sparkle if one already exists at this node
                if (!activeSparkles.ContainsKey(randomNode))
                {
                    Color sparkleColor = colorVariation ? GetRandomColor() : primaryColor;
                    
                    activeSparkles[randomNode] = new SparkleData
                    {
                        startTime = currentTime,
                        color = sparkleColor,
                        intensity = 1f
                    };
                }
            }
        }
        
        private Color GetRandomColor()
        {
            if (colorVariation)
            {
                float hue = (float)random.NextDouble();
                return Color.HSVToRGB(hue, 1f, 1f);
            }
            else
            {
                return primaryColor;
            }
        }
    }
}

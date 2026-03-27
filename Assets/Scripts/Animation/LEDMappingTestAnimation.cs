using UnityEngine;
using System.Collections.Generic;
using BranchingLEDAnimator.Animation;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Simple test animation for debugging LED mapping issues
    /// Lights up specific patterns to verify physical-to-logical mapping
    /// </summary>
    [CreateAssetMenu(fileName = "LEDMappingTest", menuName = "LED Animation/Debug/LED Mapping Test")]
    public class LEDMappingTestAnimation : LEDAnimationType
    {
        [Header("Test Patterns")]
        public TestPattern testPattern = TestPattern.Endpoints;
        public Color testColor = Color.red;
        public float pulseSpeed = 2f;
        public bool enableSequentialTest = false;
        public float sequentialDelay = 0.5f;
        
        public enum TestPattern
        {
            Endpoints,           // Light only endpoint nodes
            FirstNode,          // Light only first node of each branch
            Sequential,         // Light nodes one by one
            AlternatingStrips,  // Light every other logical strip
            SingleStrip,        // Light only one strip at a time
            AllNodes           // Light all nodes (for brightness test)
        }
        
        private int currentSequentialIndex = 0;
        private float lastSequentialTime = 0f;
        
        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float animationTime,
            int currentFrame)
        {
            Color[] colors = new Color[nodePositions.Count];
            
            // Initialize all to black
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            switch (testPattern)
            {
                case TestPattern.Endpoints:
                    TestEndpoints(colors, sourceNodes);
                    break;
                    
                case TestPattern.FirstNode:
                    TestFirstNodes(colors, nodePositions.Count);
                    break;
                    
                case TestPattern.Sequential:
                    TestSequential(colors, nodePositions.Count, animationTime);
                    break;
                    
                case TestPattern.AlternatingStrips:
                    TestAlternatingStrips(colors, nodePositions.Count);
                    break;
                    
                case TestPattern.SingleStrip:
                    TestSingleStrip(colors, nodePositions.Count, animationTime);
                    break;
                    
                case TestPattern.AllNodes:
                    TestAllNodes(colors);
                    break;
            }
            
            return colors;
        }
        
        private void TestEndpoints(Color[] colors, List<int> sourceNodes)
        {
            // Light up all endpoint nodes
            foreach (int nodeIndex in sourceNodes)
            {
                if (nodeIndex >= 0 && nodeIndex < colors.Length)
                {
                    colors[nodeIndex] = GetPulsingColor();
                }
            }
        }
        
        private void TestFirstNodes(Color[] colors, int totalNodes)
        {
            // Light up first few nodes (assuming they're at the start of each branch)
            int nodesToLight = Mathf.Min(10, totalNodes);
            for (int i = 0; i < nodesToLight; i++)
            {
                colors[i] = GetPulsingColor();
            }
        }
        
        private void TestSequential(Color[] colors, int totalNodes, float animationTime)
        {
            if (!enableSequentialTest) return;
            
            // Light nodes one by one with delay
            if (animationTime - lastSequentialTime > sequentialDelay)
            {
                currentSequentialIndex = (currentSequentialIndex + 1) % totalNodes;
                lastSequentialTime = animationTime;
            }
            
            // Light current node
            colors[currentSequentialIndex] = testColor;
            
            // Also light previous few nodes with dimming
            for (int i = 1; i <= 3; i++)
            {
                int prevIndex = (currentSequentialIndex - i + totalNodes) % totalNodes;
                float dimFactor = 1f - (i * 0.3f);
                colors[prevIndex] = testColor * dimFactor;
            }
        }
        
        private void TestAlternatingStrips(Color[] colors, int totalNodes)
        {
            // Assuming each logical strip has roughly equal nodes
            // This is a rough approximation - you may need to adjust based on your setup
            int estimatedNodesPerStrip = totalNodes / 9; // You mentioned 9 strips
            
            for (int stripIndex = 0; stripIndex < 9; stripIndex += 2) // Every other strip
            {
                int startNode = stripIndex * estimatedNodesPerStrip;
                int endNode = Mathf.Min((stripIndex + 1) * estimatedNodesPerStrip, totalNodes);
                
                for (int nodeIndex = startNode; nodeIndex < endNode; nodeIndex++)
                {
                    colors[nodeIndex] = GetPulsingColor();
                }
            }
        }
        
        private void TestSingleStrip(Color[] colors, int totalNodes, float animationTime)
        {
            // Cycle through strips one at a time
            int stripCount = 9; // Your 9 logical strips
            int currentStrip = Mathf.FloorToInt(animationTime / 2f) % stripCount;
            
            int estimatedNodesPerStrip = totalNodes / stripCount;
            int startNode = currentStrip * estimatedNodesPerStrip;
            int endNode = Mathf.Min((currentStrip + 1) * estimatedNodesPerStrip, totalNodes);
            
            for (int nodeIndex = startNode; nodeIndex < endNode; nodeIndex++)
            {
                colors[nodeIndex] = testColor;
            }
        }
        
        private void TestAllNodes(Color[] colors)
        {
            // Light all nodes for brightness/connection test
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = GetPulsingColor();
            }
        }
        
        private Color GetPulsingColor()
        {
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            return Color.Lerp(inactiveColor, testColor, pulse);
        }
    }
}

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace BranchingLEDAnimator.Animation.Editor
{
    /// <summary>
    /// Creates graph topology animation asset files
    /// </summary>
    public static class GraphTopologyAnimationCreator
    {
        [MenuItem("Tools/LED Animations/Create Graph Theory Animations")]
        public static void CreateGraphTheoryAnimations()
        {
            CreateGraphTopologyAnimations();
            CreateNetworkFlowAnimations();
            CreateShortestPathAnimations();
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("✅ Created graph theory animation assets!");
        }
        
        [MenuItem("Assets/Create/LED Animations/Graph Theory/Topology Pulses")]
        public static void CreateGraphTopologyAnimations()
        {
            // High Valence Pulses
            var highValencePulses = ScriptableObject.CreateInstance<GraphTopologyAnimation>();
            highValencePulses.animationName = "Junction Pulses";
            highValencePulses.speed = 1f;
            highValencePulses.duration = 4f;
            highValencePulses.loop = true;
            highValencePulses.primaryColor = Color.red;
            highValencePulses.secondaryColor = Color.white;
            highValencePulses.inactiveColor = Color.black;
            
            AssetDatabase.CreateAsset(highValencePulses, "Assets/Scripts/Animation/JunctionPulses.asset");
            
            // Simultaneous Pulses
            var simultaneousPulses = ScriptableObject.CreateInstance<GraphTopologyAnimation>();
            simultaneousPulses.animationName = "Topology Sync Pulses";
            simultaneousPulses.speed = 1.5f;
            simultaneousPulses.duration = 3f;
            simultaneousPulses.loop = true;
            simultaneousPulses.primaryColor = Color.cyan;
            simultaneousPulses.secondaryColor = Color.magenta;
            simultaneousPulses.inactiveColor = Color.black;
            
            AssetDatabase.CreateAsset(simultaneousPulses, "Assets/Scripts/Animation/TopologySyncPulses.asset");
            
            Debug.Log("✅ Created Graph Topology animation assets");
        }
        
        [MenuItem("Assets/Create/LED Animations/Graph Theory/Junction Breathing")]
        public static void CreateJunctionBreathingAnimation()
        {
            // Junction Breathing Effect
            var junctionBreathing = ScriptableObject.CreateInstance<GraphTopologyAnimation>();
            junctionBreathing.animationName = "Junction Breathing";
            junctionBreathing.speed = 1f;
            junctionBreathing.duration = 6f;
            junctionBreathing.loop = true;
            junctionBreathing.primaryColor = new Color(1f, 0.3f, 0.1f); // Warm orange-red
            junctionBreathing.secondaryColor = new Color(0.1f, 0.5f, 1f); // Cool blue
            junctionBreathing.inactiveColor = new Color(0.05f, 0.05f, 0.1f); // Deep blue-black
            
            // Note: Unity's inspector will show the breathing-specific settings
            // breathingMode = true, breathingCycles = 2f (set via inspector)
            
            AssetDatabase.CreateAsset(junctionBreathing, "Assets/Scripts/Animation/JunctionBreathing.asset");
            
            Debug.Log("✅ Created Junction Breathing animation asset - Enable 'Breathing Mode' in inspector!");
        }
        
        [MenuItem("Assets/Create/LED Animations/Graph Theory/Network Flow")]
        public static void CreateNetworkFlowAnimations()
        {
            // Source to Sinks Flow
            var sourceToSinks = ScriptableObject.CreateInstance<NetworkFlowAnimation>();
            sourceToSinks.animationName = "Hub to Endpoints Flow";
            sourceToSinks.speed = 1.5f;
            sourceToSinks.duration = 12f;
            sourceToSinks.loop = true;
            sourceToSinks.primaryColor = Color.cyan;
            sourceToSinks.secondaryColor = Color.blue;
            sourceToSinks.inactiveColor = new Color(0.02f, 0.02f, 0.05f);
            
            AssetDatabase.CreateAsset(sourceToSinks, "Assets/Scripts/Animation/HubToEndpointsFlow.asset");
            
            // Circulation Flow - Fixed parameters
            var circulation = ScriptableObject.CreateInstance<NetworkFlowAnimation>();
            circulation.animationName = "Network Circulation";
            circulation.speed = 1f;
            circulation.duration = 15f; // Longer duration for better loops
            circulation.loop = true;
            circulation.primaryColor = Color.green;
            circulation.secondaryColor = Color.yellow;
            circulation.inactiveColor = new Color(0.02f, 0.05f, 0.02f);
            
            AssetDatabase.CreateAsset(circulation, "Assets/Scripts/Animation/NetworkCirculation.asset");
            
            // Random Walk - Fixed parameters
            var randomWalk = ScriptableObject.CreateInstance<NetworkFlowAnimation>();
            randomWalk.animationName = "Random Network Walk";
            randomWalk.speed = 1.5f;
            randomWalk.duration = 10f; // Longer duration for better loops
            randomWalk.loop = true;
            randomWalk.primaryColor = Color.magenta;
            randomWalk.secondaryColor = Color.white;
            randomWalk.inactiveColor = new Color(0.05f, 0.02f, 0.05f);
            
            AssetDatabase.CreateAsset(randomWalk, "Assets/Scripts/Animation/RandomNetworkWalk.asset");
            
            Debug.Log("✅ Created Network Flow animation assets - Loop issues fixed!");
        }
        
        [MenuItem("Assets/Create/LED Animations/Graph Theory/Shortest Paths")]
        public static void CreateShortestPathAnimations()
        {
            // All Pairs Shortest Paths - Fixed parameters
            var allPairs = ScriptableObject.CreateInstance<ShortestPathAnimation>();
            allPairs.animationName = "All Pairs Shortest Paths";
            allPairs.speed = 1.2f;
            allPairs.duration = 12f; // Longer duration for better loops
            allPairs.loop = true;
            allPairs.primaryColor = Color.green;
            allPairs.secondaryColor = Color.blue;
            allPairs.inactiveColor = new Color(0.02f, 0.05f, 0.02f);
            
            AssetDatabase.CreateAsset(allPairs, "Assets/Scripts/Animation/AllPairsShortestPaths.asset");
            
            // Radial from Center - Fixed parameters
            var radialPaths = ScriptableObject.CreateInstance<ShortestPathAnimation>();
            radialPaths.animationName = "Radial Shortest Paths";
            radialPaths.speed = 1f;
            radialPaths.duration = 10f; // Longer duration for better loops
            radialPaths.loop = true;
            radialPaths.primaryColor = Color.yellow;
            radialPaths.secondaryColor = Color.red;
            radialPaths.inactiveColor = new Color(0.05f, 0.05f, 0.02f);
            
            AssetDatabase.CreateAsset(radialPaths, "Assets/Scripts/Animation/RadialShortestPaths.asset");
            
            // Endpoint Connections - Fixed parameters
            var endpointConnections = ScriptableObject.CreateInstance<ShortestPathAnimation>();
            endpointConnections.animationName = "Endpoint Connections";
            endpointConnections.speed = 1f;
            endpointConnections.duration = 15f; // Much longer for connecting all endpoints
            endpointConnections.loop = true;
            endpointConnections.primaryColor = Color.blue;
            endpointConnections.secondaryColor = Color.cyan;
            endpointConnections.inactiveColor = new Color(0.02f, 0.02f, 0.08f);
            
            AssetDatabase.CreateAsset(endpointConnections, "Assets/Scripts/Animation/EndpointConnections.asset");
            
            Debug.Log("✅ Created Shortest Path animation assets - Loop & exploration issues fixed!");
        }
    }
}
#endif

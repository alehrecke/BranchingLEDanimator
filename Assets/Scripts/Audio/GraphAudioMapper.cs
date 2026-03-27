using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Animation;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BranchingLEDAnimator.Audio
{
    /// <summary>
    /// Maps graph topology to audio characteristics and manages tonal strategies
    /// Core component for the graph-based audio workflow
    /// </summary>
    public class GraphAudioMapper : MonoBehaviour
    {
        [Header("Graph Analysis")]
        [SerializeField] private bool autoAnalyzeOnGraphLoad = true;
        [SerializeField] private bool showDebugInfo = true;
        
        [Header("Tonal Strategy")]
        [SerializeField] private TonalStrategy tonalStrategy = TonalStrategy.Harmonic;
        [SerializeField] private MusicalScale musicalScale = MusicalScale.Pentatonic;
        [SerializeField] private float rootFrequency = 220f; // A3
        [SerializeField] private int octaveRange = 3;
        
        [Header("Node Audio Mapping")]
        [SerializeField] private NodeAudioSettings sourceNodeSettings = new NodeAudioSettings 
        { 
            chimeType = ChimeType.Bell, 
            volume = 0.8f, 
            decay = 2.0f, 
            harmonics = 4 
        };
        [SerializeField] private NodeAudioSettings highValenceSettings = new NodeAudioSettings 
        { 
            chimeType = ChimeType.Gong, 
            volume = 0.9f, 
            decay = 3.0f, 
            harmonics = 5 
        };
        [SerializeField] private NodeAudioSettings regularNodeSettings = new NodeAudioSettings 
        { 
            chimeType = ChimeType.Chime, 
            volume = 0.5f, 
            decay = 1.0f, 
            harmonics = 2 
        };
        
        [Header("Background Audio")]
        [SerializeField] private BackgroundAudioSettings backgroundSettings = new BackgroundAudioSettings();
        
        // Graph analysis results
        [System.Serializable]
        public class GraphAudioAnalysis
        {
            public Dictionary<int, NodeAudioData> nodeAudioMap = new Dictionary<int, NodeAudioData>();
            public List<int> sourceNodes = new List<int>();
            public List<int> highValenceNodes = new List<int>(); // Valence > 2
            public List<int> endpointNodes = new List<int>(); // Valence = 1
            public List<int> regularNodes = new List<int>(); // Valence = 2
            public float[] scaleFrequencies;
            public bool isValid = false;
        }
        
        [System.Serializable]
        public class NodeAudioData
        {
            public int nodeIndex;
            public int valence;
            public Vector3 position;
            public float frequency;
            public NodeAudioSettings audioSettings;
            public AudioClip preGeneratedChime; // Pre-generated for smooth playback
            public bool isSourceNode;
            public bool isHighValence;
            public bool isEndpoint;
        }
        
        [System.Serializable]
        public class NodeAudioSettings
        {
            public ChimeType chimeType = ChimeType.Bell;
            [Range(0f, 1f)] public float volume = 0.7f;
            [Range(0.1f, 5f)] public float decay = 1.5f;
            [Range(1, 8)] public int harmonics = 3;
            [Range(0f, 1f)] public float stereoSpread = 0.3f; // For spatial audio
        }
        
        [System.Serializable]
        public class BackgroundAudioSettings
        {
            public bool enableBackground = true;
            public BackgroundType backgroundType = BackgroundType.Ambient;
            [Range(0f, 1f)] public float volume = 0.2f;
            [Range(0.01f, 1f)] public float modulationSpeed = 0.1f;
            public AudioClip customBackgroundClip;
        }
        
        public enum TonalStrategy
        {
            Harmonic,      // Based on musical harmony theory
            Spatial,       // Based on node positions in 3D space
            Topological,   // Based on graph connectivity patterns
            Chromatic,     // Uses full chromatic scale
            Custom         // User-defined frequency mapping
        }
        
        public enum MusicalScale
        {
            Pentatonic,    // C D E G A (very harmonious)
            Major,         // C D E F G A B (bright)
            Minor,         // C D Eb F G Ab Bb (emotional)
            Dorian,        // C D Eb F G A Bb (modal, ambient)
            Lydian,        // C D E F# G A B (dreamy)
            Mixolydian,    // C D E F G A Bb (bluesy)
            Chromatic      // All 12 semitones
        }
        
        public enum ChimeType
        {
            Bell,          // Clean sine with harmonics
            Gong,          // Rich, complex harmonics with slow attack
            Chime,         // Bright, quick attack
            Marimba,       // Wooden, percussive
            Crystal,       // Pure, ethereal
            Custom         // User-defined synthesis
        }
        
        public enum BackgroundType
        {
            Ambient,       // Slow-moving atmospheric drones
            Rhythmic,      // Subtle rhythmic patterns
            Harmonic,      // Chord progressions based on active nodes
            Silent,        // No background
            Custom         // User-provided audio clip
        }
        
        // Current analysis
        private GraphAudioAnalysis currentAnalysis = new GraphAudioAnalysis();
        
        // References
        private LEDGraphManager graphManager;
        private LEDAnimationSystem animationSystem;
        
        void Start()
        {
            graphManager = GetComponent<LEDGraphManager>();
            animationSystem = GetComponent<LEDAnimationSystem>();
            
            if (autoAnalyzeOnGraphLoad && graphManager != null && graphManager.DataLoaded)
            {
                AnalyzeGraphAudio();
            }
        }
        
        void Update()
        {
            // Check if graph data has been loaded and we need to analyze
            if (autoAnalyzeOnGraphLoad && graphManager != null && graphManager.DataLoaded && !currentAnalysis.isValid)
            {
                AnalyzeGraphAudio();
            }
        }
        
        /// <summary>
        /// Analyze the current graph and create audio mapping
        /// </summary>
        [ContextMenu("Analyze Graph Audio")]
        public void AnalyzeGraphAudio()
        {
            if (graphManager == null || !graphManager.DataLoaded)
            {
                Debug.LogWarning("❌ GraphAudioMapper: No graph data loaded");
                return;
            }
            
            Debug.Log("🎵 Analyzing graph topology for audio mapping...");
            
            var nodePositions = graphManager.NodePositions;
            var edgeConnections = graphManager.EdgeConnections;
            var sourceNodes = graphManager.SourceNodes;
            
            // Reset analysis
            currentAnalysis = new GraphAudioAnalysis();
            currentAnalysis.sourceNodes = sourceNodes.ToList();
            
            // Calculate node valences
            Dictionary<int, int> nodeValences = CalculateNodeValences(nodePositions, edgeConnections);
            
            // Generate scale frequencies
            currentAnalysis.scaleFrequencies = GenerateScaleFrequencies();
            
            // Categorize nodes and create audio data
            for (int i = 0; i < nodePositions.Count; i++)
            {
                int valence = nodeValences.ContainsKey(i) ? nodeValences[i] : 0;
                Vector3 position = nodePositions[i];
                bool isSource = sourceNodes.Contains(i);
                bool isHighValence = valence > 2;
                bool isEndpoint = valence == 1;
                
                // Categorize node
                if (isSource)
                    currentAnalysis.sourceNodes.Add(i);
                else if (isHighValence)
                    currentAnalysis.highValenceNodes.Add(i);
                else if (isEndpoint)
                    currentAnalysis.endpointNodes.Add(i);
                else
                    currentAnalysis.regularNodes.Add(i);
                
                // Create audio data for this node
                var audioData = new NodeAudioData
                {
                    nodeIndex = i,
                    valence = valence,
                    position = position,
                    frequency = CalculateNodeFrequency(i, valence, position),
                    audioSettings = GetAudioSettingsForNode(isSource, isHighValence, isEndpoint),
                    isSourceNode = isSource,
                    isHighValence = isHighValence,
                    isEndpoint = isEndpoint
                };
                
                currentAnalysis.nodeAudioMap[i] = audioData;
            }
            
            currentAnalysis.isValid = true;
            
            // Log results
            Debug.Log($"🎼 Graph audio analysis complete:");
            Debug.Log($"   Source nodes: {currentAnalysis.sourceNodes.Count}");
            Debug.Log($"   High-valence nodes: {currentAnalysis.highValenceNodes.Count}");
            Debug.Log($"   Endpoint nodes: {currentAnalysis.endpointNodes.Count}");
            Debug.Log($"   Regular nodes: {currentAnalysis.regularNodes.Count}");
            Debug.Log($"   Musical scale: {musicalScale} ({currentAnalysis.scaleFrequencies.Length} frequencies)");
        }
        
        /// <summary>
        /// Calculate valences for all nodes
        /// </summary>
        private Dictionary<int, int> CalculateNodeValences(List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            Dictionary<int, int> valences = new Dictionary<int, int>();
            
            // Initialize all nodes with valence 0
            for (int i = 0; i < nodePositions.Count; i++)
            {
                valences[i] = 0;
            }
            
            // Count connections
            foreach (var edge in edgeConnections)
            {
                if (valences.ContainsKey(edge.x)) valences[edge.x]++;
                if (valences.ContainsKey(edge.y)) valences[edge.y]++;
            }
            
            return valences;
        }
        
        /// <summary>
        /// Calculate frequency for a specific node based on tonal strategy
        /// </summary>
        private float CalculateNodeFrequency(int nodeIndex, int valence, Vector3 position)
        {
            int scaleIndex = 0;
            
            switch (tonalStrategy)
            {
                case TonalStrategy.Harmonic:
                    // Use valence and node index for harmonic progression
                    scaleIndex = (valence * 2 + nodeIndex) % currentAnalysis.scaleFrequencies.Length;
                    break;
                    
                case TonalStrategy.Spatial:
                    // Use Z position primarily (vertical in Rhino/Grasshopper), with X as secondary
                    int zIndex = Mathf.RoundToInt((position.z + 50f) / 10f); // Adjust range as needed
                    int xIndex = Mathf.RoundToInt((position.x + 50f) / 20f);
                    scaleIndex = (zIndex * 3 + xIndex) % currentAnalysis.scaleFrequencies.Length;
                    break;
                    
                case TonalStrategy.Topological:
                    // Use valence as primary factor
                    scaleIndex = (valence + nodeIndex / 3) % currentAnalysis.scaleFrequencies.Length;
                    break;
                    
                case TonalStrategy.Chromatic:
                    // Use all 12 semitones
                    scaleIndex = nodeIndex % 12;
                    break;
                    
                case TonalStrategy.Custom:
                    // For now, default to harmonic
                    scaleIndex = (valence * 2 + nodeIndex) % currentAnalysis.scaleFrequencies.Length;
                    break;
            }
            
            return currentAnalysis.scaleFrequencies[scaleIndex];
        }
        
        /// <summary>
        /// Generate frequencies for the selected musical scale
        /// </summary>
        private float[] GenerateScaleFrequencies()
        {
            List<float> frequencies = new List<float>();
            
            // Define scale intervals (semitones from root)
            int[] intervals = musicalScale switch
            {
                MusicalScale.Pentatonic => new int[] { 0, 2, 4, 7, 9 },
                MusicalScale.Major => new int[] { 0, 2, 4, 5, 7, 9, 11 },
                MusicalScale.Minor => new int[] { 0, 2, 3, 5, 7, 8, 10 },
                MusicalScale.Dorian => new int[] { 0, 2, 3, 5, 7, 9, 10 },
                MusicalScale.Lydian => new int[] { 0, 2, 4, 6, 7, 9, 11 },
                MusicalScale.Mixolydian => new int[] { 0, 2, 4, 5, 7, 9, 10 },
                MusicalScale.Chromatic => new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
                _ => new int[] { 0, 2, 4, 7, 9 } // Default to pentatonic
            };
            
            // Generate frequencies across octave range
            for (int octave = 0; octave < octaveRange; octave++)
            {
                foreach (int interval in intervals)
                {
                    float frequency = rootFrequency * Mathf.Pow(2f, octave + interval / 12f);
                    frequencies.Add(frequency);
                }
            }
            
            return frequencies.ToArray();
        }
        
        /// <summary>
        /// Get appropriate audio settings for a node type
        /// </summary>
        private NodeAudioSettings GetAudioSettingsForNode(bool isSource, bool isHighValence, bool isEndpoint)
        {
            if (isSource)
                return sourceNodeSettings;
            else if (isHighValence)
                return highValenceSettings;
            else
                return regularNodeSettings;
        }
        
        /// <summary>
        /// Get audio data for a specific node
        /// </summary>
        public NodeAudioData GetNodeAudioData(int nodeIndex)
        {
            if (currentAnalysis.nodeAudioMap.ContainsKey(nodeIndex))
                return currentAnalysis.nodeAudioMap[nodeIndex];
            return null;
        }
        
        /// <summary>
        /// Get all nodes of a specific type
        /// </summary>
        public List<int> GetNodesByType(string nodeType)
        {
            return nodeType.ToLower() switch
            {
                "source" => currentAnalysis.sourceNodes,
                "highvalence" => currentAnalysis.highValenceNodes,
                "endpoint" => currentAnalysis.endpointNodes,
                "regular" => currentAnalysis.regularNodes,
                _ => new List<int>()
            };
        }
        
        /// <summary>
        /// Check if analysis is valid
        /// </summary>
        public bool IsAnalysisValid()
        {
            return currentAnalysis.isValid;
        }
        
        /// <summary>
        /// Get the current analysis data
        /// </summary>
        public GraphAudioAnalysis GetCurrentAnalysis()
        {
            return currentAnalysis;
        }
        
        #if UNITY_EDITOR
        [ContextMenu("Show Audio Mapping Info")]
        public void ShowAudioMappingInfo()
        {
            if (!currentAnalysis.isValid)
            {
                Debug.LogWarning("❌ No valid audio analysis. Run 'Analyze Graph Audio' first.");
                return;
            }
            
            Debug.Log("🎼 GRAPH AUDIO MAPPING INFO");
            Debug.Log("===========================");
            Debug.Log($"Tonal Strategy: {tonalStrategy}");
            Debug.Log($"Musical Scale: {musicalScale}");
            Debug.Log($"Root Frequency: {rootFrequency}Hz");
            Debug.Log($"Scale Frequencies: {string.Join(", ", currentAnalysis.scaleFrequencies.Select(f => f.ToString("F1") + "Hz"))}");
            Debug.Log("");
            Debug.Log("NODE CATEGORIES:");
            Debug.Log($"  Source nodes ({currentAnalysis.sourceNodes.Count}): {string.Join(", ", currentAnalysis.sourceNodes)}");
            Debug.Log($"  High-valence nodes ({currentAnalysis.highValenceNodes.Count}): {string.Join(", ", currentAnalysis.highValenceNodes)}");
            Debug.Log($"  Endpoint nodes ({currentAnalysis.endpointNodes.Count}): {string.Join(", ", currentAnalysis.endpointNodes)}");
            Debug.Log($"  Regular nodes ({currentAnalysis.regularNodes.Count}): {string.Join(", ", currentAnalysis.regularNodes)}");
        }
        
        [ContextMenu("Test Node Audio")]
        public void TestNodeAudio()
        {
            if (!currentAnalysis.isValid)
            {
                Debug.LogWarning("❌ No valid audio analysis. Run 'Analyze Graph Audio' first.");
                return;
            }
            
            Debug.Log("🔔 Testing audio for first few nodes...");
            int testCount = Mathf.Min(5, currentAnalysis.nodeAudioMap.Count);
            
            foreach (var kvp in currentAnalysis.nodeAudioMap.Take(testCount))
            {
                var audioData = kvp.Value;
                string nodeType = audioData.isSourceNode ? "SOURCE" : 
                                 audioData.isHighValence ? "HIGH-VALENCE" : 
                                 audioData.isEndpoint ? "ENDPOINT" : "REGULAR";
                
                Debug.Log($"  Node {audioData.nodeIndex} ({nodeType}): {audioData.frequency:F1}Hz, " +
                         $"valence={audioData.valence}, chime={audioData.audioSettings.chimeType}");
            }
        }
        #endif
    }
}

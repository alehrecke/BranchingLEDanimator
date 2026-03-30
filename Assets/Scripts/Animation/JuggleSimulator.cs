using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Player;
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Simulates multiple people playing the juggle game together.
    /// Automatically catches balls with configurable skill levels to preview
    /// how the installation would feel with a crowd of players.
    /// </summary>
    public class JuggleSimulator : MonoBehaviour
    {
        [Header("Simulation Control")]
        [Tooltip("Enable/disable the simulation")]
        public bool simulationEnabled = true;
        
        [Header("Player Skill")]
        [Tooltip("How often players successfully catch (0=never, 1=always)")]
        [Range(0f, 1f)]
        public float catchSuccessRate = 0.85f;
        
        [Tooltip("Reaction time variation - higher = more realistic misses")]
        [Range(0f, 0.5f)]
        public float reactionTimeVariation = 0.15f;
        
        [Tooltip("Simulate fatigue - skill decreases over time")]
        public bool simulateFatigue = true;
        
        [Tooltip("How much skill drops as momentum increases")]
        [Range(0f, 0.3f)]
        public float fatigueRate = 0.1f;
        
        [Header("Crowd Behavior")]
        [Tooltip("Number of simulated players")]
        [Range(1, 8)]
        public int playerCount = 4;
        
        [Tooltip("Players take turns vs. anyone catches")]
        public bool turnBasedCatching = false;
        
        [Tooltip("Simulate excitement - faster reactions during streaks")]
        public bool simulateExcitement = true;
        
        [Header("Ball Control")]
        [Tooltip("Current number of balls (adjust in real-time)")]
        [Range(1, 5)]
        public int currentBallTarget = 1;
        
        [Tooltip("Maximum balls allowed")]
        [Range(1, 5)]
        public int maxAutoBalls = 3;
        
        [Header("Auto-Scaling (Optional)")]
        [Tooltip("Automatically increase balls as crowd gets going")]
        public bool autoScaleBalls = false;
        
        [Tooltip("Add a ball every N successful catches")]
        public int catchesPerNewBall = 5;
        
        [Header("Debug")]
        public bool showDebugInfo = true;
        
        // Reference to animation
        private GraphJuggleAnimation juggleAnimation;
        private LEDGraphManager graphManager;
        
        // Simulated players
        private class SimulatedPlayer
        {
            public int assignedEndpoint = -1; // Which endpoint they're "standing at"
            public float reactionDelay;
            public float skillLevel;
            public bool isReacting = false;
            public float reactionStartTime;
            public int targetEndpoint;
        }
        
        private List<SimulatedPlayer> players = new List<SimulatedPlayer>();
        private List<int> availableEndpoints = new List<int>();
        private Dictionary<int, Vector3> endpointPositions = new Dictionary<int, Vector3>();
        
        // Tracking
        private int totalCatches = 0;
        private int totalMisses = 0;
        private int catchesSinceLastBall = 0;
        private int currentBallCount = 1;
        private bool initialized = false;
        
        void Start()
        {
            // Find animation - try to get from animator system first
            var animSystem = FindFirstObjectByType<LEDAnimationSystem>();
            if (animSystem != null)
            {
                juggleAnimation = animSystem.CurrentAnimation as GraphJuggleAnimation;
            }
        }
        
        void OnEnable()
        {
            initialized = false;
            totalCatches = 0;
            totalMisses = 0;
            catchesSinceLastBall = 0;
            currentBallCount = 1;
        }
        
        void OnDisable()
        {
            // Stop all player reactions
            foreach (var player in players)
            {
                player.isReacting = false;
            }
            
            // Reset ball count on the animation
            if (juggleAnimation != null)
            {
                juggleAnimation.SetMaxBalls(1);
            }
            
            initialized = false;
        }
        
        private bool wasEnabled = false;
        private int lastBallTarget = 1;
        private int lastPlayerCount = 4;
        private float lastCatchRate = 0.85f;
        
        void Update()
        {
            // Handle toggling off
            if (!simulationEnabled && wasEnabled)
            {
                foreach (var player in players)
                {
                    player.isReacting = false;
                }
                if (juggleAnimation != null)
                {
                    juggleAnimation.SetMaxBalls(1);
                }
                currentBallCount = 1;
                currentBallTarget = 1;
                wasEnabled = false;
                return;
            }
            
            if (!simulationEnabled) return;
            wasEnabled = true;
            
            // Clamp ball target to max
            currentBallTarget = Mathf.Clamp(currentBallTarget, 1, maxAutoBalls);
            
            // Respond to real-time ball target changes
            if (currentBallTarget != lastBallTarget)
            {
                currentBallCount = currentBallTarget;
                if (juggleAnimation != null)
                {
                    juggleAnimation.SetMaxBalls(currentBallCount);
                }
                lastBallTarget = currentBallTarget;
                if (showDebugInfo)
                {
                    Debug.Log($"🎱 Ball count set to {currentBallCount}");
                }
            }
            
            // Reinitialize players if count changed
            if (playerCount != lastPlayerCount)
            {
                InitializePlayers();
                lastPlayerCount = playerCount;
            }
            
            // Update skill levels if catch rate changed
            if (Mathf.Abs(catchSuccessRate - lastCatchRate) > 0.01f)
            {
                foreach (var player in players)
                {
                    player.skillLevel = catchSuccessRate + Random.Range(-0.1f, 0.1f);
                    player.skillLevel = Mathf.Clamp01(player.skillLevel);
                }
                lastCatchRate = catchSuccessRate;
            }
            
            // Try to find animation if not found yet
            if (juggleAnimation == null)
            {
                var animSystem = FindFirstObjectByType<LEDAnimationSystem>();
                if (animSystem != null)
                {
                    juggleAnimation = animSystem.CurrentAnimation as GraphJuggleAnimation;
                }
                if (juggleAnimation == null) return;
            }
            
            // Initialize
            if (!initialized)
            {
                TryInitialize();
                if (!initialized) return;
            }
            
            // Check for catchable balls and simulate player reactions
            SimulatePlayerBehavior();
        }
        
        void TryInitialize()
        {
            graphManager = FindFirstObjectByType<LEDGraphManager>();
            if (graphManager == null || graphManager.NodePositions == null) return;
            
            var nodePositions = graphManager.NodePositions;
            var edges = graphManager.EdgeConnections;
            if (nodePositions.Count == 0 || edges.Count == 0) return;
            
            // Build adjacency to find endpoints
            var adjacency = new Dictionary<int, List<int>>();
            for (int i = 0; i < nodePositions.Count; i++)
            {
                adjacency[i] = new List<int>();
            }
            
            foreach (var edge in edges)
            {
                if (edge.x < nodePositions.Count && edge.y < nodePositions.Count)
                {
                    if (!adjacency[edge.x].Contains(edge.y))
                        adjacency[edge.x].Add(edge.y);
                    if (!adjacency[edge.y].Contains(edge.x))
                        adjacency[edge.y].Add(edge.x);
                }
            }
            
            // Find endpoints
            availableEndpoints.Clear();
            endpointPositions.Clear();
            
            foreach (var kvp in adjacency)
            {
                if (kvp.Value.Count == 1)
                {
                    availableEndpoints.Add(kvp.Key);
                    endpointPositions[kvp.Key] = nodePositions[kvp.Key];
                }
            }
            
            if (availableEndpoints.Count == 0) return;
            
            // Create simulated players
            InitializePlayers();
            
            initialized = true;
            
            if (showDebugInfo)
            {
                Debug.Log($"🎮 JuggleSimulator initialized with {players.Count} players at {availableEndpoints.Count} endpoints");
            }
        }
        
        void InitializePlayers()
        {
            players.Clear();
            
            // Shuffle endpoints for random assignment
            var shuffledEndpoints = availableEndpoints.OrderBy(x => Random.value).ToList();
            
            for (int i = 0; i < playerCount; i++)
            {
                var player = new SimulatedPlayer
                {
                    assignedEndpoint = turnBasedCatching && i < shuffledEndpoints.Count 
                        ? shuffledEndpoints[i] 
                        : -1, // -1 means "roaming" - can catch anywhere
                    reactionDelay = Random.Range(0.1f, 0.3f),
                    skillLevel = catchSuccessRate + Random.Range(-0.1f, 0.1f),
                    isReacting = false
                };
                
                player.skillLevel = Mathf.Clamp01(player.skillLevel);
                players.Add(player);
            }
        }
        
        void SimulatePlayerBehavior()
        {
            if (!juggleAnimation.IsGameActive) return;
            
            float currentTime = Time.time;
            
            // Get all catchable endpoints
            var catchableEndpoints = juggleAnimation.GetAllCatchableEndpoints();
            
            foreach (int catchableEndpoint in catchableEndpoints)
            {
                // Find a player who can catch this
                SimulatedPlayer catcher = FindPlayerForEndpoint(catchableEndpoint);
                
                if (catcher != null && !catcher.isReacting)
                {
                    // Start reaction
                    catcher.isReacting = true;
                    catcher.reactionStartTime = currentTime;
                    catcher.targetEndpoint = catchableEndpoint;
                    
                    // Calculate actual reaction time with variation
                    float baseReaction = catcher.reactionDelay;
                    
                    // Excitement speeds up reactions during streaks
                    if (simulateExcitement && juggleAnimation.CurrentStreak > 3)
                    {
                        baseReaction *= 0.7f;
                    }
                    
                    // Fatigue slows reactions at high momentum
                    if (simulateFatigue)
                    {
                        float momentum = juggleAnimation.CurrentMomentum;
                        baseReaction *= 1f + (momentum - 1f) * fatigueRate;
                    }
                    
                    catcher.reactionDelay = baseReaction + Random.Range(-reactionTimeVariation, reactionTimeVariation);
                }
            }
            
            // Process player reactions
            foreach (var player in players)
            {
                if (!player.isReacting) continue;
                
                if (currentTime - player.reactionStartTime >= player.reactionDelay)
                {
                    // Time to act
                    TryPerformCatch(player);
                    player.isReacting = false;
                }
            }
        }
        
        SimulatedPlayer FindPlayerForEndpoint(int endpoint)
        {
            // If turn-based, find player assigned to this endpoint
            if (turnBasedCatching)
            {
                foreach (var player in players)
                {
                    if (player.assignedEndpoint == endpoint && !player.isReacting)
                    {
                        return player;
                    }
                }
                return null;
            }
            
            // Otherwise, find any available player
            var availablePlayers = players.Where(p => !p.isReacting).ToList();
            if (availablePlayers.Count == 0) return null;
            
            // Pick random available player
            return availablePlayers[Random.Range(0, availablePlayers.Count)];
        }
        
        void TryPerformCatch(SimulatedPlayer player)
        {
            if (!endpointPositions.ContainsKey(player.targetEndpoint)) return;
            
            // Calculate effective skill
            float effectiveSkill = player.skillLevel;
            
            // Fatigue reduces skill at high momentum
            if (simulateFatigue)
            {
                float momentum = juggleAnimation.CurrentMomentum;
                effectiveSkill -= (momentum - 1f) * fatigueRate;
            }
            
            effectiveSkill = Mathf.Clamp01(effectiveSkill);
            
            // Determine if catch succeeds
            bool success = Random.value < effectiveSkill;
            
            if (success)
            {
                // Trigger the catch
                Vector3 position = endpointPositions[player.targetEndpoint];
                GraphPlayerController.SimulatePress(graphManager, player.targetEndpoint, position);
                
                // Immediately release (it's a tap, not a hold)
                StartCoroutine(DelayedRelease(graphManager, player.targetEndpoint, position, 0.1f));
                
                totalCatches++;
                catchesSinceLastBall++;
                
                if (showDebugInfo)
                {
                    Debug.Log($"👆 Simulated CATCH at endpoint {player.targetEndpoint} (skill: {effectiveSkill:P0})");
                }
                
                // Auto-scale balls
                if (autoScaleBalls && catchesSinceLastBall >= catchesPerNewBall && currentBallTarget < maxAutoBalls)
                {
                    currentBallTarget++;
                    currentBallCount = currentBallTarget;
                    lastBallTarget = currentBallTarget;
                    juggleAnimation.SetMaxBalls(currentBallCount);
                    catchesSinceLastBall = 0;
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"🎱 Auto-scaled to {currentBallCount} balls!");
                    }
                }
            }
            else
            {
                // Player missed - do nothing (ball will pass through)
                totalMisses++;
                
                if (showDebugInfo)
                {
                    Debug.Log($"❌ Simulated MISS at endpoint {player.targetEndpoint} (skill: {effectiveSkill:P0})");
                }
                
                // Reset ball scaling progress on miss
                catchesSinceLastBall = Mathf.Max(0, catchesSinceLastBall - 2);
            }
        }
        
        System.Collections.IEnumerator DelayedRelease(LEDGraphManager source, int endpoint, Vector3 position, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (source != null)
                GraphPlayerController.SimulateRelease(source, endpoint, position);
        }
        
        // Context menu actions
        [ContextMenu("Reset Statistics")]
        void ResetStats()
        {
            totalCatches = 0;
            totalMisses = 0;
            catchesSinceLastBall = 0;
            currentBallCount = 1;
            currentBallTarget = 1;
            lastBallTarget = 1;
            
            if (juggleAnimation != null)
            {
                juggleAnimation.SetMaxBalls(1);
            }
            
            Debug.Log("📊 Statistics reset");
        }
        
        [ContextMenu("Add Ball Now")]
        void AddBallNow()
        {
            if (juggleAnimation != null)
            {
                currentBallCount = Mathf.Min(maxAutoBalls, currentBallCount + 1);
                juggleAnimation.SetMaxBalls(currentBallCount);
                Debug.Log($"🎱 Now at {currentBallCount} balls");
            }
        }
        
        [ContextMenu("Force Perfect Catch")]
        void ForcePerfectCatch()
        {
            if (juggleAnimation == null) return;
            
            int endpoint;
            if (juggleAnimation.TryGetCatchableEndpoint(out endpoint))
            {
                Vector3 position = endpointPositions.ContainsKey(endpoint) 
                    ? endpointPositions[endpoint] 
                    : Vector3.zero;
                    
                GraphPlayerController.SimulatePress(graphManager, endpoint, position);
                StartCoroutine(DelayedRelease(graphManager, endpoint, position, 0.1f));
                
                Debug.Log($"✨ Forced catch at endpoint {endpoint}");
            }
        }
        
        void OnGUI()
        {
            if (!showDebugInfo || !simulationEnabled) return;
            
            GUILayout.BeginArea(new Rect(10, 220, 300, 180));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("🎮 Juggle Simulator");
            GUILayout.Label($"Players: {playerCount}");
            GUILayout.Label($"Balls: {currentBallCount}/{maxAutoBalls}");
            
            if (juggleAnimation != null)
            {
                GUILayout.Label($"Momentum: {juggleAnimation.CurrentMomentum:F2}x");
                GUILayout.Label($"Score: {juggleAnimation.CurrentScore}");
                GUILayout.Label($"Streak: {juggleAnimation.CurrentStreak}");
            }
            
            GUILayout.Space(5);
            float successRate = totalCatches + totalMisses > 0 
                ? (float)totalCatches / (totalCatches + totalMisses) 
                : 0;
            GUILayout.Label($"Catches: {totalCatches} | Misses: {totalMisses}");
            GUILayout.Label($"Success Rate: {successRate:P0}");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}

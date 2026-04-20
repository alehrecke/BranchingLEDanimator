using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Player;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Interactive animation where each graph leg starts as a random color.
    /// Touching a ground endpoint triggers a hue-incremented color wipe up
    /// that leg to the nearest junction. Spamming creates visible color bands.
    /// Interior branches auto-wipe with a blended color when both adjacent
    /// junctions have been activated.
    /// </summary>
    [CreateAssetMenu(fileName = "ColorWipeAnimation", menuName = "LED Animations/Interactive/Color Wipe")]
    public class ColorWipeAnimation : LEDAnimationType
    {
        public enum AudioMode
        {
            ProceduralTones,
            CustomClips
        }

        [Header("Wipe Settings")]
        [Tooltip("Speed of the color wipe front (nodes per second)")]
        [Range(5f, 200f)]
        public float wipeSpeed = 40f;

        [Tooltip("Hue advance per trigger (wraps around the color wheel)")]
        [Range(0.01f, 0.5f)]
        public float hueIncrement = 0.08f;

        [Header("Color Settings")]
        [Range(0f, 1f)]
        public float colorSaturation = 0.85f;
        [Range(0f, 1f)]
        public float colorValue = 1f;

        [Tooltip("Color for junction nodes when idle")]
        public Color junctionColor = new Color(0.15f, 0.15f, 0.2f);

        [Header("Interior Propagation")]
        [Tooltip("Auto-wipe interior branches when both adjacent junctions are activated")]
        public bool enableInteriorPropagation = true;

        [Tooltip("Delay before interior wipe starts after both junctions are ready")]
        [Range(0f, 2f)]
        public float interiorWipeDelay = 0.3f;

        [Tooltip("Speed of interior branch wipes (nodes per second)")]
        [Range(5f, 200f)]
        public float interiorWipeSpeed = 30f;

        [Header("Audio")]
        public bool enableAudio = true;

        [Tooltip("Choose between procedural tones or custom audio clips")]
        public AudioMode audioMode = AudioMode.ProceduralTones;

        [Header("Custom Audio Clips (when AudioMode = CustomClips)")]
        [Tooltip("Clips assigned to endpoints by height (lowest to highest). Cycles if fewer clips than endpoints.")]
        public AudioClip[] customClips;

        [Tooltip("Should custom clips loop while playing?")]
        public bool loopCustomClips = false;

        [Header("Procedural Tone Settings (when AudioMode = ProceduralTones)")]
        public float baseFrequency = 220f;

        [Header("Audio Volume & Envelope")]
        [Range(0f, 1f)]
        public float audioVolume = 0.4f;
        public float audioAttack = 0.05f;
        public float audioRelease = 0.5f;

        [Header("Debug")]
        public bool showDebugInfo = false;

        // --- Data model ---

        private class ColorWipe
        {
            public float startTime;
            public float newHue;
        }

        private class Leg
        {
            public int id;
            public List<int> nodes = new List<int>();
            public bool isGround;
            public float baseHue;
            public List<ColorWipe> activeWipes = new List<ColorWipe>();
            public int endpointNode = -1; // valence-1 node, or -1 for interior
            public int junctionEnd = -1;  // junction at the far end (ground legs)
            // For interior legs: nodes[0] side junction and nodes[last] side junction
            public int junctionA = -1;
            public int junctionB = -1;
        }

        private class JunctionInfo
        {
            public int nodeIndex;
            public List<int> connectedLegIds = new List<int>();
            public float lastActivatedTime = -1f;
            public float currentHue;
            public int activationCount;
        }

        // Audio state per wipe
        private class WipeAudio
        {
            public AudioSource source;
            public float targetVolume;
            public float currentVolume;
            public float fadeStartTime;
        }

        // Graph state
        private Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
        private List<int> endpointNodes = new List<int>();
        private List<int> junctionNodes = new List<int>();
        private List<Leg> legs = new List<Leg>();
        private Dictionary<int, Leg> nodeToLeg = new Dictionary<int, Leg>();
        private Dictionary<int, List<Leg>> nodeToLegs = new Dictionary<int, List<Leg>>();
        private Dictionary<int, JunctionInfo> junctions = new Dictionary<int, JunctionInfo>();
        private Dictionary<int, Leg> endpointToLeg = new Dictionary<int, Leg>();
        private Dictionary<int, AudioClip> endpointClips = new Dictionary<int, AudioClip>();
        private Dictionary<int, float> endpointFrequencies = new Dictionary<int, float>();

        // Audio
        private GameObject audioContainer;
        private List<WipeAudio> activeAudioSources = new List<WipeAudio>();

        // State flags
        private bool analysisComplete;
        private bool subscribedToEvents;
        private List<Vector3> cachedPositions;

        public override bool SupportsLEDMode => false;

        // =====================================================================
        // Main render loop
        // =====================================================================

        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            Color[] colors = new Color[nodePositions.Count];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = inactiveColor;

            // Re-init if audio container was destroyed between sessions
            if (analysisComplete && enableAudio && audioContainer == null)
                analysisComplete = false;

            if (!analysisComplete)
            {
                AnalyzeGraph(nodePositions, edgeConnections);
                SetupAudioContainer();
                SubscribeToPlayerEvents();
            }

            float currentTime = Time.time;

            // Update wipes and check for completions
            UpdateWipes(currentTime);

            // Update audio fade envelopes
            UpdateAudio(Time.deltaTime);

            // Render each leg
            foreach (var leg in legs)
            {
                RenderLeg(leg, currentTime, colors);
            }

            // Render junction nodes as blend of connected legs
            foreach (var kvp in junctions)
            {
                RenderJunction(kvp.Value, colors);
            }

            return colors;
        }

        // =====================================================================
        // Graph analysis
        // =====================================================================

        void AnalyzeGraph(List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            cachedPositions = nodePositions;
            adjacency.Clear();
            endpointNodes.Clear();
            junctionNodes.Clear();
            legs.Clear();
            nodeToLeg.Clear();
            nodeToLegs.Clear();
            junctions.Clear();
            endpointToLeg.Clear();
            endpointClips.Clear();
            endpointFrequencies.Clear();

            // Build adjacency
            for (int i = 0; i < nodePositions.Count; i++)
                adjacency[i] = new List<int>();

            foreach (var edge in edgeConnections)
            {
                if (edge.x < nodePositions.Count && edge.y < nodePositions.Count)
                {
                    if (!adjacency[edge.x].Contains(edge.y))
                        adjacency[edge.x].Add(edge.y);
                    if (!adjacency[edge.y].Contains(edge.x))
                        adjacency[edge.y].Add(edge.x);
                }
            }

            // Classify nodes
            foreach (var kvp in adjacency)
            {
                int valence = kvp.Value.Count;
                if (valence == 1)
                    endpointNodes.Add(kvp.Key);
                else if (valence > 2)
                    junctionNodes.Add(kvp.Key);
            }

            // Create junction info objects
            foreach (int j in junctionNodes)
            {
                junctions[j] = new JunctionInfo
                {
                    nodeIndex = j,
                    currentHue = Random.value
                };
            }

            // Trace ground legs: from each endpoint, walk through valence-2 nodes to the first junction
            HashSet<int> assignedNodes = new HashSet<int>();
            int legId = 0;

            foreach (int endpoint in endpointNodes)
            {
                var leg = TraceLeg(endpoint, nodePositions);
                if (leg.nodes.Count > 0)
                {
                    leg.id = legId++;
                    leg.isGround = true;
                    leg.endpointNode = endpoint;
                    leg.baseHue = Random.value;

                    // Identify the junction end
                    int lastNode = leg.nodes[leg.nodes.Count - 1];
                    if (junctions.ContainsKey(lastNode))
                    {
                        leg.junctionEnd = lastNode;
                        leg.junctionA = -1; // endpoint side
                        leg.junctionB = lastNode;
                        junctions[lastNode].connectedLegIds.Add(leg.id);
                    }

                    legs.Add(leg);
                    endpointToLeg[endpoint] = leg;

                    // Register non-junction nodes to this leg
                    foreach (int n in leg.nodes)
                    {
                        if (!junctions.ContainsKey(n))
                        {
                            assignedNodes.Add(n);
                            nodeToLeg[n] = leg;
                        }
                        if (!nodeToLegs.ContainsKey(n))
                            nodeToLegs[n] = new List<Leg>();
                        nodeToLegs[n].Add(leg);
                    }
                }
            }

            // Trace interior legs: from each junction, follow unassigned neighbors
            foreach (int jNode in junctionNodes)
            {
                foreach (int neighbor in adjacency[jNode])
                {
                    if (assignedNodes.Contains(neighbor)) continue;
                    if (junctions.ContainsKey(neighbor)) continue; // junction-to-junction direct edge handled below

                    var leg = TraceInteriorLeg(jNode, neighbor, assignedNodes, nodePositions);
                    if (leg.nodes.Count > 0)
                    {
                        leg.id = legId++;
                        leg.isGround = false;
                        leg.baseHue = Random.value;

                        int first = leg.nodes[0];
                        int last = leg.nodes[leg.nodes.Count - 1];
                        leg.junctionA = junctions.ContainsKey(first) ? first : -1;
                        leg.junctionB = junctions.ContainsKey(last) ? last : -1;

                        if (leg.junctionA >= 0) junctions[leg.junctionA].connectedLegIds.Add(leg.id);
                        if (leg.junctionB >= 0) junctions[leg.junctionB].connectedLegIds.Add(leg.id);

                        legs.Add(leg);

                        foreach (int n in leg.nodes)
                        {
                            if (!junctions.ContainsKey(n))
                            {
                                assignedNodes.Add(n);
                                nodeToLeg[n] = leg;
                            }
                            if (!nodeToLegs.ContainsKey(n))
                                nodeToLegs[n] = new List<Leg>();
                            nodeToLegs[n].Add(leg);
                        }
                    }
                }
            }

            // Handle direct junction-to-junction edges (no intermediate valence-2 nodes)
            HashSet<long> handledJunctionPairs = new HashSet<long>();
            foreach (int jA in junctionNodes)
            {
                foreach (int neighbor in adjacency[jA])
                {
                    if (!junctions.ContainsKey(neighbor)) continue;
                    long pairKey = Mathf.Min(jA, neighbor) * 100000L + Mathf.Max(jA, neighbor);
                    if (handledJunctionPairs.Contains(pairKey)) continue;
                    handledJunctionPairs.Add(pairKey);

                    // Check if this pair is already connected by an interior leg
                    bool alreadyCovered = legs.Any(l => !l.isGround &&
                        ((l.junctionA == jA && l.junctionB == neighbor) ||
                         (l.junctionA == neighbor && l.junctionB == jA)));
                    if (alreadyCovered) continue;

                    var leg = new Leg
                    {
                        id = legId++,
                        isGround = false,
                        baseHue = Random.value,
                        junctionA = jA,
                        junctionB = neighbor,
                        nodes = new List<int> { jA, neighbor }
                    };

                    junctions[jA].connectedLegIds.Add(leg.id);
                    junctions[neighbor].connectedLegIds.Add(leg.id);
                    legs.Add(leg);

                    foreach (int n in leg.nodes)
                    {
                        if (!nodeToLegs.ContainsKey(n))
                            nodeToLegs[n] = new List<Leg>();
                        nodeToLegs[n].Add(leg);
                    }
                }
            }

            // Audio: assign frequencies and clips to ground endpoints sorted by height
            var sortedEndpoints = endpointNodes.OrderBy(e => nodePositions[e].y).ToList();
            int[] pentatonic = { 0, 2, 4, 7, 9 };
            for (int i = 0; i < sortedEndpoints.Count; i++)
            {
                int ep = sortedEndpoints[i];
                int scaleIndex = i % pentatonic.Length;
                int octave = i / pentatonic.Length;
                int semitones = pentatonic[scaleIndex] + (octave * 12);
                endpointFrequencies[ep] = baseFrequency * Mathf.Pow(2f, semitones / 12f);

                if (audioMode == AudioMode.CustomClips && customClips != null && customClips.Length > 0)
                {
                    int clipIdx = i % customClips.Length;
                    if (customClips[clipIdx] != null)
                        endpointClips[ep] = customClips[clipIdx];
                }
            }

            int groundCount = legs.Count(l => l.isGround);
            int interiorCount = legs.Count(l => !l.isGround);
            if (showDebugInfo)
                Debug.Log($"ColorWipe: {endpointNodes.Count} endpoints, {junctionNodes.Count} junctions, " +
                          $"{groundCount} ground legs, {interiorCount} interior legs");

            analysisComplete = true;
        }

        Leg TraceLeg(int startEndpoint, List<Vector3> nodePositions)
        {
            var leg = new Leg();
            leg.nodes.Add(startEndpoint);

            int current = startEndpoint;
            int previous = -1;

            while (true)
            {
                var neighbors = adjacency[current];
                int next = -1;
                foreach (int n in neighbors)
                {
                    if (n != previous) { next = n; break; }
                }

                if (next == -1) break;

                leg.nodes.Add(next);

                // Stop at junctions
                if (junctions.ContainsKey(next))
                    break;

                previous = current;
                current = next;

                if (leg.nodes.Count > 5000) break;
            }

            return leg;
        }

        Leg TraceInteriorLeg(int junctionStart, int firstStep, HashSet<int> assignedNodes, List<Vector3> nodePositions)
        {
            var leg = new Leg();
            leg.nodes.Add(junctionStart);
            leg.nodes.Add(firstStep);

            int current = firstStep;
            int previous = junctionStart;

            while (true)
            {
                if (junctions.ContainsKey(current))
                    break; // reached another junction

                var neighbors = adjacency[current];
                int next = -1;
                foreach (int n in neighbors)
                {
                    if (n != previous) { next = n; break; }
                }

                if (next == -1) break;

                leg.nodes.Add(next);
                previous = current;
                current = next;

                if (leg.nodes.Count > 5000) break;
            }

            return leg;
        }

        // =====================================================================
        // Player events
        // =====================================================================

        void SubscribeToPlayerEvents()
        {
            if (subscribedToEvents) return;
            GraphPlayerController.OnEndpointPressed += OnEndpointPressed;
            subscribedToEvents = true;
        }

        void OnEndpointPressed(LEDGraphManager source, int endpointIndex, Vector3 position)
        {
            if (OwnerGraphManager != null && source != OwnerGraphManager) return;
            if (!endpointToLeg.ContainsKey(endpointIndex)) return;

            var leg = endpointToLeg[endpointIndex];

            // Determine new hue: increment from the most recent wipe, or from baseHue
            float currentHue = leg.activeWipes.Count > 0
                ? leg.activeWipes[leg.activeWipes.Count - 1].newHue
                : leg.baseHue;
            float newHue = (currentHue + hueIncrement) % 1f;

            leg.activeWipes.Add(new ColorWipe
            {
                startTime = Time.time,
                newHue = newHue
            });

            if (showDebugInfo)
                Debug.Log($"ColorWipe: Endpoint {endpointIndex} triggered, hue {currentHue:F2} -> {newHue:F2} " +
                          $"({leg.activeWipes.Count} active wipes on leg {leg.id})");

            // Trigger audio
            if (enableAudio)
                PlayWipeAudio(endpointIndex);
        }

        // =====================================================================
        // Wipe update & interior propagation
        // =====================================================================

        void UpdateWipes(float currentTime)
        {
            foreach (var leg in legs)
            {
                if (leg.activeWipes.Count == 0) continue;

                float speed = leg.isGround ? wipeSpeed : interiorWipeSpeed;
                int legLength = leg.nodes.Count;

                // Check if oldest wipe has completed
                for (int w = leg.activeWipes.Count - 1; w >= 0; w--)
                {
                    var wipe = leg.activeWipes[w];
                    float elapsed = currentTime - wipe.startTime;
                    float wipeFront = elapsed * speed;

                    if (wipeFront >= legLength - 1)
                    {
                        // This wipe has completed — update base hue, remove it and all older wipes
                        leg.baseHue = wipe.newHue;

                        // Remove this wipe and everything older
                        leg.activeWipes.RemoveRange(0, w + 1);

                        // If this is a ground leg, notify the junction
                        if (leg.isGround && leg.junctionEnd >= 0)
                            OnGroundWipeCompleted(leg, currentTime);

                        break; // list was modified, exit loop
                    }
                }
            }
        }

        void OnGroundWipeCompleted(Leg completedLeg, float currentTime)
        {
            if (!enableInteriorPropagation) return;

            int jNode = completedLeg.junctionEnd;
            if (!junctions.ContainsKey(jNode)) return;

            var junction = junctions[jNode];
            junction.lastActivatedTime = currentTime;
            junction.currentHue = completedLeg.baseHue;
            junction.activationCount++;

            // Check all interior legs connected to this junction
            foreach (int legId in junction.connectedLegIds)
            {
                var leg = legs.FirstOrDefault(l => l.id == legId);
                if (leg == null || leg.isGround) continue;

                // Find the other junction
                int otherJunction = leg.junctionA == jNode ? leg.junctionB : leg.junctionA;
                if (otherJunction < 0 || !junctions.ContainsKey(otherJunction)) continue;

                var otherInfo = junctions[otherJunction];

                // Both junctions must have been activated at least once
                if (otherInfo.activationCount == 0) continue;

                // Avoid re-triggering if already wiping
                if (leg.activeWipes.Count > 0) continue;

                // Blend the two junction hues
                float blendedHue = BlendHues(junction.currentHue, otherInfo.currentHue);

                leg.activeWipes.Add(new ColorWipe
                {
                    startTime = currentTime + interiorWipeDelay,
                    newHue = blendedHue
                });

                if (showDebugInfo)
                    Debug.Log($"ColorWipe: Interior leg {leg.id} auto-wiping with blended hue {blendedHue:F2}");
            }
        }

        float BlendHues(float hueA, float hueB)
        {
            // Blend on the shorter arc of the hue circle
            float diff = hueB - hueA;
            if (diff > 0.5f) diff -= 1f;
            else if (diff < -0.5f) diff += 1f;
            float blended = hueA + diff * 0.5f;
            if (blended < 0f) blended += 1f;
            if (blended >= 1f) blended -= 1f;
            return blended;
        }

        // =====================================================================
        // Rendering
        // =====================================================================

        void RenderLeg(Leg leg, float currentTime, Color[] colors)
        {
            float speed = leg.isGround ? wipeSpeed : interiorWipeSpeed;
            int legLength = leg.nodes.Count;

            for (int i = 0; i < legLength; i++)
            {
                int node = leg.nodes[i];
                if (junctions.ContainsKey(node)) continue; // junctions rendered separately

                float nodeHue = leg.baseHue;

                // Walk wipes from oldest to newest; the last wipe whose front has passed this node wins
                for (int w = 0; w < leg.activeWipes.Count; w++)
                {
                    var wipe = leg.activeWipes[w];
                    float elapsed = currentTime - wipe.startTime;
                    float wipeFront = elapsed * speed;

                    if (wipeFront >= i)
                        nodeHue = wipe.newHue;
                }

                colors[node] = Color.HSVToRGB(nodeHue, colorSaturation, colorValue);
            }
        }

        void RenderJunction(JunctionInfo junction, Color[] colors)
        {
            // Blend hues of all legs connected to this junction
            List<float> hues = new List<float>();
            foreach (int legId in junction.connectedLegIds)
            {
                var leg = legs.FirstOrDefault(l => l.id == legId);
                if (leg == null) continue;

                // Use the current visual hue at the junction end of this leg
                float hue = leg.baseHue;
                if (leg.activeWipes.Count > 0)
                {
                    float speed = leg.isGround ? wipeSpeed : interiorWipeSpeed;
                    int junctionIndex = leg.nodes.IndexOf(junction.nodeIndex);
                    if (junctionIndex >= 0)
                    {
                        float currentTime = Time.time;
                        for (int w = 0; w < leg.activeWipes.Count; w++)
                        {
                            float elapsed = currentTime - leg.activeWipes[w].startTime;
                            float wipeFront = elapsed * speed;
                            if (wipeFront >= junctionIndex)
                                hue = leg.activeWipes[w].newHue;
                        }
                    }
                }
                hues.Add(hue);
            }

            if (hues.Count == 0)
            {
                colors[junction.nodeIndex] = junctionColor;
                return;
            }

            // Average hues on the circular hue wheel
            float avgHue = hues[0];
            for (int i = 1; i < hues.Count; i++)
                avgHue = BlendHues(avgHue, hues[i]);

            colors[junction.nodeIndex] = Color.HSVToRGB(avgHue, colorSaturation, colorValue);
        }

        // =====================================================================
        // Audio
        // =====================================================================

        void SetupAudioContainer()
        {
            if (!enableAudio) return;

            if (audioContainer != null)
            {
                Object.DestroyImmediate(audioContainer);
                audioContainer = null;
            }

            audioContainer = new GameObject("ColorWipeAudio");
            audioContainer.hideFlags = HideFlags.DontSave;
        }

        void PlayWipeAudio(int endpoint)
        {
            if (audioContainer == null)
            {
                audioContainer = new GameObject("ColorWipeAudio");
                audioContainer.hideFlags = HideFlags.DontSave;
            }

            var go = new GameObject($"Wipe_{endpoint}");
            go.transform.SetParent(audioContainer.transform);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = 0f;

            if (audioMode == AudioMode.CustomClips && endpointClips.ContainsKey(endpoint))
            {
                source.clip = endpointClips[endpoint];
                source.loop = loopCustomClips;
            }
            else
            {
                float freq = endpointFrequencies.ContainsKey(endpoint) ? endpointFrequencies[endpoint] : baseFrequency;
                source.clip = GenerateToneClip(freq);
                source.loop = false;
            }

            source.Play();

            activeAudioSources.Add(new WipeAudio
            {
                source = source,
                targetVolume = 1f,
                currentVolume = 0f,
                fadeStartTime = Time.time
            });
        }

        void UpdateAudio(float deltaTime)
        {
            if (!enableAudio) return;

            float attackSpeed = 1f / Mathf.Max(0.01f, audioAttack);
            float releaseSpeed = 1f / Mathf.Max(0.01f, audioRelease);

            for (int i = activeAudioSources.Count - 1; i >= 0; i--)
            {
                var wa = activeAudioSources[i];
                if (wa.source == null)
                {
                    activeAudioSources.RemoveAt(i);
                    continue;
                }

                float timeSinceStart = Time.time - wa.fadeStartTime;

                // Attack phase: fade in
                if (wa.currentVolume < wa.targetVolume)
                    wa.currentVolume = Mathf.Min(wa.currentVolume + attackSpeed * deltaTime, wa.targetVolume);

                // After the clip's natural duration (or wipe duration), start release
                float wipeDuration = (wipeSpeed > 0) ? 100f / wipeSpeed : 2f; // rough estimate
                if (timeSinceStart > audioAttack + wipeDuration * 0.5f)
                    wa.targetVolume = 0f;

                if (wa.targetVolume == 0f)
                    wa.currentVolume = Mathf.Max(wa.currentVolume - releaseSpeed * deltaTime, 0f);

                wa.source.volume = wa.currentVolume * audioVolume;

                // Clean up when silent and done
                if (wa.currentVolume <= 0f && wa.targetVolume <= 0f)
                {
                    Object.DestroyImmediate(wa.source.gameObject);
                    activeAudioSources.RemoveAt(i);
                }
            }
        }

        AudioClip GenerateToneClip(float frequency)
        {
            int sampleRate = 44100;
            float duration = 1f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Clamp01(1f - t / duration);
                float sample = Mathf.Sin(2f * Mathf.PI * frequency * t);
                sample += 0.3f * Mathf.Sin(2f * Mathf.PI * frequency * 2f * t);
                sample += 0.15f * Mathf.Sin(2f * Mathf.PI * frequency * 3f * t);
                sample *= 0.4f * envelope;
                samples[i] = sample;
            }

            var clip = AudioClip.Create($"WipeTone_{frequency:F0}Hz", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // =====================================================================
        // Cleanup
        // =====================================================================

        public void CleanupAudio()
        {
            foreach (var wa in activeAudioSources)
            {
                if (wa.source != null)
                {
                    wa.source.Stop();
                    Object.DestroyImmediate(wa.source.gameObject);
                }
            }
            activeAudioSources.Clear();

            if (audioContainer != null)
            {
                Object.DestroyImmediate(audioContainer);
                audioContainer = null;
            }
        }

        private void OnDisable()
        {
            if (subscribedToEvents)
            {
                GraphPlayerController.OnEndpointPressed -= OnEndpointPressed;
                subscribedToEvents = false;
            }

            CleanupAudio();
            analysisComplete = false;
        }

        // =====================================================================
        // Public API (for simulator compatibility)
        // =====================================================================

        public void SimulateTouchEndpoint(int endpointIndex)
        {
            if (!endpointToLeg.ContainsKey(endpointIndex)) return;

            Vector3 pos = cachedPositions != null && endpointIndex < cachedPositions.Count
                ? cachedPositions[endpointIndex]
                : Vector3.zero;

            if (OwnerGraphManager != null)
            {
                GraphPlayerController.SimulatePress(OwnerGraphManager, endpointIndex, pos);
            }
            else
            {
                var gm = Object.FindFirstObjectByType<LEDGraphManager>();
                if (gm != null)
                    GraphPlayerController.SimulatePress(gm, endpointIndex, pos);
            }
        }

        public List<int> GetEndpointNodes() => new List<int>(endpointNodes);
        public int GetGroundLegCount() => legs.Count(l => l.isGround);
        public int GetInteriorLegCount() => legs.Count(l => !l.isGround);
    }
}

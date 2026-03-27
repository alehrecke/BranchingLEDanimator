using UnityEngine;
using System.Collections.Generic;

namespace BranchingLEDAnimator.Hardware
{
    /// <summary>
    /// Predefined circuit mapping configurations for common LED setups
    /// </summary>
    [CreateAssetMenu(fileName = "CircuitMappingPreset", menuName = "LED Animation/Circuit Mapping Preset")]
    public class CircuitMappingPresets : ScriptableObject
    {
        [System.Serializable]
        public class StripConfiguration
        {
            [Header("Strip Settings")]
            public int stripIndex;
            public bool reverseDirection = false;
            public int startOffset = 0;
            
            [Header("Physical Properties")]
            public string description = "Strip description";
            public int expectedLEDCount = 33;
            public int gpioPin = 19;
        }
        
        [Header("Preset Information")]
        public string presetName = "Custom Preset";
        public string description = "Description of this mapping configuration";
        
        [Header("Hardware Configuration")]
        public int totalLEDs = 99;
        public int ledsPerStrip = 33;
        public int numberOfStrips = 3;
        public int gpioPin = 19;
        
        [Header("Strip Configurations")]
        public List<StripConfiguration> stripConfigurations = new List<StripConfiguration>();
        
        [Header("Animation Settings")]
        public LEDCircuitMapper.MappingStrategy mappingStrategy = LEDCircuitMapper.MappingStrategy.Distribute_Evenly;
        
        /// <summary>
        /// Apply this preset to a circuit mapper
        /// </summary>
        public void ApplyToMapper(LEDCircuitMapper mapper)
        {
            if (mapper == null) return;
            
            Debug.Log($"🎨 Applying preset: {presetName}");
            
            // NOTE: Legacy settings are now read-only (calculated from polyline system)
            // The new polyline-based system doesn't use these hardcoded values
            Debug.LogWarning($"CircuitMappingPresets: Legacy preset system - values are now calculated from polyline configuration");
            Debug.LogWarning($"  - Target: {totalLEDs} LEDs, {numberOfStrips} strips, {ledsPerStrip} LEDs/strip");
            Debug.LogWarning($"  - Current: {mapper.totalPhysicalLEDs} LEDs, {mapper.numLogicalStrips} strips, {mapper.ledsPerStrip} LEDs/strip");
            Debug.LogWarning($"  - To change these values, use the new polyline configuration workflow in the LED Circuit Mapper");
            
            // Apply strip configurations
            foreach (var config in stripConfigurations)
            {
                if (config.stripIndex >= 0 && config.stripIndex < numberOfStrips)
                {
                    mapper.SetStripDirection(config.stripIndex, !config.reverseDirection);
                    mapper.SetStripOffset(config.stripIndex, config.startOffset);
                    
                    Debug.Log($"  - Strip {config.stripIndex}: {config.description} " +
                             $"(Direction: {(config.reverseDirection ? "Reversed" : "Normal")}, " +
                             $"Offset: {config.startOffset})");
                }
            }
            
            Debug.Log($"✅ Preset '{presetName}' applied successfully");
        }
        
        /// <summary>
        /// Create preset from current mapper settings
        /// </summary>
        public void CaptureFromMapper(LEDCircuitMapper mapper)
        {
            if (mapper == null) return;
            
            // Capture current values (read-only, calculated from polyline system)
            totalLEDs = mapper.totalPhysicalLEDs;
            ledsPerStrip = mapper.ledsPerStrip;
            numberOfStrips = mapper.numLogicalStrips;
            mappingStrategy = mapper.mappingStrategy;
            
            Debug.LogWarning($"CircuitMappingPresets: Captured legacy values from polyline system:");
            Debug.LogWarning($"  - Total LEDs: {totalLEDs}, Strips: {numberOfStrips}, LEDs/strip: {ledsPerStrip}");
            
            // Capture current strip configurations
            stripConfigurations.Clear();
            for (int i = 0; i < numberOfStrips; i++)
            {
                var config = new StripConfiguration
                {
                    stripIndex = i,
                    reverseDirection = false, // Would need to track this in mapper
                    startOffset = 0, // Would need to track this in mapper
                    description = $"Strip {i}",
                    expectedLEDCount = ledsPerStrip,
                    gpioPin = gpioPin
                };
                stripConfigurations.Add(config);
            }
            
            Debug.Log($"📸 Captured current mapper settings to preset '{presetName}'");
        }
        
        /// <summary>
        /// Validate this preset configuration
        /// </summary>
        public bool IsValid()
        {
            if (totalLEDs <= 0 || ledsPerStrip <= 0 || numberOfStrips <= 0)
                return false;
                
            if (totalLEDs != ledsPerStrip * numberOfStrips)
            {
                Debug.LogWarning($"Preset '{presetName}': Total LEDs ({totalLEDs}) doesn't match strips × LEDs per strip ({numberOfStrips} × {ledsPerStrip} = {numberOfStrips * ledsPerStrip})");
                return false;
            }
            
            return true;
        }
        
        #if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/LED Animation System/Create Standard Presets")]
        static void CreateStandardPresets()
        {
            CreatePreset_StandardBranching();
            CreatePreset_ReversedBranching();
            CreatePreset_SingleStrip();
        }
        
        static void CreatePreset_StandardBranching()
        {
            var preset = CreateInstance<CircuitMappingPresets>();
            preset.presetName = "Standard Branching (3 strips)";
            preset.description = "Standard 3-branch LED setup with normal direction";
            preset.totalLEDs = 99;
            preset.ledsPerStrip = 33;
            preset.numberOfStrips = 3;
            preset.gpioPin = 19;
            
            for (int i = 0; i < 3; i++)
            {
                preset.stripConfigurations.Add(new StripConfiguration
                {
                    stripIndex = i,
                    reverseDirection = false,
                    startOffset = 0,
                    description = $"Branch {i} (Normal Direction)",
                    expectedLEDCount = 33,
                    gpioPin = 19
                });
            }
            
            UnityEditor.AssetDatabase.CreateAsset(preset, "Assets/CircuitMappingPresets/StandardBranching.asset");
            Debug.Log("✅ Created Standard Branching preset");
        }
        
        static void CreatePreset_ReversedBranching()
        {
            var preset = CreateInstance<CircuitMappingPresets>();
            preset.presetName = "Reversed Branching (3 strips)";
            preset.description = "3-branch LED setup with reversed direction for proper animation flow";
            preset.totalLEDs = 99;
            preset.ledsPerStrip = 33;
            preset.numberOfStrips = 3;
            preset.gpioPin = 19;
            
            for (int i = 0; i < 3; i++)
            {
                preset.stripConfigurations.Add(new StripConfiguration
                {
                    stripIndex = i,
                    reverseDirection = true, // Reverse all strips
                    startOffset = 0,
                    description = $"Branch {i} (Reversed for Animation)",
                    expectedLEDCount = 33,
                    gpioPin = 19
                });
            }
            
            UnityEditor.AssetDatabase.CreateAsset(preset, "Assets/CircuitMappingPresets/ReversedBranching.asset");
            Debug.Log("✅ Created Reversed Branching preset");
        }
        
        static void CreatePreset_SingleStrip()
        {
            var preset = CreateInstance<CircuitMappingPresets>();
            preset.presetName = "Single Strip (Linear)";
            preset.description = "Single continuous LED strip";
            preset.totalLEDs = 150;
            preset.ledsPerStrip = 150;
            preset.numberOfStrips = 1;
            preset.gpioPin = 19;
            
            preset.stripConfigurations.Add(new StripConfiguration
            {
                stripIndex = 0,
                reverseDirection = false,
                startOffset = 0,
                description = "Single Continuous Strip",
                expectedLEDCount = 150,
                gpioPin = 19
            });
            
            UnityEditor.AssetDatabase.CreateAsset(preset, "Assets/CircuitMappingPresets/SingleStrip.asset");
            Debug.Log("✅ Created Single Strip preset");
        }
        #endif
    }
}

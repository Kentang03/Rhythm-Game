using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Contoh implementasi untuk menunjukkan satu lane dengan multiple note types
/// </summary>
public class MultiTypeNoteExample : MonoBehaviour
{
    [Header("Example Configuration")]
    [Tooltip("Chart untuk demo")]
    public SongChartSO demoChart;
    
    [Header("Example Patterns")]
    [Tooltip("Buat pattern alternating (A-B-A-B...)")]
    public bool createAlternatingPattern = false;
    
    [Tooltip("Buat pattern rainbow (A-B-C-D-A-B-C-D...)")]
    public bool createRainbowPattern = false;
    
    [Tooltip("Buat pattern berdasarkan intensitas musik")]
    public bool createIntensityPattern = false;

    [Header("Visual Test")]
    [Tooltip("Test spawn note dengan type berbeda")]
    public bool testSpawnDifferentTypes = false;
    
    private void OnValidate()
    {
        if (demoChart == null) return;
        
        if (createAlternatingPattern)
        {
            createAlternatingPattern = false;
            CreateAlternatingPattern();
        }
        
        if (createRainbowPattern)
        {
            createRainbowPattern = false;
            CreateRainbowPattern();
        }
        
        if (createIntensityPattern)
        {
            createIntensityPattern = false;
            CreateIntensityPattern();
        }
        
        if (testSpawnDifferentTypes)
        {
            testSpawnDifferentTypes = false;
            TestSpawnDifferentTypes();
        }
    }
    
    /// <summary>
    /// Contoh pattern: Lane bergantian antara dua note type
    /// </summary>
    [ContextMenu("Create Alternating Pattern")]
    public void CreateAlternatingPattern()
    {
        if (demoChart == null)
        {
            Debug.LogError("Demo chart not assigned!");
            return;
        }
        
        // Pattern alternating untuk setiap lane
        NoteType[] alternatingPairs = {
            NoteType.TypeA, NoteType.TypeB,  // Lane 1: Red-Blue
            NoteType.TypeC, NoteType.TypeD,  // Lane 2: Green-Yellow
            NoteType.TypeA, NoteType.TypeC,  // Lane 3: Red-Green
            NoteType.TypeB, NoteType.TypeD   // Lane 4: Blue-Yellow
        };
        
        for (int laneIndex = 0; laneIndex < demoChart.lanes.Count; laneIndex++)
        {
            var laneData = demoChart.lanes[laneIndex];
            laneData.noteTypes.Clear();
            
            int pairStartIndex = (laneIndex % 4) * 2;
            NoteType type1 = alternatingPairs[pairStartIndex];
            NoteType type2 = alternatingPairs[pairStartIndex + 1];
            
            for (int beatIndex = 0; beatIndex < laneData.recordedBeats.Count; beatIndex++)
            {
                NoteType selectedType = (beatIndex % 2 == 0) ? type1 : type2;
                laneData.noteTypes.Add(selectedType);
            }
            
            Debug.Log($"Lane {laneIndex} ({laneData.laneName}): Alternating {type1} - {type2}");
        }
        
        MarkDirty();
        Debug.Log("Alternating pattern created!");
    }
    
    /// <summary>
    /// Contoh pattern: Setiap lane mengikuti sequence rainbow
    /// </summary>
    [ContextMenu("Create Rainbow Pattern")]
    public void CreateRainbowPattern()
    {
        if (demoChart == null)
        {
            Debug.LogError("Demo chart not assigned!");
            return;
        }
        
        NoteType[] rainbowSequence = { 
            NoteType.TypeA, // Red
            NoteType.TypeB, // Blue  
            NoteType.TypeC, // Green
            NoteType.TypeD  // Yellow
        };
        
        foreach (var laneData in demoChart.lanes)
        {
            laneData.noteTypes.Clear();
            
            for (int beatIndex = 0; beatIndex < laneData.recordedBeats.Count; beatIndex++)
            {
                NoteType rainbowType = rainbowSequence[beatIndex % rainbowSequence.Length];
                laneData.noteTypes.Add(rainbowType);
            }
            
            Debug.Log($"Lane {laneData.laneName}: Rainbow pattern applied");
        }
        
        MarkDirty();
        Debug.Log("Rainbow pattern created - all lanes cycle through Red-Blue-Green-Yellow!");
    }
    
    /// <summary>
    /// Contoh pattern: Note type berdasarkan intensitas/difficulty
    /// </summary>
    [ContextMenu("Create Intensity Pattern")]
    public void CreateIntensityPattern()
    {
        if (demoChart == null)
        {
            Debug.LogError("Demo chart not assigned!");
            return;
        }
        
        foreach (var laneData in demoChart.lanes)
        {
            laneData.noteTypes.Clear();
            
            for (int beatIndex = 0; beatIndex < laneData.recordedBeats.Count; beatIndex++)
            {
                float beatTime = laneData.recordedBeats[beatIndex];
                
                // Intensitas berdasarkan waktu dalam lagu
                // Awal lagu: mudah (TypeA), tengah: medium (TypeB/C), akhir: sulit (TypeD)
                NoteType intensityType;
                
                if (beatTime < 30f) // 30 beat pertama - easy
                {
                    intensityType = NoteType.TypeA;
                }
                else if (beatTime < 60f) // 30-60 beat - medium
                {
                    intensityType = (beatIndex % 2 == 0) ? NoteType.TypeB : NoteType.TypeC;
                }
                else // > 60 beat - hard
                {
                    intensityType = NoteType.TypeD;
                }
                
                laneData.noteTypes.Add(intensityType);
            }
            
            Debug.Log($"Lane {laneData.laneName}: Intensity pattern applied");
        }
        
        MarkDirty();
        Debug.Log("Intensity pattern created - difficulty increases over time!");
    }
    
    /// <summary>
    /// Test spawning note dengan type berbeda (runtime test)
    /// </summary>
    [ContextMenu("Test Spawn Different Types")]
    public void TestSpawnDifferentTypes()
    {
        Debug.Log("=== Testing Multiple Note Types in Single Lane ===");
        
        // Cari LaneController untuk testing
        LaneController[] laneControllers = FindObjectsByType<LaneController>(FindObjectsSortMode.None);
        
        if (laneControllers.Length == 0)
        {
            Debug.LogWarning("No LaneController found for testing!");
            return;
        }
        
        LaneController testLane = laneControllers[0];
        
        // Setup untuk central judgment mode
        testLane.useCentralJudgment = true;
        
        // Simulate different note types
        Debug.Log($"Lane {testLane.name} configuration:");
        Debug.Log($"  - Use Central Judgment: {testLane.useCentralJudgment}");
        Debug.Log($"  - Lane Note Type: {testLane.laneNoteType}");
        Debug.Log($"  - Input Key: {testLane.inputKey}");
        
        // Print note types dalam chart jika ada
        if (demoChart != null && demoChart.lanes.Count > 0)
        {
            var laneData = demoChart.lanes[0];
            Debug.Log($"Chart note types for first lane ({laneData.noteTypes.Count} notes):");
            
            for (int i = 0; i < Mathf.Min(10, laneData.noteTypes.Count); i++) // Print first 10
            {
                Debug.Log($"    Beat {i}: {laneData.noteTypes[i]}");
            }
            
            if (laneData.noteTypes.Count > 10)
            {
                Debug.Log($"    ... and {laneData.noteTypes.Count - 10} more notes");
            }
        }
        
        Debug.Log("Test completed! Check console for note type information.");
    }
    
    /// <summary>
    /// Contoh setup untuk mixed gameplay
    /// </summary>
    [ContextMenu("Setup Mixed Gameplay Example")]
    public void SetupMixedGameplayExample()
    {
        if (demoChart == null)
        {
            Debug.LogError("Demo chart not assigned!");
            return;
        }
        
        Debug.Log("Setting up mixed gameplay example...");
        
        // Lane 1: Hanya TypeA (Red) - traditional single type
        if (demoChart.lanes.Count > 0)
        {
            var lane1 = demoChart.lanes[0];
            lane1.noteTypes.Clear();
            for (int i = 0; i < lane1.recordedBeats.Count; i++)
            {
                lane1.noteTypes.Add(NoteType.TypeA);
            }
            Debug.Log($"Lane 1: All Red notes ({NoteType.TypeA})");
        }
        
        // Lane 2: Mixed TypeB dan TypeC - alternating
        if (demoChart.lanes.Count > 1)
        {
            var lane2 = demoChart.lanes[1];
            lane2.noteTypes.Clear();
            for (int i = 0; i < lane2.recordedBeats.Count; i++)
            {
                NoteType alternating = (i % 2 == 0) ? NoteType.TypeB : NoteType.TypeC;
                lane2.noteTypes.Add(alternating);
            }
            Debug.Log($"Lane 2: Alternating Blue-Green notes");
        }
        
        // Lane 3: Random mix - chaos mode
        if (demoChart.lanes.Count > 2)
        {
            var lane3 = demoChart.lanes[2];
            lane3.noteTypes.Clear();
            NoteType[] allTypes = { NoteType.TypeA, NoteType.TypeB, NoteType.TypeC, NoteType.TypeD };
            
            for (int i = 0; i < lane3.recordedBeats.Count; i++)
            {
                NoteType randomType = allTypes[Random.Range(0, allTypes.Length)];
                lane3.noteTypes.Add(randomType);
            }
            Debug.Log($"Lane 3: Random mix of all note types");
        }
        
        // Lane 4: Pattern berdasarkan beat (if exists)
        if (demoChart.lanes.Count > 3)
        {
            var lane4 = demoChart.lanes[3];
            lane4.noteTypes.Clear();
            
            for (int i = 0; i < lane4.recordedBeats.Count; i++)
            {
                // Pattern: setiap 4 beat cycle through all types
                NoteType cyclic = (NoteType)(i % 4);
                lane4.noteTypes.Add(cyclic);
            }
            Debug.Log($"Lane 4: Cyclic pattern through all types");
        }
        
        MarkDirty();
        Debug.Log("Mixed gameplay example setup complete!");
        Debug.Log("Each lane now demonstrates different note type usage patterns.");
    }
    
    private void MarkDirty()
    {
        #if UNITY_EDITOR
        if (demoChart != null)
        {
            UnityEditor.EditorUtility.SetDirty(demoChart);
        }
        #endif
    }
}
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Editor utility untuk mengatur note types pada lane data
/// Memungkinkan satu lane mengeluarkan note berbeda-beda
/// </summary>
public class LaneNoteTypeEditor : MonoBehaviour
{
    [Header("Lane Note Type Configuration")]
    [Tooltip("Chart yang akan diedit")]
    public SongChartSO targetChart;
    
    [Header("Quick Actions")]
    [Tooltip("Isi semua lane dengan random note types")]
    public bool fillWithRandomTypes = false;
    
    [Tooltip("Clear semua note types (kembali ke default)")]
    public bool clearAllNoteTypes = false;
    
    [Tooltip("Generate pattern berdasarkan index beat")]
    public bool generatePatternByBeat = false;

    [Header("Pattern Configuration")]
    [Tooltip("Pattern note types yang akan diulang")]
    public List<NoteType> patternSequence = new List<NoteType>()
    {
        NoteType.TypeA,
        NoteType.TypeB,
        NoteType.TypeC,
        NoteType.TypeD
    };
    
    [Header("Lane Specific Setup")]
    [Tooltip("Index lane yang akan diedit (0-based)")]
    public int selectedLaneIndex = 0;
    
    [Tooltip("Note type untuk semua note di lane terpilih")]
    public NoteType singleNoteType = NoteType.TypeA;
    
    [Tooltip("Apply single note type ke lane terpilih")]
    public bool applySingleType = false;

    private void OnValidate()
    {
        if (targetChart == null) return;
        
        if (fillWithRandomTypes)
        {
            fillWithRandomTypes = false;
            FillWithRandomTypes();
        }
        
        if (clearAllNoteTypes)
        {
            clearAllNoteTypes = false;
            ClearAllNoteTypes();
        }
        
        if (generatePatternByBeat)
        {
            generatePatternByBeat = false;
            GeneratePatternByBeat();
        }
        
        if (applySingleType)
        {
            applySingleType = false;
            ApplySingleTypeToLane();
        }
    }
    
    [ContextMenu("Fill with Random Note Types")]
    public void FillWithRandomTypes()
    {
        if (targetChart == null)
        {
            Debug.LogError("Target chart is not assigned!");
            return;
        }
        
        NoteType[] availableTypes = { NoteType.TypeA, NoteType.TypeB, NoteType.TypeC, NoteType.TypeD };
        
        foreach (var laneData in targetChart.lanes)
        {
            laneData.noteTypes.Clear();
            
            for (int i = 0; i < laneData.recordedBeats.Count; i++)
            {
                NoteType randomType = availableTypes[Random.Range(0, availableTypes.Length)];
                laneData.noteTypes.Add(randomType);
            }
            
            Debug.Log($"Lane {laneData.laneName}: Filled with {laneData.noteTypes.Count} random note types");
        }
        
        MarkChartDirty();
        Debug.Log("All lanes filled with random note types!");
    }
    
    [ContextMenu("Clear All Note Types")]
    public void ClearAllNoteTypes()
    {
        if (targetChart == null)
        {
            Debug.LogError("Target chart is not assigned!");
            return;
        }
        
        foreach (var laneData in targetChart.lanes)
        {
            laneData.noteTypes.Clear();
            Debug.Log($"Lane {laneData.laneName}: Note types cleared");
        }
        
        MarkChartDirty();
        Debug.Log("All note types cleared!");
    }
    
    [ContextMenu("Generate Pattern by Beat")]
    public void GeneratePatternByBeat()
    {
        if (targetChart == null || patternSequence.Count == 0)
        {
            Debug.LogError("Target chart not assigned or pattern sequence is empty!");
            return;
        }
        
        foreach (var laneData in targetChart.lanes)
        {
            laneData.noteTypes.Clear();
            
            for (int i = 0; i < laneData.recordedBeats.Count; i++)
            {
                NoteType patternType = patternSequence[i % patternSequence.Count];
                laneData.noteTypes.Add(patternType);
            }
            
            Debug.Log($"Lane {laneData.laneName}: Applied pattern to {laneData.noteTypes.Count} notes");
        }
        
        MarkChartDirty();
        Debug.Log("Pattern applied to all lanes!");
    }
    
    [ContextMenu("Apply Single Type to Selected Lane")]
    public void ApplySingleTypeToLane()
    {
        if (targetChart == null)
        {
            Debug.LogError("Target chart is not assigned!");
            return;
        }
        
        if (selectedLaneIndex < 0 || selectedLaneIndex >= targetChart.lanes.Count)
        {
            Debug.LogError($"Invalid lane index: {selectedLaneIndex}. Valid range: 0-{targetChart.lanes.Count - 1}");
            return;
        }
        
        var laneData = targetChart.lanes[selectedLaneIndex];
        laneData.noteTypes.Clear();
        
        for (int i = 0; i < laneData.recordedBeats.Count; i++)
        {
            laneData.noteTypes.Add(singleNoteType);
        }
        
        MarkChartDirty();
        Debug.Log($"Lane {laneData.laneName}: All {laneData.noteTypes.Count} notes set to {singleNoteType}");
    }
    
    /// <summary>
    /// Set note type untuk beat tertentu di lane tertentu
    /// </summary>
    public void SetNoteTypeForBeat(int laneIndex, int beatIndex, NoteType noteType)
    {
        if (targetChart == null || laneIndex < 0 || laneIndex >= targetChart.lanes.Count)
        {
            Debug.LogError("Invalid parameters or chart not assigned!");
            return;
        }
        
        var laneData = targetChart.lanes[laneIndex];
        
        // Extend list jika perlu
        while (laneData.noteTypes.Count <= beatIndex)
        {
            laneData.noteTypes.Add(NoteType.Default);
        }
        
        laneData.noteTypes[beatIndex] = noteType;
        MarkChartDirty();
    }
    
    /// <summary>
    /// Dapatkan note type untuk beat tertentu di lane tertentu
    /// </summary>
    public NoteType GetNoteTypeForBeat(int laneIndex, int beatIndex)
    {
        if (targetChart == null || laneIndex < 0 || laneIndex >= targetChart.lanes.Count)
        {
            return NoteType.Default;
        }
        
        var laneData = targetChart.lanes[laneIndex];
        
        if (beatIndex >= 0 && beatIndex < laneData.noteTypes.Count)
        {
            return laneData.noteTypes[beatIndex];
        }
        
        return NoteType.Default;
    }
    
    /// <summary>
    /// Print informasi note types untuk debugging
    /// </summary>
    [ContextMenu("Print Note Types Info")]
    public void PrintNoteTypesInfo()
    {
        if (targetChart == null)
        {
            Debug.LogError("Target chart is not assigned!");
            return;
        }
        
        Debug.Log($"=== Note Types Info for {targetChart.name} ===");
        
        for (int i = 0; i < targetChart.lanes.Count; i++)
        {
            var laneData = targetChart.lanes[i];
            Debug.Log($"Lane {i} ({laneData.laneName}):");
            Debug.Log($"  Beats: {laneData.recordedBeats.Count}");
            Debug.Log($"  Note Types: {laneData.noteTypes.Count}");
            
            if (laneData.noteTypes.Count > 0)
            {
                var typeCounts = new Dictionary<NoteType, int>();
                foreach (var type in laneData.noteTypes)
                {
                    if (!typeCounts.ContainsKey(type))
                        typeCounts[type] = 0;
                    typeCounts[type]++;
                }
                
                foreach (var kvp in typeCounts)
                {
                    Debug.Log($"    {kvp.Key}: {kvp.Value} notes");
                }
            }
        }
    }
    
    /// <summary>
    /// Mark chart as dirty untuk save
    /// </summary>
    private void MarkChartDirty()
    {
        #if UNITY_EDITOR
        if (targetChart != null)
        {
            UnityEditor.EditorUtility.SetDirty(targetChart);
        }
        #endif
    }
    
    /// <summary>
    /// Generate note types berdasarkan posisi lane (untuk demo)
    /// </summary>
    [ContextMenu("Generate Demo Pattern")]
    public void GenerateDemoPattern()
    {
        if (targetChart == null)
        {
            Debug.LogError("Target chart is not assigned!");
            return;
        }
        
        for (int laneIndex = 0; laneIndex < targetChart.lanes.Count; laneIndex++)
        {
            var laneData = targetChart.lanes[laneIndex];
            laneData.noteTypes.Clear();
            
            for (int beatIndex = 0; beatIndex < laneData.recordedBeats.Count; beatIndex++)
            {
                // Pattern demo: setiap lane bergantian type berdasarkan beat
                NoteType demoType = (NoteType)((laneIndex + beatIndex) % 4);
                laneData.noteTypes.Add(demoType);
            }
        }
        
        MarkChartDirty();
        Debug.Log("Demo pattern generated - each lane will have different note types per beat!");
    }
}
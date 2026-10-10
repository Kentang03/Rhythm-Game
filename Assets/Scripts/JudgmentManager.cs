using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manager untuk mengatur sistem judgment point secara keseluruhan.
/// Memungkinkan switching antara mode per-lane dan central judgment.
/// </summary>
public class JudgmentManager : MonoBehaviour
{
    [Header("Judgment System Configuration")]
    [Tooltip("Mode judgment yang akan digunakan")]
    public JudgmentMode systemJudgmentMode = JudgmentMode.PerLane;
    
    [Header("Central Judgment Setup")]
    [Tooltip("Prefab untuk central judgment point")]
    public GameObject centralJudgmentPrefab;
    
    [Tooltip("Posisi central judgment point (jika menggunakan central mode)")]
    public Transform centralJudgmentPosition;
    
    [Tooltip("Key mappings untuk central judgment mode")]
    public List<NoteKeyMapping> centralKeyMappings = new List<NoteKeyMapping>()
    {
        new NoteKeyMapping { noteType = NoteType.TypeA, inputKey = UnityEngine.InputSystem.Key.A, description = "Red Note - Key A" },
        new NoteKeyMapping { noteType = NoteType.TypeB, inputKey = UnityEngine.InputSystem.Key.S, description = "Blue Note - Key S" },
        new NoteKeyMapping { noteType = NoteType.TypeC, inputKey = UnityEngine.InputSystem.Key.D, description = "Green Note - Key D" },
        new NoteKeyMapping { noteType = NoteType.TypeD, inputKey = UnityEngine.InputSystem.Key.F, description = "Yellow Note - Key F" }
    };
    
    [Header("Visual Prefabs")]
    [Tooltip("Prefab untuk note type A (merah)")]
    public GameObject noteTypeAPrefab;
    
    [Tooltip("Prefab untuk note type B (biru)")]
    public GameObject noteTypeBPrefab;
    
    [Tooltip("Prefab untuk note type C (hijau)")]  
    public GameObject noteTypeCPrefab;
    
    [Tooltip("Prefab untuk note type D (kuning)")]
    public GameObject noteTypeDPrefab;
    
    private GameObject centralJudgmentInstance;
    private JudgmentPoint[] perLaneJudgmentPoints;
    
    public static JudgmentManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple JudgmentManager instances detected!");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeJudgmentSystem();
    }

    /// <summary>
    /// Initialize sistem judgment berdasarkan mode yang dipilih
    /// </summary>
    public void InitializeJudgmentSystem()
    {
        // Bersihkan setup sebelumnya
        CleanupJudgmentSystem();
        
        switch (systemJudgmentMode)
        {
            case JudgmentMode.PerLane:
                SetupPerLaneJudgment();
                break;
            case JudgmentMode.Central:
                SetupCentralJudgment();
                break;
        }
        
        Debug.Log($"JudgmentManager: Initialized {systemJudgmentMode} judgment system");
    }
    
    /// <summary>
    /// Setup judgment point per-lane (sistem existing)
    /// </summary>
    private void SetupPerLaneJudgment()
    {
        // Disable central judgment jika ada
        if (centralJudgmentInstance != null)
        {
            centralJudgmentInstance.SetActive(false);
        }
        
        // Enable semua per-lane judgment points
        perLaneJudgmentPoints = FindObjectsByType<JudgmentPoint>(FindObjectsSortMode.None);
        foreach (var jp in perLaneJudgmentPoints)
        {
            if (jp.judgmentMode == JudgmentMode.PerLane)
            {
                jp.gameObject.SetActive(true);
            }
        }
        
        // Update LaneController untuk menggunakan judgment points
        UpdateLaneControllersForPerLane();
    }
    
    /// <summary>
    /// Setup central judgment point
    /// </summary>
    private void SetupCentralJudgment()
    {
        // Disable per-lane judgment points
        perLaneJudgmentPoints = FindObjectsByType<JudgmentPoint>(FindObjectsSortMode.None);
        foreach (var jp in perLaneJudgmentPoints)
        {
            if (jp.judgmentMode == JudgmentMode.PerLane)
            {
                jp.gameObject.SetActive(false);
            }
        }
        
        // Create atau enable central judgment point
        if (centralJudgmentInstance == null && centralJudgmentPrefab != null)
        {
            Vector3 spawnPos = centralJudgmentPosition != null ? 
                centralJudgmentPosition.position : Vector3.zero;
            
            centralJudgmentInstance = Instantiate(centralJudgmentPrefab, spawnPos, Quaternion.identity);
            
            // Setup key mappings
            JudgmentPoint centralJP = centralJudgmentInstance.GetComponent<JudgmentPoint>();
            if (centralJP != null)
            {
                centralJP.judgmentMode = JudgmentMode.Central;
                centralJP.noteKeyMappings = new List<NoteKeyMapping>(centralKeyMappings);
            }
        }
        else if (centralJudgmentInstance != null)
        {
            centralJudgmentInstance.SetActive(true);
        }
        
        // Update LaneController untuk menggunakan central judgment
        UpdateLaneControllersForCentral();
    }
    
    /// <summary>
    /// Update LaneController untuk mode per-lane
    /// </summary>
    private void UpdateLaneControllersForPerLane()
    {
        LaneController[] laneControllers = FindObjectsByType<LaneController>(FindObjectsSortMode.None);
        
        foreach (var lc in laneControllers)
        {
            // Re-enable input handling pada LaneController
            lc.enabled = true;
        }
    }
    
    /// <summary>
    /// Update LaneController untuk mode central
    /// </summary>
    private void UpdateLaneControllersForCentral()
    {
        LaneController[] laneControllers = FindObjectsByType<LaneController>(FindObjectsSortMode.None);
        
        foreach (var lc in laneControllers)
        {
            // Disable input handling pada LaneController (judgment point yang handle)
            // Tapi tetap aktif untuk spawning note
            // Kita perlu modify LaneController untuk support ini
        }
    }
    
    /// <summary>
    /// Cleanup sistem judgment sebelumnya
    /// </summary>
    private void CleanupJudgmentSystem()
    {
        // Bersihkan instance yang ada jika perlu
        if (centralJudgmentInstance != null && systemJudgmentMode == JudgmentMode.PerLane)
        {
            centralJudgmentInstance.SetActive(false);
        }
    }
    
    /// <summary>
    /// Switch judgment mode runtime
    /// </summary>
    public void SwitchJudgmentMode(JudgmentMode newMode)
    {
        if (systemJudgmentMode != newMode)
        {
            systemJudgmentMode = newMode;
            InitializeJudgmentSystem();
        }
    }
    
    /// <summary>
    /// Dapatkan prefab note berdasarkan type
    /// </summary>
    public GameObject GetNotePrefabForType(NoteType noteType)
    {
        switch (noteType)
        {
            case NoteType.TypeA: return noteTypeAPrefab;
            case NoteType.TypeB: return noteTypeBPrefab;
            case NoteType.TypeC: return noteTypeCPrefab;
            case NoteType.TypeD: return noteTypeDPrefab;
            default: return null; // Fallback ke prefab default dari LaneController
        }
    }
    
    /// <summary>
    /// Dapatkan key yang sesuai untuk note type (untuk central mode)
    /// </summary>
    public UnityEngine.InputSystem.Key GetKeyForNoteType(NoteType noteType)
    {
        foreach (var mapping in centralKeyMappings)
        {
            if (mapping.noteType == noteType)
            {
                return mapping.inputKey;
            }
        }
        return UnityEngine.InputSystem.Key.None;
    }
}
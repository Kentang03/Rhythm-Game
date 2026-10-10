using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Utility script untuk memudahkan setup dan testing sistem judgment
/// </summary>
public class JudgmentSystemSetup : MonoBehaviour
{
    [Header("Quick Setup")]
    [Tooltip("Tekan untuk setup central judgment mode")]
    public bool setupCentralMode = false;
    
    [Tooltip("Tekan untuk setup per-lane mode")]
    public bool setupPerLaneMode = false;
    
    [Header("Runtime Controls")]
    [Tooltip("Key untuk switch ke central mode")]
    public Key switchToCentralKey = Key.F1;
    
    [Tooltip("Key untuk switch ke per-lane mode")]
    public Key switchToPerLaneKey = Key.F2;

    private void Update()
    {
        if (Keyboard.current == null) return;
        
        // Runtime switching
        if (Keyboard.current[switchToCentralKey].wasPressedThisFrame)
        {
            SwitchToCentralMode();
        }
        
        if (Keyboard.current[switchToPerLaneKey].wasPressedThisFrame)
        {
            SwitchToPerLaneMode();
        }
    }

    private void OnValidate()
    {
        // Setup otomatis di editor
        if (setupCentralMode)
        {
            setupCentralMode = false;
            SetupCentralJudgmentMode();
        }
        
        if (setupPerLaneMode)
        {
            setupPerLaneMode = false;
            SetupPerLaneJudgmentMode();
        }
    }
    
    [ContextMenu("Setup Central Judgment Mode")]
    public void SetupCentralJudgmentMode()
    {
        Debug.Log("Setting up Central Judgment Mode...");
        
        // Cari atau buat JudgmentManager
        JudgmentManager manager = FindAnyObjectByType<JudgmentManager>();
        if (manager == null)
        {
            GameObject managerObj = new GameObject("JudgmentManager");
            manager = managerObj.AddComponent<JudgmentManager>();
        }
        
        manager.systemJudgmentMode = JudgmentMode.Central;
        
        // Update semua LaneController untuk menggunakan central judgment
        LaneController[] laneControllers = FindObjectsByType<LaneController>(FindObjectsSortMode.None);
        for (int i = 0; i < laneControllers.Length; i++)
        {
            laneControllers[i].useCentralJudgment = true;
            
            // Assign note type berdasarkan index lane
            switch (i % 4)
            {
                case 0: laneControllers[i].laneNoteType = NoteType.TypeA; break;
                case 1: laneControllers[i].laneNoteType = NoteType.TypeB; break;
                case 2: laneControllers[i].laneNoteType = NoteType.TypeC; break;
                case 3: laneControllers[i].laneNoteType = NoteType.TypeD; break;
            }
        }
        
        // Disable existing per-lane judgment points
        JudgmentPoint[] judgmentPoints = FindObjectsByType<JudgmentPoint>(FindObjectsSortMode.None);
        foreach (var jp in judgmentPoints)
        {
            if (jp.judgmentMode == JudgmentMode.PerLane)
            {
                jp.gameObject.SetActive(false);
            }
        }
        
        Debug.Log("Central Judgment Mode setup completed!");
        PrintSetupInfo();
    }
    
    [ContextMenu("Setup Per-Lane Judgment Mode")]
    public void SetupPerLaneJudgmentMode()
    {
        Debug.Log("Setting up Per-Lane Judgment Mode...");
        
        // Cari atau buat JudgmentManager
        JudgmentManager manager = FindAnyObjectByType<JudgmentManager>();
        if (manager == null)
        {
            GameObject managerObj = new GameObject("JudgmentManager");
            manager = managerObj.AddComponent<JudgmentManager>();
        }
        
        manager.systemJudgmentMode = JudgmentMode.PerLane;
        
        // Update semua LaneController untuk menggunakan per-lane judgment
        LaneController[] laneControllers = FindObjectsByType<LaneController>(FindObjectsSortMode.None);
        foreach (var lc in laneControllers)
        {
            lc.useCentralJudgment = false;
            
            // Buat judgment point untuk lane ini jika belum ada
            EnsureLaneHasJudgmentPoint(lc);
        }
        
        Debug.Log("Per-Lane Judgment Mode setup completed!");
        PrintSetupInfo();
    }
    
    /// <summary>
    /// Pastikan lane memiliki judgment point
    /// </summary>
    private void EnsureLaneHasJudgmentPoint(LaneController laneController)
    {
        // Cek apakah sudah ada judgment point di lane ini
        JudgmentPoint existingJP = laneController.GetComponentInChildren<JudgmentPoint>();
        
        if (existingJP == null)
        {
            // Buat judgment point baru
            GameObject jpObj = new GameObject($"JudgmentPoint_{laneController.name}");
            jpObj.transform.SetParent(laneController.transform);
            
            if (laneController.hitPoint != null)
            {
                jpObj.transform.position = laneController.hitPoint.position;
            }
            
            JudgmentPoint jp = jpObj.AddComponent<JudgmentPoint>();
            jp.judgmentMode = JudgmentMode.PerLane;
            jp.assignedKey = laneController.inputKey;
            jp.hitPointTransform = laneController.hitPoint;
        }
    }
    
    /// <summary>
    /// Runtime switch ke central mode
    /// </summary>
    public void SwitchToCentralMode()
    {
        JudgmentManager manager = FindAnyObjectByType<JudgmentManager>();
        if (manager != null)
        {
            manager.SwitchJudgmentMode(JudgmentMode.Central);
            Debug.Log("Switched to Central Judgment Mode");
        }
    }
    
    /// <summary>
    /// Runtime switch ke per-lane mode
    /// </summary>
    public void SwitchToPerLaneMode()
    {
        JudgmentManager manager = FindAnyObjectByType<JudgmentManager>();
        if (manager != null)
        {
            manager.SwitchJudgmentMode(JudgmentMode.PerLane);
            Debug.Log("Switched to Per-Lane Judgment Mode");
        }
    }
    
    /// <summary>
    /// Print informasi setup untuk debugging
    /// </summary>
    private void PrintSetupInfo()
    {
        JudgmentManager manager = FindAnyObjectByType<JudgmentManager>();
        if (manager != null)
        {
            Debug.Log($"Current Judgment Mode: {manager.systemJudgmentMode}");
        }
        
        LaneController[] laneControllers = FindObjectsByType<LaneController>(FindObjectsSortMode.None);
        Debug.Log($"Found {laneControllers.Length} LaneControllers");
        
        foreach (var lc in laneControllers)
        {
            Debug.Log($"Lane {lc.name}: Central={lc.useCentralJudgment}, NoteType={lc.laneNoteType}, InputKey={lc.inputKey}");
        }
        
        JudgmentPoint[] judgmentPoints = FindObjectsByType<JudgmentPoint>(FindObjectsSortMode.None);
        Debug.Log($"Found {judgmentPoints.Length} JudgmentPoints");
        
        foreach (var jp in judgmentPoints)
        {
            Debug.Log($"JudgmentPoint {jp.name}: Mode={jp.judgmentMode}, Active={jp.gameObject.activeSelf}");
        }
        
        Debug.Log("=== Controls ===");
        Debug.Log($"Press {switchToCentralKey} to switch to Central Mode");
        Debug.Log($"Press {switchToPerLaneKey} to switch to Per-Lane Mode");
        Debug.Log("=== Note Type Mapping (Central Mode) ===");
        Debug.Log("TypeA (Red) - Key A");
        Debug.Log("TypeB (Blue) - Key S"); 
        Debug.Log("TypeC (Green) - Key D");
        Debug.Log("TypeD (Yellow) - Key F");
    }
}
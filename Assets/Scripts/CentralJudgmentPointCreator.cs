using UnityEngine;

/// <summary>
/// Utility untuk membuat central judgment point prefab di editor
/// </summary>
public class CentralJudgmentPointCreator : MonoBehaviour
{
    [Header("Central Judgment Point Creator")]
    [Tooltip("Klik untuk membuat central judgment point")]
    public bool createCentralJudgmentPoint = false;
    
    [Tooltip("Posisi central judgment point")]
    public Vector3 centralPosition = Vector3.zero;

    private void OnValidate()
    {
        if (createCentralJudgmentPoint)
        {
            createCentralJudgmentPoint = false;
            CreateCentralJudgmentPoint();
        }
    }
    
    [ContextMenu("Create Central Judgment Point")]
    public void CreateCentralJudgmentPoint()
    {
        // Cek apakah sudah ada central judgment point
        JudgmentPoint existingCentral = FindAnyObjectByType<JudgmentPoint>();
        if (existingCentral != null && existingCentral.judgmentMode == JudgmentMode.Central)
        {
            Debug.LogWarning("Central Judgment Point already exists!");
            return;
        }
        
        // Buat GameObject untuk central judgment point
        GameObject centralJP = new GameObject("CentralJudgmentPoint");
        centralJP.transform.position = centralPosition;
        
        // Tambahkan JudgmentPoint component
        JudgmentPoint judgmentPoint = centralJP.AddComponent<JudgmentPoint>();
        judgmentPoint.judgmentMode = JudgmentMode.Central;
        
        // Setup key mappings
        judgmentPoint.noteKeyMappings = new System.Collections.Generic.List<NoteKeyMapping>()
        {
            new NoteKeyMapping { 
                noteType = NoteType.TypeA, 
                inputKey = UnityEngine.InputSystem.Key.A, 
                description = "Red Note - Key A" 
            },
            new NoteKeyMapping { 
                noteType = NoteType.TypeB, 
                inputKey = UnityEngine.InputSystem.Key.S, 
                description = "Blue Note - Key S" 
            },
            new NoteKeyMapping { 
                noteType = NoteType.TypeC, 
                inputKey = UnityEngine.InputSystem.Key.D, 
                description = "Green Note - Key D" 
            },
            new NoteKeyMapping { 
                noteType = NoteType.TypeD, 
                inputKey = UnityEngine.InputSystem.Key.F, 
                description = "Yellow Note - Key F" 
            }
        };
        
        // Buat visual indicator (opsional)
        CreateVisualIndicator(centralJP);
        
        Debug.Log("Central Judgment Point created successfully!");
        Debug.Log("Key Mappings:");
        Debug.Log("- Red (TypeA): Key A");
        Debug.Log("- Blue (TypeB): Key S");
        Debug.Log("- Green (TypeC): Key D");
        Debug.Log("- Yellow (TypeD): Key F");
    }
    
    /// <summary>
    /// Buat visual indicator untuk central judgment point
    /// </summary>
    private void CreateVisualIndicator(GameObject parent)
    {
        // Buat cube sebagai visual indicator
        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        indicator.name = "JudgmentIndicator";
        indicator.transform.SetParent(parent.transform);
        indicator.transform.localPosition = Vector3.zero;
        indicator.transform.localScale = Vector3.one * 0.5f;
        
        // Buat material khusus untuk indicator
        Renderer renderer = indicator.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material indicatorMat = new Material(Shader.Find("Standard"));
            indicatorMat.color = Color.yellow;
            indicatorMat.SetFloat("_Metallic", 0.5f);
            indicatorMat.SetFloat("_Smoothness", 0.8f);
            renderer.material = indicatorMat;
        }
        
        // Tambahkan hit point transform reference
        JudgmentPoint jp = parent.GetComponent<JudgmentPoint>();
        if (jp != null)
        {
            jp.hitPointTransform = indicator.transform;
        }
    }
}
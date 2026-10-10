using UnityEngine;

/// <summary>
/// Utility untuk setup NoteArcConnector di scene dengan mudah
/// </summary>
public class NoteArcConnectorSetup : MonoBehaviour
{
    [Header("Arc Connector Setup")]
    [Tooltip("Klik untuk membuat NoteArcConnector di scene")]
    public bool createArcConnector = false;
    
    [Header("Arc Visual Configuration")]
    [Tooltip("Material untuk arc (opsional, akan dibuat otomatis jika kosong)")]
    public Material customArcMaterial;
    
    [Tooltip("Lebar garis arc")]
    [Range(0.01f, 1.0f)]
    public float arcWidth = 0.15f;
    
    [Tooltip("Jumlah segment untuk busur (semakin banyak semakin halus)")]
    [Range(10, 100)]
    public int arcSegments = 40;
    
    [Tooltip("Tinggi busur dari garis lurus")]
    [Range(0f, 10f)]
    public float arcHeight = 3f;
    
    [Tooltip("Tolerance waktu untuk note bersamaan (dalam beats)")]
    [Range(0.001f, 0.1f)]
    public float beatTolerance = 0.02f;
    
    [Header("Glow Configuration")]
    [Tooltip("Enable glow effect")]
    public bool enableGlow = true;
    
    [Tooltip("Intensitas cahaya glow (lebih tinggi = lebih terang)")]
    [Range(0f, 20f)]
    public float glowIntensity = 10f;
    
    [Tooltip("Transparansi arc")]
    [Range(0f, 1f)]
    public float arcAlpha = 0.9f;

    private void OnValidate()
    {
        if (createArcConnector)
        {
            createArcConnector = false;
            CreateArcConnectorInScene();
        }
    }
    
    [ContextMenu("Create Arc Connector")]
    public void CreateArcConnectorInScene()
    {
        // Cek apakah sudah ada NoteArcConnector
        NoteArcConnector existing = FindAnyObjectByType<NoteArcConnector>();
        if (existing != null)
        {
            Debug.LogWarning("NoteArcConnector already exists in scene!");
            return;
        }
        
        // Buat GameObject untuk arc connector
        GameObject arcConnectorObj = new GameObject("NoteArcConnector");
        NoteArcConnector connector = arcConnectorObj.AddComponent<NoteArcConnector>();
        
        // Apply settings
        connector.arcMaterial = customArcMaterial;
        connector.arcWidth = arcWidth;
        connector.arcSegments = arcSegments;
        connector.arcHeight = arcHeight;
        connector.beatTolerance = beatTolerance;
        connector.enableGlow = enableGlow;
        connector.glowIntensity = glowIntensity;
        connector.arcAlpha = arcAlpha;
        
        Debug.Log("NoteArcConnector created successfully with Glow Effect!");
        Debug.Log("Arc connector akan otomatis mendeteksi dan menghubungkan note dengan:");
        Debug.Log("- Waktu spawn yang sama (dalam tolerance)");
        Debug.Log("- Warna/Note Type yang sama");
        Debug.Log("- Berada di lane yang berbeda");
        Debug.Log($"- Glow Effect: {(enableGlow ? "ENABLED" : "DISABLED")}");
    }
}

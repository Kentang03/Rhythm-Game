using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sistem untuk membuat garis lengkung busur (arc) yang menghubungkan 2 note
/// yang keluar di waktu yang sama dengan warna/keycode yang sama di lane berbeda
/// </summary>
public class NoteArcConnector : MonoBehaviour
{
    [Header("Arc Visual Settings")]
    [Tooltip("Material untuk arc line (akan dibuat otomatis dengan glow effect jika kosong)")]
    public Material arcMaterial;
    
    [Tooltip("Lebar garis arc")]
    [Range(0.01f, 1.0f)]
    public float arcWidth = 0.1f;
    
    [Tooltip("Jumlah segment untuk membuat busur (semakin banyak semakin halus)")]
    [Range(10, 100)]
    public int arcSegments = 30;
    
    [Tooltip("Tinggi arc dari garis lurus (0 = garis lurus, nilai positif = busur ke atas)")]
    [Range(0f, 10f)]
    public float arcHeight = 2f;
    
    [Header("Glow Settings")]
    [Tooltip("Enable glow effect pada arc")]
    public bool enableGlow = true;
    
    [Tooltip("Intensitas glow (brightness) - nilai lebih tinggi = lebih terang")]
    [Range(0f, 20f)]
    public float glowIntensity = 8f;
    
    [Tooltip("Transparansi arc")]
    [Range(0f, 1f)]
    public float arcAlpha = 0.9f;
    
    [Header("Arc Detection Settings")]
    [Tooltip("Tolerance waktu untuk menganggap 2 note 'bersamaan' (dalam beats)")]
    [Range(0.001f, 0.1f)]
    public float beatTolerance = 0.01f;
    
    [Tooltip("Central judgment point untuk menentukan arah arc keluar (opsional, akan auto-detect jika kosong)")]
    public Transform centralJudgmentPoint;
    
    [Header("Runtime Info")]
    [Tooltip("List arc yang aktif saat ini")]
    public List<ArcConnection> activeArcs = new List<ArcConnection>();
    
    // Singleton
    public static NoteArcConnector Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("NoteArcConnector: Multiple instances detected!");
            Destroy(gameObject);
        }
        
        // Create default material with glow if not assigned
        if (arcMaterial == null)
        {
            CreateGlowMaterial();
        }
        
        // Auto-detect central judgment point jika tidak di-assign
        if (centralJudgmentPoint == null)
        {
            JudgmentPoint[] judgmentPoints = FindObjectsByType<JudgmentPoint>(FindObjectsSortMode.None);
            foreach (var jp in judgmentPoints)
            {
                if (jp.judgmentMode == JudgmentMode.Central)
                {
                    centralJudgmentPoint = jp.transform;
                    Debug.Log($"Auto-detected central judgment point: {jp.name}");
                    break;
                }
            }
        }
    }
    
    private void Update()
    {
        // Update semua arc yang aktif
        for (int i = activeArcs.Count - 1; i >= 0; i--)
        {
            var arc = activeArcs[i];
            
            // Cek apakah kedua note masih ada
            if (arc.note1 == null || arc.note2 == null || 
                !arc.note1.gameObject.activeSelf || !arc.note2.gameObject.activeSelf)
            {
                // Salah satu note sudah hilang, hapus arc
                if (arc.arcObject != null)
                {
                    Destroy(arc.arcObject);
                }
                activeArcs.RemoveAt(i);
                continue;
            }
            
            // Update posisi arc mengikuti note
            UpdateArcVisual(arc);
        }
    }
    
    /// <summary>
    /// Deteksi dan buat arc untuk note yang baru spawn
    /// </summary>
    public void CheckAndCreateArc(NoteController newNote)
    {
        if (newNote == null) return;
        
        // Cari semua note aktif lainnya
        NoteController[] allNotes = FindObjectsByType<NoteController>(FindObjectsSortMode.None);
        
        foreach (var otherNote in allNotes)
        {
            // Skip note yang sama
            if (otherNote == newNote) continue;
            
            // Skip note yang tidak aktif
            if (!otherNote.gameObject.activeSelf) continue;
            
            // Cek apakah sudah ada arc untuk kombinasi note ini
            if (ArcExists(newNote, otherNote)) continue;
            
            // Cek apakah note memenuhi kriteria untuk arc:
            // 1. Waktu spawn sama (dalam tolerance)
            // 2. Note type sama (warna/keycode sama)
            // 3. Beda instance (sudah dicek di atas)
            
            float beatDiff = Mathf.Abs(newNote.targetBeat - otherNote.targetBeat);
            
            if (beatDiff <= beatTolerance && newNote.noteType == otherNote.noteType)
            {
                // Kedua note memenuhi kriteria, buat arc!
                CreateArc(newNote, otherNote);
            }
        }
    }
    
    /// <summary>
    /// Cek apakah arc sudah ada untuk 2 note ini
    /// </summary>
    private bool ArcExists(NoteController note1, NoteController note2)
    {
        foreach (var arc in activeArcs)
        {
            if ((arc.note1 == note1 && arc.note2 == note2) ||
                (arc.note1 == note2 && arc.note2 == note1))
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Create atau update glow material
    /// </summary>
    private void CreateGlowMaterial()
    {
        Shader glowShader = Shader.Find("Universal Render Pipeline/ArcLineGlow");
        
        if (glowShader != null && enableGlow)
        {
            arcMaterial = new Material(glowShader);
            arcMaterial.SetColor("_BaseColor", Color.white);
            arcMaterial.SetColor("_EmissionColor", Color.white);
            arcMaterial.SetFloat("_EmissionStrength", glowIntensity);
            Debug.Log("✓ Arc Glow Material created for URP!");
        }
        else
        {
            Debug.LogWarning("URP Glow shader not found! Creating fallback material...");
            // Fallback: Use Unlit shader
            arcMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            arcMaterial.SetFloat("_Surface", 1); // Transparent
            arcMaterial.SetFloat("_Blend", 0); // Alpha blend
            arcMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            arcMaterial.renderQueue = 3000;
        }
    }
    
    /// <summary>
    /// Update glow material properties
    /// </summary>
    private void UpdateGlowMaterial(Material material, Color noteColor)
    {
        if (material == null) return;
        
        if (material.shader.name == "Universal Render Pipeline/ArcLineGlow")
        {
            // URP custom glow shader
            material.SetColor("_BaseColor", noteColor);
            material.SetColor("_EmissionColor", noteColor);
            material.SetFloat("_EmissionStrength", glowIntensity);
        }
        else
        {
            // Fallback
            material.color = noteColor;
        }
    }
    
    /// <summary>
    /// Buat arc connection antara 2 note
    /// </summary>
    private void CreateArc(NoteController note1, NoteController note2)
    {
        // Buat GameObject untuk arc
        GameObject arcObj = new GameObject($"Arc_{note1.name}_to_{note2.name}");
        arcObj.transform.SetParent(transform);
        
        // Tambahkan LineRenderer
        LineRenderer lineRenderer = arcObj.AddComponent<LineRenderer>();
        
        // Create instance material untuk arc ini
        Material instanceMaterial = new Material(arcMaterial);
        lineRenderer.material = instanceMaterial;
        
        lineRenderer.startWidth = arcWidth;
        lineRenderer.endWidth = arcWidth;
        lineRenderer.positionCount = arcSegments;
        lineRenderer.useWorldSpace = true;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.alignment = LineAlignment.View; // Always face camera
        
        // Set color based on note type
        Color arcColor = GetColorForNoteType(note1.noteType);
        
        // Buat warna lebih bright untuk glow
        Color brightColor = arcColor * (1f + glowIntensity * 0.3f);
        
        // Update glow material dengan warna note
        UpdateGlowMaterial(instanceMaterial, brightColor);
        
        // Set LineRenderer colors (fallback untuk non-glow shader)
        lineRenderer.startColor = brightColor;
        lineRenderer.endColor = brightColor;
        
        // Buat arc connection data
        ArcConnection arc = new ArcConnection
        {
            note1 = note1,
            note2 = note2,
            arcObject = arcObj,
            lineRenderer = lineRenderer
        };
        
        activeArcs.Add(arc);
        
        // Update visual pertama kali
        UpdateArcVisual(arc);
        
        Debug.Log($"Arc created: {note1.noteType} note at beat {note1.targetBeat:F2}");
    }
    
    /// <summary>
    /// Update visual arc mengikuti posisi note
    /// Arc akan melengkung ke LUAR (menjauhi center/judgment point)
    /// </summary>
    private void UpdateArcVisual(ArcConnection arc)
    {
        if (arc.lineRenderer == null) return;
        
        Vector3 pos1 = arc.note1.transform.position;
        Vector3 pos2 = arc.note2.transform.position;
        
        // Hitung titik tengah antara 2 note
        Vector3 midPoint = (pos1 + pos2) / 2f;
        
        // Tentukan arah arc "keluar" berdasarkan posisi relative terhadap center
        Vector3 outwardDirection;
        
        if (centralJudgmentPoint != null)
        {
            // Hitung vektor dari center ke midpoint
            // Arc akan melengkung ke arah ini (menjauhi center)
            outwardDirection = (midPoint - centralJudgmentPoint.position).normalized;
        }
        else
        {
            // Fallback: gunakan perpendicular dari garis note1-note2
            Vector3 direction = (pos2 - pos1);
            
            // Cross product dengan forward untuk dapat perpendicular
            outwardDirection = Vector3.Cross(direction, Vector3.forward).normalized;
            
            // Jika hasilnya terlalu kecil, gunakan cross dengan up
            if (outwardDirection.magnitude < 0.01f)
            {
                outwardDirection = Vector3.Cross(direction, Vector3.up).normalized;
            }
            
            // Pilih arah yang menjauhi origin (0,0,0)
            if (Vector3.Dot(outwardDirection, midPoint) < 0)
            {
                outwardDirection = -outwardDirection;
            }
        }
        
        // Generate arc points menggunakan quadratic bezier curve
        for (int i = 0; i < arcSegments; i++)
        {
            float t = i / (float)(arcSegments - 1);
            
            // Quadratic bezier: P(t) = (1-t)²P0 + 2(1-t)tP1 + t²P2
            // Control point di midPoint + offset ke arah luar
            Vector3 controlPoint = midPoint + outwardDirection * arcHeight;
            
            float oneMinusT = 1f - t;
            Vector3 point = oneMinusT * oneMinusT * pos1 +
                          2f * oneMinusT * t * controlPoint +
                          t * t * pos2;
            
            arc.lineRenderer.SetPosition(i, point);
        }
    }
    
    /// <summary>
    /// Dapatkan warna untuk note type
    /// </summary>
    private Color GetColorForNoteType(NoteType noteType)
    {
        switch (noteType)
        {
            case NoteType.TypeA: return new Color(1f, 0f, 0f, 0.7f); // Red
            case NoteType.TypeB: return new Color(0f, 0f, 1f, 0.7f); // Blue
            case NoteType.TypeC: return new Color(0f, 1f, 0f, 0.7f); // Green
            case NoteType.TypeD: return new Color(1f, 1f, 0f, 0.7f); // Yellow
            default: return new Color(1f, 1f, 1f, 0.7f); // White
        }
    }
    
    /// <summary>
    /// Hapus arc secara manual
    /// </summary>
    public void RemoveArc(ArcConnection arc)
    {
        if (activeArcs.Contains(arc))
        {
            if (arc.arcObject != null)
            {
                Destroy(arc.arcObject);
            }
            activeArcs.Remove(arc);
        }
    }
    
    /// <summary>
    /// Hapus semua arc
    /// </summary>
    public void ClearAllArcs()
    {
        foreach (var arc in activeArcs)
        {
            if (arc.arcObject != null)
            {
                Destroy(arc.arcObject);
            }
        }
        activeArcs.Clear();
    }
}

/// <summary>
/// Data struktur untuk menyimpan informasi arc connection
/// </summary>
[System.Serializable]
public class ArcConnection
{
    public NoteController note1;
    public NoteController note2;
    public GameObject arcObject;
    public LineRenderer lineRenderer;
}

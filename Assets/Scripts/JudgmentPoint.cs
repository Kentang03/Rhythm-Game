using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Komponen judgment point yang bisa digunakan untuk single lane atau multi-lane.
/// Mendukung input mapping berdasarkan note type/visual.
/// </summary>
public class JudgmentPoint : MonoBehaviour
{
    [Header("Judgment Point Configuration")]
    [Tooltip("Mode judgment point - PerLane (satu per lane) atau Central (satu untuk semua)")]
    public JudgmentMode judgmentMode = JudgmentMode.PerLane;
    
    [Header("Per-Lane Mode Settings")]
    [Tooltip("Key input untuk lane ini (hanya untuk mode PerLane)")]
    public Key assignedKey = Key.None;
    
    [Header("Central Mode Settings")]
    [Tooltip("Mapping note type ke key input (untuk mode Central)")]
    public List<NoteKeyMapping> noteKeyMappings = new List<NoteKeyMapping>();
    
    [Header("Visual Settings")]
    [Tooltip("Transform untuk menandai posisi hit point")]
    public Transform hitPointTransform;
    
    [Header("Timing Settings")]
    [Tooltip("Window timing untuk perfect hit (dalam beats)")]
    public float perfectWindow = 0.35f;
    
    [Tooltip("Window timing untuk good hit (dalam beats)")]  
    public float goodWindow = 0.7f;
    
    [Tooltip("Window timing untuk bad hit (dalam beats)")]
    public float badWindow = 1.0f;
    
    // Active notes dalam range judgment point ini
    private List<NoteController> activeNotes = new List<NoteController>();
    
    // Static instance untuk central judgment mode
    public static JudgmentPoint CentralInstance { get; private set; }
    
    public Vector3 Position => hitPointTransform != null ? hitPointTransform.position : transform.position;

    private void Awake()
    {
        // Register sebagai central instance jika mode central
        if (judgmentMode == JudgmentMode.Central)
        {
            if (CentralInstance == null)
            {
                CentralInstance = this;
            }
            else
            {
                Debug.LogWarning("Multiple Central Judgment Points detected! Only one should exist.");
            }
        }
    }

    private void Update()
    {
        switch (judgmentMode)
        {
            case JudgmentMode.PerLane:
                HandlePerLaneInput();
                break;
            case JudgmentMode.Central:
                HandleCentralInput();
                break;
        }
        
        CheckMissedNotes();
    }

    /// <summary>
    /// Handle input untuk mode per-lane (sistem existing)
    /// </summary>
    private void HandlePerLaneInput()
    {
        if (Keyboard.current == null || assignedKey == Key.None) return;
        
        if (Keyboard.current[assignedKey].wasPressedThisFrame)
        {
            HandleInput(assignedKey);
        }
        
        if (Keyboard.current[assignedKey].wasReleasedThisFrame)
        {
            HandleRelease(assignedKey);
        }
    }
    
    /// <summary>
    /// Handle input untuk mode central - cek semua mapped keys
    /// </summary>
    private void HandleCentralInput()
    {
        if (Keyboard.current == null) return;
        
        foreach (var mapping in noteKeyMappings)
        {
            if (Keyboard.current[mapping.inputKey].wasPressedThisFrame)
            {
                HandleInput(mapping.inputKey);
            }
            
            if (Keyboard.current[mapping.inputKey].wasReleasedThisFrame)
            {
                HandleRelease(mapping.inputKey);
            }
        }
    }
    
    /// <summary>
    /// Handle press input untuk key tertentu
    /// </summary>
    private void HandleInput(Key pressedKey)
    {
        float currentBeat = Conductor.Instance.songPositionInBeats;
        NoteController targetNote = GetNextActiveNote(pressedKey);

        if (targetNote != null)
        {
            float diff = Mathf.Abs(currentBeat - targetNote.targetBeat);
            
            if (diff < perfectWindow)
            {
                ScoreManager.Instance.Hit("Perfect");
                if (targetNote.isHoldNote) 
                    targetNote.StartHold();
                else 
                    targetNote.Deactivate();
            }
            else if (diff < goodWindow)
            {
                if (targetNote.isHoldNote)
                {
                    ScoreManager.Instance.Miss();
                    targetNote.MarkMissed();
                }
                else
                {
                    ScoreManager.Instance.Hit("Good");
                    targetNote.Deactivate();
                }
            }
            else if (diff < badWindow)
            {
                if (targetNote.isHoldNote)
                {
                    ScoreManager.Instance.Miss();
                    targetNote.MarkMissed();
                }
                else
                {
                    ScoreManager.Instance.Bad();
                    targetNote.Deactivate();
                }
            }
            else
            {
                Debug.Log($"Too Early / Whiff! Key: {pressedKey}");
            }
        }
        else
        {
            Debug.Log($"No matching note for key: {pressedKey}");
        }
    }
    
    /// <summary>
    /// Handle release input untuk key tertentu
    /// </summary>
    private void HandleRelease(Key releasedKey)
    {
        foreach (var note in activeNotes)
        {
            if (note.isHolding && IsNoteMatchingKey(note, releasedKey))
            {
                note.ReleaseHold();
                break;
            }
        }
    }
    
    /// <summary>
    /// Dapatkan note aktif berikutnya yang cocok dengan key
    /// </summary>
    private NoteController GetNextActiveNote(Key inputKey)
    {
        foreach (var note in activeNotes)
        {
            if (!note.isHit && !note.isMissed && !note.isHolding && 
                note.gameObject.activeSelf && IsNoteMatchingKey(note, inputKey))
            {
                return note;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Cek apakah note cocok dengan key input
    /// </summary>
    private bool IsNoteMatchingKey(NoteController note, Key inputKey)
    {
        if (judgmentMode == JudgmentMode.PerLane)
        {
            return inputKey == assignedKey;
        }
        else // Central mode
        {
            // Cek berdasarkan note type dari NoteController
            foreach (var mapping in noteKeyMappings)
            {
                if (mapping.inputKey == inputKey && 
                    mapping.noteType == note.noteType)
                {
                    return true;
                }
            }
            return false;
        }
    }
    
    /// <summary>
    /// Cek note yang terlewat
    /// </summary>
    private void CheckMissedNotes()
    {
        float currentBeat = Conductor.Instance.songPositionInBeats;
        
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            var note = activeNotes[i];
            
            if (!note.isHit && !note.isHolding && 
                currentBeat > note.targetBeat + badWindow)
            {
                Debug.Log($"MISS - Note Type: {note.noteType}");
                ScoreManager.Instance.Miss();

                if (note.isHoldNote) 
                    note.MarkMissed();
                else 
                    note.Deactivate();
            }
        }
    }
    
    /// <summary>
    /// Register note ke judgment point ini
    /// </summary>
    public void RegisterNote(NoteController note)
    {
        if (!activeNotes.Contains(note))
        {
            activeNotes.Add(note);
        }
    }
    
    /// <summary>
    /// Unregister note dari judgment point ini
    /// </summary>
    public void UnregisterNote(NoteController note)
    {
        activeNotes.Remove(note);
    }
    
    /// <summary>
    /// Dapatkan key yang tepat untuk note type tertentu (untuk central mode)
    /// </summary>
    public Key GetKeyForNoteType(NoteType noteType)
    {
        foreach (var mapping in noteKeyMappings)
        {
            if (mapping.noteType == noteType)
            {
                return mapping.inputKey;
            }
        }
        return Key.None;
    }
}

/// <summary>
/// Mode operasi judgment point
/// </summary>
public enum JudgmentMode
{
    PerLane,  // Satu judgment point per lane (sistem existing)
    Central   // Satu judgment point untuk semua lane
}

/// <summary>
/// Mapping antara note type dan input key untuk central judgment mode
/// </summary>
[System.Serializable]
public class NoteKeyMapping
{
    [Tooltip("Tipe note yang akan menggunakan key ini")]
    public NoteType noteType;
    
    [Tooltip("Key input untuk note type ini")]
    public Key inputKey;
    
    [Tooltip("Nama/deskripsi untuk editor")]
    public string description;
}

/// <summary>
/// Enum untuk tipe-tipe note yang berbeda
/// </summary>
public enum NoteType
{
    TypeA,    // Note merah, key A
    TypeB,    // Note biru, key B  
    TypeC,    // Note hijau, key C
    TypeD,    // Note kuning, key D
    Default   // Note default (untuk backward compatibility)
}
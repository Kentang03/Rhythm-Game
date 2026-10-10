using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class LaneController : MonoBehaviour
{
    [Header("Config")]
    public Key inputKey;
    public GameObject notePrefab;
    public Transform spawnPoint;
    public Transform hitPoint;
    public Transform missPoint;
    
    [Header("Note Type (untuk Central Judgment)")]
    [Tooltip("Note type untuk lane ini (hanya digunakan pada central judgment mode)")]
    public NoteType laneNoteType = NoteType.Default;
    
    [Header("Judgment Mode")]
    [Tooltip("Apakah lane ini menggunakan judgment point sendiri atau central")]
    public bool useCentralJudgment = false;

    [Header("Timing")]
    public float spawnDistanceInBeats = 2f;

    public List<float> noteTimestamps = new List<float>();
    public List<float> holdDurations = new List<float>();

    private int spawnIndex = 0;
    private int inputIndex = 0;

    private List<NoteController> activeNotes = new List<NoteController>();

    void Update()
    {
        CheckAndSpawnNotes();

        // Hanya handle input jika tidak menggunakan central judgment
        if (!useCentralJudgment)
        {
            CheckPlayerInput();
            CheckMissedNotes();
        }
    }

    void CheckAndSpawnNotes()
    {
        if (spawnIndex < noteTimestamps.Count)
        {
            float targetTime = noteTimestamps[spawnIndex];
            float currentBeat = Conductor.Instance.songPositionInBeats;
            
            // Sesuaikan spawn distance dengan speed multiplier
            float speedMultiplier = LaneManager.Instance != null ? LaneManager.Instance.GetNoteSpeedMultiplier() : 1.0f;
            float effectiveSpawnDistance = spawnDistanceInBeats / speedMultiplier;
            
            if (currentBeat >= targetTime - effectiveSpawnDistance)
            {
                float holdDuration = (holdDurations != null && spawnIndex < holdDurations.Count)
                    ? holdDurations[spawnIndex]
                    : 0f;
                
                // Dapatkan note type untuk beat ini (jika ada data dari LaneManager)
                NoteType noteTypeForThisBeat = GetNoteTypeForBeat(spawnIndex);
                
                SpawnNote(targetTime, holdDuration, noteTypeForThisBeat);
                spawnIndex++;
            }
        }
    }

    void SpawnNote(float targetBeat, float holdDuration = 0f, NoteType specificNoteType = NoteType.Default)
    {
        // Tentukan note type yang akan digunakan
        NoteType finalNoteType = specificNoteType;
        
        // Jika specific type adalah Default, gunakan lane note type (untuk central mode)
        if (finalNoteType == NoteType.Default && useCentralJudgment)
        {
            finalNoteType = laneNoteType;
        }
        
        // Pilih prefab berdasarkan note type
        GameObject prefabToUse = GetPrefabForNoteType(finalNoteType);
        
        // Hitung rotasi note agar menghadap arah gerak (dari spawnPoint ke hitPoint)
        Vector3 moveDir = (hitPoint.position - spawnPoint.position).normalized;
        Quaternion spawnRotation = moveDir != Vector3.zero
            ? Quaternion.LookRotation(moveDir, Vector3.up)
            : Quaternion.identity;

        GameObject obj = Instantiate(prefabToUse, spawnPoint.position, spawnRotation, transform);
        NoteController note = obj.GetComponent<NoteController>();

        // Sesuaikan spawn beat dengan speed multiplier
        float speedMultiplier = LaneManager.Instance != null ? LaneManager.Instance.GetNoteSpeedMultiplier() : 1.0f;
        float effectiveSpawnDistance = spawnDistanceInBeats / speedMultiplier;

        note.targetBeat = targetBeat;
        note.spawnBeat = targetBeat - effectiveSpawnDistance;
        note.spawnTransform = spawnPoint;
        note.endTransform   = hitPoint;
        note.missTransform  = missPoint;
        note.holdDuration = holdDuration;
        
        // Set note type
        note.noteType = finalNoteType;

        note.Initialize();

        activeNotes.Add(note);
    }

    void CheckPlayerInput()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[inputKey].wasPressedThisFrame) HandleInput();
        if (Keyboard.current[inputKey].wasReleasedThisFrame) HandleRelease();
    }

    void HandleInput()
    {
        float currentBeat = Conductor.Instance.songPositionInBeats;
        NoteController targetNote = GetNextActiveNote();

        if (targetNote != null)
        {
            float diff = Mathf.Abs(currentBeat - targetNote.targetBeat);

            if (diff < 0.35f)
            {
                ScoreManager.Instance.Hit("Perfect");
                if (targetNote.isHoldNote) 
                targetNote.StartHold();
                else 
                targetNote.Deactivate();
            }
            else if (diff < 0.7f)
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
            else if (diff < 1f)
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
                Debug.Log("Too Early / Whiff!");
            }
        }
    }
    void HandleRelease()
    {
        foreach (var note in activeNotes)
        {
            // If we find a note that is currently being held, release it
            if (note.isHolding)
            {
                note.ReleaseHold();
                break; // We found the active hold note, no need to check the rest
            }
        }
    }

    NoteController GetNextActiveNote()
    {
        foreach (var note in activeNotes)
        {
            //Skip note yang sudah di miss (hold menunggu tail habis) atau sudah di hit
            if (!note.isHit && !note.isMissed &&!note.isHolding && note.gameObject.activeSelf)
                return note;
        }
        return null;
    }

    void CheckMissedNotes()
    {
        if (inputIndex < spawnIndex)
        {
            foreach (var note in activeNotes)
            {
                if (!note.isHit && !note.isHolding && Conductor.Instance.songPositionInBeats > note.targetBeat + 0.7f)
                {
                    Debug.Log("MISS");
                    ScoreManager.Instance.Miss();

                    if (note.isHoldNote) note.MarkMissed();
                    else note.Deactivate();
                }
            }
        }
    }
    
    /// <summary>
    /// Dapatkan note type untuk beat tertentu dari chart data
    /// </summary>
    private NoteType GetNoteTypeForBeat(int beatIndex)
    {
        // Cek apakah ada chart data dengan note types
        if (LaneManager.Instance != null && LaneManager.Instance.currentChart != null)
        {
            // Cari lane data yang sesuai dengan LaneController ini
            foreach (var laneData in LaneManager.Instance.currentChart.lanes)
            {
                if (laneData.inputKey == inputKey || 
                    (laneData.recordedBeats.Count == noteTimestamps.Count && 
                     AreBeatListsEqual(laneData.recordedBeats, noteTimestamps)))
                {
                    // Jika ada note types data dan index valid
                    if (laneData.noteTypes != null && 
                        beatIndex < laneData.noteTypes.Count)
                    {
                        return laneData.noteTypes[beatIndex];
                    }
                    break;
                }
            }
        }
        
        // Fallback ke lane default note type atau Default
        return useCentralJudgment ? laneNoteType : NoteType.Default;
    }
    
    /// <summary>
    /// Helper method untuk membandingkan beat lists
    /// </summary>
    private bool AreBeatListsEqual(List<float> list1, List<float> list2)
    {
        if (list1.Count != list2.Count) return false;
        
        for (int i = 0; i < list1.Count; i++)
        {
            if (Mathf.Abs(list1[i] - list2[i]) > 0.01f) return false;
        }
        return true;
    }
    
    /// <summary>
    /// Dapatkan prefab yang sesuai untuk note type
    /// </summary>
    private GameObject GetPrefabForNoteType(NoteType noteType)
    {
        // Prioritas: JudgmentManager prefabs
        if (JudgmentManager.Instance != null)
        {
            GameObject typedPrefab = JudgmentManager.Instance.GetNotePrefabForType(noteType);
            if (typedPrefab != null)
            {
                return typedPrefab;
            }
        }
        
        // Fallback ke default prefab
        return notePrefab;
    }
}
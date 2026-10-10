using UnityEngine;
using UnityEngine.InputSystem;
public class NoteController : MonoBehaviour
{
    [Header("Timing")]
    public float targetBeat;
    public float spawnBeat;

    [Header("Hold")]
    [Tooltip("Durasi hold dalam detik, 0 = note biasa")]
    public float holdDuration = 0f;
    public bool isHoldNote => holdDuration > 0.05f;

    [Header("Position")]
    public Transform spawnTransform;
    public Transform endTransform;
    public Transform missTransform;

    // Posisi dibaca tiap frame dari Transform supaya ikut rotasi parent (note root)
    private Vector3 startPos => spawnTransform != null ? spawnTransform.position : _startPosFallback;
    private Vector3 endPos   => endTransform   != null ? endTransform.position   : _endPosFallback;
    private Vector3 missPos  => missTransform  != null ? missTransform.position  : _missPoseFallback;

    // Fallback kalau Transform tidak di-assign (backward compat)
    private Vector3 _startPosFallback;
    private Vector3 _endPosFallback;
    private Vector3 _missPoseFallback;

    [Header("Note Type")]
    [Tooltip("Tipe note ini - menentukan visual dan key mapping")]
    public NoteType noteType = NoteType.Default;

    [Header("State")]
    public bool isHit = false;
    public bool isHolding = false;
    public bool isHoldCompleted = false;
    public bool isMissed = false;
    public bool holdStarted = false;
    
    [Header("Judgment Point")]
    [Tooltip("Judgment point yang menangani note ini (auto-assigned)")]
    public JudgmentPoint assignedJudgmentPoint;

    [Header("Hold Tick")]
    [Tooltip("Interval waktu (detik) antar tick score saat hold")]
    public float holdTickInterval = 0.1f;
    private float holdTickTimer = 0f;

    [Header("Visual")]
    [Tooltip("Transform child untuk ekor hold note, bisa kosong jika nggak ada")]
    public Transform holdTail;
    [Tooltip("Transform child untuk kepala note. Disembunyikan saat hold dimulai")]
    public Transform noteHead;

    [Header("Hold Tail Consume Visual")]
    [Tooltip("Material holdTail HARUS pakai shader Custom/HoldNoteConsume (atau shader lain yang punya property _ConsumeAmount) supaya efek ini jalan.")]
    [SerializeField, Range(0.001f, 0.5f)] private float fadeWidth = 0.06f;
    [Tooltip("1 = konsumsi dari UV.y 0->1, -1 = arah sebaliknya. Kalau arah fade kebalik saat di-test, tinggal flip ini.")]
    [SerializeField] private float consumeDirection = 1f;
    [Tooltip("Kecepatan hilangnya tail. Semakin KECIL nilainya, semakin PERLAHAN tail menghilang (butuh waktu lebih lama dari holdDuration aslinya untuk full transparent). Semakin BESAR, semakin CEPAT hilang. 1 = pas sinkron dengan holdDuration.")]
    public float consumeSpeed = 1f;

    private float initialTailLength = 0f;
    private Vector3 backDirLocal;

    private float holdEndBeat;
    private float secPerBeat;
    private float holdElapsed = 0f;

    private float holdDurationSeconds = 0f;
    private bool tailVisualInitialized = false;

    // Visual consumption dengan transparency (spatial wipe via shader)
    private Renderer holdTailRenderer;
    private MaterialPropertyBlock propBlock;
    private static readonly int ConsumeAmountId = Shader.PropertyToID("_ConsumeAmount");
    private static readonly int FadeWidthId = Shader.PropertyToID("_FadeWidth");
    private static readonly int ConsumeDirectionId = Shader.PropertyToID("_ConsumeDirection");

    private float lastAppliedConsumeAmount = -1f; // -1 supaya apply pertama selalu jalan

    public void Initialize()
    {
        secPerBeat = Conductor.Instance.secPerBeat;
        holdDurationSeconds = holdDuration * secPerBeat;
        
        // Auto-assign ke judgment point yang sesuai
        AssignToJudgmentPoint();
        
        // Check dan buat arc connection jika ada note lain yang memenuhi kriteria
        if (NoteArcConnector.Instance != null)
        {
            NoteArcConnector.Instance.CheckAndCreateArc(this);
        }
        
        if (isHoldNote)
        {
            holdEndBeat = targetBeat + holdDuration;

            if (holdTail == null)
            {
                Transform found = transform.Find("HoldTail");
                if (found != null) holdTail = found;
            }

            if (noteHead == null)
            {
                Transform found = transform.Find("NoteHead");
                if (found != null) noteHead = found;
            }

            // Initialize material property block untuk shader-based consume wipe
            if (holdTail != null)
            {
                holdTailRenderer = holdTail.GetComponent<Renderer>();
                if (holdTailRenderer != null)
                {
                    propBlock = new MaterialPropertyBlock();

                    holdTailRenderer.GetPropertyBlock(propBlock);
                    propBlock.SetFloat(FadeWidthId, fadeWidth);
                    propBlock.SetFloat(ConsumeDirectionId, consumeDirection);
                    holdTailRenderer.SetPropertyBlock(propBlock);
                }
            }

            // UpdateHoldTailVisual dipanggil di frame pertama Update()
            // supaya posisi Transform sudah final setelah rotasi note root diterapkan
        }
        else
        {
            if (holdTail != null) holdTail.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Inisialisasi visual tail di frame pertama supaya posisi Transform
        // sudah final (termasuk setelah rotasi note root diterapkan)
        if (isHoldNote && !tailVisualInitialized)
        {
            tailVisualInitialized = true;
            UpdateHoldTailVisual();
        }

        float currentBeat = Conductor.Instance.songPositionInBeats;
        
        // Terapkan speed multiplier dari LaneManager
        float speedMultiplier = LaneManager.Instance != null ? LaneManager.Instance.GetNoteSpeedMultiplier() : 1.0f;
        float effectiveBeatRange = Mathf.Max(targetBeat - spawnBeat, 0.001f) / speedMultiplier;
        float t = Mathf.Clamp01((currentBeat - spawnBeat) / effectiveBeatRange);

        transform.position = Vector3.LerpUnclamped(startPos, endPos, t);

        float tMiss = (currentBeat - targetBeat) / (1.0f / speedMultiplier);
        transform.position = Vector3.LerpUnclamped(endPos, missPos, tMiss);


        // Note missed: tail tetap gerak, tunggu selesai lalu hilang
        if (isMissed && isHoldNote)
        {
            // Hitung berapa lama sejak kepala note melewati hit point
            float timeSinceHead = (currentBeat - targetBeat) * secPerBeat;
            // Tail dianggap selesai pas waktu berlalu
            if (timeSinceHead >= holdDurationSeconds)
            {
                Deactivate();
            }
            return; // Jangan proses hold logic
        }

        if (isHolding && isHoldNote)
        {
            holdElapsed += Time.deltaTime;

            // Tick Score
            holdTickTimer += Time.deltaTime;
            if (holdTickTimer > holdTickInterval)
            {
                holdTickTimer = 0f;
                ScoreManager.Instance.HoldTick();
            }

            UpdateHoldTailConsumed();

            if (currentBeat >= holdEndBeat) CompleteHold();
        }
    }

    public void StartHold()
    {
        if (!isHoldNote) return;
        if (holdStarted) return;
        holdStarted = true;
        isHolding = true;
        holdTickTimer = 0f;
        holdElapsed = 0f;

        // Sembunyikan kepala note saat hold dimulai
        if (noteHead != null)
        {
            noteHead.gameObject.SetActive(false);
        }
        else
        {
            // Fallback: sembunykan renderer di root object sendiri
            Renderer r = GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }
    }

    public void MarkMissed()
    {
        if (!isHoldNote) { Deactivate(); return; }
        isMissed = true;
        holdStarted = true; // Block note input
        isHolding = false;
        holdTickTimer = 0f;
        holdElapsed = 0f;

        // Sembunyikan kepala note
        if (noteHead != null) noteHead.gameObject.SetActive(false);
        else
        {
            Renderer r = GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }
        if (holdTail != null)
        {
            holdTail.gameObject.SetActive(false);
        }
        Deactivate();
    }

    public void ReleaseHold()
    {
        if (!isHoldNote || !isHolding) return;
        isHolding = false;
        holdTickTimer = 0f;

        float currentBeat = Conductor.Instance.songPositionInBeats;
        float holdProgress = (currentBeat - targetBeat) / (holdEndBeat - targetBeat);

        if (holdProgress >= 0.8f) CompleteHold();
        else
        {
            Debug.Log($"Hold released early: {holdProgress * 100f:F0}%");
            Deactivate();
        }
    }

    private void CompleteHold()
    {
        if (isHoldCompleted) return;
        isHoldCompleted = true;
        isHolding = false;
        Debug.Log("Hold Complete!");
        Deactivate();
    }

    private void UpdateHoldTailVisual()
    {
        if (holdTail == null) return;

        float laneLength = Vector3.Distance(startPos, endPos);
        if (laneLength <= 0f) return;

        float holdBeats = holdDuration;
        float spawnWindowBeats = targetBeat - spawnBeat;
        float holdRatio = Mathf.Max(holdBeats / spawnWindowBeats);
        initialTailLength = holdRatio * laneLength;

        Vector3 backDirWorld = (startPos - endPos).normalized;
        backDirLocal = transform.InverseTransformDirection(backDirWorld);

        // Tentukan sumbu dominan dari backDirLocal untuk tahu arah memanjang tail.
        // Lane atas/bawah → dominan di Y lokal, lane kiri/kanan → dominan di X lokal.
        Vector3 absDir = new Vector3(Mathf.Abs(backDirLocal.x), Mathf.Abs(backDirLocal.y), Mathf.Abs(backDirLocal.z));
        bool isHorizontalLane = absDir.x > absDir.y && absDir.x > absDir.z;

        Vector3 s = holdTail.localScale;

        float meshHalfExtent = 0.5f; // fallback
        MeshFilter tailMeshFilter = holdTail.GetComponent<MeshFilter>();
        
        if (isHorizontalLane)
        {
            // Lane kiri/kanan: scale di sumbu X, baca extents.x dari mesh
            s.x = initialTailLength;
            holdTail.localScale = s;

            if (tailMeshFilter != null && tailMeshFilter.sharedMesh != null)
                meshHalfExtent = tailMeshFilter.sharedMesh.bounds.extents.x;

            holdTail.localPosition = backDirLocal * (meshHalfExtent * s.x);
        }
        else
        {
            // Lane atas/bawah: scale di sumbu Y, baca extents.y dari mesh
            s.y = initialTailLength;
            holdTail.localScale = s;

            if (tailMeshFilter != null && tailMeshFilter.sharedMesh != null)
                meshHalfExtent = tailMeshFilter.sharedMesh.bounds.extents.y;

            holdTail.localPosition = backDirLocal * (meshHalfExtent * s.y);
        }

        // Reset consume amount ke 0 (belum ada yang terkonsumsi / full visible)
        SetConsumeAmount(0f, forceApply: true);

        if (holdTail != null && !holdTail.gameObject.activeSelf)
            holdTail.gameObject.SetActive(true);
    }

    private void UpdateHoldTailConsumed()
    {
        // consumeSpeed mengatur seberapa cepat holdElapsed "terasa" bagi fade:
        // < 1 = fade lebih lambat dari holdDuration asli, > 1 = lebih cepat.
        float progress = Mathf.Clamp01((holdElapsed / holdDurationSeconds) * consumeSpeed);
        SetConsumeAmount(progress);

        // Sembunyikan tail kalau sudah full terkonsumsi (hemat render, bukan lagi ganti transform)
        if (progress >= 0.99f && holdTail != null && holdTail.gameObject.activeSelf)
        {
            holdTail.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Update progress konsumsi visual tail lewat MaterialPropertyBlock.
    /// Tidak menyentuh transform/scale sama sekali - hanya 1 float ke GPU.
    /// </summary>
    private void SetConsumeAmount(float normalizedAmount, bool forceApply = false)
    {
        if (holdTailRenderer == null || propBlock == null) return;

        normalizedAmount = Mathf.Clamp01(normalizedAmount);

        if (!forceApply && Mathf.Approximately(normalizedAmount, lastAppliedConsumeAmount))
            return;

        lastAppliedConsumeAmount = normalizedAmount;

        holdTailRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(ConsumeAmountId, normalizedAmount);
        holdTailRenderer.SetPropertyBlock(propBlock);
    }

    public void Deactivate()
    {
        // Unregister dari judgment point
        if (assignedJudgmentPoint != null)
        {
            assignedJudgmentPoint.UnregisterNote(this);
        }
        
        gameObject.SetActive(false);
        isHit = true;
    }
    
    /// <summary>
    /// Assign note ini ke judgment point yang sesuai
    /// </summary>
    private void AssignToJudgmentPoint()
    {
        // Prioritas: Central judgment point jika ada dan note memiliki type yang di-support
        if (JudgmentPoint.CentralInstance != null && noteType != NoteType.Default)
        {
            Key requiredKey = JudgmentPoint.CentralInstance.GetKeyForNoteType(noteType);
            if (requiredKey != Key.None)
            {
                assignedJudgmentPoint = JudgmentPoint.CentralInstance;
                assignedJudgmentPoint.RegisterNote(this);
                return;
            }
        }
        
        // Fallback: cari judgment point per-lane terdekat
        JudgmentPoint[] allJudgmentPoints = FindObjectsByType<JudgmentPoint>(FindObjectsSortMode.None);
        float closestDistance = float.MaxValue;
        JudgmentPoint closestPoint = null;
        
        foreach (var jp in allJudgmentPoints)
        {
            if (jp.judgmentMode == JudgmentMode.PerLane)
            {
                float distance = Vector3.Distance(transform.position, jp.Position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPoint = jp;
                }
            }
        }
        
        if (closestPoint != null)
        {
            assignedJudgmentPoint = closestPoint;
            assignedJudgmentPoint.RegisterNote(this);
        }
        else
        {
            Debug.LogWarning($"NoteController: Tidak bisa menemukan judgment point untuk note {name}");
        }
    }
}
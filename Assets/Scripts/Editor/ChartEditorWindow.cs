using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

public enum ChartNoteType
{
    Tap = 0,
    Hold = 1
}

[System.Serializable]
public class NoteData
{
    public float startBeat;      // posisi awal (dalam beat)
    public float durationBeats;  // panjang note (dalam beat), misal 0.25, 0.5, 1, 2
    public int lane;
}

[System.Serializable]
public class ChartNote
{
    public float beat;
    public int lane;            // 1-based
    public ChartNoteType type;
    public float length;        // in beats, only used by Hold notes
}

public class ChartEditorWindow : EditorWindow
{
    // Project Data
    private string projectName = "New Chart";
    private float bpm = 120f;
    private float duration = 60f; // in seconds
    private AudioClip audioClip;
    private List<ChartNote> notes = new List<ChartNote>();

    // 1 = per beat, 0.5 = 1/2 beat, 0.25 = 1/4 beat, 0.125 = 1/8 beat
    private float snapBeats = 0.25f;
    private readonly float[] snapOptions = { 1f, 0.5f, 0.25f, 0.125f };
    private readonly string[] snapLabels = { "1/1", "1/2", "1/4", "1/8" };
    private int snapIndex = 2;
    

    // Timeline visualization
    private Vector2 scrollPosition;
    private float timelineWidth = 2000f;
    private float timelineHeight = 300f;
    private float currentTime = 0f; // playhead position in seconds
    private float timelineViewWidth = 800f;
    private float visibleMinX = 0f;
    private float visibleMaxX = 2000f;

    // Grid settings
    private float pixelsPerSecond = 50f;
    private float beatsPerMeasure = 4f;

    // Colors
    private Color gridColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    private Color measureLineColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    private Color playheadColor = new Color(0.2f, 0.5f, 1f, 1f);
    private static readonly Color TapColor = new Color(0.30f, 0.79f, 0.94f, 1f);
    private static readonly Color HoldColor = new Color(0.96f, 0.64f, 0.38f, 1f);

    // File path
    private string projectFilePath = "";

    // Audio playback
    private bool isPlaying = false;
    private double audioStartTime = 0;
    private System.Reflection.Assembly unityEditorAssembly;
    private System.Type audioUtilClass;
    private MethodInfo playPreviewClipMethod;
    private MethodInfo stopAllPreviewClipsMethod;
    private float laneLength = 1;

    // Layout constants (tweak these to fit your window)
    private const float PanelHeight = 200f;
    private const float KeyframeRowHeight = 22f;
    private const float KeyframeListHeight = 112f;
    private const float RulerHeight = 24f;   // top band of the timeline (beat labels + scrub area)
    private const float FooterHeight = 20f;  // bottom band of the timeline (second labels)
    private const float NoteHeadWidth = 14f;

    // Note editing
    private ChartNoteType selectedNoteType = ChartNoteType.Tap;
    private int snapDivision = 4;
    private static readonly int[] SnapValues = { 1, 2, 4, 8 };
    private static readonly string[] SnapLabels = { "1 beat", "1/2 beat", "1/4 beat", "1/8 beat" };
    private bool isPlacingHold = false;
    private int holdLane;
    private float holdStartBeat;
    private float holdPreviewLength;

    // Keyframe panel
    private static readonly string[] FilterLabels = { "All", "Tap", "Hold" };
    private int keyframeFilter = 0; // 0 = all, 1 = tap, 2 = hold
    private bool followPlayhead = true;
    private Vector2 keyframeScroll;
    private readonly List<ChartNote> keyframeView = new List<ChartNote>();
    private bool keyframeViewDirty = true;
    private int lastCurrentRow = -2;
    private int pendingScrollRow = -1;

    // Styles (created lazily inside OnGUI)
    private GUIStyle timeStyle, beatStyle, mutedLabel, hintStyle, emptyStyle, rowStyle, rowPastStyle, rowCenterStyle;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // Derived values
    private int LaneCount { get { return Mathf.Clamp(Mathf.RoundToInt(laneLength), 1, 8); } }
    private float SecondsPerBeat { get { return 60f / bpm; } }
    private float TotalBeats { get { return duration / SecondsPerBeat; } }
    private float CurrentBeat { get { return currentTime / SecondsPerBeat; } }
    private float LaneHeight { get { return (timelineHeight - RulerHeight - FooterHeight) / LaneCount; } }

    [MenuItem("Window/Chart Editor")]
    public static void ShowWindow()
    {
        ChartEditorWindow window = GetWindow<ChartEditorWindow>("Chart Editor");
        window.minSize = new Vector2(900, 600);
        window.Show();
    }

    private void OnEnable()
    {
        // Initialize window
        Debug.Log("Chart Editor Window Enabled");

        // Get AudioUtil class for editor audio playback
        unityEditorAssembly = typeof(AudioImporter).Assembly;
        audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");

        if (audioUtilClass != null)
        {
            playPreviewClipMethod = audioUtilClass.GetMethod(
                "PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null
            );

            stopAllPreviewClipsMethod = audioUtilClass.GetMethod(
                "StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public
            );
        }

        // Register update callback for audio playback
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        // Stop audio when window closes
        StopAudio();
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (!isPlaying) return;

        // Update playhead position from the playback clock (works with or without audio)
        double elapsed = EditorApplication.timeSinceStartup - audioStartTime;
        currentTime = (float)elapsed;

        float end = audioClip != null ? audioClip.length : duration;

        // Check if playback finished
        if (currentTime >= end)
        {
            currentTime = end;
            StopAudio();
        }

        Repaint();
    }

    private void OnGUI()
    {
        EnsureStyles();
        HandleGlobalShortcuts();

        DrawToolbar();

        float leftWidth = Mathf.Clamp(position.width * 0.34f, 320f, 480f);
        float rightWidth = position.width - leftWidth - 24f;

        EditorGUILayout.BeginHorizontal();

        // Left column: Project Settings + Note Settings
        EditorGUILayout.BeginVertical(GUILayout.Width(leftWidth));
        DrawProjectSettings();
        DrawNoteSetting();
        EditorGUILayout.EndVertical();

        // Right column: Audio + Keyframes (the previously empty top-right area)
        EditorGUILayout.BeginHorizontal();
        DrawAudioPanel(Mathf.Clamp(rightWidth * 0.45f, 270f, 360f));
        DrawKeyframePanel();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndHorizontal();

        DrawTimeline();
        // DrawPlayheadControls();

        // Auto repaint to keep timeline updated
        if (Event.current.type == EventType.MouseDrag)
        {
            Repaint();
        }
    }

    private void EnsureStyles()
    {
        if (timeStyle != null) return;

        Font mono = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Menlo", "Courier New" }, 13);
        Color muted = new Color(0.66f, 0.66f, 0.66f, 1f);

        timeStyle = new GUIStyle(EditorStyles.label) { font = mono, fontSize = 13, alignment = TextAnchor.MiddleLeft };

        beatStyle = new GUIStyle(timeStyle) { fontSize = 12 };
        beatStyle.normal.textColor = new Color(0.62f, 0.76f, 1f, 1f);

        mutedLabel = new GUIStyle(EditorStyles.label) { fontSize = 11, clipping = TextClipping.Clip };
        mutedLabel.normal.textColor = muted;

        hintStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        hintStyle.normal.textColor = muted;

        emptyStyle = new GUIStyle(EditorStyles.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        emptyStyle.normal.textColor = muted;

        rowStyle = new GUIStyle(EditorStyles.label) { font = mono, fontSize = 12, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        rowStyle.normal.textColor = new Color(0.89f, 0.89f, 0.89f, 1f);

        rowPastStyle = new GUIStyle(rowStyle);
        rowPastStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        rowCenterStyle = new GUIStyle(rowStyle) { alignment = TextAnchor.MiddleCenter };
        rowCenterStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    }

    private void HandleGlobalShortcuts()
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space && !EditorGUIUtility.editingTextField)
        {
            TogglePlayback();
            e.Use();
        }
    }

    private void DrawToolbar()
    {
        snapIndex = EditorGUILayout.Popup("Snap", snapIndex, snapLabels);
        snapBeats = snapOptions[snapIndex]; 
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("New Project", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            NewProject();
        }

        if (GUILayout.Button("Load Project", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            LoadProject();
        }

        if (GUILayout.Button("Save Project", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            SaveProject();
        }
        if (GUILayout.Button("Test Project", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            Debug.Log("Testing");
        }
        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawProjectSettings()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Project Settings", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Project Name:", GUILayout.Width(100));
        projectName = EditorGUILayout.TextField(projectName);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("BPM:", GUILayout.Width(100));
        bpm = EditorGUILayout.FloatField(bpm);
        bpm = Mathf.Clamp(bpm, 1f, 300f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Duration (sec):", GUILayout.Width(100));

        // If audio clip is assigned, use its length
        if (audioClip != null)
        {
            duration = audioClip.length;
            EditorGUILayout.FloatField(duration);
            GUI.enabled = false;
            EditorGUILayout.LabelField("(Auto from audio)", EditorStyles.miniLabel, GUILayout.Width(100));
            GUI.enabled = true;
        }
        else
        {
            duration = EditorGUILayout.FloatField(duration);
            duration = Mathf.Clamp(duration, 1f, 600f);
        }

        EditorGUILayout.EndHorizontal();

        // Display calculated info
        float totalBeats = (bpm / 60f) * duration;
        EditorGUILayout.LabelField($"Total Beats: {totalBeats:F2}", EditorStyles.miniLabel);

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);
    }

    private void DrawNoteSetting()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Note Settings", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Lane Lenght", GUILayout.Width(100));
        laneLength = EditorGUILayout.FloatField(laneLength);
        laneLength = Mathf.Clamp(laneLength, 1f, 8f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Type", GUILayout.Width(100));

        if (GUILayout.Toggle(selectedNoteType == ChartNoteType.Tap, "Tap Note", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            selectedNoteType = ChartNoteType.Tap;
        }

        if (GUILayout.Toggle(selectedNoteType == ChartNoteType.Hold, "Hold Note", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            selectedNoteType = ChartNoteType.Hold;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Snap", GUILayout.Width(100));
        snapDivision = EditorGUILayout.IntPopup(snapDivision, SnapLabels, SnapValues);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Click a lane to place a note. Drag for Hold length. Right-click to delete.", hintStyle);

        EditorGUILayout.EndVertical();
    }

    // ---------------------------------------------------------------------
    // Audio panel
    // ---------------------------------------------------------------------

    private void DrawAudioPanel(float width)
    {
        EditorGUILayout.BeginVertical("box", GUILayout.Width(width), GUILayout.Height(PanelHeight));
        EditorGUILayout.LabelField("Audio", EditorStyles.boldLabel);

        // Track row
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(audioClip != null ? audioClip.name : "No music loaded", mutedLabel, GUILayout.Height(20));
        if (GUILayout.Button(audioClip != null ? "Change" : "+ Add music", GUILayout.Width(88)))
        {
            AddMusic();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Transport row
        EditorGUILayout.BeginHorizontal();

        Color previousBackground = GUI.backgroundColor;
        GUI.backgroundColor = playheadColor;
        if (GUILayout.Button(isPlaying ? "⏸ Pause" : "▶ Play", GUILayout.Height(32), GUILayout.Width(82)))
        {
            TogglePlayback();
        }
        GUI.backgroundColor = previousBackground;

        if (GUILayout.Button("⏹ Stop", GUILayout.Height(32), GUILayout.Width(66)))
        {
            StopAudio();
        }

        GUILayout.Space(6);

        EditorGUILayout.BeginVertical();
        GUILayout.Label(FormatTime(currentTime) + " / " + FormatTime(duration), timeStyle);
        GUILayout.Label("Beat " + CurrentBeat.ToString("0.00", Inv), beatStyle);
        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Seek bar
        EditorGUI.BeginChangeCheck();
        float seek = GUILayout.HorizontalSlider(currentTime, 0f, duration);
        if (EditorGUI.EndChangeCheck())
        {
            SeekTo(seek);
        }

        EditorGUILayout.Space(4);
        GUILayout.Label("Space: play or pause. Click a keyframe to jump to its beat.", hintStyle);

        EditorGUILayout.EndVertical();
    }

    private void AddMusic()
    {
        string path = EditorUtility.OpenFilePanel("Select music", Application.dataPath, "mp3,wav,ogg");
        if (string.IsNullOrEmpty(path)) return;

        string assetPath;
        string dataPath = Application.dataPath; // .../Project/Assets

        if (path.StartsWith(dataPath, System.StringComparison.OrdinalIgnoreCase))
        {
            assetPath = "Assets" + path.Substring(dataPath.Length);
        }
        else
        {
            // The clip must live inside the project so the path can be saved and reloaded.
            bool copy = EditorUtility.DisplayDialog(
                "Copy music into project?",
                "This file is outside your Unity project. Copy it to Assets/ChartAudio so it can be saved with the chart?",
                "Copy",
                "Cancel");
            if (!copy) return;

            string folder = dataPath + "/ChartAudio";
            Directory.CreateDirectory(folder);

            string fileName = Path.GetFileName(path);
            File.Copy(path, folder + "/" + fileName, true);
            assetPath = "Assets/ChartAudio/" + fileName;
        }

        AssetDatabase.ImportAsset(assetPath);
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);

        if (clip == null)
        {
            EditorUtility.DisplayDialog("Add music", "Couldn't load that file as an audio clip.", "OK");
            return;
        }

        StopAudio();
        audioClip = clip;
        duration = clip.length;
        currentTime = 0f;
        Repaint();
    }

    private void TogglePlayback()
    {
        if (isPlaying)
        {
            PauseAudio();
        }
        else
        {
            PlayAudio();
        }
    }

    // ---------------------------------------------------------------------
    // Keyframe panel
    // ---------------------------------------------------------------------

    private void DrawKeyframePanel()
    {
        EnsureKeyframeView();

        EditorGUILayout.BeginVertical("box", GUILayout.Height(PanelHeight));

        // Header
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Keyframes (" + notes.Count + ")", EditorStyles.boldLabel);
        if (GUILayout.Button(new GUIContent("‹", "Previous keyframe"), EditorStyles.miniButtonLeft, GUILayout.Width(26)))
        {
            JumpToAdjacentKeyframe(-1);
        }
        if (GUILayout.Button(new GUIContent("›", "Next keyframe"), EditorStyles.miniButtonRight, GUILayout.Width(26)))
        {
            JumpToAdjacentKeyframe(1);
        }
        EditorGUILayout.EndHorizontal();

        // Filter + follow
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        keyframeFilter = GUILayout.Toolbar(keyframeFilter, FilterLabels, GUILayout.Width(135));
        if (EditorGUI.EndChangeCheck())
        {
            keyframeViewDirty = true;
        }
        GUILayout.FlexibleSpace();
        followPlayhead = GUILayout.Toggle(followPlayhead, new GUIContent("Follow", "Auto-scroll the list with the playhead"), GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        EnsureKeyframeView();

        // Column header
        Rect header = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(header, new Color(0.19f, 0.19f, 0.19f, 1f));
        GUI.Label(new Rect(header.x + 8, header.y, 64, header.height), "Beat", mutedLabel);
        GUI.Label(new Rect(header.x + 76, header.y, 40, header.height), "Lane", mutedLabel);
        GUI.Label(new Rect(header.x + 130, header.y, 100, header.height), "Type", mutedLabel);

        // List
        Rect listRect = GUILayoutUtility.GetRect(0, KeyframeListHeight, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(listRect, new Color(0.16f, 0.16f, 0.16f, 1f));
        DrawKeyframeList(listRect);

        EditorGUILayout.EndVertical();
    }

    private void DrawKeyframeList(Rect listRect)
    {
        if (keyframeView.Count == 0)
        {
            GUI.Label(listRect, "No keyframes yet.\nClick a lane in the timeline to add a note.", emptyStyle);
            return;
        }

        int cur = FindCurrentRow();
        if (cur != lastCurrentRow)
        {
            lastCurrentRow = cur;
            if (followPlayhead && cur >= 0)
            {
                pendingScrollRow = cur;
            }
        }

        if (pendingScrollRow >= 0)
        {
            keyframeScroll.y = Mathf.Max(0f, pendingScrollRow * KeyframeRowHeight - listRect.height * 0.5f + KeyframeRowHeight * 0.5f);
            pendingScrollRow = -1;
        }

        float contentHeight = keyframeView.Count * KeyframeRowHeight;
        float viewWidth = listRect.width - (contentHeight > listRect.height ? 15f : 0f);

        keyframeScroll = GUI.BeginScrollView(listRect, keyframeScroll, new Rect(0, 0, viewWidth, contentHeight));

        // Only draw the rows that are visible, so long charts stay cheap
        int first = Mathf.Max(0, Mathf.FloorToInt(keyframeScroll.y / KeyframeRowHeight));
        int last = Mathf.Min(keyframeView.Count - 1, Mathf.CeilToInt((keyframeScroll.y + listRect.height) / KeyframeRowHeight));

        Event e = Event.current;
        ChartNote toDelete = null;
        ChartNote toJump = null;

        for (int i = first; i <= last; i++)
        {
            ChartNote n = keyframeView[i];
            Rect row = new Rect(0, i * KeyframeRowHeight, viewWidth, KeyframeRowHeight);
            bool isCurrent = i == cur;
            bool isPast = i < cur;

            if (isCurrent)
            {
                EditorGUI.DrawRect(row, new Color(0.23f, 0.51f, 0.96f, 0.22f));
                EditorGUI.DrawRect(new Rect(row.x, row.y, 3, row.height), playheadColor);
            }
            else if (i % 2 == 1)
            {
                EditorGUI.DrawRect(row, new Color(1f, 1f, 1f, 0.03f));
            }

            GUIStyle style = isPast ? rowPastStyle : rowStyle;

            GUI.Label(new Rect(8, row.y, 64, row.height), FormatBeat(n.beat), style);
            GUI.Label(new Rect(76, row.y, 40, row.height), "L" + n.lane, style);

            Color typeColor = n.type == ChartNoteType.Tap ? TapColor : HoldColor;
            EditorGUI.DrawRect(new Rect(130, row.y + row.height * 0.5f - 4f, 8, 8), typeColor);

            string typeText = n.type == ChartNoteType.Tap
                ? "Tap"
                : "Hold  " + n.length.ToString("0.##", Inv) + " beats";
            GUI.Label(new Rect(144, row.y, Mathf.Max(0f, viewWidth - 144f - 26f), row.height), typeText, style);

            Rect deleteRect = new Rect(viewWidth - 24f, row.y, 22f, row.height);
            GUI.Label(deleteRect, "×", rowCenterStyle);

            if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
            {
                if (deleteRect.Contains(e.mousePosition))
                {
                    toDelete = n;
                }
                else
                {
                    toJump = n;
                }
                e.Use();
            }
        }

        GUI.EndScrollView();

        if (toDelete != null)
        {
            notes.Remove(toDelete);
            keyframeViewDirty = true;
            Repaint();
        }
        else if (toJump != null)
        {
            JumpToBeat(toJump.beat);
        }
    }

    private void EnsureKeyframeView()
    {
        if (!keyframeViewDirty) return;
        keyframeViewDirty = false;

        keyframeView.Clear();
        for (int i = 0; i < notes.Count; i++)
        {
            ChartNote n = notes[i];
            bool include = keyframeFilter == 0
                || (keyframeFilter == 1 && n.type == ChartNoteType.Tap)
                || (keyframeFilter == 2 && n.type == ChartNoteType.Hold);
            if (include)
            {
                keyframeView.Add(n);
            }
        }

        // Force the follow logic to re-center on the current row
        lastCurrentRow = -2;
    }

    private int FindCurrentRow()
    {
        float beat = CurrentBeat + 0.0005f;
        int cur = -1;
        for (int i = 0; i < keyframeView.Count; i++)
        {
            if (keyframeView[i].beat <= beat)
            {
                cur = i;
            }
            else
            {
                break;
            }
        }
        return cur;
    }

    private void JumpToAdjacentKeyframe(int direction)
    {
        EnsureKeyframeView();
        float beat = CurrentBeat;
        ChartNote target = null;

        if (direction < 0)
        {
            for (int i = 0; i < keyframeView.Count; i++)
            {
                if (keyframeView[i].beat < beat - 0.001f) target = keyframeView[i];
                else break;
            }
        }
        else
        {
            for (int i = 0; i < keyframeView.Count; i++)
            {
                if (keyframeView[i].beat > beat + 0.001f)
                {
                    target = keyframeView[i];
                    break;
                }
            }
        }

        if (target != null)
        {
            JumpToBeat(target.beat);
        }
    }

    private void JumpToBeat(float beat)
    {
        SeekTo(beat * SecondsPerBeat);
        EnsurePlayheadVisible();
    }

    private void EnsurePlayheadVisible()
    {
        float x = currentTime * pixelsPerSecond;
        if (x < scrollPosition.x + 20f || x > scrollPosition.x + timelineViewWidth - 20f)
        {
            scrollPosition.x = Mathf.Max(0f, x - timelineViewWidth * 0.3f);
        }
    }

    // ---------------------------------------------------------------------
    // Notes
    // ---------------------------------------------------------------------

    private void AddNote(int lane, float beat, ChartNoteType type, float length)
    {
        for (int i = 0; i < notes.Count; i++)
        {
            if (notes[i].lane == lane && Mathf.Abs(notes[i].beat - beat) < 0.0001f)
            {
                return; // already a note here
            }
        }

        notes.Add(new ChartNote
        {
            beat = beat,
            lane = lane,
            type = type,
            length = type == ChartNoteType.Hold ? length : 0f
        });

        SortNotes();
        Repaint();
    }

    private void SortNotes()
    {
        notes.Sort((a, b) =>
        {
            int c = a.beat.CompareTo(b.beat);
            return c != 0 ? c : a.lane.CompareTo(b.lane);
        });
        keyframeViewDirty = true;
    }

    private float SnapBeat(float localX)
    {
        float beat = localX / pixelsPerSecond / SecondsPerBeat;
        beat = Mathf.Round(beat * snapDivision) / snapDivision;
        return Mathf.Clamp(beat, 0f, TotalBeats);
    }

    private int LaneFromY(float y)
    {
        if (y < RulerHeight || y > timelineHeight - FooterHeight) return -1;
        int lane = Mathf.FloorToInt((y - RulerHeight) / LaneHeight) + 1;
        return (lane >= 1 && lane <= LaneCount) ? lane : -1;
    }

    private Rect GetNoteRect(float beat, int lane, ChartNoteType type, float length)
    {
        float laneHeight = LaneHeight;
        float x = beat * SecondsPerBeat * pixelsPerSecond;
        float y = RulerHeight + (lane - 1) * laneHeight + 3f;
        float width = NoteHeadWidth;

        if (type == ChartNoteType.Hold)
        {
            width += length * SecondsPerBeat * pixelsPerSecond;
        }

        return new Rect(x - NoteHeadWidth * 0.5f, y, width, laneHeight - 6f);
    }

    private ChartNote HitTestNote(Vector2 position)
    {
        int lane = LaneFromY(position.y);
        if (lane < 0) return null;

        for (int i = notes.Count - 1; i >= 0; i--)
        {
            ChartNote n = notes[i];
            if (n.lane != lane) continue;

            Rect r = GetNoteRect(n.beat, n.lane, n.type, n.length);
            if (position.x >= r.xMin && position.x <= r.xMax)
            {
                return n;
            }
        }
        return null;
    }
    
    // field class
    private int resizingIndex = -1;

    private void DrawNotes()
    {
        int laneCount = LaneCount;
        float secondsPerBeat = 60f / bpm;
        float pixelsPerBeat = secondsPerBeat * pixelsPerSecond;
        Event e = Event.current;

        for (int i = 0; i < notes.Count; i++)
        {
            ChartNote n = notes[i];
            if (n.lane > laneCount) continue;

            Rect r = GetNoteRect(n.beat, n.lane, n.type, n.length);
            if (r.xMax < visibleMinX || r.xMin > visibleMaxX) continue;

            DrawNoteRect(r, n.type, 1f);

            // resize handle hanya untuk Hold
            if (n.type == ChartNoteType.Hold)
            {
                float handleWidth = Mathf.Min(6f, r.width * 0.3f);
                Rect handle = new Rect(r.xMax - handleWidth, r.y, handleWidth, r.height);
                EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeHorizontal);

                if (e.type == EventType.MouseDown && e.button == 0 && handle.Contains(e.mousePosition))
                {
                    resizingIndex = i;
                    e.Use();
                }
            }
        }

        // drag diproses sekali, di luar loop
        if (resizingIndex >= 0 && e.type == EventType.MouseDrag)
        {
            ChartNote n = notes[resizingIndex];
            float mouseBeat = e.mousePosition.x / pixelsPerBeat;
            float rawLength = mouseBeat - n.beat;
            n.length = Mathf.Max(snapBeats, Mathf.Round(rawLength / snapBeats) * snapBeats);
            notes[resizingIndex] = n; // aman kalau ChartNote adalah struct
            e.Use();
            Repaint();
        }

        if (e.type == EventType.MouseUp)
            resizingIndex = -1;

        // preview hold, digambar sekali
        if (isPlacingHold)
        {
            Rect preview = GetNoteRect(holdStartBeat, holdLane, ChartNoteType.Hold, holdPreviewLength);
            DrawNoteRect(preview, ChartNoteType.Hold, 0.55f);
        }
    }
    private void DrawNoteRect(Rect r, ChartNoteType type, float alpha)
    {
        Color baseColor = type == ChartNoteType.Tap ? TapColor : HoldColor;

        if (type == ChartNoteType.Hold)
        {
            Color body = baseColor;
            body.a = 0.55f * alpha;
            EditorGUI.DrawRect(r, body);
        }

        Color head = baseColor;
        head.a = alpha;
        EditorGUI.DrawRect(new Rect(r.x, r.y, Mathf.Min(NoteHeadWidth, r.width), r.height), head);
    }

    // ---------------------------------------------------------------------
    // Timeline
    // ---------------------------------------------------------------------

    private void DrawTimeline()
    {
        EditorGUILayout.LabelField("Timeline", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Zoom:", GUILayout.Width(50));
        pixelsPerSecond = EditorGUILayout.Slider(pixelsPerSecond, 10f, 200f);
        EditorGUILayout.EndHorizontal();

        timelineWidth = duration * pixelsPerSecond;

        Rect scrollViewRect = GUILayoutUtility.GetRect(position.width - 20, timelineHeight + 15); // +15 untuk scrollbar
        if (Event.current.type == EventType.Repaint)
        {
            timelineViewWidth = scrollViewRect.width;
        }

        scrollPosition = GUI.BeginScrollView(
            scrollViewRect,
            scrollPosition,
            new Rect(0, 0, timelineWidth, timelineHeight),
            true, false); // horizontal scrollbar selalu tampil, vertikal tidak

        visibleMinX = scrollPosition.x;
        visibleMaxX = scrollPosition.x + scrollViewRect.width;

        EditorGUI.DrawRect(new Rect(0, 0, timelineWidth, timelineHeight), new Color(0.2f, 0.2f, 0.2f, 1f));
        EditorGUI.DrawRect(new Rect(0, 0, timelineWidth, RulerHeight), new Color(0.17f, 0.17f, 0.17f, 1f));

        DrawGridLines();
        DrawNotes();
        DrawPlayhead();

        // Input diproses DI DALAM scroll view, pakai koordinat lokal
        HandleTimelineInput();

        GUI.EndScrollView();
    }

    private void DrawGridLines()
    {
        float secondsPerBeat = 60f / bpm;
        float totalBeats = duration / secondsPerBeat;

        // Draw beat lines
        for (int beat = 0; beat <= Mathf.CeilToInt(totalBeats); beat++)
        {
            float timeInSeconds = beat * secondsPerBeat;
            float xPos = timeInSeconds * pixelsPerSecond;

            // Check if this is a measure line (every 4 beats by default)
            bool isMeasureLine = (beat % beatsPerMeasure == 0);
            Color lineColor = isMeasureLine ? measureLineColor : gridColor;
            float lineWidth = isMeasureLine ? 2f : 1f;

            // Draw vertical line
            Rect lineRect = new Rect(xPos, 0, lineWidth, timelineHeight);
            EditorGUI.DrawRect(lineRect, lineColor);

            // Draw beat number for measure lines
            if (isMeasureLine)
            {
                Rect labelRect = new Rect(xPos + 3, 4, 50, 18);
                GUI.Label(labelRect, $"B{beat}", EditorStyles.miniLabel);
            }
        }

        // Draw time markers every second
        for (int sec = 0; sec <= Mathf.CeilToInt(duration); sec++)
        {
            float xPos = sec * pixelsPerSecond;
            Rect labelRect = new Rect(xPos + 3, timelineHeight - FooterHeight, 50, 18);
            GUI.Label(labelRect, $"{sec}s", EditorStyles.miniLabel);
        }

        // Lane separators (lane area sits between the ruler and the footer)
        int laneCount = LaneCount;
        float laneHeight = LaneHeight;

        for (int gridLane = 0; gridLane <= laneCount; gridLane++)
        {
            float lineThickness = 2f;
            float yPos = RulerHeight + laneHeight * gridLane;

            Rect laneRect = new Rect(0, yPos, timelineWidth, lineThickness);
            EditorGUI.DrawRect(laneRect, gridColor);
        }
    }

    private void DrawPlayhead()
    {
        float xPos = currentTime * pixelsPerSecond;

        // Draw playhead line
        Rect playheadRect = new Rect(xPos - 1, 0, 3, timelineHeight);
        EditorGUI.DrawRect(playheadRect, playheadColor);

        // Draw playhead triangle at top
        Handles.BeginGUI();
        Handles.color = playheadColor;
        Vector3[] trianglePoints = new Vector3[3]
        {
            new Vector3(xPos, 0),
            new Vector3(xPos - 8, 15),
            new Vector3(xPos + 8, 15)
        };
        Handles.DrawAAConvexPolygon(trianglePoints);
        Handles.EndGUI();
    }

    private int controlID_Dragging = 0;
    private bool isScrubbing = false;

    private void HandleTimelineInput()
    {
        Event e = Event.current;

        // Ruler (top band): scrub the playhead. Lane area: place and delete notes.
        Rect rulerRect = new Rect(0, 0, timelineWidth, RulerHeight);
        Rect laneAreaRect = new Rect(0, RulerHeight, timelineWidth, timelineHeight - RulerHeight - FooterHeight);

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0 && rulerRect.Contains(e.mousePosition))
                {
                    isScrubbing = true;
                    SetTimeFromMouse(e.mousePosition.x);
                    e.Use();
                }
                else if (e.button == 0 && laneAreaRect.Contains(e.mousePosition))
                {
                    BeginPlaceNote(e.mousePosition);
                    e.Use();
                }
                else if (e.button == 1 && laneAreaRect.Contains(e.mousePosition))
                {
                    ChartNote hit = HitTestNote(e.mousePosition);
                    if (hit != null)
                    {
                        notes.Remove(hit);
                        keyframeViewDirty = true;
                        Repaint();
                    }
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (isScrubbing)
                {
                    SetTimeFromMouse(e.mousePosition.x);
                    e.Use();
                }
                else if (isPlacingHold)
                {
                    float minLength = 1f / snapDivision;
                    holdPreviewLength = Mathf.Max(minLength, SnapBeat(e.mousePosition.x) - holdStartBeat);
                    e.Use();
                }
                else if (e.button == 2) // Middle mouse: pan timeline
                {
                    scrollPosition.x -= e.delta.x;
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (isScrubbing)
                {
                    isScrubbing = false;
                    e.Use();
                }
                else if (isPlacingHold)
                {
                    AddNote(holdLane, holdStartBeat, ChartNoteType.Hold, holdPreviewLength);
                    isPlacingHold = false;
                    e.Use();
                }
                break;

            case EventType.MouseLeaveWindow:
                isScrubbing = false;
                isPlacingHold = false;
                break;
        }
    }

    private void BeginPlaceNote(Vector2 mousePosition)
    {
        int lane = LaneFromY(mousePosition.y);
        if (lane < 0) return;

        float beat = SnapBeat(mousePosition.x);

        if (selectedNoteType == ChartNoteType.Tap)
        {
            AddNote(lane, beat, ChartNoteType.Tap, 0f);
        }
        else
        {
            isPlacingHold = true;
            holdLane = lane;
            holdStartBeat = beat;
            holdPreviewLength = 1f / snapDivision;
        }
    }

    private void SetTimeFromMouse(float localX)
    {
        // mousePosition di dalam ScrollView sudah lokal, TIDAK perlu ditambah scrollPosition
        SeekTo(localX / pixelsPerSecond);
    }

    // private void DrawPlayheadControls()
    // {
    //     EditorGUILayout.Space(10);
    //     EditorGUILayout.BeginVertical("box");
    //     EditorGUILayout.LabelField("Playhead Control", EditorStyles.boldLabel);

    //     EditorGUILayout.BeginHorizontal();
    //     EditorGUILayout.LabelField("Current Time:", GUILayout.Width(100));

    //     float newTime = EditorGUILayout.Slider(currentTime, 0f, duration);
    //     if (newTime != currentTime)
    //     {
    //         SeekTo(newTime);
    //     }

    //     EditorGUILayout.LabelField($"{currentTime:F2}s", GUILayout.Width(60));
    //     EditorGUILayout.EndHorizontal();

    //     // Display current beat
    //     float secondsPerBeat = 60f / bpm;
    //     float currentBeat = currentTime / secondsPerBeat;
    //     EditorGUILayout.LabelField($"Current Beat: {currentBeat:F2}", EditorStyles.miniLabel);

    //     EditorGUILayout.EndVertical();
    // }

    // ---------------------------------------------------------------------
    // Playback
    // ---------------------------------------------------------------------

    // Single entry point for moving the playhead (scrub, slider, keyframe jump).
    private void SeekTo(float time)
    {
        currentTime = Mathf.Clamp(time, 0f, duration);

        // If playing, restart from the new position (keeps audio in sync)
        if (isPlaying)
        {
            BeginPlayback();
        }

        Repaint();
    }

    private void PlayAudio()
    {
        // Pressing play at the very end starts over
        if (currentTime >= duration - 0.001f)
        {
            currentTime = 0f;
        }

        BeginPlayback();
    }

    private void BeginPlayback()
    {
        StopPreviewClips();

        if (audioClip != null)
        {
            if (playPreviewClipMethod != null)
            {
                int startSample = Mathf.Clamp((int)(currentTime * audioClip.frequency), 0, Mathf.Max(0, audioClip.samples - 1));
                playPreviewClipMethod.Invoke(null, new object[] { audioClip, startSample, false });
            }
            else
            {
                Debug.LogWarning("AudioUtil.PlayPreviewClip was not found in this Unity version. The playhead moves, but there is no sound.");
            }
        }

        // Without an audio clip the playhead runs on an internal clock, so charts can be tested silently
        audioStartTime = EditorApplication.timeSinceStartup - currentTime;
        isPlaying = true;
    }

    private void StopPreviewClips()
    {
        if (stopAllPreviewClipsMethod != null)
        {
            stopAllPreviewClipsMethod.Invoke(null, null);
        }
    }

    private void PauseAudio()
    {
        StopPreviewClips();
        isPlaying = false;
    }

    private void StopAudio()
    {
        PauseAudio();
        currentTime = 0f;
    }

    // ---------------------------------------------------------------------
    // Formatting
    // ---------------------------------------------------------------------

    private static string FormatBeat(float beat)
    {
        return "B" + beat.ToString("0.00", Inv);
    }

    private static string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float rest = seconds - minutes * 60f;
        return minutes + ":" + rest.ToString("00.0", Inv);
    }

    // ---------------------------------------------------------------------
    // Project
    // ---------------------------------------------------------------------

    private void NewProject()
    {
        StopAudio();

        projectName = "New Chart";
        bpm = 120f;
        duration = 60f;
        currentTime = 0f;
        projectFilePath = "";
        audioClip = null;

        notes.Clear();
        isPlacingHold = false;
        keyframeViewDirty = true;
        keyframeScroll = Vector2.zero;

        Debug.Log("New project created");
        Repaint();
    }

    private void LoadProject()
    {
        string path = EditorUtility.OpenFilePanel("Load Chart Project", Application.dataPath, "json");

        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                StopAudio();

                string jsonContent = File.ReadAllText(path);
                ChartProjectData data = JsonUtility.FromJson<ChartProjectData>(jsonContent);

                projectName = data.projectName;
                bpm = data.bpm;
                duration = data.duration;
                currentTime = 0f;
                projectFilePath = path;

                if (data.laneCount > 0)
                {
                    laneLength = data.laneCount;
                }

                notes = data.notes != null ? new List<ChartNote>(data.notes) : new List<ChartNote>();
                isPlacingHold = false;
                keyframeScroll = Vector2.zero;
                SortNotes();

                // Load audio clip if path exists
                if (!string.IsNullOrEmpty(data.audioClipPath))
                {
                    audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(data.audioClipPath);
                    if (audioClip != null)
                    {
                        duration = audioClip.length;
                    }
                    else
                    {
                        Debug.LogWarning($"Audio clip not found at {data.audioClipPath}");
                    }
                }
                else
                {
                    audioClip = null;
                }

                Debug.Log($"Project loaded: {projectName}");
                Repaint();
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Load Error", $"Failed to load project:\n{e.Message}", "OK");
            }
        }
    }

    private void SaveProject()
    {
        string path = projectFilePath;

        if (string.IsNullOrEmpty(path))
        {
            path = EditorUtility.SaveFilePanel("Save Chart Project", Application.dataPath, projectName, "json");
        }

        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                ChartProjectData data = new ChartProjectData
                {
                    projectName = projectName,
                    bpm = bpm,
                    duration = duration,
                    audioClipPath = audioClip != null ? AssetDatabase.GetAssetPath(audioClip) : "",
                    laneCount = LaneCount,
                    notes = new List<ChartNote>(notes)
                };

                string jsonContent = JsonUtility.ToJson(data, true);
                File.WriteAllText(path, jsonContent);
                projectFilePath = path;

                // Refresh Unity's asset database so the file appears immediately
                AssetDatabase.Refresh();

                Debug.Log($"Project saved: {path}");
                EditorUtility.DisplayDialog("Save Successful", $"Project saved to:\n{path}", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Save Error", $"Failed to save project:\n{e.Message}", "OK");
            }
        }
    }
}

[System.Serializable]
public class ChartProjectData
{
    public string projectName;
    public float bpm;
    public float duration;
    public string audioClipPath; // Store path to audio clip
    public int laneCount;
    public List<ChartNote> notes = new List<ChartNote>();
}
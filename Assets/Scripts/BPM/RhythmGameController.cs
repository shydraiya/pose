using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BeatManager))]
public class RhythmGameController : MonoBehaviour
{
    [Header("Lane Setup")]
    [SerializeField] private Transform[] _laneTargets;
    [SerializeField] private KeyCode[] _laneKeys = { KeyCode.A, KeyCode.S, KeyCode.D };
    [SerializeField] private Vector3 _judgePlateOffset = new Vector3(0f, 0f, 1.2f);
    [SerializeField] private Vector3 _noteHitOffset = new Vector3(0f, 0f, -1.2f);
    [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 7f, 0f);
    [SerializeField] private Vector3 _despawnOffset = new Vector3(0f, -5f, 0f);
    [SerializeField] private Vector3 _noteScale = new Vector3(1.4f, 1.4f, 1.4f);
    [SerializeField] private float _noteBrightnessMultiplier = 1.35f;
    [SerializeField] private float _noteSmoothness = 0.15f;
    [SerializeField] private Vector3 _laneGuideScale = new Vector3(2.4f, 6.5f, 1f);
    [SerializeField] private Vector3 _laneGuideOffset = new Vector3(0f, 3.25f, -0.6f);
    [SerializeField] private float _laneGuideAlpha = 0.035f;

    [Header("Chart")]
    [SerializeField] private float _beatsLeadTime = 4f;
    [SerializeField] private float _songStartBeat = 2f;
    [SerializeField] private int _maxChartedBeats = 64;

    [Header("Timing")]
    [SerializeField] private float _perfectWindow = 0.15f;
    [SerializeField] private float _goodWindow = 0.3f;
    [SerializeField] private float _missWindow = 0.45f;
    [SerializeField] private float _despawnBeatsAfterHitLine = 1.5f;

    [Header("Judge Colors")]
    [SerializeField] private Color _perfectColor = new Color(1f, 0.95f, 0.45f, 1f);
    [SerializeField] private Color _goodColor = new Color(0.35f, 1f, 0.75f, 1f);
    [SerializeField] private Color _missColor = new Color(1f, 0.35f, 0.35f, 1f);

    private readonly List<NoteEvent> _chart = new List<NoteEvent>();
    private readonly List<RhythmLaneNote>[] _laneNotes =
    {
        new List<RhythmLaneNote>(),
        new List<RhythmLaneNote>(),
        new List<RhythmLaneNote>()
    };

    private BeatManager _beatManager;
    private PulseToTheBeat[] _lanePulses;
    private float _lastSongTime;
    private int _nextChartIndex;
    private int _score;
    private int _combo;
    private int _bestCombo;
    private string _lastJudge = "Ready";
    private Transform _laneGuideRoot;

    public float CurrentSongBeat => _beatManager == null ? 0f : _beatManager.SongTime / Mathf.Max(0.0001f, _beatManager.SecondsPerBeat);
    public float MissWindowBeats => SecondsToBeats(_missWindow);

    private void Awake()
    {
        _beatManager = GetComponent<BeatManager>();
        CacheLaneTargets();
        ApplyJudgePlateOffset();
        BuildLaneGuides();
        BuildChart();
    }

    private void Update()
    {
        if (_beatManager == null || _beatManager.AudioSource == null || _beatManager.AudioSource.clip == null)
        {
            return;
        }

        HandleSongLoop();
        SpawnUpcomingNotes();
        HandleInput();
    }

    private void OnDestroy()
    {
        if (_laneGuideRoot != null)
        {
            Destroy(_laneGuideRoot.gameObject);
        }
    }

    private void OnGUI()
    {
        GUI.color = Color.white;
        GUI.Label(new Rect(20f, 20f, 280f, 25f), $"Score: {_score}");
        GUI.Label(new Rect(20f, 45f, 280f, 25f), $"Combo: {_combo}  Best: {_bestCombo}");
        GUI.Label(new Rect(20f, 70f, 280f, 25f), $"Judge: {_lastJudge}");
        GUI.Label(new Rect(20f, 95f, 340f, 25f), $"Keys: {string.Join(" / ", _laneKeys)}");
    }

    private void CacheLaneTargets()
    {
        if (_laneTargets != null && _laneTargets.Length >= 3)
        {
            _lanePulses = new PulseToTheBeat[_laneTargets.Length];
            for (int i = 0; i < _laneTargets.Length; i++)
            {
                _lanePulses[i] = _laneTargets[i].GetComponent<PulseToTheBeat>();
            }

            return;
        }

        PulseToTheBeat[] pulses = FindObjectsOfType<PulseToTheBeat>();
        List<PulseToTheBeat> laneCandidates = new List<PulseToTheBeat>();
        foreach (PulseToTheBeat pulse in pulses)
        {
            if (pulse.gameObject.name.StartsWith("Cube"))
            {
                laneCandidates.Add(pulse);
            }
        }

        laneCandidates.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        int laneCount = Mathf.Min(3, laneCandidates.Count);
        _laneTargets = new Transform[laneCount];
        _lanePulses = new PulseToTheBeat[laneCount];

        for (int i = 0; i < laneCount; i++)
        {
            _laneTargets[i] = laneCandidates[i].transform;
            _lanePulses[i] = laneCandidates[i];
        }
    }

    private void BuildChart()
    {
        _chart.Clear();

        if (_beatManager == null || _beatManager.AudioSource == null || _beatManager.AudioSource.clip == null || _laneTargets == null || _laneTargets.Length == 0)
        {
            return;
        }

        int totalBeatsFromClip = Mathf.FloorToInt(_beatManager.AudioSource.clip.length / Mathf.Max(0.0001f, _beatManager.SecondsPerBeat));
        int totalBeats = Mathf.Min(totalBeatsFromClip, _maxChartedBeats);

        for (int beat = Mathf.CeilToInt(_songStartBeat); beat < totalBeats; beat++)
        {
            int lane = beat % _laneTargets.Length;
            _chart.Add(new NoteEvent(beat, lane));

            if (beat % 4 == 1)
            {
                _chart.Add(new NoteEvent(beat + 0.5f, (lane + 1) % _laneTargets.Length));
            }
            else if (beat % 4 == 3)
            {
                _chart.Add(new NoteEvent(beat + 0.5f, (lane + _laneTargets.Length - 1) % _laneTargets.Length));
            }
        }

        _chart.Sort((a, b) => a.BeatTime.CompareTo(b.BeatTime));
    }

    private void ApplyJudgePlateOffset()
    {
        if (_laneTargets == null)
        {
            return;
        }

        for (int i = 0; i < _laneTargets.Length; i++)
        {
            if (_laneTargets[i] != null)
            {
                _laneTargets[i].position += _judgePlateOffset;
            }
        }
    }

    private void BuildLaneGuides()
    {
        if (_laneTargets == null || _laneTargets.Length == 0)
        {
            return;
        }

        if (_laneGuideRoot != null)
        {
            Destroy(_laneGuideRoot.gameObject);
        }

        GameObject rootObject = new GameObject("LaneGuides");
        rootObject.transform.SetParent(transform, false);
        _laneGuideRoot = rootObject.transform;

        for (int laneIndex = 0; laneIndex < _laneTargets.Length; laneIndex++)
        {
            Transform laneTarget = _laneTargets[laneIndex];
            GameObject guideObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            guideObject.name = $"LaneGuide_{laneIndex}";
            guideObject.transform.SetParent(_laneGuideRoot, false);
            guideObject.transform.position = laneTarget.position + _laneGuideOffset;
            guideObject.transform.localScale = _laneGuideScale;
            guideObject.transform.rotation = Quaternion.identity;

            Collider guideCollider = guideObject.GetComponent<Collider>();
            if (guideCollider != null)
            {
                Destroy(guideCollider);
            }

            MeshRenderer guideRenderer = guideObject.GetComponent<MeshRenderer>();
            MeshRenderer laneRenderer = laneTarget.GetComponent<MeshRenderer>();
            if (guideRenderer != null)
            {
                Material guideMaterial = laneRenderer != null
                    ? new Material(laneRenderer.sharedMaterial)
                    : new Material(Shader.Find("Universal Render Pipeline/Lit"));

                ConfigureGuideMaterialForTransparency(guideMaterial);

                if (guideMaterial.HasProperty("_BaseColor"))
                {
                    Color laneColor = guideMaterial.GetColor("_BaseColor");
                    laneColor.a = _laneGuideAlpha;
                    guideMaterial.SetColor("_BaseColor", laneColor);
                }
                else if (guideMaterial.HasProperty("_Color"))
                {
                    Color laneColor = guideMaterial.color;
                    laneColor.a = _laneGuideAlpha;
                    guideMaterial.color = laneColor;
                }

                guideRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                guideRenderer.receiveShadows = false;
                guideRenderer.sharedMaterial = guideMaterial;
            }
        }
    }

    private void ConfigureGuideMaterialForTransparency(Material guideMaterial)
    {
        if (guideMaterial == null)
        {
            return;
        }

        if (guideMaterial.HasProperty("_Surface"))
        {
            guideMaterial.SetFloat("_Surface", 1f);
        }

        if (guideMaterial.HasProperty("_Blend"))
        {
            guideMaterial.SetFloat("_Blend", 0f);
        }

        if (guideMaterial.HasProperty("_ZWrite"))
        {
            guideMaterial.SetFloat("_ZWrite", 0f);
        }

        if (guideMaterial.HasProperty("_SrcBlend"))
        {
            guideMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (guideMaterial.HasProperty("_DstBlend"))
        {
            guideMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        guideMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        guideMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        guideMaterial.DisableKeyword("_ALPHATEST_ON");
        guideMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private Material CreateNoteMaterial(Material sourceMaterial)
    {
        Shader shader = sourceMaterial != null ? sourceMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
        Material noteMaterial = new Material(shader);
        Color sourceColor = Color.white;

        if (sourceMaterial != null)
        {
            if (sourceMaterial.HasProperty("_BaseColor"))
            {
                sourceColor = sourceMaterial.GetColor("_BaseColor");
            }
            else if (sourceMaterial.HasProperty("_Color"))
            {
                sourceColor = sourceMaterial.color;
            }
        }

        Color noteColor = new Color(
            Mathf.Clamp01(sourceColor.r * _noteBrightnessMultiplier),
            Mathf.Clamp01(sourceColor.g * _noteBrightnessMultiplier),
            Mathf.Clamp01(sourceColor.b * _noteBrightnessMultiplier),
            1f);

        if (noteMaterial.HasProperty("_Surface"))
        {
            noteMaterial.SetFloat("_Surface", 0f);
        }

        if (noteMaterial.HasProperty("_Blend"))
        {
            noteMaterial.SetFloat("_Blend", 0f);
        }

        if (noteMaterial.HasProperty("_ZWrite"))
        {
            noteMaterial.SetFloat("_ZWrite", 1f);
        }

        if (noteMaterial.HasProperty("_SrcBlend"))
        {
            noteMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        }

        if (noteMaterial.HasProperty("_DstBlend"))
        {
            noteMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        }

        if (noteMaterial.HasProperty("_Smoothness"))
        {
            noteMaterial.SetFloat("_Smoothness", _noteSmoothness);
        }

        if (noteMaterial.HasProperty("_BaseColor"))
        {
            noteMaterial.SetColor("_BaseColor", noteColor);
        }

        if (noteMaterial.HasProperty("_Color"))
        {
            noteMaterial.color = noteColor;
        }

        noteMaterial.renderQueue = -1;
        noteMaterial.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        noteMaterial.DisableKeyword("_ALPHATEST_ON");
        noteMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        return noteMaterial;
    }

    private void HandleSongLoop()
    {
        float songTime = _beatManager.SongTime;
        if (songTime + 0.1f < _lastSongTime)
        {
            ResetRunState();
        }

        _lastSongTime = songTime;
    }

    private void SpawnUpcomingNotes()
    {
        while (_nextChartIndex < _chart.Count && _chart[_nextChartIndex].BeatTime <= CurrentSongBeat + _beatsLeadTime)
        {
            SpawnNote(_chart[_nextChartIndex]);
            _nextChartIndex++;
        }
    }

    private void SpawnNote(NoteEvent noteEvent)
    {
        if (noteEvent.LaneIndex >= _laneTargets.Length)
        {
            return;
        }

        Transform laneTarget = _laneTargets[noteEvent.LaneIndex];
        GameObject noteObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        noteObject.name = $"Note_{noteEvent.LaneIndex}_{noteEvent.BeatTime:0.00}";

        Collider noteCollider = noteObject.GetComponent<Collider>();
        if (noteCollider != null)
        {
            Destroy(noteCollider);
        }

        RhythmLaneNote laneNote = noteObject.AddComponent<RhythmLaneNote>();
        MeshRenderer laneRenderer = laneTarget.GetComponent<MeshRenderer>();
        Material noteMaterial = CreateNoteMaterial(laneRenderer == null ? null : laneRenderer.sharedMaterial);
        Vector3 hitPosition = laneTarget.position + _noteHitOffset;

        laneNote.Initialize(
            this,
            laneTarget,
            noteEvent.LaneIndex,
            noteEvent.BeatTime,
            noteEvent.BeatTime - _beatsLeadTime,
            noteEvent.BeatTime + _despawnBeatsAfterHitLine,
            hitPosition,
            hitPosition + _spawnOffset,
            hitPosition + _despawnOffset,
            noteMaterial,
            _noteScale);

        _laneNotes[noteEvent.LaneIndex].Add(laneNote);
    }

    private void HandleInput()
    {
        for (int laneIndex = 0; laneIndex < _laneKeys.Length && laneIndex < _laneTargets.Length; laneIndex++)
        {
            if (Input.GetKeyDown(_laneKeys[laneIndex]))
            {
                TriggerLanePulse(laneIndex);
                TryHitLane(laneIndex);
            }
        }
    }

    private void TryHitLane(int laneIndex)
    {
        CleanupResolvedNotes(laneIndex);
        if (_laneNotes[laneIndex].Count == 0)
        {
            RegisterJudge("Miss", 0, false, laneIndex);
            return;
        }

        RhythmLaneNote note = _laneNotes[laneIndex][0];
        float beatDelta = Mathf.Abs(note.BeatTime - CurrentSongBeat);

        if (beatDelta <= SecondsToBeats(_perfectWindow))
        {
            ResolveHit(note, laneIndex, "Perfect", 300);
            return;
        }

        if (beatDelta <= SecondsToBeats(_goodWindow))
        {
            ResolveHit(note, laneIndex, "Good", 150);
            return;
        }

        RegisterJudge("Miss", 0, false, laneIndex);
    }

    private void ResolveHit(RhythmLaneNote note, int laneIndex, string judge, int points)
    {
        note.MarkJudged();
        note.Resolve();
        CleanupResolvedNotes(laneIndex);
        RegisterJudge(judge, points, true, laneIndex);
    }

    public void ResolveMiss(RhythmLaneNote note)
    {
        int laneIndex = note.LaneIndex;
        RemoveLaneNote(note, laneIndex);
        RegisterJudge("Miss", 0, false, laneIndex);
    }

    private void RegisterJudge(string judge, int points, bool hit, int laneIndex)
    {
        _lastJudge = judge;
        _score += points;
        ShowJudgeColor(judge, laneIndex);

        if (hit)
        {
            _combo++;
            _bestCombo = Mathf.Max(_bestCombo, _combo);
        }
        else
        {
            _combo = 0;
        }
    }

    private void TriggerLanePulse(int laneIndex)
    {
        if (_lanePulses != null && laneIndex < _lanePulses.Length && _lanePulses[laneIndex] != null)
        {
            _lanePulses[laneIndex].Pulse();
        }
    }

    private void ShowJudgeColor(string judge, int laneIndex)
    {
        if (_lanePulses == null || laneIndex >= _lanePulses.Length || _lanePulses[laneIndex] == null)
        {
            return;
        }

        if (judge == "Perfect")
        {
            _lanePulses[laneIndex].PulseWithColor(_perfectColor);
            return;
        }

        if (judge == "Good")
        {
            _lanePulses[laneIndex].PulseWithColor(_goodColor);
            return;
        }

        _lanePulses[laneIndex].PulseWithColor(_missColor);
    }

    private void CleanupResolvedNotes(int laneIndex)
    {
        _laneNotes[laneIndex].RemoveAll(note => note == null || note.IsResolved || !note.CanBeHit);
    }

    private void RemoveLaneNote(RhythmLaneNote note, int laneIndex)
    {
        if (laneIndex < 0 || laneIndex >= _laneNotes.Length)
        {
            return;
        }

        _laneNotes[laneIndex].Remove(note);
    }

    private void ResetRunState()
    {
        _lastSongTime = 0f;
        _nextChartIndex = 0;
        _combo = 0;
        _lastJudge = "Loop Restart";

        for (int laneIndex = 0; laneIndex < _laneNotes.Length; laneIndex++)
        {
            foreach (RhythmLaneNote note in _laneNotes[laneIndex])
            {
                if (note != null)
                {
                    Destroy(note.gameObject);
                }
            }

            _laneNotes[laneIndex].Clear();
        }
    }

    private float SecondsToBeats(float seconds)
    {
        return seconds / Mathf.Max(0.0001f, _beatManager.SecondsPerBeat);
    }

    private struct NoteEvent
    {
        public NoteEvent(float beatTime, int laneIndex)
        {
            BeatTime = beatTime;
            LaneIndex = laneIndex;
        }

        public float BeatTime { get; }
        public int LaneIndex { get; }
    }
}

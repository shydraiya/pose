using UnityEngine;

public class RhythmLaneNote : MonoBehaviour
{
    private RhythmGameController _controller;
    private Transform _target;
    private float _beatTime;
    private float _spawnBeat;
    private float _despawnBeat;
    private Vector3 _hitPosition;
    private Vector3 _spawnPosition;
    private Vector3 _despawnPosition;
    private bool _isResolved;
    private bool _hasBeenJudged;

    public int LaneIndex { get; private set; }
    public float BeatTime => _beatTime;
    public bool IsResolved => _isResolved;
    public bool CanBeHit => !_isResolved && !_hasBeenJudged;

    public void Initialize(
        RhythmGameController controller,
        Transform target,
        int laneIndex,
        float beatTime,
        float spawnBeat,
        float despawnBeat,
        Vector3 hitPosition,
        Vector3 spawnPosition,
        Vector3 despawnPosition,
        Material laneMaterial,
        Vector3 scale)
    {
        _controller = controller;
        _target = target;
        LaneIndex = laneIndex;
        _beatTime = beatTime;
        _spawnBeat = spawnBeat;
        _despawnBeat = despawnBeat;
        _hitPosition = hitPosition;
        _spawnPosition = spawnPosition;
        _despawnPosition = despawnPosition;

        transform.position = spawnPosition;
        transform.localScale = scale;
        name = $"LaneNote_{laneIndex}_{beatTime:0.00}";

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null && laneMaterial != null)
        {
            meshRenderer.sharedMaterial = laneMaterial;
        }
    }

    private void Update()
    {
        if (_controller == null || _target == null)
        {
            Destroy(gameObject);
            return;
        }

        float currentBeat = _controller.CurrentSongBeat;

        if (currentBeat <= _beatTime)
        {
            float approachProgress = Mathf.InverseLerp(_spawnBeat, _beatTime, currentBeat);
            transform.position = Vector3.Lerp(_spawnPosition, _hitPosition, approachProgress);
        }
        else
        {
            float exitProgress = Mathf.InverseLerp(_beatTime, _despawnBeat, currentBeat);
            transform.position = Vector3.Lerp(_hitPosition, _despawnPosition, exitProgress);
        }

        if (!_hasBeenJudged && currentBeat - _beatTime > _controller.MissWindowBeats)
        {
            _hasBeenJudged = true;
            _controller.ResolveMiss(this);
        }

        if (!_isResolved && currentBeat >= _despawnBeat)
        {
            Resolve();
        }
    }

    public void MarkJudged()
    {
        _hasBeenJudged = true;
    }

    public void Resolve()
    {
        if (_isResolved)
        {
            return;
        }

        _isResolved = true;
        Destroy(gameObject);
    }
}

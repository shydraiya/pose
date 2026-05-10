using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class PoseHandProxy : MonoBehaviour
{
    [SerializeField] private float _followSpeed = 20f;
    [SerializeField] private float _upwardBias = 0.15f;

    private BoxCollider _boxCollider;
    private Rigidbody _rigidbody;
    private Vector3 _previousPosition;

    public Vector3 Velocity { get; private set; }
    public Vector3 SurfaceNormal { get; private set; } = Vector3.up;
    public Vector3 SurfaceTangent { get; private set; } = Vector3.right;

    private void Awake()
    {
        _boxCollider = GetComponent<BoxCollider>();
        _boxCollider.isTrigger = false;
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        _previousPosition = transform.position;
    }

    public void SetPlateSize(Vector3 size)
    {
        if (_boxCollider == null)
        {
            _boxCollider = GetComponent<BoxCollider>();
        }

        _boxCollider.size = size;
    }

    public void SetTrackedState(bool isTracked, Vector3 targetPosition, bool hasElbowPosition, Vector3 elbowPosition)
    {
        if (!isTracked)
        {
            Velocity = Vector3.zero;
            SurfaceNormal = Vector3.up;
            SurfaceTangent = Vector3.right;
            gameObject.SetActive(false);
            return;
        }

        UpdateSurfaceOrientation(hasElbowPosition, targetPosition, elbowPosition);

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            _rigidbody.position = targetPosition;
            _previousPosition = targetPosition;
            Velocity = Vector3.zero;
            return;
        }

        Vector3 nextPosition = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * _followSpeed);
        Velocity = (nextPosition - _previousPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        _rigidbody.MovePosition(nextPosition);
        _previousPosition = nextPosition;
    }

    private void UpdateSurfaceOrientation(bool hasElbowPosition, Vector3 wristPosition, Vector3 elbowPosition)
    {
        if (!hasElbowPosition)
        {
            SurfaceNormal = Vector3.up;
            SurfaceTangent = Vector3.right;
            _rigidbody.MoveRotation(Quaternion.identity);
            return;
        }

        Vector3 forearmDirection = wristPosition - elbowPosition;
        if (forearmDirection.sqrMagnitude < 0.0001f)
        {
            SurfaceNormal = Vector3.up;
            SurfaceTangent = Vector3.right;
            _rigidbody.MoveRotation(Quaternion.identity);
            return;
        }

        forearmDirection.z = 0f;
        SurfaceTangent = forearmDirection.normalized;

        Vector3 perpendicular = new Vector3(-SurfaceTangent.y, SurfaceTangent.x, 0f);
        if (perpendicular.y < 0f)
        {
            perpendicular = -perpendicular;
        }

        Vector3 normalWithBias = perpendicular + Vector3.up * _upwardBias;
        SurfaceNormal = normalWithBias.sqrMagnitude < 0.0001f ? Vector3.up : normalWithBias.normalized;

        _rigidbody.MoveRotation(Quaternion.LookRotation(Vector3.forward, SurfaceNormal));
    }
}

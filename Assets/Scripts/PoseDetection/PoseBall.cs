using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class PoseBall : MonoBehaviour
{
    [Header("Bounce")]
    [SerializeField] private float _bounceSpeed = 5f;
    [SerializeField] private float _minimumUpwardDirection = 0.35f;
    [SerializeField] private float _handVelocityInfluence = 0.002f;
    [SerializeField] private float _minimumIncomingSpeed = 2f;

    [Header("Fall")]
    [SerializeField] private float _fallGravityMultiplier = 1f;

    [Header("Movement Plane")]
    [SerializeField] private bool _lockToXYPlane = true;

    [Header("Bounds")]
    [SerializeField] private float _respawnTopY = 1.2f;
    [SerializeField] private float _outOfBoundsY = -1.2f;
    [SerializeField] private float _outOfBoundsX = 1.4f;
    [SerializeField] private float _outOfBoundsZ = 1f;
    [SerializeField] private bool _useCameraViewBounds = true;
    [SerializeField] private Camera _boundsCamera;
    [SerializeField] private bool _showResetBounds = true;
    [SerializeField] private Color _boundsColor = new Color(1f, 0.85f, 0.2f, 0.85f);
    [SerializeField] private float _boundsLineWidth = 0.02f;

    private Rigidbody _rigidbody;
    private Vector3 _spawnPosition;
    private float _movementPlaneZ;
    private int _bounceCount;
    private TextMeshProUGUI _bounceText;
    private LineRenderer _resetBoundsLine;
    private Material _resetBoundsMaterial;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.useGravity = false;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void Start()
    {
        CreateBounceHud();
        _spawnPosition = transform.position;
        _movementPlaneZ = _spawnPosition.z;
        ApplyMovementConstraints();
        if (_boundsCamera == null)
        {
            _boundsCamera = Camera.main;
        }

        CreateResetBoundsView();
        Respawn();
    }

    private void Update()
    {
        KeepOnMovementPlane();
        ApplyExtraGravity();
        UpdateResetBoundsView();

        if (IsOutOfBounds())
        {
            Respawn();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        PoseHandProxy handProxy = collision.collider.GetComponent<PoseHandProxy>();
        if (handProxy == null)
        {
            return;
        }

        BounceFrom(handProxy, collision);
    }

    public void Respawn()
    {
        transform.position = GetPlanarPosition(_spawnPosition);
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _bounceCount = 0;
        UpdateBounceHud();
    }

    private void ApplyExtraGravity()
    {
        if (Mathf.Approximately(_fallGravityMultiplier, 0f))
        {
            return;
        }

        Vector3 scaledGravity = Physics.gravity * _fallGravityMultiplier;
        if (_lockToXYPlane)
        {
            scaledGravity.z = 0f;
        }

        _rigidbody.AddForce(scaledGravity, ForceMode.Acceleration);
    }

    private void BounceFrom(PoseHandProxy handProxy, Collision collision)
    {
        Vector3 incomingVelocity = GetPlanarVector(_rigidbody.linearVelocity);
        if (incomingVelocity.sqrMagnitude < _minimumIncomingSpeed * _minimumIncomingSpeed)
        {
            incomingVelocity = Vector3.down * _minimumIncomingSpeed;
        }

        Vector3 surfaceNormal = collision.contactCount > 0 ? collision.GetContact(0).normal : handProxy.SurfaceNormal;
        surfaceNormal = GetPlanarVector(surfaceNormal);
        if (surfaceNormal.sqrMagnitude < 0.0001f)
        {
            surfaceNormal = Vector3.up;
        }
        else
        {
            surfaceNormal.Normalize();
        }

        if (Vector3.Dot(incomingVelocity, surfaceNormal) > 0f)
        {
            surfaceNormal = -surfaceNormal;
        }

        Vector3 reflectedDirection = Vector3.Reflect(incomingVelocity.normalized, surfaceNormal);
        if (reflectedDirection.y < _minimumUpwardDirection)
        {
            reflectedDirection.y = _minimumUpwardDirection;
            reflectedDirection.Normalize();
        }

        Vector3 nextVelocity = reflectedDirection * _bounceSpeed;
        nextVelocity += GetPlanarVector(handProxy.Velocity) * _handVelocityInfluence;

        if (nextVelocity.y < _bounceSpeed * _minimumUpwardDirection)
        {
            nextVelocity.y = _bounceSpeed * _minimumUpwardDirection;
        }

        _rigidbody.linearVelocity = GetPlanarVector(nextVelocity);
        _bounceCount++;
        UpdateBounceHud();
    }

    private void ApplyMovementConstraints()
    {
        if (!_lockToXYPlane)
        {
            return;
        }

        _rigidbody.constraints |= RigidbodyConstraints.FreezePositionZ;
        KeepOnMovementPlane();
    }

    private void KeepOnMovementPlane()
    {
        if (!_lockToXYPlane)
        {
            return;
        }

        Vector3 position = transform.position;
        if (!Mathf.Approximately(position.z, _movementPlaneZ))
        {
            position.z = _movementPlaneZ;
            transform.position = position;
        }

        _rigidbody.linearVelocity = GetPlanarVector(_rigidbody.linearVelocity);
    }

    private Vector3 GetPlanarPosition(Vector3 position)
    {
        if (_lockToXYPlane)
        {
            position.z = _movementPlaneZ;
        }

        return position;
    }

    private Vector3 GetPlanarVector(Vector3 vector)
    {
        if (_lockToXYPlane)
        {
            vector.z = 0f;
        }

        return vector;
    }

    private void CreateBounceHud()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("PoseBallHUD");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        GameObject panelObject = new GameObject("BouncePanel");
        panelObject.transform.SetParent(canvas.transform, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(20f, -20f);
        panelRect.sizeDelta = new Vector2(240f, 64f);

        GameObject textObject = new GameObject("BounceText");
        textObject.transform.SetParent(panelObject.transform, false);
        _bounceText = textObject.AddComponent<TextMeshProUGUI>();
        _bounceText.fontSize = 30f;
        _bounceText.alignment = TextAlignmentOptions.Center;
        _bounceText.color = Color.white;
        _bounceText.text = "Bounces: 0";

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 8f);
        textRect.offsetMax = new Vector2(-12f, -8f);

        UpdateBounceHud();
    }

    private bool IsOutOfBounds()
    {
        if (_useCameraViewBounds && _boundsCamera != null)
        {
            Vector3 viewportPosition = _boundsCamera.WorldToViewportPoint(transform.position);
            return viewportPosition.z <= _boundsCamera.nearClipPlane ||
                   viewportPosition.z >= _boundsCamera.farClipPlane ||
                   viewportPosition.x < 0f ||
                   viewportPosition.x > 1f ||
                   viewportPosition.y < 0f ||
                   viewportPosition.y > 1f;
        }

        return transform.position.y < _outOfBoundsY ||
               Mathf.Abs(transform.position.x) > _outOfBoundsX ||
               Mathf.Abs(transform.position.z) > _outOfBoundsZ;
    }

    private void CreateResetBoundsView()
    {
        if (!_showResetBounds)
        {
            return;
        }

        GameObject boundsObject = new GameObject("CameraResetBounds");
        boundsObject.transform.SetParent(transform, false);
        _resetBoundsLine = boundsObject.AddComponent<LineRenderer>();
        _resetBoundsLine.useWorldSpace = true;
        _resetBoundsLine.loop = true;
        _resetBoundsLine.positionCount = 4;
        _resetBoundsLine.startWidth = _boundsLineWidth;
        _resetBoundsLine.endWidth = _boundsLineWidth;
        _resetBoundsLine.numCornerVertices = 3;
        _resetBoundsLine.numCapVertices = 3;
        _resetBoundsLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _resetBoundsLine.receiveShadows = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        _resetBoundsMaterial = new Material(shader);
        if (_resetBoundsMaterial.HasProperty("_BaseColor"))
        {
            _resetBoundsMaterial.SetColor("_BaseColor", _boundsColor);
        }

        if (_resetBoundsMaterial.HasProperty("_Color"))
        {
            _resetBoundsMaterial.color = _boundsColor;
        }

        _resetBoundsLine.sharedMaterial = _resetBoundsMaterial;
    }

    private void UpdateResetBoundsView()
    {
        if (_resetBoundsLine == null)
        {
            return;
        }

        bool canShowCameraBounds = _useCameraViewBounds && _boundsCamera != null;
        _resetBoundsLine.gameObject.SetActive(_showResetBounds && canShowCameraBounds);
        if (!canShowCameraBounds)
        {
            return;
        }

        float depth = Vector3.Dot(_spawnPosition - _boundsCamera.transform.position, _boundsCamera.transform.forward);
        depth = Mathf.Clamp(depth, _boundsCamera.nearClipPlane + 0.01f, _boundsCamera.farClipPlane - 0.01f);

        _resetBoundsLine.SetPosition(0, _boundsCamera.ViewportToWorldPoint(new Vector3(0f, 0f, depth)));
        _resetBoundsLine.SetPosition(1, _boundsCamera.ViewportToWorldPoint(new Vector3(0f, 1f, depth)));
        _resetBoundsLine.SetPosition(2, _boundsCamera.ViewportToWorldPoint(new Vector3(1f, 1f, depth)));
        _resetBoundsLine.SetPosition(3, _boundsCamera.ViewportToWorldPoint(new Vector3(1f, 0f, depth)));
    }

    private void UpdateBounceHud()
    {
        if (_bounceText != null)
        {
            _bounceText.text = $"Bounces: {_bounceCount}";
        }
    }

    private void OnDestroy()
    {
        if (_resetBoundsMaterial != null)
        {
            Destroy(_resetBoundsMaterial);
        }
    }
}

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

    [Header("Bounds")]
    [SerializeField] private float _respawnTopY = 1.2f;
    [SerializeField] private float _outOfBoundsY = -1.2f;
    [SerializeField] private float _outOfBoundsX = 1.4f;
    [SerializeField] private float _outOfBoundsZ = 1f;

    private Rigidbody _rigidbody;
    private Vector3 _spawnPosition;
    private int _bounceCount;
    private TextMeshProUGUI _bounceText;

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
        Respawn();
    }

    private void Update()
    {
        ApplyExtraGravity();

        if (transform.position.y < _outOfBoundsY ||
            Mathf.Abs(transform.position.x) > _outOfBoundsX ||
            Mathf.Abs(transform.position.z) > _outOfBoundsZ)
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
        transform.position = _spawnPosition;
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
        _rigidbody.AddForce(scaledGravity, ForceMode.Acceleration);
    }

    private void BounceFrom(PoseHandProxy handProxy, Collision collision)
    {
        Vector3 incomingVelocity = _rigidbody.linearVelocity;
        if (incomingVelocity.sqrMagnitude < _minimumIncomingSpeed * _minimumIncomingSpeed)
        {
            incomingVelocity = Vector3.down * _minimumIncomingSpeed;
        }

        Vector3 surfaceNormal = collision.contactCount > 0 ? collision.GetContact(0).normal : handProxy.SurfaceNormal;
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
        nextVelocity += handProxy.Velocity * _handVelocityInfluence;

        if (nextVelocity.y < _bounceSpeed * _minimumUpwardDirection)
        {
            nextVelocity.y = _bounceSpeed * _minimumUpwardDirection;
        }

        _rigidbody.linearVelocity = nextVelocity;
        _bounceCount++;
        UpdateBounceHud();
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

    private void UpdateBounceHud()
    {
        if (_bounceText != null)
        {
            _bounceText.text = $"Bounces: {_bounceCount}";
        }
    }
}

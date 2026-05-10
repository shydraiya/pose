using UnityEngine;

public class PoseBallGameController : MonoBehaviour
{
    [SerializeField] private PoseManager _poseManager;
    [SerializeField] private PoseBall _poseBall;
    [SerializeField] private Vector3 _handPlateSize = new Vector3(0.42f, 0.08f, 0.18f);
    [SerializeField] private PrimitiveType _handPrimitive = PrimitiveType.Cube;
    [SerializeField] private bool _showHandPaddles = true;
    [SerializeField] private Color _leftHandColor = new Color(0.2f, 0.85f, 1f, 0.4f);
    [SerializeField] private Color _rightHandColor = new Color(1f, 0.55f, 0.25f, 0.4f);

    private PoseHandProxy _leftHandProxy;
    private PoseHandProxy _rightHandProxy;

    private void Awake()
    {
        _leftHandProxy = CreateHandProxy("LeftHandProxy", _leftHandColor);
        _rightHandProxy = CreateHandProxy("RightHandProxy", _rightHandColor);

        if (_poseBall == null)
        {
            _poseBall = FindFirstObjectByType<PoseBall>();
        }
    }

    private void Update()
    {
        if (_poseManager == null)
        {
            return;
        }

        bool hasLeftHand = _poseManager.TryGetLeftHandPosition(out Vector3 leftHandPosition);
        bool hasLeftElbow = _poseManager.TryGetLeftElbowPosition(out Vector3 leftElbowPosition);
        bool hasRightHand = _poseManager.TryGetRightHandPosition(out Vector3 rightHandPosition);
        bool hasRightElbow = _poseManager.TryGetRightElbowPosition(out Vector3 rightElbowPosition);

        UpdateHandProxy(_leftHandProxy, hasLeftHand, leftHandPosition, hasLeftElbow, leftElbowPosition);
        UpdateHandProxy(_rightHandProxy, hasRightHand, rightHandPosition, hasRightElbow, rightElbowPosition);
    }

    private PoseHandProxy CreateHandProxy(string objectName, Color paddleColor)
    {
        GameObject handObject = GameObject.CreatePrimitive(_handPrimitive);
        handObject.name = objectName;
        handObject.transform.SetParent(transform, false);
        handObject.transform.localScale = _handPlateSize;

        MeshRenderer meshRenderer = handObject.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = _showHandPaddles;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sharedMaterial = CreateVisiblePaddleMaterial(paddleColor);
        }

        Collider colliderComponent = handObject.GetComponent<Collider>();
        if (colliderComponent != null)
        {
            Destroy(colliderComponent);
        }

        BoxCollider boxCollider = handObject.AddComponent<BoxCollider>();
        boxCollider.isTrigger = false;

        Rigidbody rigidbody = handObject.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        PoseHandProxy handProxy = handObject.AddComponent<PoseHandProxy>();
        handProxy.SetPlateSize(_handPlateSize);
        handObject.SetActive(false);
        return handProxy;
    }

    private Material CreateVisiblePaddleMaterial(Color paddleColor)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", paddleColor);
        }

        if (material.HasProperty("_Color"))
        {
            material.color = paddleColor;
        }

        return material;
    }

    private void UpdateHandProxy(PoseHandProxy handProxy, bool isTracked, Vector3 handPosition, bool hasElbowPosition, Vector3 elbowPosition)
    {
        if (handProxy == null)
        {
            return;
        }

        handProxy.SetTrackedState(isTracked, handPosition, hasElbowPosition, elbowPosition);
    }
}

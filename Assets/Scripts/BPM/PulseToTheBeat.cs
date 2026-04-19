using System.Collections;
using UnityEngine;

public class PulseToTheBeat : MonoBehaviour
{
    [SerializeField] bool _useTestBeat;
    [SerializeField] float _pulseSize = 1.15f;
    [SerializeField] float _returnSpeed = 5f;
    [SerializeField] float _colorReturnSpeed = 8f;
    private Vector3 _startSize;
    private MeshRenderer _meshRenderer;
    private Material _runtimeMaterial;
    private Color _baseColor = Color.white;
    private Color _currentColor = Color.white;

    private void Start()
    {
        _startSize = transform.localScale;
        _meshRenderer = GetComponent<MeshRenderer>();
        if (_meshRenderer != null)
        {
            _runtimeMaterial = _meshRenderer.material;
            _baseColor = GetMaterialColor(_runtimeMaterial);
            _currentColor = _baseColor;
        }

        if (_useTestBeat)
        {
            StartCoroutine(TestBeat());
        }
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, _startSize, Time.deltaTime * _returnSpeed);

        if (_runtimeMaterial != null)
        {
            _currentColor = Color.Lerp(_currentColor, _baseColor, Time.deltaTime * _colorReturnSpeed);
            SetMaterialColor(_runtimeMaterial, _currentColor);
        }
    }

    public void Pulse()
    {
        transform.localScale = _startSize * _pulseSize;
    }

    public void PulseWithColor(Color color)
    {
        Pulse();

        if (_runtimeMaterial == null)
        {
            return;
        }

        _currentColor = color;
        SetMaterialColor(_runtimeMaterial, _currentColor);
    }

    private Color GetMaterialColor(Material material)
    {
        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        if (material.HasProperty("_Color"))
        {
            return material.color;
        }

        return Color.white;
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.color = color;
        }
    }

    IEnumerator TestBeat()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            Pulse();
        }
    }

}

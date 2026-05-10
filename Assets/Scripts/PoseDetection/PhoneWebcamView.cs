using UnityEngine;
using UnityEngine.UI;

public class PhoneWebcamView : MonoBehaviour
{
    public RawImage rawImage;
    public string targetDeviceNameContains = "DroidCam"; 
    public int requestedWidth = 1280;
    public int requestedHeight = 720;
    public int requestedFPS = 30;
    public bool flipHorizontally = true;
    public bool flipVertically;

    public WebCamTexture WebcamTexture { get; private set; }

    void Start()
    {
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            Debug.LogError("사용 가능한 웹캠이 없습니다.");
            return;
        }

        string selectedName = null;

        foreach (var d in devices)
        {
            Debug.Log("Found webcam: " + d.name);

            if (!string.IsNullOrEmpty(targetDeviceNameContains) &&
                d.name.ToLower().Contains(targetDeviceNameContains.ToLower()))
            {
                selectedName = d.name;
                break;
            }
        }

        if (string.IsNullOrEmpty(selectedName))
        {
            selectedName = devices[0].name;
            Debug.LogWarning("타겟 장치를 못 찾아서 첫 번째 장치를 사용합니다: " + selectedName);
        }

        WebcamTexture = new WebCamTexture(selectedName, requestedWidth, requestedHeight, requestedFPS);
        rawImage.texture = WebcamTexture;
        WebcamTexture.Play();
    }

    void Update()
    {
        ApplyDisplayTransform();
    }

    void ApplyDisplayTransform()
    {
        if (rawImage == null || WebcamTexture == null)
        {
            return;
        }

        rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);

        RectTransform rectTransform = rawImage.rectTransform;
        if (rectTransform != null)
        {
            rectTransform.localEulerAngles = new Vector3(0f, 0f, -WebcamTexture.videoRotationAngle);

            float scaleX = flipHorizontally ? -1f : 1f;
            bool shouldFlipVertically = flipVertically ^ WebcamTexture.videoVerticallyMirrored;
            float scaleY = shouldFlipVertically ? -1f : 1f;
            rectTransform.localScale = new Vector3(scaleX, scaleY, 1f);
        }
    }

    void OnDestroy()
    {
        if (WebcamTexture != null && WebcamTexture.isPlaying)
            WebcamTexture.Stop();
    }
}

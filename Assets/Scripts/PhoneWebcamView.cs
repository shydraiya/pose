using UnityEngine;
using UnityEngine.UI;

public class PhoneWebcamView : MonoBehaviour
{
    public RawImage rawImage;
    public string targetDeviceNameContains = "DroidCam"; 
    public int requestedWidth = 1280;
    public int requestedHeight = 720;
    public int requestedFPS = 30;

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
        rawImage.material.mainTexture = WebcamTexture;
        WebcamTexture.Play();
    }

    void OnDestroy()
    {
        if (WebcamTexture != null && WebcamTexture.isPlaying)
            WebcamTexture.Stop();
    }
}
using UnityEngine;

public class WebcamList : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        foreach (var d in WebCamTexture.devices)
        {
            Debug.Log("Webcam Device: " + d.name);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

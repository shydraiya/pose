using System;
using UnityEngine;

public class ImagePreview : MonoBehaviour
{
    public GameObject imageQuad;
    public bool mirrorHorizontally = true;

    public void SetTexture(Texture texture)
    {
        imageQuad.GetComponent<MeshRenderer>().material.mainTexture = texture;
        var aspectRatio = texture.width / (float)texture.height;
        var mirrorScale = mirrorHorizontally ? -1f : 1f;
        imageQuad.transform.localScale = new Vector3(aspectRatio * mirrorScale, 1f, 1f);
    }
}

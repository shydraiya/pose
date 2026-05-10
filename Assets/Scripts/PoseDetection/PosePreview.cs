using TMPro;
using UnityEngine;

public class PosePreview : MonoBehaviour
{
    public BoundingBox boundingBox;
    public BoundingCircle boundingCircle;
    public Keypoint[] keypoints;

    public bool enable_box = true;
    public bool enable_circle = true;
    public bool enable_points = true;

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }

    public void SetBoundingBox(bool active, Vector3 position, Vector2 size)
    {
        if (enable_box)
            boundingBox.Set(active, position, size);
    }

    public void SetBoundingCircle(bool active, Vector3 position, float radius)
    {
        if(enable_circle)
            boundingCircle.Set(active, position, radius);
    }

    public void SetKeypoint(int index, bool active, Vector3 position)
    {
        if(enable_points)
            keypoints[index].Set(active, position);
    }
}

using UnityEngine;
using Google.AR.Core;

/*public class PathTracking : MonoBehaviour
{
    public GameObject blueCirclePrefab;
    private Vector3 lastPosition;

    void Start()
    {
        lastPosition = transform.position;
    }

    void Update()
    {
        // Get the current position and orientation of the device

        Pose devicePose = Frame.Pose;

        // Calculate the distance between the current position and the last position
        float distance = Vector3.Distance(lastPosition, devicePose.position);

        // If the device has moved a certain distance, create a new blue circle
        if (distance > 0.1f)
        {
            GameObject blueCircle = Instantiate(blueCirclePrefab, devicePose.position, Quaternion.identity);
            blueCircle.transform.parent = transform;
            lastPosition = devicePose.position;
        }
    }
}
*/
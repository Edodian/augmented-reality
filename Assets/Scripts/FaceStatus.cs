using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Drop this on any GameObject in the scene (e.g. XR Origin).
// Face mode:   shows "Face detected" / "No face".
// Marker mode: shows "Marker: <name>" / "Looking for marker...".
// No UI setup needed.
public class FaceStatus : MonoBehaviour
{
    ARFaceManager faceManager;
    ARTrackedImageManager imageManager;
    GUIStyle style;

    void Awake()
    {
        faceManager = FindAnyObjectByType<ARFaceManager>(FindObjectsInactive.Include);
        imageManager = FindAnyObjectByType<ARTrackedImageManager>(FindObjectsInactive.Include);
    }

    void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 56 };
            style.normal.textColor = Color.green;
        }

        string text = "";

        if (faceManager != null && faceManager.enabled)
        {
            int count = faceManager.trackables.count;
            text = count > 0 ? $"Face detected ({count})" : "No face";
        }
        else if (imageManager != null && imageManager.enabled)
        {
            text = "Looking for marker...";
            foreach (var image in imageManager.trackables)
            {
                if (image.trackingState == TrackingState.Tracking)
                {
                    text = $"Marker: {image.referenceImage.name}";
                    break;
                }
            }
        }

        GUI.Label(new Rect(40, 100, Screen.width - 80, 120), text, style);
    }
}

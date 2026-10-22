using UnityEngine;
using UnityEngine.XR.ARFoundation;

// Drop this on any GameObject in the scene (e.g. XR Origin).
// Shows "Face detected" / "No face" in big text. No UI setup needed.
public class FaceStatus : MonoBehaviour
{
    ARFaceManager faceManager;
    GUIStyle style;

    void Awake()
    {
        faceManager = FindAnyObjectByType<ARFaceManager>();
    }

    void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 56 };
            style.normal.textColor = Color.green;
        }

        int count = faceManager != null ? faceManager.trackables.count : 0;
        string text = count > 0 ? $"Face detected ({count})" : "No face";

        GUI.Label(new Rect(40, 100, Screen.width - 80, 120), text, style);
    }
}

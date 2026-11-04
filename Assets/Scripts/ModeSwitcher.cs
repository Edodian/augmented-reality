using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Put this on the XR Origin. Hook the Flip button's OnClick to ToggleMode().
//   Face mode   = front (selfie) camera + face tracking
//   Marker mode = back camera + marker (image) tracking
public class ModeSwitcher : MonoBehaviour
{
    [SerializeField] ARCameraManager cameraManager;
    [SerializeField] ARFaceManager faceManager;
    [SerializeField] ARTrackedImageManager imageManager;

    [Tooltip("Shown only in face mode, e.g. the ButtonBar with the 3 prefab buttons.")]
    [SerializeField] GameObject faceModeUI;

    [Tooltip("Optional: shown only in marker mode.")]
    [SerializeField] GameObject markerModeUI;

    [SerializeField] bool startInFaceMode = true;

    public bool IsFaceMode { get; private set; }

    void Awake()
    {
        if (cameraManager == null) cameraManager = GetComponentInChildren<ARCameraManager>();
        if (faceManager == null) faceManager = GetComponent<ARFaceManager>();
        if (imageManager == null) imageManager = GetComponent<ARTrackedImageManager>();
    }

    void Start()
    {
        SetMode(startInFaceMode);
    }

    // Call this from the Flip button.
    public void ToggleMode()
    {
        SetMode(!IsFaceMode);
    }

    public void SetMode(bool faceMode)
    {
        IsFaceMode = faceMode;

        // Only one tracker on at a time, otherwise ARCore may pick the wrong camera.
        faceManager.enabled = faceMode;
        imageManager.enabled = !faceMode;

        // Hide objects left over from the mode we just left.
        faceManager.SetTrackablesActive(faceMode);
        imageManager.SetTrackablesActive(!faceMode);

        cameraManager.requestedFacingDirection =
            faceMode ? CameraFacingDirection.User : CameraFacingDirection.World;

        if (faceModeUI != null) faceModeUI.SetActive(faceMode);
        if (markerModeUI != null) markerModeUI.SetActive(!faceMode);
    }
}

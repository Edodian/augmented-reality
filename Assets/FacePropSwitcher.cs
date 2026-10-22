using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Put this on the XR Origin (the same object that has AR Face Manager).
// Each UI button calls SelectPrefab(0), SelectPrefab(1) or SelectPrefab(2).
public class FacePropSwitcher : MonoBehaviour
{
    [SerializeField] ARFaceManager faceManager;
    [SerializeField] GameObject[] prefabs = new GameObject[3];

    [Tooltip("Where the object floats, relative to the center of the head (meters).")]
    [SerializeField] Vector3 offset = new Vector3(0f, 0.18f, 0f);

    const string PropName = "FaceProp";
    int selected;

    void Awake()
    {
        if (faceManager == null)
            faceManager = GetComponent<ARFaceManager>();
    }

    // Hook this up to each button's OnClick and pass 0, 1 or 2.
    public void SelectPrefab(int index)
    {
        if (index < 0 || index >= prefabs.Length || prefabs[index] == null)
            return;

        selected = index;

        // Swap the object on every face that's currently tracked.
        foreach (var face in faceManager.trackables)
            SpawnOn(face);
    }

    void Update()
    {
        foreach (var face in faceManager.trackables)
        {
            // New face? Give it the currently selected object.
            var prop = face.transform.Find(PropName);
            if (prop == null)
                prop = SpawnOn(face);

            // Hide the object while the face is lost.
            if (prop != null)
                prop.gameObject.SetActive(face.trackingState == TrackingState.Tracking);
        }
    }

    Transform SpawnOn(ARFace face)
    {
        var old = face.transform.Find(PropName);
        if (old != null)
        {
            old.SetParent(null);
            Destroy(old.gameObject);
        }

        if (prefabs[selected] == null)
            return null;

        var obj = Instantiate(prefabs[selected], face.transform);
        obj.name = PropName;
        obj.transform.localPosition = offset;
        obj.transform.localRotation = Quaternion.identity;
        return obj.transform;
    }
}

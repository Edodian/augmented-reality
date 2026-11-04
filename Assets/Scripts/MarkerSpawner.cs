using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Put this on the XR Origin (the same object as AR Tracked Image Manager).
// Each detected marker gets the prefab whose Marker Name matches the
// image's Name in the Reference Image Library (case and extra spaces are ignored).
// With Show Debug on, yellow text at the bottom of the screen says what it's doing.
public class MarkerSpawner : MonoBehaviour
{
    [Serializable]
    public class MarkerPrefab
    {
        [Tooltip("Name from the Reference Image Library. Case and extra spaces don't matter.")]
        public string markerName;
        public GameObject prefab;
    }

    [SerializeField] ARTrackedImageManager imageManager;
    [SerializeField] List<MarkerPrefab> markerPrefabs = new List<MarkerPrefab>();

    [Tooltip("Optional: used for any marker that has no matching entry above.")]
    [SerializeField] GameObject fallbackPrefab;

    [Tooltip("Position relative to the marker's center, in meters. Y points up out of the marker.")]
    [SerializeField] Vector3 offset = new Vector3(0f, 0.03f, 0f);

    [Tooltip("Hide the object when the marker isn't fully tracked (out of view).")]
    [SerializeField] bool hideWhenLost = true;

    [Tooltip("Shows what the spawner is doing at the bottom of the screen. Turn off for the final demo.")]
    [SerializeField] bool showDebug = true;

    const string PropName = "MarkerProp";
    readonly StringBuilder debugText = new StringBuilder();
    GUIStyle style;

    void Awake()
    {
        if (imageManager == null)
            imageManager = GetComponent<ARTrackedImageManager>();
        if (imageManager == null)
            imageManager = FindAnyObjectByType<ARTrackedImageManager>(FindObjectsInactive.Include);
    }

    void Update()
    {
        debugText.Clear();

        if (imageManager == null)
        {
            debugText.Append("MarkerSpawner: no AR Tracked Image Manager found in the scene");
            return;
        }

        if (!imageManager.enabled)
            return; // face mode

        if (imageManager.trackables.count == 0)
            debugText.Append("MarkerSpawner running, no markers yet");

        foreach (var image in imageManager.trackables)
        {
            string markerName = image.referenceImage.name;

            // New marker? Give it its object.
            var prop = image.transform.Find(PropName);
            if (prop == null)
                prop = SpawnOn(image, markerName);

            bool tracking = image.trackingState == TrackingState.Tracking;
            if (prop != null)
                prop.gameObject.SetActive(!hideWhenLost || tracking);

            if (!showDebug)
                continue;

            debugText.Append($"'{markerName}' [{image.trackingState}] -> ");
            if (prop == null)
            {
                debugText.AppendLine($"NO PREFAB. List: {ListNames()}");
            }
            else
            {
                float scale = prop.lossyScale.x;
                debugText.Append($"spawned, scale {scale:0.00}, {(prop.gameObject.activeSelf ? "visible" : "hidden")}");
                debugText.AppendLine(scale > 0.3f ? "  <- probably too big, camera may be inside it" : "");
            }
        }
    }

    Transform SpawnOn(ARTrackedImage image, string markerName)
    {
        var prefab = FindPrefab(markerName);
        if (prefab == null)
            return null;

        var obj = Instantiate(prefab, image.transform);
        obj.name = PropName;
        obj.transform.localPosition = offset;
        obj.transform.localRotation = Quaternion.identity;
        obj.SetActive(true); // in case the prefab was saved inactive
        return obj.transform;
    }

    GameObject FindPrefab(string markerName)
    {
        string key = Normalize(markerName);
        foreach (var entry in markerPrefabs)
        {
            if (entry != null && entry.prefab != null && Normalize(entry.markerName) == key)
                return entry.prefab;
        }
        return fallbackPrefab;
    }

    static string Normalize(string s) => (s ?? "").Trim().ToLowerInvariant();

    string ListNames()
    {
        if (markerPrefabs.Count == 0)
            return "list is EMPTY";

        var sb = new StringBuilder();
        foreach (var entry in markerPrefabs)
        {
            if (sb.Length > 0) sb.Append(", ");
            if (entry == null) { sb.Append("?"); continue; }
            sb.Append($"'{entry.markerName}'");
            if (entry.prefab == null) sb.Append(" (no prefab!)");
        }
        return sb.ToString();
    }

    void OnGUI()
    {
        if (!showDebug || debugText.Length == 0)
            return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 36, wordWrap = true };
            style.normal.textColor = Color.yellow;
        }

        GUI.Label(new Rect(40, Screen.height - 420, Screen.width - 80, 380), debugText.ToString(), style);
    }
}
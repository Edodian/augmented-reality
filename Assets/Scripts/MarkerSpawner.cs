using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Put this on the XR Origin (the same object as AR Tracked Image Manager).
// Each detected marker gets the prefab whose Marker Name matches the
// image's Name in the Reference Image Library (case and extra spaces are ignored).
//
// Overlap Mode stops several objects from piling up on one card when ARCore
// mistakes a card for more than one reference image:
//   One Per Spot      - only one object per physical card (default)
//   Only One At A Time - only one object on screen, ever
//   Show All          - old behavior
//
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

    public enum OverlapMode { OnePerSpot, OnlyOneAtATime, ShowAll }

    [SerializeField] ARTrackedImageManager imageManager;
    [SerializeField] List<MarkerPrefab> markerPrefabs = new List<MarkerPrefab>();

    [Tooltip("Optional: used for any marker that has no matching entry above.")]
    [SerializeField] GameObject fallbackPrefab;

    [Tooltip("Position relative to the marker's center, in meters. Y points up out of the marker.")]
    [SerializeField] Vector3 offset = new Vector3(0f, 0.03f, 0f);

    [Header("Filtering")]
    [Tooltip("How to handle several markers detected at once.")]
    [SerializeField] OverlapMode overlapMode = OverlapMode.OnePerSpot;

    [Tooltip("Two markers closer than this (times the marker's width) count as the same card.")]
    [Range(0.1f, 2f)]
    [SerializeField] float sameSpotFraction = 0.6f;

    [Tooltip("Hide the object when the marker isn't fully tracked.")]
    [SerializeField] bool hideWhenLost = true;

    [Tooltip("Hide the object when its marker is outside the camera view, even if ARCore still says Tracking.")]
    [SerializeField] bool hideWhenOffScreen = true;

    [Tooltip("Shows what the spawner is doing at the bottom of the screen. Turn off for the final demo.")]
    [SerializeField] bool showDebug = true;

    // Names of the markers whose objects are visible right now (used by FaceStatus).
    public IReadOnlyList<string> VisibleMarkers => visibleNames;

    const string PropName = "MarkerProp";
    const float ScoreSmoothing = 0.5f;  // seconds
    const float Stickiness = 0.2f;      // keeps the shown marker from flickering to a rival
    const float DefaultMarkerWidth = 0.1f;

    class Entry
    {
        public Transform prop;
        public float score;     // 0..1, how steadily this marker has been tracked lately
        public bool shown;
        public string hideReason;
    }

    readonly Dictionary<TrackableId, Entry> entries = new Dictionary<TrackableId, Entry>();
    readonly HashSet<TrackableId> seen = new HashSet<TrackableId>();
    readonly List<TrackableId> stale = new List<TrackableId>();
    readonly List<ARTrackedImage> candidates = new List<ARTrackedImage>();
    readonly List<ARTrackedImage> accepted = new List<ARTrackedImage>();
    readonly List<string> visibleNames = new List<string>();
    readonly StringBuilder debugText = new StringBuilder();
    Camera cam;
    GUIStyle style;

    void Awake()
    {
        if (imageManager == null)
            imageManager = GetComponent<ARTrackedImageManager>();
        if (imageManager == null)
            imageManager = FindAnyObjectByType<ARTrackedImageManager>(FindObjectsInactive.Include);
        cam = Camera.main;
    }

    void Update()
    {
        debugText.Clear();
        visibleNames.Clear();

        if (imageManager == null)
        {
            debugText.Append("MarkerSpawner: no AR Tracked Image Manager found in the scene");
            return;
        }

        if (!imageManager.enabled)
            return; // face mode

        if (imageManager.trackables.count == 0)
            debugText.Append("MarkerSpawner running, no markers yet");

        if (cam == null)
            cam = Camera.main;

        float blend = 1f - Mathf.Exp(-Time.deltaTime / ScoreSmoothing);

        // 1. Spawn objects, update scores, and drop markers that are lost or off screen.
        seen.Clear();
        candidates.Clear();
        foreach (var image in imageManager.trackables)
        {
            var id = image.trackableId;
            seen.Add(id);
            if (!entries.TryGetValue(id, out var e))
            {
                e = new Entry();
                entries[id] = e;
            }

            if (e.prop == null)
                e.prop = image.transform.Find(PropName);
            if (e.prop == null)
                e.prop = SpawnOn(image, image.referenceImage.name);

            bool tracking = image.trackingState == TrackingState.Tracking;
            e.score = Mathf.Lerp(e.score, tracking ? 1f : 0f, blend);

            e.hideReason = null;
            if (e.prop == null)
                e.hideReason = "NO PREFAB. List: " + ListNames();
            else if (hideWhenLost && !tracking)
                e.hideReason = "hidden: lost";
            else if (hideWhenOffScreen && !IsOnScreen(image.transform.position))
                e.hideReason = "hidden: off screen";
            else
                candidates.Add(image);
        }

        // Forget markers ARCore removed.
        stale.Clear();
        foreach (var id in entries.Keys)
            if (!seen.Contains(id)) stale.Add(id);
        foreach (var id in stale)
            entries.Remove(id);

        // 2. Best candidates first: tracking now > steadily tracked > already shown.
        candidates.Sort((a, b) => Priority(b).CompareTo(Priority(a)));

        // 3. Keep the best one per card (or overall), hide the rest.
        accepted.Clear();
        foreach (var image in candidates)
        {
            ARTrackedImage winner = null;
            if (overlapMode == OverlapMode.OnlyOneAtATime && accepted.Count > 0)
            {
                winner = accepted[0];
            }
            else if (overlapMode == OverlapMode.OnePerSpot)
            {
                foreach (var other in accepted)
                {
                    float limit = sameSpotFraction * Mathf.Max(Width(image), Width(other));
                    if (Vector3.Distance(image.transform.position, other.transform.position) < limit)
                    {
                        winner = other;
                        break;
                    }
                }
            }

            if (winner != null)
                entries[image.trackableId].hideReason = $"hidden: same card as '{winner.referenceImage.name}'";
            else
                accepted.Add(image);
        }

        // 4. Apply, and write the debug readout.
        foreach (var image in imageManager.trackables)
        {
            var e = entries[image.trackableId];
            e.shown = e.prop != null && e.hideReason == null;

            if (e.prop != null && e.prop.gameObject.activeSelf != e.shown)
                e.prop.gameObject.SetActive(e.shown);

            if (e.shown)
                visibleNames.Add(image.referenceImage.name);

            if (!showDebug)
                continue;

            debugText.Append($"'{image.referenceImage.name}' [{image.trackingState}] {e.score:0.00} -> ");
            if (e.shown)
            {
                float scale = e.prop.lossyScale.x;
                debugText.Append($"SHOWN, scale {scale:0.00}");
                debugText.AppendLine(scale > 0.3f ? "  <- probably too big, camera may be inside it" : "");
            }
            else
            {
                debugText.AppendLine(e.hideReason);
            }
        }
    }

    float Priority(ARTrackedImage image)
    {
        var e = entries[image.trackableId];
        float p = e.score;
        if (image.trackingState == TrackingState.Tracking) p += 2f;
        if (e.shown) p += Stickiness;
        return p;
    }

    static float Width(ARTrackedImage image)
    {
        float w = Mathf.Max(image.size.x, image.size.y);
        return w > 0.001f ? w : DefaultMarkerWidth;
    }

    bool IsOnScreen(Vector3 worldPosition)
    {
        if (cam == null) return true;
        Vector3 v = cam.WorldToViewportPoint(worldPosition);
        return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
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
        obj.SetActive(false); // shown by Update once it passes the filters
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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;

[RequireComponent(typeof(ARTrackedImageManager))]
public class MultiImageTracker : MonoBehaviour
{
    #region Data Structures
    [Serializable]
    public class CodesData { public string code; public string npm; public string name; }

    [Serializable]
    public class LeaderboardEntry { public string npm; public string name; public long timestamp; }

    [Serializable]
    public class LeaderboardResponse { public bool success; public List<LeaderboardEntry> leaderboard; }

    [Serializable]
    public struct MarkerPrefab { public string imageName; public GameObject prefab; }
    #endregion

    [Header("Prefabs")]
    public List<MarkerPrefab> markerPrefabs = new List<MarkerPrefab>();
    public GameObject podiumPrefab;
    public GameObject errorPrefab;
    public UIDocument uiDocument;

    [Header("Backend Settings")]
    public string backendURL = "https://batikqr.verdex.id/pattern/scan";
    public string leaderboardURL = "https://batikqr.verdex.id/pattern/leaderboard";
    public int requiredCount = 3;

    private ARTrackedImageManager trackedImageManager;
    private List<ARTrackedImage> currentlyTrackedImages = new List<ARTrackedImage>();
    private GameObject activePodium;
    private bool codesSent = false;
    private float _startTime;
    private bool _timerStarted = false;
    private Coroutine _refreshCoroutine;

    [Header("UI Settings")]
    private VisualElement _root;
    private Button _leaderboardButton;
    private Label _statusLabel;

    void Awake() => trackedImageManager = GetComponent<ARTrackedImageManager>();

    void OnEnable()
    {
        if (uiDocument != null)
        {
            _root = uiDocument.rootVisualElement;
            // Gunakan null-coalescing untuk mencegah error saat inisialisasi
            _leaderboardButton = _root.Q<Button>("LeaderboardButton");
            _statusLabel = _root.Q<Label>("StatusLabel");

            if (_leaderboardButton != null) _leaderboardButton.style.display = DisplayStyle.None;
        }

        if (trackedImageManager != null)
            trackedImageManager.trackablesChanged.AddListener(OnChanged);
    }

    void OnDisable()
    {
        if (trackedImageManager != null)
            trackedImageManager.trackablesChanged.RemoveListener(OnChanged);
        if (_refreshCoroutine != null) StopCoroutine(_refreshCoroutine);
    }

    void OnChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            if (!currentlyTrackedImages.Contains(trackedImage))
            {
                if (!_timerStarted) { _startTime = Time.time; _timerStarted = true; }
                currentlyTrackedImages.Add(trackedImage);

                string name = trackedImage.referenceImage.name;
                foreach (var item in markerPrefabs)
                {
                    if (item.imageName == name && trackedImage.transform.childCount == 0)
                    {
                        GameObject island = Instantiate(item.prefab, trackedImage.transform.position, trackedImage.transform.rotation);
                        island.transform.SetParent(trackedImage.transform);
                        island.transform.localRotation = Quaternion.Euler(-90, 180, 0);
                    }
                }
            }
        }

        foreach (var trackedImage in eventArgs.updated)
        {
            bool isVisible = trackedImage.trackingState == TrackingState.Tracking;
            foreach (Transform child in trackedImage.transform)
                child.gameObject.SetActive(isVisible);
        }

        foreach (var kvp in eventArgs.removed)
        {
            currentlyTrackedImages.Remove(kvp.Value);
            if (kvp.Value.transform.childCount > 0) Destroy(kvp.Value.transform.GetChild(0).gameObject);
        }

        if (currentlyTrackedImages.Count >= requiredCount && !codesSent)
        {
            codesSent = true;
            DetectAndSendCodes();
        }
    }

    private void DetectAndSendCodes()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("Main Camera tidak ditemukan!");
            return;
        }

        // Filter null untuk mencegah NullReference saat sorting
        var validImages = currentlyTrackedImages.Where(img => img != null).ToList();
        if (validImages.Count < requiredCount) return;

        var sorted = validImages
            .OrderBy(img => cam.WorldToScreenPoint(img.transform.position).x)
            .ToList();

        string finalCode = string.Join(",", sorted.Select(img => img.referenceImage.name));
        float duration = Time.time - _startTime;
        string timeStr = duration < 1f ? duration.ToString("F1") + "s" : Mathf.RoundToInt(duration).ToString() + "s";

        // Spawn Podium di Marker Tengah (Index 1 dari 3)
        if (podiumPrefab != null && activePodium == null)
        {
            Transform centerMarker = sorted[1].transform;

            // 1. Spawn podium
            activePodium = Instantiate(podiumPrefab, centerMarker.position, Quaternion.identity);

            // 2. Set parent agar ikut bergerak kalau marker geser
            activePodium.transform.SetParent(centerMarker);

            // 3. Reset posisi ke tengah marker (naikkan 0.01f agar tidak flicker)
            activePodium.transform.localPosition = new Vector3(0, 0.1f, 0);

            // 4. LOGIKA LOOK AT (Paling Akurat)
            // Ambil posisi kamera, tapi samakan tingginya dengan podium agar tidak nunduk
            Vector3 targetPos = Camera.main.transform.position;
            targetPos.y = activePodium.transform.position.y;

            // Paksa podium menghadap kamera (World Space)
            activePodium.transform.LookAt(targetPos);

            // 5. Koreksi rotasi 90 derajat karena model podium dibuat menghadap sumbu X positif
            activePodium.transform.Rotate(0, -90, 0);
        }

        CodesData data = new CodesData
        {
            code = finalCode,
            npm = PlayerPrefs.GetString("npm", "unknown"),
            name = PlayerPrefs.GetString("fullName", "unknown")
        };

        PlayerPrefs.SetString("lastPatternCode", finalCode);

        string jsonToSend = JsonUtility.ToJson(data);
        Debug.Log("<color=yellow>[Batik AR] Payload: </color>" + jsonToSend);
        StartCoroutine(SendPostRequest(jsonToSend, finalCode));
    }

    IEnumerator SendPostRequest(string jsonBody, string currentCode)
    {
        using (UnityWebRequest request = new UnityWebRequest(backendURL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Scan Sukses!");
                if (_leaderboardButton != null)
                {
                    _leaderboardButton.style.display = DisplayStyle.Flex;
                    _leaderboardButton.SetEnabled(true);
                    _leaderboardButton.visible = true;
                    Debug.Log("<color=cyan>[Batik AR] Button Leaderboard Set to Visible</color>");
                }
                else
                {
                    Debug.LogError("[Batik AR] Tombol 'LeaderboardButton' tidak ditemukan di UIDocument!");
                }

                if (_refreshCoroutine != null) StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = StartCoroutine(RefreshPodiumRoutine(currentCode));
            }
            else
            {
                codesSent = false; // Reset agar bisa coba lagi
                Debug.LogError($"[Batik AR] HTTP Error: {request.error}");
                _statusLabel.text = "Error: " + request.error;
                // ShowErrorPrefab();

                yield return new WaitForSeconds(3f);
                _statusLabel.text = "";
            }
        }
    }

    IEnumerator RefreshPodiumRoutine(string codes)
    {
        while (true)
        {
            using (UnityWebRequest request = UnityWebRequest.Get($"{leaderboardURL}?codes={codes}"))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    LeaderboardResponse res = JsonUtility.FromJson<LeaderboardResponse>(request.downloadHandler.text);
                    if (res != null && res.success) UpdatePodiumVisuals(res.leaderboard);
                }
            }
            yield return new WaitForSeconds(1f);
        }
    }

    private void UpdatePodiumVisuals(List<LeaderboardEntry> entries)
    {
        if (activePodium == null) return;

        // Urutkan berdasarkan waktu tercepat
        var sortedEntries = entries.OrderBy(e => e.timestamp).ToList();

        for (int i = 0; i < 3; i++)
        {
            string targetName = $"Top{i + 1}";
            Transform labelT = GetChildRecursive(activePodium.transform, targetName);

            if (labelT != null)
            {
                // Gunakan TMP_Text agar kompatibel dengan TextMeshPro - Text (UI) maupun 3D
                TMP_Text tmpText = labelT.GetComponent<TMP_Text>();

                if (tmpText != null)
                {
                    if (i < sortedEntries.Count)
                    {
                        string shortName = sortedEntries[i].name.Length > 8
                            ? sortedEntries[i].name.Substring(0, 8) + ".."
                            : sortedEntries[i].name;

                        tmpText.text = $"{shortName}\n{sortedEntries[i].npm}";
                    }
                    else
                    {
                        tmpText.text = "Waiting...";
                    }
                }
                else
                {
                    Debug.LogWarning($"[Batik AR] Objek {targetName} ditemukan tapi tidak punya komponen TextMeshPro!");
                }
            }
        }
    }

    // Fungsi ini penting untuk mencari objek Top1, Top2, Top3 jika mereka ada di dalam folder anak
    private Transform GetChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform result = GetChildRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private void ClearAllTrackedObjects()
    {
        if (activePodium != null) Destroy(activePodium);
        if (_refreshCoroutine != null) StopCoroutine(_refreshCoroutine);

        foreach (var trackedImage in currentlyTrackedImages)
        {
            if (trackedImage != null && trackedImage.transform != null)
            {
                foreach (Transform child in trackedImage.transform)
                    Destroy(child.gameObject);
            }
        }
        codesSent = false;
        _timerStarted = false;
    }

    private void ShowErrorPrefab()
    {
        ClearAllTrackedObjects();
        if (errorPrefab != null)
        {
            // Hapus error lama jika ada
            GameObject[] oldErrors = GameObject.FindGameObjectsWithTag("ErrorText");
            foreach (var g in oldErrors) Destroy(g);

            Instantiate(errorPrefab, Vector3.zero, Quaternion.identity);
        }
    }
}
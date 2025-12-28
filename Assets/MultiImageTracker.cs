using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;
using UnityEngine.Networking;
using System.Text;
using UnityEngine.UIElements; // Ditambahkan untuk UI
using UnityEngine.SceneManagement;

[RequireComponent(typeof(ARTrackedImageManager))]
public class MultiImageTracker : MonoBehaviour
{
    [System.Serializable]
    public class CodesData
    {
        public string code; // Format: "279"
        public string npm;
        public string name;
    }

    [System.Serializable]
    public struct MarkerPrefab
    {
        public string imageName;
        public GameObject prefab;
    }

    public List<MarkerPrefab> markerPrefabs = new List<MarkerPrefab>();
    private ARTrackedImageManager trackedImageManager;
    private List<ARTrackedImage> currentlyTrackedImages = new List<ARTrackedImage>();

    [Header("Backend Settings")]
    public string backendURL = "https://batikqr.verdex.id/pattern/scan";
    public int requiredCount = 3;
    private bool codesSent = false;

    [Header("UI Settings")]
    public GameObject errorPrefab;
    private VisualElement _root;
    private Button _leaderboardButton;
    private Label _statusLabel;

    void Awake() => trackedImageManager = GetComponent<ARTrackedImageManager>();

    void OnEnable()
    {
        // Inisialisasi UI dengan aman
        var uiDoc = GetComponent<UIDocument>();
        if (uiDoc == null)
        {
            uiDoc = GameObject.FindAnyObjectByType<UIDocument>();
        }

        if (uiDoc != null)
        {
            _root = uiDoc.rootVisualElement;
            _leaderboardButton = _root.Q<Button>("LeaderboardButton");
            _statusLabel = _root.Q<Label>("StatusLabel");
        }

        if (trackedImageManager != null)
            trackedImageManager.trackablesChanged.AddListener(OnChanged);
    }

    void OnDisable()
    {
        if (trackedImageManager != null)
            trackedImageManager.trackablesChanged.RemoveListener(OnChanged);
    }

    void OnChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        // 1. ADDED (Sesuai kode lama kamu)
        foreach (var trackedImage in eventArgs.added)
        {
            if (!currentlyTrackedImages.Contains(trackedImage))
            {
                currentlyTrackedImages.Add(trackedImage);
                string name = trackedImage.referenceImage.name;
                foreach (var item in markerPrefabs)
                {
                    if (item.imageName == name && trackedImage.transform.childCount == 0)
                    {
                        // 1. Buat objek tanpa rotasi dulu (identity)
                        GameObject island = Instantiate(item.prefab, trackedImage.transform.position, trackedImage.transform.rotation);

                        // 2. Pasang sebagai child dari marker
                        island.transform.SetParent(trackedImage.transform);

                        // 3. PAKSA ROTASI AGAR BERDIRI
                        // Kita putar 90 derajat pada sumbu X agar prefab "bangun" dari posisi tiduran
                        island.transform.localRotation = Quaternion.Euler(-90, 180, 0);

                        Debug.Log($"[AR] Pulau {name} sekarang sudah berdiri tegak.");
                    }
                }
            }
        }

        // 2. UPDATED (Sesuai kode lama kamu)
        foreach (var trackedImage in eventArgs.updated)
        {
            bool isVisible = trackedImage.trackingState == TrackingState.Tracking;
            foreach (Transform child in trackedImage.transform)
                child.gameObject.SetActive(isVisible);
        }

        // 3. REMOVED (Sesuai kode lama kamu - Solusi CS8121)
        foreach (KeyValuePair<TrackableId, ARTrackedImage> kvp in eventArgs.removed)
        {
            ARTrackedImage trackedImage = kvp.Value;
            currentlyTrackedImages.Remove(trackedImage);
            if (trackedImage.transform != null && trackedImage.transform.childCount > 0)
                Destroy(trackedImage.transform.GetChild(0).gameObject);
        }

        // 4. CHECK & SEND (Gunakan Count biasa agar deteksi lebih 'sensitif' seperti kode lama)
        if (currentlyTrackedImages.Count >= requiredCount && !codesSent)
        {
            DetectAndSendCodes();
            codesSent = true;
        }
        else if (currentlyTrackedImages.Count < requiredCount)
        {
            codesSent = false;
        }
    }

    private void DetectAndSendCodes()
    {
        if (Camera.main == null) return;

        Camera cam = Camera.main;

        // Lakukan sorting berdasarkan posisi horizontal di layar (X)
        // Tidak peduli HP miring/horizontal, sisi kiri layar tetap X terkecil
        var sortedImages = currentlyTrackedImages
            .OrderBy(image =>
            {
                Vector3 screenPos = cam.WorldToScreenPoint(image.transform.position);
                return screenPos.x;
            })
            .ToList();

        List<string> codesList = new List<string>();
        foreach (var img in sortedImages)
        {
            codesList.Add(img.referenceImage.name);
        }

        string finalCode = string.Join(",", codesList);

        // Log ini akan membantumu melihat urutan di terminal saat HP miring
        Debug.Log($"[Batik AR] HP Orientation: {Input.deviceOrientation}, Order: {finalCode}");

        CodesData data = new CodesData
        {
            code = finalCode,
            npm = PlayerPrefs.GetString("npm", "unknown"),
            name = PlayerPrefs.GetString("fullName", "unknown")
        };

        PlayerPrefs.SetString("lastPatternCode", finalCode);
        StartCoroutine(SendPostRequest(JsonUtility.ToJson(data)));
    }

    IEnumerator SendPostRequest(string jsonBody)
    {
        using (UnityWebRequest request = new UnityWebRequest(backendURL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error: {request.error}");
                _statusLabel.text = "Error: " + request.error;
                ShowErrorPrefab();
            }
            else
            {
                Debug.Log("POST Success!");
                _leaderboardButton.style.display = DisplayStyle.Flex;
            }
        }
    }

    private void ClearAllTrackedObjects()
    {
        // Loop melalui semua gambar yang saat ini dilacak (yang seharusnya masih aktif)
        foreach (var trackedImage in currentlyTrackedImages)
        {
            // Hancurkan semua anak objek (prefab 3D) di bawah transform ARTrackedImage
            if (trackedImage.transform != null)
            {
                foreach (Transform child in trackedImage.transform)
                {
                    Destroy(child.gameObject);
                }
            }

            // Opsional: Hapus ARTrackedImage dari list jika Anda ingin skrip berhenti 
            // memprosesnya untuk sementara waktu, tapi ini bisa mengganggu ARFoundation.
            // Cukup biarkan list tetap ada, dan hanya anak objeknya yang dihancurkan.
        }

        // Karena objek anak sudah dihancurkan, sekarang kita set kembali codesSent = false
        // agar pengguna bisa mencoba scan lagi setelah error diatasi/prefab error hilang.
        codesSent = false;

        Debug.Log("[Batik AR] Semua Prefab Marker 3D telah dibersihkan.");
    }

    private void ShowErrorPrefab()
    {
        ClearAllTrackedObjects();
        // Pastikan errorPrefab sudah di-set di Inspector
        if (errorPrefab == null)
        {
            Debug.LogError("[Batik AR] Error Prefab belum diset di Inspector!");
            return;
        }

        // Hancurkan semua prefab error yang mungkin sudah ada agar tidak menumpuk
        // Gunakan Tag "ErrorText" pada prefab error Anda untuk membuatnya mudah dicari
        var existingErrors = GameObject.FindGameObjectsWithTag("ErrorText");
        foreach (var error in existingErrors)
        {
            Destroy(error);
        }

        // Anda bisa memilih untuk memunculkannya di tengah layar kamera atau di scene
        // Contoh: Memunculkannya di World Origin (0,0,0) atau di posisi lain.
        // Jika Anda ingin menampilkannya sebagai Canvas UI, Anda perlu menyesuaikan kode ini.
        GameObject errorInstance = Instantiate(errorPrefab, Vector3.zero, Quaternion.identity);

        // Atur agar objek error tetap ada di scene meskipun marker hilang
        // Jika Anda ingin menargetkan posisi relatif ke kamera AR, Anda bisa menggunakan 
        // Camera.main.transform.position atau memanggil ARSessionOrigin.camera.transform.position

        Debug.Log("[Batik AR] Menampilkan Error Prefab.");

        // Opsional: Hapus prefab error setelah beberapa detik
        // Destroy(errorInstance, 5.0f); 
    }
}
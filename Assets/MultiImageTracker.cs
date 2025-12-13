using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;
using UnityEngine.Networking;
using System.Text;

[RequireComponent(typeof(ARTrackedImageManager))]
public class MultiImageTracker : MonoBehaviour
{
    // Class helper untuk membuat struktur JSON: { "codes": ["code1", "code2", ...] }
    [System.Serializable]
    public class CodesData
    {
        public List<string> codes;
    }

    [System.Serializable]
    public struct MarkerPrefab
    {
        public string imageName;
        public GameObject prefab;
    }

    public List<MarkerPrefab> markerPrefabs = new List<MarkerPrefab>();
    private ARTrackedImageManager trackedImageManager;

    // List untuk menyimpan gambar yang saat ini dilacak
    private List<ARTrackedImage> currentlyTrackedImages = new List<ARTrackedImage>();

    // Variabel untuk HTTP POST
    [Header("Backend Settings")]
    public string backendURL = "https://api.gprestore.net/not-found";
    public int requiredCount = 3;
    private bool codesSent = false;

    [Header("UI Settings")]
    public GameObject errorPrefab; // Prefab untuk menampilkan error

    // --- Unity Lifecycle Methods ---

    void Awake()
    {
        trackedImageManager = GetComponent<ARTrackedImageManager>();
    }

    void OnEnable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackablesChanged.AddListener(OnChanged);
        }
    }

    void OnDisable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackablesChanged.RemoveListener(OnChanged);
        }
    }

    // --- Tracking Logic ---
    void OnChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        // === 1. Logika untuk marker yang BARU terdeteksi (Added) ===
        foreach (var trackedImage in eventArgs.added)
        {
            currentlyTrackedImages.Add(trackedImage);

            string name = trackedImage.referenceImage.name;
            foreach (var item in markerPrefabs)
            {
                if (item.imageName == name)
                {
                    if (trackedImage.transform.childCount == 0)
                    {
                        Instantiate(item.prefab, trackedImage.transform);
                        Debug.Log($"[AR Tracker] Added: {name}. Total tracked: {currentlyTrackedImages.Count}");
                    }
                }
            }
        }

        // === 2. Logika untuk marker yang UPDATE (misal tracking lost/found) ===
        foreach (var trackedImage in eventArgs.updated)
        {
            bool isVisible = trackedImage.trackingState == TrackingState.Tracking;
            foreach (Transform child in trackedImage.transform)
            {
                child.gameObject.SetActive(isVisible);
            }
        }

        // === 3. Logika untuk marker yang HILANG (Removed) --- SOLUSI FINAL CS8121
        // Mendefinisikan secara eksplisit KeyValuePair untuk mengatasi error casting/pattern matching
        foreach (KeyValuePair<TrackableId, ARTrackedImage> kvp in eventArgs.removed)
        {
            ARTrackedImage trackedImage = kvp.Value; // Ambil Value (ARTrackedImage)

            // Hapus dari List
            currentlyTrackedImages.Remove(trackedImage);

            // Hancurkan objek 3D dari scene
            if (trackedImage.transform != null && trackedImage.transform.childCount > 0)
            {
                Destroy(trackedImage.transform.GetChild(0).gameObject);
            }
            Debug.Log($"[AR Tracker] Removed: {trackedImage.referenceImage.name}. Total tracked: {currentlyTrackedImages.Count}");
        }

        // --- 4. Cek Kondisi dan Kirim Data ---

        if (currentlyTrackedImages.Count == requiredCount && !codesSent)
        {
            DetectAndSendCodes();
            codesSent = true;
        }
        else if (currentlyTrackedImages.Count < requiredCount)
        {
            codesSent = false;
        }
    }

    // --- Fungsi Pengurutan dan HTTP POST ---

    private void DetectAndSendCodes()
    {
        // 1. Urutkan berdasarkan posisi X (World Position Horizontal)
        var sortedImages = currentlyTrackedImages
            .OrderBy(image => image.transform.position.x)
            .ToList();

        // 2. Kumpulkan nama batik (codes) yang sudah diurutkan
        List<string> codesToSend = new List<string>();
        foreach (var trackedImage in sortedImages)
        {
            codesToSend.Add(trackedImage.referenceImage.name);
        }

        // 3. Buat objek dan string JSON
        CodesData data = new CodesData { codes = codesToSend };
        string json = JsonUtility.ToJson(data);

        Debug.Log($"[Batik AR] Codes terdeteksi dan diurutkan: {json}");

        // 4. Kirim HTTP POST
        StartCoroutine(SendPostRequest(json));
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
                Debug.LogError($"[Batik AR] HTTP Error: {request.error}");
                // Saat error, tampilkan prefab error_text_prefab.glb
                ShowErrorPrefab();
            }
            else
            {
                Debug.Log($"[Batik AR] HTTP POST Success! Response: {request.downloadHandler.text}");

                // Saat berhasil, kita bisa menampilkan notifikasi atau efek di sini
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
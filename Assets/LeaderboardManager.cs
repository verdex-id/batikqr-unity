using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Globalization;

[Serializable]
public class LeaderboardEntry
{
    public int id;
    public string codes;
    public long timestamp; // Menggunakan long untuk menampung milidetik
    public string npm;
    public string name;
}

[Serializable]
public class LeaderboardResponse
{
    public bool success;
    public List<LeaderboardEntry> leaderboard; // Nama harus sama dengan "leaderboard" di JSON
}

public class LeaderboardManager : MonoBehaviour
{
    // Nama UXML element dari LeaderboardScreen.uxml
    private const string ListName = "LeaderboardList";
    private const string PatternLabelName = "PatternLabel";
    private const string BackButtonName = "BackButton";

    // Referensi ke root VisualElement
    private VisualElement _root;
    private ScrollView _leaderboardList;
    private Label _patternCodeLabel;
    private Button _backButton;

    void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _leaderboardList = _root.Q<ScrollView>(ListName);
        _patternCodeLabel = _root.Q<Label>(PatternLabelName);
        _backButton = _root.Q<Button>(BackButtonName);

        // Tambah event listener untuk tombol kembali
        _backButton.clicked += OnBackButtonClicked;

        // Ambil kode pattern dari PlayerPrefs (atau sumber lain)
        string patternCode = PlayerPrefs.GetString("lastPatternCode", "unknown");

        _patternCodeLabel.text = $"Code {patternCode}";

        StartCoroutine(SendGetRequest(patternCode));
    }

    private void OnBackButtonClicked()
    {
        SceneManager.LoadScene(1);
    }

    // Fungsi utama untuk mengambil dan menampilkan data
    private void DisplayDataToUI(List<LeaderboardEntry> entries)
    {
        _leaderboardList.Clear();

        // Urutkan berdasarkan waktu tercepat (timestamp terkecil)
        entries.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));

        for (int i = 0; i < entries.Count; i++)
        {
            var data = entries[i];

            VisualElement item = CreateLeaderboardItem(i + 1, data);
            _leaderboardList.Add(item);
        }
    }

    // --- BAGIAN INI YANG DISESUAIKAN ---
    private VisualElement CreateLeaderboardItem(int rank, LeaderboardEntry data)
    {
        VisualElement item = new VisualElement();
        item.AddToClassList("leaderboard-item");

        // Tambah class rank spesial (1, 2, 3) untuk warna border emas/perak/perunggu
        if (rank <= 3) item.AddToClassList($"rank-{rank}");

        // Format Waktu
        DateTimeOffset dto = DateTimeOffset.FromUnixTimeMilliseconds(data.timestamp);
        string timeString = dto.ToString("HH:mm:ss", CultureInfo.InvariantCulture); // Format jam:menit:detik (07:45:30)

        // 1. Rank Column
        Label rankLabel = new Label(rank.ToString());
        rankLabel.AddToClassList("item-text");
        rankLabel.AddToClassList("rank-col"); // Sesuai USS baru

        // 2. Name Column
        // Max 10 karakter, jika lebih potong dan tambahkan "..."
        if (data.name.Length > 10)
        {
            data.name = data.name.Substring(0, 10) + "...";
        }
        Label nameLabel = new Label(data.name);
        nameLabel.AddToClassList("item-text");
        nameLabel.AddToClassList("name-col"); // Sesuai USS baru

        // 3. Time Column
        Label timeLabel = new Label(timeString);
        timeLabel.AddToClassList("item-text");
        timeLabel.AddToClassList("time-col"); // Sesuai USS baru

        // Masukkan ke dalam baris
        item.Add(rankLabel);
        item.Add(nameLabel);
        item.Add(timeLabel);

        return item;
    }

    IEnumerator SendGetRequest(string codes)
    {
        string url = $"https://batikqr.verdex.id/pattern/leaderboard?codes={codes}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Batik AR] HTTP Error: {request.error}");
            }
            else
            {
                string jsonResponse = request.downloadHandler.text;

                // Parsing JSON ke Class Wrapper
                LeaderboardResponse response = JsonUtility.FromJson<LeaderboardResponse>(jsonResponse);

                if (response != null && response.success)
                {
                    // Kirim data ke UI
                    DisplayDataToUI(response.leaderboard);
                }
                else
                {
                    Debug.LogWarning("API Success but data is empty or success field is false.");
                }
            }
        }
    }
}
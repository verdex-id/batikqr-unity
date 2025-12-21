using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;
using System.Globalization;

// Struktur data yang mencerminkan tipe 'Presence' dari backend kamu
public struct PresenceData
{
    public string npm;
    public string name;
    public long timestamp; // Sesuai dengan tipe INTEGER (Timestamp) di DB
}

public class LeaderboardManager : MonoBehaviour
{
    // Nama UXML element dari LeaderboardScreen.uxml
    private const string ListName = "LeaderboardList";
    private const string PatternLabelName = "PatternLabel";

    // Referensi ke root VisualElement
    private VisualElement _root;
    private ScrollView _leaderboardList;
    private Label _patternCodeLabel;

    void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _leaderboardList = _root.Q<ScrollView>(ListName);
        _patternCodeLabel = _root.Q<Label>(PatternLabelName);

        // Panggil fungsi untuk mengisi data (Ganti dengan Logic Fetching API/DB kamu)
        PopulateLeaderboard();
    }

    // Fungsi utama untuk mengambil dan menampilkan data
    private void PopulateLeaderboard()
    {
        // --- Langkah 1: Ganti dengan Logic Backend Kamu ---
        // Di sini kamu akan memanggil API/Service kamu untuk mendapatkan data Presence[]
        // Contoh: PatternService.getLeaderboard(currentCodes)

        // Data Mock (Contoh data Presence yang sudah diurutkan dari tercepat ke lambat)
        List<PresenceData> mockData = GetMockLeaderboardData();

        // --- Langkah 2: Set Pattern Code (Contoh: Ambil dari Current Pattern) ---
        _patternCodeLabel.text = "Current Pattern Code: 2, 7, 9";

        // --- Langkah 3: Isi List ---
        _leaderboardList.Clear(); // Bersihkan list sebelum mengisi

        for (int i = 0; i < mockData.Count; i++)
        {
            var data = mockData[i];
            int rank = i + 1; // Rank dimulai dari 1

            // Buat VisualElement (Baris) untuk setiap entri
            VisualElement item = CreateLeaderboardItem(rank, data);
            _leaderboardList.Add(item);
        }
    }

    // --- BAGIAN INI YANG DISESUAIKAN ---
    private VisualElement CreateLeaderboardItem(int rank, PresenceData data)
    {
        VisualElement item = new VisualElement();
        item.AddToClassList("leaderboard-item");

        // Tambah class rank spesial (1, 2, 3) untuk warna border emas/perak/perunggu
        if (rank <= 3) item.AddToClassList($"rank-{rank}");

        // Format Waktu
        DateTimeOffset dto = DateTimeOffset.FromUnixTimeMilliseconds(data.timestamp);
        string timeString = dto.ToString("HH:mm", CultureInfo.InvariantCulture); // Format jam:menit (07:45)

        // 1. Rank Column
        Label rankLabel = new Label(rank.ToString());
        rankLabel.AddToClassList("item-text");
        rankLabel.AddToClassList("rank-col"); // Sesuai USS baru

        // 2. ID Column (DULU "npm-col", SEKARANG "id-col")
        Label idLabel = new Label(data.npm);
        idLabel.AddToClassList("item-text");
        idLabel.AddToClassList("id-col"); // <--- PERUBAHAN DISINI

        // 3. Name Column
        Label nameLabel = new Label(data.name);
        nameLabel.AddToClassList("item-text");
        nameLabel.AddToClassList("name-col"); // Sesuai USS baru

        // 4. Time Column
        Label timeLabel = new Label(timeString);
        timeLabel.AddToClassList("item-text");
        timeLabel.AddToClassList("time-col"); // Sesuai USS baru

        // Masukkan ke dalam baris
        item.Add(rankLabel);
        item.Add(idLabel);
        item.Add(nameLabel);
        item.Add(timeLabel);

        return item;
    }

    // Dummy Data untuk Testing UI
    private List<PresenceData> GetMockLeaderboardData()
    {
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        return new List<PresenceData>
        {
            new PresenceData { npm = "5220411001", name = "Tito", timestamp = now - 5000 },
            new PresenceData { npm = "5220411002", name = "Zaki", timestamp = now - 4800 },
            new PresenceData { npm = "5220411003", name = "Saputro", timestamp = now - 4500 },
            new PresenceData { npm = "5220411004", name = "Sultan", timestamp = now - 4000 },
            new PresenceData { npm = "5220411040", name = "Akmal", timestamp = now - 3500 }, // Sesuai data test
            new PresenceData { npm = "5220411006", name = "Ghiffari", timestamp = now - 3000 },
            new PresenceData { npm = "5220411007", name = "Agil", timestamp = now - 2500 },
            new PresenceData { npm = "5220411008", name = "Ghani", timestamp = now - 2000 },
            new PresenceData { npm = "5220411009", name = "Istikmal", timestamp = now - 1500 },
            new PresenceData { npm = "5220411010", name = "Gita Evodie", timestamp = now - 1000 }
        };
    }
}
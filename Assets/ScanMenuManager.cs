using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;
using System.Globalization;

public class ScanMenuManager : MonoBehaviour
{
    private const string BackButtonName = "BackButton";
    private const string LeaderboardButtonName = "LeaderboardButton";
    private const string StatusLabelName = "StatusLabel";

    // Referensi ke root VisualElement
    private VisualElement _root;
    private Button _backButton;
    private Button _leaderboardButton;
    private Label _statusLabel;

    void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _backButton = _root.Q<Button>(BackButtonName);
        _leaderboardButton = _root.Q<Button>(LeaderboardButtonName);
        _statusLabel = _root.Q<Label>(StatusLabelName);

        // Hide leaderboard button initially
        _leaderboardButton.style.display = DisplayStyle.None;
        _statusLabel.text = "";

        // Tambah event listener untuk tombol kembali
        _backButton.clicked += OnBackButtonClicked;
        _leaderboardButton.clicked += OnLeaderboardButtonClicked;
    }

    private void OnLeaderboardButtonClicked()
    {
        SceneManager.LoadScene(2);
    }

    private void OnBackButtonClicked()
    {
        SceneManager.LoadScene(0);
    }
}
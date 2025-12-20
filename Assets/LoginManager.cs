using UnityEngine;
using UnityEngine.UIElements; // Wajib ada
using System.Collections;

public class LoginManager : MonoBehaviour
{
    private TextField _npmField;
    private TextField _fullNameField;
    private Button _loginButton;

    private Label _statusLabel;

    void OnEnable()
    {
        // 1. Ambil root dari UI Document
        var root = GetComponent<UIDocument>().rootVisualElement;

        // 2. Cari elemen berdasarkan nama yang di-set di UXML
        _npmField = root.Q<TextField>("NPMInput");
        _fullNameField = root.Q<TextField>("NameInput");
        _loginButton = root.Q<Button>("LoginButton");
        _statusLabel = root.Q<Label>("LoginStatus");
        _statusLabel.style.display = DisplayStyle.None; // Pastikan awalnya tersembunyi lewat code atau USS

        // 3. Daftarkan fungsi klik
        _loginButton.clicked += OnLoginButtonClicked;
    }

    private void OnLoginButtonClicked()
    {
        string npm = _npmField.value;
        string fullName = _fullNameField.value;

        Debug.Log($"Mencoba login sebagai: {npm}, {fullName}");

        SaveToLocalStorage(npm, fullName);
        StartCoroutine(ShowSuccessMessage("Login Berhasil!"));
    }

    private void SaveToLocalStorage(string npm, string fullName)
    {
        // Simpan string dengan key "fullName" dan "npm"
        PlayerPrefs.SetString("npm", npm);
        PlayerPrefs.SetString("fullName", fullName);
        PlayerPrefs.Save(); // Pastikan tersimpan ke disk
        Debug.Log("Data tersimpan di local!");

        // Untuk mengambilnya kembali nanti:
        // string savedNpm = PlayerPrefs.GetString("npm");
        // string savedFullName = PlayerPrefs.GetString("fullName");
    }

    IEnumerator ShowSuccessMessage(string message)
    {
        _statusLabel.text = message;
        _statusLabel.style.display = DisplayStyle.Flex; // Munculkan label

        // Tunggu 3 detik
        yield return new WaitForSeconds(3.0f);

        // Sembunyikan kembali
        _statusLabel.style.display = DisplayStyle.None;
    }
}

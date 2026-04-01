using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoseScreenUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject loseScreenPanel;
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button quitButton;

    [Header("Scene Settings")]
    [SerializeField] private string gameSceneName = "Level3"; // Name of your main game scene

    void Start()
    {
        // Set up button clicks
        playAgainButton.onClick.AddListener(PlayAgain);
        quitButton.onClick.AddListener(QuitGame);

        // Make sure panel is visible
        if (loseScreenPanel != null)
        {
            loseScreenPanel.SetActive(true);
        }
    }

    void PlayAgain()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    void QuitGame()
    {
    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }
}

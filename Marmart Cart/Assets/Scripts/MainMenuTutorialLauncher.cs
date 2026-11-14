using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Main-menu-only tutorial launcher using Unity's legacy Input API.
///
/// Controls:
/// - Keyboard 1 / Numpad 1 / Xbox A -> load 2P tutorial scene
/// - Keyboard 2 / Numpad 2 / Xbox B -> load 4P tutorial scene
///
/// This script deliberately does NOT know or store player count.
/// Each destination tutorial scene is responsible for its own setup.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuTutorialLauncher : MonoBehaviour
{
    [Header("Tutorial Scenes")]
    [SerializeField] private string twoPlayerTutorialScene;
    [SerializeField] private string fourPlayerTutorialScene;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool loadRequested;

    private void Update()
    {
        if (loadRequested) return;

        bool loadTwoPlayer =
            Input.GetKeyDown(KeyCode.Alpha1) ||
            Input.GetKeyDown(KeyCode.Keypad1) ||
            Input.GetKeyDown(KeyCode.JoystickButton0);

        bool loadFourPlayer =
            Input.GetKeyDown(KeyCode.Alpha2) ||
            Input.GetKeyDown(KeyCode.Keypad2) ||
            Input.GetKeyDown(KeyCode.JoystickButton1);

        if (loadTwoPlayer)
        {
            LoadScene(twoPlayerTutorialScene);
        }
        else if (loadFourPlayer)
        {
            LoadScene(fourPlayerTutorialScene);
        }
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError(
                "[MainMenuTutorialLauncher] Destination scene name is empty.",
                this
            );
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"[MainMenuTutorialLauncher] Scene '{sceneName}' cannot be loaded. " +
                "Check Build Settings / Build Profile.",
                this
            );
            return;
        }

        loadRequested = true;

        // Defensive in case the menu is ever reached from a paused game.
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            sceneName,
            LoadSceneMode.Single
        );
    }
}

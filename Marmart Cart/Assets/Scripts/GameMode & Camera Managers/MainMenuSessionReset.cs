using UnityEngine;

/// <summary>
/// Main-menu session boundary.
///
/// Entering Main Menu means the previous tutorial/gameplay match session is
/// over, so any DontDestroyOnLoad GMode from that session must be discarded.
///
/// Main Menu itself does not create, own, or care about a player count.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuSessionReset : MonoBehaviour
{
    private void Awake()
    {
        Time.timeScale = 1f;
        GMode.DestroyPersistentInstance();
    }
}

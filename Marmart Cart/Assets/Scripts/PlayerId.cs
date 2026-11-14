using UnityEngine;

public enum PlayerIndex { P1 = 0, P2 = 1, P3 = 2, P4 = 3 }

public class PlayerId : MonoBehaviour
{
    public PlayerIndex playerIndex = PlayerIndex.P1;
}
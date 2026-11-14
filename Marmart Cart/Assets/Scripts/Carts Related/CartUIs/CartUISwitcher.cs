using UnityEngine;

public class CartUISwitcher : MonoBehaviour
{
    [SerializeField] private GameObject UIsizeFor2p;
    [SerializeField] private GameObject UIsizeFor4p;
    private void Start()
    {
        UIsizeFor2p.SetActive(false);
        UIsizeFor4p.SetActive(false);
        if (GMode.Instance.PlayerCount() == 2)
        {
            UIsizeFor2p.SetActive(true);
        }
        else if (GMode.Instance.PlayerCount() == 4)
        {
            UIsizeFor4p.SetActive(true);
        }
    }
}

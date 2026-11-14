using UnityEngine;

public class DinoTile : MonoBehaviour
{
    public enum PlayerOwner { Player1, Player2 }
    [SerializeField] private PlayerOwner owner;

    [Header("Status")]
    [SerializeField] private bool isCovered = false;

    public bool IsCovered => isCovered;
    public PlayerOwner Owner => owner;
    private bool isCoveredByShelf = false;

    private ShelvesBehavior shelf;

    [Header("Materials")]
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private Material coveredMaterial;

    private Renderer tileRenderer;
    void Awake()
    {
        tileRenderer = this.transform.GetChild(0).GetComponent<Renderer>();
        if (tileRenderer && defaultMaterial)
            tileRenderer.material = defaultMaterial;
    }
    private void OnTriggerStay(Collider other)
    {
        // Match player/carts by tag
        if (owner == PlayerOwner.Player1 && (other.CompareTag("Player1")))
        {
            isCovered = true;
            SetCoveredVisual(true);
        }
        else if (owner == PlayerOwner.Player2 && other.CompareTag("Player2"))
        {
            isCovered = true;
            SetCoveredVisual(true);
        }

        if (other.CompareTag("Obstacles"))
        {
            isCoveredByShelf = true;
            isCovered = true;
            SetCoveredVisual(true);

            // If shelf has behavior script, let it track this tile
            shelf = other.GetComponent<ShelvesBehavior>();
            
        }
        if (other.CompareTag("Walls"))
        {
            isCovered = true;
            SetCoveredVisual(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (owner == PlayerOwner.Player1 && other.CompareTag("Player1"))
        {
            isCovered = false;
            SetCoveredVisual(false);
        }
        else if (owner == PlayerOwner.Player2 && other.CompareTag("Player2"))
        {
            isCovered = false;
            SetCoveredVisual(false);
        }
    }
    public void UncoverTileFromObstacle()
    {
        isCovered = false;
        SetCoveredVisual(false);
        isCoveredByShelf = false;
    }
    private void OnTriggerEnter(Collider other)
    {

        // Destroy if it hits a tile of the opposite player
        DinoTile otherTile = other.GetComponent<DinoTile>();
        if (otherTile != null && otherTile.owner != this.owner)
        {
            DestroyTile();
        }
    }

    private void SetCoveredVisual(bool covered)
    {
        if (tileRenderer)
        {
            tileRenderer.material = covered ? coveredMaterial : defaultMaterial;
        }
    }
    private void Update()
    {
        if (isCoveredByShelf)
        {
            if (shelf == null)
            {
                isCovered = false;
                SetCoveredVisual(false);
                isCoveredByShelf = false; // stop checking
            }
            else if (shelf != null && shelf.getIsBeingSucked())
                {
                    UncoverTileFromObstacle();
                }
            
        }
    }
    public void DestroyTile()
    {
        Destroy(gameObject);
    }
}
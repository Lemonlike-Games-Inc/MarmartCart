using System.Collections.Generic;
using UnityEngine;

public class ComboDealUIController : MonoBehaviour
{
    [SerializeField] private RectTransform originPoint;
    [SerializeField] private float spacing = 20f;
    [SerializeField] private float moveDuration = 0.3f;
    [SerializeField] private float spawnOffsetX = 1920f; // right edge offset

    private List<ComboDeal> activeDeals = new();

    public Vector2 GetSpawnStartPosition()
    {
        return new Vector2(spawnOffsetX, 0f);
    }

    public void Register(ComboDeal deal)
    {
        deal.stackIndex = activeDeals.Count;
        activeDeals.Add(deal);
        StartCoroutine(MoveToPosition(deal));
    }

    public void Unregister(ComboDeal deal)
    {
        int removedIndex = deal.stackIndex;
        activeDeals.Remove(deal);

        foreach (var d in activeDeals)
        {
            if (d.stackIndex > removedIndex)
            {
                d.stackIndex--;
                StartCoroutine(MoveToPosition(d));
            }
        }
    }

    private IEnumerator<WaitForEndOfFrame> MoveToPosition(ComboDeal deal)
    {
        float offsetX = deal.stackIndex * (deal.PanelWidth * 2 + spacing);
        RectTransform rect = deal.GetComponent<RectTransform>();
        Vector2 start = rect.anchoredPosition;
        Vector2 targetPos = new Vector2(originPoint.anchoredPosition.x + offsetX, 0f);

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            rect.anchoredPosition = Vector2.Lerp(start, targetPos, t);
            yield return new WaitForEndOfFrame();
        }

        rect.anchoredPosition = targetPos;
    }
}

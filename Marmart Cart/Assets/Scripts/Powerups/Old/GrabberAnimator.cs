using System.Collections;
using UnityEngine;

public class GrabberAnimator : MonoBehaviour
{
    //[Header("Parts")]
    //[SerializeField] private Transform handle;   // base of the grabber
    //[SerializeField] private Transform body;     // cylinder that scales on Z
    //[SerializeField] private Transform head;     // grabber head mesh

    //[Header("Animation Settings")]
    //[SerializeField] private float extendScaleZ = 10f; // from 1 → 10
    //[SerializeField] private float extendTime = 0.2f;
    //[SerializeField] private float retractTime = 0.2f;

    //private Vector3 bodyBaseScale;
    //private Vector3 headBaseLocalPos;
    //private Vector3 headExtendedLocalPos;
    //private float baseDistance;
    //private Vector3 dirFromHandleToHead;
    //private Coroutine animRoutine;

    //[SerializeField] private GrabberBehavior grabberBehavior;


    //void Awake()
    //{
    //    if (body == null || head == null || handle == null)
    //    {
    //        Debug.LogError("GrabberAnimator: please assign handle, body, and head.");
    //        enabled = false;
    //        return;
    //    }

    //    bodyBaseScale = body.localScale;
    //    headBaseLocalPos = head.localPosition;

    //    Vector3 handleLocalPos = handle.localPosition;
    //    Vector3 offset = headBaseLocalPos - handleLocalPos;
    //    baseDistance = offset.magnitude;
    //    dirFromHandleToHead = offset.normalized;

    //    // Compute where the head should be at FULL EXTENSION using tested magic numbers (work with extendScaleZ = 10, extendTime = 0.2f, retractTime = 0.2f)
    //    float distanceExtended = baseDistance * extendScaleZ * 3.3f;
    //    headExtendedLocalPos = handleLocalPos + -1f * dirFromHandleToHead * distanceExtended;
    //}

    //private void Update()
    //{
    //    // For testing in editor
    //    if (Input.GetKeyDown(KeyCode.G))
    //    {
    //        PlayGrabber();
    //    }
    //}
    ///// <summary>
    ///// Call this from your powerup when the grabber is used
    ///// </summary>
    //public void PlayGrabber()
    //{
    //    if (animRoutine != null)
    //        StopCoroutine(animRoutine);

    //    animRoutine = StartCoroutine(ExtendAndRetractCoroutine());
    //}

    //private IEnumerator ExtendAndRetractCoroutine()
    //{
    //    // EXTEND
    //    float t = 0f;
    //    while (t < 1f)
    //    {
    //        float alpha = t / extendTime;
    //        float scaleFactor = Mathf.Lerp(1f, extendScaleZ, alpha);

    //        ApplyGrabberScale(scaleFactor);

    //        t += Time.deltaTime;
    //        yield return null;
    //    }
    //    ApplyGrabberScale(extendScaleZ);

    //    // (optional) slight pause at max extension
    //    yield return new WaitForSeconds(0.05f);

    //    // RETRACT
    //    t = 0f;
    //    while (t < 1f)
    //    {
    //        float alpha = t / retractTime;
    //        float scaleFactor = Mathf.Lerp(extendScaleZ, 1f, alpha);

    //        ApplyGrabberScale(scaleFactor);

    //        t += Time.deltaTime;
    //        yield return null;
    //    }
    //    ApplyGrabberScale(1f);
    //    if (grabberBehavior != null)
    //        grabberBehavior.OnRetractComplete();

    //    // Hide the grabber so it can be used again later
    //    gameObject.SetActive(false);

    //    animRoutine = null;
    //}

    //private void ApplyGrabberScale(float scaleFactor)
    //{
    //    // Scale the cylinder body along its local Z
    //    body.localScale = new Vector3(
    //        bodyBaseScale.x,
    //        bodyBaseScale.y,
    //        bodyBaseScale.z * scaleFactor
    //    );

    //    // Normalize scaleFactor into t ∈ [0,1] where:
    //    // scaleFactor = 1          → t = 0
    //    // scaleFactor = extendScaleZ → t = 1
    //    float t = Mathf.InverseLerp(1f, extendScaleZ, scaleFactor);

    //    // Lerp head between base and extended pos
    //    head.localPosition = Vector3.Lerp(headBaseLocalPos, headExtendedLocalPos, t);
    //}
}

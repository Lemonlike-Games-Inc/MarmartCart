using System.Text;
using TMPro;
using UnityEngine;

public class CartDriftDebugVisualizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CartControlScript cartControlInput;
    [SerializeField] private CartDriftController driftController;
    [SerializeField] private Rigidbody cartBody;

    [Header("Master Toggle")]
    [SerializeField] private bool showDebug = true;

    [Header("Individual Line Toggles")]
    [SerializeField] private bool showCurrentInputLine = true;   // green
    [SerializeField] private bool showPathDirectionLine = true;  // cyan / blue
    [SerializeField] private bool showEntryInputLine = true;     // yellow
    [SerializeField] private bool showEntryForwardLine = true;   // white

    [Header("Individual Text Toggles")]
    [SerializeField] private bool showDebugText = true;
    [SerializeField] private bool showSpeedText = true;
    [SerializeField] private bool showStateText = true;
    [SerializeField] private bool showSideText = true;
    [SerializeField] private bool showInputAngleText = true;
    [SerializeField] private bool showTightnessText = true;

    [Header("Line Settings")]
    [SerializeField] private float lineHeight = 1.5f;
    [SerializeField] private float lineLength = 3f;
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private Material lineMaterial;

    [Header("Line Colors")]
    [SerializeField] private Color currentInputColor = Color.green;
    [SerializeField] private Color pathDirectionColor = Color.cyan;
    [SerializeField] private Color entryInputColor = Color.yellow;
    [SerializeField] private Color entryForwardColor = Color.white;

    [Header("Text")]
    [SerializeField] private TMP_Text debugText;
    [SerializeField] private Vector3 textOffset = new Vector3(0f, 2.2f, 0f);

    private LineRenderer currentInputLine;
    private LineRenderer pathDirectionLine;
    private LineRenderer entryInputLine;
    private LineRenderer entryForwardLine;

    private readonly StringBuilder textBuilder = new StringBuilder();

    [Header("Wheel Grip Debug Text")]
    [SerializeField] private bool showWheelGripText = true;

    [Tooltip("Assign the 2 or 4 LeadingCartBehaviour wheel scripts here.")]
    [SerializeField] private LeadingCartBehaviour[] wheelGripDebugSources;

    [SerializeField] private bool autoFindWheelGripSources = true;

    [SerializeField] private bool showWheelLateralVelocity = true;
    [SerializeField] private bool showWheelBaseGrip = true;
    [SerializeField] private bool showWheelDriftMultiplier = true;
    [SerializeField] private bool showWheelFinalGrip = true;
    [SerializeField] private bool showWheelForce = false;
    private void Awake()
    {
        currentInputLine = CreateLine("Current Input Line", currentInputColor);
        pathDirectionLine = CreateLine("Path Direction Line", pathDirectionColor);
        entryInputLine = CreateLine("Entry Input Line", entryInputColor);
        entryForwardLine = CreateLine("Entry Forward Line", entryForwardColor);
        if (autoFindWheelGripSources && (wheelGripDebugSources == null || wheelGripDebugSources.Length == 0))
        {
            wheelGripDebugSources = GetComponentsInChildren<LeadingCartBehaviour>();
        }
    }

    private void LateUpdate()
    {
        if (!showDebug || cartControlInput == null || cartBody == null)
        {
            DisableAllVisuals();
            return;
        }

        Vector3 origin = cartBody.worldCenterOfMass + Vector3.up * lineHeight;

        Vector3 currentInputDirection = cartControlInput.desiredDirection.sqrMagnitude > 0.001f
            ? cartControlInput.desiredDirection.normalized
            : Vector3.zero;

        Vector3 pathDirection = GetPathDirection();

        bool isDrifting = driftController != null && driftController.IsDrifting;

        DrawLine(
            currentInputLine,
            origin,
            currentInputDirection,
            lineLength,
            showCurrentInputLine
        );

        DrawLine(
            pathDirectionLine,
            origin,
            pathDirection,
            lineLength,
            showPathDirectionLine
        );

        DrawLine(
            entryInputLine,
            origin,
            driftController != null ? driftController.EntryInputDirection : Vector3.zero,
            lineLength * 0.8f,
            showEntryInputLine && isDrifting
        );

        DrawLine(
            entryForwardLine,
            origin,
            driftController != null ? driftController.EntryForward : Vector3.zero,
            lineLength * 0.8f,
            showEntryForwardLine && isDrifting
        );

        UpdateText();
    }

    private LineRenderer CreateLine(string lineName, Color color)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.useWorldSpace = true;

        if (lineMaterial != null)
        {
            line.material = lineMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                line.material = new Material(shader);
        }

        line.startColor = color;
        line.endColor = color;
        line.enabled = false;

        return line;
    }

    private void DrawLine(LineRenderer line, Vector3 origin, Vector3 direction, float length, bool shouldShow)
    {
        if (line == null)
            return;

        if (!shouldShow || direction.sqrMagnitude < 0.001f)
        {
            line.enabled = false;
            return;
        }

        line.enabled = true;
        line.SetPosition(0, origin);
        line.SetPosition(1, origin + direction.normalized * length);
    }

    private Vector3 GetPathDirection()
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(cartBody.linearVelocity, Vector3.up);

        if (planarVelocity.sqrMagnitude > 0.1f)
            return planarVelocity.normalized;

        return cartBody.transform.forward;
    }

    private void UpdateText()
    {
        if (debugText == null)
            return;

        if (!showDebugText)
        {
            debugText.gameObject.SetActive(false);
            return;
        }

        debugText.gameObject.SetActive(true);

        float speed = Vector3.ProjectOnPlane(cartBody.linearVelocity, Vector3.up).magnitude;

        textBuilder.Clear();

        if (showSpeedText)
            textBuilder.AppendLine($"Speed: {speed:F1}");

        if (driftController != null)
        {
            if (showStateText)
                textBuilder.AppendLine($"Drift: {driftController.CurrentStateName}");

            if (showSideText)
                textBuilder.AppendLine($"Side: {driftController.DriftSideName}");

            if (showInputAngleText)
                textBuilder.AppendLine($"Input Angle: {driftController.CurrentInputAngle:F1}");

            if (showTightnessText)
                textBuilder.AppendLine($"Tightness: {driftController.CurrentTightness:F2}");
        }

        AppendWheelGripDebugText(textBuilder);
        debugText.text = textBuilder.ToString();
    }
    private void AppendWheelGripDebugText(System.Text.StringBuilder textBuilder)
    {
        //if (!showWheelGripText)
        //    return;

        //if (wheelGripDebugSources == null || wheelGripDebugSources.Length == 0)
        //    return;

        //textBuilder.AppendLine();
        //textBuilder.AppendLine("=== Wheel Grip ===");

        //for (int i = 0; i < wheelGripDebugSources.Length; i++)
        //{
        //    LeadingCartBehaviour wheel = wheelGripDebugSources[i];

        //    if (wheel == null)
        //        continue;

        //    textBuilder.Append($"{wheel.DebugWheelName} [{wheel.DebugWheelRole}] ");

        //    if (showWheelLateralVelocity)
        //        textBuilder.Append($"latVel:{wheel.DebugLateralVelocity:F2} ");

        //    if (showWheelBaseGrip)
        //        textBuilder.Append($"base:{wheel.DebugBaseGripFactor:F2} ");

        //    if (showWheelDriftMultiplier)
        //        textBuilder.Append($"mult:{wheel.DebugDriftGripMultiplier:F2} ");

        //    if (showWheelFinalGrip)
        //        textBuilder.Append($"final:{wheel.DebugFinalGripFactor:F2} ");

        //    if (showWheelForce)
        //        textBuilder.Append($"force:{wheel.DebugSteeringForceMagnitude:F0} ");

        //    textBuilder.AppendLine();
        //}
    }
    private void DisableAllVisuals()
    {
        if (currentInputLine != null) currentInputLine.enabled = false;
        if (pathDirectionLine != null) pathDirectionLine.enabled = false;
        if (entryInputLine != null) entryInputLine.enabled = false;
        if (entryForwardLine != null) entryForwardLine.enabled = false;

        if (debugText != null)
            debugText.gameObject.SetActive(false);
    }
}
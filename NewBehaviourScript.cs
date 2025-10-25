// Hand tracking code is commented out for now

using UnityEngine.XR;
using UnityEngine.XR.Hands;
using System.Collections.Generic;
using System.Collections.Generic;
using UnityEngine;


public class XRSpiralOverlay : MonoBehaviour
{
    [Header("Spiral Settings")]
    public int spiralPoints = 1000;
    public float spiralWidth = 0.008f;
    public int numberOfCoils = 3;
    public float spiralSize = 0.09f;
    public Color spiralColor = new Color(1f, 1f, 0f, 1f);
    public Color activeSpiralColor = new Color(0f, 1f, 0f, 1f);
    public Color completedSpiralColor = new Color(0f, 0f, 1f, 1f);

    [Header("Segment-Based Completion")]
    public int completionSegment = 800;
    public float minDrawingTime = 4.0f;

    [Header("Depth Settings")]
    public float requiredZDistance = 0.5f;
    public float zTolerance = 0.05f;

    [Header("Finger Trail Settings")]
    public Color fingerTrailColor = new Color(1f, 0f, 0f, 1f);
    public float fingerTrailWidth = 0.005f;
    public float startRadius = 0.02f;

    [Header("XR Reset Button Settings")]
    public Vector3 buttonOffset = new Vector3(0, -0.2f, 0.3f);
    public Vector2 buttonSize = new Vector2(0.1f, 0.05f);
    public Color buttonColor = Color.cyan;
    public Color buttonTextColor = Color.black;

    private Camera mainCamera;
    private LineRenderer spiralRenderer;
    private LineRenderer startCircleRenderer;
    private LineRenderer fingerTrailRenderer;
    private List<Vector3> fingerTrailPoints = new List<Vector3>();
    private List<Vector3> spiralWorldPoints = new List<Vector3>();

    private bool drawingStarted = false;
    private bool drawingCompleted = false;
    private float drawingStartTime;
    private int currentSpiralSegment = 0;

    private GameObject resetButtonObj;

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject camObj = new GameObject("MainCamera");
            mainCamera = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            mainCamera.tag = "MainCamera";
        }

        // Transparent background for passthrough
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0, 0, 0, 0);

        SetupSpiral();
        SetupStartCircle();
        SetupFingerTrail();
        SetupResetButton();
    }

    void Update()
    {
        // Hand tracking code commented out
        /*
        Vector3 fingertipWorldPos = GetFingertipWorldPosition();
        if (fingertipWorldPos == Vector3.zero) return; // skip if hand not tracked

        Vector3 spiralCenter = spiralRenderer.transform.position;

        if (!drawingStarted && !drawingCompleted)
        {
            float distToCenter = Vector2.Distance(
                new Vector2(fingertipWorldPos.x, fingertipWorldPos.y),
                new Vector2(spiralCenter.x, spiralCenter.y)
            );
            float zDistance = Mathf.Abs(fingertipWorldPos.z - spiralCenter.z);

            if (distToCenter <= startRadius && zDistance <= zTolerance)
            {
                StartDrawing();
            }
        }

        if (drawingStarted && !drawingCompleted)
        {
            AddFingerTrailPoint(fingertipWorldPos);
            UpdateSegmentCompletion();
        }
        */
    }

    // Hand tracking function commented out
    /*
    Vector3 GetFingertipWorldPosition()
    {
        if (XRHandSubsystemHelpers.TryGetLeftHand(out XRHand leftHand) && leftHand.isTracked)
        {
            XRHandJoint indexTip = leftHand.GetJoint(XRHandJointID.IndexTip);
            if (indexTip.TryGetPose(out Pose pose))
            {
                return pose.position; // world space position
            }
        }

        return Vector3.zero;
    }
    */

    void SetupSpiral()
    {
        GameObject spiralObj = new GameObject("SpiralOverlay");
        spiralObj.transform.SetParent(mainCamera.transform);
        spiralObj.transform.localPosition = new Vector3(0, 0, requiredZDistance);
        spiralObj.transform.localRotation = Quaternion.identity;

        spiralRenderer = spiralObj.AddComponent<LineRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = spiralColor;
        spiralRenderer.material = mat;
        spiralRenderer.startWidth = spiralWidth;
        spiralRenderer.endWidth = spiralWidth;
        spiralRenderer.useWorldSpace = false;
        spiralRenderer.positionCount = spiralPoints;
        spiralRenderer.loop = false;

        GenerateSpiralPoints();
    }

    void GenerateSpiralPoints()
    {
        float b = spiralSize / (numberOfCoils * 2f * Mathf.PI);
        Vector3[] positions = new Vector3[spiralPoints];
        for (int i = 0; i < spiralPoints; i++)
        {
            float t = (i / (float)(spiralPoints - 1)) * numberOfCoils * 2f * Mathf.PI;
            float radius = b * t;
            positions[i] = new Vector3(radius * Mathf.Cos(t), radius * Mathf.Sin(t), 0);
        }
        spiralRenderer.SetPositions(positions);
        CacheSpiralWorldPoints();
    }

    void SetupStartCircle()
    {
        GameObject circleObj = new GameObject("StartCircle");
        circleObj.transform.SetParent(mainCamera.transform);
        circleObj.transform.localPosition = new Vector3(0, 0, requiredZDistance);

        startCircleRenderer = circleObj.AddComponent<LineRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = Color.green;
        startCircleRenderer.material = mat;
        startCircleRenderer.startWidth = 0.002f;
        startCircleRenderer.endWidth = 0.002f;
        startCircleRenderer.useWorldSpace = false;
        startCircleRenderer.loop = true;

        int segments = 100;
        startCircleRenderer.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            startCircleRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * startRadius, Mathf.Sin(angle) * startRadius, 0));
        }
    }

    void SetupFingerTrail()
    {
        // You can leave the renderer setup here for later, no hand tracking
        /*
        GameObject trailObj = new GameObject("FingerTrail");
        trailObj.transform.position = Vector3.zero;

        fingerTrailRenderer = trailObj.AddComponent<LineRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = fingerTrailColor;
        fingerTrailRenderer.material = mat;
        fingerTrailRenderer.startWidth = fingerTrailWidth;
        fingerTrailRenderer.endWidth = fingerTrailWidth;
        fingerTrailRenderer.useWorldSpace = true;
        fingerTrailRenderer.positionCount = 0;
        */
    }

    void AddFingerTrailPoint(Vector3 worldPos)
    {
        if (fingerTrailPoints.Count == 0 || Vector3.Distance(fingerTrailPoints[fingerTrailPoints.Count - 1], worldPos) > 0.001f)
        {
            fingerTrailPoints.Add(worldPos);
            fingerTrailRenderer.positionCount = fingerTrailPoints.Count;
            fingerTrailRenderer.SetPositions(fingerTrailPoints.ToArray());
        }
    }

    void StartDrawing()
    {
        drawingStarted = true;
        drawingStartTime = Time.time;
        spiralRenderer.material.color = activeSpiralColor;
        startCircleRenderer.enabled = false;
        fingerTrailPoints.Clear();
        fingerTrailRenderer.positionCount = 0;
        currentSpiralSegment = 0;
    }

    void UpdateSegmentCompletion()
    {
        if (fingerTrailPoints.Count == 0) return;

        Vector3 currentPos = fingerTrailPoints[fingerTrailPoints.Count - 1];
        float closestDistance = float.MaxValue;
        int closestIndex = 0;

        for (int i = 0; i < spiralWorldPoints.Count; i++)
        {
            float distance = Vector2.Distance(
                new Vector2(currentPos.x, currentPos.y),
                new Vector2(spiralWorldPoints[i].x, spiralWorldPoints[i].y)
            );
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        if (closestIndex > currentSpiralSegment)
            currentSpiralSegment = closestIndex;

        if (currentSpiralSegment >= completionSegment && Time.time - drawingStartTime >= minDrawingTime)
            CompleteDrawing();
    }

    void CompleteDrawing()
    {
        drawingCompleted = true;
        spiralRenderer.material.color = completedSpiralColor;
    }

    void CacheSpiralWorldPoints()
    {
        spiralWorldPoints.Clear();
        for (int i = 0; i < spiralRenderer.positionCount; i++)
            spiralWorldPoints.Add(spiralRenderer.transform.TransformPoint(spiralRenderer.GetPosition(i)));
    }

    void SetupResetButton()
    {
        resetButtonObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        resetButtonObj.name = "ResetButton";
        resetButtonObj.transform.SetParent(mainCamera.transform);
        resetButtonObj.transform.localPosition = buttonOffset;
        resetButtonObj.transform.localScale = new Vector3(buttonSize.x, buttonSize.y, 0.02f);

        var renderer = resetButtonObj.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Standard"));
        renderer.material.color = buttonColor;

        GameObject textObj = new GameObject("ButtonText");
        textObj.transform.SetParent(resetButtonObj.transform);
        textObj.transform.localPosition = new Vector3(0, 0, -0.02f);
        TextMesh textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = "RESET";
        textMesh.fontSize = 100;
        textMesh.characterSize = 0.0025f;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.color = buttonTextColor;

        BoxCollider collider = resetButtonObj.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        resetButtonObj.AddComponent<XRResetButton>().Init(this);
    }

    public void ResetDrawingFromButton()
    {
        drawingStarted = false;
        drawingCompleted = false;
        currentSpiralSegment = 0;
        fingerTrailPoints.Clear();
        fingerTrailRenderer.positionCount = 0;
        spiralRenderer.material.color = spiralColor;
        startCircleRenderer.enabled = true;
        drawingStartTime = 0f;
        Debug.Log("Spiral reset via XR button");
    }
}

// XRResetButton class unchanged
public class XRResetButton : MonoBehaviour
{
    private XRSpiralOverlay spiralScript;
    public void Init(XRSpiralOverlay script) => spiralScript = script;
    void OnTriggerEnter(Collider other) => spiralScript.ResetDrawingFromButton();
}

// Hand helper class commented out
/*
public static class XRHandSubsystemHelpers
{
    public static bool TryGetLeftHand(out XRHand hand)
    {
        hand = default;
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetInstances(subsystems);
        if (subsystems.Count > 0 && subsystems[0].running)
        {
            hand = subsystems[0].leftHand;
            return hand.isTracked;
        }
        return false;
    }
}
*/

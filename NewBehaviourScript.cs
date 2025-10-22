using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class WebcamSpiralOverlay : MonoBehaviour
{
    [Header("Spiral Settings")]
    public int spiralPoints = 1000;
    public float spiralWidth = 0.008f;
    public int numberOfCoils = 3;
    public float spiralSize = 0.15f;
    public Color spiralColor = Color.yellow;
    public Color activeSpiralColor = Color.green;
    public Color completedSpiralColor = Color.blue;

    [Header("Segment-Based Completion")]
    public int completionSegment = 800; // Must reach 80% of spiral points (800/1000)
    public float minDrawingTime = 4.0f;
    private int currentSpiralSegment = 0;

    [Header("Depth Camera Simulation")]
    public float requiredZDistance = 0.5f;
    public float zTolerance = 0.05f;
    public float simulatedZDepth = 0.5f;
    public float depthChangeSpeed = 0.01f;

    [Header("Input Source")]
    public bool useCSVInput = false;
    public string csvFilePath = "Assets/frames.csv";

    [Header("Webcam Settings")]
    public int webcamIndex = 0;
    public bool mirrorWebcam = true;

    [Header("Finger Trail Settings")]
    public Color fingerTrailColor = Color.red;
    public float fingerTrailWidth = 0.005f;
    public float startRadius = 0.02f;

    [Header("CSV Coordinate Adjustment")]
    public bool autoCenterAndScale = true;
    public float margin = 0.1f; // 10% margin around the spiral

    [Header("CSV Playback Settings")]
    public float csvPlaybackSpeed = 30.0f; // Increase from 1.0 to 30.0 for real-time
    public bool useFrameBasedPlayback = true;
    public int framesPerPoint = 1; // How many Unity frames to show each CSV point

    private Vector3 csvMinBounds;
    private Vector3 csvMaxBounds;
    private Vector3 csvCenter;
    private Vector3 csvScale;

    private Camera mainCamera;
    private WebCamTexture webcamTexture;
    private GameObject webcamPlane;
    private LineRenderer spiralRenderer;
    private LineRenderer startCircleRenderer;
    private LineRenderer fingerTrailRenderer;
    private List<Vector3> fingerTrailPoints = new List<Vector3>();
    private List<Vector3> spiralWorldPoints = new List<Vector3>();
    private List<Vector3> csvCoordinates = new List<Vector3>();
    private List<Vector3> rawCSVCoordinates = new List<Vector3>(); // Store raw coordinates
    private int currentCSVIndex = 0;
    private float lastCSVUpdateTime = 0f;

    [Header("Debug Controls")]
    public bool forceStartDrawing = false;

    private bool drawingStarted = false;
    private bool drawingCompleted = false;
    private float drawingStartTime;
    private float lastDrawingTime;
    private float completionPercentage = 0f;

    void Start()
    {
        SetupCamera();
        SetupWebcam();
        SetupSpiral();
        SetupStartCircle();
        SetupFingerTrail();

        simulatedZDepth = requiredZDistance;

        if (useCSVInput)
        {
            LoadCSVCoordinates();
        }
    }

    private void Update()
    {
        HandleDepthInput();

        Vector3 fingertipWorldPos = GetFingertipWorldPosition();
        Vector3 spiralCenter = spiralRenderer.transform.position;

        // Debug coordinates to see what's happening
        if (useCSVInput)
        {
            DebugCoordinatePositions();
        }

        // Force start drawing for testing
        if (forceStartDrawing && !drawingStarted && !drawingCompleted)
        {
            StartDrawing();
            forceStartDrawing = false;
        }

        if (!drawingStarted && !drawingCompleted)
        {
            float distToCenter = Vector2.Distance(
                new Vector2(fingertipWorldPos.x, fingertipWorldPos.y),
                new Vector2(spiralCenter.x, spiralCenter.y)
            );
            float zDistance = Mathf.Abs(fingertipWorldPos.z - spiralCenter.z);

            bool isAtCorrectZ = zDistance <= zTolerance;
            bool isWithinRadius = distToCenter <= startRadius;

            if (isWithinRadius && isAtCorrectZ)
            {
                StartDrawing();
            }
        }

        if (drawingStarted && !drawingCompleted)
        {
            AddFingerTrailPoint(fingertipWorldPos);
            UpdateSegmentCompletion();
        }
    }

    void SetupCamera()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject camObj = new GameObject("MainCamera");
            mainCamera = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            camObj.tag = "MainCamera";
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.black;
            mainCamera.transform.position = new Vector3(0, 0, -1f); // Position camera properly
            mainCamera.orthographic = false;
            mainCamera.fieldOfView = 60f;
        }
    }

    void SetupWebcam()
    {
        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogError("No webcams found!");
            return;
        }

        int deviceIndex = Mathf.Clamp(webcamIndex, 0, WebCamTexture.devices.Length - 1);
        string deviceName = WebCamTexture.devices[deviceIndex].name;
        webcamTexture = new WebCamTexture(deviceName, 1280, 720, 30);
        webcamTexture.Play();

        webcamPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        webcamPlane.name = "WebcamBackground";
        Destroy(webcamPlane.GetComponent<Collider>());

        float distance = 1f; // Closer to camera
        webcamPlane.transform.position = mainCamera.transform.position + mainCamera.transform.forward * distance;
        webcamPlane.transform.rotation = mainCamera.transform.rotation;

        // Calculate proper scale for webcam plane
        float height = 2f * distance * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * mainCamera.aspect;
        webcamPlane.transform.localScale = new Vector3(width, height, 1f);

        Material webcamMat = new Material(Shader.Find("Unlit/Texture"));
        webcamMat.mainTexture = webcamTexture;
        if (mirrorWebcam)
        {
            webcamMat.mainTextureScale = new Vector2(-1, 1);
            webcamMat.mainTextureOffset = new Vector2(1, 0);
        }
        webcamPlane.GetComponent<Renderer>().material = webcamMat;
    }

    void SetupSpiral()
    {
        GameObject spiralObj = new GameObject("SpiralOverlay");
        spiralObj.transform.SetParent(mainCamera.transform);
        spiralObj.transform.localPosition = new Vector3(0, 0, requiredZDistance);
        spiralObj.transform.localRotation = Quaternion.identity;

        spiralRenderer = spiralObj.AddComponent<LineRenderer>();
        spiralRenderer.material = new Material(Shader.Find("Sprites/Default"));
        spiralRenderer.material.color = spiralColor;

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
            float x = radius * Mathf.Cos(t);
            float y = radius * Mathf.Sin(t);
            positions[i] = new Vector3(x, y, 0);
        }
        spiralRenderer.SetPositions(positions);

        // Cache world points for completion detection
        CacheSpiralWorldPoints();

        Debug.Log($"Spiral generated with {spiralPoints} points. Completion requires reaching segment {completionSegment}");
    }

    void SetupStartCircle()
    {
        GameObject circleObj = new GameObject("StartCircle");
        circleObj.transform.SetParent(mainCamera.transform);
        circleObj.transform.localPosition = new Vector3(0, 0, requiredZDistance);
        circleObj.transform.localRotation = Quaternion.identity;

        startCircleRenderer = circleObj.AddComponent<LineRenderer>();
        startCircleRenderer.material = new Material(Shader.Find("Sprites/Default"));
        startCircleRenderer.material.color = Color.green;
        startCircleRenderer.startWidth = 0.002f;
        startCircleRenderer.endWidth = 0.002f;
        startCircleRenderer.useWorldSpace = false;
        startCircleRenderer.loop = true;

        int segments = 100;
        startCircleRenderer.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            float x = Mathf.Cos(angle) * startRadius;
            float y = Mathf.Sin(angle) * startRadius;
            startCircleRenderer.SetPosition(i, new Vector3(x, y, 0));
        }
    }

    void SetupFingerTrail()
    {
        GameObject trailObj = new GameObject("FingerTrail");
        trailObj.transform.SetParent(mainCamera.transform);
        trailObj.transform.localPosition = Vector3.zero;
        trailObj.transform.localRotation = Quaternion.identity;

        fingerTrailRenderer = trailObj.AddComponent<LineRenderer>();
        fingerTrailRenderer.material = new Material(Shader.Find("Sprites/Default"));
        fingerTrailRenderer.material.color = fingerTrailColor;
        fingerTrailRenderer.startWidth = fingerTrailWidth;
        fingerTrailRenderer.endWidth = fingerTrailWidth;
        fingerTrailRenderer.useWorldSpace = true;
        fingerTrailRenderer.positionCount = 0;
    }

    void LoadCSVCoordinates()
    {
        rawCSVCoordinates.Clear();
        csvCoordinates.Clear();

        if (!File.Exists(csvFilePath))
        {
            Debug.LogError($"CSV file not found: {csvFilePath}");
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(csvFilePath);
            List<Vector3> coordinates = new List<Vector3>();

            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line) || line.StartsWith("X") || line.StartsWith("x"))
                    continue;

                string[] values = line.Split(',');

                if (values.Length >= 3)
                {
                    if (float.TryParse(values[0], out float x) &&
                        float.TryParse(values[1], out float y) &&
                        float.TryParse(values[2], out float z))
                    {
                        coordinates.Add(new Vector3(x, y, z));
                    }
                }
            }

            if (coordinates.Count == 0)
            {
                Debug.LogError("No valid coordinates found in CSV file");
                return;
            }

            rawCSVCoordinates = new List<Vector3>(coordinates);

            if (autoCenterAndScale)
            {
                CalculateAutoAdjustment();
                ApplyAutoAdjustment();
            }
            else
            {
                csvCoordinates = coordinates;
            }

            Debug.Log($"Loaded {csvCoordinates.Count} coordinates from CSV");
            Debug.Log($"Data range: X({csvMinBounds.x}-{csvMaxBounds.x}), Y({csvMinBounds.y}-{csvMaxBounds.y}), Z({csvMinBounds.z}-{csvMaxBounds.z})");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading CSV file: {e.Message}");
        }
    }

    void CalculateAutoAdjustment()
    {
        if (rawCSVCoordinates.Count == 0) return;

        // Find min and max bounds of the CSV data
        csvMinBounds = rawCSVCoordinates[0];
        csvMaxBounds = rawCSVCoordinates[0];

        foreach (Vector3 pos in rawCSVCoordinates)
        {
            csvMinBounds = Vector3.Min(csvMinBounds, pos);
            csvMaxBounds = Vector3.Max(csvMaxBounds, pos);
        }

        // Calculate center of the data
        csvCenter = (csvMinBounds + csvMaxBounds) / 2f;

        // Calculate the size of the data
        Vector3 dataSize = csvMaxBounds - csvMinBounds;

        // Normalize approach: scale data to fit within -0.5 to 0.5 range, then apply spiral size
        float maxDataDimension = Mathf.Max(dataSize.x, dataSize.y);

        if (maxDataDimension < 0.001f)
        {
            Debug.LogError("CSV data range is too small!");
            csvScale = Vector3.one;
            return;
        }

        // First normalize to unit size, then scale to spiral size
        float normalizeScale = 1.0f / maxDataDimension;
        float spiralScale = spiralSize * (1f - margin);

        float finalScale = normalizeScale * spiralScale;

        csvScale = new Vector3(finalScale, finalScale, 0.001f);

        Debug.Log($"Auto-adjusted CSV: NormalizeScale={normalizeScale:F6}, SpiralScale={spiralScale}, FinalScale={finalScale:F6}");
    }

    void ApplyAutoAdjustment()
    {
        csvCoordinates.Clear();

        foreach (Vector3 rawPos in rawCSVCoordinates)
        {
            // Center the XY data around zero, ignore Z
            Vector3 centered = new Vector3(
                rawPos.x - csvCenter.x,
                rawPos.y - csvCenter.y,
                0  // Z should be 0 in local space
            );

            // Apply scaling to XY only
            Vector3 scaled = Vector3.Scale(centered, csvScale);

            // REMOVE the Y flip or try X flip instead
            // scaled.y = -scaled.y; // Remove this line

            // If it's still flipped, try flipping X instead:
            // scaled.x = -scaled.x;

            // Or try both flips based on what you see:
            // scaled.x = -scaled.x;
            // scaled.y = -scaled.y;

            csvCoordinates.Add(scaled);
        }

        Debug.Log($"Applied scaling to {csvCoordinates.Count} points - testing flip correction");
    }

    Vector3 GetFingertipWorldPosition()
    {
        if (useCSVInput)
        {
            return GetCSVFingerPosition();
        }
        else
        {
            return GetMouseFingerPosition();
        }
    }

    Vector3 GetMouseFingerPosition()
    {
        Vector3 screenPos = Input.mousePosition;
        screenPos.z = simulatedZDepth;
        return mainCamera.ScreenToWorldPoint(screenPos);
    }

    Vector3 GetCSVFingerPosition()
    {
        if (csvCoordinates.Count == 0)
        {
            Debug.LogWarning("No CSV coordinates loaded");
            return spiralRenderer.transform.position;
        }

        if (useFrameBasedPlayback)
        {
            // Frame-based playback (more reliable)
            if (Time.frameCount % framesPerPoint == 0)
            {
                currentCSVIndex++;
                if (currentCSVIndex >= csvCoordinates.Count)
                {
                    currentCSVIndex = csvCoordinates.Count - 1;
                    if (!drawingCompleted)
                    {
                        Debug.Log("Reached end of CSV data");
                        drawingCompleted = true;
                    }
                }
            }
        }
        else
        {
            // Time-based playback
            if (Time.time - lastCSVUpdateTime > (1.0f / csvPlaybackSpeed))
            {
                currentCSVIndex++;
                lastCSVUpdateTime = Time.time;

                if (currentCSVIndex >= csvCoordinates.Count)
                {
                    currentCSVIndex = csvCoordinates.Count - 1;
                    if (!drawingCompleted)
                    {
                        Debug.Log("Reached end of CSV data");
                        drawingCompleted = true;
                    }
                }
            }
        }

        Vector3 adjustedPos = csvCoordinates[currentCSVIndex];

        // Convert from spiral local space to world space
        Vector3 worldPos = spiralRenderer.transform.TransformPoint(adjustedPos);

        return worldPos;
    }

    void HandleDepthInput()
    {
        if (!useCSVInput)
        {
            if (Input.GetKey(KeyCode.Q))
            {
                simulatedZDepth -= depthChangeSpeed * Time.deltaTime * 60f;
                simulatedZDepth = Mathf.Max(0.1f, simulatedZDepth);
            }

            if (Input.GetKey(KeyCode.E))
            {
                simulatedZDepth += depthChangeSpeed * Time.deltaTime * 60f;
                simulatedZDepth = Mathf.Min(2.0f, simulatedZDepth);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                simulatedZDepth = requiredZDistance;
            }
        }
    }

    void StartDrawing()
    {
        drawingStarted = true;
        drawingStartTime = Time.time;
        lastDrawingTime = Time.time;
        spiralRenderer.material.color = activeSpiralColor;
        startCircleRenderer.enabled = false;

        // Clear any existing trail and reset segment tracking
        fingerTrailPoints.Clear();
        fingerTrailRenderer.positionCount = 0;
        currentSpiralSegment = 0;

        string inputSource = useCSVInput ? "CSV data" : "mouse";
        Debug.Log($"Drawing started - segment-based completion active (Input: {inputSource})");
    }

    void CacheSpiralWorldPoints()
    {
        spiralWorldPoints.Clear();
        for (int i = 0; i < spiralRenderer.positionCount; i++)
        {
            Vector3 localPoint = spiralRenderer.GetPosition(i);
            Vector3 worldPoint = spiralRenderer.transform.TransformPoint(localPoint);
            spiralWorldPoints.Add(worldPoint);
        }
        Debug.Log($"Cached {spiralWorldPoints.Count} spiral world points for completion detection");
    }

    void UpdateSegmentCompletion()
    {
        if (fingerTrailPoints.Count == 0) return;

        Vector3 currentPos = fingerTrailPoints[fingerTrailPoints.Count - 1];

        // Find closest spiral point
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

        // Track highest spiral segment reached (never goes backward)
        if (closestIndex > currentSpiralSegment)
        {
            currentSpiralSegment = closestIndex;
        }

        completionPercentage = Mathf.Clamp01((float)currentSpiralSegment / spiralPoints);

        // Debug progress occasionally
        if (Time.frameCount % 90 == 0)
        {
            Debug.Log($"Segment Progress: {currentSpiralSegment}/{spiralPoints} ({completionPercentage:P0}) - Current closest: {closestIndex}");
        }

        if (currentSpiralSegment >= completionSegment && Time.time - drawingStartTime >= minDrawingTime)
        {
            CompleteDrawing();
        }
    }

    void CompleteDrawing()
    {
        drawingCompleted = true;
        spiralRenderer.material.color = completedSpiralColor;
        Debug.Log($"Drawing completed! Reached segment {currentSpiralSegment}/{spiralPoints}, Time: {Time.time - drawingStartTime:F1}s");
    }

    void AddFingerTrailPoint(Vector3 worldPos)
    {
        // Only add point if it's significantly different from the last point
        if (fingerTrailPoints.Count == 0 ||
            Vector3.Distance(fingerTrailPoints[fingerTrailPoints.Count - 1], worldPos) > 0.001f)
        {
            fingerTrailPoints.Add(worldPos);
            fingerTrailRenderer.positionCount = fingerTrailPoints.Count;
            fingerTrailRenderer.SetPositions(fingerTrailPoints.ToArray());

            lastDrawingTime = Time.time;
        }
    }

    public void ResetFingerTrail()
    {
        fingerTrailPoints.Clear();
        fingerTrailRenderer.positionCount = 0;
        drawingStarted = false;
        drawingCompleted = false;
        completionPercentage = 0f;
        currentSpiralSegment = 0;
        currentCSVIndex = 0;
        lastCSVUpdateTime = 0f;

        spiralRenderer.material.color = spiralColor;
        startCircleRenderer.enabled = true;
        simulatedZDepth = requiredZDistance;

        Debug.Log("Drawing reset");
    }

    public void ToggleInputSource()
    {
        useCSVInput = !useCSVInput;
        ResetFingerTrail();

        if (useCSVInput && rawCSVCoordinates.Count == 0)
        {
            LoadCSVCoordinates();
        }

        Debug.Log($"Input source switched to: {(useCSVInput ? "CSV" : "Mouse")}");
    }

    void OnDestroy()
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
            webcamTexture.Stop();
    }

    // Debug method to visualize the coordinate conversion
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        if (useCSVInput && csvCoordinates.Count > 0 && currentCSVIndex < csvCoordinates.Count)
        {
            Vector3 currentWorldPos = GetCSVFingerPosition();
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(currentWorldPos, 0.005f);

            // Draw line from spiral center to current position
            Vector3 spiralCenter = spiralRenderer.transform.position;
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(spiralCenter, currentWorldPos);
        }

        // Draw completion segment position for reference
        if (spiralWorldPoints.Count > completionSegment)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(spiralWorldPoints[completionSegment], 0.01f);
        }
    }

    void DebugCoordinatePositions()
    {
        if (!useCSVInput || csvCoordinates.Count == 0) return;

        Vector3 fingertipWorldPos = GetCSVFingerPosition();
        Vector3 spiralCenter = spiralRenderer.transform.position;

        // DEBUG: Check all the coordinate spaces
        Vector3 cameraLocal = mainCamera.transform.InverseTransformPoint(fingertipWorldPos);
        Vector3 spiralLocal = spiralRenderer.transform.InverseTransformPoint(fingertipWorldPos);

        float distToCenter = Vector2.Distance(
            new Vector2(fingertipWorldPos.x, fingertipWorldPos.y),
            new Vector2(spiralCenter.x, spiralCenter.y)
        );
        float zDistance = Mathf.Abs(fingertipWorldPos.z - spiralCenter.z);

        bool isAtCorrectZ = zDistance <= zTolerance;
        bool isWithinRadius = distToCenter <= startRadius;

        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"=== CSV COORDINATE DEBUG ===");
            Debug.Log($"Adjusted CSV: {csvCoordinates[currentCSVIndex]}");
            Debug.Log($"World Position: {fingertipWorldPos}");
            Debug.Log($"Spiral World Position: {spiralCenter}");
            Debug.Log($"Spiral Local Position: {spiralLocal}");
            Debug.Log($"Camera Local Position: {cameraLocal}");
            Debug.Log($"XY Distance to Center: {distToCenter:F4} (radius: {startRadius:F4})");
            Debug.Log($"Z Distance: {zDistance:F4} (tolerance: {zTolerance:F4})");
            Debug.Log($"Within Radius: {isWithinRadius}, Correct Z: {isAtCorrectZ}");
            Debug.Log($"=== END DEBUG ===");
        }
    }
}

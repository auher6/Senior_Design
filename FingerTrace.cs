using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

public class FingerTrailManager : MonoBehaviour
{
    [Header("Trail Settings")]
    [Tooltip("Assign an existing TrailRenderer in the scene.")]
    public TrailRenderer trailInstance;

    [Tooltip("Track right hand if true, left hand if false.")]
    public bool trackRightHand = true;

    [Tooltip("Smoothing speed for finger movement.")]
    public float smoothSpeed = 10f;

    private XRHandSubsystem handSubsystem;
    private Vector3 currentPos;

    void Start()
    {
        // Get the hand subsystem
        var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
        if (loader != null)
            handSubsystem = loader.GetLoadedSubsystem<XRHandSubsystem>();

        if (trailInstance != null)
            currentPos = trailInstance.transform.position;
        else
            Debug.LogError("Assign the TrailRenderer from the scene to 'trailInstance'!");
    }

    void Update()
    {
        if (handSubsystem == null || trailInstance == null)
            return;

        // Get the right or left hand
        XRHand hand = trackRightHand ? handSubsystem.rightHand : handSubsystem.leftHand;

        if (!hand.isTracked)
            return;

        XRHandJoint indexTip = hand.GetJoint(XRHandJointID.IndexTip);
        if (indexTip.TryGetPose(out Pose pose))
        {
            // Smooth the movement
            currentPos = Vector3.Lerp(currentPos, pose.position, Time.deltaTime * smoothSpeed);

            // Move the trail
            trailInstance.transform.position = currentPos;
            trailInstance.transform.rotation = pose.rotation;
        }
    }
}


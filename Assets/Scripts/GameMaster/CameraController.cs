using Unity.Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private GameInputs gameInput;
    [SerializeField] private LayerMask groundMask;

    [Header("Camera Settings")]
    public float moveSpeed = 50f;
    public float rotateSpeed = 100f;
    public float zoomSpeed = 5f;
    public float zoomAmount = 5f;

    [Header("Zoom Limits")]
    public float fovMin = 10f;
    public float fovMax = 60f;
    public float followOffsetMin = 5f;
    public float followOffsetMax = 60f;
    public float followOffsetMinY = 5f;
    public float followOffsetMaxY = 13f;
    [Tooltip("Forward slide step size vs Y-drop size")]
    public float slideForwardMultiplier = 1.5f;

    [Header("Advanced Options")]
    public bool enableEdgeScroll = true;
    public bool useDragPan = false;
    public int edgeScrollSize = 20;

    public enum ZoomMode { FOV, MoveForward, LowerY, Slide }
    [SerializeField] private ZoomMode zoomMode = ZoomMode.FOV;

    // Internal
    private Vector3 lastPosition;
    private CinemachineFollow followComponent;
    private Vector3 followOffset;
    private float targetFOV = 60f;
    float scroll = 0;
    
    // Smoothing
    private Vector3 targetPosition;
    [Header("Smoothing")]
    public float positionLerpSpeed = 10f;


    private void Start()
    {
        followComponent = cinemachineCamera.GetComponent<CinemachineFollow>();
        followOffset = followComponent.FollowOffset;
        targetFOV = cinemachineCamera.Lens.FieldOfView;
        targetPosition = transform.position;
    }

    private void Update()
    {
        HandleMovement();
        HandleRotation();

        if (enableEdgeScroll)
            HandleEdgeScrolling();

        if (useDragPan)
            HandleDragPan();

        HandleZoom();
        HandleFocus();
        
        // Apply smoothed movement
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * positionLerpSpeed);
    }

    #region Movement

    private void HandleMovement()
    {
        Vector2 input = gameInput.GetMoveInput();
        if (input.sqrMagnitude > 0.01f) isFocusing = false; // Interrupt focus

        Vector3 dir = transform.forward * input.y + transform.right * input.x;
        targetPosition += dir * moveSpeed * Time.deltaTime;
    }

    private void HandleRotation()
    {
        float rotateInput = gameInput.GetRotateInput();
        transform.Rotate(Vector3.up, rotateInput * rotateSpeed * Time.deltaTime);
    }

    private void HandleEdgeScrolling()
    {
        Vector2 mousePos = gameInput.GetPointerPosition();
        Vector3 inputDir = Vector3.zero;

        if (mousePos.x < edgeScrollSize) inputDir.x -= 1f;
        if (mousePos.x > Screen.width - edgeScrollSize) inputDir.x += 1f;
        if (mousePos.y < edgeScrollSize) inputDir.z -= 1f;
        if (mousePos.y > Screen.height - edgeScrollSize) inputDir.z += 1f;

        Vector3 moveDir = transform.forward * inputDir.z + transform.right * inputDir.x;
        targetPosition += moveDir * moveSpeed * Time.deltaTime;
    }

    private void HandleDragPan()
    {
        if (gameInput.IsPanPressed())
        {
            Vector2 mouseDelta = gameInput.GetLookDelta();
            Vector3 panDir = new Vector3(-mouseDelta.x, 0f, -mouseDelta.y) * 0.1f;
            targetPosition += transform.right * panDir.x + transform.forward * panDir.z;
        }
    }

    // Set to true only when the camera's ground raycast actually hits
    private bool isMouseOverGround = false;
    public bool IsMouseOverGround => isMouseOverGround;

    public Vector3 GetMousePosition()
    {
        Vector3 mousePos = gameInput.GetPointerPosition();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundMask))
        {
            lastPosition = hit.point;
            isMouseOverGround = true;
        }
        else
        {
            isMouseOverGround = false;
        }
        return lastPosition;
    }

    #endregion

    #region Zoom

    private void HandleZoom()
    {
        scroll = gameInput.GetZoomInput();

        switch (zoomMode)
        {
            case ZoomMode.FOV:
                HandleCameraZoomFOV(scroll);
                break;
            case ZoomMode.MoveForward:
                HandleCameraZoomMoveFoward(scroll);
                break;
            case ZoomMode.LowerY:
                HandleZoomLowerY(scroll);
                break;
            case ZoomMode.Slide:
                HandleZoomSlide(scroll);
                break;
        }
    }

    public void FocusOnChunk(Vector3 centerPosition)
    {
        // Cancel any active transitions
        isFocusing = false;
        
        // Offset Z backwards slightly to account for the viewing angle looking "forward" into the grid
        Vector3 targetOffset = new Vector3(centerPosition.x, centerPosition.y , centerPosition.z - 5f);

        // Instruct camera to lerp to this position naturally
        targetPosition = targetOffset;
    }

    private void HandleCameraZoomFOV(float scroll)
    {
        if (scroll > 0)
        {
            targetFOV -= zoomAmount; // Zoom in
        }
        if (scroll < 0)
        {
            targetFOV += zoomAmount; // Zoom out
        }

        targetFOV = Mathf.Clamp(targetFOV, fovMin, fovMax); // Clamp the field of view

        cinemachineCamera.Lens.FieldOfView = Mathf.Lerp(cinemachineCamera.Lens.FieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
    }

    private void HandleCameraZoomMoveFoward(float scroll)
    {
        Vector3 zoomDir = followOffset.normalized;

        if (scroll > 0)
        {
            followOffset -= zoomDir * zoomAmount; // Zoom in
        }
        if (scroll < 0)
        {
            followOffset += zoomDir * zoomAmount; // Zoom out
        }

        if (followOffset.magnitude < followOffsetMin || Vector3.Dot(followOffset, zoomDir) < 0)
        {
            followOffset = zoomDir * followOffsetMin;
        }

        if (followOffset.magnitude > followOffsetMax)
        {
            followOffset = zoomDir * followOffsetMax;
        }

        followComponent.FollowOffset = Vector3.Lerp(followComponent.FollowOffset, followOffset, Time.deltaTime * zoomSpeed);
    }

    private void HandleZoomLowerY(float scroll)
    {
        if (scroll > 0)
        {
            followOffset.y -= zoomAmount; // Zoom in
        }
        if (scroll < 0)
        {
            followOffset.y += zoomAmount; // Zoom out
        }

        followOffset.y = Mathf.Clamp(followOffset.y, followOffsetMinY, followOffsetMaxY); // Clamp Y offset

        followComponent.FollowOffset = Vector3.Lerp(followComponent.FollowOffset, followOffset, Time.deltaTime * zoomSpeed);
    }
    
    private void HandleZoomSlide(float scroll)
    {
        if (scroll == 0f) 
        {
            // Just continuously lerp to target regardless
            followComponent.FollowOffset = Vector3.Lerp(followComponent.FollowOffset, followOffset, Time.deltaTime * zoomSpeed);
            return;
        }

        Vector3 zoomDirLocal = followOffset.normalized; // In relation to rig

        if (scroll > 0) // Zoom In
        {
            // First, push down Y if it's over minimum
            if (followOffset.y > followOffsetMinY)
            {
                followOffset.y -= zoomAmount;
                // If we overshoot, carry over to forward motion
                if (followOffset.y < followOffsetMinY)
                {
                    float remainder = followOffsetMinY - followOffset.y;
                    followOffset.y = followOffsetMinY;
                    followOffset -= zoomDirLocal * (remainder * slideForwardMultiplier); 
                }
            }
            else
            {
                // Y is at bottom, move forward
                followOffset -= zoomDirLocal * (zoomAmount * slideForwardMultiplier);
            }
        }
        else if (scroll < 0) // Zoom Out
        {
            // First, pull backward if magnitude is less than max and Y is minimum
            if (followOffset.magnitude < followOffsetMax && followOffset.y <= followOffsetMinY + 0.1f)
            {
                followOffset += zoomDirLocal * (zoomAmount * slideForwardMultiplier);
                
                // If it hits back limit or something, start raising Y?
                // Let's just say we can always raise Y if magnitude reaches a certain point.
                // Simple logic: If we pulled back past limit, transfer to Y
                if (followOffset.magnitude > followOffsetMax)
                {
                    float remainder = followOffset.magnitude - followOffsetMax;
                    followOffset = followOffset.normalized * followOffsetMax;
                    followOffset.y += (remainder / slideForwardMultiplier);
                }
            }
            else
            {
                // We are pulled back enough (or Y is already rising), raise Y
                followOffset.y += zoomAmount;
            }
        }

        // Final Clamps
        followOffset.y = Mathf.Clamp(followOffset.y, followOffsetMinY, followOffsetMaxY);

        // Prevent inversion or zooming too close by comparing direction against the original un-zoomed direction
        if (followOffset.magnitude < followOffsetMin || Vector3.Dot(followOffset, zoomDirLocal) < 0f)
        {
            followOffset = zoomDirLocal * followOffsetMin;
            // Guarantee we don't breach Y floor while clamped to minimum magnitude
            if (followOffset.y < followOffsetMinY) followOffset.y = followOffsetMinY;
        }

        followComponent.FollowOffset = Vector3.Lerp(followComponent.FollowOffset, followOffset, Time.deltaTime * zoomSpeed);
    }

    #endregion

    // Focus Logic
    private Vector3 targetFocusPos;
    private bool isFocusing = false;
    private float focusSpeed = 5f;

    public void FocusOn(Vector3 targetPos)
    {
        targetFocusPos = targetPos;
        // Keep current Y? Or zoom in? User said "lerp toward it".
        // Usually we keep Y and just pan.
        targetFocusPos.y = transform.position.y; 
        isFocusing = true;
    }

    private void HandleFocus()
    {
        if (isFocusing)
        {
            targetPosition = Vector3.Lerp(targetPosition, targetFocusPos, Time.deltaTime * focusSpeed);
            if (Vector3.Distance(targetPosition, targetFocusPos) < 0.1f)
            {
                isFocusing = false;
            }
        }
    }
}

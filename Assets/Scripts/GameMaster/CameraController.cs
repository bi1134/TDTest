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

    [Header("Advanced Options")]
    public bool enableEdgeScroll = true;
    public bool useDragPan = false;
    public int edgeScrollSize = 20;

    public enum ZoomMode { FOV, MoveForward, LowerY }
    [SerializeField] private ZoomMode zoomMode = ZoomMode.FOV;

    // Internal
    private Vector3 lastPosition;
    private CinemachineFollow followComponent;
    private Vector3 followOffset;
    private float targetFOV = 60f;
    float scroll = 0;


    private void Start()
    {
        followComponent = cinemachineCamera.GetComponent<CinemachineFollow>();
        followOffset = followComponent.FollowOffset;
        targetFOV = cinemachineCamera.Lens.FieldOfView;
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
    }

    #region Movement

    private void HandleMovement()
    {
        Vector2 input = gameInput.GetMoveInput();
        Vector3 dir = transform.forward * input.y + transform.right * input.x;
        transform.position += dir * moveSpeed * Time.deltaTime;
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
        transform.position += moveDir * moveSpeed * Time.deltaTime;
    }

    private void HandleDragPan()
    {
        if (gameInput.IsPanPressed())
        {
            Vector2 mouseDelta = gameInput.GetLookDelta();
            Vector3 panDir = new Vector3(-mouseDelta.x, 0f, -mouseDelta.y) * 0.1f;
            transform.position += transform.right * panDir.x + transform.forward * panDir.z;
        }
    }

    public Vector3 GetMousePosition()
    {
        Vector3 mousePos = gameInput.GetPointerPosition();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundMask))
        {
            lastPosition = hit.point;
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
        }
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

        if (followOffset.magnitude < followOffsetMin)
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

    #endregion
}

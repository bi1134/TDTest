using UnityEngine;

public class GameInputs : MonoBehaviour
{
    private PlayerInputActions inputActions;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
        inputActions.Camera.Enable();
    }

    public Vector2 GetMoveInput()
    {
        Vector2 inputVector = inputActions.Camera.Move.ReadValue<Vector2>(); ;

        inputVector = inputVector.normalized; // Normalize the vector to ensure consistent speed
        return inputVector;
    }


    public float GetZoomInput()
    {
        return inputActions.Camera.Zoom.ReadValue<Vector2>().y;
    }

    public Vector2 GetLookDelta() => inputActions.Camera.Look.ReadValue<Vector2>();
    public Vector2 GetPointerPosition() => inputActions.Camera.Pointer.ReadValue<Vector2>();

    public bool IsPanPressed() => inputActions.Camera.Pan.IsPressed();
    public float GetRotateInput() => inputActions.Camera.Rotate.ReadValue<float>();
}

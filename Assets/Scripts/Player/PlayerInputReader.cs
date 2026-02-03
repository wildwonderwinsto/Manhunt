using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool CrouchPressed { get; private set; }

    void OnMove(InputValue value) => MoveInput = value.Get<Vector2>();
    void OnLook(InputValue value) => LookInput = value.Get<Vector2>();
    void OnJump(InputValue value) => JumpPressed = value.isPressed;
    void OnSprint(InputValue value) => SprintHeld = value.isPressed;
    void OnCrouch(InputValue value) => CrouchPressed = value.isPressed;

        // Use InputAction.CallbackContext's timing info
    public bool WasJumpPressedThisPhysicsStep()
    {
        return JumpPressed && lastJumpPressTime >= Time.fixedTime - Time.fixedDeltaTime;
    }
}
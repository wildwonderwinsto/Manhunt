using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool CrouchPressed { get; private set; }

    private float lastJumpPressTime;

    void OnMove(InputValue value) => MoveInput = value.Get<Vector2>();
    void OnLook(InputValue value) => LookInput = value.Get<Vector2>();   
    void OnSprint(InputValue value) => SprintHeld = value.isPressed;
    void OnCrouch(InputValue value) => CrouchPressed = value.isPressed;

    void OnJump(InputValue value)
    {
        JumpPressed = value.isPressed;
        if (value.isPressed)
        {
            lastJumpPressTime = Time.time;
        }
    }

    public bool WasJumpPressedThisPhysicsStep()
    {
        return JumpPressed && lastJumpPressTime >= Time.fixedTime - Time.fixedDeltaTime;
    }
}
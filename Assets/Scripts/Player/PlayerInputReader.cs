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

    public bool JumpHeld { get; private set; } // ✅ Added for variable gravity

    void OnJump(InputValue value)
    {
        JumpPressed = value.isPressed;
        JumpHeld = value.isPressed; // ✅ Track held state separate from trigger
        if (value.isPressed)
        {
            lastJumpPressTime = Time.time;
        }
    }

    // ✅ ADD THESE BACK:
    public void ConsumeJump() => JumpPressed = false;
    public void ConsumeCrouch() => CrouchPressed = false;

    
}
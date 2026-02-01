using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core.Input
{
    /// <summary>
    /// Central input hub that listens to the Input System and broadcasts events.
    /// 
    /// This ScriptableObject decouples gameplay systems from the Input System implementation,
    /// allowing any system (movement, camera, UI) to listen to input without tight coupling.
    /// 
    /// SETUP INSTRUCTIONS:
    /// 1. Select your Input Actions asset (.inputactions file)
    /// 2. In the Inspector, enable "Generate C# Class"
    /// 3. Name the generated class "GameInput"
    /// 4. Click "Apply"
    /// 5. Right-click in Project window -> Create -> Game -> Input Reader
    /// 6. Assign the created InputReader asset to PlayerController in the inspector
    /// </summary>
    [CreateAssetMenu(fileName = "InputReader", menuName = "Game/Input Reader")]
    public class PlayerInputReader : ScriptableObject, GameInput.IPlayerActions
    {
        #region Events
        /// <summary>Fired whenever movement input changes (WASD). Broadcasts raw input vector.</summary>
        public event System.Action<Vector2> MoveInputChanged = delegate { };

        /// <summary>Fired whenever look input changes (mouse/gamepad look). Broadcasts raw input vector.</summary>
        public event System.Action<Vector2> LookInputChanged = delegate { };

        /// <summary>Fired when jump button is pressed down (ascending phase).</summary>
        public event System.Action JumpPressed = delegate { };

        /// <summary>Fired when jump button is released (enables variable jump height mechanics).</summary>
        public event System.Action JumpReleased = delegate { };

        /// <summary>Fired when sprint button is pressed down.</summary>
        public event System.Action SprintPressed = delegate { };

        /// <summary>Fired when sprint button is released.</summary>
        public event System.Action SprintReleased = delegate { };
        #endregion

        #region Input State Properties
        /// <summary>Current movement input (X=right, Y=forward). Updated continuously.</summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>Current look input (X=yaw, Y=pitch). Updated continuously.</summary>
        public Vector2 LookInput { get; private set; }

        /// <summary>Is jump button currently held? Used for variable jump height.</summary>
        public bool IsJumpHeld { get; private set; }

        /// <summary>Is sprint button currently held?</summary>
        public bool IsSprintHeld { get; private set; }
        #endregion

        #region Runtime State
        private GameInput _gameInput;
        #endregion

        #region Lifecycle

        private void OnEnable()
        {
            if (_gameInput == null)
            {
                _gameInput = new GameInput();
                _gameInput.Player.SetCallbacks(this);
            }

            _gameInput.Player.Enable();
        }

        private void OnDisable()
        {
            if (_gameInput != null)
            {
                _gameInput.Player.Disable();
            }
        }

        #endregion

        #region Input System Callbacks (IPlayerActions)

        /// <summary>
        /// Called by Input System when movement input changes.
        /// Broadcasts to all listeners so they can react to new input.
        /// </summary>
        public void OnMove(InputAction.CallbackContext context)
        {
            if (context.action == null) return;
            MoveInput = context.ReadValue<Vector2>();
            MoveInputChanged?.Invoke(MoveInput);
        }

        /// <summary>
        /// Called by Input System when look input changes.
        /// Broadcasts to all listeners so they can react to new camera input.
        /// </summary>
        public void OnLook(InputAction.CallbackContext context)
        {
            if (context.action == null) return;
            LookInput = context.ReadValue<Vector2>();
            LookInputChanged?.Invoke(LookInput);
        }

        /// <summary>Jump input handler with phase detection for variable jump height.</summary>
        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.action == null) return;
            if (context.phase == InputActionPhase.Performed)
            {
                IsJumpHeld = true;
                JumpPressed?.Invoke();
            }
            else if (context.phase == InputActionPhase.Canceled)
            {
                IsJumpHeld = false;
                JumpReleased?.Invoke();
            }
        }

        /// <summary>Sprint input handler with phase detection.</summary>
        public void OnSprint(InputAction.CallbackContext context)
        {
            if (context.action == null) return;
            if (context.phase == InputActionPhase.Performed)
            {
                IsSprintHeld = true;
                SprintPressed?.Invoke();
            }
            else if (context.phase == InputActionPhase.Canceled)
            {
                IsSprintHeld = false;
                SprintReleased?.Invoke();
            }
        }

        /// <summary>Reserved for future attack system implementation.</summary>
        public void OnAttack(InputAction.CallbackContext context) { }
        
        /// <summary>Reserved for future interaction system.</summary>
        public void OnInteract(InputAction.CallbackContext context) { }
        
        /// <summary>Reserved for future crouch system implementation.</summary>
        public void OnCrouch(InputAction.CallbackContext context) { }
        
        /// <summary>Reserved for future weapon selection system.</summary>
        public void OnPrevious(InputAction.CallbackContext context) { }
        
        /// <summary>Reserved for future weapon selection system.</summary>
        public void OnNext(InputAction.CallbackContext context) { }

        #endregion
    }
}

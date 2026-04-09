using UnityEngine;
using UnityEngine.InputSystem;

namespace SupanthaPaul
{
	public class InputSystem : MonoBehaviour
	{
		// input string caching
		static readonly string HorizontalInput = "Horizontal";
		static readonly string JumpInput = "Jump";
		static readonly string DashInput = "Dash";

		public static PlayerInput PlayerInput;

		public static Vector2 Movement;
		public static bool JumpWasPressed;
		public static bool JumpIsHeld;
		public static bool JumpWasReleased;
        public static bool DashWasPressed;

        private InputAction moveAction;
		private InputAction jumpAction;
		private InputAction dashAction;

        private void Awake()
        {
            PlayerInput = GetComponent<PlayerInput>();

			moveAction = PlayerInput.actions["Move"];
            jumpAction = PlayerInput.actions["Jump"];
            dashAction = PlayerInput.actions["Dash"];
        }

        private void Update()
        {
            Movement = moveAction.ReadValue<Vector2>();

			JumpWasPressed = jumpAction.WasPressedThisFrame();
			JumpIsHeld = jumpAction.IsPressed();
			JumpWasReleased = jumpAction.WasReleasedThisFrame();

			DashWasPressed = dashAction.WasPressedThisFrame();
        }

        public static float HorizontalRaw()
		{
			return Input.GetAxisRaw(HorizontalInput);
		}

		/*
		public static bool Jump()
		{
			return Input.GetButtonDown(JumpInput);
		}

		public static bool Dash()
		{
			return Input.GetButtonDown(DashInput);
		}
		*/
	}
}

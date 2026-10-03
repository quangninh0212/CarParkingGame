using UnityEngine;

namespace CarParkingGame.Vehicle
{
    public struct VehicleInputState
    {
        public float throttle;
        public float steer;
        public bool brakeHeld;

        public static VehicleInputState Idle => new VehicleInputState();
    }

    // Keeps input reading out of the vehicle physics. SimpleInput stays the mobile
    // path; the keyboard path is the editor/debug fallback that already existed.
    public static class VehicleInput
    {
        public const string VerticalAxis = "Vertical";
        public const string HorizontalAxis = "Horizontal";

        // SimpleInput's on-screen button is named "Break" in the scene. The spelling
        // is wrong but renaming it would break that serialized wiring, so it stays.
        public const string BrakeButton = "Break";

        // Player steering sensitivity from Settings. Separate from CarController's
        // turnSensitivity, which is the vehicle's own physics tuning.
        public static float SteeringSensitivity = 1f;

        public static VehicleInputState ReadKeyboard()
        {
            var state = new VehicleInputState
            {
                throttle = Input.GetAxis(VerticalAxis),
                steer = ScaleSteer(Input.GetAxis(HorizontalAxis)),
                brakeHeld = Input.GetKey(KeyCode.Space)
            };

#if ENABLE_INPUT_SYSTEM
            // The project runs both input backends. Not every Editor view feeds key
            // presses to the legacy Input Manager, so the new Input System's keyboard is
            // read as well and whichever reports the stronger input wins.
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;

            if (keyboard != null)
            {
                state = Merge(state, new VehicleInputState
                {
                    throttle = Digital(
                        keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                        keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed),
                    steer = ScaleSteer(Digital(
                        keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed,
                        keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)),
                    brakeHeld = keyboard.spaceKey.isPressed
                });
            }
#endif

            return state;
        }

        private static float Digital(bool positive, bool negative)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

        public static VehicleInputState ReadMobile()
        {
            return new VehicleInputState
            {
                throttle = SimpleInput.GetAxis(VerticalAxis),
                steer = ScaleSteer(SimpleInput.GetAxis(HorizontalAxis)),
                brakeHeld = SimpleInput.GetButton(BrakeButton)
            };
        }

        // Combines two input sources, taking whichever is pushed harder on each axis.
        public static VehicleInputState Merge(VehicleInputState a, VehicleInputState b)
        {
            return new VehicleInputState
            {
                throttle = Mathf.Abs(a.throttle) >= Mathf.Abs(b.throttle) ? a.throttle : b.throttle,
                steer = Mathf.Abs(a.steer) >= Mathf.Abs(b.steer) ? a.steer : b.steer,
                brakeHeld = a.brakeHeld || b.brakeHeld
            };
        }

        private static float ScaleSteer(float steer)
        {
            return Mathf.Clamp(steer * SteeringSensitivity, -1f, 1f);
        }
    }
}

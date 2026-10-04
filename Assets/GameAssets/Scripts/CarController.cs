using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;
using CarParkingGame.Vehicle;

public class CarController : MonoBehaviour
{
    public enum ControlMode
    {
        Keyboard,
        Buttons
    };

    public enum Axel
    {
        Front,
        Rear
    };

    [Serializable]
    public struct Wheel
    {
        public GameObject wheelModel;
        public WheelCollider wheelCollider;
        public Axel axel;
    }

    public ControlMode control;

    public float maxAcceleration = 20f;
    public float brakeAcceleration = 200000f;

    public float turnSensitivity = 1.2f;
    public float maxSteerAngle = 30f;

    public Vector3 centerOfMass;

    public List<Wheel> wheels;

    float moveInput;
    float steerInput;

    private Rigidbody carRB;

    public Transform carPos;

    private float authoredMaxAcceleration;
    private float authoredBrakeAcceleration;
    private float authoredTurnSensitivity;
    private float accelerationMultiplier = 1f;
    private float brakeMultiplier = 1f;
    private float handlingMultiplier = 1f;
    private bool vehicleEnabled = true;
    private bool brakeHeld;
    private bool brakesApplied;

    private void Awake()
    {
        carRB = GetComponent<Rigidbody>();

        // The authored values are the baseline for both freezing the car and garage
        // upgrades, so the old "maxAcceleration = 0, then = 5" pattern can no longer
        // permanently retune it and an upgrade is never applied on top of itself.
        authoredMaxAcceleration = maxAcceleration;
        authoredBrakeAcceleration = brakeAcceleration;
        authoredTurnSensitivity = turnSensitivity;
    }

    private void Start()
    {
        carRB.centerOfMass = centerOfMass;
        StartCoroutine(SetCar());
    }

    private void Update()
    {
        GetInputs();
        WheelsAnimation();
    }

    private void LateUpdate()
    {
        Move();
        Steer();
        Brake();
    }

    IEnumerator SetCar()
    {
        yield return new WaitForSeconds(1f);
        this.gameObject.transform.position = carPos.position;
    }

    public float MoveInput => moveInput;

    public float ThrottleInput => moveInput;
    public float SteeringInput => steerInput;
    public bool IsBraking => brakesApplied;

    // The brake actually being held, as opposed to IsBraking, which is also true whenever
    // the car is coasting with no throttle. Only the held one is worth a noise.
    public bool IsBrakeHeld => brakeHeld;
    public bool VehicleEnabled => vehicleEnabled;
    public Vector3 Velocity => carRB != null ? carRB.linearVelocity : Vector3.zero;
    public float CurrentSpeed => carRB != null ? carRB.linearVelocity.magnitude : 0f;
    public float CurrentSpeedKmh => CurrentSpeed * 3.6f;

    // Kept because the scene may invoke these by name through UnityEvents.
    public void SetMoveInput(float input)
    {
        moveInput = input;
    }

    public void SteerInput(float input)
    {
        steerInput = input;
    }

    // Replaces zeroing maxAcceleration to freeze the car: input is dropped, the brakes
    // go on, and the car's authored acceleration is restored when control returns.
    public void SetVehicleEnabled(bool enabled)
    {
        vehicleEnabled = enabled;
        moveInput = 0f;
        steerInput = 0f;
        brakeHeld = false;

        if (enabled)
        {
            ApplyPerformance();
        }
        else
        {
            ApplyBrakeTorque(true);
        }
    }

    // Garage upgrades scale the authored values rather than overwriting them, so levels
    // never stack and downgrading is just a smaller multiplier.
    public void SetPerformanceMultipliers(float acceleration, float brake, float handling)
    {
        accelerationMultiplier = Mathf.Clamp(acceleration, 0.5f, 2f);
        brakeMultiplier = Mathf.Clamp(brake, 0.5f, 2f);
        handlingMultiplier = Mathf.Clamp(handling, 0.5f, 2f);

        ApplyPerformance();
    }

    private void ApplyPerformance()
    {
        maxAcceleration = authoredMaxAcceleration * accelerationMultiplier;
        brakeAcceleration = authoredBrakeAcceleration * brakeMultiplier;
        turnSensitivity = authoredTurnSensitivity * handlingMultiplier;
    }

    void GetInputs()
    {
        if (!vehicleEnabled)
        {
            moveInput = 0f;
            steerInput = 0f;
            brakeHeld = false;
            return;
        }

        VehicleInputState input = control == ControlMode.Keyboard
            ? VehicleInput.ReadKeyboard()
            : VehicleInput.ReadMobile();

#if UNITY_EDITOR || UNITY_STANDALONE
        // Driving the on-screen joystick with a laptop mouse is close to impossible, so
        // on desktop the keyboard works alongside it. Phone builds are unaffected.
        if (control == ControlMode.Buttons)
        {
            input = VehicleInput.Merge(input, VehicleInput.ReadKeyboard());
        }
#endif

        moveInput = input.throttle;
        steerInput = input.steer;
        brakeHeld = input.brakeHeld;
    }

    void Move()
    {
        float torqueInput = vehicleEnabled ? moveInput : 0f;

        foreach (var wheel in wheels)
        {
            wheel.wheelCollider.motorTorque =
                torqueInput * 600 * maxAcceleration * Time.deltaTime;
        }
    }

    void Steer()
    {
        foreach (var wheel in wheels)
        {
            if (wheel.axel == Axel.Front)
            {
                var steerAngle =
                    steerInput * turnSensitivity * maxSteerAngle;

                wheel.wheelCollider.steerAngle =
                    Mathf.Lerp(
                        wheel.wheelCollider.steerAngle,
                        steerAngle,
                        0.5f
                    );
            }
        }
    }

    void Brake()
    {
        // Braking whenever there is no throttle is what makes the car settle when the
        // joystick is released, so that stays. What changes is that the on-screen brake
        // button now also bites while the throttle is held - previously the button was
        // dead, because the surrounding condition was true in every control mode anyway.
        ApplyBrakeTorque(!vehicleEnabled || brakeHeld || moveInput == 0);
    }

    void ApplyBrakeTorque(bool applyBrakes)
    {
        brakesApplied = applyBrakes;

        float torque = applyBrakes ? 300 * brakeAcceleration * Time.deltaTime : 0f;

        foreach (var wheel in wheels)
        {
            if (wheel.wheelCollider != null)
            {
                wheel.wheelCollider.brakeTorque = torque;
            }
        }
    }

    void WheelsAnimation()
    {
        foreach (var wheel in wheels)
        {
            Quaternion rot;
            Vector3 pos;

            wheel.wheelCollider.GetWorldPose(out pos, out rot);

            wheel.wheelModel.transform.position = pos;
            wheel.wheelModel.transform.rotation = rot;
        }
    }
}
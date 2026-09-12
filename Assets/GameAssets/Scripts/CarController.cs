using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;

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

    private void Start()
    {
        carRB = GetComponent<Rigidbody>();
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

        if(Input.GetKey(KeyCode.Space) || control == ControlMode.Keyboard)
        {
            Brake();
        }
        else if(SimpleInput.GetButton("Break") || control == ControlMode.Buttons)
        {
            Brake();
        }
    }

    IEnumerator SetCar()
    {
        yield return new WaitForSeconds(1f);
        this.gameObject.transform.position = carPos.position;
    }

    public float MoveInput => moveInput;

    public void SetMoveInput(float input)
    {
        moveInput = input;
    }

    public void SteerInput(float input)
    {
        steerInput = input;
    }

    void GetInputs()
    {
        if (control == ControlMode.Keyboard)
        {
            moveInput = Input.GetAxis("Vertical");
            steerInput = Input.GetAxis("Horizontal");
        }

        else if(control == ControlMode.Buttons)
        {
            moveInput = SimpleInput.GetAxis("Vertical");
            steerInput = SimpleInput.GetAxis("Horizontal");
        }
    }

    void Move()
    {
        foreach (var wheel in wheels)
        {
            wheel.wheelCollider.motorTorque =
                moveInput * 600 * maxAcceleration * Time.deltaTime;
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
        if (moveInput == 0)
        {
            foreach (var wheel in wheels)
            {
                wheel.wheelCollider.brakeTorque =
                    300 * brakeAcceleration * Time.deltaTime;
            }
        }
        else
        {
            foreach (var wheel in wheels)
            {
                wheel.wheelCollider.brakeTorque = 0;
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
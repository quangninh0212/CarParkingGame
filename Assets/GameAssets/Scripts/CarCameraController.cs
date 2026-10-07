using UnityEngine;
using UnityEngine.EventSystems;

public class CarCameraController : MonoBehaviour
{
    public Transform[] cars;

    public Vector3 offset = new Vector3(0, 1.2f, -10);
    public float followSmoothSpeed = 0f;

    public float mouseSensitivity = 500f;

    // Scaled by the player's Settings slider. Static for the same reason the steering
    // sensitivity is: the settings are applied at startup, before anything has gone
    // looking for the camera, and there is only ever one of these in the scene.
    public static float SensitivityScale = 1f;
    public float rotationSmoothSpeed = 0f;

    public float minVerticalAngle = -6f;
    public float maxVerticalAngle = 60f;

    private Transform activeCar;
    private Vector3 currentOffset;
    private Vector3 currentVelocity;
    private Vector3 rotationVelocity;
    private Vector2 mouseDelta;

    void Start()
    {
        currentOffset = offset;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        activeCar = FindActiveCar();

        if (activeCar == null) return;

        FollowCar();
        HandleMouseRotation();
    }

    Transform FindActiveCar()
    {
        foreach (Transform car in cars)
        {
            if (car.gameObject.activeSelf)
            {
                return car;
            }
        }

        return null;
    }

    void FollowCar()
    {
        Vector3 desiredPosition = activeCar.position + currentOffset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            followSmoothSpeed
        );

        transform.LookAt(activeCar.position + Vector3.up * 1.5f);
    }

    void HandleMouseRotation()
    {
        if(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        float sensitivity = mouseSensitivity * SensitivityScale;

        float mouseX =
            Input.GetAxis("Mouse X") *
            sensitivity *
            Time.deltaTime;

        float mouseY =
            -Input.GetAxis("Mouse Y") *
            sensitivity *
            Time.deltaTime;

        mouseDelta += new Vector2(mouseX, mouseY);

        mouseDelta.y = Mathf.Clamp(
            mouseDelta.y,
            minVerticalAngle,
            maxVerticalAngle
        );

        Quaternion targetRotation =
            Quaternion.Euler(mouseDelta.y, mouseDelta.x, 0);

        currentOffset = Vector3.SmoothDamp(
            currentOffset,
            targetRotation * offset,
            ref rotationVelocity,
            rotationSmoothSpeed
        );
    }
}
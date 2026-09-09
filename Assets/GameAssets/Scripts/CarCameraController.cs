using UnityEngine;
using UnityEngine.EventSystems;

public class CarCameraController : MonoBehaviour
{
    public Transform[] cars;

    public Vector3 offset = new Vector3(0, 1.2f, -10);
    public float followSmoothSpeed = 0f;

    public float mouseSensitivity = 500f;
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

        float mouseX =
            Input.GetAxis("Mouse X") *
            mouseSensitivity *
            Time.deltaTime;

        float mouseY =
            -Input.GetAxis("Mouse Y") *
            mouseSensitivity *
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
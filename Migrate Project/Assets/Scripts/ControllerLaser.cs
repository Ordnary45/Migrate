using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class ControllerLaser : MonoBehaviour
{
    [Header("Laser Settings")]
    [SerializeField] private Transform controllerTransform;
    [SerializeField] private float laserMaxDistance = 10f;
    [SerializeField] private Color laserColor = Color.red;
    [SerializeField] private float laserWidth = 0.01f;

    [Header("Visual")]
    [SerializeField] private GameObject laserEndPoint;
    [SerializeField] private LineRenderer lineRenderer;

    private GameObject currentHitObject;
    private Button currentButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Setup line renderer if not assigned
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }
        }

        // Configure line renderer
        lineRenderer.startWidth = laserWidth;
        lineRenderer.endWidth = laserWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = laserColor;
        lineRenderer.endColor = laserColor;

        // Create laser end point if not assigned
        if (laserEndPoint == null)
        {
            laserEndPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            laserEndPoint.transform.localScale = Vector3.one * 0.05f;
            laserEndPoint.GetComponent<Renderer>().material.color = laserColor;
            Destroy(laserEndPoint.GetComponent<Collider>());
            laserEndPoint.transform.SetParent(transform);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (controllerTransform == null) return;

        // Cast ray from controller
        Ray ray = new Ray(controllerTransform.position, controllerTransform.forward);
        RaycastHit hit;

        bool hitSomething = Physics.Raycast(ray, out hit, laserMaxDistance);

        if (hitSomething)
        {
            // Update laser visuals
            lineRenderer.SetPosition(0, controllerTransform.position);
            lineRenderer.SetPosition(1, hit.point);

            if (laserEndPoint != null)
            {
                laserEndPoint.transform.position = hit.point;
                laserEndPoint.SetActive(true);
            }

            // Check if hitting UI element
            Button button = hit.collider.GetComponent<Button>();
            if (button != null)
            {
                if (currentButton != button)
                {
                    // Exit previous button
                    if (currentButton != null)
                    {
                        ExecuteEvents.Execute(currentButton.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                    }

                    // Enter new button
                    currentButton = button;
                    ExecuteEvents.Execute(currentButton.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                }

                // Check for click
                if (IsClickTriggered())
                {
                    ClickButton(currentButton);
                }
            }
            else
            {
                ClearButtonHover();
            }
        }
        else
        {
            // No hit - draw the laser to max distance
            Vector3 endPoint = controllerTransform.position + controllerTransform.forward * laserMaxDistance;
            lineRenderer.SetPosition(0, controllerTransform.position);
            lineRenderer.SetPosition(1, endPoint);

            if (laserEndPoint != null)
            {
                laserEndPoint.transform.position = endPoint;
                laserEndPoint.SetActive(true);
            }

            ClearButtonHover();
        }
    }

    public void SetLaserColor(Color color)
    {
        // If line renderer and laser point exist change color
        laserColor = color;
        if (lineRenderer != null)
        {
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }
        if (laserEndPoint != null)
        {
            laserEndPoint.GetComponent<Renderer>().material.color = color;
        }
    }

    bool IsClickTriggered()
    {
        // Check Oculus Touch controllers
        bool triggerPressed = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger) ||
                              OVRInput.Get(OVRInput.Button.SecondaryIndexTrigger);

        return triggerPressed;
    }

    void ClickButton(Button button)
    {
        if (button != null && button.interactable)
        {
            // Simulate button click
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }
    }

    void ClearButtonHover()
    {
        if (currentButton != null)
        {
            ExecuteEvents.Execute(currentButton.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            currentButton = null;
        }
    }
}

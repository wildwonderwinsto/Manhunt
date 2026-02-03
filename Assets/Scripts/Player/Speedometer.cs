using UnityEngine;

public class Speedometer : MonoBehaviour
{
    private Rigidbody rb;
    private float displaySpeed;

    void Start() => rb = GetComponent<Rigidbody>();

    void FixedUpdate()
    {
        // Calculate speed in physics step
        Vector3 hVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        displaySpeed = hVel.magnitude * 32f; // Cache for rendering
    }

    void OnGUI()
    {
        // Just display cached value
        GUI.Label(new Rect(Screen.width / 2 - 50, Screen.height - 100, 200, 100), Mathf.Round(displaySpeed).ToString(), style);
    }
}
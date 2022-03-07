using UnityEngine;

public class SimpleCameraController : MonoBehaviour
{
    public float mouseSensitivity = 100f;
    public float moveSpeed = 1f;

    float xRotation = 0f;
    float yRotation = 0f;

    // Update is called once per frame
    void Update()
    {

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        yRotation += mouseX;

        transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);


        Vector3 p_Velocity = new Vector3();
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            p_Velocity += new Vector3(0, 0, moveSpeed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            p_Velocity += new Vector3(0, 0, -moveSpeed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            p_Velocity += new Vector3(-moveSpeed * Time.deltaTime, 0, 0);
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            p_Velocity += new Vector3(moveSpeed * Time.deltaTime, 0, 0);
        }
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.PageDown))
        {
            p_Velocity += new Vector3(0, -moveSpeed * Time.deltaTime, 0);
        }
        if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Keypad0) || Input.GetKey(KeyCode.PageUp))
        {
            p_Velocity += new Vector3(0, moveSpeed * Time.deltaTime, 0);
        }
        transform.Translate(p_Velocity);

    }
}

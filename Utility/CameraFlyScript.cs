using UnityEngine;
using System.Collections;

public class CameraFlyScript : MonoBehaviour {

    public float MaxSpeed = 5.0f;
    Vector3 speed;
    Vector3 targetSpeed;

	// Update is called once per frame
	void Update ()
    {
        targetSpeed = Vector3.zero;

        if (Input.GetKey(KeyCode.W))
            targetSpeed += transform.forward;
        if (Input.GetKey(KeyCode.A))
            targetSpeed += -transform.right;
        if (Input.GetKey(KeyCode.S))
            targetSpeed += -transform.forward;
        if (Input.GetKey(KeyCode.D))
            targetSpeed += transform.right;
        if (Input.GetKey(KeyCode.E))
            targetSpeed += transform.up;
        if (Input.GetKey(KeyCode.Q))
            targetSpeed += -transform.up;

        targetSpeed.Normalize();

        if (Input.GetKey(KeyCode.LeftShift))
            targetSpeed *= 4.0f;

        targetSpeed *= MaxSpeed;

        speed = Vector3.Lerp(speed, targetSpeed, Time.deltaTime * 7.0f);

        transform.position += speed * Time.deltaTime;
    }
}

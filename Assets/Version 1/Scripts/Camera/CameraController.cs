using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;
    public Vector3 offset;

    Vector3 offsetDirection;
    float targetDistance;

    Light cameraLight;
    // Start is called before the first frame update
    void Start()
    {
        offsetDirection = offset.normalized;
        targetDistance = offset.magnitude;
        cameraLight = GetComponentInChildren<Light>(true);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            cameraLight.enabled = !cameraLight.isActiveAndEnabled;
        }

        targetDistance = Mathf.Clamp(targetDistance + Input.GetAxis("Mouse ScrollWheel"), 1, 20);

        
        float distanceToTarget = (transform.position - target.position).magnitude;
        Vector3 transformedOffset = target.TransformDirection(offsetDirection * targetDistance);
        transform.position = Vector3.Lerp(transform.position, target.position + transformedOffset, 5*(distanceToTarget/targetDistance)*Time.deltaTime);
        transform.LookAt(target);
    }
}

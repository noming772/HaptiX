using UnityEngine;

public class HandAnimationController : MonoBehaviour
{
    public string handKey = "HAND_A";

    private Animator animator;

    public bool ShouldFollowRayRotation = true;
    public GameObject grabColliderObject;
    public GameObject idleColliderObject;

    public bool IsGrabbing { get; private set; } = false;
    public bool IsInContactWithObject { get; private set; } = false;

    public Transform grabAnchor;

    private Vector3? followTargetPosition = null;
    private Quaternion baseRotation = Quaternion.identity;
    private bool hasInitializedRotation = false;
    private Quaternion accumulatedRotation = Quaternion.identity;

    public Vector3 rotationCorrection = new Vector3(10f, 115f, 65f);

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void OnStartGrabbing(Transform targetObject)
    {
        if (IsGrabbing) return;

        animator.Play("Grab");

        IsGrabbing = true;
        UpdateColliders();
        IgnoreCollisionWith(targetObject, true);

        SetInitialRotation();
        hasInitializedRotation = true;
        accumulatedRotation = Quaternion.identity;
    }

    public void OnRelease(Transform targetObject)
    {
        if (!IsGrabbing) return;

        animator.Play("Release");

        IsGrabbing = false;
        UpdateColliders();
        IgnoreCollisionWith(targetObject, false);
    }

    public void PlayIdle()
    {
        if (!IsGrabbing)
        {
            animator.Play("Idle");
            UpdateColliders();
        }
    }

    public void SetContactWithObject(bool isContact)
    {
        IsInContactWithObject = isContact;
    }

    public void FollowRay(Ray ray)
    {
        Vector3 targetPosition = ray.origin + ray.direction.normalized * 5.0f;
        Vector3 cameraForward = Camera.main.transform.forward;

        Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
        Quaternion correction = Quaternion.Euler(rotationCorrection);
        transform.rotation = targetRotation * correction;

        followTargetPosition = targetPosition;

        if (!hasInitializedRotation)
        {
            baseRotation = transform.rotation;
            hasInitializedRotation = true;
        }
    }

    public void FollowRay(Ray ray, bool useYAxis)
    {
        float fixedZ = ray.origin.z + ray.direction.normalized.z * 5.0f;
        Vector3 targetPosition;

        if (useYAxis)
        {
            Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, fixedZ));
            if (plane.Raycast(ray, out float distance))
            {
                Vector3 intersection = ray.GetPoint(distance);
                targetPosition = new Vector3(intersection.x, intersection.y, fixedZ);
            }
            else
            {
                Vector3 fallback = ray.origin + ray.direction.normalized * 4.5f;
                targetPosition = new Vector3(fallback.x, fallback.y, fixedZ);
            }
        }
        else
        {
            float fixedY = 1.5f;
            float fixedLength = 9.5f;
            Vector3 center = ray.origin + ray.direction.normalized * (fixedLength * 0.5f);
            targetPosition = new Vector3(center.x, fixedY, fixedZ);
        }

        if (!hasInitializedRotation && !IsGrabbing)
        {
            SetInitialRotation();
            hasInitializedRotation = true;
        }

        float minY = 0.8f;
        if (targetPosition.y < minY) targetPosition.y = minY;

        if (grabAnchor != null)
        {
            Vector3 anchorOffset = grabAnchor.position - transform.position;
            transform.position = targetPosition - anchorOffset;
            grabAnchor.position = targetPosition;
        }
        else
        {
            transform.position = targetPosition;
        }
    }

    public void RotateHand(float rotationDelta)
    {
        if (Mathf.Abs(rotationDelta) > 0.01f)
        {
            Quaternion delta = Quaternion.AngleAxis(rotationDelta, transform.up);
            accumulatedRotation = accumulatedRotation * delta;

            Quaternion finalRot = baseRotation * accumulatedRotation;
            transform.rotation = finalRot;

            if (grabAnchor != null) grabAnchor.rotation = finalRot;

            Debug.Log($" delta={rotationDelta}, rotation={finalRot.eulerAngles}");
        }
    }

    public void UpdateColliders()
    {
        if (grabColliderObject != null) grabColliderObject.SetActive(IsGrabbing);
        if (idleColliderObject != null) idleColliderObject.SetActive(!IsGrabbing);
    }

    public void IgnoreCollisionWith(Transform targetObject, bool ignore)
    {
        if (targetObject == null) return;

        Collider[] handColliders = GetComponentsInChildren<Collider>();
        Collider[] targetColliders = targetObject.GetComponentsInChildren<Collider>();

        foreach (var hc in handColliders)
        {
            foreach (var tc in targetColliders)
            {
                Physics.IgnoreCollision(hc, tc, ignore);
            }
        }
    }

    private void SetInitialRotation()
    {
        baseRotation = transform.rotation;
        if (grabAnchor != null)
        {
            grabAnchor.rotation = baseRotation;
        }
        Debug.Log("baseRotation : " + baseRotation.eulerAngles);
    }

    void OnDrawGizmos()
    {
        if (grabAnchor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(grabAnchor.position, 0.01f);
        }
    }

    void LateUpdate()
    {
        if (followTargetPosition.HasValue)
        {
            Vector3 target = followTargetPosition.Value;

            if (grabAnchor != null)
            {
                Vector3 anchorOffset = grabAnchor.position - transform.position;
                Vector3 targetPos = target - anchorOffset;
                transform.position = targetPos;
                grabAnchor.position = target;
            }
            else
            {
                transform.position = target;
            }
        }
    }
}

using UnityEngine;

public class HandCollisionDetector : MonoBehaviour
{
    public HandAnimationController handController;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<DragDropable>() != null)
        {
            handController.SetContactWithObject(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<DragDropable>() != null)
        {
            handController.SetContactWithObject(false);
        }
    }
}

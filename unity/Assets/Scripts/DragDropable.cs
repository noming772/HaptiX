using UnityEngine;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

public class DragDropable : MonoBehaviour
{
    private Rigidbody rb;
    private readonly List<Transform> grabbedHands = new List<Transform>();
    private readonly HashSet<string> vibratingHands = new HashSet<string>();
    private AudioSource audioSource;

    private readonly Dictionary<Transform, Quaternion> handInitialRotations = new();
    private readonly Dictionary<Transform, Quaternion> objectInitialRotations = new();
    private readonly Dictionary<Transform, Vector3> handOffsets = new();

    private Transform GetLatestHand()
    {
        return grabbedHands.Count > 0 ? grabbedHands[grabbedHands.Count - 1] : null;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();

        if (rb == null) Debug.LogError(gameObject.name + " No Rigidbody");
        if (audioSource == null) Debug.LogWarning("No AudioSource");
    }

    public void StartDrag(Transform handAnchor, Vector3 worldOffset, Quaternion originalWorldRotation)
    {
        if (grabbedHands.Contains(handAnchor)) return;

        if (grabbedHands.Count == 0)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        grabbedHands.Add(handAnchor);
        objectInitialRotations[handAnchor] = transform.rotation;
        handInitialRotations[handAnchor] = handAnchor.rotation;
        handOffsets[handAnchor] = handAnchor.InverseTransformPoint(transform.position);

        var handController = handAnchor.root.GetComponent<HandAnimationController>();
        if (handController != null)
        {
            handController.OnStartGrabbing(transform);
        }
    }

    public void EndDrag(Transform handAnchor)
    {
        if (!grabbedHands.Contains(handAnchor)) return;

        Collider[] objectColliders = GetComponentsInChildren<Collider>();
        Collider[] handColliders = handAnchor.root.GetComponentsInChildren<Collider>();
        foreach (var oc in objectColliders)
        {
            foreach (var hc in handColliders)
            {
                Physics.IgnoreCollision(oc, hc, false);
            }
        }

        var handController = handAnchor.root.GetComponent<HandAnimationController>();
        if (handController != null)
        {
            handController.OnRelease(transform);
        }

        grabbedHands.Remove(handAnchor);
        handInitialRotations.Remove(handAnchor);
        objectInitialRotations.Remove(handAnchor);
        handOffsets.Remove(handAnchor);

        if (grabbedHands.Count == 0)
        {
            rb.useGravity = true;
            rb.isKinematic = false;
            Debug.Log($"{gameObject.name} Release");
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Hand")) return;

        string handKey = DetectHandKey(collision.transform);
        Debug.Log("Vibration Start : handKey=" + (handKey ?? "null"));

        if (!string.IsNullOrEmpty(handKey))
        {
            if (vibratingHands.Add(handKey))
                SendVibrationUDP(handKey);
        }
        else
        {
            SendVibrationUDP(null);
        }

        if (audioSource != null && !audioSource.isPlaying) audioSource.Play();
    }

    void OnCollisionExit(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Hand")) return;

        string handKey = DetectHandKey(collision.transform);
        Debug.Log("Vibration Done : handKey=" + (handKey ?? "null"));

        if (!string.IsNullOrEmpty(handKey))
        {
            if (vibratingHands.Remove(handKey))
                SendStopSoundUDP(handKey);
        }
        else
        {
            SendStopSoundUDP(null);
        }

        if (vibratingHands.Count == 0 && audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    void SendVibrationUDP(string handKey)
    {
        var targets = ResolveTargets(handKey, true);
        if (targets.Count == 0) return;

        foreach (var t in targets)
        {
            using (UdpClient c = new UdpClient())
            {
                byte[] b = Encoding.UTF8.GetBytes(t.msg);
                c.Send(b, b.Length, t.ip, UDPReceiver.PhonePort);
            }
            Debug.Log($"Send Vibration: {t.msg} ¡æ {t.ip}:{UDPReceiver.PhonePort}");
        }
    }

    void SendStopSoundUDP(string handKey)
    {
        var targets = ResolveTargets(handKey, false);
        foreach (var t in targets)
        {
            using (UdpClient c = new UdpClient())
            {
                byte[] b = Encoding.UTF8.GetBytes(t.msg);
                c.Send(b, b.Length, t.ip, UDPReceiver.PhonePort);
            }
            Debug.Log($"Send STOP: {t.msg} ¡æ {t.ip}:{UDPReceiver.PhonePort}");
        }
    }

    List<(string ip, string msg)> ResolveTargets(string handKey, bool vibrate)
    {
        var list = new List<(string ip, string msg)>();

        if (!string.IsNullOrEmpty(handKey))
        {
            string ip = UDPReceiver.PhoneRegistry.Get(handKey);
            if (!string.IsNullOrEmpty(ip))
            {
                string suffix = handKey.EndsWith("A") ? "A" : "B";
                list.Add((ip, vibrate ? $"VIBRATE:{suffix}" : $"STOP:{suffix}"));
                return list;
            }
        }

        foreach (var ip in UDPReceiver.PhoneRegistry.AllIPs())
            list.Add((ip, vibrate ? "VIBRATE" : "STOP"));

        return list;
    }

    void LateUpdate()
    {
        Transform latestHand = GetLatestHand();
        if (latestHand == null) return;
        if (!handInitialRotations.ContainsKey(latestHand)) return;

        Quaternion handInitRot = handInitialRotations[latestHand];
        Quaternion objInitRot = objectInitialRotations[latestHand];
        Vector3 offset = handOffsets[latestHand];

        Quaternion delta = latestHand.rotation * Quaternion.Inverse(handInitRot);
        transform.rotation = delta * objInitRot;

        Vector3 targetPosition = latestHand.TransformPoint(offset);
        float minY = 0.01f;
        if (targetPosition.y < minY) targetPosition.y = minY;

        transform.position = targetPosition;
    }

    string DetectHandKey(Transform t)
    {
        var ctrl = t.GetComponentInParent<HandAnimationController>();
        if (ctrl != null && !string.IsNullOrEmpty(ctrl.handKey))
            return ctrl.handKey.ToUpper();

        return null;
    }
}

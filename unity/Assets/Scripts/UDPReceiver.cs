using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using UnityEngine;
using System.Collections.Concurrent;
using System.Collections.Generic;

public class UDPReceiver : MonoBehaviour
{
    private UdpClient udpClient;
    private Camera mainCamera;

    private bool useYAxisA = true, useYAxisB = true;
    private bool skipFirstStartA = false, skipFirstStartB = false;
    private string lastAxisA = "Y", lastAxisB = "Y";

    public static string LastPhoneIP { get; private set; }
    public static int PhonePort => 7777;

    public HandAnimationController handA;
    public HandAnimationController handB;

    private Ray lastRayA = new Ray(Vector3.zero, Vector3.zero);
    private Ray lastRayB = new Ray(Vector3.zero, Vector3.zero);
    private bool isRayActiveA = false;
    private bool isRayActiveB = false;

    private float lastRotationDeltaA = 0f;
    private float lastRotationDeltaB = 0f;

    private DragDropable grabbedObjectA = null;
    private DragDropable grabbedObjectB = null;

    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    void Start()
    {
        mainCamera = Camera.main;
        var ep = new IPEndPoint(IPAddress.Any, PhonePort);
        udpClient = new UdpClient(ep);
        udpClient.BeginReceive(new AsyncCallback(ReceiveData), null);
        Debug.Log("UDP Receiver started on port 7777");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            SendTestUDP();
        }

        while (mainThreadActions.TryDequeue(out var action))
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }
    }

    public static class PhoneRegistry
    {
        private static readonly Dictionary<string, string> phoneIPByHand = new();

        public static void Set(string hand, string ip)
        {
            hand = hand?.Trim().ToUpper();
            if (hand == "HAND_A" || hand == "HAND_B")
                phoneIPByHand[hand] = ip;
        }

        public static string Get(string hand)
        {
            hand = hand?.Trim().ToUpper();
            return phoneIPByHand.TryGetValue(hand, out var ip) ? ip : null;
        }

        public static IEnumerable<string> AllIPs() => phoneIPByHand.Values.Distinct();
    }

    void SendTestUDP()
    {
        using var sender = new UdpClient();
        string msg = "HAND_A:MOVE:300,300,50,0,1,1080,1920";
        byte[] data = Encoding.UTF8.GetBytes(msg);
        sender.Send(data, data.Length, "127.0.0.1", PhonePort);
    }

    void FixedUpdate()
    {
        UpdateHand(handA, ref grabbedObjectA, isRayActiveA, lastRayA, lastRotationDeltaA, useYAxisA);
        UpdateHand(handB, ref grabbedObjectB, isRayActiveB, lastRayB, lastRotationDeltaB, useYAxisB);
    }

    void UpdateHand(
        HandAnimationController hand,
        ref DragDropable grabbed,
        bool isActive,
        Ray ray,
        float rotationDelta,
        bool useY)
    {
        if (!isActive || hand == null) return;

        hand.FollowRay(ray, useY);

        Debug.DrawRay(ray.origin, ray.direction * 9.5f, hand == handA ? Color.red : Color.blue);

        if (Mathf.Abs(rotationDelta) > 0.01f)
            hand.RotateHand(rotationDelta);

        if (grabbed == null && hand.grabAnchor != null)
        {
            const float grabDistanceThreshold = 0.3f;
            Collider[] nearbyObjects = Physics.OverlapSphere(hand.grabAnchor.position, grabDistanceThreshold);

            foreach (var col in nearbyObjects)
            {
                DragDropable target = col.GetComponent<DragDropable>();
                if (target != null)
                {
                    Quaternion objectRotationBefore = target.transform.rotation;
                    grabbed = target;
                    hand.OnStartGrabbing(grabbed.transform);

                    Vector3 offsetToUse = grabbed.transform.position - hand.grabAnchor.position;
                    target.StartDrag(hand.grabAnchor, offsetToUse, objectRotationBefore);
                    break;
                }
            }
        }

        if (grabbed == null && Mathf.Abs(rotationDelta) < 0.01f)
        {
            hand.PlayIdle();
            hand.UpdateColliders();
        }
    }

    void ReceiveData(IAsyncResult result)
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, PhonePort);
        byte[] data = udpClient.EndReceive(result, ref remoteEP);

        string sourceIP = remoteEP.Address.ToString();
        LastPhoneIP = sourceIP;

        string message = Encoding.UTF8.GetString(data);

        mainThreadActions.Enqueue(() =>
        {
            Debug.Log($"[UDP Message] from {sourceIP}: {message}");
            TryRegisterPhoneForHand(message, sourceIP);
            ProcessReceivedData(message);
        });

        udpClient.BeginReceive(new AsyncCallback(ReceiveData), null);
    }

    void TryRegisterPhoneForHand(string msg, string ip)
    {
        int c = msg.IndexOf(':');
        if (c <= 0) return;

        string handToken = msg.Substring(0, c).Trim().ToUpper();
        PhoneRegistry.Set(handToken, ip);
    }

    void ProcessReceivedData(string data)
    {
        string[] parts = data.Split(':');
        if (parts.Length < 3) return;

        string objectType = parts[0];
        string cleanedObjectType = objectType.Trim().ToUpper();
        string command = parts[1];

        Debug.Log($"[UDP Message] cleanedObjectType: {cleanedObjectType}, command: {command}");

        string[] coords = parts[2].Split(',');
        if (!float.TryParse(coords[0], out float normX) || !float.TryParse(coords[1], out float normY)) return;

        float pinchDistance = (coords.Length >= 3 && float.TryParse(coords[2], out float pinch)) ? pinch : 0f;
        float rotationDelta = (coords.Length >= 4 && float.TryParse(coords[3], out float rot)) ? rot : 0f;

        string axisToken = (coords.Length >= 8) ? coords[7].Trim().ToUpper() : "Y";

        bool isA = cleanedObjectType == "HAND_A";
        bool axisChanged = false;

        if (isA)
        {
            useYAxisA = (axisToken == "Y");
            axisChanged = (axisToken != lastAxisA);
            if (axisChanged)
            {
                skipFirstStartA = true;
                lastAxisA = axisToken;
            }
        }
        else
        {
            useYAxisB = (axisToken == "Y");
            axisChanged = (axisToken != lastAxisB);
            if (axisChanged)
            {
                skipFirstStartB = true;
                lastAxisB = axisToken;
            }
        }

        float phoneW = (coords.Length >= 7 && float.TryParse(coords[5], out float w)) ? w : Screen.width;
        float phoneH = (coords.Length >= 7 && float.TryParse(coords[6], out float h)) ? h : Screen.height;

        float pxX = (phoneW > 0f) ? (normX / phoneW) * Screen.width : normX;
        float pxY = (phoneH > 0f) ? (1f - (normY / phoneH)) * Screen.height : normY;

        bool useY = isA ? useYAxisA : useYAxisB;
        Ray ray;

        if (useY)
        {
            ray = mainCamera.ScreenPointToRay(new Vector3(pxX, pxY, 0));
        }
        else
        {
            float xr = (phoneW > 0f) ? normX / phoneW : 0.5f;
            float yr = (phoneH > 0f) ? 1f - (normY / phoneH) : 0.5f;
            float xWorld = Mathf.Lerp(-5.25f, 5.05f, xr);
            float zWorld = Mathf.Lerp(-4.8f, 5.08f, yr);
            Vector3 origin = new Vector3(xWorld, mainCamera.transform.position.y + 5f, zWorld);
            ray = new Ray(origin, Vector3.down);
        }

        const float pinchThreshold = 200f;

        if (command == "START")
        {
            if (isA && skipFirstStartA)
            {
                lastRayA = ray;
                skipFirstStartA = false;
                return;
            }
            if (!isA && skipFirstStartB)
            {
                lastRayB = ray;
                skipFirstStartB = false;
                return;
            }
        }

        if (command == "START" || command == "MOVE")
        {
            if (pinchDistance < pinchThreshold)
            {
                if (isA)
                {
                    lastRayA = ray;
                    isRayActiveA = true;
                    lastRotationDeltaA = rotationDelta;
                }
                else
                {
                    lastRayB = ray;
                    isRayActiveB = true;
                    lastRotationDeltaB = rotationDelta;
                }
            }
            else
            {
                if (isA)
                    Release(handA, ref grabbedObjectA, ref isRayActiveA, ref lastRayA);
                else
                    Release(handB, ref grabbedObjectB, ref isRayActiveB, ref lastRayB);
            }
        }
        else if (command == "STOP")
        {
            if (isA)
                Release(handA, ref grabbedObjectA, ref isRayActiveA, ref lastRayA);
            else
                Release(handB, ref grabbedObjectB, ref isRayActiveB, ref lastRayB);
        }
    }

    void Release(
        HandAnimationController hand,
        ref DragDropable grabbed,
        ref bool isActive,
        ref Ray ray)
    {
        if (grabbed != null)
        {
            grabbed.EndDrag(hand.grabAnchor);
            hand.OnRelease(grabbed.transform);
            grabbed = null;
        }

        isActive = false;
        ray = new Ray(Vector3.zero, Vector3.zero);
    }
}

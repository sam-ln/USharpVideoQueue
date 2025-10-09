using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.UdonNetworkCalling; // for SendCustomNetworkEvent with params
using VRC.Udon.Common.Interfaces; // NetworkEventTarget

/// <summary>
/// RPCTimer — schedule one-shot timers that call a [NetworkCallable] method
/// on a target UdonSharpBehaviour via SendCustomNetworkEvent(..., params object[] args).
///
/// - Returns an int timerId that you can Cancel(...)
/// - Uses Networking.GetServerTimeInSeconds() for stable deadlines
/// - Capacity is fixed-size; linear scan is fine for small counts
/// - Arguments must be types supported by your param-enabled SendCustomNetworkEvent
///   (e.g., int, float, bool, string, VRCUrl, Vector3, etc.; max ~8 params)
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class RPCTimer : UdonSharpBehaviour
{
    [Header("Capacity")] [Tooltip("Maximum number of simultaneous timers.")]
    public int capacity = 32;

    [Header("Timing")] [Tooltip("If true, deadlines use server time; otherwise local Time.time.")]
    public bool useServerTime = true;

    // Storage
    private bool[] _active;
    private int[] _ids;
    private UdonSharpBehaviour[] _targets;
    private string[] _eventNames;
    private float[] _dueAt;
    private object[][] _args;

    private int _nextId = 1;
    private int _activeCount = 0;
    private bool _initialized = false;

    // -------------------- Lifecycle --------------------

    private void Start()
    {
        if (_initialized) return;
        if (capacity < 1) capacity = 1;

        _active = new bool[capacity];
        _ids = new int[capacity];
        _targets = new UdonSharpBehaviour[capacity];
        _eventNames = new string[capacity];
        _dueAt = new float[capacity];
        _args = new object[capacity][];

        _initialized = true;
    }

    public void Update()
    {
        if (_activeCount == 0) return;

        float now = useServerTime
            ? (float)Networking.GetServerTimeInSeconds()
            : Time.time;

        for (int i = 0; i < capacity; i++)
        {
            if (!_active[i]) continue;
            if (now < _dueAt[i]) continue;

            // Fire & clear
            var target = _targets[i];
            var eventName = _eventNames[i];
            var args = _args[i];

            // Clear first to avoid double-fire if the callback re-schedules
            _ClearSlot(i);

            if (target != null && !string.IsNullOrEmpty(eventName))
            {
                // IMPORTANT: Your method must be [NetworkCallable] and accept the same params
                // as provided here (types supported by your network-calling implementation).
                _SendWithArgs(target, NetworkEventTarget.Self, eventName, args);
            }
        }
    }

    // -------------------- API --------------------

    
    /// <summary>
    /// Cancels the running timer and reschedules a one-shot networked call after delaySeconds.
    /// The target method MUST be [NetworkCallable] and accept the provided args.
    /// Returns a positive timerId, or -1 if capacity is full / bad input.
    /// </summary>
    public int CancelRunningAndSchedule(
        UdonSharpBehaviour target,
        int existingTimerId,
        string networkMethodName,
        float delaySeconds,
        params object[] args)
    {
        if (existingTimerId != -1) Cancel(existingTimerId);
        return Schedule(target, networkMethodName, delaySeconds, args);
    }

    /// <summary>
    /// Schedule a one-shot networked call after delaySeconds.
    /// The target method MUST be [NetworkCallable] and accept the provided args.
    /// Returns a positive timerId, or -1 if capacity is full / bad input.
    /// </summary>
    public int Schedule(
        UdonSharpBehaviour target,
        string networkMethodName,
        float delaySeconds,
        params object[] args)
    {
        if (!_initialized) Start();
        if (target == null || string.IsNullOrEmpty(networkMethodName)) return -1;

        // Immediate path
        if (delaySeconds <= 0f)
        {
            _SendWithArgs(target, NetworkEventTarget.Self, networkMethodName, args);
            return _ConsumeId();
        }

        int slot = _FindFreeSlot();
        if (slot == -1) return -1;

        int id = _ConsumeId();

        _active[slot] = true;
        _ids[slot] = id;
        _targets[slot] = target;
        _eventNames[slot] = networkMethodName;
        _args[slot] = args; // NOTE: args array is referenced; don't mutate after scheduling.

        float now = useServerTime
            ? (float)Networking.GetServerTimeInSeconds()
            : Time.time;

        _dueAt[slot] = now + delaySeconds;

        _activeCount++;
        return id;
    }

    /// <summary>
    /// Cancel a pending timer by id. Returns true if it was pending and got canceled.
    /// </summary>
    public bool Cancel(int timerId)
    {
        if (!_initialized || timerId <= 0) return false;

        for (int i = 0; i < capacity; i++)
        {
            if (!_active[i]) continue;
            if (_ids[i] != timerId) continue;

            _ClearSlot(i);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Cancel all timers targeting the given behaviour.
    /// </summary>
    public void CancelAllFor(UdonSharpBehaviour target)
    {
        if (!_initialized || target == null) return;

        for (int i = 0; i < capacity; i++)
        {
            if (!_active[i]) continue;
            if (_targets[i] != target) continue;
            _ClearSlot(i);
        }
    }

    /// <summary>
    /// Cancel all timers.
    /// </summary>
    public void CancelAll()
    {
        if (!_initialized) return;

        for (int i = 0; i < capacity; i++)
        {
            if (_active[i]) _ClearSlot(i);
        }
    }

    // -------------------- Internals --------------------


    private void _SendWithArgs(UdonSharpBehaviour target, NetworkEventTarget netTarget, string eventName, object[] args)
    {
        var recv = (IUdonEventReceiver)target;
        int n = (args == null) ? 0 : args.Length;

        switch (n)
        {
            case 0:
                target.SendCustomNetworkEvent(netTarget, eventName);
                break;
            case 1:
                NetworkCalling.SendCustomNetworkEvent(recv, netTarget, eventName, args[0]);
                break;
            case 2:
                NetworkCalling.SendCustomNetworkEvent(recv, netTarget, eventName, args[0], args[1]);
                break;
            case 3:
                NetworkCalling.SendCustomNetworkEvent(recv, netTarget, eventName, args[0], args[1], args[2]);
                break;
            case 4:
                NetworkCalling.SendCustomNetworkEvent(recv, netTarget, eventName, args[0], args[1], args[2], args[3]);
                break;
            case 5:
                NetworkCalling.SendCustomNetworkEvent(recv, netTarget, eventName, args[0], args[1], args[2], args[3],
                    args[4]);
                break;
            case 6:
                NetworkCalling.SendCustomNetworkEvent(recv, netTarget, eventName, args[0], args[1], args[2], args[3],
                    args[4], args[5]);
                break;
            case 7:
                NetworkCalling.SendCustomNetworkEvent(recv, netTarget, eventName, args[0], args[1], args[2], args[3],
                    args[4], args[5], args[6]);
                break;
            case 8:
                NetworkCalling.SendCustomNetworkEvent(recv, netTarget, eventName, args[0], args[1], args[2], args[3],
                    args[4], args[5], args[6], args[7]);
                break;
            default:
                Debug.LogWarning("[RPCTimer] Too many parameters (max 8).");
                break;
        }
    }

    private int _ConsumeId()
    {
        int id = _nextId++;
        if (_nextId == int.MaxValue) _nextId = 1;
        return id;
    }

    private int _FindFreeSlot()
    {
        for (int i = 0; i < capacity; i++)
            if (!_active[i])
                return i;
        return -1;
    }

    private void _ClearSlot(int i)
    {
        _active[i] = false;
        _targets[i] = null;
        _eventNames[i] = null;
        _args[i] = null;
        _ids[i] = 0;
        _dueAt[i] = 0f;
        _activeCount--;
        if (_activeCount < 0) _activeCount = 0;
    }
}
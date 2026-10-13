using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Theo dõi marker bằng ARTrackedImageManager (AR Foundation 6, sự kiện trackablesChanged).
// Đặt HeartAnchor trùng với ảnh marker. Coi cả Tracking lẫn Limited là đang thấy ảnh
// (marker nằm yên trên bàn, máy yếu như Vsmart Joy hay rơi về Limited);
// chỉ tắt HeartAnchor khi ảnh về None hoặc bị xóa khỏi danh sách (args.removed).
// Gắn lên XR Origin (cùng chỗ với ARTrackedImageManager).
public class HeartTracker : MonoBehaviour
{
    [SerializeField] ARTrackedImageManager imageManager;

    [Tooltip("HeartAnchor: được đặt theo marker và bật/tắt khi thấy/mất marker")]
    [SerializeField] Transform heartAnchor;

    [Tooltip("Script trồi lên trên HeartAnchor")]
    [SerializeField] HeartEmerge emerge;

    [Tooltip("Tên ảnh trong Reference Image Library; để trống thì nhận mọi ảnh")]
    [SerializeField] string imageName = "";

    [Tooltip("In log [DBG]; tắt khi build bản chính để đỡ tốn")]
    [SerializeField] bool debugLogs = true;

    float lastSeen = float.NegativeInfinity;
    bool visible;

    // Ảnh đang điều khiển HeartAnchor; ảnh khác bị bỏ qua cho tới khi ảnh này mất
    TrackableId currentId = TrackableId.invalidId;

    // Trạng thái theo dõi trước đó của từng ảnh, để chỉ log khi có thay đổi
    readonly Dictionary<TrackableId, TrackingState> lastStates = new Dictionary<TrackableId, TrackingState>();

    void Awake()
    {
        if (imageManager == null) imageManager = GetComponent<ARTrackedImageManager>();
        heartAnchor.gameObject.SetActive(false);
    }

    void OnEnable() => imageManager.trackablesChanged.AddListener(OnTrackablesChanged);
    void OnDisable() => imageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);

    void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (var image in args.added)
        {
            if (debugLogs) Debug.Log($"[DBG] HeartTracker: nhận ảnh mới '{image.referenceImage.name}' ({image.trackingState})", this);
            Handle(image);
        }
        foreach (var image in args.updated) Handle(image);
        foreach (var pair in args.removed)
        {
            lastStates.Remove(pair.Key);
            // Giá trị ARTrackedImage có thể đã bị hủy, chỉ dùng TrackableId
            if (pair.Key == currentId) Hide("ảnh bị xóa khỏi danh sách");
        }
    }

    void Handle(ARTrackedImage image)
    {
        LogStateChange(image);
        if (!string.IsNullOrEmpty(imageName) && image.referenceImage.name != imageName) return;
        // Đang bám theo một ảnh khác thì bỏ qua ảnh này
        if (visible && image.trackableId != currentId) return;

        if (image.trackingState == TrackingState.None)
        {
            if (visible) Hide("trackingState = None");
            return;
        }

        // Tracking hoặc Limited: vẫn cập nhật vị trí và giữ HeartAnchor bật
        heartAnchor.SetPositionAndRotation(image.transform.position, image.transform.rotation);

        if (!visible)
        {
            float lostSeconds = Time.time - lastSeen;
            visible = true;
            currentId = image.trackableId;
            heartAnchor.gameObject.SetActive(true);
            if (debugLogs) Debug.Log($"[DBG] HeartTracker: BẬT HeartAnchor tại {heartAnchor.position.ToString("F3")} (mất dấu {lostSeconds:F2}s)", this);
            if (emerge != null) emerge.NotifyFound(lostSeconds);
        }
        lastSeen = Time.time;
    }

    void Hide(string reason)
    {
        if (!visible) return;
        visible = false;
        currentId = TrackableId.invalidId;
        heartAnchor.gameObject.SetActive(false);
        if (debugLogs) Debug.Log($"[DBG] HeartTracker: TẮT HeartAnchor tại {heartAnchor.position.ToString("F3")} ({reason})", this);
    }

    void LogStateChange(ARTrackedImage image)
    {
        if (!debugLogs) return;
        var state = image.trackingState;
        bool known = lastStates.TryGetValue(image.trackableId, out var old);
        if (known && old == state) return;
        lastStates[image.trackableId] = state;
        string from = known ? old.ToString() : "(mới)";
        Debug.Log($"[DBG] HeartTracker: '{image.referenceImage.name}' trackingState {from} -> {state}", this);
    }
}

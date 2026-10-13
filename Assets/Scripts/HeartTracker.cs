using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Theo dõi marker bằng ARTrackedImageManager (AR Foundation 6, sự kiện trackablesChanged).
// Đặt HeartAnchor trùng với ảnh marker, làm mượt để tim không rung.
// Chỉ bật HeartAnchor và cập nhật vị trí khi ảnh ở trạng thái Tracking.
// ARCore gần như không trả về None khi marker ra khỏi khung hình mà giữ Limited mãi,
// nên Limited chỉ được "nới" một chút: giữ tim đứng yên ở vị trí cũ, nếu Limited kéo dài
// quá limitedTimeout giây thì coi như mất dấu và tắt HeartAnchor.
// Cũng tắt khi ảnh về None hoặc bị xóa khỏi danh sách (args.removed).
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

    [Tooltip("Ảnh ở Limited liên tục quá số giây này thì coi là mất dấu (máy yếu hay chớp Limited ngắn nên đừng đặt quá nhỏ)")]
    [SerializeField] float limitedTimeout = 1f;

    [Tooltip("Độ nhanh bám theo marker (1/giây). Lớn: bám sát nhưng rung; nhỏ: mượt nhưng trễ")]
    [SerializeField] float smoothing = 12f;

    [Tooltip("In log [DBG]; tắt khi build bản chính để đỡ tốn")]
    [SerializeField] bool debugLogs = true;

    // Lần cuối ảnh ở trạng thái Tracking (Limited không tính)
    float lastSeen = float.NegativeInfinity;
    // Thời điểm ảnh đang bám bắt đầu rơi vào Limited; âm = đang Tracking
    float limitedSince = -1f;
    bool visible;

    // Tư thế marker mới nhất; HeartAnchor tiến dần tới đây trong Update
    Vector3 targetPos;
    Quaternion targetRot = Quaternion.identity;

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

        switch (image.trackingState)
        {
            case TrackingState.Tracking:
                targetPos = image.transform.position;
                targetRot = image.transform.rotation;
                limitedSince = -1f;
                if (!visible) Show(image);
                lastSeen = Time.time;
                break;

            case TrackingState.Limited:
                // Tư thế lúc Limited thường sai/đứng im: không cập nhật, chỉ bắt đầu đếm giờ.
                // Ảnh chưa hiện thì không bật chỉ vì Limited.
                if (visible && limitedSince < 0f) limitedSince = Time.time;
                break;

            default: // None
                if (visible) Hide("trackingState = None");
                break;
        }
    }

    void Update()
    {
        if (!visible) return;

        if (limitedSince >= 0f && Time.time - limitedSince > limitedTimeout)
        {
            Hide($"Limited quá {limitedTimeout:F1}s");
            return;
        }

        // Làm mượt không phụ thuộc fps: hệ số 1 - e^(-k·dt)
        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        heartAnchor.SetPositionAndRotation(
            Vector3.Lerp(heartAnchor.position, targetPos, t),
            Quaternion.Slerp(heartAnchor.rotation, targetRot, t));
    }

    void Show(ARTrackedImage image)
    {
        float lostSeconds = Time.time - lastSeen;
        visible = true;
        currentId = image.trackableId;
        // Mới thấy lại thì đặt thẳng vào chỗ, không trượt từ vị trí cũ tới
        heartAnchor.SetPositionAndRotation(targetPos, targetRot);
        heartAnchor.gameObject.SetActive(true);
        if (debugLogs) Debug.Log($"[DBG] HeartTracker: BẬT HeartAnchor tại {heartAnchor.position.ToString("F3")} (mất dấu {lostSeconds:F2}s)", this);
        if (emerge != null) emerge.NotifyFound(lostSeconds);
    }

    void Hide(string reason)
    {
        if (!visible) return;
        visible = false;
        limitedSince = -1f;
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

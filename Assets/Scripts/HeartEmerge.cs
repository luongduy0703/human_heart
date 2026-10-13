using UnityEngine;

// Cho trái tim trồi lên từ dưới mặt giấy và lớn dần.
// Gắn lên HeartAnchor. HeartModel phải là con trực tiếp của HeartAnchor,
// và vị trí/kích thước đặt sẵn trong Editor chính là trạng thái "đã ra hết".
// Phần tim còn nằm dưới mặt giấy bị Occluder (DepthMask) che đi.
public class HeartEmerge : MonoBehaviour
{
    [Tooltip("Mô hình tim sẽ được di chuyển và phóng to")]
    [SerializeField] Transform heartModel;

    [Tooltip("Nhóm các điểm HeartPart, chỉ bật khi tim đã ra hết")]
    [SerializeField] GameObject hotspots;

    [Tooltip("Thời gian trồi lên (giây)")]
    [SerializeField] float duration = 4f;

    [Tooltip("Độ sâu ban đầu dưới mặt giấy (mét), nên lớn hơn chiều cao tim để lúc đầu bị che hết")]
    [SerializeField] float sinkDepth = 0.12f;

    [Tooltip("Tỉ lệ kích thước lúc bắt đầu so với kích thước cuối")]
    [SerializeField, Range(0.05f, 1f)] float startScale = 0.3f;

    [Tooltip("Mất dấu marker lâu hơn số giây này thì phát lại từ đầu")]
    [SerializeField] float replayAfterLost = 2f;

    [Tooltip("In log [DBG] tiến độ mỗi 0.5 giây; tắt khi build bản chính để đỡ tốn")]
    [SerializeField] bool debugLogs = true;

    const float LogInterval = 0.5f;

    public Transform HeartModel => heartModel;
    public GameObject Hotspots => hotspots;

    // Báo khi hoạt ảnh trồi lên bắt đầu lại từ đầu (HeartInteraction dùng để trả tim về góc nhìn gốc)
    public event System.Action Replayed;

    Vector3 finalPos, finalScale, startPos;
    float elapsed;
    float nextLog;
    bool playing;
    bool initialized;

    void Awake() => Init();

    void Init()
    {
        if (initialized) return;
        initialized = true;
        // Lưu trạng thái cuối (đặt trong Editor), điểm bắt đầu nằm dưới mặt giấy
        // theo trục Y của HeartAnchor (trục Y của ảnh marker là pháp tuyến tờ giấy).
        finalPos = heartModel.localPosition;
        finalScale = heartModel.localScale;
        startPos = finalPos - Vector3.up * sinkDepth;
        if (hotspots != null) hotspots.SetActive(false);
    }

    // HeartTracker gọi khi tìm thấy lại marker sau một lúc bị ẩn
    public void NotifyFound(float lostSeconds)
    {
        if (lostSeconds > replayAfterLost) Replay();
    }

    public void Replay()
    {
        Init();
        elapsed = 0f;
        nextLog = 0f;
        playing = true;
        Apply(0f);

        if (hotspots != null)
        {
            // Tắt hết nhãn để lần sau hiện lại từ đầu (chỉ chạy khi phát lại nên không tốn)
            foreach (var part in hotspots.GetComponentsInChildren<HeartPart>(true))
                part.Show(false);
            hotspots.SetActive(false);
        }
        Replayed?.Invoke();
    }

    void Update()
    {
        if (!playing) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        Apply(Mathf.SmoothStep(0f, 1f, t));

        if (debugLogs && elapsed >= nextLog)
        {
            nextLog = elapsed + LogInterval; // khung hình bị giật cũng không in dồn nhiều dòng
            LogProgress(t);
        }

        if (t >= 1f)
        {
            playing = false;
            if (hotspots != null) hotspots.SetActive(true);
            if (debugLogs) LogProgress(t); // luôn in dòng cuối khi đã ra hết
        }
    }

    void LogProgress(float t)
    {
        Debug.Log($"[DBG] HeartEmerge: progress {t:P0} ({elapsed:F2}/{duration}s), " +
                  $"HeartModel localPosition {heartModel.localPosition.ToString("F3")}", this);
    }

    void Apply(float s)
    {
        heartModel.localPosition = Vector3.LerpUnclamped(startPos, finalPos, s);
        heartModel.localScale = finalScale * Mathf.LerpUnclamped(startScale, 1f, s);
    }
}

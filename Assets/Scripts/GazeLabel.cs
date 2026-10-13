using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Chọn bằng cách nhìn: bắn tia từ giữa camera, nhìn vào một HeartPart đủ dwellTime giây
// thì chọn bộ phận đó một lần. Phải nhìn ra chỗ khác rồi nhìn lại mới bật/tắt tiếp.
// Chọn bằng chạm: cầm tay (không đeo kính) thì chạm vào bộ phận nào bật/tắt bộ phận đó;
// đeo kính (HeadsetView đang bật) thì cú chạm là nút bấm của kính, chọn thứ đang nhìn.
// "Chạm" = nhấn rồi thả nhanh, gần như không di chuyển, chỉ một ngón; nhờ vậy kéo xoay
// hoặc chụm zoom (HeartInteraction) không vô tình bật/tắt nhãn.
// Gắn lên camera AR (Main Camera trong XR Origin).
public class GazeLabel : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] float maxDistance = 3f;
    [SerializeField] LayerMask layers = ~0;
    [SerializeField] float dwellTime = 1.5f;

    [Tooltip("Cầm tay (không đeo kính) có dùng nhìn chờ không; tắt để khỏi bật nhãn ngoài ý muốn khi đang xoay tim")]
    [SerializeField] bool dwellInHandheld = false;

    [Tooltip("Bật: chọn một bộ phận thì chỉ nhãn của nó hiện, nhãn khác tự ẩn. Tắt: mỗi bộ phận bật/tắt riêng")]
    [SerializeField] bool onlyOneLabel = true;

    [Tooltip("Cho phép chạm màn hình (hoặc click chuột trong Editor) để bật/tắt nhãn")]
    [SerializeField] bool tapToSelect = true;

    [Tooltip("Nhấn lâu hơn số giây này thì không tính là chạm")]
    [SerializeField] float tapMaxSeconds = 0.35f;

    [Tooltip("Ngón tay đi xa hơn số pixel này thì là kéo, không phải chạm")]
    [SerializeField] float tapMaxMovePixels = 20f;

    [Tooltip("Tùy chọn: Image kiểu Filled ở giữa mỗi mắt để hiện tiến độ nhìn")]
    [SerializeField] Image[] reticleFills;

    static readonly Vector3 Center = new Vector3(0.5f, 0.5f, 0f);

    HeartPart current;
    float timer;
    bool fired;

    // Lưu lại collider gần nhất để không phải GetComponent mỗi khung hình
    Collider lastCollider;
    HeartPart lastPart;

    // Chỉ cảnh báo một lần để không làm đầy Console mỗi khung hình
    bool warnedNoCamera;

    // Theo dõi cú chạm đang diễn ra
    bool tapTracking;
    Vector2 tapStart;
    float tapStartTime;

    void Awake()
    {
        ResolveCamera();
        WarnNullReticles();
        SetReticle(0f);
    }

    // Thứ tự tìm camera: ô cam trong Inspector → Camera trên cùng object → Camera.main
    bool ResolveCamera()
    {
        if (cam != null) return true;
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam != null) return true;

        if (!warnedNoCamera)
        {
            warnedNoCamera = true;
            Debug.LogWarning($"{nameof(GazeLabel)} trên '{name}': không tìm thấy camera " +
                             "(chưa gán ô cam và không có camera nào tag MainCamera). Tạm dừng gaze.", this);
        }
        return false;
    }

    void WarnNullReticles()
    {
        if (reticleFills == null) return;
        foreach (var img in reticleFills)
        {
            if (img == null)
            {
                Debug.LogWarning($"{nameof(GazeLabel)} trên '{name}': reticleFills có phần tử trống, sẽ bỏ qua.", this);
                return;
            }
        }
    }

    void Update()
    {
        // Camera có thể chưa sẵn sàng lúc Awake (XR Origin sinh sau) hoặc bị xóa giữa chừng
        if (!ResolveCamera()) return;

        HeartPart target = null;
        Ray ray = cam.ViewportPointToRay(Center);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layers, QueryTriggerInteraction.Collide))
        {
            if (hit.collider != lastCollider)
            {
                lastCollider = hit.collider;
                lastPart = lastCollider.GetComponentInParent<HeartPart>();
            }
            target = lastPart;
        }
        else
        {
            lastCollider = null;
            lastPart = null;
        }

        if (target != current)
        {
            current = target;
            timer = 0f;
            fired = false;
        }

        if (tapToSelect && TryGetTap(out Vector2 screenPos))
        {
            HeartPart tapped = HeadsetView.Active ? current : PartAt(cam.ScreenPointToRay(screenPos));
            if (tapped != null)
            {
                Choose(tapped);
                // Đã bật/tắt bằng chạm thì đừng để dwell bật/tắt lại ngay sau đó
                if (tapped == current) fired = true;
            }
        }

        bool dwell = HeadsetView.Active || dwellInHandheld;
        if (dwell && current != null && !fired)
        {
            timer += Time.deltaTime;
            if (timer >= dwellTime)
            {
                Choose(current);
                fired = true;
            }
        }

        SetReticle(dwell && current != null && !fired ? timer / dwellTime : 0f);
    }

    void Choose(HeartPart part)
    {
        if (onlyOneLabel) part.Select();
        else part.Toggle();
    }

    // Chỉ raycast khi có cú chạm nên không cần cache như tia nhìn
    HeartPart PartAt(Ray ray)
    {
        return Physics.Raycast(ray, out RaycastHit hit, maxDistance, layers, QueryTriggerInteraction.Collide)
            ? hit.collider.GetComponentInParent<HeartPart>()
            : null;
    }

    // true vào khung hình ngón tay (hoặc chuột) được thả ra, nếu đó là một cú chạm
    bool TryGetTap(out Vector2 pos)
    {
        var ts = Touchscreen.current;
        if (ts != null)
        {
            var touch = ts.primaryTouch;
            if (touch.press.wasPressedThisFrame) BeginTap(touch.position.ReadValue());
            if (tapTracking && TouchCount(ts) > 1) tapTracking = false; // hai ngón = chụm zoom
            if (touch.press.wasReleasedThisFrame) return EndTap(touch.position.ReadValue(), out pos);
        }

        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) BeginTap(mouse.position.ReadValue());
            if (mouse.leftButton.wasReleasedThisFrame) return EndTap(mouse.position.ReadValue(), out pos);
        }

        pos = default;
        return false;
    }

    void BeginTap(Vector2 pos)
    {
        tapTracking = true;
        tapStart = pos;
        tapStartTime = Time.unscaledTime;
    }

    bool EndTap(Vector2 releasePos, out Vector2 pos)
    {
        pos = releasePos;
        bool ok = tapTracking
                  && Time.unscaledTime - tapStartTime <= tapMaxSeconds
                  && (releasePos - tapStart).sqrMagnitude <= tapMaxMovePixels * tapMaxMovePixels;
        tapTracking = false;
        return ok;
    }

    static int TouchCount(Touchscreen ts)
    {
        int n = 0;
        var touches = ts.touches;
        for (int i = 0; i < touches.Count; i++)
            if (touches[i].isInProgress) n++;
        return n;
    }

    void SetReticle(float amount)
    {
        if (reticleFills == null) return;
        foreach (var img in reticleFills)
            if (img != null) img.fillAmount = amount;
    }
}

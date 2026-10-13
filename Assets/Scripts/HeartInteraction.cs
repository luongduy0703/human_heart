using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Tương tác với tim khi cầm máy trên tay (không đeo kính):
// - Kéo một ngón: xoay tim. Kéo ngang xoay quanh pháp tuyến tờ giấy, kéo dọc lật tới/lui.
// - Chụm/mở hai ngón: thu nhỏ/phóng to. Đáy tim luôn giữ trên mặt giấy.
// - Chạm nhanh (không kéo) do GazeLabel xử lý: bật/tắt nhãn của Hotspot.
// - Trong Editor: kéo chuột trái để xoay, cuộn chuột để zoom.
// Lúc chạy, script tạo object "InteractPivot" ở tâm tim, làm con của HeartModel, và chuyển
// Visual + Hotspots vào đó. HeartEmerge vẫn điều khiển HeartModel (trồi lên), còn script này
// chỉ xoay/thu phóng InteractPivot nên hai việc không giẫm lên nhau.
// Tắt khi đang ở chế độ kính (HeadsetView.Active). Gắn lên HeartAnchor, cạnh HeartEmerge.
[RequireComponent(typeof(HeartEmerge))]
public class HeartInteraction : MonoBehaviour
{
    [Tooltip("Số độ xoay khi kéo hết một chiều rộng màn hình")]
    [SerializeField] float degreesPerScreen = 270f;

    [Tooltip("Cho phép kéo dọc để lật tim tới/lui; tắt thì chỉ xoay quanh pháp tuyến tờ giấy")]
    [SerializeField] bool allowTilt = true;

    [SerializeField] float minScale = 0.5f;
    [SerializeField] float maxScale = 3f;

    [Tooltip("Ngón tay phải đi quá số pixel này mới tính là kéo (dưới mức này là chạm)")]
    [SerializeField] float dragThresholdPixels = 20f;

    [Tooltip("Mỗi nấc cuộn chuột phóng to/thu nhỏ bao nhiêu (chỉ dùng trong Editor/PC)")]
    [SerializeField] float scrollZoomStep = 0.1f;

    enum Mode { None, Pending, Rotate, Pinch, Blocked }

    HeartEmerge emerge;
    Transform heartModel;
    Transform pivot;
    Vector3 center;     // tâm tim trong tọa độ HeartModel
    float bottomY;      // đáy tim (mặt giấy) trong tọa độ HeartModel
    float scale = 1f;

    Mode mode;
    int touchId;
    Vector2 startPos, prevPos;
    float prevPinchDist;

    readonly Vector2[] touchPos = new Vector2[2];
    readonly int[] touchIds = new int[2];

    void Awake()
    {
        emerge = GetComponent<HeartEmerge>();
        heartModel = emerge.HeartModel;
        if (heartModel == null)
        {
            Debug.LogError($"{nameof(HeartInteraction)}: HeartEmerge chưa gán Heart Model. Tắt tương tác.", this);
            enabled = false;
            return;
        }
        CreatePivot();
    }

    void OnEnable() { if (emerge != null) emerge.Replayed += ResetView; }
    void OnDisable()
    {
        if (emerge != null) emerge.Replayed -= ResetView;
        mode = Mode.None;
    }

    // Trả tim về góc nhìn và kích thước ban đầu
    public void ResetView()
    {
        if (pivot == null) return;
        pivot.localRotation = Quaternion.identity;
        SetScale(1f);
        mode = Mode.None;
    }

    void CreatePivot()
    {
        // Khung bao của mô hình tim (bỏ qua các sphere/nhãn của Hotspots)
        Transform hotspots = emerge.Hotspots != null ? emerge.Hotspots.transform : null;
        var renderers = new List<Renderer>();
        foreach (var r in heartModel.GetComponentsInChildren<Renderer>(true))
            if (hotspots == null || !r.transform.IsChildOf(hotspots)) renderers.Add(r);

        if (renderers.Count > 0)
        {
            Bounds b = FitHeartToMarker.GetBoundsIn(heartModel, renderers.ToArray());
            center = b.center;
            bottomY = b.min.y;
        }

        pivot = new GameObject("InteractPivot").transform;
        pivot.SetParent(heartModel, false);
        pivot.localPosition = center;

        // Chuyển mọi con hiện có (Visual, Hotspots) vào pivot, giữ nguyên tư thế
        var children = new List<Transform>();
        foreach (Transform child in heartModel) if (child != pivot) children.Add(child);
        foreach (var child in children) child.SetParent(pivot, true);
    }

    void Update()
    {
        if (HeadsetView.Active || !heartModel.gameObject.activeInHierarchy)
        {
            mode = Mode.None;
            return;
        }

        var ts = Touchscreen.current;
        int count = ts != null ? ReadTouches(ts) : 0;

        if (count >= 2) HandlePinch();
        else if (count == 1) HandleDrag(touchIds[0], touchPos[0]);
        else if (!HandleMouse()) mode = Mode.None;
    }

    int ReadTouches(Touchscreen ts)
    {
        int n = 0;
        var touches = ts.touches;
        for (int i = 0; i < touches.Count; i++)
        {
            TouchControl t = touches[i];
            if (!t.isInProgress) continue;
            if (n < 2)
            {
                touchIds[n] = t.touchId.ReadValue();
                touchPos[n] = t.position.ReadValue();
            }
            n++;
        }
        return n;
    }

    void HandlePinch()
    {
        float dist = Vector2.Distance(touchPos[0], touchPos[1]);
        if (mode != Mode.Pinch)
        {
            mode = Mode.Pinch;
            prevPinchDist = dist;
            return;
        }
        if (prevPinchDist > 1f) SetScale(scale * dist / prevPinchDist);
        prevPinchDist = dist;
    }

    void HandleDrag(int id, Vector2 pos)
    {
        // Vừa chụm hai ngón xong, còn lại một ngón: chờ nhấc hết tay để tim không bị giật xoay
        if (mode == Mode.Pinch || mode == Mode.Blocked)
        {
            mode = Mode.Blocked;
            return;
        }

        if (mode == Mode.None || id != touchId)
        {
            mode = Mode.Pending;
            touchId = id;
            startPos = prevPos = pos;
            return;
        }

        if (mode == Mode.Pending)
        {
            if ((pos - startPos).sqrMagnitude < dragThresholdPixels * dragThresholdPixels) return;
            mode = Mode.Rotate;
        }

        Rotate(pos - prevPos);
        prevPos = pos;
    }

    // Chuột chỉ dùng khi không có cảm ứng (Editor/PC). Trả về true nếu đang có thao tác chuột.
    bool HandleMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null) return false;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
            SetScale(scale * (1f + Mathf.Sign(scroll) * scrollZoomStep));

        if (!mouse.leftButton.isPressed) return false;
        HandleDrag(-1, mouse.position.ReadValue());
        return true;
    }

    void Rotate(Vector2 delta)
    {
        float degPerPixel = degreesPerScreen / Mathf.Max(1, Screen.width);
        Transform anchor = transform;

        // Kéo sang phải thì mặt trước của tim cũng đi sang phải
        Quaternion rot = Quaternion.AngleAxis(-delta.x * degPerPixel, anchor.up);

        if (allowTilt)
        {
            // Trục lật = hướng "phải" của camera, chiếu lên mặt giấy để không bị nghiêng lệch
            Camera cam = Camera.main;
            Vector3 right = cam != null ? Vector3.ProjectOnPlane(cam.transform.right, anchor.up) : anchor.right;
            if (right.sqrMagnitude > 1e-6f)
                rot = Quaternion.AngleAxis(delta.y * degPerPixel, right.normalized) * rot;
        }

        pivot.rotation = rot * pivot.rotation;
    }

    void SetScale(float s)
    {
        scale = Mathf.Clamp(s, minScale, maxScale);
        pivot.localScale = Vector3.one * scale;
        // Giữ đáy tim ở mặt giấy: pivot nằm ở tâm nên phải nâng lên theo tỉ lệ
        pivot.localPosition = new Vector3(center.x, bottomY + (center.y - bottomY) * scale, center.z);
    }
}

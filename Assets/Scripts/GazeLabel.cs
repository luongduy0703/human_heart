using UnityEngine;
using UnityEngine.UI;

// Chọn bằng cách nhìn: bắn tia từ giữa camera, nhìn vào một HeartPart đủ dwellTime giây
// thì gọi Toggle() một lần. Phải nhìn ra chỗ khác rồi nhìn lại mới bật/tắt tiếp.
// Gắn lên camera AR (Main Camera trong XR Origin).
public class GazeLabel : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] float maxDistance = 3f;
    [SerializeField] LayerMask layers = ~0;
    [SerializeField] float dwellTime = 1.5f;

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

        if (current != null && !fired)
        {
            timer += Time.deltaTime;
            if (timer >= dwellTime)
            {
                current.Toggle();
                fired = true;
            }
        }

        SetReticle(current != null && !fired ? timer / dwellTime : 0f);
    }

    void SetReticle(float amount)
    {
        if (reticleFills == null) return;
        foreach (var img in reticleFills)
            if (img != null) img.fillAmount = amount;
    }
}

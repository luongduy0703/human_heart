using UnityEngine;
using UnityEngine.UI;

// Chế độ kính VR: camera AR vẽ vào RenderTexture, rồi hiển thị cùng một ảnh lên
// hai RawImage trái/phải. Mỗi bên lấy nửa giữa khung hình (uv x 0.25 → 0.75)
// để đúng tỉ lệ nửa màn hình. Khóa 30 fps và không cho tắt màn hình.
// Gắn lên Canvas (Screen Space - Overlay) chứa hai RawImage.
public class HeadsetView : MonoBehaviour
{
    [SerializeField] Camera arCamera;
    [SerializeField] RawImage leftEye;
    [SerializeField] RawImage rightEye;

    [Tooltip("Độ phân giải RenderTexture so với màn hình; giảm xuống nếu máy yếu")]
    [SerializeField, Range(0.3f, 1f)] float resolutionScale = 0.6f;

    [SerializeField] int targetFps = 30;

    [Tooltip("Tự đặt anchor cho hai RawImage chiếm nửa trái / nửa phải màn hình")]
    [SerializeField] bool autoLayout = true;

    RenderTexture rt;
    int lastW, lastH;

    void Awake()
    {
        QualitySettings.vSyncCount = 0;          // tắt vSync thì targetFrameRate mới có tác dụng
        Application.targetFrameRate = targetFps;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        if (autoLayout)
        {
            Layout(leftEye.rectTransform, 0f, 0.5f);
            Layout(rightEye.rectTransform, 0.5f, 1f);
        }
        var middleHalf = new Rect(0.25f, 0f, 0.5f, 1f);
        leftEye.uvRect = middleHalf;
        rightEye.uvRect = middleHalf;

        CreateClearCamera();
    }

    void OnEnable() => Rebuild();

    void Update()
    {
        // Tạo lại RenderTexture nếu kích thước màn hình đổi (xoay máy...)
        if (Screen.width != lastW || Screen.height != lastH) Rebuild();
    }

    void OnDisable()
    {
        if (arCamera != null) arCamera.targetTexture = null;
        Release();
    }

    void Rebuild()
    {
        lastW = Screen.width;
        lastH = Screen.height;
        Release();

        int w = Mathf.Max(1, Mathf.RoundToInt(lastW * resolutionScale));
        int h = Mathf.Max(1, Mathf.RoundToInt(lastH * resolutionScale));
        // Cần depth 24 bit để DepthMask che được phần tim dưới mặt giấy
        rt = new RenderTexture(w, h, 24, RenderTextureFormat.Default) { name = "HeadsetRT", antiAliasing = 1 };
        rt.Create();

        arCamera.targetTexture = rt;
        leftEye.texture = rt;
        rightEye.texture = rt;
    }

    void Release()
    {
        if (rt == null) return;
        rt.Release();
        Destroy(rt);
        rt = null;
    }

    static void Layout(RectTransform r, float xMin, float xMax)
    {
        r.anchorMin = new Vector2(xMin, 0f);
        r.anchorMax = new Vector2(xMax, 1f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    // Camera AR không còn vẽ ra màn hình, nên cần một camera rỗng xóa màn hình về màu đen,
    // nếu không phần ngoài hai RawImage sẽ bị rác hình.
    void CreateClearCamera()
    {
        var go = new GameObject("HeadsetClearCamera");
        go.transform.SetParent(transform, false);
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 0;
        cam.useOcclusionCulling = false;
        cam.depth = arCamera.depth + 1;
    }
}

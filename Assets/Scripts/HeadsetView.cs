using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Chế độ kính VR (kiểu Cardboard), gắn lên Canvas (Screen Space - Overlay) chứa hai RawImage.
// - Camera AR vẽ vào RenderTexture; mỗi mắt lấy nửa giữa khung hình (uv x 0.25 → 0.75)
//   để đúng tỉ lệ nửa màn hình. Cách cắt này chỉ đúng khi máy nằm ngang nên khóa LandscapeLeft.
// - stereo = true: camera AR chỉ vẽ ảnh camera thật (nền chung, vì điện thoại chỉ có một camera),
//   còn tim được vẽ bởi hai camera mắt lệch nhau ipd, phủ lên nền → có chiều sâu 3D.
//   Tốn thêm một lần vẽ tim; máy quá yếu thì tắt stereo.
// - lensCorrection: méo thùng trước để bù méo gối của thấu kính (shader Custom/LensDistortUI).
// Khóa 30 fps và không cho tắt màn hình.
public class HeadsetView : MonoBehaviour
{
    // true khi đang ở chế độ kính; GazeLabel đọc để coi cú chạm là nút bấm của kính
    public static bool Active { get; private set; }

    [SerializeField] Camera arCamera;
    [SerializeField] RawImage leftEye;
    [SerializeField] RawImage rightEye;

    [Tooltip("Độ phân giải RenderTexture so với màn hình; giảm xuống nếu máy yếu")]
    [SerializeField, Range(0.3f, 1f)] float resolutionScale = 0.6f;

    [SerializeField] int targetFps = 30;

    [Tooltip("Tự đặt anchor cho hai RawImage chiếm nửa trái / nửa phải màn hình")]
    [SerializeField] bool autoLayout = true;

    [Header("3D")]
    [Tooltip("Hai camera mắt lệch nhau để tim có chiều sâu. Tắt: hai mắt cùng một ảnh, nhẹ hơn")]
    [SerializeField] bool stereo = true;

    [Tooltip("Khoảng cách giữa hai mắt (mét)")]
    [SerializeField] float ipd = 0.064f;

    [Header("Bù méo thấu kính")]
    [SerializeField] bool lensCorrection = true;

    [Tooltip("Hệ số méo bậc 2 và bậc 4; Cardboard v1 khoảng 0.22 / 0.24. Chỉnh theo kính thật")]
    [SerializeField] float k1 = 0.22f;
    [SerializeField] float k2 = 0.24f;

    [Tooltip("Shader Custom/LensDistortUI; để trống thì tìm theo tên (khi build cần kéo vào để không bị loại bỏ)")]
    [SerializeField] Shader lensShader;

    static readonly Rect MiddleHalf = new Rect(0.25f, 0f, 0.5f, 1f);
    static readonly Rect FullRect = new Rect(0f, 0f, 1f, 1f);
    static readonly int K1Id = Shader.PropertyToID("_K1");
    static readonly int K2Id = Shader.PropertyToID("_K2");
    static readonly int UvRectId = Shader.PropertyToID("_UvRect");
    static readonly int AspectId = Shader.PropertyToID("_Aspect");

    RenderTexture backgroundRT, leftRT, rightRT;
    Camera leftCam, rightCam;
    RawImage leftOverlay, rightOverlay;
    readonly List<Material> lensMaterials = new List<Material>();
    int savedCullingMask;
    int lastW, lastH;
    bool ready;

    void Awake()
    {
        if (arCamera == null || leftEye == null || rightEye == null)
        {
            Debug.LogError($"{nameof(HeadsetView)} trên '{name}': chưa gán Ar Camera / Left Eye / Right Eye. Tắt chế độ kính.", this);
            enabled = false;
            return;
        }

        Screen.orientation = ScreenOrientation.LandscapeLeft;
        QualitySettings.vSyncCount = 0;          // tắt vSync thì targetFrameRate mới có tác dụng
        Application.targetFrameRate = targetFps;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        if (autoLayout)
        {
            Layout(leftEye.rectTransform, 0f, 0.5f);
            Layout(rightEye.rectTransform, 0.5f, 1f);
        }

        if (stereo)
        {
            leftCam = CreateEyeCamera("LeftEyeCamera", -ipd * 0.5f);
            rightCam = CreateEyeCamera("RightEyeCamera", ipd * 0.5f);
            leftOverlay = CreateOverlay(leftEye);
            rightOverlay = CreateOverlay(rightEye);
        }

        if (lensCorrection) SetupLensMaterials();
        CreateClearCamera();
        ready = true;
    }

    void OnEnable()
    {
        if (!ready) return;
        Active = true;
        if (stereo)
        {
            // Camera AR chỉ còn vẽ ảnh nền; tim do hai camera mắt vẽ
            savedCullingMask = arCamera.cullingMask;
            arCamera.cullingMask = 0;
            leftCam.cullingMask = rightCam.cullingMask = savedCullingMask;
            leftCam.enabled = rightCam.enabled = true;
        }
        Rebuild();
    }

    void OnDisable()
    {
        if (!ready) return;
        Active = false;
        arCamera.targetTexture = null;
        if (stereo)
        {
            arCamera.cullingMask = savedCullingMask;
            leftCam.enabled = rightCam.enabled = false;
        }
        Release();
    }

    void OnDestroy()
    {
        foreach (var m in lensMaterials) Destroy(m);
        lensMaterials.Clear();
    }

    void Update()
    {
        // Tạo lại RenderTexture nếu kích thước màn hình đổi
        if (Screen.width != lastW || Screen.height != lastH) Rebuild();
    }

    void LateUpdate()
    {
        if (!stereo) return;
        // ARCameraManager đặt projection của camera AR theo camera thật mỗi khung hình (trong Update).
        // Mắt chỉ vẽ nửa giữa khung hình: nhân 2 hàng x của ma trận chiếu = cắt lấy nửa giữa theo chiều ngang.
        Matrix4x4 p = arCamera.projectionMatrix;
        p.m00 *= 2f; p.m01 *= 2f; p.m02 *= 2f; p.m03 *= 2f;
        leftCam.projectionMatrix = p;
        rightCam.projectionMatrix = p;
    }

    void Rebuild()
    {
        lastW = Screen.width;
        lastH = Screen.height;
        Release();

        int w = Mathf.Max(1, Mathf.RoundToInt(lastW * resolutionScale));
        int h = Mathf.Max(1, Mathf.RoundToInt(lastH * resolutionScale));

        // Cần depth 24 bit để DepthMask che được phần tim dưới mặt giấy
        backgroundRT = CreateRT("HeadsetRT", w, h, RenderTextureFormat.Default);
        arCamera.targetTexture = backgroundRT;
        leftEye.texture = rightEye.texture = backgroundRT;
        leftEye.uvRect = rightEye.uvRect = MiddleHalf;

        if (stereo)
        {
            // Mỗi mắt chỉ cần nửa chiều ngang; ARGB32 để giữ alpha (nền trong suốt)
            int eyeW = Mathf.Max(1, w / 2);
            leftRT = CreateRT("HeadsetLeftRT", eyeW, h, RenderTextureFormat.ARGB32);
            rightRT = CreateRT("HeadsetRightRT", eyeW, h, RenderTextureFormat.ARGB32);
            leftCam.targetTexture = leftRT;
            rightCam.targetTexture = rightRT;
            leftOverlay.texture = leftRT;
            rightOverlay.texture = rightRT;
        }

        UpdateLensMaterials();
    }

    static RenderTexture CreateRT(string rtName, int w, int h, RenderTextureFormat format)
    {
        var rt = new RenderTexture(w, h, 24, format) { name = rtName, antiAliasing = 1 };
        rt.Create();
        return rt;
    }

    void Release()
    {
        if (leftCam != null) leftCam.targetTexture = null;
        if (rightCam != null) rightCam.targetTexture = null;
        ReleaseRT(ref backgroundRT);
        ReleaseRT(ref leftRT);
        ReleaseRT(ref rightRT);
    }

    static void ReleaseRT(ref RenderTexture rt)
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

    // Camera mắt: con của camera AR, lệch ngang offsetX; nền trong suốt để phủ lên ảnh camera thật
    Camera CreateEyeCamera(string camName, float offsetX)
    {
        var go = new GameObject(camName);
        go.transform.SetParent(arCamera.transform, false);
        go.transform.localPosition = new Vector3(offsetX, 0f, 0f);
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.clear;
        cam.nearClipPlane = arCamera.nearClipPlane;
        cam.farClipPlane = arCamera.farClipPlane;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.useOcclusionCulling = false;
        cam.enabled = false;
        return cam;
    }

    // RawImage phủ kín mắt, hiển thị ảnh của camera mắt lên trên ảnh nền
    static RawImage CreateOverlay(RawImage eye)
    {
        var go = new GameObject(eye.name + "Overlay", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(eye.transform, false);
        var img = go.GetComponent<RawImage>();
        img.raycastTarget = false;
        img.uvRect = FullRect;
        Layout(img.rectTransform, 0f, 1f);
        return img;
    }

    void SetupLensMaterials()
    {
        if (lensShader == null) lensShader = Shader.Find("Custom/LensDistortUI");
        if (lensShader == null)
        {
            Debug.LogWarning($"{nameof(HeadsetView)}: không tìm thấy shader Custom/LensDistortUI, bỏ qua bù méo thấu kính.", this);
            return;
        }
        // Mỗi RawImage một material vì _UvRect khác nhau (nền cắt nửa giữa, lớp phủ dùng cả ảnh)
        foreach (var img in new[] { leftEye, rightEye, leftOverlay, rightOverlay })
        {
            if (img == null) continue;
            var m = new Material(lensShader) { name = img.name + "Lens" };
            img.material = m;
            lensMaterials.Add(m);
        }
    }

    void UpdateLensMaterials()
    {
        // Mỗi mắt chiếm nửa màn hình (autoLayout)
        float aspect = lastH > 0 ? 0.5f * lastW / lastH : 1f;
        foreach (var m in lensMaterials)
        {
            m.SetFloat(K1Id, k1);
            m.SetFloat(K2Id, k2);
            m.SetFloat(AspectId, aspect);
        }
        SetUvRect(leftEye);
        SetUvRect(rightEye);
        SetUvRect(leftOverlay);
        SetUvRect(rightOverlay);
    }

    static void SetUvRect(RawImage img)
    {
        if (img == null || img.material == null || !img.material.HasProperty(UvRectId)) return;
        Rect r = img.uvRect;
        img.material.SetVector(UvRectId, new Vector4(r.x, r.y, r.width, r.height));
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

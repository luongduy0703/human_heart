using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Công cụ Editor dựng Hotspots trong scene đang mở. Menu: Heart VR → ...
// "Thiết lập Hotspots" (có Ctrl+Z):
//  1. Đặt Hotspots về (0,0,0), không xoay, scale 1 (con của HeartModel, lên xuống cùng tim).
//  2. Tạo thêm các bộ phận trong danh sách Parts còn thiếu.
//  3. Mỗi HeartPart: scale 1, SphereCollider bán kính ColliderRadius; bộ phận mới, đang nằm ngoài tim,
//     hoặc khi chọn "đặt lại vị trí" thì đặt theo bảng Parts rồi dò bề mặt tim để nằm sát bề mặt.
//  4. Thêm sphere nhỏ có màu (Marker) và nhãn TextMeshPro 3D (Label, có FaceCamera, scale 0.01),
//     rồi gán Label và Marker vào HeartPart.
//  5. Xóa GazeLabel thừa (cái không nằm trên camera, ví dụ trên XR Origin).
//  6. Gắn HeartInteraction (kéo xoay, chụm zoom) lên HeartAnchor nếu chưa có.
// "Đặt các điểm sát bề mặt tim": giữ vị trí ngang của các điểm (đã kéo tay), chỉ chỉnh độ cao cho sát bề mặt.
static class HeartSceneSetup
{
    const float ColliderRadius = 0.015f;    // 1.5 cm (nhiều bộ phận trên tim ~12 cm nên để nhỏ)
    const float MarkerDiameter = 0.012f;    // 1.2 cm
    const float SurfaceOffset = 0.003f;     // tâm chấm cao hơn bề mặt 3 mm: chấm lộ ra khoảng 3/4
    const float LabelScale = 0.01f;
    const float LabelFontSize = 14f;        // ở scale 0.01 chữ cao khoảng 1.4 cm
    const float LabelOffset = 0.025f;       // nhãn cách điểm 2.5 cm
    const string MaterialFolder = "Assets/Materials/Hotspots";
    const string TmpEssentials = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

    // pos: vị trí nhìn từ mặt trước, chia cho nửa kích thước khung bao tim:
    //      x: -1 mép trái người xem (bên phải bệnh nhân) .. 1 mép phải; y: -1 mỏm tim .. 1 đầu các mạch máu lớn.
    // height: độ cao bề mặt (0 mặt sau .. 1 mặt trước của khung bao); chỉ dùng khi không dò được bề mặt lưới.
    struct PartInfo
    {
        public string display; public Vector2 pos; public float height; public Color color;
        public PartInfo(string d, float x, float y, float h, Color c) { display = d; pos = new Vector2(x, y); height = h; color = c; }
    }

    // Tên object → tên trên nhãn (tiếng Anh), vị trí, màu.
    // Vị trí đo trên Assets/Models/heart.glb (mặt trước có tiểu nhĩ, rãnh liên thất, cung động mạch chủ);
    // các điểm cách nhau ít nhất 3 cm khi tim dài 20 cm. Đổi mô hình khác thì phải đo lại.
    static readonly Dictionary<string, PartInfo> Parts = new Dictionary<string, PartInfo>
    {
        // Bốn buồng tim (tâm nhĩ đặt trên tiểu nhĩ, phần tâm nhĩ nhìn thấy từ mặt trước)
        { "TamNhiPhai",       new PartInfo("Right atrium",          -0.701f,  0.26f, 0.776f, new Color(0.25f, 0.45f, 1f)) },
        { "TamThatPhai",      new PartInfo("Right ventricle",       -0.310f, -0.34f, 0.980f, new Color(0.2f, 0.85f, 1f)) },
        { "TamNhiTrai",       new PartInfo("Left atrium",            0.440f,  0.12f, 0.776f, new Color(1f, 0.6f, 0.15f)) },
        { "TamThatTrai",      new PartInfo("Left ventricle",         0.424f, -0.42f, 0.892f, new Color(1f, 0.2f, 0.25f)) },
        // Mạch máu lớn ở đáy tim
        { "DongMachChu",      new PartInfo("Aorta",                  0.196f,  0.74f, 0.545f, new Color(0.85f, 0f, 0.1f)) },
        { "DongMachPhoi",     new PartInfo("Pulmonary trunk",        0.163f,  0.40f, 0.680f, new Color(0.35f, 0.25f, 0.9f)) },
        { "TinhMachChuTren",  new PartInfo("Superior vena cava",    -0.489f,  0.62f, 0.487f, new Color(0.1f, 0.2f, 0.7f)) },
        { "TinhMachChuDuoi",  new PartInfo("Inferior vena cava",    -0.848f, -0.36f, 0.722f, new Color(0.15f, 0.35f, 0.6f)) },
        { "TinhMachPhoi",     new PartInfo("Pulmonary veins",        0.865f,  0.37f, 0.344f, new Color(0.95f, 0.4f, 0.6f)) },
        // Mạch vành và mỏm tim
        { "DongMachVanhPhai", new PartInfo("Right coronary artery", -0.816f, -0.05f, 0.786f, new Color(1f, 0.9f, 0.2f)) },
        { "DongMachVanhTrai", new PartInfo("Left coronary artery",   0.098f, -0.16f, 0.980f, new Color(0.6f, 1f, 0.3f)) },
        { "MomTim",           new PartInfo("Apex",                   0.098f, -0.88f, 0.776f, new Color(1f, 1f, 1f)) },
    };

    [MenuItem("Heart VR/Thiết lập Hotspots")]
    static void Setup() => Run(false);

    [MenuItem("Heart VR/Thiết lập Hotspots (đặt lại vị trí)")]
    static void SetupAndReposition() => Run(true);

    [MenuItem("Heart VR/Đặt các điểm sát bề mặt tim")]
    static void SnapPartsToSurface()
    {
        if (!TryGetContext(out _, out Transform heartModel, out Transform hotspots)) return;
        if (!TryGetHeartBounds(heartModel, hotspots, out Bounds heart))
        {
            Debug.LogError("[Setup] Không tìm thấy Renderer nào của tim trong HeartModel.", heartModel);
            return;
        }
        var meshes = GetHeartMeshes(heartModel, hotspots);

        // Đang chọn bộ phận nào thì chỉ đặt các bộ phận đó, không chọn gì thì đặt tất cả
        var parts = new List<HeartPart>();
        foreach (var p in Selection.GetFiltered<HeartPart>(SelectionMode.Editable))
            if (p.transform.IsChildOf(hotspots)) parts.Add(p);
        if (parts.Count == 0) parts.AddRange(hotspots.GetComponentsInChildren<HeartPart>(true));

        Undo.SetCurrentGroupName("Heart VR: Đặt điểm sát bề mặt");
        int group = Undo.GetCurrentGroup();
        int ok = 0;
        foreach (var part in parts)
        {
            if (SnapToSurface(part.transform, hotspots, heart, meshes)) ok++;
            else Debug.LogWarning($"[Setup] Không dò được bề mặt tim dưới '{part.name}' (điểm nằm ngoài hình tim khi nhìn từ trên?).", part);
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(hotspots.gameObject.scene);
        Debug.Log($"[Setup] Đã đặt {ok}/{parts.Count} điểm sát bề mặt tim. Nhớ lưu scene (Ctrl+S).", hotspots);
    }

    static void Run(bool reposition)
    {
        if (!EnsureTmp()) return;
        if (!TryGetContext(out HeartEmerge emerge, out Transform heartModel, out Transform hotspots)) return;

        Undo.SetCurrentGroupName("Heart VR: Thiết lập Hotspots");
        int group = Undo.GetCurrentGroup();

        Undo.RecordObject(hotspots, "Hotspots");
        hotspots.localPosition = Vector3.zero;
        hotspots.localRotation = Quaternion.identity;
        hotspots.localScale = Vector3.one;

        if (!TryGetHeartBounds(heartModel, hotspots, out Bounds heart))
        {
            Debug.LogError("[Setup] Không tìm thấy Renderer nào của tim trong HeartModel.", heartModel);
            return;
        }
        var meshes = GetHeartMeshes(heartModel, hotspots);

        var created = CreateMissingParts(hotspots);

        var parts = hotspots.GetComponentsInChildren<HeartPart>(true);
        int placed = 0, unsnapped = 0;
        foreach (var part in parts)
        {
            int result = SetupPart(part, heart, reposition || created.Contains(part), hotspots.gameObject.layer, hotspots, meshes);
            if (result > 0) placed++;
            if (result == 2) unsnapped++;
        }
        if (unsnapped > 0)
            Debug.LogWarning($"[Setup] {unsnapped} điểm không dò được bề mặt tim, đang dùng độ cao trong bảng; hãy kiểm tra lại các điểm đó.", hotspots);

        RemoveExtraGazeLabels();

        if (emerge.GetComponent<HeartInteraction>() == null)
        {
            Undo.AddComponent<HeartInteraction>(emerge.gameObject);
            Debug.Log($"[Setup] Đã gắn HeartInteraction lên '{emerge.name}'.", emerge);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(hotspots.gameObject.scene);
        Debug.Log($"[Setup] Xong {parts.Length} HeartPart ({created.Count} mới tạo, {placed} được đặt lại vị trí). " +
                  $"Khung bao tim (theo Hotspots): tâm {heart.center.ToString("F3")}, kích thước {heart.size.ToString("F3")}. " +
                  "Kiểm tra các điểm rồi lưu scene (Ctrl+S).", hotspots);
    }

    static bool TryGetContext(out HeartEmerge emerge, out Transform heartModel, out Transform hotspots)
    {
        heartModel = hotspots = null;
        emerge = Object.FindAnyObjectByType<HeartEmerge>(FindObjectsInactive.Include);
        if (emerge == null)
        {
            Debug.LogError("[Setup] Không tìm thấy HeartEmerge trong scene.");
            return false;
        }
        var so = new SerializedObject(emerge);
        heartModel = so.FindProperty("heartModel").objectReferenceValue as Transform;
        var hotspotsGo = so.FindProperty("hotspots").objectReferenceValue as GameObject;
        if (heartModel == null || hotspotsGo == null)
        {
            Debug.LogError("[Setup] HeartEmerge chưa gán Heart Model hoặc Hotspots.", emerge);
            return false;
        }
        hotspots = hotspotsGo.transform;
        return true;
    }

    // Tạo object cho các bộ phận trong Parts chưa có trong Hotspots
    static HashSet<HeartPart> CreateMissingParts(Transform hotspots)
    {
        var existing = new HashSet<string>();
        foreach (var p in hotspots.GetComponentsInChildren<HeartPart>(true)) existing.Add(p.name);

        var created = new HashSet<HeartPart>();
        foreach (var name in Parts.Keys)
        {
            if (existing.Contains(name)) continue;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "HeartPart");
            go.layer = hotspots.gameObject.layer;
            go.transform.SetParent(hotspots, false);
            created.Add(go.AddComponent<HeartPart>());
            Debug.Log($"[Setup] Tạo bộ phận mới '{name}'.", go);
        }
        return created;
    }

    // Trả về 0: giữ vị trí cũ, 1: đặt lại và dò được bề mặt, 2: đặt lại nhưng không dò được bề mặt
    static int SetupPart(HeartPart part, Bounds heart, bool reposition, int layer, Transform hotspots, List<MeshFilter> meshes)
    {
        Transform t = part.transform;
        bool known = Parts.TryGetValue(t.name, out PartInfo info);
        if (!known) info = new PartInfo(t.name, 0f, 0f, 1f, Color.gray);

        Undo.RecordObject(t, "HeartPart");
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        int result = 0;
        Bounds roomy = heart;
        roomy.Expand(0.02f);
        if (reposition || !roomy.Contains(t.localPosition))
        {
            t.localPosition = SuggestPosition(heart, info);
            result = SnapToSurface(t, hotspots, heart, meshes) ? 1 : 2;
        }

        var col = t.GetComponent<SphereCollider>();
        if (col == null) col = Undo.AddComponent<SphereCollider>(t.gameObject);
        Undo.RecordObject(col, "Collider");
        col.radius = ColliderRadius;
        col.center = Vector3.zero;

        var marker = CreateMarker(t, info, layer);
        var label = CreateLabel(t, info, heart, layer);

        var so = new SerializedObject(part);
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("marker").objectReferenceValue = marker;
        so.ApplyModifiedProperties();
        return result;
    }

    // Tim nằm ngửa (thấp theo Y): mặt trước hướng lên +Y, mạch máu lớn hướng +Z.
    // Tim đứng (cao theo Y): mặt trước hướng về người xem (-Z), mạch máu lớn hướng lên +Y.
    static bool IsLying(Bounds b) => b.size.y < 0.6f * Mathf.Max(b.size.x, b.size.z);

    static Vector3 SuggestPosition(Bounds b, PartInfo info)
    {
        Vector3 c = b.center, e = b.extents;
        if (IsLying(b))
            return new Vector3(c.x + info.pos.x * e.x, b.min.y + info.height * b.size.y, c.z + info.pos.y * e.z);
        return new Vector3(c.x + info.pos.x * e.x, c.y + info.pos.y * e.y, b.max.z - info.height * b.size.z);
    }

    // Bắn tia từ phía mặt trước vào tim tại vị trí ngang hiện tại của điểm, đặt điểm ngay trên bề mặt
    static bool SnapToSurface(Transform part, Transform space, Bounds heart, List<MeshFilter> meshes)
    {
        Vector3 p = part.localPosition;
        bool lying = IsLying(heart);
        Vector3 originLocal = lying ? new Vector3(p.x, heart.max.y + 0.05f, p.z) : new Vector3(p.x, p.y, heart.min.z - 0.05f);
        Vector3 dirLocal = lying ? Vector3.down : Vector3.forward;
        var ray = new Ray(space.TransformPoint(originLocal), space.TransformDirection(dirLocal));
        if (!RaycastMeshes(ray, meshes, out Vector3 hit)) return false;

        Undo.RecordObject(part, "Snap");
        part.localPosition = space.InverseTransformPoint(hit) - dirLocal * SurfaceOffset;
        return true;
    }

    static List<MeshFilter> GetHeartMeshes(Transform heartModel, Transform hotspots)
    {
        var list = new List<MeshFilter>();
        foreach (var mf in heartModel.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null && !mf.transform.IsChildOf(hotspots)) list.Add(mf);
        return list;
    }

    // Điểm trúng gần gốc tia nhất trên các lưới của tim
    static bool RaycastMeshes(Ray ray, List<MeshFilter> meshes, out Vector3 point)
    {
        point = default;
        float best = float.MaxValue;
        foreach (var mf in meshes)
        {
            if (!RaycastMesh(ray, mf, out Vector3 p)) continue;
            float d = (p - ray.origin).sqrMagnitude;
            if (d < best) { best = d; point = p; }
        }
        return best < float.MaxValue;
    }

    // HandleUtility.IntersectRayMesh là hàm nội bộ của Editor, đọc được cả lưới không bật Read/Write
    static readonly MethodInfo IntersectRayMeshMethod = typeof(HandleUtility).GetMethod(
        "IntersectRayMesh", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null,
        new[] { typeof(Ray), typeof(Mesh), typeof(Matrix4x4), typeof(RaycastHit).MakeByRefType() }, null);

    static bool RaycastMesh(Ray ray, MeshFilter mf, out Vector3 point)
    {
        point = default;
        var renderer = mf.GetComponent<Renderer>();
        Bounds worldBounds = renderer != null ? renderer.bounds : new Bounds(mf.transform.position, Vector3.one * 10f);
        worldBounds.Expand(0.01f);

        // Cách 1: hàm nội bộ (nhanh, không cần collider)
        if (IntersectRayMeshMethod != null)
        {
            try
            {
                var args = new object[] { ray, mf.sharedMesh, mf.transform.localToWorldMatrix, null };
                if ((bool)IntersectRayMeshMethod.Invoke(null, args))
                {
                    point = ((RaycastHit)args[3]).point;
                    if (worldBounds.Contains(point)) return true;
                }
            }
            catch (System.Exception) { /* bản Unity khác chữ ký hàm: dùng cách 2 */ }
        }

        // Cách 2: MeshCollider tạm thời trên object ẩn, xóa ngay sau khi dò
        var probe = new GameObject("__HeartSurfaceProbe") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            probe.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
            probe.transform.localScale = mf.transform.lossyScale;
            var mc = probe.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            Physics.SyncTransforms();
            if (mc.Raycast(ray, out RaycastHit hit, 10f))
            {
                point = hit.point;
                return true;
            }
            return false;
        }
        finally
        {
            Object.DestroyImmediate(probe);
        }
    }

    static Transform CreateMarker(Transform parent, PartInfo info, int layer)
    {
        Transform old = parent.Find("Marker");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Undo.RegisterCreatedObjectUndo(go, "Marker");
        go.name = "Marker";
        go.layer = layer;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // HeartPart đã có collider riêng
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one * MarkerDiameter;

        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = GetMaterial(parent.name, info.color);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }

    static GameObject CreateLabel(Transform parent, PartInfo info, Bounds heart, int layer)
    {
        Transform old = parent.Find("Label");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var go = new GameObject("Label", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Label");
        go.layer = layer;
        go.transform.SetParent(parent, false);

        // Đẩy nhãn ra ngoài tim (theo hướng từ tâm tim tới điểm) và hơi lên trên
        Vector3 outward = parent.localPosition - heart.center;
        outward.y = 0f;
        outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.back;
        go.transform.localPosition = (outward + Vector3.up * 0.5f).normalized * LabelOffset;
        go.transform.localScale = Vector3.one * LabelScale;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = info.display;
        tmp.fontSize = LabelFontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.color = Color.white;
        tmp.rectTransform.sizeDelta = new Vector2(20f, 4f);
        tmp.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        go.AddComponent<FaceCamera>();
        // Lúc chạy HeartPart tự bật/tắt nhãn; ẩn sẵn để cửa sổ Scene đỡ rối khi kéo các điểm
        go.SetActive(false);
        return go;
    }

    static Material GetMaterial(string partName, Color color)
    {
        string path = $"{MaterialFolder}/Hotspot_{partName}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Hotspots");
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Khung bao các Renderer của tim (bỏ qua Hotspots), theo tọa độ local của 'space'
    static bool TryGetHeartBounds(Transform heartModel, Transform space, out Bounds result)
    {
        result = default;
        bool any = false;
        foreach (var r in heartModel.GetComponentsInChildren<Renderer>(true))
        {
            if (r.transform.IsChildOf(space)) continue;
            Bounds lb = r.localBounds;
            Vector3 min = lb.min, max = lb.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                Vector3 p = space.InverseTransformPoint(r.transform.TransformPoint(corner));
                if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                else result.Encapsulate(p);
            }
        }
        return any;
    }

    // Giữ GazeLabel trên camera, xóa các cái còn lại (ví dụ trên XR Origin)
    static void RemoveExtraGazeLabels()
    {
        var all = Object.FindObjectsByType<GazeLabel>(FindObjectsInactive.Include);
        bool hasOnCamera = false;
        foreach (var g in all) if (g.GetComponent<Camera>() != null) hasOnCamera = true;
        if (!hasOnCamera)
        {
            Debug.LogWarning("[Setup] Không có GazeLabel nào nằm trên camera; giữ nguyên, hãy gắn GazeLabel lên Main Camera.");
            return;
        }
        foreach (var g in all)
        {
            if (g.GetComponent<Camera>() != null) continue;
            Debug.Log($"[Setup] Xóa GazeLabel thừa trên '{g.name}'.");
            Undo.DestroyObjectImmediate(g);
        }
    }

    // Nhãn cần TMP Essential Resources (font mặc định); chưa có thì đề nghị import
    static bool EnsureTmp()
    {
        if (Resources.Load<TMP_Settings>("TMP Settings") != null) return true;
        if (EditorUtility.DisplayDialog("Thiếu TMP Essential Resources",
                "Nhãn chữ cần TextMeshPro Essential Resources. Import ngay bây giờ?\n" +
                "Import xong hãy chạy lại menu Heart VR.", "Import", "Hủy"))
            UnityEditor.AssetPackage.Package.Import(TmpEssentials, false);
        return false;
    }
}

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Gắn vào object Visual (con của HeartModel).
// Dùng trong Editor: bấm ⋮ ở mục "Fit Heart To Marker" → "Fit heart 10cm".
// Việc làm: thu phóng đều Visual để tim có kích thước đúng targetHeight,
// rồi dời Visual sao cho tâm tim (X, Z) nằm ở 0 và điểm thấp nhất nằm ở y = 0,
// tất cả tính theo tọa độ local của HeartModel (tức mặt giấy là y = 0).
// Script KHÔNG xoay mô hình: hãy xoay tim theo hướng mong muốn trước khi chạy lệnh.
public class FitHeartToMarker : MonoBehaviour
{
    [SerializeField] float targetHeight = 0.10f; // kích thước mong muốn của tim (mét)

    // Bật: cạnh DÀI NHẤT của tim bằng targetHeight (dùng khi tim nằm áp lên giấy).
    // Tắt: chiều cao theo trục Y bằng targetHeight (dùng khi tim đứng thẳng).
    [SerializeField] bool fitLongestSide = true;

    [ContextMenu("Fit heart 10cm")]
    void FitHeart()
    {
        Transform heartModel = transform.parent;
        if (heartModel == null)
        {
            Debug.LogWarning("[Fit] Visual phải là con của HeartModel.");
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("[Fit] Không tìm thấy Renderer nào bên trong Visual.");
            return;
        }

        // (1) Bounds gộp của tim, theo tọa độ local của HeartModel
        Bounds before = GetBoundsIn(heartModel, renderers);
        Vector3 s = before.size;
        float height = fitLongestSide ? Mathf.Max(s.x, Mathf.Max(s.y, s.z)) : s.y;
        if (height < 1e-6f)
        {
            Debug.LogWarning("[Fit] Chiều cao tim bằng 0, kiểm tra lại mô hình.");
            return;
        }

        Debug.Log($"[Fit] Trước: kích thước {before.size.ToString("F4")} m, " +
                  $"tâm {before.center.ToString("F4")}, đáy y = {before.min.y:F4}");

#if UNITY_EDITOR
        Undo.RecordObject(transform, "Fit heart 10cm"); // cho phép Ctrl+Z
#endif

        // (2) Thu phóng đều quanh gốc của Visual.
        // Phóng k lần quanh điểm P thì mọi điểm x biến thành P + k*(x - P).
        float k = targetHeight / height;
        Vector3 pivot = transform.localPosition;
        transform.localScale *= k;

        Vector3 newCenter = pivot + k * (before.center - pivot);
        float newBottom = pivot.y + k * (before.min.y - pivot.y);

        // (3) Dời Visual: tâm X, Z về 0, đáy về y = 0
        transform.localPosition += new Vector3(-newCenter.x, -newBottom, -newCenter.z);

        Bounds after = GetBoundsIn(heartModel, renderers);
        Debug.Log($"[Fit] Sau: kích thước {after.size.ToString("F4")} m, " +
                  $"tâm {after.center.ToString("F4")}, đáy y = {after.min.y:F4}, " +
                  $"Visual scale {transform.localScale.ToString("F5")}");
    }

    // Gộp 8 góc hộp bao (localBounds) của từng Renderer, đổi sang tọa độ của 'space'
    internal static Bounds GetBoundsIn(Transform space, Renderer[] renderers)
    {
        bool hasPoint = false;
        Bounds result = new Bounds();

        foreach (Renderer r in renderers)
        {
            Bounds lb = r.localBounds;
            Vector3 min = lb.min, max = lb.max;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);

                Vector3 p = space.InverseTransformPoint(r.transform.TransformPoint(corner));

                if (!hasPoint) { result = new Bounds(p, Vector3.zero); hasPoint = true; }
                else result.Encapsulate(p);
            }
        }
        return result;
    }
}

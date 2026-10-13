using System.Collections.Generic;
using UnityEngine;

// Một bộ phận của tim (tâm nhĩ, tâm thất, động mạch...) kèm nhãn tên.
// Cần có Collider (trên chính object này hoặc con của nó) để GazeLabel bắn tia trúng.
// SelectionBase: bấm vào chấm màu (Marker) trong cửa sổ Scene sẽ chọn cả bộ phận này,
// nên kéo đi thì collider và nhãn đi theo, không chỉ riêng quả cầu.
[SelectionBase]
public class HeartPart : MonoBehaviour
{
    [Tooltip("Object nhãn (chữ + nền), nên gắn FaceCamera để luôn quay về người xem")]
    [SerializeField] GameObject label;

    [Tooltip("Tùy chọn: chấm màu đánh dấu bộ phận, được phóng to khi bộ phận đang được chọn")]
    [SerializeField] Transform marker;

    [Tooltip("Chấm màu to gấp mấy lần khi đang hiện nhãn")]
    [SerializeField] float selectedMarkerScale = 1.6f;

    [SerializeField] bool showOnStart = false;

    // Các bộ phận đang bật, để Select() ẩn nhãn của những bộ phận khác
    static readonly List<HeartPart> active = new List<HeartPart>();

    Vector3 markerScale;
    bool markerScaleSaved;

    public bool IsShown => label != null && label.activeSelf;

    void Awake() => Show(showOnStart);

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    // Bật/tắt nhãn của riêng bộ phận này
    public void Toggle() => Show(!IsShown);

    // Chọn bộ phận: hiện nhãn của nó và ẩn nhãn các bộ phận khác; chọn lại thì ẩn
    public void Select()
    {
        bool show = !IsShown;
        if (show)
            foreach (var part in active)
                if (part != this) part.Show(false);
        Show(show);
    }

    public void Show(bool show)
    {
        if (label != null) label.SetActive(show);
        if (marker == null) return;
        // HeartEmerge có thể gọi Show() khi Hotspots còn tắt (chưa chạy Awake) nên lưu kích thước gốc ở lần gọi đầu
        if (!markerScaleSaved)
        {
            markerScale = marker.localScale;
            markerScaleSaved = true;
        }
        marker.localScale = show ? markerScale * selectedMarkerScale : markerScale;
    }
}

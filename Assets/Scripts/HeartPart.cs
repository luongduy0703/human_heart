using UnityEngine;

// Một bộ phận của tim (tâm nhĩ, tâm thất, động mạch...) kèm nhãn tên.
// Cần có Collider (trên chính object này hoặc con của nó) để GazeLabel bắn tia trúng.
public class HeartPart : MonoBehaviour
{
    [Tooltip("Object nhãn (chữ + nền), nên gắn FaceCamera để luôn quay về người xem")]
    [SerializeField] GameObject label;

    [SerializeField] bool showOnStart = false;

    public bool IsShown => label != null && label.activeSelf;

    void Awake() => Show(showOnStart);

    // Bật/tắt nhãn, GazeLabel gọi khi người dùng nhìn đủ lâu
    public void Toggle() => Show(!IsShown);

    public void Show(bool show)
    {
        if (label != null) label.SetActive(show);
    }
}

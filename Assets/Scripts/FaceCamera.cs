using UnityEngine;

// Luôn xoay object (nhãn chữ) về phía camera để đọc được từ mọi góc.
public class FaceCamera : MonoBehaviour
{
    [Tooltip("Để trống thì dùng Camera.main")]
    [SerializeField] Transform target;

    void LateUpdate()
    {
        if (target == null)
        {
            var main = Camera.main;
            if (main == null) return;
            target = main.transform;
        }

        // Hướng trục Z ra xa camera thì chữ (TextMeshPro/TextMesh) mới không bị ngược
        Vector3 dir = transform.position - target.position;
        if (dir.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(dir, target.up);
    }
}

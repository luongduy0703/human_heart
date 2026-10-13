# Trái tim sống — AR trong kính VR

Bài thực hành AR: điện thoại đặt trong kính VR (kiểu Cardboard), nhìn vào một tờ giấy in marker thì **trái tim 3D chui ra dần dần từ mặt giấy**. Khi tim đã ra hết, **nhìn chằm chằm vào một bộ phận tim khoảng 1.5 giây** để bật/tắt nhãn tên của bộ phận đó (chọn bằng ánh nhìn, không cần chạm màn hình).

Project được làm nhẹ để chạy trên điện thoại cấu hình yếu (ví dụ Vsmart Joy): khóa 30 fps, render ở độ phân giải thấp hơn màn hình, không dùng hậu kỳ.

---

## Mục lục

1. [Yêu cầu](#1-yêu-cầu)
2. [Cách hoạt động](#2-cách-hoạt-động)
3. [Cấu trúc thư mục và script](#3-cấu-trúc-thư-mục-và-script)
4. [Cài đặt project (làm một lần)](#4-cài-đặt-project-làm-một-lần)
5. [Chuẩn bị marker](#5-chuẩn-bị-marker)
6. [Dựng scene từng bước](#6-dựng-scene-từng-bước)
7. [Build lên điện thoại](#7-build-lên-điện-thoại)
8. [Sử dụng với kính VR](#8-sử-dụng-với-kính-vr)
9. [Các thông số có thể chỉnh](#9-các-thông-số-có-thể-chỉnh)
10. [Debug bằng log `[DBG]`](#10-debug-bằng-log-dbg)
11. [Xử lý sự cố](#11-xử-lý-sự-cố)
12. [Mẹo cho điện thoại yếu](#12-mẹo-cho-điện-thoại-yếu)

---

## 1. Yêu cầu

| Thành phần | Phiên bản / ghi chú |
|---|---|
| Unity Editor | **6000.6.4f1** (Unity 6.6), có cài module **Android Build Support** (gồm OpenJDK, Android SDK & NDK) |
| Render pipeline | URP 17 (đã có sẵn trong project) |
| AR | **AR Foundation 6.6.2** + **Google ARCore XR Plugin 6.6.2** (đã có trong `Packages/manifest.json`) |
| Input | Chỉ dùng **Input System mới** (Active Input Handling = Input System Package) |
| Điện thoại | Android có hỗ trợ ARCore (xem danh sách thiết bị ARCore của Google) và đã cài **Google Play Services for AR** |
| Kính | Kính VR dạng hộp đặt điện thoại (Cardboard hoặc tương tự), **phải để lộ camera sau** của điện thoại |
| Marker | Tờ giấy in ảnh `Assets/Markers/heart_marker2.jpg` |

> Máy có cài cả Unity 2021.3, **đừng mở project bằng bản đó**.

---

## 2. Cách hoạt động

```
Camera AR thấy marker
        │
        ▼
HeartTracker ── đặt HeartAnchor trùng vị trí marker, bật HeartAnchor
        │
        ▼
HeartEmerge ── HeartModel trồi lên từ dưới mặt giấy và lớn dần (SmoothStep)
        │        phần tim còn dưới mặt giấy bị Occluder (DepthMask) che đi
        ▼
Tim ra hết → bật Hotspots (các bộ phận tim có Collider)
        │
        ▼
GazeLabel ── tia từ giữa camera trúng một HeartPart đủ 1.5 s → HeartPart.Toggle()
        │
        ▼
Nhãn hiện/ẩn, FaceCamera luôn xoay nhãn về phía người xem

HeadsetView (chạy song song): camera AR vẽ vào RenderTexture → hiển thị lên 2 nửa màn hình trái/phải
```

**Mẹo "chui ra từ tờ giấy":** `Occluder` là một mặt phẳng tàng hình nằm đúng mặt giấy. Shader `Custom/DepthMask` **không vẽ màu** (nên vẫn thấy hình camera) nhưng **có ghi độ sâu** và được vẽ **trước** tim (`Queue = Geometry-10`). Vì vậy mọi phần tim nằm dưới mặt phẳng đó bị che, trông như tim đang chui lên từ trong tờ giấy.

**Quy tắc bật/tắt tim** (`HeartTracker`):
- Trạng thái ảnh **Tracking** hoặc **Limited** → coi là đang thấy marker: cập nhật vị trí, giữ tim bật. (Máy yếu hay rơi về Limited dù marker nằm yên trên bàn.)
- Trạng thái **None** hoặc ảnh **bị xóa khỏi danh sách** → tắt tim.
- Khi thấy lại marker mà đã mất dấu **quá 2 giây** → phát lại hoạt ảnh trồi lên từ đầu.

---

## 3. Cấu trúc thư mục và script

```
Assets/
├── Markers/
│   ├── heart_marker2.jpg        ảnh marker để in
│   └── HeartLibrary.asset       Reference Image Library (ảnh tên "heart", 0.196 × 0.257 m)
├── Models/heart.glb             mô hình tim (nhập bằng glTFast)
├── Scenes/SampleScene.unity     scene chính (scene duy nhất trong Build)
├── Scripts/
│   ├── DepthMask.shader         shader URP chỉ ghi depth, không ghi màu
│   ├── DepthMaskMat.mat         material dùng shader trên, gán cho Occluder
│   ├── HeartTracker.cs          theo dõi marker, bật/tắt và đặt vị trí HeartAnchor
│   ├── HeartEmerge.cs           hoạt ảnh tim trồi lên, bật Hotspots khi xong
│   ├── HeadsetView.cs           chế độ kính VR: 2 nửa màn hình, khóa 30 fps
│   ├── GazeLabel.cs             chọn bằng ánh nhìn (dwell 1.5 s)
│   ├── HeartPart.cs             một bộ phận tim + nhãn, có Toggle()
│   ├── FaceCamera.cs            xoay nhãn về phía camera
│   └── FitHeartToMarker.cs      công cụ Editor: chỉnh kích thước và đặt tim lên mặt giấy
└── Settings/                    URP asset cho PC và Mobile
```

| Script | Gắn lên | Việc làm |
|---|---|---|
| `HeartTracker` | `XR Origin` | Nghe sự kiện `trackablesChanged` của `ARTrackedImageManager` (AR Foundation 6), đặt `HeartAnchor` theo marker, báo cho `HeartEmerge` khi thấy lại marker |
| `HeartEmerge` | `HeartAnchor` | Di chuyển `HeartModel` từ dưới mặt giấy lên, phóng to dần; xong thì bật `Hotspots` |
| `HeadsetView` | `Canvas` | Camera AR vẽ vào RenderTexture, hiển thị lên `LeftEye`/`RightEye` (mỗi bên lấy nửa giữa khung hình); khóa 30 fps; không tắt màn hình; tự tạo camera đen để xóa nền |
| `GazeLabel` | `Main Camera` (camera AR) | Bắn tia từ giữa camera; nhìn một `HeartPart` đủ lâu thì gọi `Toggle()`; có thể hiện vòng tiến độ |
| `HeartPart` | Mỗi bộ phận trong `Hotspots` | Giữ tham chiếu tới object nhãn, `Toggle()` / `Show(bool)` |
| `FaceCamera` | Object nhãn | Xoay nhãn về phía `Camera.main` mỗi khung hình |
| `FitHeartToMarker` | `Visual` (con của `HeartModel`) | Chỉ dùng trong Editor, qua menu chuột phải của component |

---

## 4. Cài đặt project (làm một lần)

### Bước 4.1 — Mở project
1. Mở **Unity Hub** → **Add** → chọn thư mục `heart_VR`.
2. Kiểm tra cột Editor Version là **6000.6.4f1**, rồi mở project.

### Bước 4.2 — Chuyển sang Android
1. **File → Build Profiles**.
2. Chọn **Android** → **Switch Platform** (lần đầu chờ khá lâu).
3. Trong **Scene List**, kiểm tra có `Scenes/SampleScene` và đang được tick.

### Bước 4.3 — Kiểm tra package AR
1. **Window → Package Manager** → mục **In Project**.
2. Phải thấy **AR Foundation 6.6.2** và **Google ARCore XR Plugin 6.6.2**. Nếu thiếu, chọn **Unity Registry**, tìm và cài.

### Bước 4.4 — Bật ARCore
1. **Edit → Project Settings → XR Plug-in Management**.
2. Chọn **tab Android** (biểu tượng robot) → tick **Google ARCore**.
3. Mở mục con **Project Validation** → bấm **Fix All** để Unity tự sửa các thiết lập ARCore cần.

### Bước 4.5 — Player Settings (tab Android)
**Edit → Project Settings → Player → Android**:

| Mục | Giá trị |
|---|---|
| Resolution and Presentation → **Default Orientation** | **Landscape Left** |
| Other Settings → **Auto Graphics API** | Bỏ tick, để **OpenGLES3** đứng đầu (xóa Vulkan nếu Project Validation báo lỗi) |
| Other Settings → **Minimum API Level** | Theo yêu cầu của ARCore (Project Validation sẽ báo nếu thấp quá) |
| Other Settings → **Scripting Backend** | **IL2CPP** |
| Other Settings → **Target Architectures** | Tick **ARM64** |
| Other Settings → **Active Input Handling** | **Input System Package (New)** (đã đặt sẵn) |

### Bước 4.6 — URP: thêm AR Background Renderer Feature
Thiếu bước này thì **màn hình đen, không thấy hình camera**.

1. Mở `Assets/Settings/Mobile_Renderer.asset` (và `PC_Renderer.asset` nếu chất lượng đang dùng PC asset).
2. Trong Inspector, kiểm tra danh sách **Renderer Features** đã có **AR Background Renderer Feature** chưa. Nếu chưa: **Add Renderer Feature → AR Background Renderer Feature**.
3. Kiểm tra **Edit → Project Settings → Quality**: mức chất lượng mặc định của Android nên dùng `Mobile_RPAsset`.

### Bước 4.7 — TextMeshPro (cho nhãn chữ)
Lần đầu tạo chữ TextMeshPro, Unity sẽ hỏi nhập tài nguyên: bấm **Import TMP Essentials**. (Hoặc **Window → TextMeshPro → Import TMP Essential Resources**.)

---

## 5. Chuẩn bị marker

1. **In** `Assets/Markers/heart_marker2.jpg` sao cho **chiều rộng ảnh in ra đúng 19.6 cm** (chiều cao khoảng 25.7 cm, vừa giấy A4). Khi in, tắt "Fit to page" / "Scale to fit" để máy in không tự co giãn.
2. Đo lại bằng thước. **Kích thước in phải khớp với kích thước khai báo trong thư viện**, nếu không tim sẽ bị lơ lửng hoặc lún xuống giấy.
3. Nếu in ra kích thước khác: mở `Assets/Markers/HeartLibrary.asset`, sửa **Physical Size** của ảnh `heart` theo số đo thật (đơn vị mét, ví dụ 19.6 cm = `0.196`).
4. Dùng giấy mờ (không bóng), đặt phẳng trên bàn, chỗ đủ sáng, tránh đèn chiếu thẳng gây lóa.

> Muốn đổi sang ảnh marker khác: thêm ảnh vào `Assets/Markers`, trong `HeartLibrary` bấm **Add Image**, kéo ảnh vào, đặt **Name**, tick **Specify Size** và nhập kích thước thật. Ảnh tốt cho ARCore là ảnh nhiều chi tiết, tương phản cao, không lặp họa tiết.

---

## 6. Dựng scene từng bước

`SampleScene` đã dựng sẵn phần lớn. Các bước dưới đây dùng để **kiểm tra lại** từng phần, hoặc dựng lại từ đầu nếu cần.

### Sơ đồ Hierarchy mong muốn

```
AR Session                         (ARSession, ARInputManager)
XR Origin                          (XROrigin, ARTrackedImageManager, HeartTracker)
└── Camera Offset
    └── Main Camera                (Camera, ARCameraManager, ARCameraBackground,
                                    TrackedPoseDriver, GazeLabel)  — tag: MainCamera
HeartAnchor                        (HeartEmerge)
├── Occluder                       (Plane/Quad + DepthMaskMat, KHÔNG có Collider)
└── HeartModel
    ├── Visual                     (mô hình heart.glb, FitHeartToMarker)
    └── Hotspots
        ├── TamNhiPhai             (SphereCollider, HeartPart)
        │   └── Label              (TextMeshPro, FaceCamera)
        ├── TamNhiTrai             ...
        ├── TamThatPhai            ...
        └── TamThatTrai            ...
Canvas                             (Screen Space - Overlay, HeadsetView)
├── LeftEye                        (RawImage)  [└── Reticle (Image, tùy chọn)]
└── RightEye                       (RawImage)  [└── Reticle (Image, tùy chọn)]
EventSystem                        (EventSystem, InputSystemUIInputModule)
Directional Light
Global Volume
```

### Bước 6.1 — AR Session và XR Origin
1. Nếu scene còn `Main Camera` cũ không nằm trong `XR Origin`: xóa nó đi.
2. Chuột phải trong Hierarchy → **XR → AR Session**.
3. Chuột phải → **XR → XR Origin (Mobile AR)**. Camera AR nằm ở `XR Origin/Camera Offset/Main Camera`.
4. Chọn `Main Camera`, kiểm tra **Tag = MainCamera** (`FaceCamera` dùng `Camera.main`).

### Bước 6.2 — Camera AR
Chọn `XR Origin/Camera Offset/Main Camera`:
1. Trong component **AR Camera Manager**, đặt **Render Mode** (Background Rendering Mode) = **Before Opaques**.
   > Nếu để *After Opaques*, hình camera vẽ sau tim và bị Occluder chặn → chỗ Occluder thành **lỗ đen**.
2. Tắt **Post Processing** trên Camera (đỡ tốn cho máy yếu).
3. **Add Component → GazeLabel**:
   - **Cam**: để trống (tự lấy Camera trên cùng object, rồi đến `Camera.main`).
   - **Max Distance** = 3, **Dwell Time** = 1.5.
   - **Layers**: nên chỉ chọn layer chứa Hotspots (xem Bước 6.6).
   - **Reticle Fills**: tùy chọn (xem Bước 6.8).

### Bước 6.3 — Theo dõi marker
Chọn `XR Origin`:
1. **Add Component → AR Tracked Image Manager**:
   - **Serialized Library** = kéo `Assets/Markers/HeartLibrary.asset` vào.
   - **Max Number Of Moving Images** = 1.
   - **Tracked Image Prefab** = **để trống** (không sinh prefab; tim là object có sẵn trong scene).
2. **Add Component → HeartTracker**:
   - **Image Manager** = kéo component `AR Tracked Image Manager` ở trên vào.
   - **Heart Anchor** = kéo `HeartAnchor` **từ cửa sổ Hierarchy** (không kéo từ cửa sổ Project).
   - **Emerge** = kéo `HeartAnchor` vào (Unity tự lấy component `HeartEmerge` trên đó).
   - **Image Name** = `heart` (hoặc để trống để nhận mọi ảnh trong thư viện).
   - **Debug Logs** = bật khi đang thử, tắt khi build bản chính.

> `HeartAnchor` phải **có sẵn trong scene**. Vị trí của nó trong Editor không quan trọng vì lúc chạy sẽ bị dời tới chỗ marker; quan trọng là vị trí các object con so với nó. Khi app chạy, `HeartTracker` tắt `HeartAnchor` cho tới khi thấy marker. Đặt nó ở gốc Hierarchy hoặc làm con của `XR Origin` đều được, **không** đặt làm con của camera.

### Bước 6.4 — HeartAnchor và mặt giấy
Quy ước tọa độ trong `HeartAnchor`: **mặt giấy là y = 0, trục Y hướng lên khỏi tờ giấy**, gốc tọa độ là tâm ảnh marker.

1. Tạo (hoặc chọn) object rỗng `HeartAnchor` ở gốc Hierarchy, Position (0,0,0), Rotation (0,0,0), Scale (1,1,1).
2. **Occluder** — con của `HeartAnchor`:
   - Tạo bằng **3D Object → Plane**. Position (0, 0, 0), Rotation (0, 0, 0), Scale (0.04, 1, 0.04) → mặt phẳng 40 × 40 cm, đủ che vùng quanh marker.
     (Nếu dùng **Quad** thì Rotation X = 90 để mặt quay lên, Scale (0.4, 0.4, 1).)
   - Gán material `Assets/Scripts/DepthMaskMat.mat`.
   - **Xóa component Mesh Collider** của Occluder, để tia nhìn không bị chặn.
   - Occluder **không** được là con của `HeartModel` (nếu không nó sẽ di chuyển theo tim).

### Bước 6.5 — HeartModel, Visual và kích thước tim
1. `HeartModel` là **con trực tiếp** của `HeartAnchor` (bắt buộc, vì `HeartEmerge` di chuyển nó theo trục Y của cha). Đặt Position (0, 0, 0), Rotation (0, 0, 0), Scale (1, 1, 1).
2. Kéo `Assets/Models/heart.glb` vào làm con của `HeartModel`, đổi tên thành `Visual`.
3. Xoay `Visual` theo hướng mong muốn (ví dụ tim đứng thẳng, mặt trước hướng về phía người xem).
4. Chọn `Visual` → **Add Component → FitHeartToMarker**:
   - **Target Height** = 0.10 (tim cao/dài 10 cm; marker rộng 19.6 cm).
   - **Fit Longest Side**: bật nếu tim nằm áp lên giấy (cạnh dài nhất = 10 cm); tắt nếu tim đứng thẳng (chiều cao Y = 10 cm).
5. Bấm nút **⋮** (hoặc chuột phải) trên tiêu đề component **Fit Heart To Marker** → **Fit heart 10cm**.
   - Script thu phóng đều `Visual`, rồi dời để tâm tim (X, Z) nằm ở 0 và **điểm thấp nhất nằm đúng y = 0** (mặt giấy).
   - Console in kích thước trước/sau với tiền tố `[Fit]`. Có thể **Ctrl+Z** để hoàn tác.
   - Script **không xoay** mô hình, nên phải xoay ở bước 3 trước.
6. Trạng thái lúc này của `HeartModel` (vị trí, kích thước) chính là trạng thái **"đã ra hết"**. Lúc chạy, `HeartEmerge` sẽ bắt đầu từ thấp hơn `Sink Depth` mét và nhỏ hơn rồi đi dần tới trạng thái này.

### Bước 6.6 — Hotspots (các bộ phận tim)
1. Tạo object rỗng `Hotspots` làm con của `HeartModel` (để nó lên xuống cùng tim).
2. Tạo layer mới tên `Hotspot` (**Inspector → Layer → Add Layer…**), gán cho `Hotspots` và mọi con của nó (chọn **Yes, change children**).
3. Với mỗi bộ phận (`TamNhiPhai`, `TamNhiTrai`, `TamThatPhai`, `TamThatTrai`, …):
   1. Tạo object rỗng làm con của `Hotspots`, đặt vào đúng chỗ bộ phận đó trên mô hình tim.
   2. **Add Component → Sphere Collider**, chỉnh **Radius** khoảng 0.01–0.02 (1–2 cm) để dễ nhìn trúng. Có thể tick **Is Trigger**.
   3. **Add Component → HeartPart**.
   4. Tạo nhãn: chuột phải vào bộ phận → **3D Object → Text - TextMeshPro**, đặt tên `Label`, nhập tên tiếng Việt (ví dụ "Tâm nhĩ phải"), đặt hơi cao hơn bộ phận. Chữ trong thế giới thật rất nhỏ: chỉnh **Font Size** và Scale cho chữ cao khoảng 1 cm (ví dụ Font Size 1, Scale 0.01).
   5. Trên `Label`: **Add Component → FaceCamera** (để trống ô Target).
   6. Quay lại bộ phận, trong **HeartPart**: kéo `Label` vào ô **Label**. **Show On Start** để tắt.
4. Không cần tắt `Hotspots` bằng tay: `HeartEmerge` tự tắt lúc bắt đầu và chỉ bật khi tim đã ra hết.
5. Quay lại `Main Camera` → `GazeLabel` → **Layers** = chỉ chọn `Hotspot`.

### Bước 6.7 — HeartEmerge
Chọn `HeartAnchor` → **Add Component → HeartEmerge**:
- **Heart Model** = kéo `HeartModel` vào.
- **Hotspots** = kéo `Hotspots` vào.
- **Duration** = 4 (giây trồi lên).
- **Sink Depth** = 0.12 — phải **lớn hơn chiều cao tim** (0.10) để lúc đầu tim bị che hoàn toàn.
- **Start Scale** = 0.3.
- **Replay After Lost** = 2.
- **Debug Logs** = bật khi đang thử.

### Bước 6.8 — Chế độ kính VR (Canvas)
1. Chuột phải → **UI → Canvas**. **Render Mode = Screen Space - Overlay**.
2. Trong Canvas tạo 2 **UI → Raw Image**: `LeftEye` và `RightEye`. Không cần chỉnh vị trí, `HeadsetView` tự chia đôi màn hình.
3. Chọn `Canvas` → **Add Component → HeadsetView**:
   - **Ar Camera** = kéo `Main Camera` (camera AR) vào.
   - **Left Eye** / **Right Eye** = kéo 2 RawImage tương ứng.
   - **Resolution Scale** = 0.6 (giảm xuống 0.4–0.5 nếu máy giật).
   - **Target Fps** = 30.
   - **Auto Layout** = bật.
4. **(Tùy chọn) Vòng tiến độ nhìn:** trong mỗi RawImage tạo **UI → Image** tên `Reticle`, anchor ở giữa (Alt + chọn preset middle-center), kích thước khoảng 40 × 40.
   - **Source Image** = một sprite hình tròn (ví dụ sprite có sẵn `Knob`).
   - **Image Type = Filled**, **Fill Method = Radial 360**, **Fill Amount = 0**.
   - Kéo cả 2 `Reticle` vào ô **Reticle Fills** của `GazeLabel`.
5. Chọn `EventSystem`: nếu Inspector báo đang dùng *Standalone Input Module*, bấm nút **Replace with InputSystemUIInputModule** (project chỉ bật Input System mới, module cũ sẽ báo lỗi).

### Bước 6.9 — Lưu scene
**Ctrl+S**. Kiểm tra Console không có lỗi đỏ (lỗi biên dịch sẽ chặn Build).

---

## 7. Build lên điện thoại

1. Trên điện thoại: bật **Tùy chọn nhà phát triển** (bấm 7 lần vào *Số hiệu bản tạo* trong *Giới thiệu điện thoại*) → bật **Gỡ lỗi USB**.
2. Cài/cập nhật **Google Play Services for AR** từ CH Play.
3. Cắm cáp USB, trên điện thoại chọn **Cho phép gỡ lỗi USB**.
4. Trong Unity: **File → Build Profiles → Android** → **Run Device** chọn đúng máy → **Build And Run**. Đặt tên file `.apk` khi được hỏi.
5. Lần đầu mở app: **cho phép quyền Camera**.
6. Thử trước **không đeo kính**: cầm máy ngang, hướng camera vào marker. Màn hình sẽ chia đôi, tim chui ra từ tờ giấy sau vài giây.

---

## 8. Sử dụng với kính VR

1. Mở app, đặt điện thoại **nằm ngang** vào kính, đường chia giữa 2 hình trùng với vách ngăn giữa 2 thấu kính. Camera sau phải không bị che.
2. Đặt marker phẳng trên bàn, đeo kính, nhìn xuống marker ở khoảng cách 30–50 cm.
3. Chờ tim chui ra hết (mặc định 4 giây).
4. Nhìn thẳng vào một bộ phận tim (tâm điểm là giữa mỗi nửa màn hình). Giữ ánh nhìn **1.5 giây** → nhãn bật. Nhìn ra chỗ khác rồi nhìn lại 1.5 giây → nhãn tắt.
5. Nếu marker bị mất dấu hoàn toàn rồi thấy lại sau hơn 2 giây, tim sẽ chui ra lại từ đầu và các nhãn được tắt hết.

> Hai nửa màn hình hiển thị **cùng một hình** (không phải 3D nổi) và không có hiệu chỉnh méo thấu kính. Nếu thấy mờ, chỉnh tiêu cự / khoảng cách thấu kính của kính.

---

## 9. Các thông số có thể chỉnh

| Script | Thông số | Mặc định | Ý nghĩa |
|---|---|---|---|
| `HeartTracker` | Image Name | `""` | Tên ảnh trong thư viện cần theo dõi; trống = mọi ảnh |
| | Debug Logs | bật | In log `[DBG]` |
| `HeartEmerge` | Duration | 4 | Số giây tim trồi lên |
| | Sink Depth | 0.12 | Độ sâu ban đầu dưới mặt giấy (m), phải > chiều cao tim |
| | Start Scale | 0.3 | Kích thước lúc đầu so với kích thước cuối |
| | Replay After Lost | 2 | Mất dấu lâu hơn số giây này thì phát lại |
| | Debug Logs | bật | In tiến độ mỗi 0.5 giây |
| `GazeLabel` | Max Distance | 3 | Tầm xa tia nhìn (m) |
| | Layers | Everything | Layer tia nhìn được phép trúng |
| | Dwell Time | 1.5 | Số giây phải nhìn để bật/tắt nhãn |
| | Reticle Fills | trống | Các Image hiện tiến độ nhìn |
| `HeadsetView` | Resolution Scale | 0.6 | Độ phân giải RenderTexture so với màn hình |
| | Target Fps | 30 | Khung hình/giây tối đa |
| | Auto Layout | bật | Tự chia đôi màn hình cho 2 RawImage |
| `HeartPart` | Show On Start | tắt | Nhãn hiện sẵn khi bắt đầu |
| `FitHeartToMarker` | Target Height | 0.10 | Kích thước tim mong muốn (m) |
| | Fit Longest Side | bật | Bật: cạnh dài nhất = Target Height; tắt: chiều cao Y = Target Height |

---

## 10. Debug bằng log `[DBG]`

Khi **Debug Logs** bật, `HeartTracker` và `HeartEmerge` in các dòng bắt đầu bằng `[DBG]`:

| Dòng log | Khi nào |
|---|---|
| `[DBG] HeartTracker: nhận ảnh mới 'heart' (Tracking)` | ARCore vừa nhận ra marker |
| `[DBG] HeartTracker: 'heart' trackingState Tracking -> Limited` | Trạng thái theo dõi đổi (chỉ in khi thay đổi) |
| `[DBG] HeartTracker: BẬT HeartAnchor tại (x, y, z) (mất dấu 3.20s)` | Tim được bật |
| `[DBG] HeartTracker: TẮT HeartAnchor tại (x, y, z) (trackingState = None)` | Tim bị tắt, kèm lý do |
| `[DBG] HeartEmerge: progress 40% (1.60/4s), HeartModel localPosition (...)` | Mỗi 0.5 giây khi tim đang trồi lên, và một dòng khi xong |

`GazeLabel` in cảnh báo (một lần) nếu không tìm thấy camera hoặc ô **Reticle Fills** có phần tử trống.

**Xem log trên điện thoại:**
- Trong Unity: **Window → Analysis → Android Logcat** (cài package *Android Logcat* nếu chưa có), gõ `[DBG]` vào ô lọc.
- Hoặc dòng lệnh: `adb logcat -s Unity` rồi tìm `[DBG]`.

---

## 11. Xử lý sự cố

| Hiện tượng | Nguyên nhân thường gặp | Cách sửa |
|---|---|---|
| Màn hình đen, không thấy hình camera | Thiếu **AR Background Renderer Feature**; chưa cấp quyền Camera; chưa cài Google Play Services for AR | Bước 4.6; cấp quyền trong Cài đặt → Ứng dụng; cài từ CH Play |
| Có **lỗ đen** ở vùng quanh marker | Render Mode của AR Camera Manager là *After Opaques* | Đặt **Before Opaques** (Bước 6.2) |
| Thấy cả phần tim dưới mặt giấy | Occluder không nằm ở y = 0, quay mặt xuống dưới, quá nhỏ, hoặc chưa gán `DepthMaskMat` | Kiểm tra Bước 6.4 |
| Tim lơ lửng trên giấy hoặc lún xuống | Kích thước in khác **Physical Size** trong thư viện; chưa chạy **Fit heart 10cm** | Bước 5 và Bước 6.5 |
| Tim không bao giờ xuất hiện | Không có log `nhận ảnh mới` → ARCore không nhận ra marker; có log nhưng **Image Name** sai | Đủ sáng, giấy phẳng, không lóa; đặt Image Name = `heart` hoặc để trống |
| Tim hiện ngay, không có hoạt ảnh | `HeartModel` không phải con trực tiếp của `HeartAnchor`; **Sink Depth** quá nhỏ | Bước 6.5, Bước 6.7 |
| Nhìn mãi không bật nhãn | Tim chưa ra hết (Hotspots còn tắt); bộ phận thiếu Collider; **Layers** của GazeLabel không chứa layer Hotspot; Occluder còn Collider | Bước 6.4, 6.6 |
| Nhãn bị ngược / không quay về người xem | Thiếu `FaceCamera`; camera không có tag `MainCamera` | Bước 6.1, 6.6 |
| Console báo lỗi `InvalidOperationException` về `UnityEngine.Input` | EventSystem dùng Standalone Input Module | Bước 6.8 mục 5 |
| Tim không chui ra lại khi quay đi rồi nhìn lại | Đúng thiết kế: trạng thái Limited vẫn giữ tim bật; chỉ phát lại khi ảnh về None hoặc bị xóa rồi thấy lại sau > 2 s | Giảm/tăng **Replay After Lost** nếu cần |
| Giật, nóng máy | Máy yếu | Xem mục 12 |

---

## 12. Mẹo cho điện thoại yếu

- Giảm **Resolution Scale** của `HeadsetView` xuống 0.4–0.5.
- **Tắt Debug Logs** trên `HeartTracker` và `HeartEmerge` khi build bản chính (ghi log tốn CPU).
- Dùng `Mobile_RPAsset`: tắt **HDR**, tắt **Shadows** (hoặc tắt *Cast Shadows* trên Directional Light), **Anti-aliasing (MSAA) = Disabled**.
- Tắt **Post Processing** trên camera, có thể xóa `Global Volume`.
- Nếu `heart.glb` nhiều polygon, giảm lưới bằng phần mềm 3D (Blender: *Decimate*) trước khi nhập.
- Giữ **Max Number Of Moving Images** = 1 và chỉ để 1 ảnh trong `HeartLibrary`.

---

### Quản lý phiên bản

Project dùng **Unity Version Control (Plastic SCM)**, không dùng git. Các thư mục `Library/`, `Temp/`, `Logs/`, `obj/` do Unity tự sinh và đã bị bỏ qua (`ignore.conf`). Khi thêm hoặc di chuyển asset, luôn giữ file `.meta` đi kèm.

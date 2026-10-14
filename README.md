# Trái tim sống 

Bài thực hành: điện thoại nhìn vào một tờ giấy in marker thì **trái tim 3D chui ra dần dần từ mặt giấy**. Khi tim đã ra hết, **nhìn chằm chằm vào một bộ phận tim khoảng 1.5 giây** để bật/tắt nhãn tên của bộ phận đó (chọn bằng ánh nhìn). Khi cầm máy trên tay: **kéo một ngón để xoay tim**, **chụm/mở hai ngón để thu nhỏ/phóng to**, **chạm vào chấm màu (hotspot)** trên tim để hiện tên bộ phận đó (chỉ nhãn của bộ phận đang chọn hiện, nhãn khác tự ẩn). Có 12 bộ phận, nhãn ghi bằng tiếng Anh: Right/Left atrium, Right/Left ventricle, Aorta, Pulmonary trunk, Superior/Inferior vena cava, Pulmonary veins, Right/Left coronary artery, Apex.

Project được làm nhẹ để chạy trên điện thoại cấu hình yếu (ví dụ Vsmart Joy): khóa 30 fps, render ở độ phân giải thấp hơn màn hình, không dùng hậu kỳ.

// Kết quả mong đợi: xuất hiện quả tim có hiệu ứng chui ra từ trong giấy
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
13. [Phiên bản web chạy bằng webcam laptop](#13-phiên-bản-web-chạy-bằng-webcam-laptop)

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


---

## 2. Cách hoạt động

```
Camera AR thấy marker
        │
        ▼
HeartTracker ── đặt HeartAnchor theo marker (làm mượt), bật HeartAnchor
        │
        ▼
HeartEmerge ── HeartModel trồi lên từ dưới mặt giấy và lớn dần (SmoothStep)
        │        phần tim còn dưới mặt giấy bị Occluder (DepthMask) che đi
        ▼
Tim ra hết → bật Hotspots (các bộ phận tim có Collider)
        │
        ▼
GazeLabel ── đeo kính: tia từ giữa camera trúng một HeartPart đủ 1.5 s; cầm tay: chạm vào nó → HeartPart.Toggle()
        │
        ▼
Nhãn hiện/ẩn, FaceCamera luôn xoay nhãn về phía người xem

HeartInteraction (cầm tay): kéo một ngón → xoay tim, chụm hai ngón → zoom (xoay/zoom InteractPivot trong HeartModel)

HeadsetView (chạy song song): camera AR vẽ ảnh camera thật vào RenderTexture, hai camera mắt lệch nhau
            vẽ tim → hiển thị lên 2 nửa màn hình trái/phải (có chiều sâu 3D, bù méo thấu kính)
```

**Mẹo "chui ra từ tờ giấy":** `Occluder` là một mặt phẳng tàng hình nằm đúng mặt giấy. Shader `Custom/DepthMask` **không vẽ màu** (nên vẫn thấy hình camera) nhưng **có ghi độ sâu** và được vẽ **trước** tim (`Queue = Geometry-10`). Vì vậy mọi phần tim nằm dưới mặt phẳng đó bị che, trông như tim đang chui lên từ trong tờ giấy.

**Quy tắc bật/tắt tim** (`HeartTracker`):
- Trạng thái ảnh **Tracking** → đang thấy marker: bật tim, cập nhật vị trí. Tim không nhảy thẳng tới vị trí mới mà trượt dần tới đó (hệ số `1 - e^(-Smoothing·dt)`) để không bị rung.
- Trạng thái **Limited** → giữ tim đứng yên ở vị trí cũ, không cập nhật. ARCore gần như không bao giờ trả về None khi marker ra khỏi khung hình mà giữ Limited mãi, nên nếu Limited kéo dài quá **Limited Timeout** (1 giây) thì coi là mất dấu và tắt tim. Chớp Limited ngắn (máy yếu hay bị) không làm tắt tim.
- Trạng thái **None** hoặc ảnh **bị xóa khỏi danh sách** → tắt tim.
- Khi thấy lại marker mà đã mất dấu **quá 2 giây** → phát lại hoạt ảnh trồi lên từ đầu.

---

## 3. Cấu trúc thư mục và script

```
heart_VR/                              thư mục gốc của repo
├── Assets/                            project Unity (bản điện thoại)
│   ├── Markers/
│   │   ├── heart_marker2.jpg          ảnh marker để in (bản web dùng chung)
│   │   └── HeartLibrary.asset         Reference Image Library (ảnh tên "heart", 0.196 × 0.257 m)
│   ├── Materials/Hotspots/            material màu của 12 chấm hotspot (menu Heart VR tạo)
│   ├── Models/heart.glb               mô hình tim, nhập bằng glTFast (bản web dùng chung)
│   ├── Scenes/SampleScene.unity       scene chính (scene duy nhất trong Build)
│   ├── Scripts/
│   │   ├── DepthMask.shader           shader URP chỉ ghi depth, không ghi màu
│   │   ├── DepthMaskMat.mat           material dùng shader trên, gán cho Occluder
│   │   ├── HeartTracker.cs            theo dõi marker, bật/tắt và đặt vị trí HeartAnchor
│   │   ├── HeartEmerge.cs             hoạt ảnh tim trồi lên, bật Hotspots khi xong
│   │   ├── HeartInteraction.cs        cầm tay: kéo để xoay, chụm hai ngón để zoom
│   │   ├── HeadsetView.cs             chế độ kính VR: 2 nửa màn hình, stereo, bù méo, khóa 30 fps
│   │   ├── LensDistortUI.shader       shader UI méo thùng để bù méo thấu kính (HeadsetView dùng)
│   │   ├── GazeLabel.cs               chọn bộ phận bằng ánh nhìn (dwell 1.5 s) hoặc chạm
│   │   ├── HeartPart.cs               một bộ phận tim + chấm màu + nhãn
│   │   ├── FaceCamera.cs              xoay nhãn về phía camera
│   │   ├── FitHeartToMarker.cs        công cụ Editor: chỉnh kích thước và đặt tim lên mặt giấy
│   │   └── Editor/
│   │       └── HeartSceneSetup.cs     menu Heart VR: dựng 12 hotspot, đặt sát bề mặt tim
│   ├── Settings/                      URP asset cho PC và Mobile, Build Profile Android
│   ├── TextMesh Pro/                  font và shader cho nhãn chữ (TMP Essential Resources)
│   ├── XR/                            cài đặt ARCore và XR Plug-in Management
│   └── Prefabs/, TutorialInfo/, Readme.asset   còn lại từ template, không dùng
├── Packages/                          danh sách package Unity (manifest.json)
├── ProjectSettings/                   cài đặt project Unity
├── web/                               bản chạy trên trình duyệt bằng webcam laptop (mục 13)
│   ├── index.html                     giao diện; nạp three.js và MindAR từ CDN
│   ├── main.js                        logic AR: nhận diện marker, trồi lên, xoay/zoom, hotspot
│   ├── heart-data.js                  vị trí đặt tim và 12 hotspot
│   ├── start.bat                      chạy web server và mở trình duyệt
│   └── README.md                      hướng dẫn chạy bản web
├── file .glb/heart.glb                bản sao mô hình tim, project không dùng
├── README.md                          tài liệu này
├── .gitignore, ignore.conf            danh sách file bỏ qua cho Git / Unity Version Control
└── .nojekyll                          để GitHub Pages đưa file lên nguyên trạng
```

**Script của bản Unity (điện thoại):**

| Script | Gắn lên | Việc làm |
|---|---|---|
| `HeartTracker` | `XR Origin` | Nghe sự kiện `trackablesChanged` của `ARTrackedImageManager` (AR Foundation 6), đặt `HeartAnchor` theo marker (làm mượt), tắt khi Limited quá lâu, báo cho `HeartEmerge` khi thấy lại marker |
| `HeartEmerge` | `HeartAnchor` | Di chuyển `HeartModel` từ dưới mặt giấy lên, phóng to dần; xong thì bật `Hotspots` |
| `HeartInteraction` | `HeartAnchor` | Cầm tay: kéo một ngón để xoay, chụm/mở hai ngón để zoom (Editor: kéo chuột trái, cuộn chuột). Lúc chạy tự tạo `InteractPivot` ở tâm tim và chuyển `Visual` + `Hotspots` vào đó; tắt khi đeo kính; trả về góc nhìn gốc khi tim trồi lên lại |
| `HeadsetView` | `Canvas` | Camera AR vẽ ảnh camera thật vào RenderTexture, hiển thị lên `LeftEye`/`RightEye` (mỗi bên lấy nửa giữa khung hình); bật **Stereo** thì tự tạo 2 camera mắt lệch nhau **Ipd** để vẽ tim (có chiều sâu); bù méo thấu kính; khóa màn hình ngang, 30 fps, không tắt màn hình; tự tạo camera đen để xóa nền |
| `GazeLabel` | `Main Camera` (camera AR) | Bắn tia từ giữa camera; nhìn một `HeartPart` đủ lâu thì chọn nó (mặc định chỉ khi đeo kính). Chạm màn hình cũng chọn: cầm tay thì chạm vào bộ phận, đeo kính thì chạm là nút bấm và chọn thứ đang nhìn. Chỉ tính là chạm khi nhấn-thả nhanh, không kéo, một ngón. **Only One Label** bật thì mỗi lúc chỉ một nhãn; có thể hiện vòng tiến độ |
| `HeartPart` | Mỗi bộ phận trong `Hotspots` | Giữ tham chiếu tới nhãn (`Label`) và chấm màu (`Marker`). `Select()` hiện nhãn bộ phận này, ẩn nhãn các bộ phận khác và phóng to chấm đang chọn; `Toggle()` / `Show(bool)` bật/tắt riêng. Bấm chấm màu trong cửa sổ Scene sẽ chọn cả bộ phận |
| `FaceCamera` | Object nhãn | Xoay nhãn về phía `Camera.main` mỗi khung hình |
| `FitHeartToMarker` | `Visual` (con của `HeartModel`) | Chỉ dùng trong Editor, qua menu chuột phải của component |
| `HeartSceneSetup` | (không gắn; menu **Heart VR**) | Chỉ dùng trong Editor: dựng 12 hotspot ở vị trí đo sẵn, dò bề mặt tim để đặt chấm sát mặt, xem Bước 6.6 |
| `DepthMask.shader` | `DepthMaskMat` trên `Occluder` | Chỉ ghi depth ở mặt giấy để che phần tim còn nằm dưới giấy |
| `LensDistortUI.shader` | RawImage của `HeadsetView` | Méo thùng hình trước để bù méo của thấu kính kính VR |

**File của bản web (thư mục `web/`):**

| File | Việc làm |
|---|---|
| `index.html` | Trang web: khung AR (video webcam + canvas 3D), nhãn tên, bảng hướng dẫn, nút **Đặt lại góc nhìn**, thông báo lỗi. Importmap nạp three.js 0.160.0 và MindAR 1.2.5 từ CDN |
| `main.js` | `loadTargets()` chuyển ảnh marker sang dữ liệu của MindAR (lần đầu) rồi lưu trong trình duyệt. `buildScene()` đặt tim, occluder và 12 hotspot lên marker, xử lý tìm thấy/mất dấu marker và hoạt ảnh trồi lên. `setupInput()` xử lý kéo xoay, lăn/chụm zoom và bấm chọn hotspot |
| `heart-data.js` | Ma trận đặt lưới tim lên marker, tâm tim, vị trí/tên/màu 12 hotspot (đổi từ bảng `Parts` của `HeartSceneSetup`) |
| `start.bat` | Chạy web server Python ở thư mục gốc repo (để đọc được `Assets/`) rồi mở trình duyệt |
| `README.md` | Hướng dẫn chạy và sử dụng bản web |

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
| Other Settings → **Active Input Handling** | **Input System Package (New)** (không để *Both*: Android không hỗ trợ, Unity sẽ hỏi lúc build; đổi xong Unity tự khởi động lại) |

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
XR Origin                          (XROrigin, ARTrackedImageManager, HeartTracker) — KHÔNG gắn GazeLabel ở đây
└── Camera Offset
    └── Main Camera                (Camera, ARCameraManager, ARCameraBackground,
                                    TrackedPoseDriver, GazeLabel)  — tag: MainCamera
HeartAnchor                        (HeartEmerge, HeartInteraction)
├── Occluder                       (Plane/Quad + DepthMaskMat, KHÔNG có Collider)
└── HeartModel
    ├── Visual                     (mô hình heart.glb, FitHeartToMarker)
    └── Hotspots
        ├── TamNhiPhai             (SphereCollider bán kính ~1.5 cm, HeartPart)
        │   ├── Marker             (Sphere nhỏ có màu, không Collider)
        │   └── Label              (TextMeshPro, FaceCamera, scale 0.01)
        ├── TamNhiTrai             ...
        ├── TamThatPhai            ...
        ├── TamThatTrai            ...
        ├── DongMachChu            ... (động mạch chủ)
        ├── DongMachPhoi           ... (thân động mạch phổi)
        ├── TinhMachChuTren        ... (tĩnh mạch chủ trên)
        ├── TinhMachChuDuoi        ... (tĩnh mạch chủ dưới)
        ├── TinhMachPhoi           ... (tĩnh mạch phổi)
        ├── DongMachVanhPhai       ... (động mạch vành phải)
        ├── DongMachVanhTrai       ... (động mạch vành trái)
        └── MomTim                 ... (mỏm tim)
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
   - **Max Distance** = 3, **Dwell Time** = 1.5, **Tap To Select** = bật.
   - **Layers**: nên chỉ chọn layer chứa Hotspots (xem Bước 6.6).
   - **Reticle Fills**: tùy chọn (xem Bước 6.8).
   - Chỉ để **một** `GazeLabel` trong scene, trên camera. Nếu `XR Origin` cũng có `GazeLabel` thì xóa đi (menu **Heart VR → Thiết lập Hotspots** tự xóa).

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
   - **Limited Timeout** = 1 (giây ở Limited liên tục trước khi coi là mất dấu).
   - **Smoothing** = 12 (càng lớn càng bám sát marker nhưng dễ rung).
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
**Cách nhanh:** sau khi đã tạo `Hotspots` và các bộ phận có `HeartPart` (mục 1–3.3 dưới đây) và gán `Hotspots` vào `HeartEmerge` (Bước 6.7), chọn menu **Heart VR → Thiết lập Hotspots**. Công cụ sẽ:
- đặt `Hotspots` về Position (0,0,0), Rotation 0, Scale 1;
- tạo thêm các bộ phận còn thiếu trong danh sách 12 bộ phận ở sơ đồ Hierarchy trên;
- với mỗi bộ phận: Scale 1, **Sphere Collider** bán kính 1.5 cm, thêm sphere nhỏ có màu tên `Marker` (material trong `Assets/Materials/Hotspots/`), tạo nhãn `Label` bằng TextMeshPro 3D (tên tiếng Anh, ví dụ "Right atrium", gắn `FaceCamera`, scale 0.01) và kéo sẵn vào ô **Label** và **Marker** của `HeartPart`;
- bộ phận mới hoặc đang nằm ngoài tim được đặt vào **vị trí đo sẵn trên `heart.glb`** (tâm nhĩ trên hai tiểu nhĩ, động mạch vành trái trên rãnh liên thất trước, động mạch chủ trên cung động mạch chủ…; các điểm cách nhau ≥ 3 cm), rồi tự dò bề mặt lưới để chấm nằm sát mặt tim. Dùng **Heart VR → Thiết lập Hotspots (đặt lại vị trí)** để đặt lại **tất cả** 12 điểm theo vị trí đo sẵn (mất các chỉnh tay trước đó). Đổi sang mô hình tim khác thì phải đo và sửa lại bảng `Parts` trong `HeartSceneSetup.cs`;
- xóa `GazeLabel` thừa không nằm trên camera (ví dụ trên `XR Origin`);
- gắn `HeartInteraction` lên `HeartAnchor` nếu chưa có.

Sau đó kiểm tra các điểm, chỉnh thêm nếu muốn (trong phạm vi khoảng ±0.1 m quanh tim) và **Ctrl+S**:
- Bấm vào chấm màu trong cửa sổ Scene (Unity chọn cả bộ phận, Inspector phải hiện **Heart Part**), nhấn **W**, kéo mũi tên đỏ/xanh dương để dời trên mặt giấy, mũi tên xanh lá để chỉnh độ cao.
- Nhìn từ trên (**Top**) để kéo ngang tới đúng chỗ, rồi chọn menu **Heart VR → Đặt các điểm sát bề mặt tim** để độ cao tự khớp bề mặt (đang chọn điểm nào thì chỉ chỉnh điểm đó, không chọn gì thì chỉnh tất cả). Không cần tự chỉnh mũi tên xanh lá. **Alt + kéo chuột trái** để xoay góc nhìn, cuộn chuột để zoom, **F** để zoom tới object đang chọn.
- Nhãn `Label` được ẩn sẵn trong Editor cho đỡ rối (lúc chạy app tự hiện khi chọn); muốn xem thì tick ô cạnh tên `Label` trong Inspector, xem xong bỏ tick.

Chạy lại menu không làm mất vị trí đã chỉnh (trừ lệnh "đặt lại vị trí"); `Marker` và `Label` được tạo lại. Có thể **Ctrl+Z** để hoàn tác. Nếu chưa có TMP Essential Resources, công cụ sẽ hỏi import; import xong chạy lại menu.

**Làm bằng tay:**
1. Tạo object rỗng `Hotspots` làm con của `HeartModel` (để nó lên xuống cùng tim), Position (0,0,0).
2. Tạo layer mới tên `Hotspot` (**Inspector → Layer → Add Layer…**), gán cho `Hotspots` và mọi con của nó (chọn **Yes, change children**).
3. Với mỗi bộ phận (`TamNhiPhai`, `TamNhiTrai`, `TamThatPhai`, `TamThatTrai`, …):
   1. Tạo object rỗng làm con của `Hotspots`, đặt vào đúng chỗ bộ phận đó trên mô hình tim.
   2. **Add Component → Sphere Collider**, chỉnh **Radius** khoảng 0.015–0.02 (1.5–2 cm) để dễ nhìn trúng. Có thể tick **Is Trigger**. Scale của bộ phận để (1,1,1).
   3. **Add Component → HeartPart**.
   4. (Tùy chọn) Thêm một **3D Object → Sphere** nhỏ làm con (Scale 0.012, xóa Collider của nó), gán material có màu, để người xem biết chỗ nào chọn được.
   5. Tạo nhãn: chuột phải vào bộ phận → **3D Object → Text - TextMeshPro**, đặt tên `Label`, nhập tên bộ phận (ví dụ "Right atrium"), đặt hơi cao hơn bộ phận. Chữ trong thế giới thật rất nhỏ: Scale 0.01 và **Font Size** khoảng 14 thì chữ cao khoảng 1.4 cm.
   6. Trên `Label`: **Add Component → FaceCamera** (để trống ô Target).
   7. Quay lại bộ phận, trong **HeartPart**: kéo `Label` vào ô **Label**, kéo sphere màu (nếu có) vào ô **Marker**. **Show On Start** để tắt.
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

Tiếp tục trên `HeartAnchor` → **Add Component → HeartInteraction** (menu **Heart VR → Thiết lập Hotspots** tự gắn). Không cần kéo gì vào: script lấy `HeartModel` và `Hotspots` từ `HeartEmerge`. Lúc chạy, nó tạo object `InteractPivot` ở tâm tim (con của `HeartModel`) và chuyển `Visual` + `Hotspots` vào đó, nên trong Hierarchy lúc Play sẽ thấy cấp này; trong Editor (không Play) cấu trúc giữ nguyên.

### Bước 6.8 — Chế độ kính VR (Canvas)
1. Chuột phải → **UI → Canvas**. **Render Mode = Screen Space - Overlay**.
2. Trong Canvas tạo 2 **UI → Raw Image**: `LeftEye` và `RightEye`. Không cần chỉnh vị trí, `HeadsetView` tự chia đôi màn hình.
3. Chọn `Canvas` → **Add Component → HeadsetView**:
   - **Ar Camera** = kéo `Main Camera` (camera AR) vào.
   - **Left Eye** / **Right Eye** = kéo 2 RawImage tương ứng.
   - **Resolution Scale** = 0.6 (giảm xuống 0.4–0.5 nếu máy giật).
   - **Target Fps** = 30.
   - **Auto Layout** = bật.
   - **Stereo** = bật (tim có chiều sâu 3D; tắt nếu máy quá yếu), **Ipd** = 0.064.
   - **Lens Correction** = bật, **K1** = 0.22, **K2** = 0.24 (giá trị của Cardboard v1; chỉnh theo kính thật: đường thẳng ở mép hình cong phình ra thì giảm, cong thắt vào thì tăng).
   - **Lens Shader** = kéo `Assets/Scripts/LensDistortUI.shader` vào (cần khi build, nếu không shader có thể bị loại khỏi bản build).
   - `HeadsetView` tự khóa màn hình **ngang (Landscape Left)**, vì cách cắt nửa giữa khung hình chỉ đúng khi máy nằm ngang.
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
4. Nhìn thẳng vào một bộ phận tim (tâm điểm là giữa mỗi nửa màn hình). Giữ ánh nhìn **1.5 giây** → nhãn bật. Nhìn ra chỗ khác rồi nhìn lại 1.5 giây → nhãn tắt. Kính có nút bấm (chạm màn hình) thì bấm nút để bật/tắt ngay bộ phận đang nhìn.
5. Nếu marker ra khỏi khung hình quá 1 giây, tim tắt; thấy lại sau hơn 2 giây thì tim chui ra lại từ đầu và các nhãn được tắt hết.

> Với **Stereo** bật, hai mắt thấy tim từ hai góc lệch nhau nên tim nổi 3D. Ảnh camera thật phía sau vẫn là **một ảnh chung** cho hai mắt (điện thoại chỉ có một camera), nên tờ giấy trông phẳng. Hình được méo thùng trước để bù méo của thấu kính; nếu mép hình vẫn cong, chỉnh **K1**/**K2**. Nếu thấy mờ, chỉnh tiêu cự / khoảng cách thấu kính của kính.

**Không đeo kính (cầm tay):** tắt object `Canvas` (khi đó `HeadsetView` không chạy, màn hình hiện hình camera AR bình thường). Khi tim đã hiện:
- **Kéo một ngón** trên màn hình: kéo ngang xoay tim quanh trục vuông góc tờ giấy, kéo dọc lật tim tới/lui.
- **Chụm / mở hai ngón**: thu nhỏ / phóng to (0.5×–3×). Đáy tim luôn giữ trên mặt giấy.
- **Chạm nhanh vào chấm màu** (hotspot) trên tim: hiện tên bộ phận đó và chấm màu to lên; nhãn của bộ phận đang hiện trước đó tự ẩn. Chạm lại bộ phận đang chọn để ẩn. Các chấm chỉ bấm được sau khi tim đã trồi lên hết. Muốn hiện được nhiều nhãn cùng lúc thì tắt **Only One Label** của `GazeLabel`.
- Khi tim trồi lên lại (mất dấu marker quá 2 giây), góc xoay và độ zoom trở về ban đầu.
- Nhìn chờ 1.5 giây mặc định chỉ dùng khi đeo kính (bật **Dwell In Handheld** của `GazeLabel` nếu muốn dùng cả khi cầm tay).

---

## 9. Các thông số có thể chỉnh

| Script | Thông số | Mặc định | Ý nghĩa |
|---|---|---|---|
| `HeartTracker` | Image Name | `""` | Tên ảnh trong thư viện cần theo dõi; trống = mọi ảnh |
| | Limited Timeout | 1 | Số giây ở Limited liên tục trước khi coi là mất dấu |
| | Smoothing | 12 | Độ nhanh bám theo marker (1/giây); lớn = bám sát nhưng rung |
| | Debug Logs | bật | In log `[DBG]` |
| `HeartEmerge` | Duration | 4 | Số giây tim trồi lên |
| | Sink Depth | 0.12 | Độ sâu ban đầu dưới mặt giấy (m), phải > chiều cao tim |
| | Start Scale | 0.3 | Kích thước lúc đầu so với kích thước cuối |
| | Replay After Lost | 2 | Mất dấu lâu hơn số giây này thì phát lại |
| | Debug Logs | bật | In tiến độ mỗi 0.5 giây |
| `HeartInteraction` | Degrees Per Screen | 270 | Số độ xoay khi kéo hết một chiều rộng màn hình |
| | Allow Tilt | bật | Kéo dọc để lật tim; tắt thì chỉ xoay ngang |
| | Min Scale / Max Scale | 0.5 / 3 | Giới hạn zoom |
| | Drag Threshold Pixels | 20 | Ngón tay đi quá mức này mới tính là kéo |
| | Scroll Zoom Step | 0.1 | Mỗi nấc cuộn chuột zoom bao nhiêu (Editor) |
| `GazeLabel` | Max Distance | 3 | Tầm xa tia nhìn (m) |
| | Layers | Everything | Layer tia nhìn được phép trúng |
| | Dwell Time | 1.5 | Số giây phải nhìn để bật/tắt nhãn |
| | Dwell In Handheld | tắt | Dùng nhìn chờ cả khi cầm tay (không đeo kính) |
| | Only One Label | bật | Chọn một bộ phận thì chỉ nhãn của nó hiện, nhãn khác tự ẩn; tắt = mỗi bộ phận bật/tắt riêng |
| | Tap To Select | bật | Chạm màn hình / click chuột để bật/tắt nhãn |
| | Tap Max Seconds | 0.35 | Nhấn lâu hơn mức này thì không tính là chạm |
| | Tap Max Move Pixels | 20 | Ngón tay đi xa hơn mức này thì là kéo, không phải chạm |
| | Reticle Fills | trống | Các Image hiện tiến độ nhìn |
| `HeadsetView` | Resolution Scale | 0.6 | Độ phân giải RenderTexture so với màn hình |
| | Target Fps | 30 | Khung hình/giây tối đa |
| | Auto Layout | bật | Tự chia đôi màn hình cho 2 RawImage |
| | Stereo | bật | Hai camera mắt vẽ tim để có chiều sâu (tốn thêm một lần vẽ tim) |
| | Ipd | 0.064 | Khoảng cách hai mắt (m) |
| | Lens Correction | bật | Bù méo thấu kính |
| | K1 / K2 | 0.22 / 0.24 | Hệ số méo thùng |
| `HeartPart` | Show On Start | tắt | Nhãn hiện sẵn khi bắt đầu |
| | Marker | (menu tự gán) | Chấm màu của bộ phận, được phóng to khi đang chọn |
| | Selected Marker Scale | 1.6 | Chấm màu to gấp mấy lần khi đang chọn |
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
| `[DBG] HeartTracker: TẮT HeartAnchor tại (x, y, z) (Limited quá 1.0s)` | Tim bị tắt, kèm lý do (`Limited quá …`, `trackingState = None`, `ảnh bị xóa khỏi danh sách`) |
| `[DBG] HeartEmerge: progress 40% (1.60/4s), HeartModel localPosition (...)` | Mỗi 0.5 giây khi tim đang trồi lên, và một dòng khi xong |

`GazeLabel` in cảnh báo (một lần) nếu không tìm thấy camera hoặc ô **Reticle Fills** có phần tử trống. `HeadsetView` báo lỗi và tự tắt nếu thiếu **Ar Camera** / **Left Eye** / **Right Eye**.

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
| Lúc build hiện hộp thoại *Input Handling on Android* ("Both" is not supported) | **Active Input Handling** đang là *Both* | Bấm **Cancel Build**, đặt **Input System Package (New)** (Bước 4.5), cho Unity khởi động lại rồi build lại |
| Tim không chui ra lại khi quay đi rồi nhìn lại | Mất dấu chưa quá **Replay After Lost** (2 s, tính từ lần cuối ở trạng thái Tracking) | Giảm/tăng **Replay After Lost** nếu cần |
| Tim tắt/bật liên tục dù marker vẫn trong khung hình | Máy yếu rơi về Limited lâu hơn **Limited Timeout** | Tăng **Limited Timeout** (1.5–2 s); thêm ánh sáng |
| Tim rung | **Smoothing** quá lớn | Giảm **Smoothing** (8–10) |
| Tim trễ, trượt theo khi di chuyển máy | **Smoothing** quá nhỏ | Tăng **Smoothing** |
| Chạm vào chấm màu không hiện tên | Tim chưa trồi lên hết (Hotspots còn tắt); chạm hơi lâu hoặc trượt ngón (bị tính là kéo); `HeartPart` chưa gán **Label**; **Layers** của `GazeLabel` không chứa layer của Hotspots | Chạm nhanh, gọn; chạy lại **Heart VR → Thiết lập Hotspots**; tăng **Tap Max Seconds** / **Tap Max Move Pixels** |
| Kéo/chụm không xoay, không zoom | Thiếu `HeartInteraction` trên `HeartAnchor`; đang bật chế độ kính (`Canvas` đang bật) | Bước 6.7; tắt `Canvas` khi cầm tay |
| Đeo kính thấy hai hình tim không chập lại | **Ipd** không khớp kính / mắt | Chỉnh **Ipd**, hoặc tắt **Stereo** |
| Giật, nóng máy | Máy yếu | Xem mục 12 |

---

## 12. Mẹo cho điện thoại yếu

- Giảm **Resolution Scale** của `HeadsetView` xuống 0.4–0.5.
- Nếu vẫn giật: tắt **Stereo** của `HeadsetView` (tim chỉ vẽ một lần).
- **Tắt Debug Logs** trên `HeartTracker` và `HeartEmerge` khi build bản chính (ghi log tốn CPU).
- Dùng `Mobile_RPAsset`: tắt **HDR**, tắt **Shadows** (hoặc tắt *Cast Shadows* trên Directional Light), **Anti-aliasing (MSAA) = Disabled**.
- Tắt **Post Processing** trên camera, có thể xóa `Global Volume`.
- Nếu `heart.glb` nhiều polygon, giảm lưới bằng phần mềm 3D (Blender: *Decimate*) trước khi nhập.
- Giữ **Max Number Of Moving Images** = 1 và chỉ để 1 ảnh trong `HeartLibrary`.

---

## 13. Phiên bản web chạy bằng webcam laptop

Thư mục `web/` (cạnh `Assets/`) là bản chạy trên trình duyệt Chrome/Edge bằng webcam laptop, viết bằng MindAR + three.js. Bản này có cùng hiệu ứng tim trồi lên từ tờ giấy và cùng 12 hotspot; kéo chuột để xoay, lăn chuột để zoom, bấm chấm màu để hiện tên. Nó dùng chung `Assets/Models/heart.glb` và ảnh marker với bản Unity, còn Unity bỏ qua thư mục `web/` nên bản điện thoại không bị ảnh hưởng.

Cách chạy nhanh trên Windows: nháy đúp `web/start.bat` (cần Python 3), cho phép camera, rồi giơ tờ giấy in marker vào giữa khung hình webcam. Nếu bật GitHub Pages cho repo, trang còn chạy online tại https://luongduy0703.github.io/human_heart_vr/web/ mà không cần cài gì. Hướng dẫn đầy đủ (Windows, macOS/Linux, GitHub Pages) xem [web/README.md](web/README.md).

---

### Quản lý phiên bản

Project dùng **Unity Version Control (Plastic SCM)**, không dùng git. Các thư mục `Library/`, `Temp/`, `Logs/`, `obj/` do Unity tự sinh và đã bị bỏ qua (`ignore.conf`). Khi thêm hoặc di chuyển asset, luôn giữ file `.meta` đi kèm.

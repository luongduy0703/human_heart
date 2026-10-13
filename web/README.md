# Trái tim AR trên web (webcam laptop)

Phiên bản chạy trên trình duyệt của project: webcam laptop nhìn vào tờ giấy in marker thì **trái tim 3D trồi lên từ mặt giấy**. Kéo chuột để xoay tim, lăn chuột để phóng to/thu nhỏ, bấm vào chấm màu để xem tên bộ phận.

Trang dùng chung mô hình `Assets/Models/heart.glb` và ảnh marker `Assets/Markers/heart_marker2.jpg` với bản Unity trên điện thoại. Unity bỏ qua thư mục `web/` (nằm ngoài `Assets/`), nên thư mục này không ảnh hưởng gì đến bản Unity.

## Hướng dẫn chạy

Cần có:
- Laptop có webcam, trình duyệt **Chrome** hoặc **Edge** bản mới.
- **Có Internet**: thư viện three.js và MindAR được tải từ CDN jsDelivr.
- **Marker**: in ảnh `Assets/Markers/heart_marker2.jpg` (cỡ nào cũng được, nên in A4; tim tự to nhỏ theo tờ giấy). Chưa in thì mở ảnh này trên điện thoại, chỉnh sáng tối đa rồi giơ màn hình trước webcam.

Chọn một trong ba cách dưới đây.

### Cách 1: chạy trên máy Windows (dùng `start.bat`)

**Bước 1. Lấy project về máy** (bỏ qua nếu máy đã có thư mục project):
- Vào https://github.com/luongduy0703/human_heart_vr, bấm nút xanh **Code → Download ZIP**, rồi giải nén ra một thư mục, ví dụ `C:\human_heart_vr`.
- Hoặc dùng git: `git clone https://github.com/luongduy0703/human_heart_vr.git`

**Bước 2. Cài Python 3** (chỉ làm một lần):
- Mở **Command Prompt**, gõ `python --version`. Nếu hiện `Python 3.x.x` thì máy đã có Python, sang bước 3.
- Nếu chưa có: tải tại https://www.python.org/downloads/. Khi cài nhớ tick **Add python.exe to PATH**.

**Bước 3. Mở trang:** vào thư mục `web` của project và **nháy đúp `start.bat`**.
- Một cửa sổ đen hiện dòng `Web server: http://localhost:8000/web/`, sau khoảng 2 giây trình duyệt tự mở trang.
- Nếu trình duyệt không tự mở, gõ `http://localhost:8000/web/` vào thanh địa chỉ.

**Bước 4. Cho phép camera:** trình duyệt hỏi quyền dùng camera, bấm **Cho phép**. Lần đầu trang "chuẩn bị marker" vài giây; những lần sau mở ngay.

**Bước 5. Giơ marker:** khi khung góc dưới báo *"Đưa tờ giấy in marker vào trước webcam."*, giơ tờ giấy vào **giữa khung hình**, cách webcam khoảng 30–50 cm. Tim trồi lên từ tờ giấy trong 4 giây.

**Bước 6. Tắt:** đóng cửa sổ đen.

### Cách 2: chạy trên macOS hoặc Linux

Mở Terminal ở **thư mục gốc của project** (thư mục chứa `Assets` và `web`, không phải trong `web`), gõ:

```
python3 -m http.server 8000 --bind 127.0.0.1
```

Mở `http://localhost:8000/web/` bằng Chrome hoặc Edge, rồi làm tiếp từ bước 4 ở trên. Muốn tắt thì nhấn **Ctrl+C** trong Terminal.

### Cách 3: chạy online bằng GitHub Pages (không cần cài Python)

Chủ repo bật GitHub Pages một lần:
1. Trên GitHub, vào repo `human_heart_vr` → **Settings** → **Pages**.
2. Mục **Source** chọn **Deploy from a branch**; mục **Branch** chọn **main** và thư mục **/ (root)** → **Save**.
3. Chờ 1–2 phút. Trang sẽ chạy tại https://luongduy0703.github.io/human_heart_vr/web/

Sau đó ai cũng mở được link trên bằng Chrome hoặc Edge, cho phép camera rồi giơ marker. Link cũng mở được trên điện thoại (trang dùng camera sau), nhưng cách này chưa được thử. GitHub Pages miễn phí với repo public. File `.nojekyll` ở thư mục gốc giúp GitHub Pages đưa file lên nguyên trạng, không xử lý qua Jekyll.

> **Lưu ý:** không nháy đúp mở trực tiếp `index.html` (địa chỉ dạng `file:///…`). Khi đó trình duyệt chặn webcam và không tải được mô hình. Web server cũng phải chạy ở thư mục gốc của project, vì trang đọc mô hình và ảnh marker trong thư mục `Assets/`.

## Sử dụng

| Thao tác | Chuột | Màn hình cảm ứng |
|---|---|---|
| Xoay tim | Giữ chuột trái và kéo | Kéo một ngón |
| Phóng to / thu nhỏ (0.5×–3×) | Lăn chuột | Chụm / mở hai ngón |
| Hiện tên bộ phận | Bấm vào chấm màu | Chạm nhanh vào chấm màu |
| Về góc nhìn ban đầu | Nút **Đặt lại góc nhìn** | Nút **Đặt lại góc nhìn** |

- Chỉ hiện **một nhãn mỗi lúc**: bấm chấm khác thì nhãn đổi theo, bấm lại chấm đang chọn thì nhãn ẩn đi. Chấm đang chọn to hơn các chấm khác.
- Các chấm chỉ bấm được khi tim đã trồi lên hết.
- Mất dấu marker hơn 2 giây rồi thấy lại thì tim trồi lên lại từ đầu, góc nhìn và nhãn trở về ban đầu.
- 12 bộ phận, nhãn tiếng Anh: Right/Left atrium, Right/Left ventricle, Aorta, Pulmonary trunk, Superior/Inferior vena cava, Pulmonary veins, Right/Left coronary artery, Apex.

**Mẹo:**
- Giấy phẳng, đủ sáng, không bị lóa.
- Giữ tờ giấy ở giữa khung hình, cách webcam khoảng 30–50 cm. Tờ giấy nên chiếm từ một nửa chiều cao khung hình trở lên.
- Hình webcam **không lật như gương** (giống ảnh chụp bằng camera), để hai bên phải/trái của tim hiển thị đúng giải phẫu.

## Cấu trúc thư mục

```
web/
├── index.html     giao diện; nạp three.js 0.160.0 và MindAR 1.2.5 từ CDN (importmap)
├── main.js        nhận diện marker, hoạt ảnh trồi lên, occluder, xoay/zoom, hotspot, nhãn
├── heart-data.js  ma trận đặt tim lên marker, vị trí/tên/màu 12 hotspot (đo trên heart.glb)
├── start.bat      chạy web server ở thư mục gốc repo rồi mở trình duyệt
└── README.md
```

> MindAR 1.2.5 chỉ chạy với three.js bản trước r162, nên `index.html` cố định three.js ở 0.160.0. Đừng nâng bản three.js.

## Cách hoạt động (so với bản Unity)

| Bản Unity | Bản web |
|---|---|
| `HeartTracker` (AR Foundation, ARCore) | MindAR nhận diện ảnh marker; `onTargetFound` / `onTargetLost`. Bộ lọc One Euro chống rung (`FILTER_MIN_CF`, `FILTER_BETA`) |
| `HeartEmerge` | `update()` trong `main.js`: trồi lên từ độ sâu 12 cm dưới giấy, lớn dần từ 0.3 lên 1 trong 4 giây (SmoothStep) |
| `DepthMask` + `Occluder` | Mặt phẳng ở mặt giấy với `colorWrite: false`, `renderOrder = -1`: vẽ trước tim, chỉ ghi depth nên che phần tim dưới giấy mà vẫn thấy hình webcam |
| `HeartInteraction` | `setupInput()`: xoay quanh tâm tim theo trục của camera, zoom mà đáy tim vẫn nằm trên giấy |
| `GazeLabel` + `HeartPart` | `pick()` bắn tia từ chỗ bấm; `select()` chỉ hiện một nhãn |
| Bảng `Parts` trong `HeartSceneSetup.cs` | `heart-data.js` (cùng vị trí, tên, màu) |

- **Đơn vị:** 1 = chiều rộng ảnh marker. Tim dài khoảng 1.02 đơn vị, nên khi marker in rộng 19.6 cm thì tim dài 20 cm giống bản Unity. In nhỏ hơn thì tim nhỏ theo; MindAR không cần biết kích thước in.
- **Chuẩn bị marker:** MindAR cần chuyển ảnh marker thành dữ liệu đặc trưng (định dạng `.mind`). Trang tự chuyển ở lần chạy đầu rồi lưu trong trình duyệt (IndexedDB, khóa `heart_marker2-v1`). Khi đổi ảnh marker, sửa `MARKER_URL` và đổi tên `CACHE_KEY` trong `main.js`.

## Thông số chỉnh được (đầu file `main.js`)

| Hằng số | Mặc định | Ý nghĩa |
|---|---|---|
| `EMERGE_SECONDS` | 4 | Số giây tim trồi lên |
| `SINK_DEPTH` | 12 cm | Độ sâu ban đầu dưới mặt giấy |
| `START_SCALE` | 0.3 | Kích thước lúc bắt đầu so với kích thước cuối |
| `REPLAY_AFTER_LOST` | 2 | Mất dấu lâu hơn số giây này thì phát lại |
| `DEG_PER_SCREEN` | 270 | Số độ xoay khi kéo hết chiều rộng màn hình |
| `MIN_ZOOM` / `MAX_ZOOM` | 0.5 / 3 | Giới hạn phóng to / thu nhỏ |
| `MARKER_RADIUS` | 0.4 cm | Bán kính chấm màu |
| `HIT_RADIUS` | 1.5 cm | Bán kính vùng bấm quanh chấm |
| `FILTER_MIN_CF` | 0.0005 | Tim rung thì giảm (ví dụ 0.0001) |
| `FILTER_BETA` | 300 | Tim trễ khi di chuyển giấy nhanh thì tăng (ví dụ 1000) |
| `MISS_TOLERANCE` | 10 | Số khung hình liên tiếp không thấy marker thì coi là mất dấu |

## Xử lý sự cố

| Hiện tượng | Cách sửa |
|---|---|
| Báo "Trình duyệt chỉ cho dùng webcam khi mở trang qua http://localhost…" | Mở bằng `start.bat` hoặc `http://localhost:8000/web/`, không mở file trực tiếp |
| Báo "Không tải được mô hình hoặc ảnh marker" | Web server chạy sai thư mục, phải là thư mục gốc repo. Thử mở `http://localhost:8000/Assets/Models/heart.glb` |
| Báo "Không mở được webcam" | Cho phép quyền camera (biểu tượng camera trên thanh địa chỉ); đóng Zoom, Teams, Camera… rồi tải lại trang |
| Khung quét vẫn hiện, tim không xuất hiện | Đưa tờ giấy vào giữa khung hình, lại gần hơn, thêm ánh sáng, tránh lóa |
| Trang không chạy, console (F12) báo lỗi tải từ `cdn.jsdelivr.net` | Kiểm tra kết nối Internet |
| Cửa sổ đen báo cổng 8000 đang bận | Sửa `set PORT=8000` trong `start.bat` thành số khác, ví dụ 8080 |
| Muốn xem trạng thái bên trong | Mở `http://localhost:8000/web/?debug`, nhấn F12, gõ `heartApp` trong Console |

## Ghi nguồn

- Mô hình tim: [Human Heart 3D Model | Anatomy | Medical Project](https://sketchfab.com/3d-models/human-heart-3d-model-anatomy-medical-project-5da08bb137014f0392c69f9997f777cd) của Mesh-Magnet, giấy phép [CC BY 4.0](http://creativecommons.org/licenses/by/4.0/).
- [MindAR](https://github.com/hiukim/mind-ar-js) và [three.js](https://threejs.org/), giấy phép MIT.

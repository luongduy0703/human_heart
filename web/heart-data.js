// Dữ liệu đặt tim và 12 hotspot lên marker, đo trên Assets/Models/heart.glb.
// Hệ tọa độ của MindAR: gốc ở tâm ảnh marker, 1 đơn vị = chiều rộng ảnh (19.6 cm khi in),
// x sang phải, y về phía đầu ảnh, z vuông góc ra khỏi tờ giấy (mặt giấy là z = 0).
// Số liệu lấy từ scene Unity (tư thế Visual, FitHeartToMarker cao 20 cm) và bảng Parts trong
// Assets/Scripts/Editor/HeartSceneSetup.cs; đổi mô hình hoặc kích thước tim thì phải tính lại.

// Ma trận 4x4 (thứ tự cột, dùng với Matrix4.fromArray) đưa đỉnh lưới của heart.glb vào hệ tọa độ marker:
// tim nằm ngửa trên giấy, mặt trước hướng lên, các mạch máu lớn hướng về phía đầu ảnh.
export const HEART_MESH_MATRIX = [
  0.51383, 0, 0, 0,
  0, 0.51383, 0, 0,
  0, 0, 0.51383, 0,
  0.005719, 0.00044, 0.276541, 1
];

// Tâm khung bao của tim (điểm xoay/zoom) và đáy tim (nằm đúng mặt giấy)
export const HEART_CENTER = [0, 0, 0.278338];
export const HEART_BOTTOM = 0;

// 12 bộ phận: tên trên nhãn, màu chấm (sRGB 0..1), vị trí tâm chấm (cao hơn bề mặt tim 3 mm)
export const HOTSPOTS = [
  { id: 'TamNhiPhai', label: 'Right atrium', color: [0.25, 0.45, 1], position: [-0.219388, 0.132653, 0.432219] },
  { id: 'TamThatPhai', label: 'Right ventricle', color: [0.2, 0.85, 1], position: [-0.096939, -0.173469, 0.54551] },
  { id: 'TamNhiTrai', label: 'Left atrium', color: [1, 0.6, 0.15], position: [0.137755, 0.061224, 0.431968] },
  { id: 'TamThatTrai', label: 'Left ventricle', color: [1, 0.2, 0.25], position: [0.132653, -0.214286, 0.49648] },
  { id: 'DongMachChu', label: 'Aorta', color: [0.85, 0, 0.1], position: [0.061224, 0.377551, 0.30339] },
  { id: 'DongMachPhoi', label: 'Pulmonary trunk', color: [0.35, 0.25, 0.9], position: [0.05102, 0.204082, 0.378611] },
  { id: 'TinhMachChuTren', label: 'Superior vena cava', color: [0.1, 0.2, 0.7], position: [-0.153061, 0.316327, 0.27092] },
  { id: 'TinhMachChuDuoi', label: 'Inferior vena cava', color: [0.15, 0.35, 0.6], position: [-0.265306, -0.183673, 0.401724] },
  { id: 'TinhMachPhoi', label: 'Pulmonary veins', color: [0.95, 0.4, 0.6], position: [0.270408, 0.188776, 0.191459] },
  { id: 'DongMachVanhPhai', label: 'Right coronary artery', color: [1, 0.9, 0.2], position: [-0.255102, -0.02551, 0.43768] },
  { id: 'DongMachVanhTrai', label: 'Left coronary artery', color: [0.6, 1, 0.3], position: [0.030612, -0.081633, 0.545704] },
  { id: 'MomTim', label: 'Apex', color: [1, 1, 1], position: [0.030612, -0.44898, 0.432] },
];

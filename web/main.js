// Trái tim AR chạy trên trình duyệt bằng webcam laptop: tim 3D trồi lên từ tờ giấy in marker
// (Assets/Markers/heart_marker2.jpg). MindAR nhận diện ảnh, three.js vẽ 3D. Cùng cách làm với bản Unity:
//  - Occluder: mặt phẳng tàng hình ở mặt giấy, chỉ ghi depth, che phần tim còn nằm dưới giấy.
//  - Tim trồi lên trong 4 giây; mất dấu marker quá 2 giây rồi thấy lại thì phát lại từ đầu.
//  - Kéo chuột/một ngón để xoay, lăn chuột/chụm hai ngón để phóng to thu nhỏ,
//    bấm chấm màu (hotspot) để hiện tên bộ phận, mỗi lúc chỉ một nhãn.
// Web server phải chạy ở thư mục gốc của repo (xem start.bat) để đọc được mô hình và ảnh trong Assets/.
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
// mindar-image phải nạp trước: nó gán lại window.MINDAR.IMAGE, nạp sau sẽ xóa MindARThree khỏi biến toàn cục
import { Compiler } from 'mindar-image';
import { MindARThree } from 'mindar-image-three';
import { HEART_MESH_MATRIX, HEART_CENTER, HEART_BOTTOM, HOTSPOTS } from './heart-data.js';

const MODEL_URL = '../Assets/Models/heart.glb';
const MARKER_URL = '../Assets/Markers/heart_marker2.jpg';
const CACHE_KEY = 'heart_marker2-v1';    // đổi tên khi thay ảnh marker để trình duyệt chuyển lại

// Đơn vị: 1 = chiều rộng ảnh marker (19.6 cm khi in)
const MARKER_WIDTH_M = 0.196;
const EMERGE_SECONDS = 4;
const SINK_DEPTH = 0.12 / MARKER_WIDTH_M;    // lúc đầu tim nằm sâu 12 cm dưới mặt giấy
const START_SCALE = 0.3;
const REPLAY_AFTER_LOST = 2;                 // giây

const DEG_PER_SCREEN = 270;                  // kéo hết chiều rộng màn hình = xoay 270°
const MIN_ZOOM = 0.5, MAX_ZOOM = 3;
const TAP_MAX_MS = 350, TAP_MAX_MOVE = 8;    // nhấn-thả nhanh, đi không quá 8 px thì là bấm

const MARKER_RADIUS = 0.004 / MARKER_WIDTH_M;  // chấm màu đường kính 0.8 cm (tim chiếm gần hết màn hình laptop)
const HIT_RADIUS = 0.015 / MARKER_WIDTH_M;     // vùng bấm bán kính 1.5 cm
const SELECTED_SCALE = 1.6;

// Độ mượt khi bám marker (bộ lọc One Euro của MindAR). Rung nhiều: giảm FILTER_MIN_CF; trễ nhiều: tăng FILTER_BETA.
const FILTER_MIN_CF = 0.0005;
const FILTER_BETA = 300;
const MISS_TOLERANCE = 10;                   // số khung hình không thấy marker liên tiếp thì coi là mất dấu

const container = document.getElementById('ar');
const labelEl = document.getElementById('label');
const statusEl = document.getElementById('status');
const resetBtn = document.getElementById('reset');
const errorEl = document.getElementById('error');

const setStatus = (text) => { statusEl.textContent = text; };

function showError(text) {
  errorEl.textContent = text;
  errorEl.hidden = false;
  setStatus('Có lỗi, xem thông báo trên màn hình.');
}

main();

async function main() {
  if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
    showError('Trình duyệt chỉ cho dùng webcam khi mở trang qua http://localhost hoặc https. ' +
              'Hãy chạy start.bat rồi mở http://localhost:8000/web/ (không mở trực tiếp file index.html).');
    return;
  }

  let targets, gltf;
  try {
    setStatus('Đang chuẩn bị marker…');
    targets = await loadTargets((p) => setStatus(`Đang chuẩn bị marker (chỉ làm lần đầu)… ${Math.round(p)}%`));
    setStatus('Đang tải mô hình tim…');
    gltf = await new GLTFLoader().loadAsync(MODEL_URL);
  } catch (e) {
    console.error(e);
    showError('Không tải được mô hình hoặc ảnh marker. Web server phải chạy ở thư mục gốc của repo ' +
              '(dùng start.bat) để đọc được thư mục Assets/.');
    return;
  }

  const mindar = new MindARThree({
    container,
    imageTargetSrc: URL.createObjectURL(new Blob([targets])),
    maxTrack: 1,
    uiError: 'no',
    filterMinCF: FILTER_MIN_CF,
    filterBeta: FILTER_BETA,
    missTolerance: MISS_TOLERANCE,
  });
  const app = buildScene(mindar, gltf);
  setupInput(app);
  // Mở trang với ?debug để xem trạng thái trong DevTools (window.heartApp) hoặc chạy kiểm thử tự động
  if (new URLSearchParams(location.search).has('debug')) window.heartApp = { app, mindar, THREE };

  try {
    setStatus('Đang mở webcam… (bấm Cho phép nếu trình duyệt hỏi)');
    await mindar.start();
  } catch (e) {
    console.error(e);
    showError('Không mở được webcam. Hãy cho phép trang dùng camera (biểu tượng camera trên thanh địa chỉ) ' +
              'và đóng các ứng dụng khác đang dùng webcam (Zoom, Teams, Camera…), rồi tải lại trang.');
    return;
  }
  setStatus('Đưa tờ giấy in marker vào trước webcam.');

  const clock = new THREE.Clock();
  mindar.renderer.setAnimationLoop(() => {
    app.update(Math.min(clock.getDelta(), 0.1));
    mindar.renderer.render(mindar.scene, mindar.camera);
    app.updateLabel();
  });
}

// ---------- Marker: file .mind của MindAR ----------

// Ảnh marker phải chuyển sang dữ liệu đặc trưng (.mind) cho MindAR. Làm một lần (vài giây),
// rồi lưu trong trình duyệt (IndexedDB) để những lần sau mở ngay.
async function loadTargets(onProgress) {
  const cached = await cacheGet(CACHE_KEY);
  if (cached) return cached;

  const img = new Image();
  img.src = MARKER_URL;
  await img.decode();
  const compiler = new Compiler();
  await compiler.compileImageTargets([img], onProgress);
  const data = compiler.exportData();
  await cacheSet(CACHE_KEY, data);
  return data;
}

function openCache() {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open('heart-ar', 1);
    req.onupgradeneeded = () => req.result.createObjectStore('targets');
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

async function cacheGet(key) {
  try {
    const db = await openCache();
    return await new Promise((resolve, reject) => {
      const req = db.transaction('targets').objectStore('targets').get(key);
      req.onsuccess = () => resolve(req.result ? new Uint8Array(req.result) : null);
      req.onerror = () => reject(req.error);
    });
  } catch {
    return null;  // trình duyệt chặn lưu trữ (chế độ ẩn danh…): chuyển lại mỗi lần
  }
}

async function cacheSet(key, data) {
  try {
    const db = await openCache();
    await new Promise((resolve, reject) => {
      const tx = db.transaction('targets', 'readwrite');
      tx.objectStore('targets').put(data.slice().buffer, key);
      tx.oncomplete = resolve;
      tx.onerror = () => reject(tx.error);
    });
  } catch { /* không lưu được thì thôi */ }
}

// ---------- Cảnh 3D ----------

function buildScene(mindar, gltf) {
  const { scene, camera } = mindar;
  scene.add(new THREE.HemisphereLight(0xffffff, 0x445566, 1.6));
  const sun = new THREE.DirectionalLight(0xffffff, 2.2);
  sun.position.set(0.5, 1, 1);  // camera của MindAR đứng yên ở gốc, nên ánh sáng luôn chiếu từ phía người xem
  scene.add(sun);

  const anchor = mindar.addAnchor(0);

  // Occluder ở mặt giấy (z = 0): không vẽ màu, chỉ ghi depth và được vẽ trước tim,
  // nên phần tim nằm dưới mặt giấy bị che mà vẫn thấy hình webcam phía sau
  const occluder = new THREE.Mesh(new THREE.PlaneGeometry(4, 4), new THREE.MeshBasicMaterial({ colorWrite: false }));
  occluder.renderOrder = -1;
  anchor.group.add(occluder);

  const heartRoot = new THREE.Group();   // như HeartModel trong Unity: chạy hoạt ảnh trồi lên
  const pivot = new THREE.Group();       // như InteractPivot: xoay/zoom quanh tâm tim
  const content = new THREE.Group();
  content.position.set(-HEART_CENTER[0], -HEART_CENTER[1], -HEART_CENTER[2]);
  anchor.group.add(heartRoot);
  heartRoot.add(pivot);
  pivot.add(content);

  // Lấy lưới tim trong glb, bỏ qua các node trung gian của Sketchfab, đặt bằng ma trận đã đo
  let heartMesh = null;
  gltf.scene.traverse((o) => { if (o.isMesh && !heartMesh) heartMesh = o; });
  heartMesh.removeFromParent();
  heartMesh.position.set(0, 0, 0);
  heartMesh.quaternion.identity();
  heartMesh.scale.set(1, 1, 1);
  heartMesh.geometry.applyMatrix4(new THREE.Matrix4().fromArray(HEART_MESH_MATRIX));
  content.add(heartMesh);

  // Hotspot: chấm màu nhìn thấy + quả cầu lớn hơn, không vẽ, để bấm cho dễ trúng
  const hotspotGroup = new THREE.Group();
  hotspotGroup.visible = false;          // chỉ bật khi tim đã trồi lên hết
  content.add(hotspotGroup);
  const markerGeo = new THREE.SphereGeometry(MARKER_RADIUS, 20, 14);
  const hitGeo = new THREE.SphereGeometry(HIT_RADIUS, 12, 8);
  const hitMat = new THREE.MeshBasicMaterial({ visible: false });
  const hotspots = HOTSPOTS.map((h) => {
    const group = new THREE.Group();
    group.position.fromArray(h.position);
    const color = new THREE.Color().setRGB(h.color[0], h.color[1], h.color[2], THREE.SRGBColorSpace);
    const marker = new THREE.Mesh(markerGeo, new THREE.MeshBasicMaterial({ color }));
    const hit = new THREE.Mesh(hitGeo, hitMat);
    const spot = { ...h, marker, cssColor: `#${color.getHexString(THREE.SRGBColorSpace)}` };
    hit.userData.hotspot = spot;
    group.add(marker, hit);
    hotspotGroup.add(group);
    return { spot, hit };
  });
  const pickTargets = [heartMesh, ...hotspots.map((h) => h.hit)];

  const state = {
    anchorVisible: false,
    everFound: false,
    lostAt: 0,
    emergeT: 1,
    zoom: 1,
    selected: null,
  };

  function select(spot) {
    if (state.selected) state.selected.marker.scale.setScalar(1);
    state.selected = state.selected === spot ? null : spot;  // bấm lại chấm đang chọn thì ẩn nhãn
    if (state.selected) {
      state.selected.marker.scale.setScalar(SELECTED_SCALE);
      labelEl.textContent = state.selected.label;
      labelEl.style.borderColor = state.selected.cssColor;
    }
  }

  function applyZoom() {
    pivot.scale.setScalar(state.zoom);
    // pivot nằm ở tâm tim: nâng lên theo tỉ lệ để đáy tim luôn nằm trên mặt giấy
    pivot.position.set(HEART_CENTER[0], HEART_CENTER[1], HEART_BOTTOM + (HEART_CENTER[2] - HEART_BOTTOM) * state.zoom);
  }

  function resetView() {
    pivot.quaternion.identity();
    state.zoom = 1;
    applyZoom();
  }

  function replay() {
    state.emergeT = 0;
    hotspotGroup.visible = false;
    if (state.selected) select(state.selected);  // bỏ chọn, ẩn nhãn
    resetView();
    applyEmerge(0);
  }

  function applyEmerge(s) {
    heartRoot.position.z = THREE.MathUtils.lerp(-SINK_DEPTH, 0, s);
    heartRoot.scale.setScalar(THREE.MathUtils.lerp(START_SCALE, 1, s));
  }

  anchor.onTargetFound = () => {
    state.anchorVisible = true;
    const lostFor = performance.now() / 1000 - state.lostAt;
    if (!state.everFound || lostFor > REPLAY_AFTER_LOST) replay();
    state.everFound = true;
    setStatus('Đã thấy marker.');
  };
  anchor.onTargetLost = () => {
    state.anchorVisible = false;
    state.lostAt = performance.now() / 1000;
    setStatus('Mất dấu marker: đưa tờ giấy vào lại trước webcam.');
  };

  const projected = new THREE.Vector3();

  return {
    camera,
    anchor,
    pivot,
    state,
    pickTargets,
    hotspotGroup,
    select,
    resetView,
    applyZoom,

    update(dt) {
      if (state.emergeT >= 1) return;
      state.emergeT = Math.min(1, state.emergeT + dt / EMERGE_SECONDS);
      const t = state.emergeT;
      applyEmerge(t * t * (3 - 2 * t));  // SmoothStep giống bản Unity
      if (t >= 1) hotspotGroup.visible = true;
    },

    // Nhãn là một thẻ HTML đặt ngay phía trên chấm đang chọn (chữ luôn nét và quay về người xem)
    updateLabel() {
      const spot = state.selected;
      if (!spot || !state.anchorVisible || !hotspotGroup.visible) {
        labelEl.hidden = true;
        return;
      }
      spot.marker.getWorldPosition(projected).project(camera);
      if (projected.z > 1) {
        labelEl.hidden = true;
        return;
      }
      const x = (projected.x * 0.5 + 0.5) * container.clientWidth;
      const y = (-projected.y * 0.5 + 0.5) * container.clientHeight;
      labelEl.style.transform = `translate(${x}px, ${y}px) translate(-50%, calc(-100% - 16px))`;
      labelEl.hidden = false;
    },
  };
}

// ---------- Chuột / cảm ứng ----------

function setupInput(app) {
  const pointers = new Map();   // pointerId -> {x, y}
  let tap = null;               // cú nhấn có thể là "bấm" (chưa di chuyển)
  let blocked = false;          // vừa chụm hai ngón xong: chờ nhấc hết tay mới cho xoay tiếp
  let pinchDist = 0;
  const raycaster = new THREE.Raycaster();
  const ndc = new THREE.Vector2();
  const inv = new THREE.Matrix4();
  const axis = new THREE.Vector3();
  const q = new THREE.Quaternion();

  resetBtn.addEventListener('click', () => app.resetView());

  const twoFingerDistance = () => {
    const [a, b] = [...pointers.values()];
    return Math.hypot(a.x - b.x, a.y - b.y);
  };

  container.addEventListener('pointerdown', (e) => {
    if (e.pointerType === 'mouse' && e.button !== 0) return;
    container.setPointerCapture(e.pointerId);
    pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
    if (pointers.size === 1) {
      tap = { id: e.pointerId, x: e.clientX, y: e.clientY, time: performance.now(), moved: false };
    } else {
      tap = null;
      if (pointers.size === 2) pinchDist = twoFingerDistance();
    }
  });

  container.addEventListener('pointermove', (e) => {
    const p = pointers.get(e.pointerId);
    if (!p) return;
    const dx = e.clientX - p.x, dy = e.clientY - p.y;
    p.x = e.clientX;
    p.y = e.clientY;

    if (pointers.size === 1) {
      if (tap && !tap.moved && Math.hypot(e.clientX - tap.x, e.clientY - tap.y) > TAP_MAX_MOVE) tap.moved = true;
      if (!blocked && (!tap || tap.moved)) rotate(dx, dy);
    } else if (pointers.size === 2) {
      const d = twoFingerDistance();
      if (pinchDist > 0) zoomBy(d / pinchDist);
      pinchDist = d;
    }
  });

  const endPointer = (e) => {
    if (!pointers.has(e.pointerId)) return;
    if (tap && tap.id === e.pointerId && !tap.moved && performance.now() - tap.time <= TAP_MAX_MS) {
      pick(e.clientX, e.clientY);
    }
    pointers.delete(e.pointerId);
    if (pointers.size === 0) {
      tap = null;
      blocked = false;
    } else if (pointers.size === 1) {
      blocked = true;
    }
  };
  container.addEventListener('pointerup', endPointer);
  container.addEventListener('pointercancel', endPointer);

  container.addEventListener('wheel', (e) => {
    e.preventDefault();
    zoomBy(Math.exp(-e.deltaY * 0.0015));
  }, { passive: false });

  // Xoay quanh tâm tim theo trục của camera: kéo ngang xoay quanh trục dọc màn hình, kéo dọc xoay quanh trục ngang
  function rotate(dx, dy) {
    if (!app.state.anchorVisible) return;
    const radPerPixel = THREE.MathUtils.degToRad(DEG_PER_SCREEN) / container.clientWidth;
    inv.copy(app.anchor.group.matrixWorld).invert();
    axis.setFromMatrixColumn(app.camera.matrixWorld, 1).transformDirection(inv);  // trục dọc của camera
    q.setFromAxisAngle(axis, dx * radPerPixel);
    app.pivot.quaternion.premultiply(q);
    axis.setFromMatrixColumn(app.camera.matrixWorld, 0).transformDirection(inv);  // trục ngang của camera
    q.setFromAxisAngle(axis, dy * radPerPixel);
    app.pivot.quaternion.premultiply(q);
  }

  function zoomBy(factor) {
    if (!app.state.anchorVisible) return;
    app.state.zoom = THREE.MathUtils.clamp(app.state.zoom * factor, MIN_ZOOM, MAX_ZOOM);
    app.applyZoom();
  }

  function pick(clientX, clientY) {
    if (!app.state.anchorVisible || !app.hotspotGroup.visible) return;
    const r = container.getBoundingClientRect();
    ndc.set(((clientX - r.left) / r.width) * 2 - 1, -((clientY - r.top) / r.height) * 2 + 1);
    raycaster.setFromCamera(ndc, app.camera);
    // Tia trúng tim trước (chấm bị tim che) thì không chọn gì
    const hit = raycaster.intersectObjects(app.pickTargets, false)[0];
    if (hit && hit.object.userData.hotspot) app.select(hit.object.userData.hotspot);
  }
}

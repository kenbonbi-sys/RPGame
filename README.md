# Rừng Thì Thầm — Prototype Action RPG pixel art (Unity 6 · 2D URP)

Prototype top-down action RPG: khám phá rừng, nhặt đồ, làm nhiệm vụ và đánh boss **Gấu Ma Rừng Già** với nhiều pattern tấn công. Toàn bộ art (tile, nhân vật, boss, icon, UI, VFX) và âm thanh đều được **sinh bằng script** (không dùng asset bên thứ ba), nên bạn có thể thay bằng art thật bất cứ lúc nào.

## Mở và chạy

- **Chơi ngay:** `Builds/Windows/RungThiTham.exe`
- **Mở trong Unity:** Unity Hub → *Add project from disk* → chọn thư mục `RPG` → mở bằng **Unity 6000.6.0f1** → mở scene `Assets/Scenes/Game.unity` → bấm **Play**.

## Điều khiển (theo layout của game tham khảo)

| Phím | Chức năng |
|---|---|
| Giữ **chuột trái / phải** hoặc **phím mũi tên** | Di chuyển |
| **Nhấp vào quái** | Tự đi tới và chém (dùng skill Q) |
| **Q W E R A S D** | Chém Gió · Cầu Lửa · Mũi Băng · Lôi Phạt · Hồi Phục · Khiên Thánh · Bão Kiếm |
| **Space** | Lướt (bất tử trong chốc lát, dùng để né vòng đỏ) |
| **1 2 3** | Bình Máu · Bình Năng Lượng · Thuốc Thảo Mộc |
| **F** | Nói chuyện với NPC |
| **B / I** · **J** · **Tab** | Túi đồ · Bách Khoa Trùm · Đổi nhiệm vụ đang theo dõi |
| **F1** · **Esc** | Hướng dẫn · Tạm dừng |
| **F5–F9** (cheat) | Hồi đầy · Tua giờ · Tới boss · Về làng · Hạ quái gần |

## Nội dung đã có

- **Bản đồ** 100×64 tile: Làng Lá Xanh → Rừng Thì Thầm (đường đất, cỏ rậm, 5 bãi quái) → đấu trường Rừng Già Cổ Thụ (cột đá cổ, Tảng Đá Lớn). Có vùng + tên khu vực, minimap, chu kỳ ngày/đêm (đèn 2D: lửa trại, đèn lồng, cửa sổ nhà, ánh sáng quanh nhân vật).
- **Người chơi:** 4 hướng, animation idle/walk/attack/cast/dash/hurt/dead, 8 skill có VFX riêng, combo chém 3 đòn, crit, hit-stop, rung màn hình.
- **Quái:** Slime Rêu (nhảy lao tới), Nấm Độc (bắn bào tử làm chậm). Có AI tuần tra / đuổi / quay về, rớt đồ, hồi sinh theo bãi.
- **Boss Gấu Ma Rừng Già (Cấp 6):** Vồ, **Dậm Đất** (choáng), **Ném Đá Lớn** (đá rơi xuống thành *★ Tảng Đá Lớn* phá được), **Chụp Quăng** (nhảy vồ; đáp trúng Tảng Đá Lớn thì boss bị **Choáng!**), **Cuồng Nộ** khi dưới 50% máu. Mỗi chiêu có vòng cảnh báo trên đất và hiện "Kỹ năng: …" trên đầu boss.
- **Bách Khoa Trùm:** tự ghi lại quái và kỹ năng boss lần đầu gặp (hiện ở log bên trái, xem đầy đủ bằng phím J).
- **Nhiệm vụ:** chuỗi chính với Trưởng Làng + nhiệm vụ phụ của Bé Mai (hiện "(+1 · Tab)" như bản tham khảo).
- **HUD:** thanh máu boss khung trang trí, minimap + tên vùng + ngày/đêm, quest tracker, orb Máu/Năng lượng dạng chất lỏng, thanh bình thuốc, skill bar với cooldown/chi phí, log sự kiện, số damage bay, nameplate, hội thoại có portrait, túi đồ + tooltip, màn chết/hồi sinh, banner khu vực/chiến thắng.
- **Âm thanh:** 42 SFX, nhạc rừng, nhạc boss, âm nền rừng (tự tổng hợp, `Tools/AudioGen`).

## Cấu trúc project

```
Assets/
  Scripts/  Core · Combat · Player · Skills · Enemies · World · Items · UI · VFX · Anim · Data · Debug · Editor
  Art/      ảnh sinh từ Tools/ArtGen (+ art_manifest.json: cắt sprite, pivot, 9-slice, animation)
  Prefabs/  Characters · Props · Gameplay · VFX (45 hiệu ứng)
  Data/     Items, Skills (ScriptableObject — chỉnh chỉ số trong Inspector), Anims, VFX/Audio library
  Shaders/  RPG/VFX Additive (HDR → Bloom), RPG/VFX Alpha, RPG/Sprite Silhouette (hit flash)
  Settings/ URP 2D Renderer, Volumes (Bloom, Vignette, Impact, Danger)
Tools/
  ArtGen/   python build_all.py  → vẽ lại toàn bộ pixel art
  AudioGen/ python gen_audio.py  → tổng hợp lại âm thanh
```

## Menu Tools/RPG trong Unity

- **Build Everything** — import art → tạo data → VFX → prefab → dựng lại scene. ⚠️ Ghi đè các prefab, data và scene được sinh ra.
- **Steps/1–6** — chạy từng bước. Nếu bạn đã tự chỉnh prefab/VFX, chỉ dùng **Steps/6. Rebuild Scene Only** để dựng lại map mà giữ nguyên prefab.
- **Build Windows Player** — xuất `Builds/Windows/RungThiTham.exe`.

## Mở rộng

- **Chỉnh VFX:** mở `Assets/Prefabs/VFX/<tên>.prefab`. Mỗi hiệu ứng là Particle System + sprite flipbook + Light2D. Tăng `_Intensity` của material để glow (Bloom) mạnh hơn.
- **Thêm skill:** tạo class kế thừa `SkillDef` và viết `Execute()`, tạo asset qua *Create → RPG → Skills*, rồi gán vào `PlayerSkills.slots` trên prefab Player.
- **Thay art:** thay PNG trong `Assets/Art` (giữ kích thước frame), hoặc kéo sprite mới vào các `SpriteAnimSet` trong `Assets/Data/Anims`.
- **Thêm quái:** kế thừa `EnemyBase` (xem `SlimeAI`, `ShroomAI`), boss tham khảo `BossBear`.
- **Chạy test tự động:** `RungThiTham.exe -autoshot -autoshotDir "D:\shots"` sẽ tự chơi một vòng, chụp màn hình rồi thoát.

## Giấy phép

- Font **Inter** (SIL Open Font License 1.1), lấy từ bộ cài Unity.
- Toàn bộ art/âm thanh còn lại do script trong `Tools/` sinh ra. Bộ icon Franuka chỉ dùng làm tham khảo phong cách, không có trong project.

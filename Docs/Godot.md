# Rừng Thì Thầm 3D (Godot)

Từ 14/10/2026 game được chuyển sang **Godot 4.7 3D**, góc nhìn như *Shadow of the Colossus*: camera treo sau lưng nhân vật, boss là những colossus khổng lồ phải leo lên và đâm vào điểm yếu. Bản Unity trong `Assets/` vẫn là bản bạn bè đang chơi online (máy chủ protocol 13) cho tới khi bản Godot bắt kịp. Hai bản sống chung một repo, cùng nhánh `main`.

Project nằm trong `Godot/`. Code viết bằng **GDScript** (máy chưa có .NET). Engine: `C:\Users\PC\Godot\Godot_v4.7.2-stable_win64.exe`, đã đối chiếu SHA512 với bản chính thức.

## Chạy

- **Chơi:** bấm đúp `Godot/choi.cmd`, hoặc `Godot_v4.7.2-stable_win64.exe --path Godot`.
- **Mở editor:** `Godot/choi.cmd editor`, hoặc mở `Godot/project.godot` từ Godot Project Manager.
- **Chụp màn hình:** F12, ảnh lưu vào `%APPDATA%\Godot\app_userdata\Rừng Thì Thầm 3D\screenshots`.

| Phím | Tác dụng |
|---|---|
| **WASD** · chuột | Chạy theo hướng camera · xoay camera |
| **Space** | Nhảy; khi đang bám thì bật nhảy khỏi bề mặt |
| **Shift** | Nhấp: Lướt (lộn né, bất tử 0,32 s) · giữ: chạy nhanh |
| **Chuột trái** | Combo kiếm 3 đòn. Khi đang bám hoặc đứng trên lưng colossus: giữ để vung kiếm, thả để đâm (giữ càng lâu càng đau) |
| **Giữ chuột phải** | Bám: túm lông colossus hoặc đá có thể bám, leo bằng WASD |
| **Q** / chuột giữa | Quay camera nhìn về phía boss |
| **F1** · **F3** · **Esc** | Bảng phím · thông số (FPS, trạng thái) · nhả chuột |

Có hỗ trợ tay cầm: cần trái chạy, cần phải xoay camera, A nhảy, B Lướt, X chém, RB bám, LB nhìn boss.

## Cấu trúc

```
Godot/
  project.godot        cài đặt (Jolt Physics, Forward+, cửa sổ 1600×900)
  choi.cmd             chạy game / mở editor
  scenes/              main.tscn (thế giới), hero.tscn
  scripts/
    core/              controls.gd (mọi phím, autoload Controls) · game.gd (autoload Game: hero trên màn hình, hit-stop, rung)
    camera/            sotc_camera.gd
    player/            hero.gd
    enemies/           colossus.gd (nền chung) · gau_ma.gd · weak_point.gd · boulder.gd
    world/             world_map.gd (bố cục) · terrain*.gd · foliage.gd · village.gd · arena.gd · props.gd · palette.gd
    ui/hud.gd · vfx/fx.gd · debug/autoshot.gd
  shaders/             đất, nước, lông colossus, ấn ký điểm yếu, lá cây lay gió
  world/               terrain_data.res (đất đã bake) · map_preview.png · environment.tres (trời, sương, màu)
  assets/third_party/  model CC0 (xem cuối trang)
  tests/               test_runner.tscn
  tools/               bake_world.gd · list_anims.gd · measure.gd
```

## Thế giới

`WorldMap` giữ nguyên bố cục của `WorldBuilder.cs` bản Unity (cùng các mốc tính theo ô 200×128), **1 ô = 4 m**, nên thế giới rộng 800×512 m: Làng Lá Xanh ở tây nam, đường mòn qua các bãi quái tới Hang Gấu Ma, đường phía đông sang Đầm Lầy Sương Mù (ao, hồ Xà Mẫu với 4 gò đất), phía bắc là vách cao nguyên Thảo Nguyên Gió và khối núi Pha Lê. Viền bản đồ là núi.

- Đất là một hàm độ cao (`WorldMap.height`, `WorldMap.splat`) được **bake** thành `world/terrain_data.res`. Đổi bố cục xong thì chạy lại:
  `Godot_v4.7.2-stable_win64_console.exe --headless --path Godot -s tools/bake_world.gd` (khoảng 4 giây, vẽ luôn `map_preview.png`). Test `baked land matches WorldMap` báo khi quên bake.
- `Terrain` dựng lưới 64 m mỗi mảnh và một va chạm heightmap 1 m. `Foliage` rải cây, đá, bụi, nấm và hoa theo hạt giống cố định. Cây và đá lớn có va chạm.
- Model Kenney được đổi màu sang bảng màu trầm trong `palette.gd`, và lá cây lay theo gió.

## Camera kiểu SotC (`sotc_camera.gd`)

Camera nằm trên `SpringArm3D` (không xuyên tường), đi theo hero hơi trễ cho có trọng lượng. Khi hero chạy và người chơi không đụng chuột trong 1,3 s, camera tự xoay dần ra sau lưng (trừ khi hero chạy ngược về phía camera). Đến gần colossus thì camera lùi ra 6,4 m và mở góc 70°. Khi đang bám, camera áp sát còn 3,4 m. Giữ Q để quay về phía boss. Rung màn hình đi qua `h_offset`/`v_offset`, vì vị trí camera do spring arm quyết định.

## Hero (`hero.gd`)

Trạng thái: đất, không trung, Lướt, chém, bám, đâm, ngã, chết. Model là KayKit Knight (cao khoảng 1,85 m).

- **Sức bám** (vòng vàng góc phải dưới, 100 điểm): bám đứng yên tốn 5/s, leo tốn 10/s, colossus lắc mình tốn thêm tới 34/s, gồng đâm tốn thêm 6/s. Hồi 38/s trên đất, 14/s khi đứng trên lưng colossus. Hết sức bám thì rơi.
- **Leo:** bề mặt bám được là va chạm có `meta climbable = true` (lông). Giáp vỏ cây, mặt nạ xương và móng thì không bám được. Điểm bám được lưu theo toạ độ riêng của bộ phận đang bám, nên hero đi theo con quái khi nó bước, quay hay lắc. Leo tới mép thì tự trèo lên mặt trên.
- **Đứng trên lưng:** hero được mang theo bộ phận dưới chân. Khi colossus lắc mình mà không giữ chuột phải thì bị hất văng.
- **Rơi:** rơi quá 7,5 m mất 7 máu mỗi mét.
- Gục ngã thì sau 3,5 s về lại làng.

## Colossus

`Colossus` là lớp nền chung. Thân con quái là các `AnimatableBody3D` trên lớp vật lý `colossus` (layer 4), được đặt lại vị trí mỗi khung hình. Lông có túm hình nón cho ra dáng xù. **Điểm yếu** (`WeakPoint`) là ấn ký phát sáng: điểm *vital* giữ máu boss, điểm thường gây hiệu ứng khi bị phá. Chém kiếm vào da không có tác dụng (chỉ toé lửa). Mọi colossus hồi sinh sau 3 phút như mọi boss trong game.

### Gấu Ma Rừng Già (`gau_ma.gd`)

Cao khoảng 18 m, dài khoảng 30 m, sống trong Hang Gấu Ma (sàn phẳng bán kính 50 m, có 4 Tảng Đá Lớn và tàn tích cột đá). Bốn chân bước theo kiểu IK thủ tục (chân chéo nhau thay phiên), mỗi bước làm rung đất, giẫm trúng thì bị ngã.

- **Đường leo:** lông sau bắp chân sau → mông → lưng → cổ → đỉnh đầu. Hai bên sườn có giáp vỏ cây, ngực có tấm giáp, mặt là mặt nạ xương.
- **Điểm yếu:** bắp chân sau trái (45 máu, phá được thì gấu **Quỵ Gối** khoảng 10 s, lưng hạ xuống thành dốc) · u lưng (120) · đỉnh đầu (120). Đâm hết 2 điểm vital thì gấu ngã xuống.
- **Chiêu** (lấy từ boss 2D, mỗi chiêu có vòng cảnh báo đỏ trên đất và chữ "Kỹ năng: …"):
  - *Vồ*: quét chân trước.
  - *Dậm Đất*: chồm lên, giáng xuống, sóng chấn bán kính 15 m.
  - *Ném Đá Lớn*: ném đá, đá nằm lại thành *Tảng Đá Lớn*, tối đa 4 tảng.
  - *Chụp Quăng*: nhảy vồ. Đáp cạnh một Tảng Đá Lớn thì bị **Choáng** 7 s, đầu gục xuống đất, leo lên đầu được ngay.
  - *Lắc Mình*: khi có người trên lưng.
  - *Cuồng Nộ*: dưới 50% máu, mắt đỏ, nhanh hơn và lắc nhiều hơn.

## Kiểm tra

- **Test** (khoảng 10 s): `Godot_v4.7.2-stable_win64_console.exe --headless --path Godot res://tests/test_runner.tscn`. Mã thoát 0 là đạt. Gồm bố cục thế giới, đất đã bake, giải IK, phím, điểm yếu/quỵ gối/ngã/hồi sinh, sát thương rơi, **leo chân gấu có vật lý thật**, giới hạn số đá.
- **AutoShot** (khoảng 75 s, mở cửa sổ): `Godot_v4.7.2-stable_win64_console.exe --path Godot -- --autoshot --shots-dir D:/shots [--autoshot-only world|climb|skills|brawl]`. Chụp 19 cảnh (làng, rừng, đấu trường, bám chân, đâm, quỵ gối, trên lưng, lắc mình, Dậm Đất, Ném Đá, Chụp Quăng và Choáng), rồi để gấu tự đánh 40 s. Mã 0 là sạch, 1 là có lỗi trong log, 2 là quá giờ.
- Chạy nhanh không cần cửa sổ: `--headless --path Godot --quit-after 200`.

## Lộ trình chuyển cả game

| Giai đoạn | Nội dung | Trạng thái |
|---|---|---|
| 1 · Nền móng | Project, phím, camera SotC, hero (chạy/nhảy/Lướt/combo/bám/leo/đâm/rơi), thế giới 800×512 m theo bố cục Unity, làng, Hang Gấu Ma, **Gấu Ma thành colossus**, HUD, test, AutoShot | **Xong** (14/10/2026) |
| 2 · Chiến đấu & tiến trình | Công thức sát thương, kháng hệ, trạng thái (Bỏng, Lạnh, Tích Điện, Độc…), Trấn Áp, hit-stop 3 mức, Lướt Hoàn Hảo, 8 chiêu và Sách Chiêu, cấp/XP/điểm chỉ số; số liệu chuyển từ `Assets/Data/*.asset` sang JSON bằng script; quái thường của rừng (Slime Rêu, Nấm Độc, Sói…) | Chưa |
| 3 · Đồ, nhiệm vụ, hội thoại | Túi đồ, trang bị, chế tạo, rơi đồ, nhiệm vụ (`QuestDef`), hội thoại Yarn (đọc lại các file `.yarn`), NPC làng, Bách Khoa Trùm, lưu game, các cửa sổ B/I/C, J, K | Chưa |
| 4 · Các vùng | Đầm Lầy (Cóc Tía, Xà Mẫu thành colossus dưới nước), Hang Pha Lê (hang trong lòng núi, Golem Pha Lê Cổ, Nhện Chúa), Thảo Nguyên Gió (gió, Khe Vực, Cột Gió, Bò Rừng Sắt, Thủ Lĩnh Hắc Phong) | Chưa |
| 5 · Nhân vật | Lớp D&D và chủng tộc (KayKit có Knight, Barbarian, Mage, Rogue), tạo nhân vật, màn hình chính xanh lá–vàng | Chưa |
| 6 · Âm thanh | Dùng lại SFX/nhạc từ `Tools/AudioGen`, âm thanh 3D theo vị trí | Chưa |
| 7 · Online | Máy chủ Godot headless luôn bật, tài khoản, kênh, party, chat, máy chủ quyết định luật như bản FishNet | Chưa |
| 8 · Phát hành | Xuất bản Windows (cần tải export templates 1,28 GB), thay bản Unity cho bạn bè | Chưa |

Quy tắc giữ từ bản Unity: luật game không phụ thuộc "hero trên màn hình này" (`Game.hero` chỉ dùng cho camera, HUD và rung; colossus tìm hero qua nhóm `heroes`); thời gian chỉ chậm lại qua `Game.hitstop`/`Game.slow_motion`; mọi phím khai báo trong `controls.gd`; boss luôn hồi sinh.

## Asset bên thứ ba

Tất cả đều **CC0** (dùng tự do, kể cả thương mại, không bắt buộc ghi công). Giấy phép đi kèm từng thư mục:

- **KayKit Adventurers 1.0** và **KayKit Skeletons 1.0** của Kay Lousberg ([kaylousberg.com](https://www.kaylousberg.com)), lấy từ github.com/KayKit-Game-Assets (chỉ thư mục glTF): nhân vật có 76–95 hoạt ảnh, vũ khí.
- **Nature Kit** của Kenney ([kenney.nl](https://kenney.nl)): 329 model cây, đá, cây bụi, đồ vật (chỉ bản GLB).
- Font **Inter** (SIL OFL 1.1), chép từ `Assets/Fonts`.

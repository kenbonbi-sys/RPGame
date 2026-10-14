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
| **Shift** | Bấm là Lướt ngay, cả khi đang trên không (lộn né, bất tử 0,28 s, hồi 2,2 s; lướt trên không gần như không rơi) · giữ quá 0,22 s: chạy nhanh |
| **Chuột trái** | Đòn thường của vũ khí (kiếm: combo Chém Gió 3 đòn). Khi đang bám hoặc đứng trên lưng colossus: giữ để vung kiếm, thả để đâm (giữ càng lâu càng đau) |
| **1 2 3 4 5** · **R** | 5 chiêu của lớp nhân vật · Tuyệt kỹ (bản Unity: W E R A S · D) |
| **Z X C** | Bình Máu · Bình Năng Lượng · Thuốc Thảo Mộc (bản Unity: 1 2 3) |
| **Giữ chuột phải** | Bám: túm lông colossus hoặc đá có thể bám, leo bằng WASD |
| **Q** / chuột giữa | Quay camera nhìn về phía boss |
| **F1** · **F3** · **Esc** | Bảng phím · thông số (FPS, trạng thái) · nhả chuột |

Có hỗ trợ tay cầm: cần trái chạy, cần phải xoay camera, A nhảy, B Lướt, X chém, RB bám, LB nhìn boss.

Chiêu hướng thẳng theo camera, tự bẻ về con quái gần tâm nhìn (khóa mềm 32°); chiêu nhắm điểm rơi vào chỗ tâm màn hình chạm đất, hoặc vào con quái đang được khóa.

## Cấu trúc

```
Godot/
  project.godot        cài đặt (Jolt Physics, Forward+, cửa sổ 1600×900)
  choi.cmd             chạy game / mở editor
  scenes/              main.tscn (thế giới), hero.tscn
  scripts/
    core/              controls.gd (mọi phím, autoload Controls) · db.gd (autoload DB: số liệu) · game.gd (autoload Game: hero trên màn hình, hit-stop, rung)
    combat/            combat.gd · damage_info.gd · health.gd · status_effects.gd · poise.gd · shot.gd
    abilities/         ability.gd · ability_context.gd · ability_runner.gd (14 loại khối) · aim.gd
    progression/       player_stats.gd (6 chỉ số D&D, cấp, XP)
    items/             inventory.gd · loot.gd
    camera/            sotc_camera.gd
    player/            hero.gd · player_skills.gd
    enemies/           colossus.gd (nền chung) · gau_ma.gd · weak_point.gd · boulder.gd · enemy.gd (quái thường) · slime.gd · shroom.gd · enemy_spawner.gd · creature_art.gd
    world/             world_map.gd (bố cục) · terrain*.gd · foliage.gd · village.gd · arena.gd · props.gd · palette.gd · grass.gd
                       mesh_kit.gd (dựng hình bằng code) · tree_art.gd (cây, bụi) · house_art.gd (nhà, sạp, thùng, cờ, rào)
    ui/hud.gd · vfx/fx.gd · debug/autoshot.gd
  shaders/             đất, nước, lông colossus, ấn ký điểm yếu, tán cây (canopy), bề mặt vẽ tay (painted), đá, cỏ, ánh sáng chung (stylized_light)
  world/               terrain_data.res (đất đã bake) · map_preview.png · environment.tres (trời, sương, màu)
  data/                số liệu chuyển từ Unity (abilities, items, classes, races, quests, combat, progression .json)
  assets/third_party/  model CC0 (xem cuối trang)
  tests/               test_runner.tscn
  tools/               bake_world.gd · list_anims.gd · measure.gd
```

## Số liệu: một nguồn cho cả hai bản

Số liệu nằm trong các ScriptableObject của bản Unity (`Assets/Data`). `python Tools/GodotData/unity_to_json.py` chuyển chúng thành `Godot/data/*.json`: 92 chiêu, 88 vật phẩm, 12 lớp, 9 chủng tộc, 30 nhiệm vụ, `Combat` và `Progression`. Khối hiệu ứng `[SerializeReference]` được ghi kèm `$type`, tham chiếu sang asset khác thành `{"$ref": "abilities/fireball"}`, mảng số Unity nén hex được giải ra. **Đừng sửa tay file JSON:** sửa asset bên Unity (hoặc bảng Balance) rồi chạy lại script. Lưu ý: JSON của Godot đọc mọi số thành float, nên khi so sánh với số nguyên phải ép `int()`.

## Thế giới

`WorldMap` giữ nguyên bố cục của `WorldBuilder.cs` bản Unity (cùng các mốc tính theo ô 200×128), **1 ô = 4 m**, nên thế giới rộng 800×512 m: Làng Lá Xanh ở tây nam, đường mòn qua các bãi quái tới Hang Gấu Ma, đường phía đông sang Đầm Lầy Sương Mù (ao, hồ Xà Mẫu với 4 gò đất), phía bắc là vách cao nguyên Thảo Nguyên Gió và khối núi Pha Lê. Viền bản đồ là núi.

- Đất là một hàm độ cao (`WorldMap.height`, `WorldMap.splat`) được **bake** thành `world/terrain_data.res`. Đổi bố cục xong thì chạy lại:
  `Godot_v4.7.2-stable_win64_console.exe --headless --path Godot -s tools/bake_world.gd` (khoảng 4 giây, vẽ luôn `map_preview.png`). Test `baked land matches WorldMap` báo khi quên bake.
- `Terrain` dựng lưới 64 m mỗi mảnh và một va chạm heightmap 1 m. `Foliage` rải cây, đá, bụi, nấm và hoa theo hạt giống cố định. Cây và đá lớn có va chạm.
- **Nhìn như Dauntless:** ánh sáng kiểu vẽ tay dùng chung (`shaders/stylized_light.gdshaderinc`): ranh sáng–tối mềm, bóng ngả xanh lam chứ không xám, viền sáng ở mép. Tán cây dùng pháp tuyến "khối cầu" nên đổ bóng tròn như một khối lá, mặt dưới tối và có ánh xuyên lá (`leaves.gdshader`). Đá ấm, có rêu trên mặt hướng lên (`rock.gdshader`). Đất xanh tươi, đường đất ấm (`terrain.gdshader`). Trời xanh đậm, sương nhẹ theo khoảng cách, bloom và SSAO (`world/environment.tres`).
- **Cỏ** (`grass.gd`, `grass.gdshader`): lưới ô 16 m quanh camera (7×7 ô, mỗi ô tới 520 bụi, mỗi bụi 7 lá), chỉ mọc trên đất cỏ phẳng, không mọc trên đường, đá, bùn, nước. Cỏ lay theo gió, rẽ ra quanh hero, nhỏ dần rồi biến mất ở 34–46 m.
- **Cây và bụi** (`tree_art.gd`, `canopy.gdshader`): dựng bằng code chứ không dùng model Kenney. Thân to thuôn dần, rễ xoè, vài cành vươn vào tán; tán là 6–10 cục phồng (icosphere đẩy bằng nhiễu) có pháp tuyến ngả ra từ tâm cả tán, nên cả tán đổ bóng như một khối vẽ tay. Màu đỉnh–giữa–dưới, độ sâu trong tán và sắc từng cục nằm trong màu đỉnh của lưới. Các loại: sồi, sồi sẫm, cây tròn, cây cao, cây hoa hồng (4% trong rừng), thông tầng, cây đầm lầy cong có rêu rủ, keo thảo nguyên tán bằng, bụi. Mỗi loại 2–4 biến thể, mỗi biến thể có bản gần và bản xa (ít đa giác) đổi nhau ở 150 m (bụi 70 m).
- `mesh_kit.gd` là bộ dựng hình dùng chung: khối hộp bo cạnh, cục phồng, ống thuôn có uốn, lăng trụ tam giác; tự sửa chiều mặt tam giác.
- Đá, khúc gỗ, nấm, hoa vẫn là Kenney, đổi màu trong `palette.gd`.

### Làng Lá Xanh (`village.gd`, `house_art.gd`)

Bảy ngôi nhà khung gỗ quanh bãi cỏ giữa làng, dựng bằng code: móng đá có hai hàng đá nhô, tường vữa kem trong khung gỗ sẫm (cột, giằng chéo, dầm), tầng hai nhô ra có đầu dầm lộ, mái dốc lợp **từng viên ngói** (đỏ cam, xanh lam, xanh rêu...) có rêu, ván diềm, hai thanh gỗ bắt chéo ở đầu hồi, ống khói đá bốc khói, cửa ván trên hai bậc đá với đèn lồng sáng, cửa sổ có khung, cánh chớp mở và hộp hoa. Thêm cây sồi lớn giữa làng, hai sạp chợ mái vải sọc, thùng gỗ, thùng thưa, bốn cột cờ màu làng, hàng rào ruộng lúa. Mọi thứ dùng `painted.gdshader` (màu theo đỉnh lưới, vân vẽ tay, rêu trên mặt hướng lên).

## Giao diện kiểu Dauntless (`hud.gd`)

Font **Barlow Condensed** (SemiBold, Bold, ExtraBold), khung xiên như Dauntless.

- **Góc trái trên:** chân dung hero quay trực tiếp (một camera nhỏ chỉ thấy lớp 11), vòng quanh chân dung là máu; huy hiệu cấp; tên và lớp; thanh máu xanh ngọc (phần xanh lam là lượng sát thương đang được buff đỡ, phần trắng là máu vừa mất đang rút dần); thanh vàng là sức bám; năng lượng chia ô 10 điểm; vạch kinh nghiệm; ô buff với số giây còn lại.
- **Giữa trên:** la bàn B, ĐB, Đ, ĐN, N, TN, T, TB theo hướng camera; chấm xanh là làng, hình thoi đỏ là colossus. Khi colossus đang săn hero thì tên và thanh máu của nó hiện ngay dưới la bàn, còn lại là tên vùng.
- **Phải trên:** biểu tượng tam giác ngược với phần trăm máu còn lại của colossus.
- **Giữa dưới:** 8 ô chiêu có biểu tượng theo hệ (kiếm, lửa, băng, sét, độc, thánh, bóng tối, hỗ trợ, di chuyển), phím, năng lượng tốn, hồi chiêu dạng màn che có số giây, tên chiêu.
- **Phải dưới:** 3 bình thuốc Z X C và số lượng.
- **Trái dưới:** nhật ký sự kiện (nhặt đồ, hạ quái, kinh nghiệm, lên cấp, boss phát hiện) qua `Game.log(text, color)` / tín hiệu `Game.message`, mờ đi sau 8 s; dòng "[Enter] Để trò chuyện" dành cho bản online.
- **Bảng tên quái** (`nameplate.gd`): cả bảng xoay theo camera nên nền và thanh máu luôn khớp nhau; phần vàng là máu vừa mất rút dần. Số sát thương dùng Barlow ExtraBold.

## Camera kiểu SotC (`sotc_camera.gd`)

Camera nằm trên `SpringArm3D` (không xuyên tường), đi theo hero hơi trễ cho có trọng lượng. Khi hero chạy và người chơi không đụng chuột trong 1,3 s, camera tự xoay dần ra sau lưng (trừ khi hero chạy ngược về phía camera). Đến gần colossus thì camera lùi ra 6,4 m và mở góc 70°. Khi đang bám, camera áp sát còn 3,4 m. Giữ Q để quay về phía boss. Rung màn hình đi qua `h_offset`/`v_offset`, vì vị trí camera do spring arm quyết định.

## Hero (`hero.gd`)

Trạng thái: đất, không trung, Lướt, chém, bám, đâm, ngã, chết.

**Model** (`hero_model.gd`): bộ đồ **Ranger** của Quaternius (Modular Character Outfits – Fantasy), đầu, mắt, tóc lấy từ Universal Base Characters (phần đầu và cổ được cắt khỏi thân theo trọng số xương), khăn quàng đỏ và thanh kiếm bản to gắn vào xương tay phải. Hoạt ảnh lấy từ Universal Animation Library 1 và 2 (cùng bộ xương kiểu Unreal: `pelvis`, `spine_01`, `upperarm_l`...): chỉ chép xoay của từng xương và vị trí hông (đã đổi tỉ lệ), bỏ xương gốc và scale. `ALIASES` ánh xạ tên hoạt ảnh cũ (KayKit) sang tên trong UAL, nên `hero.gd` không phải đổi. Lưới của hero nằm ở lớp hiển thị 2 (không dính decal) và lớp 11 (chỉ hero, cho camera chân dung trên HUD).

- **Sức bám** (thanh vàng dưới thanh máu, 100 điểm): bám đứng yên tốn 5/s, leo tốn 10/s, colossus lắc mình tốn thêm tới 34/s, gồng đâm tốn thêm 6/s. Hồi 38/s trên đất, 14/s khi đứng trên lưng colossus. Hết sức bám thì rơi.
- **Leo:** bề mặt bám được là va chạm có `meta climbable = true` (lông). Giáp vỏ cây, mặt nạ xương và móng thì không bám được. Điểm bám được lưu theo toạ độ riêng của bộ phận đang bám, nên hero đi theo con quái khi nó bước, quay hay lắc. Leo tới mép thì tự trèo lên mặt trên.
- **Đứng trên lưng:** hero được mang theo bộ phận dưới chân. Khi colossus lắc mình mà không giữ chuột phải thì bị hất văng.
- **Rơi:** rơi quá 7,5 m mất 7 máu mỗi mét.
- Gục ngã thì sau 3,5 s về lại làng.

## Chiến đấu (giai đoạn 2)

Theo đúng luật bản Unity, đã đối chiếu từng con số:

- **Sát thương:** `power × Attack × (1 + 12%/cấp chiêu) × thưởng sát thương × (×1,3 sau Lướt Hoàn Hảo)`, chí mạng tính riêng từng mục tiêu, rồi bên nhận: bất tử thì né, giáp `armor / (armor + 50 + 5 × cấp kẻ đánh)` (giảm mọi hệ), kháng hệ (−50%..75%), dao động ±5%, làm tròn kiểu banker, tối thiểu 1. Đòn kết liễu không gây trạng thái.
- **Trạng thái:** Bỏng, Lạnh (4 tầng thành Đóng Băng), Tích Điện (3 tầng thành Phóng Điện), Độc, Choáng, Trói, Làm Chậm, Nguyền, Phán Xét; khống chế lặp lại trong 6 s ngắn đi ×0,6; Thanh Trấn Áp (vỡ: choáng 3 s, nhận thêm 50%); hit-stop 35/70/120 ms.
- **Nhân vật:** 6 chỉ số D&D (lớp đặt mảng 15/14/13/12/10/8, chủng tộc cộng thêm), cấp 1–40, XP `50 × cấp^1,6`, máu/năng lượng/công/giáp/chí mạng/hồi chiêu theo `Progression`. Tạm thời hero là **Chiến Binh loài người cầm kiếm** cho tới khi có màn tạo nhân vật (giai đoạn 5). Năng lượng hồi 3,5/s; máu hồi 2,5/s sau 5 s không trúng đòn.
- **Chiêu:** đủ 14 loại khối của bản Unity (Cue, Damage, Projectile, Dash, Heal, Buff, Line, Burst, Pulse, Combo, Chain, Beam, Orb, Clone), nên cả 92 chiêu đều chạy được từ dữ liệu. Thanh 8 ô (đòn thường theo vũ khí, 5 chiêu, Tuyệt kỹ, Lướt), hồi chiêu theo chiêu, hồi chung 0,08 s, bộ đệm phím 150 ms, chiêu lướt cắt ngang được tư thế sau thời điểm cam kết. Khoảng cách trong dữ liệu nhân `AbilityRunner.SPACE = 1,25` cho hợp góc nhìn 3D. Hiệu ứng hình dựng theo từ khóa tên VFX (`skill_fx.gd`).
- **Lướt Hoàn Hảo:** đòn đánh trúng trong 0,15 s đầu của Lướt: chậm thời gian 35% trong 0,3 s, +15 năng lượng, chiêu kế tiếp ×1,3 và +25 Trấn Áp.
- **Túi đồ và rơi đồ:** 48 ô, vàng; đồ rơi nảy ra rồi bay về hero trong 3,5 m. Bộ đồ khởi đầu như bản Unity (4 Bình Máu, 4 Bình Năng Lượng, 2 Thuốc Thảo Mộc, 3 Táo).

### Quái rừng

Bản Unity chỉ có hai loài trong rừng; cả hai đã sang 3D (dựng bằng code), đúng 5 bãi của `WorldBuilder` (9 Slime Rêu, 5 Nấm Độc, hồi sau 25 s khi không có ai đứng gần):

- **Slime Rêu** (cấp 2, 60 máu): nhảy cóc đuổi theo, ngồi thụp 0,25 s rồi lao tới (10 sát thương, đẩy lùi 5), chạm vào người gây 6. Rơi Gel Slime, vàng, đôi khi Bình Máu. 16 XP.
- **Nấm Độc** (cấp 3, 48 máu, yếu Lửa 30%): giữ khoảng cách, lùi khi bị áp sát, đi ngang, bắn bào tử (9 sát thương Độc, chậm 35% trong 1,8 s). Rơi Mũ Nấm Đỏ, vàng, Bình Năng Lượng, Cỏ Thuốc. 20 XP.

Hình dáng (`creature_art.gd`): Slime là thạch trong suốt có ánh ướt, sỏi và cành con mắc bên trong, mũ rêu có mầm lá và một cây nấm đỏ tí hon, mắt to có chấm sáng và lông mày cau. Nấm Độc có mũ tím sần với các đốm nổi, phiến nấm bên dưới, chân rễ, tay ngắn. Mặt quái nhìn về −Z; trước đây model bị quay ngược nên quái đi lùi, đã sửa trong `enemy.gd`.

Vùng phát hiện, bán kính dây xích, lang thang và ngủ được nhân `Enemy.AREA = 1,6` vì thế giới 3D rộng hơn; tầm đánh và đạn giữ nguyên. Sói, Ong Bắp Cày, Cây Ma, Ma Rừng, Sói Đầu Đàn, Vua Slime Hoàng Kim mới nằm trong kế hoạch của bản Unity, chưa có code nên cũng chưa chuyển.

## Colossus

`Colossus` là lớp nền chung. Thân con quái là các `AnimatableBody3D` trên lớp vật lý `colossus` (layer 4), được đặt lại vị trí mỗi khung hình. Lông vẽ tay (`fur.gdshader`): vệt lông mềm, lưng sáng bụng sẫm, rêu mọc trên lưng; các lọn lông cong dày phủ quanh thân (`Colossus.lock_mesh`). Giáp là tấm bo cạnh màu vỏ cây có rêu, mặt nạ xương và móng cũng dùng `painted.gdshader`. **Điểm yếu** (`WeakPoint`) là ấn ký phát sáng: điểm *vital* giữ máu boss, điểm thường gây hiệu ứng khi bị phá. Chém kiếm vào da không có tác dụng (chỉ toé lửa). Mọi colossus hồi sinh sau 3 phút như mọi boss trong game.

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

- **Test** (khoảng 20 s, 113 kiểm tra): `Godot_v4.7.2-stable_win64_console.exe --headless --path Godot res://tests/test_runner.tscn`. Mã thoát 0 là đạt. Gồm bố cục thế giới, đất đã bake, giải IK, phím, điểm yếu/quỵ gối/ngã/hồi sinh, sát thương rơi, **leo chân gấu có vật lý thật**, giới hạn số đá; công thức sát thương (đúng ví dụ trong test Unity: Cầu Lửa 56 vào điểm yếu Lửa ra 73, có giáp ra 64), bảng XP, chỉ số cấp 1 của 9 lớp, các trạng thái, Trấn Áp, khống chế lặp lại, hero chém chết slime, Cầu Lửa gây Bỏng, bào tử làm chậm, Lướt Hoàn Hảo.
- **AutoShot** (khoảng 75 s, mở cửa sổ): `Godot_v4.7.2-stable_win64_console.exe --path Godot -- --autoshot --shots-dir D:/shots [--autoshot-only world|forest|climb|skills|brawl]`. Chụp 29 cảnh (làng, hai góc nhà trong làng, rừng, đấu trường; bãi slime, Chém Gió, đồ rơi, Chém Xoáy, Nấm Độc bắn, Cầu Lửa, Lôi Phạt; bám chân, đâm, quỵ gối, trên lưng, lắc mình, Dậm Đất, Ném Đá, Chụp Quăng và Choáng), rồi để gấu tự đánh 40 s. Mã 0 là sạch, 1 là có lỗi trong log, 2 là quá giờ.
- Chạy nhanh không cần cửa sổ: `--headless --path Godot --quit-after 200`.

## Lộ trình chuyển cả game

| Giai đoạn | Nội dung | Trạng thái |
|---|---|---|
| 1 · Nền móng | Project, phím, camera SotC, hero (chạy/nhảy/Lướt/combo/bám/leo/đâm/rơi), thế giới 800×512 m theo bố cục Unity, làng, Hang Gấu Ma, **Gấu Ma thành colossus**, HUD, test, AutoShot | **Xong** (14/10/2026) |
| 2 · Chiến đấu & tiến trình | Công thức sát thương, kháng hệ, trạng thái, Trấn Áp, hit-stop 3 mức, Lướt Hoàn Hảo, cả 92 chiêu chạy từ dữ liệu, cấp/XP, 6 chỉ số D&D; số liệu chuyển từ `Assets/Data` bằng script; Slime Rêu và Nấm Độc; túi đồ, rơi đồ, bình thuốc | **Phần lớn xong** (15/10/2026). Còn: cửa sổ cộng điểm chỉ số, Sách Chiêu (K), cấp chiêu |
| 2b · Art kiểu Dauntless | Hero Quaternius (bộ đồ, đầu, tóc, hoạt ảnh UAL), ánh sáng kiểu vẽ tay, tán cây tròn, cỏ dày, đá rêu, trời và sương; HUD như Dauntless; bảng tên quái sửa lệch thanh máu; Lướt cả trên không | **Phần lớn xong** (15/10/2026): thêm nhà làng, cây và bụi dựng bằng code, slime/nấm/Gấu Ma vẽ tay. Còn: vách núi và đá còn góc cạnh, thân Gấu Ma vẫn ghép từ khối đơn giản, NPC trong làng |
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
- **Universal Base Characters**, **Universal Animation Library** 1 và 2, **Modular Character Outfits – Fantasy** của Quaternius ([quaternius.com](https://quaternius.com)): thân, đầu, tóc, bộ đồ Ranger/Peasant, khoảng 120 hoạt ảnh. Ảnh lớn hơn 2048 px được thu nhỏ còn 2048.
- Font **Inter** (SIL OFL 1.1), chép từ `Assets/Fonts`.
- Font **Barlow Condensed** của Jeremy Tribby (SIL OFL 1.1, `assets/fonts/BarlowCondensed-OFL.txt`), từ Google Fonts.

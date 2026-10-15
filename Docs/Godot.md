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
| **Chuột phải** | **Đòn nặng** của vũ khí đang cầm (kiếm: Trảm): giữ để tụ lực (thanh tụ lực hiện trên thanh chiêu), thả để ra đòn. Lướt huỷ được lúc đang tụ |
| **V** | Đổi sang vũ khí kế tiếp (tay cầm: phải của D-pad) |
| **Giữ E** | Bám: túm lông colossus hoặc đá có thể bám, leo bằng WASD (trước đây là chuột phải) |
| **Q** / chuột giữa | Quay camera nhìn về phía boss |
| **F1** · **F3** · **Esc** | Bảng phím · thông số (FPS, trạng thái) · nhả chuột |

Có hỗ trợ tay cầm: cần trái chạy, cần phải xoay camera, A nhảy, B Lướt, X chém, RT Trảm, RB bám, LB nhìn boss.

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
                       mesh_kit.gd (dựng hình bằng code) · tree_art.gd (cây, bụi) · rock_art.gd (đá, khối vách) · house_art.gd (nhà, sạp, thùng, cờ, rào)
    ui/hud.gd · ui/game_icons.gd (biểu tượng vẽ bằng SVG) · vfx/fx.gd · debug/autoshot.gd
  shaders/             đất, nước, lông colossus, ấn ký điểm yếu, tán cây (canopy), bề mặt vẽ tay (painted), đá, cỏ, ánh sáng chung (stylized_light)
  world/               terrain_data.res (đất đã bake) · map_preview.png · environment.tres (trời, sương, màu)
  data/                số liệu chuyển từ Unity (abilities, items, classes, races, quests, combat, progression .json)
  assets/creatures/    slime.glb · shroom.glb · gau_ma.glb, nặn bằng Tools/Sculpt (xem dưới)
  assets/third_party/  model CC0 (xem cuối trang)
  tests/               test_runner.tscn
  tools/               bake_world.gd · list_anims.gd · measure.gd · model_shots.tscn (chụp một model) · icon_sheet.tscn (bảng mọi biểu tượng)
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
- **Đá** (`rock_art.gd`): một quả cầu bị 13–15 mặt phẳng ngẫu nhiên cắt, nên có những mặt phẳng lớn, cạnh bo mềm, đỉnh phẳng cho rêu mọc và đáy phẳng cắm xuống đất. Mỗi mặt một sắc ấm hoặc lạnh, khối vách có thêm vân tầng. Các loại: tảng tròn, phiến, cột, khối vách. Va chạm là khối lồi tính đúng từ các mặt cắt (`RockArt.hull`), vì Jolt không dựng được khối lồi từ quá nhiều điểm gần đồng phẳng. Dùng cho đá rải khắp nơi, 4 Tảng Đá Lớn trong hang và đá Gấu Ma ném.
- **Vách núi:** mọi sườn dốc (pháp tuyến y < 0,8, trừ đường đi) được cắm các khối đá vách lớn 6–20 m, nghiêng theo sườn và lún sâu (`Foliage._scatter_cliffs`), nên sườn núi thành vách đá xếp chồng chứ không phải mặt đất bị kéo dài. Shader đất vẽ đá thành từng tầng: mỗi tầng một sắc, nếp tối bên dưới, gờ sáng bên trên, vài vết nứt dọc.
- Khúc gỗ, nấm, hoa vẫn là Kenney, đổi màu trong `palette.gd`.

### Làng Lá Xanh (`village.gd`, `house_art.gd`)

Bảy ngôi nhà khung gỗ quanh bãi cỏ giữa làng, dựng bằng code: móng đá có hai hàng đá nhô, tường vữa kem trong khung gỗ sẫm (cột, giằng chéo, dầm), tầng hai nhô ra có đầu dầm lộ, mái dốc lợp **từng viên ngói** (đỏ cam, xanh lam, xanh rêu...) có rêu, ván diềm, hai thanh gỗ bắt chéo ở đầu hồi, ống khói đá bốc khói, cửa ván trên hai bậc đá với đèn lồng sáng, cửa sổ có khung, cánh chớp mở và hộp hoa. Thêm cây sồi lớn giữa làng, hai sạp chợ mái vải sọc, thùng gỗ, thùng thưa, bốn cột cờ màu làng, hàng rào ruộng lúa. Mọi thứ dùng `painted.gdshader` (màu theo đỉnh lưới, vân vẽ tay, rêu trên mặt hướng lên).

## Giao diện kiểu Dauntless (`hud.gd`)

Font **Barlow Condensed** (SemiBold, Bold, ExtraBold), khung xiên như Dauntless.

- **Góc trái trên:** chân dung hero quay trực tiếp (một camera nhỏ chỉ thấy lớp 11), vòng quanh chân dung là máu; huy hiệu cấp; tên và lớp; thanh máu xanh ngọc (phần xanh lam là lượng sát thương đang được buff đỡ, phần trắng là máu vừa mất đang rút dần); thanh vàng là sức bám; năng lượng chia ô 10 điểm; vạch kinh nghiệm; ô buff với số giây còn lại.
- **Giữa trên:** la bàn B, ĐB, Đ, ĐN, N, TN, T, TB theo hướng camera; chấm xanh là làng, hình thoi đỏ là colossus. Khi colossus đang săn hero thì tên và thanh máu của nó hiện ngay dưới la bàn, còn lại là tên vùng.
- **Phải trên:** biểu tượng tam giác ngược với phần trăm máu còn lại của colossus.
- **Giữa dưới:** 8 ô chiêu, mỗi chiêu một biểu tượng riêng (xem dưới), phím, năng lượng tốn, hồi chiêu dạng màn che có số giây, tên chiêu.
- **Phải dưới:** 3 bình thuốc Z X C và số lượng.
- **Trái dưới:** nhật ký sự kiện (nhặt đồ, hạ quái, kinh nghiệm, lên cấp, boss phát hiện), mỗi dòng có biểu tượng (đồng vàng, ngôi sao kinh nghiệm, đầu lâu, bình thuốc, giọt gel...), qua `Game.log(text, color, icon)` / tín hiệu `Game.message`; nhặt cùng một thứ liên tiếp thì cộng dồn ("+1 vàng" hai lần thành "+2 vàng"); mờ đi sau 8 s; dòng "[Enter] Để trò chuyện" dành cho bản online.

**Biểu tượng** (`game_icons.gd`): khoảng 70 hình vẽ bằng đường SVG trong khung 100 × 100 (kiếm, rìu, dao găm, cung tên, khiên, tim, lửa, bông tuyết, tia sét, đầu lâu, trăng, mặt trời, mắt, bàn tay, nắm đấm, cánh, ủng, vòng xoáy, nốt nhạc, lá, dây gai, xúc tu, quả cầu, sao chổi, mũi lướt, sóng âm, giọt, bẫy, mây, cột sáng, bình thuốc, đá quý, đồng vàng...). Mỗi chiêu trong 92 chiêu có công thức riêng theo id (`SKILLS`, trùng tên sprite `sk_<id>` của bản Unity): một hình chính sáng màu, viền tối, quầng sáng theo hệ, cộng một hình nền cùng màu hệ phía sau (Chém Gió = kiếm + vệt chém, Xung Phong = khiên + mũi lướt, Bão Kiếm = ba thanh kiếm trong vòng xoáy...). Vật phẩm lấy hình theo id (bình thuốc theo màu, gel, nấm, cỏ, đá quý, giáp, ủng, bí kíp mang màu hệ của chiêu...). Godot vẽ SVG thành ảnh lúc chạy (`Image.load_svg_from_string`) và giữ lại. Xem cả bộ: `Godot_console.exe --headless --path Godot res://tools/icon_sheet.tscn -- --out D:/icons.png`.
- **Bảng tên quái** (`nameplate.gd`): cả bảng xoay theo camera nên nền và thanh máu luôn khớp nhau. Số sát thương dùng Barlow ExtraBold.
- **Thanh máu biết đau** (`juicy_bar.gd`, dùng cho thanh máu hero, thanh máu boss dưới la bàn và bảng tên quái): không nhảy ngay tới giá trị mới mà rút dần trong 0,75 s; một bóng mờ màu kem giữ nguyên chỗ cũ 0,4 s rồi rút theo, cho thấy vừa mất bao nhiêu; lúc trúng đòn thanh loé trắng và bị xô ngang rồi rung tắt dần; màu chuyển sang hổ phách khi còn 50% máu, đỏ khi còn 25%. Màu lúc khoẻ: hero xanh ngọc, quái và boss xanh lá.

## Vũ khí kiểu Dauntless (`weapons.gd`, `weapon_art.gd`)

Bảy họ vũ khí của Dauntless, mỗi họ đứng trên một vũ khí của bản Unity (`PlayerStats.WEAPONS` giữ Tấn công và combo đòn thường, nên số liệu vẫn một nguồn). Họ quyết định đòn trúng colossus thế nào, hoạt ảnh combo, tư thế đứng và đòn nặng chuột phải:

| Họ | Vũ khí Unity · đòn thường | Đòn nặng (giữ chuột phải) | Kiểu |
|---|---|---|---|
| Kiếm | sword · Chém Gió | Trảm: chém bổ, nón 2,7 m | chém (+50% bộ phận) |
| Rìu | axe · Bổ Rìu | Bổ Núi: tụ 1,2 s, 1,8–4,2× Tấn công, nón rộng 3 m | chém |
| Búa | mace · Nện | Nện Aether: đập đất trước mặt, vùng 3,2 m, trấn áp 70–180 | cùn (trấn áp ×1,6) |
| Song Liêm | dagger · Đâm Nhanh | Lốc Liêm: xoay 3 vòng quanh mình | chém, trấn áp ×0,8 |
| Thương | spear · Đâm Thẳng | Xuyên Phá: lao tới 3,2 m rồi đâm, tầm 4,4 m | đâm (+25% bộ phận) |
| Găng Aether | fist · Liên Quyền | Quyền Aether: lao 2,6 m, cú đấm trấn áp 60–140 | cùn (trấn áp ×1,6) |
| Súng Ostia | bow · Bắn Tên | Phát Tụ Lực: viên đạn xuyên 30 m | đâm, trấn áp ×0,7 |

Mọi đòn nặng đều là đòn nặng (gián đoạn được Húc nếu trúng đầu). Đòn thường và đòn nặng của vũ khí mang nguyên tố của vũ khí và nạp thanh nguyên tố (xem dưới). Mô hình dựng từng mặt như thanh kiếm: thép vát cạnh sáng, gỗ, vàng, và phần phát sáng màu nguyên tố (rãnh lưỡi, mép rìu, lõi búa, vòng thương, ngọc găng, vòng nòng súng); song liêm, găng và súng cầm hai tay. Găng đứng thủ quyền, súng đứng cầm súng. Xem cả giá vũ khí: `--path Godot res://tools/model_shots.tscn -- --model rack --shots-dir D:/shots`.

Hero mang sẵn kho bảy món (một họ cho mỗi nguyên tố) cho tới khi có lò rèn: Kiếm Hồng Anh (Vô Hệ), Rìu Hoả Ngục (Hoả), Búa Băng Phong (Băng), Song Liêm Lôi Điểu (Lôi), Thương Mộc Lâm (Thổ), Găng Thánh Quang (Quang), Súng Hư Ảnh (Ám). HUD có thẻ vũ khí trên thanh chiêu (biểu tượng, tên, họ, nguyên tố, tên đòn nặng, [V]).

## Nguyên tố (`elements.gd`)

Sáu nguyên tố của Dauntless, đi trên nguyên tố chiến đấu sẵn có: Hoả = Lửa, Băng = Băng, Lôi = Lôi, **Thổ** (mới, chỉ bản 3D), Quang = Thánh Mộc, Ám = Ám. Cặp đối nghịch: Hoả ↔ Băng, Lôi ↔ Thổ, Quang ↔ Ám. Đòn mang nguyên tố đối nghịch với quái mạnh hơn **+50%**, cùng nguyên tố với quái yếu đi **−20%** (Dauntless: "rất mạnh" và "hơi yếu"; wiki không đọc được nên chọn số này). Áp cho mọi sát thương của các hệ đó, cả chiêu. Quái: Slime Rêu và Gấu Ma là Thổ (vũ khí Lôi khắc), Nấm Độc là Ám (vũ khí Quang khắc). Nguyên tố hiện sau tên trên bảng tên quái và tên boss.

**Kích hoạt** (proc): đòn của vũ khí nạp thanh nguyên tố trên mục tiêu; đủ 12% máu của nó (ít nhất 30) thì kích hoạt, 6 s mới kích hoạt lại được; không bao giờ kích hoạt trên quái cùng nguyên tố:

- **Hoả · Thiêu Đốt:** cháy 3 tầng (sát thương theo thời gian).
- **Băng · Băng Giá:** chậm 45% trong 5 s; colossus làm mọi thứ chậm lại (bước, chiêu, quay đầu).
- **Lôi · Tê Liệt:** quái choáng 2 s; colossus choáng 2,5 s và nhận sát thương lúc choáng.
- **Thổ · Mầm Sống:** rơi 3 quả cầu xanh quanh mục tiêu, chạm vào hồi 12% máu (15 s).
- **Quang · Ấn Quang:** dấu mặt trời trên đầu 12 s; nếu quái ra Chiêu Bất Ổn (Húc của Gấu Ma) hay quái nhỏ ra đòn thì dấu nổ, gây ít sát thương và **gián đoạn** nó; không thì nổ nhẹ khi hết giờ.
- **Ám · Nang Ám:** trên colossus mọc một nang tím trên một bộ phận ngẫu nhiên trong 30 s (máu bằng 4% của colossus); mỗi đòn trúng nang nổ sát thương bộ phận quanh nó, phá nang thì nổ to (sát thương Ám vào colossus và sát thương bộ phận nặng). Trên quái nhỏ: một cục tối phình 3 s rồi nổ, đánh mọi quái trong 3,5 m.

## Cảm giác đòn đánh

Mỗi đòn trúng (`Combat.show_hit`):

- **Loé trắng:** quái thường trắng hẳn trong 0,06–0,08 s rồi tắt nhanh (`Enemy.flash`). Colossus chỉ loé nhẹ cả thân (22%), vì loé trắng hẳn một thứ cao 10 m làm chói cả màn hình.
- **Tiếng:** một tiếng "chát" gọn đúng khung hình trúng đòn (4 bản `whack_1..4` đổi nhau, đòn nặng và chí mạng dùng `whack_heavy`); hero trúng đòn kêu `whack_hero`; vỡ bộ phận `crunch`; gián đoạn `interrupt`. Âm thanh tạo bằng `python Tools/AudioGen/gen_godot_sfx.py` (cùng bộ tổng hợp với âm thanh bản Unity), phát qua `Sfx` (12 loa 3D, cùng một tiếng trong 30 ms chỉ phát một lần).
- **Hạt văng theo hướng đòn:** tia lửa nhỏ bắn ra phía bên kia của mục tiêu theo đúng hướng nhát chém, kèm một ngôi sao loé ở điểm trúng (0,1 s).
- **Rung và khựng:** camera giật nhẹ khoảng 5 khung hình (`Game.kick`, riêng với rung chậm `Game.shake`), và mọi đòn của hero làm thời gian khựng lại ít nhất 0,05 s (khoảng 3 khung hình), đòn nặng và chí mạng lâu hơn.
- Xem từng khung hình của một đòn: `--path Godot res://tools/model_shots.tscn -- --model juice --shots-dir D:/shots` (0, 30, 60, 100, 160 ms).

## Camera kiểu SotC (`sotc_camera.gd`)

Camera nằm trên `SpringArm3D` (không xuyên tường), đi theo hero hơi trễ cho có trọng lượng. Khi hero chạy và người chơi không đụng chuột trong 1,3 s, camera tự xoay dần ra sau lưng (trừ khi hero chạy ngược về phía camera). Đến gần colossus thì camera lùi ra 6,4 m và mở góc 70°. Khi đang bám, camera áp sát còn 3,4 m. Giữ Q để quay về phía boss. Rung màn hình đi qua `h_offset`/`v_offset`, vì vị trí camera do spring arm quyết định.

## Hero (`hero.gd`)

Trạng thái: đất, không trung, Lướt, chém, bám, đâm, ngã, chết.

**Model** (`hero_model.gd`): bộ đồ **Ranger** của Quaternius (Modular Character Outfits – Fantasy), đầu, mắt, tóc lấy từ Universal Base Characters (phần đầu và cổ được cắt khỏi thân theo trọng số xương), khăn quàng đỏ và thanh kiếm gắn vào xương tay phải. Kiếm dựng từng mặt (`HeroModel.sword_mesh`): lưỡi tiết diện hình thoi có cạnh vát sáng, rãnh máu sẫm, mũi nhọn thật, chắn tay mạ vàng hai đầu vểnh về phía mũi, hai viên ngọc, chuôi quấn da từng khoanh, núm chuôi nhiều mặt. Hoạt ảnh lấy từ Universal Animation Library 1 và 2 (cùng bộ xương kiểu Unreal: `pelvis`, `spine_01`, `upperarm_l`...): chỉ chép xoay của từng xương và vị trí hông (đã đổi tỉ lệ), bỏ xương gốc và scale. `ALIASES` ánh xạ tên hoạt ảnh cũ (KayKit) sang tên trong UAL, nên `hero.gd` không phải đổi. Lưới của hero nằm ở lớp hiển thị 2 (không dính decal) và lớp 11 (chỉ hero, cho camera chân dung trên HUD).

- **Sức bám** (thanh vàng dưới thanh máu, 100 điểm): bám đứng yên tốn 5/s, leo tốn 10/s, colossus lắc mình tốn thêm tới 34/s, gồng đâm tốn thêm 6/s. Hồi 38/s trên đất, 14/s khi đứng trên lưng colossus. Hết sức bám thì rơi.
- **Leo:** bề mặt bám được là va chạm có `meta climbable = true` (lông). Giáp vỏ cây, mặt nạ xương và móng thì không bám được. Điểm bám được lưu theo toạ độ riêng của bộ phận đang bám, nên hero đi theo con quái khi nó bước, quay hay lắc. Leo tới mép thì tự trèo lên mặt trên.
- **Đứng trên lưng:** hero được mang theo bộ phận dưới chân. Khi colossus lắc mình mà không giữ chuột phải thì bị hất văng.
- **Rơi:** rơi quá 7,5 m mất 7 máu mỗi mét.
- **Chân chạm đất:** đất vẽ là tam giác 2 m còn va chạm là lưới 1 m nội suy, nên ở khoảng 9% bản đồ mặt đất nhìn thấy cao hơn chỗ chân thật sự đứng; hoạt ảnh thủ kiếm khi đứng yên cũng hạ chân 3 cm dưới tư thế gốc. Model được nâng hạ cho đế giày nằm trên mặt đất nhìn thấy (`Hero._feet_lift`, `Terrain.drawn_height_at_world`, `HeroModel.sole_height`).
- **Không rơi khỏi bản đồ:** chân colossus giáng xuống có thể ấn hero xuống dưới mặt đất, mà heightmap không có gì bên dưới; hễ hero (không đang bám) thấp hơn mặt đất 0,4 m là được đặt lại lên trên (`Hero._keep_above_land`).
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

Hình dáng (`creature_art.gd`, lưới nặn bằng `Tools/Sculpt`): Slime là khối thạch xanh bạc hà trong suốt hình giọt kẹo, chân chảy xệ thành bảy giọt, đôi mắt giận dữ đen bóng có chấm sáng, miệng cau có lưỡi, mũ rêu dày mép lởm chởm có sợi rêu rủ xuống, mầm lá và một cây nấm đỏ đốm trắng, sỏi và cành con mắc bên trong. Nấm Độc có thân bụng phệ màu kem ửng tím, mũ tím đậm nhô đỉnh với mép gợn sóng và mụn vàng chanh, phiến nấm bên dưới, vòng da dưới mũ, mắt vàng chanh phát sáng xếch giận, mày tím, miệng há có hai răng nanh, tay ngắn, chân rễ ba ngón. Mặt quái nhìn về −Z; trước đây model bị quay ngược nên quái đi lùi, đã sửa trong `enemy.gd`.

### Nặn quái bằng trường khoảng cách (`Tools/Sculpt`)

`python Tools/Sculpt/creatures.py [slime] [shroom] [gau_ma] [--only head,torso]` nặn quái và ghi `Godot/assets/creatures/<tên>.glb` (mỗi bộ phận một node), rồi mở editor hoặc chạy `--headless --path Godot --import` để Godot nhập lại. Cần Python có numpy.

- `sdf.py`: hình cơ bản (cầu, elipxoit, côn tròn, hộp bo cạnh, xuyến, mặt phẳng, ống theo đường cong) ghép bằng hợp mềm (màu hoà vào nhau ở chỗ nối), khoét mềm (chỗ khoét có màu riêng: hốc mắt, miệng), giao, lớp vỏ ôm theo thân (`shell`, cho giáp), đẩy mặt bằng nhiễu. **Lông**: các lọn lông xếp chồng như ngói (`clumps`), dài theo hướng chải, gốc to đầu nhọn, cao nhất ngay trước mút, có rãnh sợi chạy dọc; ở đầu mút thân (mông, ngực, đầu gối) lọn đổi sang hướng chải thứ hai để luôn nằm dọc theo da. Mặt lưới dựng bằng *surface nets*, đỉnh được kéo đúng lên mặt, pháp tuyến lấy từ trường, màu đỉnh có sẵn bóng khe (tối trong kẽ) và độ cong (sáng trên gờ) cho giống vẽ tay.
- `creatures.py`: Slime (nặn ở bán kính 1, game nhân theo `radius`), Nấm Độc (mét); `bear.py`: Gấu Ma theo đơn vị gấu, mỗi bộ phận trong khung của `AnimatableBody3D` của nó và ôm đúng khối va chạm: mặt lông bắt đầu lõm vào trong 0,12 rồi lọn lông nhô ra quá một chút, nên hero bám đúng chỗ nhìn thấy. Bộ phận đã nặn được giữ trong `Tools/Sculpt/.cache`; `--only` chỉ nặn lại những phần nêu tên (cả con gấu mất khoảng 2 phút).
- Xem nhanh một model dưới trời và nắng của game: `Godot_console.exe --path Godot res://tools/model_shots.tscn -- --model slime|shroom|bear|hero|sword --shots-dir D:/shots`.

Vùng phát hiện, bán kính dây xích, lang thang và ngủ được nhân `Enemy.AREA = 1,6` vì thế giới 3D rộng hơn; tầm đánh và đạn giữ nguyên. Sói, Ong Bắp Cày, Cây Ma, Ma Rừng, Sói Đầu Đàn, Vua Slime Hoàng Kim mới nằm trong kế hoạch của bản Unity, chưa có code nên cũng chưa chuyển.

## Colossus

`Colossus` là lớp nền chung. Thân con quái là các `AnimatableBody3D` trên lớp vật lý `colossus` (layer 4), được đặt lại vị trí mỗi khung hình. Colossus có model nặn sẵn (như Gấu Ma) đặt `draw_shapes = false`: khối va chạm khi đó không vẽ gì. Cách vẽ cũ vẫn còn cho colossus khác: lông vẽ tay (`fur.gdshader`): vệt lông mềm, lưng sáng bụng sẫm, rêu mọc trên lưng; các lọn lông cong dày phủ quanh thân (`Colossus.lock_mesh`). Phần lông được vẽ bằng lưới sần (`Colossus.fur_mesh`): mặt da lồi lõm thành từng mảng dài theo thân, phần lớn lõm vào trong khối va chạm, lồi ra tối đa khoảng 0,25 m, nên tay chân hero vẫn bám đúng chỗ nhìn thấy; ấn ký điểm yếu đặt cách da 0,3 m cho khỏi bị lông che. Giáp vẽ thành nhiều tấm vỏ cây chồng lên nhau như vảy (va chạm vẫn là một khối). Mọi colossus hồi sinh sau 3 phút như mọi boss trong game.

### Đánh boss kiểu Dauntless

Theo cách behemoth của Dauntless vận hành (máu chung, bộ phận phá được, thanh choáng ẩn, gián đoạn):

- **Đánh vào đâu cũng mất máu.** Colossus có một bể máu chung (`BossHealth`, Gấu Ma 3200 như boss Unity) cho mọi bộ phận; chém, chiêu, đạn, nổ trúng bất kỳ khối va chạm nào đều trừ vào đó. Mỗi đòn biết nó trúng chỗ nào (điểm gần nhất trên khối va chạm), nên số sát thương hiện đúng chỗ trúng và đòn được tính cho bộ phận đó.
- **Bộ phận phá được.** Gấu Ma: Đầu (640 điểm phá) và bốn chân (mỗi chân 400); thân và cổ không phá được. Đòn trúng bộ phận chưa vỡ hiện **số vàng** = sát thương bộ phận. Lưỡi kiếm (đòn vật lý cận chiến, Trảm) tính **+50% sát thương bộ phận**, như vũ khí chém của Dauntless; phép và đạn tính 100%. Máu boss vẫn trừ đúng sát thương gốc ở phía sau. Đủ ngưỡng thì **vỡ**: chữ "VỠ ĐẦU!", gạc gãy rơi xuống đất (vỡ chân thì tấm vỏ cây ốp ống chân rơi), boss khựng lại 1,8 s, rơi đồ (đầu: Xương, chân: Vuốt Gấu Ma), dòng nhật ký có biểu tượng.
- **Sau khi vỡ** bộ phận đó hiện **số trắng xanh** = sát thương gốc vào máu. Số nhỏ hơn số vàng một phần ba vì không còn +50% của lưỡi kiếm, còn máu boss mất vẫn y như trước (Dauntless cũng thế: số vàng che số xám của sát thương gốc, vỡ rồi thì chỉ còn số xám).
- **Thanh choáng** (`Poise`, 300 như boss Unity; mỏng màu lam ngay dưới thanh máu boss): mọi đòn có trấn áp đều nạp vào, đánh vào đầu ×2, cổ ×1,5, chân ×1,2. Chém Gió nạp 3 mỗi đòn, Trảm 30–80, mỗi nhát đâm điểm yếu nạp bằng sát thương nhát đâm. Gần đầy (70%) thì có sao vàng xoay trên đầu. Đầy thì boss **choáng** 6 s, đầu gục xuống đất; mỗi lần choáng thanh dài thêm 25%; 4 s không bị đánh thì thanh tụt 12 mỗi giây.
- **Sát thương lúc choáng** (staggered damage): mọi đòn trong lúc boss choáng gây **+50%** và hiện **số cam**. HUD ghi "CHOÁNG x s · SÁT THƯƠNG +50%" và thanh choáng chuyển cam, rút dần.
- **Gián đoạn** (interrupt): chiêu **Húc** của Gấu Ma có tia sáng vàng toả ra trên đầu suốt lúc cúi đầu lấy đà và lao tới (`can_interrupt`). Trảm (đòn nặng) trúng đầu hoặc cổ trong lúc đó thì nó bị **GIÁN ĐOẠN**: ngã, choáng 7 s và nhận sát thương lúc choáng. Đòn nhẹ hay Trảm trúng chân thì không ngăn được. Lúc lao, đầu nó hạ xuống dưới 2,2 m nên đứng trên đất là với tới.
- **Điểm yếu** (`WeakPoint`, kiểu SotC, vẫn còn): mỗi nhát đâm làm boss mất gấp 3 lần sát thương nhát đâm (số vàng to). Phá một điểm *vital* (u lưng, đỉnh đầu, mỗi điểm 120) lấy thêm 8% máu boss và làm nó choáng ngay. Điểm ở bắp chân sau trái làm nó Quỵ Gối. Boss ngã khi hết máu (không còn luật "đâm hết 2 điểm vital").

### Gấu Ma Rừng Già (`gau_ma.gd`)

Cao khoảng 10 m tới đỉnh đầu, dài khoảng 17 m (`GauMa.S = 0,8` mét mỗi đơn vị gấu; trước là 1,4 và cao 18 m, người chơi thấy quá to; tầm các chiêu nhân theo `REACH = S / 1,4`), sống trong Hang Gấu Ma (sàn phẳng bán kính 50 m, có 4 Tảng Đá Lớn và tàn tích cột đá). Bốn chân bước theo kiểu IK thủ tục (chân chéo nhau thay phiên), mỗi bước làm rung đất, giẫm trúng thì bị ngã.

- **Đường leo:** lông sau bắp chân sau → mông → lưng (qua u vai, có thêm một khối cầu va chạm) → cổ → đỉnh đầu. Hai bên sườn có giáp vỏ cây, ngực có tấm giáp, mặt là mặt nạ xương.
- **Hình dáng** (nặn bằng `Tools/Sculpt/bear.py`): một con gấu già bị rừng mọc lên người. Lông nâu sẫm thành từng lọn chải về sau và xuống, đầu lọn sáng, bụng tối, chân "đi tất" sẫm; thảm rêu xanh dày trên lưng và u vai; hai hàng vảy vỏ cây hình bầu dục ôm theo sườn, ba tấm ngang ngực, tấm ốp ống chân, đều có vết nứt vỏ cây, địa y và rêu trên mép; mặt nạ xương ngà phủ trán, quanh mắt và sống mũi, hốc mắt sâu có mắt phát sáng, vết nứt, vạch xanh ngọc; mõm rộng, mũi đen, hàng răng và hai nanh cong; tai lông; đôi gạc như cành cây có rêu ở gốc và lá ở đầu; bàn chân bốn ngón với vuốt cong (gốc sẫm, đầu ngà); cụm nấm phát sáng xanh ngọc hai bên sườn và vai (tránh đường leo và điểm yếu). Gạc, răng, nấm chỉ là hình, không có va chạm.
- **Điểm yếu:** bắp chân sau trái (45 máu, phá được thì gấu **Quỵ Gối** khoảng 10 s, lưng hạ xuống thành dốc) · u lưng (120) · đỉnh đầu (120). Gấu ngã khi hết máu (3200).
- **Chiêu** (lấy từ boss 2D, mỗi chiêu có vòng cảnh báo đỏ trên đất và chữ "Kỹ năng: …"):
  - *Vồ*: quét chân trước.
  - *Dậm Đất*: chồm lên, giáng xuống, sóng chấn bán kính 11 đơn vị gấu (8,8 m). Hero đứng trên bàn chân cũng trúng (trước đây được coi như "đang ở trên gấu" nên không bị đánh, rồi bị chân ấn lọt xuống dưới đất).
  - *Ném Đá Lớn*: ném đá, đá nằm lại thành *Tảng Đá Lớn*, tối đa 4 tảng.
  - *Chụp Quăng*: nhảy vồ. Đáp cạnh một Tảng Đá Lớn thì bị **Choáng** 7 s, đầu gục xuống đất, leo lên đầu được ngay.
  - *Lắc Mình*: khi có người trên lưng.
  - *Húc*: cúi đầu lấy đà 1,1 s (tia sáng trên đầu, vòng cảnh báo dọc đường lao), rồi lao 1,9 s tới quá chỗ hero đứng 8 m; đầu và hai chân trước chạm ai thì người đó bị hất (34 sát thương). Có thể bị Gián Đoạn (xem trên). Dùng khi hero ở cách 8–26 m phía trước, hồi 11 s.
  - *Cuồng Nộ*: dưới 50% máu, mắt đỏ, nhanh hơn và lắc nhiều hơn.

## Kiểm tra

- **Test** (khoảng 40 s, 212 kiểm tra): `Godot_v4.7.2-stable_win64_console.exe --headless --path Godot res://tests/test_runner.tscn`. Mã thoát 0 là đạt. Gồm bố cục thế giới, đất đã bake, giải IK, phím, điểm yếu/quỵ gối/ngã/hồi sinh, sát thương rơi, **leo chân gấu có vật lý thật**, giới hạn số đá; công thức sát thương (đúng ví dụ trong test Unity: Cầu Lửa 56 vào điểm yếu Lửa ra 73, có giáp ra 64), bảng XP, chỉ số cấp 1 của 9 lớp, các trạng thái, Trấn Áp, khống chế lặp lại, hero chém chết slime, Cầu Lửa gây Bỏng, bào tử làm chậm, Lướt Hoàn Hảo, Dậm Đất không đẩy hero xuống dưới đất ở cả năm chỗ đứng quanh chân gấu; máu chung, sát thương bộ phận +50% của lưỡi kiếm, chân vỡ ở đúng ngưỡng rồi chỉ còn sát thương gốc, điểm yếu ×3 và +8%, thanh choáng (đầu ×2), +50% lúc choáng, Húc chỉ bị Trảm trúng đầu gián đoạn, đầu khi lao hạ trong tầm với, Trảm đầy lực gây khoảng 3,2 lần Tấn công, thanh máu rút trong 0,75 s với bóng mờ trễ 0,4 s và đổi màu ở 50% / 25%; nguyên tố khắc +50% / cùng hệ −20%, sáu kiểu kích hoạt trên quái nhỏ và trên Gấu Ma (Lôi làm choáng, Băng làm chậm, Ám mọc nang nổ ra sát thương bộ phận, Quang làm Húc tự gián đoạn), chỉ đòn vũ khí mới kích hoạt, không kích hoạt trên quái cùng hệ, bảy họ vũ khí đúng đòn thường và số món cầm, đòn nặng của cả bảy họ đều trúng.
- **AutoShot** (khoảng 145 s, mở cửa sổ): `Godot_v4.7.2-stable_win64_console.exe --path Godot -- --autoshot --shots-dir D:/shots [--autoshot-only world|forest|climb|skills|hunt|arsenal|brawl]`. Tour `hunt`: số vàng trên chân, tụ lực Trảm, Trảm, vỡ đầu, Húc với tia sáng, gián đoạn bằng Trảm thật (giữ rồi thả chuột phải đúng lúc), số cam lúc choáng. Tour `arsenal`: bảy vũ khí, mỗi món một combo và đòn nặng vào slime, rồi Song Liêm Lôi Điểu đánh Gấu Ma Thổ. Tour `forest` có thêm ảnh đúng khung trúng đòn, thanh máu quái đang rút và thanh máu hero sau một đòn mạnh. Chụp 47 cảnh (làng, hai góc nhà trong làng, rừng, đấu trường; bãi slime, Chém Gió, đồ rơi, Chém Xoáy, Nấm Độc bắn, Cầu Lửa, Lôi Phạt; bám chân, đâm, quỵ gối, trên lưng, lắc mình, Dậm Đất, Ném Đá, Chụp Quăng và Choáng), rồi để gấu tự đánh 40 s. Mã 0 là sạch, 1 là có lỗi trong log, 2 là quá giờ.
- Chạy nhanh không cần cửa sổ: `--headless --path Godot --quit-after 200`.

## Lộ trình chuyển cả game

| Giai đoạn | Nội dung | Trạng thái |
|---|---|---|
| 1 · Nền móng | Project, phím, camera SotC, hero (chạy/nhảy/Lướt/combo/bám/leo/đâm/rơi), thế giới 800×512 m theo bố cục Unity, làng, Hang Gấu Ma, **Gấu Ma thành colossus**, HUD, test, AutoShot | **Xong** (14/10/2026) |
| 2 · Chiến đấu & tiến trình | Công thức sát thương, kháng hệ, trạng thái, Trấn Áp, hit-stop 3 mức, Lướt Hoàn Hảo, cả 92 chiêu chạy từ dữ liệu, cấp/XP, 6 chỉ số D&D; số liệu chuyển từ `Assets/Data` bằng script; Slime Rêu và Nấm Độc; túi đồ, rơi đồ, bình thuốc | **Phần lớn xong** (15/10/2026). Còn: cửa sổ cộng điểm chỉ số, Sách Chiêu (K), cấp chiêu |
| 2b · Art kiểu Dauntless | Hero Quaternius (bộ đồ, đầu, tóc, hoạt ảnh UAL), ánh sáng kiểu vẽ tay, tán cây tròn, cỏ dày, đá rêu, trời và sương; HUD như Dauntless; bảng tên quái sửa lệch thanh máu; Lướt cả trên không | **Phần lớn xong** (15/10/2026): thêm nhà làng, cây và bụi dựng bằng code, slime/nấm/Gấu Ma vẽ tay; đá và vách núi dựng bằng code, Gấu Ma có lông sần, gạc, nanh, vuốt, nấm phát sáng. Còn: NPC trong làng, mặt đất trên đỉnh vách còn phẳng, thanh kiếm |
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

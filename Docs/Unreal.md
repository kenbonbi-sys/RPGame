# Rừng Thì Thầm trên Unreal Engine 5

Ngày 17/10/2026 dự án quyết định chuyển hẳn sang **Unreal Engine 5**. Mục tiêu vẫn như cũ: nhìn và chơi giống *Dauntless* nhất có thể. Dauntless được làm bằng Unreal Engine 4.

## Vì sao chuyển

Sau nhiều lượt chỉnh, bản Godot vẫn "chưa giống gì cả". Cái thiếu chủ yếu không nằm ở code:

- **Tài sản:** nhân vật, quái và cảnh vật của Dauntless do hoạ sĩ làm tay. Bản Godot dùng bộ người dựng sẵn miễn phí, cây đá dựng bằng code, quái nặn bằng công thức.
- **Hoạt ảnh:** Dauntless có hoạt ảnh làm riêng cho từng đòn. Bản Godot ghép thư viện chung sang khung xương khác, chưa có hoà trộn theo tốc độ, chưa có chân bám đất.

Unreal có lợi ở đúng những chỗ đó:

- **Kho tài sản Unreal:** bộ nhân vật **Paragon** của Epic miễn phí, có đủ hoạt ảnh, phong cách rất gần Dauntless (chỉ được dùng trong dự án Unreal). Ngoài ra có Fab.
- **Hệ thống hoạt ảnh sẵn có:** Animation Blueprint, blend space, root motion, IK chân, montage cho đòn đánh.
- **Cài đặt đồ hoạ:** các dòng Nâng Cao trong Cài Đặt của Dauntless (Chất Lượng, Tầm Nhìn, Khử Răng Cưa, Đổ Bóng, Hậu Kỳ, Kết Cấu, Hiệu Ứng) chính là các nhóm Scalability có sẵn của Unreal.
- **Mạng:** replication và dedicated server có sẵn, hợp với hướng online nhiều người chơi.

InstaLOD chỉ là công cụ tối ưu (giảm tam giác, tạo LOD, gộp texture), không tạo ra phong cách hình ảnh. Unreal 5 đã tự tạo LOD và có Nanite, nên lúc đầu chưa cần InstaLOD.

## Cái gì giữ lại

- **Bản Unity** (`Assets/`) vẫn là bản bạn bè đang chơi online cho tới khi bản Unreal bắt kịp.
- **Bản Godot** (`Godot/`) dừng phát triển, giữ làm **bản mẫu** khi viết lại:
  - luật chiến đấu (sát thương, trạng thái, choáng, nguyên tố, giáp);
  - số liệu (`Godot/data/*.json`, sinh từ Unity bằng `Tools/GodotData/unity_to_json.py`);
  - bố cục thế giới và độ cao đất (`WorldMap`, `terrain_data.res`);
  - dàn NPC Ramsgate, menu, HUD;
  - 337 kiểm tra (`Godot/tests`). Viết lại thành test tự động của Unreal.

## Máy này

| | Có | Cần cho Unreal 5 |
|---|---|---|
| Card đồ hoạ | RTX 3060 12 GB | Đủ |
| CPU | i5-12400F, 6 nhân 12 luồng | Đủ (biên dịch shader và C++ sẽ chậm hơn máy nhiều nhân) |
| RAM | 32 GB | Đủ |
| Ổ đĩa | Còn trống khoảng 549 GB | Nên có từ 100 GB trống: engine khoảng 40–60 GB, Visual Studio C++ khoảng 10–20 GB, dự án và bộ nhớ đệm shader khoảng 10–20 GB, tài sản Paragon vài GB mỗi nhân vật |
| Epic Games Launcher | Đã cài | |
| Unreal Engine | UE 5.8.3 trong `C:\Program Files\Epic Games\UE_5.8`, cài qua Epic Games Launcher | Dự án ghi `EngineAssociation` 5.8 |
| Visual Studio | Build Tools 2026: MSVC 14.50, Windows SDK 10.0.26100, .NET Framework 4.8.1 SDK | Đủ cho UnrealBuildTool. Thiếu .NET Framework SDK thì UBT báo `SwarmInterface ... NetFxSDK`: thêm thành phần `Microsoft.Net.Component.4.8.1.SDK` bằng Visual Studio Installer |

## Cách làm việc

- **Toàn bộ gameplay viết bằng C++** (chữ, đọc được, sửa được, so được trong git). Blueprint chỉ làm lớp mỏng: gắn mesh, hoạt ảnh, chỉnh số.
- **Tự động hoá bằng script:**
  - Python trong editor (Editor Scripting) để nhập tài sản, dựng màn chơi, tạo asset;
  - biên dịch bằng dòng lệnh (UnrealBuildTool);
  - test bằng Automation Test chạy không cửa sổ;
  - chụp ảnh bằng chế độ `-game` để so với Dauntless như bản Godot.
- **Dự án nằm cùng repo,** thư mục `Unreal/`, cùng nhánh `main`. File `.uasset` / `.umap` đi qua Git LFS. Bộ nhớ đệm `DerivedDataCache`, `Intermediate`, `Saved`, `Binaries` không vào git.

## Đã có trong `Unreal/`

- **Khung dự án C++:** `RungThiTham.uproject`, hai target (`Source/RungThiTham.Target.cs`, `RungThiThamEditor.Target.cs`), module `RungThiTham` (`RungThiTham.Build.cs`), `Config/` (game mode, map mặc định, Lumen, DX12 SM6, Enhanced Input).
- **Thư viện luật** `Source/RungThiTham/Rules/`: C++ thuần (namespace `Rtt`), không dùng kiểu nào của Unreal, nên biên dịch và test được ngay khi chưa có engine, và sau này chạy được cả trong dedicated server. Như Unreal, không dùng exception.
  - `RttJson`: đọc các file `Content/Data/*.json`.
  - `RttTuning`: các hằng số cấp độ và chiến đấu (`progression.json`, `combat.json`).
  - `RttCombat`: phe, nguyên tố chiến đấu, làm tròn nửa về số chẵn, giáp theo cấp kẻ đánh, độ lệch ±5%.
  - `RttElements`: nguyên tố Dauntless (Blaze ↔ Frost, Shock ↔ Terra, Radiant ↔ Umbral; +50% / −20%).
  - `RttArmour`: 35 bộ giáp của wiki, điểm kháng từng món, ±0,5% mỗi điểm, tối đa ±50%.
  - `RttDamage`: một đòn trúng mất bao nhiêu máu, đúng thứ tự của `Health.TakeDamage` bản Unity.
  - `RttSheet`: bảng nhân vật D&D (lớp, chủng tộc, sáu chỉ số, máu, năng lượng, Attack, giáp, kháng, XP, lên cấp, cộng điểm).
- **Test luật** `Tests/RulesTests.cpp`: chép lại các ví dụ của bản Godot (Cầu Lửa 56 vào kẻ yếu lửa 30% ra 73, bảng XP, chỉ số cấp 1 của chín lớp, giáp và nguyên tố). Chạy:

  ```
  Tools\RulesCheck\check.cmd
  ```

  Script tự tìm `vcvars64.bat` của Visual Studio, biên dịch như Unreal (C++20, không exception, cảnh báo là lỗi) rồi chạy với dữ liệu thật. Hiện có 133 kiểm tra, đều đạt.
- **Game (U0)** trong `Source/RungThiTham/`:
  - `ARttHero`: chạy 5,4 m/s; giữ Shift để chạy nhanh 8,2 m/s; nhấn Shift để Lướt (bất tử 0,28 giây); nhảy 7,4 m/s với trọng lực 21 m/s². Số liệu và phím lấy từ `hero.gd` và `controls.gd` của bản Godot. Camera treo sau vai. Thân tạm (khối trụ và quả cầu) chờ nhân vật Paragon ở U1. Phím được tạo bằng Enhanced Input ngay trong C++, không cần asset.
  - `ARttGameMode`: đưa nhân vật vào màn chơi. Với `-RttShot=<file.png>`, nó chờ shader biên dịch xong, chụp ảnh rồi thoát.
  - `URttRules`: nạp `Content/Data` vào thư viện luật khi game chạy.
  - `Tests/RttTests.cpp`: test tự động của Unreal (`RungThiTham.Rules.Data`, `RungThiTham.Game.Setup`).
- **Map thử** `Content/Maps/ThuNghiem.umap` do `Tools/Unreal/make_level.py` dựng: nền 400 m, mặt trời, bầu trời, mây, sương (ánh sáng Lumen, không bake), vài khối để so tỉ lệ, PlayerStart. Mọi bề mặt dùng `Content/Materials/M_Grid`: bàn cờ ô 1 m đặt theo toạ độ thế giới. Lưới có sẵn của engine bám theo UV của từng mesh nên vỡ thành nhiễu trên mặt phẳng kéo dài 400 lần.
- **Script** `Tools/Unreal/ue.cmd`:

  ```
  Tools\Unreal\ue.cmd build    biên dịch (UnrealBuildTool)
  Tools\Unreal\ue.cmd level    dựng lại map thử /Game/Maps/ThuNghiem
  Tools\Unreal\ue.cmd test     chạy test RungThiTham, không mở cửa sổ
  Tools\Unreal\ue.cmd shot     chơi map trong cửa sổ, chụp ảnh rồi thoát
  ```

  Log nằm trong `Unreal/Saved/Tools`. Nếu engine cài ở chỗ khác, đặt biến `UE_ROOT`.
- **Dữ liệu** `Content/Data/`: `Tools/GodotData/unity_to_json.py` giờ ghi cùng một bộ file vào cả `Godot/data` và `Unreal/Content/Data`. Riêng `armour_sets.json` chép tay từ trang Armour của wiki Dauntless (bản Godot để bảng này trong `armour.gd`). Khi đóng gói, thư mục `Data` đi kèm dưới dạng file rời (`DirectoriesToAlwaysStageAsNonUFS`).

## Lộ trình

| Giai đoạn | Nội dung |
|---|---|
| U0 · Nền móng | Cài engine và Visual Studio; tạo dự án C++; Git LFS; biên dịch, test và chụp ảnh từ dòng lệnh |
| U1 · Hero và camera | Nhân vật Paragon cầm vũ khí; camera treo sau lưng kiểu SotC; chạy, chạy nhanh, nhảy, Lướt (bất tử ngắn, Lướt Hoàn Hảo); hoạt ảnh hoà trộn theo tốc độ, chân bám đất, đòn đánh bằng montage có root motion |
| U2 · Luật chiến đấu | Viết lại sang C++ đúng từng con số: Health, StatusEffects, Poise, DamageInfo, Combat, Elements, Armour, Weapons, cấp và chỉ số D&D, 92 chiêu từ dữ liệu; test giữ nguyên các ví dụ của bản Godot. **Đã xong:** sát thương, nguyên tố, giáp, bảng nhân vật và XP (thư viện `Rules/`). **Còn:** trạng thái, choáng (Poise), vũ khí, chiêu |
| U3 · Thế giới | Landscape dựng từ độ cao của `WorldMap`; cây cỏ đá theo vùng; vật liệu vẽ tay kiểu Dauntless; Làng Lá Xanh theo kiểu Ramsgate (nhà gỗ sẫm, đèn lồng, sương xanh) |
| U4 · Behemoth | Gấu Ma: bộ phận phá được, thanh choáng, sát thương lúc choáng, Húc bị gián đoạn, vòng cảnh báo và nhịp báo trước; leo bám kiểu SotC; slime và nấm |
| U5 · Giao diện | HUD, Esc, Cài Đặt (gắn thẳng vào Scalability), Trang Bị (nhân vật giữa màn hình), nhật ký, ô vật phẩm |
| U6 · Làng | Chín NPC Ramsgate, nói chuyện, cửa hàng, hồ sơ behemoth, bản đồ, bãi tập |
| U7 · Online | Replication, dedicated server, thay bản Unity cho bạn bè |

## Rủi ro

- **Biên dịch shader lần đầu** trên CPU 6 nhân khá lâu. Bộ nhớ đệm (Zen DDC) nằm trong `%LOCALAPPDATA%\UnrealEngine`.
- **Lặp lại chậm hơn:** biên dịch C++ và shader của Unreal lâu hơn Godot nhiều; mỗi vòng sửa và chụp ảnh sẽ mất vài phút thay vì vài giây.
- **Tài sản nhị phân:** mình không sửa được Blueprint và `.uasset` bằng chữ, nên mọi thứ mình làm sẽ đi qua C++ và script Python.
- **Giấy phép:** Paragon chỉ được dùng trong dự án Unreal. Tài sản từ Fab phải xem giấy phép từng món.
- **Phong cách:** Paragon gần Dauntless hơn nhiều so với bộ miễn phí hiện tại, nhưng không phải nhân vật của Dauntless. Dauntless có bản quyền, không lấy được.

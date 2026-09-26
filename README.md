# Rừng Thì Thầm — Prototype Action RPG pixel art (Unity 6 · 2D URP)

Prototype top-down action RPG: khám phá rừng, nhặt đồ, làm nhiệm vụ và đánh boss **Gấu Ma Rừng Già** với nhiều pattern tấn công. Toàn bộ art (tile, nhân vật, boss, icon, UI, VFX) và âm thanh đều được **sinh bằng script** (không dùng asset bên thứ ba), nên bạn có thể thay bằng art thật bất cứ lúc nào.

## Mở và chạy

- **Chơi ngay:** `Builds/Windows/RungThiTham.exe` (màn hình chính: *Vào thế giới* online hoặc *Chơi một mình*)
- **Mở trong Unity:** Unity Hub → *Add project from disk* → chọn thư mục `RPG` → mở bằng **Unity 6000.6.0f1** → mở scene `Assets/Scenes/Core.unity` → bấm **Play**.
- **Cấu trúc scene:** `Title.unity` (màn hình chính, cảnh đầu tiên của bản build), `Core.unity` (quản lý, nhân vật, camera, ánh sáng, HUD — luôn được tải) + mỗi vùng một scene trong `Assets/Scenes/Zones` (địa hình, NPC, quái, boss), tải thêm bởi `SceneLoader` có màn chuyển. Mở riêng một scene vùng rồi bấm Play cũng chạy được: Core tự được tải kèm.

## Chơi online

Một thế giới chung, luôn bật, cho khoảng 20 người (kế hoạch: `Docs/KeHoach-Online.md`, vận hành máy chủ: `Docs/MayChu.md`):

- **Người chơi:** mở game, bấm **Vào thế giới**. Game tự tìm máy chủ trong LAN / Radmin VPN và các địa chỉ trong `RungThiTham_Data/StreamingAssets/servers.txt`, không cần nhập IP. Lần đầu nhập tên nhân vật và mật khẩu (tên mới thì *Tạo nhân vật mới*); game nhớ đăng nhập trên máy đó. Bấm **Enter** để chat.
- **Máy chủ luôn bật:** `Tools/Server/install-server.ps1` cài bản build thành máy chủ chạy nền, tự bật khi đăng nhập Windows và tự bật lại khi lỗi. Nhân vật (cấp, túi đồ, nhiệm vụ, vị trí…) lưu trên máy chủ, sao lưu mỗi ngày.
- **Online có đủ:** quái, bãi hồi sinh, boss Gấu Ma (hồi sinh sau 3 phút), kỹ năng, trạng thái, rơi đồ riêng từng người, XP, nhiệm vụ, hội thoại — tất cả do máy chủ quyết định.
- **Dòng lệnh:** máy chủ `RungThiTham.exe -server -batchmode -nographics [-port 7770] [-data <thư mục>] [-maxplayers 20]`; vào thẳng `-client <địa chỉ> -login <tên> <mật khẩu>`; một máy vừa mở vừa chơi `-host`. Bảng lệnh `` ` ``: `players`, `net`, `say`, `leave`; lệnh cheat online chỉ dành cho GM (`gm.txt` của máy chủ).
- **Kiểm tra tự động:** `Tools/Server/netsmoke.ps1` (một máy chủ và hai người chơi đánh quái, thoát rồi vào lại).

## Điều khiển (theo layout của game tham khảo)

| Phím | Chức năng |
|---|---|
| Giữ **chuột trái / phải** hoặc **phím mũi tên** | Di chuyển |
| **Nhấp vào quái** | Tự đi tới và chém (dùng skill Q) |
| **Q W E R A S D** | Chém Gió · Cầu Lửa · Mũi Băng · Lôi Phạt · Hồi Phục · Khiên Thánh · Bão Kiếm |
| **Space** | Lướt (bất tử trong chốc lát, dùng để né vòng đỏ) |
| **1 2 3** | Bình Máu · Bình Năng Lượng · Thuốc Thảo Mộc |
| **F** | Nói chuyện với NPC |
| **B / I / C** · **J** · **Tab** | Nhân vật: chỉ số, trang bị và túi đồ trong một cửa sổ · Bách Khoa Trùm · Đổi nhiệm vụ đang theo dõi |
| **K** | Sách Chiêu: đặt chiêu hệ Băng, Lôi, Ám đã học lên thanh kỹ năng |
| **F1** · **Esc** | Hướng dẫn · Tạm dừng (Lưu game / Tải game) |
| **F5–F9** (cheat) | Hồi đầy · Tua giờ · Tới boss · Về làng · Hạ quái gần |
| **`** (cheat) | Bảng lệnh: `help`, `level 10`, `give potion_red 5`, `tp boss`, `zone <id>`, `quest <id> start`, `hitbox`, `ttk`, `status lanh 4` (gây trạng thái lên quái gần nhất, thêm `me` cho bản thân), `save 1`… |

## Nội dung đã có

- **Bản đồ** liền mạch 200×128 tile: Làng Lá Xanh → Rừng Thì Thầm → Đầm Lầy Sương Mù → Hang Pha Lê → Thảo Nguyên Gió. Có vùng + tên khu vực, minimap, bản đồ thế giới, chu kỳ ngày/đêm (đèn 2D: lửa trại, đèn lồng, cửa sổ nhà, ánh sáng quanh nhân vật).
- **Thảo Nguyên Gió (T61):** đường hầm phía tây Rừng Pha Lê dẫn ra cao nguyên cỏ vàng, cây keo và trại du mục của Già Tăng. Gió đổi hướng, xô nhân vật và bẻ đường đạn; cỏ nghiêng theo gió. Khe Vực có va chạm, ba cặp Cột Gió đưa qua hai bờ. Linh Cẩu Gió săn theo bầy, Chim Ưng Đá đánh dấu rồi bổ nhào, Bò Rừng húc vào đá sẽ choáng. Chuỗi nhiệm vụ nối tiếp Nhện Chúa; ba Đá Truyền Tống và trang bị chế tạo mới.
- **Hắc Phong (T62):** phía tây Khe Vực. Bù Nhìn Sống đứng lẫn giữa bù nhìn rơm (bù nhìn rơm lay theo gió, con sống thì không). Cung thủ bắn đón gió, đao thủ lướt chém. Mini-boss Bò Rừng Sắt bung giáp khi húc vào đá. Thủ Lĩnh Hắc Phong đứng trên đồi cối xay: gió ở đó đổi hướng mỗi 12 giây, hắn tung lưỡi gió và lốc xoáy, gọi cung thủ, nổi bão cát, và mất thăng bằng nếu bị Lướt Hoàn Hảo đúng nhát thứ ba. Chi tiết: `Docs/ThaoNguyen.md`.
- **Sách Chiêu (T63):** 18 chiêu của ba hệ Băng, Lôi, Ám (mỗi hệ một Tuyệt kỹ: Kỷ Băng Hà, Cửu Thiên Lôi, Nhật Thực), học từ Bí Kíp do boss các vùng rơi; mỗi chiêu chỉ các lớp có phép đó trong D&D học được. Phím K đặt chiêu đã học lên W E R A S (Tuyệt kỹ lên D) khi không giao chiến. Chi tiết: `Docs/NhanVat.md`.
- **Người chơi:** 4 hướng, animation idle/walk/attack/cast/dash/hurt/dead, 8 skill có VFX riêng, combo chém 3 đòn, crit, hit-stop, rung màn hình.
- **Cấp độ & chỉ số:** cấp 1–40, XP từ quái (theo cấp và bậc quái) và nhiệm vụ; mỗi cấp +3 điểm chỉ số cho Sức Mạnh, Trí Tuệ, Nhanh Nhẹn, Thể Chất (bảng C). Mọi con số nằm trong `Assets/Data/Progression.asset`.
- **Quái:** Slime Rêu (nhảy lao tới), Nấm Độc (bắn bào tử làm chậm). Có AI tuần tra / đuổi / quay về, rớt đồ, hồi sinh theo bãi.
- **Boss Gấu Ma Rừng Già (Cấp 6):** Vồ, **Dậm Đất** (choáng), **Ném Đá Lớn** (đá rơi xuống thành *★ Tảng Đá Lớn* phá được), **Chụp Quăng** (nhảy vồ; đáp trúng Tảng Đá Lớn thì boss bị **Choáng!**), **Cuồng Nộ** khi dưới 50% máu. Mỗi chiêu có vòng cảnh báo trên đất và hiện "Kỹ năng: …" trên đầu boss.
- **Công thức sát thương (mục 04):** Công × lực chiêu × thưởng sát thương theo nhãn chiêu × chí mạng × giảm do giáp × (1 − kháng hệ) × ngẫu nhiên 0.95–1.05. Mỗi loài có kháng riêng từng hệ (`Health.resistances`, từ −50% là điểm yếu tới +75%); Nấm Độc yếu Lửa. Các hằng số nằm trong `Assets/Data/Progression.asset`.
- **Thanh Trấn Áp:** Gấu Ma có thanh Trấn Áp (300) dưới thanh máu. Mỗi đòn của người chơi cộng điểm (Chém Gió 3, đòn cuối combo 9, Cầu Lửa 12, Mũi Băng 5, Lôi Phạt 8/tia, Bão Kiếm 2/nhịp; Sức Mạnh cộng thêm). Đầy thanh: choáng 3 giây, nhận thêm 50% sát thương; ngưỡng tăng 25% sau mỗi lần vỡ, không trúng đòn 2.5 giây thì thanh tụt.
- **Input buffer 150 ms và hủy đòn:** chiêu sau chờ tư thế của chiêu trước kết thúc; bấm sớm (trong 0.15 giây trước khi sẵn sàng) vẫn được ghi nhận và phát ngay khi được. Lướt cắt ngang tư thế sau *khung cam kết* của từng chiêu (`AbilityDef.commitTime`: Chém Gió tới khung trúng 0.05 s, Lôi Phạt 0.3 s…); Tuyệt kỹ không hủy được.
- **Lướt Hoàn Hảo:** đòn tấn công bị Lướt né trong 0.15 giây đầu: thời gian chậm còn 35% trong 0.3 giây, +15 năng lượng, chiêu tiếp theo trong 1.5 giây +30% sát thương và đòn trúng đầu tiên của nó cộng 25 Trấn Áp, hiện chữ “Hoàn Hảo!”. Va vào thân quái không tính.
- **Trạng thái (mục 04):** Bỏng (30% sát thương đòn gốc mỗi giây, 3 tầng, 3 giây), Lạnh (−12% tốc chạy và tốc đánh mỗi tầng; tầng 4 thành Đóng Băng 1.5 giây, boss 0.6 giây + 60 Trấn Áp), Tích Điện (tầng 3 phóng điện 80% sang 3 kẻ gần), Độc (1.5% máu tối đa mỗi giây mỗi tầng, 5 tầng, 6 giây), Choáng, Trói, Làm Chậm, Đẩy Lùi (va tường thì Choáng 0.5 giây), Nguyền, Phán Xét. Khống chế lặp lại trong 6 giây ngắn đi 40% mỗi lần; boss miễn khống chế 4 giây sau khi hết Choáng. Cầu Lửa gây 1 tầng Bỏng, Mũi Băng 2 tầng Lạnh mỗi gai.
- **Hit-stop 3 mức:** 35 ms đòn thường · 70 ms chí mạng và đòn cuối combo · 120 ms vỡ Trấn Áp và đòn kết liễu boss hoặc Tinh Anh (hạ quái thường: ít nhất 35 ms). Số của Lướt Hoàn Hảo, hit-stop và trạng thái nằm trong `Assets/Data/Combat.asset`.
- **Bách Khoa Trùm:** tự ghi lại quái và kỹ năng boss lần đầu gặp (hiện ở log bên trái, xem đầy đủ bằng phím J).
- **Lưu game:** 3 ô + tự động lưu (sau boss, khi xong nhiệm vụ, 5 phút một lần khi ngoài chiến đấu). File JSON có số phiên bản trong `%USERPROFILE%\AppData\LocalLow\<công ty>\<game>\saves`, bản cũ giữ dạng `.bak`.
- **Nhiệm vụ (QuestDef):** mỗi nhiệm vụ là một asset trong `Assets/Data/Quests` (người giao, điều kiện mở, 9 loại mục tiêu, phần thưởng, cờ, nhiệm vụ tiếp theo). Chuỗi chính với Trưởng Làng + nhiệm vụ phụ của Bé Mai; tracker "(+1 · Tab)", dấu ! / ? trên đầu NPC (vàng: chính, bạc: phụ).
- **Hội thoại (Yarn Spinner 3):** mỗi NPC một file `.yarn` trong `Assets/Dialogue`, có lựa chọn (phím 1–3), điều kiện theo nhiệm vụ và khóa `#line:` cho bản địa hóa. Hàm/lệnh Yarn: `quest_status`, `quest_left`, `item_count`, `has_flag`, `<<quest_start>>`, `<<quest_complete>>`, `<<give_item>>`, `<<victory>>` (xem `Assets/Scripts/Dialogue/YarnBindings.cs`).
- **HUD:** thanh máu boss khung trang trí, minimap + tên vùng + ngày/đêm, quest tracker, orb Máu/Năng lượng dạng chất lỏng, thanh bình thuốc, skill bar với cooldown/chi phí, log sự kiện, số damage bay, nameplate, hội thoại có portrait, túi đồ + tooltip, màn chết/hồi sinh, banner khu vực/chiến thắng.
- **Âm thanh:** 42 SFX, nhạc rừng, nhạc boss, âm nền rừng (tự tổng hợp, `Tools/AudioGen`). Âm lượng riêng cho Master · Nhạc · SFX · UI · Môi trường · Blip thoại (`AudioManager.SetVolume`, lưu trong PlayerPrefs); nhạc tự hạ khi đang hội thoại và một lúc khi boss hô chiêu. Gán một AudioMixer có các nhóm cùng tên vào `AudioManager.mixer` thì âm thanh đi qua các nhóm đó.

## Cấu trúc project

```
Assets/
  Scripts/  Core · Combat · Player · Abilities · Progression · Quests · Dialogue · Save · Enemies · World · Items · UI · VFX · Anim · Data · Debug · Editor
  Art/      ảnh sinh từ Tools/ArtGen (+ art_manifest.json: cắt sprite, pivot, 9-slice, animation)
  Prefabs/  Characters · Props · Gameplay · VFX (45 hiệu ứng)
  Data/     Items, Abilities, Quests, Progression (ScriptableObject — chỉnh chỉ số trong Inspector), Anims, VFX/Audio library
  Shaders/  RPG/VFX Additive (HDR → Bloom), RPG/VFX Alpha, RPG/Sprite Silhouette (hit flash)
  Settings/ URP 2D Renderer, Volumes (Bloom, Vignette, Impact, Danger)
Tools/
  ArtGen/   python build_all.py  → vẽ lại toàn bộ pixel art
  AudioGen/ python gen_audio.py  → tổng hợp lại âm thanh
  CompileCheck/ ./check.sh       → biên dịch thử toàn bộ C# không cần Unity (Linux, CI)
  FontGen/  python make_pixel_fonts.py → font pixel tiếng Việt Galmuri7/11 cho Assets/Fonts
```

## Menu Tools/RPG trong Unity

- **Build Everything (create missing only)** — chế độ authoring: import art → data → VFX → prefab → scene, nhưng **chỉ tạo những gì còn thiếu**. Prefab, material, item, skill, VFX library và scene đã có được giữ nguyên, nên chỉnh tay không bị mất. Texture chỉ được cắt lại khi `art_manifest.json` đổi mục của nó.
- **Force Rebuild Everything (overwrite)** — hành vi cũ: sinh lại toàn bộ và ghi đè (có hộp thoại xác nhận).
- **Steps/1–7** — chạy từng bước, cũng theo chế độ authoring. **Steps/6. Rebuild Scenes** luôn dựng lại Core và các scene vùng từ các prefab đang có. **Steps/7** chuyển túi đồ và sổ nhiệm vụ của scene Core cũ sang prefab Player (Build Everything tự chạy bước này).
- Batchmode: `-executeMethod RPG.EditorTools.Batch.BuildAll` (authoring), `Batch.ForceBuildAll`, `Batch.RebuildScene`, `Batch.MoveHeroState`.
- **Build Windows Player** — xuất `Builds/Windows/RungThiTham.exe` (batchmode: `Batch.BuildPlayer`, thêm `-buildPath "D:\out\RungThiTham.exe"` để xuất nơi khác).
- **VFX Gallery** — mở scene `Assets/Scenes/Tools/VFXGallery.unity` (tự tạo nếu chưa có) và bấm Play: mọi hiệu ứng xếp lưới 3×3 theo trang, mỗi ô ghi số hạt cao nhất và số Light2D so với ngân sách (150 hạt, 1 Light2D; Tuyệt kỹ gấp đôi, chỉnh trong `Assets/Data/VFXLibrary.asset`). Phím: 1–9 phát một ô · Space cả trang · ←/→ đổi trang · B bật/tắt Bloom · L lặp · M đo tất cả rồi in báo cáo · Tab bảng tổng.
- **VFX Budget Report** — đo mọi hiệu ứng ngay trong Editor (không cần Play) và in báo cáo ngân sách ra Console.
- **Pixel Font Test** — tạo font asset TextMeshPro cho font pixel Galmuri7 (8 px) và Galmuri11 (12 px) rồi mở scene thử chữ tiếng Việt ở ×1/×2/×3 cạnh Inter. File font lấy bằng `python Tools/FontGen/make_pixel_fonts.py`; kết quả so sánh font: `Docs/FontPixelTiengViet.md`.

## Mở rộng

- **Quy trình làm việc:** sprint 2 tuần, bảng việc, nhánh, Definition of Done và cách làm với Claude trên máy hay trên cloud: `Docs/QuyTrinh.md`. Kế hoạch tổng: `Docs/KeHoach-RungThiTham.md`.
- **Chỉnh VFX:** mở `Assets/Prefabs/VFX/<tên>.prefab`. Mỗi hiệu ứng là Particle System + sprite flipbook + Light2D. Tăng `_Intensity` của material để glow (Bloom) mạnh hơn.
- **Thêm chiêu:** chỉ cần dữ liệu. Tạo asset *Create → RPG → Ability*, ghép các khối (Damage, Projectile, Dash, Heal, Buff, Cue, Line, Burst, Pulse, Combo) trong Inspector, rồi gán vào `PlayerSkills.slots` trên prefab Player. Hướng dẫn: `Docs/ThemChieu.md`.
- **Thay art:** thay PNG trong `Assets/Art` (giữ kích thước frame), hoặc kéo sprite mới vào các `SpriteAnimSet` trong `Assets/Data/Anims`.
- **Phím điều khiển:** mọi phím định nghĩa một chỗ trong `Assets/Scripts/Core/GameControls.cs` (Input System actions). Đổi phím lúc chạy: `InputReader.Asset` + `InputReader.SaveBindingOverrides()` (lưu trong PlayerPrefs); nhãn phím trên skill bar tự cập nhật.
- **Thêm nhiệm vụ / hội thoại:** tạo asset qua *Create → RPG → Quest*, thêm vào `GameDatabase.quests`; viết node trong một file `.yarn` ở `Assets/Dialogue` và đặt tên node vào `NPC.yarnNode`.
- **Thêm quái:** kế thừa `EnemyBase` (xem `SlimeAI`, `ShroomAI`), boss tham khảo `BossBear`.
- **Nhiều người chơi (chuẩn bị online):** code không còn giả định chỉ có một nhân vật. Logic game dùng nhân vật cụ thể hoặc `Players.All`; `Players.Local` chỉ dành cho HUD, camera và phím bấm. Dữ liệu của nhân vật (chỉ số, túi đồ, nhiệm vụ, Bách Khoa Trùm) nằm trên nhân vật. Kế hoạch và quy tắc: `Docs/KeHoach-Online.md`.
- **Kiểm tra online tự động:** chạy cùng lúc `RungThiTham.exe -host -netsmoke` và `RungThiTham.exe -client 127.0.0.1 -netsmoke -batchmode -nographics`; mỗi bên đi qua lại, phải thấy nhân vật bên kia đi, rồi thoát (mã 0 là đạt).
- **Chạy test tự động:** `RungThiTham.exe -autoshot -autoshotDir "D:\shots"` sẽ tự chơi một vòng, chụp màn hình rồi thoát. Mã thoát 0 là sạch, 1 là có lỗi trong log, 2 là quá thời hạn (`-autoshotTimeout`, mặc định 480 giây).
- **Tour thảo nguyên:** thêm `-autoshotOnly steppe` để chụp cửa hang, trại, gió, Cột Gió, ba loài quái, cảnh đêm và bản đồ. Thêm `-autoshotOnly hacphong` để chụp bù nhìn, bọn cướp, Bò Rừng Sắt và mọi đòn của Thủ Lĩnh Hắc Phong. Tour `-autoshotOnly spells` chụp Bí Kíp, cửa sổ Sách Chiêu và cả 16 chiêu mới. Test cơ chế: `SteppeTests`, `HacPhongTests`, `SpellbookTests`; đường nhiệm vụ: `QuestRoadTests` (EditMode, tự vào Play Mode).
- **CI (GitHub Actions):** mỗi lần push đều biên dịch thử C#; test Unity, bản build Windows mỗi đêm và AutoShot chạy khi repo có secret giấy phép Unity. Xem `Docs/CI.md`.

## Giấy phép

- Font **Inter** (SIL Open Font License 1.1), lấy từ bộ cài Unity.
- **FishNet** (thư viện mạng, FirstGearGames): giấy phép riêng của FishNet, miễn phí khi dùng trong game; cài qua Package Manager, không chép mã nguồn vào repo.
- Font pixel **Galmuri7**, **Galmuri11** của Lee Minseo (SIL Open Font License 1.1), bản rút gọn Latin + tiếng Việt do `Tools/FontGen/make_pixel_fonts.py` tạo; giấy phép ở `Assets/Fonts/Galmuri-LICENSE.txt`.
- Toàn bộ art/âm thanh còn lại do script trong `Tools/` sinh ra. Bộ icon Franuka chỉ dùng làm tham khảo phong cách, không có trong project.

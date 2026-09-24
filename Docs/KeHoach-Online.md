# Kế hoạch online — Rừng Thì Thầm (hướng C: thế giới online nhiều người)

> Trạng thái: **giai đoạn 0 và 1 xong** (24/09/2026, trên `main`): nhiều người vào cùng một thế giới và thấy nhau đi lại. Tiếp theo là giai đoạn 2. Các con số thời gian là ước lượng thô cho 1 lập trình viên toàn thời gian; team 3 người (xem `KeHoach-RungThiTham.md`) thì chia bớt phần code, không chia được phần thử nghiệm.

## 1. Mục tiêu

- Nhiều người chơi cùng ở một thế giới liên tục, gặp nhau, đánh quái và boss cùng nhau. **Chỉ PvE**: người chơi không đánh nhau.
- **Mọi dữ liệu nhân vật lưu trên server** (không lưu trên máy người chơi), đổi máy vẫn chơi tiếp, không sửa file để gian lận được.
- Mốc đầu tiên **không phải MMO đầy đủ**, mà là *vertical slice online*: 1 vùng (Rừng Thì Thầm), 1 kênh, **thử nghiệm với khoảng 20 người**, có tài khoản và lưu nhân vật.
- **Giữ bản chơi offline một người**, dùng chung code với bản online (`GameSession.Mode`: Offline / Host / Client / Server).

## 2. Kiến trúc đề xuất

```text
                ┌─────────────────────────────┐
  Game client ──►  Backend (Nakama)           │  đăng ký / đăng nhập, danh sách nhân vật,
  (Unity)       │  + PostgreSQL               │  chọn kênh, chat, bạn bè, party
     │          └──────────────▲──────────────┘
     │ UDP (FishNet)           │ server-to-server: đọc/ghi nhân vật
     ▼                         │
  ┌──────────────────────────────────────────┐
  │ Zone server (Unity headless build)       │  mỗi (vùng × kênh) = 1 tiến trình
  │ chạy scene vùng + toàn bộ logic game     │  ví dụ: RungThiTham-k1, RungThiTham-k2
  └──────────────────────────────────────────┘
```

| Thành phần | Lựa chọn | Vì sao |
|---|---|---|
| **Mạng trong game** | **FishNet** (miễn phí, mã nguồn mở) | Có sẵn server authoritative, client-side prediction, *observer* (chỉ gửi dữ liệu những thứ ở gần người chơi), chạy dedicated server headless. Netcode for GameObjects hợp với co-op nhỏ hơn là thế giới nhiều người. |
| **Zone server** | Build Unity *Dedicated Server* (headless) | Dùng lại nguyên code chiến đấu, AI, boss, skill hiện có — lợi thế lớn nhất khi ở lại Unity. |
| **Tài khoản + dữ liệu** | **Nakama** (mã nguồn mở, tự host bằng Docker) + **PostgreSQL** | Có sẵn đăng nhập, lưu trữ, chat, bạn bè, nhóm/guild, bảng xếp hạng. Phương án thay thế: tự viết ASP.NET Core + PostgreSQL (cùng ngôn ngữ C#, nhưng phải tự làm hết). |
| **Hạ tầng** | Giai đoạn đầu: 1 VPS + Docker Compose | Rẻ, đơn giản. Khi đông người: nhiều VPS hoặc Kubernetes + Agones để tự bật/tắt zone server. |

**Nguyên tắc số 1: server quyết định mọi thứ.** Client chỉ gửi phím bấm/ý định ("dùng Cầu Lửa về hướng X"), server tính sát thương, máu, rơi đồ, XP, nhiệm vụ rồi gửi kết quả về. Client không bao giờ tự ghi dữ liệu nhân vật.

## 3. Những gì trong code hiện tại phải đổi

| Chỗ hiện tại | Vấn đề khi online | Cách đổi |
|---|---|---|
| ✓ `GameManager.I.player` — 1 nhân vật duy nhất, dùng ở 16 file | Server có nhiều người chơi | Xong ở giai đoạn 0: `Players.All` / `Players.Local`; quái chọn mục tiêu theo bảng thù hận. |
| ✓ `TimeFX` / `Time.timeScale` cho hit-stop và Lướt Hoàn Hảo | Không thể làm chậm cả thế giới vì một người | Xong ở giai đoạn 0: khi không phải offline, hit-stop chỉ giữ hình nhân vật trúng đòn, không làm chậm thời gian; phần thưởng Lướt Hoàn Hảo giữ nguyên. |
| `SaveManager` ghi JSON vào `AppData\LocalLow\...\saves`, 3 ô lưu | Người chơi sửa được file | Server ghi vào DB. 3 ô lưu → 3 ô nhân vật trên mỗi tài khoản. Giai đoạn 0 đã tách được dữ liệu từng nhân vật (`SaveManager.CaptureCharacter`). |
| ✓ Menu Esc "Tạm dừng" | Thế giới online không dừng | Xong ở giai đoạn 0: chỉ offline mới dừng thời gian. |
| Cheat F5–F9 và bảng lệnh `` ` `` | Ai cũng dùng được | Chỉ tài khoản GM, lệnh chạy trên server. |
| Lệnh Yarn `<<give_item>>`, `<<quest_complete>>`… chạy trên client | Gian lận được | Client gửi yêu cầu, server kiểm tra điều kiện rồi mới thực hiện. |
| `DayNightCycle` mỗi máy tự chạy | Mỗi người một giờ khác nhau | Giờ do server phát, client chỉ hiển thị. |
| Boss `BossBear` lưu trạng thái trong save của người chơi | Boss là của chung | Boss sống trên zone server, có thời gian hồi sinh; phần thưởng chia theo đóng góp sát thương, mỗi người nhận loot riêng. |

### Dữ liệu lưu: tách từ các `ISaveable` đang có

Code đã chia save thành từng phần (`SaveKey`), nên có thể **dùng lại JSON của từng phần** làm cột dữ liệu trong DB ở giai đoạn đầu:

| `SaveKey` hiện tại | Online lưu ở đâu |
|---|---|
| `player`, `inventory`, `quests`, `bestiary`, `dialogue` | Theo **nhân vật** (bảng `characters`, mỗi phần một cột JSONB) |
| `world`, `boss:*`, `time` | Theo **server/kênh**, không lưu trong nhân vật |

- **Khi nào ghi:** ngay khi có sự kiện quan trọng (lên cấp, nhận/mất đồ, xong nhiệm vụ, hạ boss) + định kỳ 30–60 giây + khi thoát/mất kết nối.
- **Vật phẩm:** mỗi món đồ có ID riêng; mọi thay đổi túi đồ (đặc biệt khi sau này có giao dịch) ghi trong một transaction để không nhân bản đồ được.
- **Sao lưu:** backup PostgreSQL tự động mỗi ngày.

## 4. Vấn đề riêng của game hành động

Game có Lướt Hoàn Hảo (cửa sổ 0.15 s), input buffer 150 ms, vòng cảnh báo né đòn boss — rất nhạy với độ trễ mạng (ping 50–150 ms ở Việt Nam đi server trong nước).

- **Di chuyển và Lướt:** máy người chơi tự điều khiển nhân vật mình (client-authoritative, xem mục 9) nên không có độ trễ khi bấm; giai đoạn 3 thêm kiểm tra trên server (tốc độ, dịch chuyển bất thường) để chống gian lận di chuyển.
- **Né đòn:** server "tua lại" vị trí theo thời điểm người chơi bấm (lag compensation), và nghiêng về phía người né — né thấy trên màn hình là né được.
- **Vòng cảnh báo của boss:** hiện sớm hơn trên client một chút để bù trễ.
- **Server đặt ở Việt Nam hoặc Singapore** để giữ ping thấp.

## 5. Lộ trình

| Giai đoạn | Nội dung | Kết quả kiểm tra được | Ước lượng |
|---|---|---|---|
| **0. Chuẩn bị** ✓ | Tách logic khỏi hiển thị; bỏ giả định 1 người chơi (16 file); đổi `TimeFX` thành hiệu ứng cục bộ; quái chọn mục tiêu theo aggro. Game vẫn chơi một người như cũ. | Toàn bộ test hiện có vẫn qua; chơi offline không đổi. **Xong** — xem mục 8. | 2–4 tuần |
| **1. Hai người thấy nhau** ✓ | Cài FishNet; build dedicated server; 2 client vào cùng vùng, đi lại, Lướt, thấy nhau. | Chạy server trên máy, 2 cửa sổ game thấy nhau di chuyển mượt. **Xong** — xem mục 9. | 3–5 tuần |
| **2. Tài khoản và lưu nhân vật** | Nakama + PostgreSQL bằng Docker; đăng ký/đăng nhập; tạo/chọn nhân vật; server nạp và ghi dữ liệu nhân vật. | Thoát game, mở lại trên máy khác, nhân vật còn nguyên. | 3–5 tuần |
| **3. Chiến đấu online** | Skill, sát thương, trạng thái, quái, bãi hồi sinh, boss Gấu Ma, Thanh Trấn Áp, rơi đồ, XP, nhiệm vụ, hội thoại — tất cả do server quyết định; lag compensation. | 3–5 người cùng hạ Gấu Ma, ai cũng nhận thưởng đúng. | 6–10 tuần |
| **4. Thế giới** | Nhiều vùng, chuyển vùng giữa các zone server, kênh (k1, k2…), chat, party, danh sách bạn. | Đi từ Làng sang Rừng, đổi kênh, chat được. | 4–6 tuần |
| **5. Vận hành** | Đưa lên VPS; giám sát, log, backup; chống gian lận cơ bản; thử tải 50+ người (dùng bot client — tái dùng chế độ `-autoshot` tự chơi). | Chạy thử kín với người thật. | 3–5 tuần |

**Tổng: khoảng 5–8 tháng** cho 1 người toàn thời gian để tới bản chạy thử kín. Chưa tính nội dung mới (vùng, quái, boss) và các hệ thống MMO thường có sau này (giao dịch, chợ, guild, PvP).

## 6. Chi phí vận hành (tham khảo)

- Giai đoạn phát triển: chạy hết trên máy mình, **0 đồng**.
- Chạy thử kín: 1 VPS 4 CPU / 8 GB RAM, khoảng vài trăm nghìn đến 1–2 triệu đồng/tháng.
- Khi mở rộng: tăng theo số người chơi cùng lúc; cần tính thêm băng thông và backup.

## 7. Đã chốt và còn mở

| Câu hỏi | Trả lời |
|---|---|
| Số người cùng lúc ban đầu | **Khoảng 20 người** để thử nghiệm (đã chốt). |
| PvE hay PvP | **Chỉ PvE** (đã chốt). |
| Giữ bản offline một người | **Giữ, dùng chung code** (đã chốt). |
| Tự thuê VPS hay dịch vụ có sẵn | Còn mở. Mặc định: tự thuê VPS + Docker. |
| Chỉ Windows/PC | Còn mở. Mặc định: có. |
| Ai quyết định vị trí nhân vật | Đề xuất (giai đoạn 1, có thể đổi): máy người chơi, server kiểm tra từ giai đoạn 3. Mọi thứ khác do server quyết định. |

## 8. Giai đoạn 0 đã làm gì

Game offline chơi như cũ (93 test EditMode qua, AutoShot trên bản build thoát mã 0), nhưng code không còn giả định chỉ có một nhân vật.

| Phần | Trước | Bây giờ |
|---|---|---|
| **Danh sách nhân vật** | `GameManager.I.player` | `Players.All` (mọi nhân vật trong thế giới), `Players.Local` (nhân vật máy này điều khiển: HUD, camera, phím bấm). `GameManager.localPlayer` chỉ dùng để gán `Players.Local` lúc chạy offline. |
| **Dữ liệu từng nhân vật** | `PlayerStats.I`, `Inventory.I`, `QuestSystem.I` và `Bestiary` tĩnh, nằm trên `[Game]` | Mỗi nhân vật mang `PlayerStats`, `Inventory` (túi đồ trên prefab là đồ khởi đầu của nhân vật mới), `QuestSystem` và `Bestiary` riêng: `hero.stats`, `hero.inventory`, `hero.quests`, `hero.bestiary`. |
| **Đầu vào** | `PlayerController` đọc phím trong lúc hành động | Máy điều khiển đọc phím và chuột thành `PlayerIntent` (đi hướng nào, bấm chiêu nào, ngắm đâu, uống bình nào, nói với ai); nhân vật nào cũng hành động theo `PlayerIntent` giống nhau. Giai đoạn 1: client gửi `PlayerIntent` lên server. |
| **Quái và boss** | Luôn đuổi "người chơi" | `ThreatTable`: sát thương cộng thù hận, người đi vào tầm aggro được cộng một ít; đuổi người bị ghét nhất. Boss thức khi có ai lại gần, chỉ reset khi mọi người chết hoặc rời đấu trường. |
| **Công hạ quái** | Người chơi duy nhất nhận XP, tiến độ nhiệm vụ, Bách Khoa Trùm | `Health.Attackers` ghi mọi người đã gây sát thương; `KillInfo.credited` chia cho tất cả họ (mỗi người đủ XP, hợp với PvE). Hạ bằng lệnh/cheat thì tính cho mọi người. |
| **Nhặt đồ** | Vào túi người chơi duy nhất | Bay vào người gần nhất và vào túi người đó. |
| **Thời gian** | Hit-stop, slow motion, tạm dừng đổi `Time.timeScale` | `GameSession.OwnsTime`: chỉ offline mới đổi. Thế giới chung: hit-stop giữ hình nhân vật trúng đòn (`SpriteAnimator.Hold`), không slow motion, menu không dừng game. |
| **Lưu game** | Mọi `ISaveable` vào một file | `ICharacterSaveable` (phần của một nhân vật) tách khỏi phần của thế giới. `SaveManager.CaptureCharacter(hero)` / `RestoreCharacter(hero, …)` là thứ server sẽ lưu vào DB cho từng nhân vật. File lưu offline vẫn cùng định dạng, save cũ vẫn tải được. |
| **Hiển thị riêng từng máy** | — | Nhật ký, banner, âm thanh 2D, rung màn hình, cảnh báo máu thấp, màn hình chết chỉ hiện với nhân vật của máy này; nhân vật khác nghe âm thanh theo vị trí. |

**Quy tắc cho code mới** (để không phải làm lại khi lên online):

- Không dùng `Players.Local` trong logic game; chỉ dùng cho HUD, camera, phím bấm, hiệu ứng màn hình. Logic game làm việc với nhân vật cụ thể (người gây sát thương, người nhặt đồ, người nói chuyện…).
- Không đổi `Time.timeScale` ngoài `TimeFX`.
- Dữ liệu thuộc về một nhân vật thì đặt trên nhân vật và cài `ICharacterSaveable`.
- Thông báo cho người chơi (log, banner, âm thanh 2D) chỉ phát khi nhân vật đó là `IsLocal`.

**Để lại cho giai đoạn sau:** camera, nhạc, cảnh mở màn và thanh máu của boss vẫn phát cho máy đang chơi (giai đoạn 3 sẽ chỉ phát cho người ở gần); hội thoại chạy trên máy người chơi và lệnh Yarn vẫn đổi dữ liệu trực tiếp (giai đoạn 3 chuyển thành yêu cầu gửi server).

## 9. Giai đoạn 1 đã làm gì

Nhiều người vào cùng một thế giới, thấy nhau và thấy nhau đi lại mượt. Thư viện mạng: **FishNet 4.7.3**, cài bằng Package Manager từ GitHub và ghim đúng phiên bản trong `Packages/manifest.json`. Truyền qua UDP (Tugboat), cổng mặc định **7770**.

### Cách chạy

| Muốn | Chạy | Ghi chú |
|---|---|---|
| Mở thế giới trên máy mình và chơi luôn | `RungThiTham.exe -host` | Máy này vừa là máy chủ vừa chơi. |
| Vào thế giới của máy khác | `RungThiTham.exe -client 192.168.1.5` | Thay bằng địa chỉ IP của máy mở. Không ghi địa chỉ thì vào máy này. |
| Máy chủ riêng, không có nhân vật | `RungThiTham.exe -server -batchmode -nographics` | Chạy nền, không mở cửa sổ; xem log để biết ai vào/ra. |
| Đổi cổng | thêm `-port 7780` | Máy mở và máy vào phải cùng cổng. |

Trong game, bảng lệnh `` ` `` có `host`, `join <địa chỉ>`, `leave` (về chơi một mình) và `net` (xem trạng thái). Bảng lệnh khởi động lại game ở chế độ mới; tiến độ chơi một mình chưa lưu sẽ mất, nên lưu trước.

- **Thử trên một máy:** mở một cửa sổ `-host` và một cửa sổ `-client`.
- **Trong cùng mạng LAN:** máy mở chạy `-host`, máy khác `-client <IP LAN của máy mở>`. Lần đầu mở, Windows có thể hỏi cho phép game dùng mạng: chọn cho phép trong mạng riêng (Private), nếu không máy khác không vào được.
- **Qua Internet:** máy mở phải mở cổng UDP 7770 trên router, hoặc cả nhóm dùng một mạng LAN ảo (VPN). Giai đoạn 5 sẽ có máy chủ thuê riêng nên không cần bước này.

### Cách hoạt động

- **Không đụng tới bản offline:** mọi thứ về mạng chỉ được tạo khi chơi online (`OnlineSession`). Chơi offline không có đối tượng mạng nào.
- **Nhân vật online** là `Prefabs/Characters/NetHero`, một biến thể (variant) của prefab Player có thêm `NetworkObject`, `NetworkTransform` và `NetworkHero`. Máy chủ tạo một nhân vật cho mỗi người khi họ kết nối xong. Online, nhân vật đặt sẵn trong scene Core không được dùng.
- **Di chuyển do người chơi điều khiển** (client-authoritative): máy của mỗi người tự di chuyển nhân vật mình như lúc offline, `NetworkTransform` gửi vị trí cho máy chủ và máy chủ chuyển cho mọi người. Lý do: game hành động cần điều khiển tức thì cả khi mạng trễ, và game chỉ PvE nên rủi ro gian lận di chuyển thấp. Giai đoạn 3 thêm kiểm tra tốc độ trên máy chủ. Mọi thứ khác (sát thương, quái, rơi đồ, XP, nhiệm vụ) sẽ do máy chủ quyết định.
- **Nhân vật của người khác** trên màn hình mình là "con rối" (`PlayerController.Puppet`): đi theo vị trí nhận được, tự chọn hoạt ảnh đi/lướt/đứng theo tốc độ, có bảng tên "Người chơi N" và không tự làm gì. Các nhân vật đi xuyên qua nhau.

### Chưa có ở giai đoạn 1 (có chủ đích)

- **Quái và boss tắt** khi chơi online, vì chưa đồng bộ (giai đoạn 3). Nếu để lại, mỗi người sẽ đánh một bản quái khác nhau.
- NPC, hội thoại, nhiệm vụ, túi đồ, cấp độ: vẫn chạy riêng trên máy từng người, chưa đồng bộ (giai đoạn 3).
- **Không lưu** khi chơi online (giai đoạn 2 lưu trên máy chủ). Ngày/đêm mỗi máy một giờ. Chưa có đăng nhập.
- Chưa có menu chính để chọn chơi online: dùng dòng lệnh, bảng lệnh hoặc shortcut.

### Đã kiểm tra

- Test EditMode `OnlineSessionTests`: chạy một host thật trong editor (máy chủ và người chơi cùng một máy), kiểm tra nhân vật được tạo qua mạng, camera đi theo, quái tắt, không lưu được; con rối đi theo vị trí và đổi hoạt ảnh; đọc `-host` / `-server` / `-client` / `-port`.
- `Debug/NetSmoke.cs`: chạy bản build thành hai tiến trình trên một máy (`-host -netsmoke` và `-client 127.0.0.1 -netsmoke -batchmode -nographics`). Mỗi bên tự đi qua lại và phải thấy nhân vật bên kia đi (kể cả hoạt ảnh đi). Lần chạy 24/09: host thấy bên kia đi 18.0–18.2 đơn vị, client thấy 18.6–18.7 đơn vị, không lỗi.

### Lỗi đã biết

- **Hai cửa sổ game trên cùng một máy: cửa sổ đóng sau có thể crash lúc thoát.** Thấy 2/2 lần chạy hai cửa sổ; không gặp khi chỉ có một cửa sổ (offline, host không ai vào, hoặc client chạy nền `-batchmode -nographics`). Crash nằm trong phần Unity tắt cửa sổ (Windows UI Automation, lệnh `UiaDisconnectAllProviders`), sau khi mọi code của game và FishNet đã dừng; không ảnh hưởng lúc chơi và không mất gì (online chưa lưu). Thử hai máy khác nhau thì không có tình huống này. Nếu cần xử lý: thử bản vá Unity 6000.6 mới hơn, hoặc báo lỗi cho Unity kèm file `crash.dmp`.

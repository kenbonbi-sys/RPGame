# Máy chủ online luôn bật — Rừng Thì Thầm

Thế giới online chạy trên một **máy chủ riêng**: một bản của game chạy nền, không cửa sổ, không nhân vật, luôn bật. Người chơi chỉ cần mở game, bấm **Vào thế giới**: game tự tìm máy chủ, đăng nhập bằng nhân vật đã nhớ và vào. Không phải nhập IP. Kế hoạch tổng: `Docs/KeHoach-Online.md`.

## 1. Cài máy chủ trên máy của bạn

1. Build game: Unity → **Tools/RPG/Build Windows Player** (ra `Builds/Windows/RungThiTham.exe`).
2. Chạy (PowerShell thường, không cần quyền quản trị):

   ```bash
   powershell -ExecutionPolicy Bypass -File Tools/Server/install-server.ps1
   ```

   Script chép bản build vào `%LOCALAPPDATA%\RungThiTham-Server\game`, đăng ký tác vụ **RungThiTham Server** để máy chủ **tự chạy mỗi khi bạn đăng nhập Windows**, rồi bật máy chủ ngay. Máy chủ tắt hay lỗi thì tự bật lại sau 5 giây (`run-server.ps1`).

   Nhiều kênh (khi 20 người một kênh không đủ): thêm `-Channels 2` (tối đa 4). Kênh n chạy ở cổng 7770 + 2(n − 1), mọi kênh dùng chung dữ liệu; một nhân vật chỉ ở một kênh một lúc.
3. **Tường lửa** (một lần, bạn tự làm vì cần quyền quản trị): mở PowerShell bằng *Run as administrator* rồi chạy

   ```bash
   New-NetFirewallRule -DisplayName 'Rung Thi Tham Server' -Direction Inbound -Protocol UDP -LocalPort 7770-7771 -Action Allow -Profile Any
   ```

   Cổng 7770 là cổng chơi, 7771 để game của người khác tự tìm thấy máy chủ. Hai kênh: `-LocalPort 7770-7773` (script cài in đúng lệnh).
4. **Không cho máy ngủ** khi cắm điện (Settings → System → Power & sleep → Sleep: Never). Máy ngủ thì máy chủ dừng.

Cập nhật máy chủ sau khi build bản mới: chạy lại `install-server.ps1` (dữ liệu người chơi giữ nguyên). Dừng: `Tools/Server/stop-server.ps1`: dừng vòng tự bật lại, rồi xin máy chủ tắt đúng cách (nó báo người đang chơi, lưu mọi nhân vật rồi thoát; game của họ tự vào lại khi máy chủ chạy lại); lần đăng nhập Windows sau máy chủ lại chạy. Xem tình hình: `Tools/Server/status-server.ps1` (mục 7). Gỡ hẳn chế độ tự chạy: `Unregister-ScheduledTask -TaskName "RungThiTham Server"`.

Chạy script từ một ứng dụng dạng gói MSIX (ví dụ Claude desktop): chương trình mở từ đó chỉ thấy một bản `AppData` riêng, tác vụ lúc đăng nhập không thấy các file ghi vào đó. `install-server.ps1` tự nhận ra và cài qua Task Scheduler (thấy `AppData` thật), rồi in lại kết quả.

## 2. Người chơi vào bằng cách nào

| Người chơi ở đâu | Cần làm |
|---|---|
| Cùng mạng wifi / LAN với máy chủ | Không gì cả: game tự dò trong mạng. |
| Nhà khác, dùng **Radmin VPN** | Vào cùng network Radmin với máy chủ. Game tự dò trong mạng Radmin, và thử thêm địa chỉ ghi trong `servers.txt` (mặc định `26.253.10.125`, địa chỉ Radmin của máy chủ hiện tại). |
| Qua Internet không VPN | Cần máy chủ có địa chỉ công khai: mở cổng UDP 7770–7771 trên router hoặc thuê VPS (mục 5), rồi ghi địa chỉ đó vào `servers.txt`. |

- **`servers.txt`**: danh sách địa chỉ game thử (mỗi dòng một địa chỉ, có thể kèm `:cổng`). Nằm trong `Assets/StreamingAssets` và được đóng gói vào `RungThiTham_Data/StreamingAssets/servers.txt` cạnh game: sửa file này là đổi máy chủ cho mọi người, không cần build lại.
- Trên màn hình chính còn ô **Máy chủ khác** để một người tự gõ địa chỉ (game nhớ lại).
- Lần đầu vào: nhập **tên nhân vật** và **mật khẩu** (tên mới thì bấm *Tạo nhân vật mới*). Game nhớ đăng nhập trên máy đó; đổi máy thì nhập lại tên và mật khẩu là có nhân vật cũ. Mật khẩu không bao giờ gửi qua mạng: máy người chơi chỉ gửi bằng chứng đã ký (xem `Net/LoginCrypto.cs`).
- Mất kết nối: game về màn hình chính, báo lý do và **tự vào lại sau 5 giây** (nếu máy chủ chưa chạy lại thì tìm tiếp tới khi thấy).
- Nhiều kênh: màn hình chính hiện các kênh và tự vào kênh còn chỗ có số nhỏ nhất. Trong game gõ `/kenh` để xem, `/kenh 2` để sang kênh 2.

### Lệnh chat (bấm Enter trong game)

| Gõ | Làm gì |
|---|---|
| chữ bình thường | nói với mọi người trong kênh |
| `/n <lời>` | nói với tổ đội |
| `/w <tên> <lời>` | nhắn riêng một người |
| `/moi <tên>` · `/dongy` · `/tuchoi` · `/roi` · `/duoi <tên>` | mời vào tổ đội · nhận · từ chối (hoặc bấm Y / N khi hộp lời mời hiện) · rời · mời ra (đội trưởng) |
| `/ketban <tên>` · `/huyban <tên>` · `/banbe` | bạn bè: thêm · bỏ · xem ai đang chơi, ở kênh nào |
| `/kenh` · `/kenh <số>` | xem các kênh · đổi kênh |
| `/ai` · `/giup` | ai đang chơi · danh sách lệnh |

Tổ đội tối đa 5 người; thành viên đứng gần chỗ quái chết cùng nhận XP, tiến độ nhiệm vụ và đồ rơi riêng.

## 3. Dữ liệu và sao lưu

Tất cả nằm trong `%LOCALAPPDATA%\RungThiTham-Server\data`:

| Thư mục / file | Nội dung |
|---|---|
| `accounts/` | Mỗi tài khoản một file: tên, muối (salt) và khóa đăng nhập. Không có mật khẩu. |
| `characters/` | Mỗi nhân vật một file, cùng định dạng file lưu offline: cấp, chỉ số, túi đồ, nhiệm vụ, Bách Khoa Trùm, biến hội thoại, vị trí. Bản trước giữ dạng `.bak`. |
| `backups/yyyy-MM-dd/` | Bản sao `accounts` + `characters` mỗi ngày một lần, giữ 14 ngày gần nhất. |
| `gm.txt` | Tên các tài khoản quản trị (GM), mỗi dòng một tên. Sửa lúc máy chủ đang chạy cũng được. |
| `online/` | Một file `.lock` cho mỗi nhân vật đang chơi (ở kênh nào). Máy chủ tự dọn; file bị bỏ lại sau khi máy chủ lỗi không có tác dụng gì. |
| `status-k<kênh>.json` | Tình hình kênh, ghi 10 giây một lần (mục 7). |

Máy chủ lưu một nhân vật: 30 giây một lần khi có thay đổi, khoảng 2 giây sau khi lên cấp hoặc hạ boss, khi người chơi thoát, và toàn bộ khi máy chủ tắt. Khôi phục một nhân vật: dừng máy chủ, chép file từ `backups/<ngày>/characters/` về `characters/`, bật lại.

Log: `%LOCALAPPDATA%\RungThiTham-Server\logs\server-k<kênh>-*.log` (ai vào, ai ra, lưu, chat, lệnh GM, người bị kéo về vì đi quá nhanh, lỗi). Mỗi kênh giữ 30 file mới nhất. Danh sách bạn bè lưu trong file tài khoản.

## 4. Quản trị (GM)

- Thêm tên vào `data/gm.txt`. GM mở bảng lệnh bằng phím `` ` `` trong game: các lệnh thay đổi thế giới (`heal`, `level 10`, `give potion_red 5`, `tp boss`, `kill`, `time 0.5`, `quest …`, `save`) chạy trên máy chủ, cho nhân vật của GM. Người thường gõ thì bị từ chối. Phím F5–F9 cũng thành lệnh gửi máy chủ.
- Lệnh ai cũng dùng: `players` (ai đang chơi, kèm ping khi xem từ máy chủ), `net` (trạng thái, ping), `say <lời>` (hoặc bấm **Enter** để chat, mục 2), `leave` (về màn hình chính).

## 5. Khi muốn máy chủ chạy 24/7 không phụ thuộc máy nhà

Thuê một VPS Windows (giai đoạn 5 của kế hoạch), chép thư mục `Tools/Server` và bản build lên, chạy `install-server.ps1` như trên, mở cổng UDP 7770–7771 trong tường lửa của nhà cung cấp, rồi ghi địa chỉ VPS vào `servers.txt` của bản game phát cho mọi người. Không cần VPN nữa.

## 6. Chạy tay và kiểm tra

- Máy chủ trong cửa sổ dòng lệnh (xem log trực tiếp):

  ```bash
  Builds/Windows/RungThiTham.exe -server -batchmode -nographics -logFile - -data ServerData
  ```

  Tham số: `-port 7770`, `-maxplayers 20`, `-servername "Rừng Thì Thầm"`, `-data <thư mục>`.
- Vào thẳng một máy chủ bỏ qua màn hình chính: `RungThiTham.exe -client 26.253.10.125 -login <tên> <mật khẩu>`.
- Chơi thử một máy (máy này vừa mở thế giới vừa chơi): `RungThiTham.exe -host`.
- Kiểm tra tự động (máy chủ + 2 người chơi: thấy nhau, tổ đội, chat, bạn bè, đánh quái; vào lại xem nhân vật còn nguyên; đổi kênh 1 → 2):

  ```bash
  powershell -ExecutionPolicy Bypass -File Tools/Server/netsmoke.ps1
  ```
- Thử tải (máy chủ thử trên cổng 7799 và N bot chạy nền; mỗi bot khoảng 250 MB RAM):

  ```bash
  powershell -ExecutionPolicy Bypass -File Tools/Server/loadtest.ps1 -Bots 10 -Seconds 120
  ```

  Lần chạy 24/09 với 10 bot: máy chủ 48–57 FPS, 14% một nhân CPU, khoảng 234 MB, không lỗi.
- Một bot tự chơi vào máy chủ bất kỳ: `RungThiTham.exe -client <địa chỉ> -login Bot1 matkhau -register -bot -botSeconds 300 -batchmode -nographics`.

## 7. Giám sát

```bash
powershell -ExecutionPolicy Bypass -File Tools/Server/status-server.ps1
```

In ra: tác vụ tự chạy, số tiến trình máy chủ, rồi mỗi kênh: số người (tên), chạy bao lâu, FPS (bình thường khoảng 60), bộ nhớ, số tài khoản, số lỗi từ lúc chạy, câu trả lời khi hỏi như game của người chơi, và các dòng lỗi / cảnh báo mới nhất trong log. File trạng thái cập nhật quá 30 giây là máy chủ kênh đó có thể đã dừng. Máy chủ ghi cảnh báo vào log khi dưới 25 FPS.

Chống gian lận: máy chủ kéo về chỗ cũ nhân vật nào đi xa hơn mức có thể (dịch chuyển tức thời, chạy nhanh gấp nhiều lần) và ghi `put back` vào log. Người chơi bình thường, kể cả mạng giật, không bị kéo.

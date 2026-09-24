# Máy chủ online luôn bật — Rừng Thì Thầm

Thế giới online chạy trên một **máy chủ riêng**: một bản của game chạy nền, không cửa sổ, không nhân vật, luôn bật. Người chơi chỉ cần mở game, bấm **Vào thế giới**: game tự tìm máy chủ, đăng nhập bằng nhân vật đã nhớ và vào. Không phải nhập IP. Kế hoạch tổng: `Docs/KeHoach-Online.md`.

## 1. Cài máy chủ trên máy của bạn

1. Build game: Unity → **Tools/RPG/Build Windows Player** (ra `Builds/Windows/RungThiTham.exe`).
2. Chạy (PowerShell thường, không cần quyền quản trị):

   ```bash
   powershell -ExecutionPolicy Bypass -File Tools/Server/install-server.ps1
   ```

   Script chép bản build vào `%LOCALAPPDATA%\RungThiTham-Server\game`, đăng ký tác vụ **RungThiTham Server** để máy chủ **tự chạy mỗi khi bạn đăng nhập Windows**, rồi bật máy chủ ngay. Máy chủ tắt hay lỗi thì tự bật lại sau 5 giây (`run-server.ps1`).
3. **Tường lửa** (một lần, bạn tự làm vì cần quyền quản trị): mở PowerShell bằng *Run as administrator* rồi chạy

   ```bash
   New-NetFirewallRule -DisplayName 'Rung Thi Tham Server' -Direction Inbound -Protocol UDP -LocalPort 7770,7771 -Action Allow -Profile Any
   ```

   Cổng 7770 là cổng chơi, 7771 để game của người khác tự tìm thấy máy chủ.
4. **Không cho máy ngủ** khi cắm điện (Settings → System → Power & sleep → Sleep: Never). Máy ngủ thì máy chủ dừng.

Cập nhật máy chủ sau khi build bản mới: chạy lại `install-server.ps1` (dữ liệu người chơi giữ nguyên). Dừng: `Tools/Server/stop-server.ps1` (dừng cả vòng tự bật lại; lần đăng nhập Windows sau máy chủ lại chạy). Gỡ hẳn chế độ tự chạy: `Unregister-ScheduledTask -TaskName "RungThiTham Server"`.

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
- Mất kết nối: game về màn hình chính, báo lý do và **tự vào lại sau 5 giây**.

## 3. Dữ liệu và sao lưu

Tất cả nằm trong `%LOCALAPPDATA%\RungThiTham-Server\data`:

| Thư mục / file | Nội dung |
|---|---|
| `accounts/` | Mỗi tài khoản một file: tên, muối (salt) và khóa đăng nhập. Không có mật khẩu. |
| `characters/` | Mỗi nhân vật một file, cùng định dạng file lưu offline: cấp, chỉ số, túi đồ, nhiệm vụ, Bách Khoa Trùm, biến hội thoại, vị trí. Bản trước giữ dạng `.bak`. |
| `backups/yyyy-MM-dd/` | Bản sao `accounts` + `characters` mỗi ngày một lần, giữ 14 ngày gần nhất. |
| `gm.txt` | Tên các tài khoản quản trị (GM), mỗi dòng một tên. Sửa lúc máy chủ đang chạy cũng được. |

Máy chủ lưu một nhân vật: 30 giây một lần khi có thay đổi, khoảng 2 giây sau khi lên cấp hoặc hạ boss, khi người chơi thoát, và toàn bộ khi máy chủ tắt. Khôi phục một nhân vật: dừng máy chủ, chép file từ `backups/<ngày>/characters/` về `characters/`, bật lại.

Log: `%LOCALAPPDATA%\RungThiTham-Server\logs\server-*.log` (ai vào, ai ra, lưu, chat, lệnh GM, lỗi).

## 4. Quản trị (GM)

- Thêm tên vào `data/gm.txt`. GM mở bảng lệnh bằng phím `` ` `` trong game: các lệnh thay đổi thế giới (`heal`, `level 10`, `give potion_red 5`, `tp boss`, `kill`, `time 0.5`, `quest …`, `save`) chạy trên máy chủ, cho nhân vật của GM. Người thường gõ thì bị từ chối. Phím F5–F9 cũng thành lệnh gửi máy chủ.
- Lệnh ai cũng dùng: `players` (ai đang chơi), `net` (trạng thái, ping), `say <lời>` (hoặc bấm **Enter** để chat), `leave` (về màn hình chính).

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
- Kiểm tra tự động (1 máy chủ + 2 người chơi, đánh quái, thoát rồi vào lại xem nhân vật còn nguyên):

  ```bash
  powershell -ExecutionPolicy Bypass -File Tools/Server/netsmoke.ps1
  ```

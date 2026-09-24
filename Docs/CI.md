# CI: kiểm tra tự động trên GitHub (T14)

File `.github/workflows/ci.yml`, chạy bằng GitHub Actions và [GameCI](https://game.ci).

| Job | Khi nào | Làm gì | Cần giấy phép Unity |
|---|---|---|---|
| **Compile check** | Mọi lần push, mọi PR | Biên dịch toàn bộ C# bằng `Tools/CompileCheck` (1–2 phút) | Không |
| **EditMode tests** | PR, push lên `main`/`dev`, mỗi đêm, chạy tay | Chạy toàn bộ test trong Unity; kết quả hiện thành check “EditMode test results” trên commit | Có |
| **Windows player** | Mỗi đêm, chạy tay | Build `RungThiTham.exe`, lưu 3 ngày ở mục Artifacts của lần chạy | Có |
| **AutoShot tour** | Sau bản build | Mở bản build trên máy Windows của GitHub, chạy `-autoshot`, lưu ảnh chụp và `player.log` | Có |

Chưa có giấy phép thì ba job Unity tự bỏ qua, chỉ Compile check chạy; lần chạy vẫn xanh và ghi chú “No UNITY_LICENSE…”.

## Bật các job Unity

Vào **Settings → Secrets and variables → Actions → New repository secret** của repo và thêm:

- **Unity Personal (miễn phí):**
  - `UNITY_LICENSE`: toàn bộ nội dung file `Unity_lic.ulf`. Kích hoạt giấy phép Personal trong Unity Hub trên máy mình trước, rồi mở file ở `C:\ProgramData\Unity\Unity_lic.ulf` (Windows) hoặc `~/.local/share/unity3d/Unity/Unity_lic.ulf` (Linux).
  - `UNITY_EMAIL`, `UNITY_PASSWORD`: tài khoản Unity đó.
- **Unity Pro / Plus:** `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD`.

Cách lấy giấy phép có thể đổi theo phiên bản Unity; nếu các bước trên không khớp, làm theo hướng dẫn “Activation” mới nhất trên game.ci.

## Build mỗi đêm

- Chạy lúc 02:00 giờ Việt Nam và build nhánh `dev`.
- GitHub chỉ chạy lịch hẹn giờ theo file workflow nằm trên nhánh mặc định (`main`), nên build đêm bắt đầu sau khi `ci.yml` được gộp vào `main`.
- Chạy tay bất cứ lúc nào: tab **Actions → CI → Run workflow**, chọn nhánh.

## Xem kết quả

Tab **Actions** → chọn lần chạy → phần **Artifacts** ở cuối trang:

- `RungThiTham-Windows`: bản build, tải về giải nén là chơi được.
- `AutoShot`: ảnh chụp tour tự động + `player.log`. Mã thoát của AutoShot: 0 là sạch, 1 là có lỗi hoặc exception trong log, 2 là tour không xong trong thời hạn.
- `test-results`: kết quả test dạng XML.

## Giới hạn cần biết

- **Phút chạy và dung lượng:** repo riêng tư trên gói GitHub Free có 2 000 phút mỗi tháng (máy Windows tính gấp đôi) và 500 MB lưu artifact. Vì vậy test Unity không chạy ở mọi lần push lên nhánh việc, bản build chỉ giữ 3 ngày, và LFS cùng thư mục `Library` được cache giữa các lần chạy. Repo công khai thì không giới hạn phút.
- **Lần đầu rất lâu:** Unity phải import toàn bộ project (có thể 20–40 phút); các lần sau dùng cache `Library`.
- **Máy CI không có GPU:** test chạy trong Docker trên Linux, bản build chạy trên máy Windows không có card đồ họa. Một test chỉ hỏng trên CI mà chạy được trong Editor thường là do phần đồ họa. Job AutoShot để ở chế độ “báo lỗi nhưng không chặn” cho tới khi chạy ổn định vài đêm.
- **Compile check chỉ kiểm tra biên dịch**, không chạy game. Xem `Tools/CompileCheck/README.md`.

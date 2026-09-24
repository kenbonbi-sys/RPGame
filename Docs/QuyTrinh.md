# Quy trình làm việc (T25)

Cụ thể hóa mục 19 của kế hoạch: nhịp 2 tuần, lúc nào cũng có bản chơi được, đo bằng số liệu.

## Nhịp sprint 2 tuần

| Khi nào | Việc | Kết quả |
|---|---|---|
| Thứ Hai tuần 1 | Lập kế hoạch sprint: chọn việc từ backlog (mục 18), tách việc cỡ L thành việc ≤ 2 ngày | Cột **Sprint** trên bảng việc |
| Hằng ngày | Làm trên `main`; CI phải xanh | Commit lên `main` |
| Thứ Sáu hằng tuần | Xem VFX Gallery (phím M để đo cả thư viện) và các đòn mới | Danh sách hiệu ứng cần sửa |
| Cuối tuần 2 | Cả team chơi thử 30 phút; quay video 1 phút | Bản build chơi được, clip cho devlog |

Để trống khoảng 20% thời gian mỗi sprint cho lỗi và việc phát sinh (mục 20: rủi ro kiệt sức). Trễ thì cắt phạm vi theo thứ tự ở mục 01, không tăng giờ.

## Bảng việc

Dùng **GitHub Issues + GitHub Projects** ngay trên repo: không tốn thêm công cụ và liên kết thẳng với commit, pull request.

- **Cột:** Backlog → Sprint → Đang làm → Chờ kiểm tra → Xong.
- **Mỗi thẻ một việc** của mục 18. Tiêu đề bắt đầu bằng mã việc: `T15 · Công thức sát thương`.
- **Nhãn:** ưu tiên `P0` `P1` `P2`; cỡ `S` `M` `L`; mảng (`chiến đấu`, `chiêu thức`, `quái & boss`, `NPC & nhiệm vụ`, `thế giới`, `tiến trình`, `visual`, `âm thanh`, `UI`, `kỹ thuật`, `sản xuất`).
- **Chờ kiểm tra** nghĩa là code đã xong nhưng còn phải chạy test trong Unity hoặc chơi thử (ví dụ việc làm từ phiên Claude trên cloud, xem dưới).
- Một việc chỉ sang **Xong** khi đạt Definition of Done.

## Nhánh

- Chỉ một nhánh: **`main`** (từ 24/09/2026, bỏ `dev` và các nhánh việc). Làm và commit thẳng trên `main`; bản build đêm lấy từ đây.
- `main` phải luôn build và chơi được: chạy test trước khi push.
- Push khi: test EditMode trong Unity qua, đã tự chơi thử; sau đó CI phải xanh.
- Commit bằng tiếng Anh, câu tóm tắt ở thể mệnh lệnh kèm mã việc, ví dụ `Save v1: 3 slots + autosave (T08)`; phần thân nói *vì sao*, không chỉ *làm gì*.
- Scene, prefab và asset là YAML: tránh hai người cùng sửa một scene trong một sprint. Bật UnityYAMLMerge theo hướng dẫn trong `.gitattributes`.

## Definition of Done

Một việc là xong khi:

- [ ] Chạy được trong bản build, Console không có lỗi.
- [ ] Có VFX và SFX, hoặc placeholder được đánh dấu rõ.
- [ ] Số liệu nằm trong ScriptableObject hoặc bảng tính, không viết cứng trong code.
- [ ] Lưu và tải lại được (nếu việc có trạng thái).
- [ ] Mọi chữ hiển thị có khóa bản địa hóa (thoại Yarn có `#line:`).
- [ ] Test EditMode qua hết (Window → General → Test Runner); việc mới có test cho phần logic.
- [ ] Tự chơi thử 5 phút; `RungThiTham.exe -autoshot` thoát với mã 0.
- [ ] CI xanh trên `main`.
- [ ] Cập nhật Bách Khoa Trùm, nhật ký, tooltip, `README.md` hoặc tài liệu trong `Docs/` nếu liên quan.

## Làm việc với Claude

| | Phiên trên máy (Claude Code desktop) | Phiên trên cloud (claude.ai/code) |
|---|---|---|
| Có Unity | Có: chạy được test, build, AutoShot | Không |
| Kiểm tra được | Test EditMode, build Windows, ảnh AutoShot | Chỉ biên dịch (`Tools/CompileCheck/check.sh`) và CI |
| Hợp với | Việc cần chỉnh scene, prefab, cảm giác chơi | Code, dữ liệu, tài liệu, CI |
| Sau khi xong | Commit và push lên `main` ngay | Push lên `main`, thẻ để ở **Chờ kiểm tra** tới khi chạy test trong Unity |

- **Luôn commit và push trước khi kết thúc phiên.** Việc chưa push chỉ nằm trên máy đó, phiên khác không thấy.
- Việc làm từ cloud chưa được chạy trong Unity: mở project, chạy Test Runner (hoặc để CI chạy khi đã có giấy phép) rồi mới chuyển thẻ sang **Xong**.

## Mức lỗi

| Mức | Ví dụ | Xử lý |
|---|---|---|
| **A** | Crash, mất save, kẹt không chơi tiếp được | Sửa ngay, chặn mọi bản phát hành |
| **B** | Sai số liệu rõ ràng, chiêu hoặc nhiệm vụ không chạy, lỗi hiển thị lớn | Sửa trong sprint hiện tại |
| **C** | Lỗi nhỏ về hình, chữ, âm thanh | Đưa vào backlog |

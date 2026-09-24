# Font pixel tiếng Việt (T19)

**Kết luận:** Galmuri7 (8 px) và Galmuri11 (12 px) có đủ 134 chữ cái tiếng Việt, vẽ đúng lưới điểm ảnh và dùng giấy phép SIL OFL. Hai cỡ này khớp với hai cỡ chữ của mục 13, nên không bắt buộc tự vẽ font trước bản demo. Rủi ro “font pixel tiếng Việt” ở mục 20 có thể hạ từ Trung bình xuống Thấp sau khi thử trong Unity (bước ở cuối trang).

Chọn font cho UI vẫn là việc của style guide (T18): Galmuri có thể là font chính, hoặc chỉ là font tạm cho tới khi có font tự vẽ.

## Đã thử

Đếm theo 134 chữ cái tiếng Việt ngoài ASCII (mọi nguyên âm với đủ 5 dấu thanh, cộng Đ/đ).

| Font | Cỡ gốc | Chữ tiếng Việt | Giấy phép | Nhận xét |
|---|---|---|---|---|
| **Galmuri7** | 8 px | 134/134 | OFL 1.1 | Đọc được ở 8 px: dấu thanh, móc (ơ ư) và dấu nặng tách rõ |
| Galmuri9 | 10 px | 134/134 | OFL 1.1 | Cỡ giữa, dự phòng cho log hoặc nameplate |
| **Galmuri11** | 12 px | 134/134 | OFL 1.1 | Rõ nhất; hợp với hội thoại và menu |
| GalmuriMono7/9/11, Galmuri14 | 8–15 px | 40/134 | OFL 1.1 | Thiếu ơ ư và các chữ có dấu thanh |
| VT323 | ~16 px | 134/134 | OFL 1.1 | Nét không nằm trên lưới điểm ảnh nên vỡ chữ ở cỡ nhỏ (xem ảnh); dáng chữ màn hình terminal |
| Handjet | — | 134/134 | OFL 1.1 | Font chấm dạng biến thể, không phải pixel art |
| Pixel Code, Monocraft | 9 px | 40–44/134 | OFL 1.1 | Thiếu tiếng Việt |
| Pixelify Sans, Silkscreen, Tiny5, Micro 5, Jersey, Bytesized, Sixtyfour, Workbench, DotGothic16, Press Start 2P | — | không có | OFL 1.1 | Google Fonts không có bộ chữ tiếng Việt cho các font này |

## So sánh

Phóng ×3, không khử răng cưa (giống atlas raster lọc Point trong TextMeshPro). Dòng mô tả của mỗi font ghi chiều cao dòng và dấu cao nhất so với ascent. Ảnh do script ở mục dưới tạo ra.

![Bốn font pixel với chữ tiếng Việt của game](font-pixel-tieng-viet.png)

## Cần biết khi dùng

- **Dấu chồng cao hơn ascent.** Trên chữ hoa có hai dấu (Ẫ, Ễ, Ở…), Galmuri7 lên tới 11 px trên chân chữ trong khi ascent là 8; Galmuri11 lên tới 17 px, ascent 12. Khối chữ nhiều dòng có chữ hoa mang dấu cần tăng Line Spacing của TMP, hoặc tránh viết hoa toàn bộ ở cỡ chữ nhỏ.
- **Chỉ phóng theo số nguyên:** 8 → 16 → 24 px, 12 → 24 → 36 px, lọc Point. Cỡ lẻ làm vỡ nét như VT323 trong ảnh. Theo mục 14, chữ nhỏ nhất là 8 px ×2 = 16 px trên màn 1080p.
- **Thiếu vài ký hiệu UI:** ► (Galmuri7), ✓ ✗ ☠ (cả hai). Giữ Inter làm fallback của font asset, hoặc đổi ► thành ▶ (cả hai font đều có).
- **Dung lượng:** file gốc nặng 3,7–5,4 MB vì có cả chữ Hàn. Bản rút gọn cho `Assets/Fonts` chỉ giữ Latin, tiếng Việt và ký hiệu (69 KB và 98 KB), đủ cho cả bản tiếng Anh. OFL cho phép rút gọn, và Galmuri không khai báo Reserved Font Name nên giữ nguyên tên được. Giấy phép: `Assets/Fonts/Galmuri-LICENSE.txt`.

## Thử trong Unity

1. **Tạo file font:** `pip install fonttools pillow`, rồi `python Tools/FontGen/make_pixel_fonts.py --preview`. Script tải Galmuri v2.404, rút gọn và ghi `Assets/Fonts/Galmuri7.ttf`, `Assets/Fonts/Galmuri11.ttf`, kèm ảnh so sánh `Docs/font-pixel-tieng-viet.png`. Commit các file này (Git LFS) cùng file `.meta` Unity tạo ra. Phiên Claude trên cloud không đẩy được file LFS lên GitHub, nên bước này làm trên máy.
2. **Tools/RPG/Pixel Font Test** tạo font asset TextMeshPro cho hai font (raster, một texel mỗi điểm ảnh, lọc Point, nạp sẵn bảng chữ tiếng Việt, atlas cố định) rồi mở scene `Assets/Scenes/Tools/PixelFontTest.unity`: chữ của game ở ×1, ×2, ×3 cạnh Inter 16 px. Xem ở Game view.

Test `PixelFontTests` kiểm tra hai file font có đủ 134 chữ cái tiếng Việt (bỏ qua, kèm lời nhắc, khi chưa chạy script).

## Việc tiếp theo

- **T18 · Style guide:** chọn Galmuri hay tự vẽ font; màu chữ, viền và bóng chữ theo bảng màu.
- **Khi làm UI bằng prefab** (việc tái cấu trúc số 15, GĐ2): chuyển HUD, log, nameplate sang Galmuri7 ×2 và hội thoại, menu sang Galmuri11 ×2, Inter làm fallback.

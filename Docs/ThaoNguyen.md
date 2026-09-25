# Thảo Nguyên Gió — T61

Thảo nguyên nằm ở góc tây bắc của bản đồ 200 × 128 ô, nối với phía tây Rừng Pha Lê qua đường hầm. Cửa Gió, Trại Du Mục và Bờ Tây Khe Vực có Đá Truyền Tống. Khe Vực kết thúc trong vách đá phía nam, không lấn xuống bìa rừng.

## Cơ chế đang có

- Gió đổi hướng theo chu kỳ 24 giây trên đồng hồ ngày/đêm chung, có lúc lặng và lúc mạnh. Ở sức gió tối đa, nhân vật bị đẩy 1,5 ô/giây và đạn lệch thêm 2,6 ô/giây. Ngoài thảo nguyên không chịu gió; minimap báo hướng, cỏ và vệt gió thể hiện bằng hình ảnh.
- Khe Vực chặn nhân vật đi bộ. Ba cặp Cột Gió đưa người chơi qua lại, đáp phía ngoài vòng đá để tránh bị đưa ngược lại ngay.
- Online: máy của mỗi người tự bay nhân vật của mình; máy chủ cho phép đi nhanh gần Cột Gió (kiểm tra di chuyển). Màn hình của người khác chỉ nhận vị trí, nên tự vẽ cung bay, luồng gió và lúc đáp theo quãng nhân vật đã bay qua (`WindColumn.WatchOthers`). Gió tính từ giờ trong ngày do máy chủ gửi, nên mọi máy thấy cùng một cơn gió mà không cần thêm tin nhắn.
- Linh Cẩu Gió gọi bầy, vây quanh rồi lao cắn theo vạch cảnh báo.
- Chim Ưng Đá bay cao, đánh dấu vị trí rồi bổ nhào. Rời vòng là né được; lúc đáp xuống nó chịu thêm 40% sát thương.
- Bò Rừng cào đất rồi húc thẳng. Dẫn nó húc vào đá để gây choáng và đánh lúc sơ hở.
- Già Tăng giải thích các cơ chế. Chuỗi chính tiếp tục từ Nhện Chúa Pha Lê: `steppe_enter` → `steppe_hyenas` → `steppe_eagles` → `steppe_bisons` (cấp đề nghị 20, 20, 21, 22).
- Nanh linh cẩu, lông chim ưng, da và sừng bò rừng là đồ rơi mới; lò rèn có mũ, giáp và giày của vùng.

## Dựng và kiểm tra

`RPG.EditorTools.Batch.GrowWorld` nhập các asset còn thiếu và dựng lại các scene vùng. `Batch.RebuildZones` chỉ dựng lại vùng từ prefab hiện có; `PrefabFactory.LoadExisting` phải nạp đủ Hyena, Eagle, Bison và GiaTang. Core và màn hình chính giữ nguyên khi dùng hai lệnh này.

Có thể chạy toàn bộ kiểm tra của vùng bằng `powershell -ExecutionPolicy Bypass -File Tools/Verify/steppe.ps1`: test hang, thảo nguyên và nhiệm vụ; build Windows; chụp đủ 18 PNG 1600×900; kiểm tra ảnh không trống. Log, XML và ảnh nằm trong thư mục có thời gian chạy ở `Logs/SteppeVerify`. Thêm `-SkipBuild` khi chỉ muốn kiểm tra lại bản build hiện có.

`SteppeTests` chạy trong EditMode và tự vào Play Mode để kiểm tra đường hầm, quái, đá truyền tống, sức gió, đạn, va chạm khe vực, chuyến bay (cả chuyến bay của người chơi khác nhìn từ màn hình này) và đòn đánh; ngắt chuyến bay phải trả lại độ cao nhân vật, va chạm và cho phép bay lần nữa. Vòng chờ phải theo `Time.time`; `WaitForSeconds` không chờ thời gian game trong kiểu test này. Test chim ưng theo dõi đúng sự kiện sát thương của đòn Bổ Nhào, vì nó đã có thể bị đánh lúc đang ở đoạn cuối cú đáp, trước khi gây sát thương.

`QuestRoadTests` kiểm tra chuỗi nhiệm vụ chính và cấp đề nghị không giảm. `Tools/Server/netsmoke.ps1` cho SmokeA bay qua Khe Vực trong khi SmokeB đứng bờ bên kia: B phải thấy A bay lên rồi đáp xuống. Build bằng `Batch.BuildPlayer`, rồi chạy:

```powershell
Builds/Windows/RungThiTham.exe -autoshot -autoshotOnly steppe -autoshotDir Logs/Steppe -autoshotTimeout 180
```

Tour chụp cửa hang, Cửa Gió, trại, cỏ trong gió, Cột Gió trước/trong/sau chuyến bay, ba loài quái, trại ban đêm và bản đồ thế giới. Mã thoát 0 là tour xong không ghi lỗi; ảnh vẫn cần xem để kiểm tra bố cục và thời điểm chụp. Chạy nền trên Windows: thêm `-batchmode -force-d3d11 -autoshotOffscreen` để vẽ camera và HUD vào texture, vì cửa sổ ẩn không có backbuffer dùng được để chụp. Tour kiểm tra file PNG đã được ghi trước khi sang ảnh tiếp theo.

## Phần tiếp theo của T62

Chưa có Bù Nhìn Sống, Cung Thủ Hắc Phong, Đao Thủ Hắc Phong, mini-boss Bò Rừng Sắt và boss Thủ Lĩnh Hắc Phong. Trại Hắc Phong và chuỗi nhiệm vụ sau bò rừng đang để dành cho phần đó. Không coi T62 đã hoàn tất chỉ vì ba loài đầu đã xuất hiện trong T61.

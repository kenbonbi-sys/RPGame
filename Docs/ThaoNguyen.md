# Thảo Nguyên Gió — T61, T62

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

## Hắc Phong (T62)

Phía tây Khe Vực là đất của bọn cướp Hắc Phong. Cả sáu loài quái của vùng, mini-boss và boss đã có trong game (cấp 22–26).

| Nơi | Chỗ (x, y) | Có gì |
|---|---|---|
| Ruộng Bỏ Hoang | (66, 104), tây Trại Du Mục | Luống đất, đống rơm, 7 bù nhìn rơm và 4 Bù Nhìn Sống |
| Bãi Sừng Sắt | (16, 75.5), tây nam, bên kia Khe Vực | Vòng 6 tảng sa thạch, Bò Rừng Sắt |
| Trại Hắc Phong | (23, 95), giữa đường phía tây | Lều đen, cờ, hàng rào cọc; cung thủ và đao thủ ở 4 chỗ |
| Đồi Cối Xay | (16, 113), tây bắc | Cối xay gió (cánh quay theo sức gió), Thủ Lĩnh Hắc Phong; gió đổi hướng mỗi 12 giây; Đá Truyền Tống Chân Đồi Cối Xay |

- **Bù Nhìn Sống** (cấp 22): đứng im giữa ruộng như bù nhìn rơm. Có hai cách nhận ra: bù nhìn rơm lay theo gió (vật liệu gió), còn con sống thì đứng yên, và đầu nó quay theo người đi qua trong 7 ô. Lại gần 2,4 ô hoặc đánh trúng là nó tỉnh. Đòn đánh lúc nó còn giả chết gây ×1,5 ("Bất ngờ!"). Tỉnh rồi thì nó dùng **Nhảy Vồ**: vòng đánh dấu chỗ đáp, thân bay lên bằng `lift` nên mọi màn hình đều thấy. Nó cũng dùng **Liềm Xoay** trúng cả vòng quanh mình. Bỏ đi thì nó về cọc và đứng im lại. Rơm cháy: chịu thêm 30% sát thương Lửa.
- **Cung Thủ Hắc Phong** (cấp 23): giữ khoảng cách 4,5–8,5 ô. Người áp sát thì nó **Nhảy Lùi**. **Mũi Tên Đón Gió**: vạch chấm cho biết tên sẽ bay đường nào; nó bắn chếch vào gió (`EnemyShots.IntoTheWind`) để gió đưa tên về đúng chỗ đã ngắm lúc giương cung. **Mưa Tên**: ba mũi bắn thẳng thành hình quạt, gió bẻ cong tùy ý.
- **Đao Thủ Hắc Phong** (cấp 24): **Lướt Chém** xuyên qua người trên vạch, rồi quay lại **Chém Liên Hoàn** hai nhát, mỗi nhát có cảnh báo hình quạt. Sau chuỗi đòn hắn đứng thở 1,1 giây và chịu ×1,35 ("Hở sườn!").
- **Bò Rừng Sắt** (mini-boss, cấp 23, 5200 máu): giáp sắt ở đầu và vai chặn 50% đòn đánh từ phía trước ("Giáp sắt!"), sườn và mông chịu ×1,25. Đòn **Lao Húc** có vạch cảnh báo và húc xuyên người. Nếu nó đâm vào sa thạch thì choáng 3,2 giây, giáp bung ra, mọi phía chịu ×1,6 ("Giáp bung!"). Ngoài ra có **Dậm Đất** (vòng, choáng 0,6 giây) và **Hất Sừng** (hình quạt, đánh bật). Khi cuồng nộ, nó dùng **Húc Liên Hoàn**: lao ba lần liền, mỗi lần nhắm lại. Rơi Sừng Sắt, Giáp Sắt Vụn, Da Bò Rừng và Huy Hiệu Hắc Phong. Thợ Rèn làm Khiên Sừng Sắt từ các món này.
- **Thủ Lĩnh Hắc Phong** (boss, cấp 26, 8200 máu): vẽ bằng `HeroArt` như người chơi (bán orc, mũ trùm đỏ, mã tấu Hắc Thiết +7), to gấp 1,5.
  - **Song Đao Chém Gió**: chém quạt trước mặt và phóng hai lưỡi gió hình chữ V. Lưỡi gió xuyên qua người và bị gió bẻ cong.
  - **Lốc Xoáy**: một cơn lốc bò về phía mục tiêu (khi cuồng nộ là hai). Cơn lốc trôi theo gió, hất văng và làm choáng mỗi người nó cán qua, vỡ khi gặp đá.
  - **Gọi Cung Thủ**: hai cung thủ trong `Brood` dưới chân đồi lên đánh cùng.
  - **Bão Cát**: khi hắn cuồng nộ, cát nổi lên trong vùng Đồi Cối Xay (bán kính 13). Màn hình ngả màu cát, sương cát bay theo gió (`ZoneArea.storm`, `AmbientParticles`).
  - **Lướt Gió Liên Hoàn**: lướt ba lần, mỗi lần có vạch cảnh báo. Nếu dùng Lướt để né nhát thứ ba (bất tử lúc lướt, `Health.Evaded`), hắn **mất thăng bằng** 2,2 giây và chịu ×1,5.
  - **Thách Đấu** (một lần mỗi trận, dưới 40% máu): cung thủ rút lui, hắn chỉ nhắm người bị thách và đánh liên tục không nghỉ trong 9 giây.
  - Rơi Mảnh Song Đao, Huy Hiệu Hắc Phong, và 30% mỗi món: Ủng Gió Hú, Khăn Hắc Phong. Ba phút sau hắn quay lại như mọi boss.
- **Nhiệm vụ chính** nối tiếp sau Bò Rừng: `steppe_scarecrows` (22) → `steppe_ironbison` (23) → `hacphong_archers` (24) → `hacphong_blades` (24) → `slay_blackwind` (26). Bước cuối chỉ về Đỉnh Tuyết Vĩnh Hằng (chưa mở). Già Tăng có lời chỉ dẫn cho từng bước.
- **Online**: mọi quái và boss do máy chủ điều khiển như trước. Những phần mới được đồng bộ như sau:
  - Bọn cướp được vẽ trên từng màn hình (`HeroLookEnemy`). Bộ hoạt ảnh riêng của prefab giữ đúng thứ tự clip, nên số clip máy chủ gửi vẫn khớp.
  - Mũi tên, lưỡi gió và lốc xoáy là khóa đạn mới `"arrow"`, `"windblade"`, `"tornado"`. Lưỡi gió và lốc xoáy xuyên người trên cả bản sao.
  - Bù nhìn nhảy bằng `lift`.
  - Bão cát bật lên trên mọi máy từ cờ cuồng nộ có sẵn của boss.
  - Né nhát thứ ba vẫn được tính sau khi máy chủ giữ đòn nửa ping (`LagCompensation`).
  - Giao thức lên 12.
- **Kiểm tra**:
  - `HacPhongTests`: bù nhìn đứng im, nhận ra được, tỉnh dậy, nhảy và xoay; tên đón gió vẫn trúng qua gió ngang, cung thủ nhảy lùi; đao thủ lướt, chém rồi hở sườn; Bò Rừng Sắt có giáp trước, húc đá thì bung giáp; gió riêng của đồi và bão cát; né nhát thứ ba làm mất thăng bằng; gọi rồi đuổi cung thủ khi thách đấu; lốc xoáy cán qua người, đi tiếp và trôi theo gió.
  - `QuestRoadTests`.
  - Tour `-autoshotOnly hacphong` gồm 33 ảnh.

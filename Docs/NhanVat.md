# Nhân vật: lớp, chủng tộc, chỉ số, vũ khí, trang bị

Hệ nhân vật theo D&D 5e (Player's Handbook 2014), chơi theo kiểu hành động của game. Có từ 25/09/2026 (góp ý người chơi). Code: `Data/ClassDef.cs`, `Data/RaceDef.cs`, `Data/WeaponKinds.cs`, `Progression/PlayerStats.cs`, `Player/HeroLook.cs`, `Player/HeroArt.cs`, `Items/Forge.cs`, `Items/Inventory.cs` (trang bị), `UI/HeroPanelUI.cs`, `UI/ForgeUI.cs`. Dữ liệu: `Editor/HeroDataFactory.cs`, kỹ năng: `Editor/ClassAbilityFactory.cs`.

## Tạo nhân vật

Nhân vật mới (và nhân vật cũ chưa có lớp) mở **màn Tạo Nhân Vật**, dựng theo trang chọn lớp của D&D Beyond:

1. **Lớp**: 12 thẻ (màu, huy hiệu, hình nhân vật, vai trò, chỉ số chính, mô tả, độ khó), lọc theo độ khó Dễ / Vừa / Khó. *Xem thêm* cho xúc xắc máu, giáp, chỉ số chính, kháng, vũ khí và 7 kỹ năng.
2. **Chủng tộc**: 9 thẻ với phần cộng chỉ số và đặc tính.
3. **Ngoại hình**: màu da (Long Duệ: màu vảy, chọn luôn tổ tiên rồng), kiểu tóc (7), màu tóc (12), râu, màu mắt, màu trang phục (12 hoặc màu của lớp), mũ/mũ trùm, vũ khí, kim loại vũ khí. Có nút *Ngẫu nhiên*.
4. **Hoàn tất**: chỉ số, kỹ năng, đặc tính.

Bên phải là nhân vật xem trước: đi, đứng, đánh, niệm phép, xoay 4 hướng. **Lớp và chủng tộc chọn một lần**; ngoại hình và vũ khí đổi lại được ở bảng Nhân Vật (B hoặc C → *Ngoại hình*). Nhân vật cũ được trả lại toàn bộ điểm đã cộng khi chọn lớp. Online máy chủ kiểm tra lựa chọn (`CharacterChoice`), lưu nó cùng nhân vật và gửi cho mọi người chơi khác để vẽ đúng.

## Sáu chỉ số

| Chỉ số | Tác dụng trong game |
|---|---|
| **Sức Mạnh** (STR) | Công của vũ khí nặng (kiếm, rìu, chùy, giáo), Trấn Áp |
| **Khéo Léo** (DEX) | Công của vũ khí khéo (dao, kiếm mảnh, mã tấu, quyền, côn) nếu cao hơn Sức Mạnh, của cung; chí mạng, tốc đánh, hồi Lướt |
| **Thể Chất** (CON) | +10 máu mỗi điểm trên 10, giáp |
| **Trí Tuệ** (INT) | Phép của Pháp Sư; giảm hồi chiêu mọi kỹ năng (trừ Q), tối đa 30% |
| **Thông Thái** (WIS) | Phép của Tu Sĩ, Tế Sư, Du Hiệp, Võ Tăng; hồi máu mạnh hơn, kháng hệ |
| **Sức Hút** (CHA) | Phép của Thi Sĩ, Thuật Sĩ, Khế Ước Sư, Hiệp Sĩ Thánh; nhặt thêm vàng |

- **Điểm gốc** = mảng chuẩn D&D 15, 14, 13, 12, 10, 8 đặt theo thứ tự của lớp, cộng phần của chủng tộc. Mỗi cấp thêm 3 điểm tự cộng.
- **Bổ trợ** kiểu D&D: 10–11 là +0, 16–17 là +3.
- **Máu** = 56 + 1.8 × trung bình xúc xắc máu × cấp + 10 × (Thể Chất − 10). Xúc xắc: d6 Pháp Sư, Thuật Sĩ; d8 Thi Sĩ, Tu Sĩ, Tế Sư, Võ Tăng, Đạo Tặc, Khế Ước Sư; d10 Chiến Binh, Hiệp Sĩ Thánh, Du Hiệp; d12 Cuồng Chiến Binh.
- **Công** = công vũ khí + 1.5 × (chỉ số − 13). Nhân vật cấp 1 có 16 ở chỉ số đánh thì mạnh đúng như nhân vật cũ.
- **Kháng (saving throw)**: lớp kháng Thông Thái thì Choáng, Trói, Nguyền ngắn hơn 20%; kháng Thể Chất thì Độc ngắn hơn 25%. Bảng Nhân Vật ghi mỗi kháng = bổ trợ + *Thành thạo* (D&D: +2 ở cấp 1, thêm 1 mỗi 4 cấp) nếu lớp thành thạo kháng đó (ô vàng).
- Nhân vật chưa có lớp dùng bộ điểm tái tạo nhân vật cũ (104 máu, sát thương như trước).

## Chủng tộc

| Chủng tộc | Cộng | Đặc tính |
|---|---|---|
| Con Người (Human) | +1 mọi chỉ số | Đa Tài: +10% kinh nghiệm |
| Tiên Tộc (High Elf) | +2 DEX, +1 INT | Nhìn Trong Tối (đèn rọi xa hơn), Dòng Máu Tiên (Choáng, Trói, Nguyền −30%); tai nhọn |
| Người Lùn (Hill Dwarf) | +2 CON, +1 WIS | Nhìn Trong Tối, kháng Độc 30% và Độc ngắn nửa, thêm máu mỗi cấp; đi chậm hơn một chút; thấp, chắc |
| Người Tí Hon (Lightfoot Halfling) | +2 DEX, +1 CHA | May Mắn (10% đòn trúng bị hụt), Gan Dạ; đi chậm hơn một chút; nhỏ |
| Long Duệ (Dragonborn) | +2 STR, +1 CHA | Màu vảy là tổ tiên rồng: kháng 50% Lửa, Lôi, Băng hoặc Độc; đầu rồng, đuôi |
| Thần Lùn (Rock Gnome) | +2 INT, +1 CON | Nhìn Trong Tối, kháng 10% mọi hệ phép; nhỏ, tai nhọn |
| Bán Tiên (Half-Elf) | +2 CHA, +1 DEX, +1 CON | Nhìn Trong Tối, Dòng Máu Tiên; tai nhọn |
| Bán Orc (Half-Orc) | +2 STR, +1 CON | Nhìn Trong Tối, Kiên Trì Bất Khuất (đòn chí tử để lại 1 máu, 90 giây một lần), chí mạng mạnh hơn; da xám xanh, răng nanh |
| Quỷ Duệ (Tiefling) | +2 CHA, +1 INT | Nhìn Trong Tối, kháng Lửa 50%, 20% đốt kẻ đánh mình; sừng, đuôi |

## Lớp và kỹ năng

Q là đòn thường của vũ khí; với vật dẫn phép (trượng, đũa, ngọc, sách, đàn) Q là phép nhỏ của lớp. Space là Lướt cho mọi lớp.

| Lớp | d | Chỉ số chính | Vũ khí | W · E · R · A · S · D |
|---|---|---|---|---|
| Cuồng Chiến Binh | 12 | STR | Rìu Lớn, Kiếm, Chùy, Giáo | Cuồng Nộ · Nện Đất · Ném Rìu · Tiếng Gầm Chiến · Bất Khuất · Bão Kiếm |
| Thi Sĩ | 8 | CHA | Kiếm Mảnh, Đàn Luýt (Q: Nốt Nhạc Xung Kích), Dao | Sóng Âm · Lời Chế Nhạo · Khúc Ca Hùng Tráng · Khúc Hát Chữa Lành · Bước Sương · Mê Hoặc |
| Tu Sĩ | 8 | WIS | Chùy, Trượng (Q: Ngọn Lửa Thánh) | Tia Dẫn Lối · Tinh Linh Hộ Vệ · Cột Sáng · Hồi Phục · Khiên Thánh · Hào Quang Hồi Sinh |
| Tế Sư Rừng Xanh | 8 | WIS | Trượng (Q: Roi Gai), Mã Tấu | Rễ Trói · Sóng Sấm · Lôi Phạt · Hồi Phục · Da Vỏ Cây · Bão Gai |
| Chiến Binh | 10 | STR · DEX | Kiếm, Rìu, Chùy, Giáo, Kiếm Mảnh, Cung | Xung Phong · Chém Xoáy · Hồi Sức · Thế Thủ · Bùng Nổ Hành Động · Bão Kiếm |
| Võ Tăng | 8 | DEX · WIS | Quyền Thủ, Côn Gỗ | Cước Phi · Chưởng Choáng · Liên Hoàn Cước · Tĩnh Tâm · Bước Gió · Chưởng Rung Động |
| Hiệp Sĩ Thánh | 10 | STR · CHA | Kiếm, Chùy, Rìu, Giáo | Trừng Phạt Thánh · Đặt Tay · Hào Quang Hộ Vệ · Khiên Thánh · Xung Phong Thánh · Phán Quyết Trời Cao |
| Du Hiệp | 10 | DEX · WIS | Cung, Mã Tấu, Dao | Mưa Tên · Tên Xuyên Tâm · Bẫy Gai · Dấu Thợ Săn · Nhảy Lùi · Tên Bão |
| Đạo Tặc | 8 | DEX | Dao, Kiếm Mảnh, Mã Tấu, Cung | Ném Dao · Nhát Chí Mạng · Bom Khói · Né Tránh · Bước Bóng · Vũ Điệu Lưỡi Dao |
| Thuật Sĩ | 6 | CHA | Ngọc, Đũa, Trượng (Q: Tia Lửa) | Cầu Lửa · Tia Hỗn Loạn · Lôi Phạt · Khiên Phép · Bước Sương · Mưa Thiên Thạch |
| Khế Ước Sư | 8 | CHA | Sách, Đũa, Ngọc (Q: Tia Hắc Ám) | Lời Nguyền Hex · Xúc Tu Hắc Ám · Hút Sinh Lực · Giáp Hắc Ám · Bước Sương · Bão Hư Không |
| Pháp Sư | 6 | INT | Trượng, Đũa, Sách (Q: Tên Ma Thuật) | Cầu Lửa · Mũi Băng · Lôi Phạt · Khiên Phép · Bước Sương · Hố Đen |

Đòn thường theo vũ khí: Kiếm và Mã Tấu chém vòng cung; Rìu bổ rộng, chậm, hất lùi; Chùy và Côn nện, đòn cuối làm choáng; Giáo và Kiếm Mảnh đâm xa, hẹp; Dao đâm rất nhanh, dễ chí mạng; Quyền đấm liền tay, đòn cuối đá bay; Cung bắn tên. Hệ **Ám** (Khế Ước Sư) là hệ sát thương mới.

## Sách Chiêu (K)

Ngoài 6 chiêu của lớp, nhân vật học thêm chiêu của ba hệ **Băng**, **Lôi**, **Ám** (T63) từ **Bí Kíp**. Mỗi Bí Kíp dạy một chiêu, chỉ các lớp có phép tương ứng trong D&D 5e mới học được; chuột phải Bí Kíp trong túi để học (sách biến mất), lớp không học được thì sách ở lại. Bí Kíp rơi trong rương boss, mỗi người có công một lần gieo: chiêu thường 22%, Tuyệt kỹ 12%.

| Hệ | Chiêu | Lớp học được | Boss giữ sách |
|---|---|---|---|
| Băng | Mũi Băng | Thuật Sĩ, Pháp Sư, Tế Sư | Cóc Tía |
| Băng | Băng Tiễn: 3 mũi tên băng, mỗi mũi 1 tầng Lạnh | Thuật Sĩ, Pháp Sư, Du Hiệp | Cóc Tía |
| Băng | Giáp Sương: 6 s nhận ít hơn 40%, kẻ đánh gần bị Lạnh | Khế Ước Sư, Pháp Sư, Thuật Sĩ, Cuồng Chiến Binh, Chiến Binh | Cóc Tía |
| Băng | Ngục Băng: sau 0.6 s cột băng 2.5 ô, sát thương lớn và Đóng Băng | Tế Sư, Thuật Sĩ, Pháp Sư | Xà Mẫu |
| Băng | Bão Tuyết: bão 4 ô trong 5 s, mỗi nhịp 1 tầng Lạnh | Tế Sư, Thuật Sĩ, Pháp Sư | Xà Mẫu |
| Băng | *Kỷ Băng Hà* (Tuyệt kỹ): 7 ô quanh thân, sát thương rất lớn và Đóng Băng | Tế Sư, Thuật Sĩ, Pháp Sư | Xà Mẫu |
| Lôi | Xích Lôi: tia sét nhảy qua 4 kẻ, mỗi lần yếu hơn 15%, +1 Tích Điện | Thuật Sĩ, Pháp Sư, Tế Sư, Du Hiệp | Golem Pha Lê Cổ |
| Lôi | Thiểm Bộ: dịch chuyển 6 ô, chỗ cũ nổ điện sau 0.5 s | Thuật Sĩ, Khế Ước Sư, Pháp Sư, Chiến Binh | Golem Pha Lê Cổ |
| Lôi | Lôi Ấn: 8 s đòn Q mạnh hơn 35% và thêm 1 Tích Điện | Chiến Binh, Du Hiệp, Hiệp Sĩ Thánh, Đạo Tặc, Cuồng Chiến Binh, Võ Tăng, Thi Sĩ | Golem Pha Lê Cổ |
| Lôi | Lôi Cầu: cầu điện bay chậm giật mọi kẻ trong 2 ô rồi nổ | Thuật Sĩ, Pháp Sư, Khế Ước Sư | Nhện Chúa Pha Lê |
| Lôi | Điện Trường: vòng điện 3 ô trong 5 s, giật và làm chậm; người dùng chạy nhanh hơn 15% | Tế Sư, Thuật Sĩ, Pháp Sư, Tu Sĩ, Cuồng Chiến Binh, Hiệp Sĩ Thánh | Nhện Chúa Pha Lê |
| Lôi | *Cửu Thiên Lôi* (Tuyệt kỹ): 9 tia sét trời, mỗi tia Choáng và Tích Điện | Tế Sư, Thuật Sĩ, Pháp Sư, Tu Sĩ | Nhện Chúa Pha Lê |
| Ám | Ám Tiễn: 3 dao bóng tối hình quạt, dễ chí mạng | Đạo Tặc, Khế Ước Sư, Võ Tăng | Bò Rừng Sắt |
| Ám | Lời Nguyền: mọi kẻ trong 3 ô bị Nguyền 8 s | Thi Sĩ, Tu Sĩ, Pháp Sư, Khế Ước Sư, Hiệp Sĩ Thánh | Bò Rừng Sắt |
| Ám | Bước Bóng | Võ Tăng, Khế Ước Sư, Thi Sĩ | Bò Rừng Sắt |
| Ám | Phân Thân: bóng của nhân vật đứng cạnh 8 s chém kẻ gần, hết giờ nổ khói đen | Đạo Tặc, Thi Sĩ, Khế Ước Sư, Pháp Sư, Thuật Sĩ | Thủ Lĩnh Hắc Phong |
| Ám | Hút Hồn: tia 2 s, hồi máu bằng 30% sát thương gây ra | Khế Ước Sư, Pháp Sư, Thuật Sĩ | Thủ Lĩnh Hắc Phong |
| Ám | *Nhật Thực* (Tuyệt kỹ): 6 s chí mạng +50%, sát thương +15%, hết giờ bóng tối nổ 5 ô | Đạo Tặc, Khế Ước Sư, Võ Tăng | Thủ Lĩnh Hắc Phong |

Phím **K** mở Sách Chiêu: trên là thanh W E R A S D với chiêu đang đặt (chiêu của lớp hay chiêu đã học, theo màu hệ), dưới là ba hệ, mỗi chiêu ghi đã học, đang ở ô nào, lớp khác hay chưa học và boss nào giữ sách. Bấm một chiêu đã học, các ô nhận được sẽ sáng lên; bấm ô để đặt. W E R A S nhận chiêu thường, **D chỉ nhận Tuyệt kỹ**. Chuột phải một ô để trả về chiêu của lớp. Chỉ đổi được khi không giao chiến (4 giây không bị đánh và không quái nào đang đuổi). Hồi chiêu đi theo chiêu: gỡ một Tuyệt kỹ vừa dùng rồi đặt lại không làm nó hồi sớm.

Chiêu đã học và thanh chiêu nằm trong ngoại hình nhân vật (`HeroLook.spells`, `HeroLook.bar`, `Player/Spellbook.cs`), nên được lưu cùng nhân vật và gửi tới mọi máy. Online máy chủ làm việc học (người chơi xin `UseItem`) và đặt chiêu (`SetSkill`), kiểm tra lớp, Tuyệt kỹ và giao chiến; đổi kiểu tóc hay vũ khí không làm mất chiêu.

## Bảng Nhân Vật (B, I hoặc C)

Một cửa sổ cho cả chỉ số, trang bị và túi đồ (góp ý 25/09: gộp túi đồ và nhân vật vào một nút, như túi đồ của Minecraft):

- **Trái, theo tờ nhân vật D&D Beyond**: sáu ô chỉ số (bổ trợ to ở giữa, điểm ở dưới, nút + ở góc khi còn điểm; rê chuột để xem chỉ số làm gì và điểm đến từ đâu), sáu kháng với *Thành thạo*, và các chỉ số chiến đấu.
- **Giữa, theo màn hình anh hùng của Kingshot**: nhân vật đứng giữa các ô trang bị (Mũ, Giáp, Giày bên trái; Vũ Khí, Tay Phụ, Nhẫn bên phải), bốn ô Máu, Giáp, Công, Năng Lượng, và đặc điểm chủng tộc, lớp.
- **Phải: túi đồ 48 ô**, lăn chuột hoặc kéo thanh bên phải để cuộn, lọc Tất cả / Trang bị / Tiêu hao / Nguyên liệu. Chuột phải vào đồ ăn, bình thuốc để dùng; bấm vào trang bị để mặc (món đang mặc ở ô đó quay về túi); bấm ô trang bị trên người để tháo. Tooltip của nguyên liệu ghi Lò Rèn cần nó cho việc gì.

## Trang bị

Năm ô: **Mũ, Giáp, Giày, Tay Phụ, Nhẫn**. Vũ khí không phải vật phẩm mà là của riêng nhân vật, rèn ở Lò Rèn. Món đang mặc rời khỏi túi; mặc, tháo và chế tạo đều do nơi giữ luật quyết (offline ngay trên máy, online máy chủ, `ActKind.Equip`), trang bị lưu cùng túi đồ (phần `inventory`).

| Món | Ô | Thêm | Thợ Rèn làm từ |
|---|---|---|---|
| Mũ Da | Mũ | +2 Giáp, +15 Máu | 60 vàng, 6 Gel Slime, 2 Mũ Nấm Đỏ |
| Giày Da | Giày | +1 Giáp, +6% hồi Lướt | 60 vàng, 4 Gel Slime, 3 Cỏ Thuốc |
| Khiên Gỗ | Tay Phụ | +3 Giáp | 50 vàng, 3 Gel Slime, 2 Cỏ Thuốc |
| Kiếm Sắt | Tay Phụ | +2 Công vật lý, +4% Tốc đánh | 80 vàng, 5 Gel Slime (có sẵn trong túi khi bắt đầu) |
| Áo Da Gấu | Giáp | +4 Giáp, +30 Máu | 150 vàng, 1 Da Gấu Ma, 1 Vuốt Gấu Ma |
| Nhẫn Trưởng Làng | Nhẫn | +1 mọi chỉ số, +5% vàng | phần thưởng nhiệm vụ Trưởng Làng |
| Giày Da Cóc | Giày | +2 Giáp, +20 Máu, +10% hồi Lướt | 180 vàng, 5 Da Cóc, 3 Răng Đỉa |
| Giáp Vảy Xà | Giáp | +8 Giáp, +40 Máu, +5% Kháng hệ | 380 vàng, 2 Vảy Xà Mẫu, 4 Da Rắn Nước, 2 Lõi Bùn |
| Vương Miện Cóc | Mũ | +3 Giáp, +2 Sức Hút, +12% vàng | 300 vàng, 1 Vương Miện Cóc Tía, 3 Cánh Chuồn Chuồn, 2 Tuyến Độc |
| Mũ Pha Lê | Mũ | +5 Giáp, +15 Năng lượng, 6% giảm hồi chiêu | 450 vàng, 10 Mảnh Pha Lê, 3 Cánh Dơi Pha Lê, 1 Lõi Golem |
| Khiên Vỏ Bọ | Tay Phụ | +7 Giáp, +40 Máu | 480 vàng, 4 Vỏ Bọ Giáp, 3 Nhân Slime Pha Lê |
| Áo Tơ Pha Lê | Giáp | +11 Giáp, +60 Máu, +10% Kháng hệ | 800 vàng, 2 Tơ Pha Lê, 5 Tơ Nhện Hang, 1 Lõi Pha Lê Cổ |
| Nhẫn Mắt Hang | Nhẫn | +5% Chí mạng, +15% sát thương chí mạng | 400 vàng, 3 Thủy Tinh Thể Mắt Hang, 1 Lam Ngọc |
| Nhẫn Răng Mimic | Nhẫn | +1 Sức Hút, +25% vàng | 350 vàng, 1 Răng Mimic, 1 Hồng Ngọc |
| Mũ Lông Ưng | Mũ | +6 Giáp, +5% Chí mạng, +40 Máu | 600 vàng, 6 Lông Ưng Đá, 3 Nanh Linh Cẩu |
| Áo Da Bò Rừng | Giáp | +13 Giáp, +90 Máu, +8% Kháng hệ | 950 vàng, 5 Da Bò Rừng, 2 Sừng Bò Rừng, 4 Nanh Linh Cẩu |
| Giày Gió | Giày | +3 Giáp, +30 Máu, +15% hồi Lướt | 720 vàng, 4 Lông Ưng Đá, 2 Da Bò Rừng |
| Khiên Sừng Sắt | Tay Phụ | +12 Giáp, +70 Máu, +5% Kháng hệ | 1100 vàng, 1 Sừng Sắt, 3 Giáp Sắt Vụn, 4 Huy Hiệu Hắc Phong |
| Ủng Gió Hú | Giày | +5 Giáp, +50 Máu, +20% hồi Lướt, +1 Khéo Léo | chỉ rơi từ Thủ Lĩnh Hắc Phong (30%) |
| Khăn Hắc Phong | Mũ | +7 Giáp, +7% Chí mạng, +20% sát thương chí mạng | chỉ rơi từ Thủ Lĩnh Hắc Phong (30%) |

Số liệu nằm trên từng `ItemDef` (`slot`, `bonuses`, `craftGold`, `craftItems`), đặt trong `Editor/AssetFactory.cs` (`DressGear`).

## Lò Rèn

Thợ Rèn ở làng Lá Xanh (cạnh lò lửa, phía đông nam). Bấm F cạnh ông để mở Lò Rèn. Cửa sổ theo kiểu màn chế tạo (góp ý 25/09: trước đây không hiểu phải bấm gì): bốn tab đánh số ở trên, mỗi tab một dòng giải thích; bên trái danh sách của tab kèm trạng thái (Rèn được, Khóa, Đang dùng, Làm được…); giữa là món đang chọn (nhân vật cầm vũ khí như sau khi rèn, hoặc món trang bị) với con số **hiện tại → sau khi rèn**; bên phải **Yêu cầu**: vàng và từng vật liệu có / cần, món còn thiếu ghi *Rơi từ* quái nào; dưới cùng một nút lớn nói rõ sẽ làm gì (RÈN LÊN +5, ĐỔI SANG VÀNG, CẦM GIÁO, CHẾ TẠO) hoặc vì sao chưa được.

- **Nâng Cấp** +1 tới +10, mỗi cấp +8% công vũ khí, từ +7 vũ khí phát sáng. Vật liệu theo đúng con đường qua các vùng:

| Lên | Vàng | Vật liệu |
|---|---|---|
| +1 | 40 | 3 Gel Slime |
| +2 | 70 | 3 Mũ Nấm Đỏ |
| +3 | 150 | 1 Vuốt Gấu Ma |
| +4 | 200 | 4 Da Cóc |
| +5 | 260 | 3 Răng Đỉa, 2 Lõi Bùn |
| +6 | 340 | 1 Vương Miện Cóc Tía |
| +7 | 430 | 2 Vảy Xà Mẫu |
| +8 | 540 | 6 Mảnh Pha Lê, 3 Cánh Dơi Pha Lê |
| +9 | 660 | 3 Tơ Nhện Hang, 3 Lõi Golem |
| +10 | 800 | 1 Nanh Xà Mẫu, 10 Mảnh Pha Lê |

- **Vũ Khí**: đổi trong số vũ khí của lớp, miễn phí; cấp rèn và kim loại giữ nguyên.
- **Chế Tạo**: trang bị ở bảng trên.
- **Kim Loại**: Thép, Đồng, Hắc Thiết miễn phí (chọn được cả lúc tạo nhân vật); Vàng cần +3 và 100 vàng; Ngọc Lục cần +6, 150 vàng, 2 Tuyến Độc; Pha Lê cần +8, 200 vàng, 5 Mảnh Pha Lê; Huyết Thạch cần +9, 300 vàng, 1 Hồng Ngọc.

Online máy chủ kiểm tra người chơi đứng cạnh Thợ Rèn và đủ vàng, vật liệu (`ActKind.Forge`, cả chế tạo).

## Vẽ nhân vật

Không có ảnh vẽ sẵn cho từng tổ hợp: `HeroArt` là bản C# của hình nhân vật ghép từ mảnh (`Tools/ArtGen/gen_player.py`), vẽ lúc chạy từ `HeroLook`. Các phần được vẽ:

- Da hoặc vảy, tai nhọn, sừng, răng nanh, đuôi, đầu rồng, vóc thấp (Người Lùn) hoặc nhỏ (Tí Hon, Thần Lùn).
- Bảy kiểu tóc, râu.
- Trang phục của lớp theo màu đã chọn:
  - áo thụng (Tế Sư, Thuật Sĩ, Khế Ước Sư, Pháp Sư),
  - áo choàng (Thi Sĩ, Hiệp Sĩ Thánh, Du Hiệp),
  - mũ trùm (Du Hiệp, Đạo Tặc, Khế Ước Sư), mũ nhọn (Pháp Sư),
  - giáp và vai giáp (Chiến Binh, Hiệp Sĩ Thánh), áo khoác phủ ngoài giáp có huy hiệu (Tu Sĩ, Hiệp Sĩ Thánh),
  - lông thú (Cuồng Chiến Binh), đai (Võ Tăng), vòng lá (Tế Sư).
- Mười lăm loại vũ khí trong tay, theo kim loại và cấp rèn.

Mỗi màn hình tự vẽ các nhân vật nó thấy, nên qua mạng mỗi ngoại hình chỉ tốn vài byte. Xem thử mọi lớp: menu `Tools/RPG/Hero Art Preview` hoặc `-executeMethod RPG.EditorTools.HeroArtPreview.Batch -previewOut <file.png>`; chụp màn tạo nhân vật: `RungThiTham.exe -creatorshot <thư mục>`; tour kỹ năng các lớp: `-autoshot -autoshotOnly classes`.

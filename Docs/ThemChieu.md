# Thêm chiêu mới chỉ bằng dữ liệu (Ability System v2)

Mỗi chiêu là một asset `AbilityDef` gồm chi phí, hồi chiêu, nhãn và một **dòng thời gian các khối hiệu ứng**. Không cần viết code cho chiêu thông thường. Mục tiêu M1: làm xong một chiêu mới trong dưới 30 phút.

## Các bước

1. **Tạo asset:** chuột phải trong `Assets/Data/Abilities` → *Create → RPG → Ability*. Đặt `id` (không dấu, duy nhất), `displayName`, `description`, `icon`.
2. **Chi phí và cách dùng:** `cooldown`, `energyCost`, `targeting` (Direction: theo hướng chuột · Point: tại vị trí chuột · Self: quanh bản thân), `maxRange`, `lockTime` và `animBase` (attack hoặc cast), `castSfx`.
3. **Nhãn:** chọn hệ (#Lửa, #Băng…) và dạng (#Cận, #Đạn, #Vùng, #Kênh, #Dịch chuyển…). Thiên phú và trang bị sau này đọc nhãn; chiêu có nhãn *Movement* được Nhanh Nhẹn giảm hồi chiêu như Lướt.
4. **Dòng thời gian:** trong `effects`, bấm **+**, rồi chọn loại khối ở ô thả xuống bên phải mỗi phần tử. Mỗi khối có `delay` (giây sau khi ra chiêu, hoặc sau khối đã gọi nó).
5. **Gán vào ô chiêu:** mở prefab `Assets/Prefabs/Characters/Player`, kéo asset vào `PlayerSkills.slots` (Q W E R A S D Space). Thêm asset vào `GameDatabase.abilities` để các hệ thống khác tìm được theo `id`.
6. **Thử:** bấm Play; hoặc chạy test *ANewAbilityMadeOnlyFromData* làm mẫu.

## Các khối hiệu ứng

| Khối | Dùng cho | Thông số chính |
|---|---|---|
| **Damage** | Đòn cận chiến, vụ nổ, vùng | Hình tròn hoặc nón, bán kính, góc; `hit` (lực chiêu, hệ, chí mạng, đẩy lùi, Trấn Áp, choáng, làm chậm, bỏng); `onAnyHit` chạy khi trúng ít nhất một mục tiêu |
| **Projectile** | Đạn, cầu phép | Prefab có component `Projectile`, tốc độ, bán kính nổ, xuyên, `hit`, VFX/SFX khi trúng |
| **Dash** | Lướt, lao tới | Quãng đường, thời gian, thời gian bất tử, bóng mờ |
| **Heal** | Hồi máu | Hồi ngay (số hoặc % máu tối đa), hồi theo giây trong một khoảng thời gian, VFX quanh người |
| **Buff** | Khiên, tăng tốc | Thời gian, hệ số tốc chạy, hệ số sát thương nhận vào, miễn choáng, VFX bám theo và VFX khi hết |
| **Cue** | Chỉ để trình bày | VFX, âm thanh, rung màn hình, nháy màn hình, xung Impact |
| **Line** | Hàng gai băng | Chạy các khối con tại từng điểm dọc hướng chiêu: số điểm, khoảng cách, nhịp, dừng khi gặp tường, to dần |
| **Burst** | Bão sét | Chạy các khối con nhiều lần trong một vùng, ưu tiên điểm có kẻ địch, có thời gian tụ lực |
| **Pulse** | Chiêu kênh (Bão Kiếm) | Chạy các khối con mỗi nhịp trong một khoảng thời gian quanh người dùng |
| **Combo** | Chuỗi đòn (Chém Gió) | Mỗi lần dùng lại trong `window` giây chuyển sang đoạn tiếp theo; các đoạn lặp lại |

## Con số

- **Lực chiêu (`power`)** là tỉ lệ của Công: `1.4` = 140%. Sát thương = lực chiêu × Công (vật lý dùng Công vật lý, mọi hệ khác dùng Công phép) × chí mạng × giảm do giáp và kháng.
- Anh hùng cấp 1 có Công 24.5, nên `power 1` ≈ 24 sát thương. 8 chiêu cũ được chuyển sang với lực chiêu cho đúng số sát thương của bản prototype (Chém Gió 90%, Cầu Lửa 188%, Mũi Băng 122%, Lôi Phạt 212%, Bão Kiếm 45% mỗi nhịp).
- **Cấp chiêu 1–5:** mỗi cấp +`powerPerLevel` lực (mặc định 12%) và −`cooldownPerLevel` hồi chiêu (4%). Cấp của từng ô nằm ở `PlayerSkills.levels`.
- **Vị trí (`Anchor`)**: tính từ người dùng (Caster), điểm hiện tại (Point: nơi viên đạn nổ, gai băng mọc…) hoặc điểm nhắm (Aim), cộng thêm `up` (lên trên) và `forward` (theo hướng chiêu).

## Khi nào cần code

Chỉ khi chiêu có cơ chế mới hẳn (ví dụ hút kẻ địch, đánh dấu rồi kích nổ, triệu hồi có AI). Khi đó thêm một lớp con của `AbilityEffect` trong `Assets/Scripts/Abilities/Effects`; nó tự xuất hiện trong ô chọn khối của Inspector.

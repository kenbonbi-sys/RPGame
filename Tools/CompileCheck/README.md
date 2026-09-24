# Kiểm tra biên dịch không cần Unity

`check.sh` biên dịch toàn bộ C# của game giống cách Unity làm, nhưng chỉ cần .NET SDK: dùng được trên Linux, trong CI không có giấy phép Unity và trong các phiên Claude Code trên cloud.

```bash
Tools/CompileCheck/check.sh             # ✓ / ✗ cho 3 assembly, liệt kê lỗi
Tools/CompileCheck/check.sh --warnings  # thêm cảnh báo trong Assets/
```

Ba assembly được kiểm tra:

| Assembly | Gồm | Define |
|---|---|---|
| `Assembly-CSharp` | `Assets/**/*.cs` ngoài thư mục `Editor` | như trong Editor (`UNITY_EDITOR`) |
| `Assembly-CSharp-Player` | cùng các file đó | như bản build (không `UNITY_EDITOR`, không `UnityEditor.dll`) |
| `Assembly-CSharp-Editor` | `Assets/**/Editor/**/*.cs`: công cụ Editor và test | `UNITY_EDITOR`, `UNITY_INCLUDE_TESTS` |

**Chỉ chứng minh code biên dịch được.** Nó không chạy game, không chạy test và không kiểm tra scene, prefab hay asset. Trước khi gộp vẫn cần chạy test trong Unity (Window → General → Test Runner) hoặc qua CI có giấy phép Unity.

## Cách hoạt động

- **Unity:** assembly tham chiếu Unity 2021.3 (module UnityEngine), UnityEditor 2021.1 và UnityEngine.UI 2020.3 lấy từ NuGet. `setup.sh` tải về `.cache/` ở lần chạy đầu.
- **API mới của Unity 6** (ví dụ `Rigidbody2D.linearVelocity`) không có trong bản tham chiếu cũ. `RefPatch` thêm chúng vào theo danh sách trong `patch.spec` (chỉ là chữ ký hàm, thân hàm ném lỗi).
- **Package:** Yarn Spinner được biên dịch từ mã nguồn thật, đúng tag ghi trong `Packages/manifest.json`. TextMeshPro, URP, Input System, FishNet, Test Framework và 2D Sprite chỉ có phần chữ ký tự viết trong `Stubs/` và `EditorStubs/`. Chữ ký FishNet chép từ đúng phiên bản ghi trong `Packages/manifest.json`; đổi phiên bản thì so lại.

## Khi báo lỗi mà trong Unity vẫn chạy

Lỗi đó nằm ở phần tự dựng, không phải ở game:

- **API Unity 6 bị thiếu** (`'Rigidbody2D' does not contain a definition for …`): thêm một dòng vào `patch.spec`, chạy lại `check.sh` (nó tự chạy lại `setup.sh` khi `patch.spec` đổi).
- **API package bị thiếu** (`'TMP_Text' does not contain …`, `InputAction…`): thêm chữ ký vào file tương ứng trong `Stubs/` hoặc `EditorStubs/`, đúng như trong package thật.

Thêm đúng chữ ký của API thật; đừng thêm cho vừa code, nếu không công cụ sẽ bỏ lọt lỗi thật.

## Yêu cầu

.NET SDK 8 trở lên (Ubuntu: `apt install dotnet-sdk-8.0`), `git`, `curl`, `unzip` hoặc `python3`. Trên Windows chạy bằng Git Bash hoặc WSL.

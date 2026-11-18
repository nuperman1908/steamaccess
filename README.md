# Steam Access

🇻🇳 Tiếng Việt | [🇬🇧 English](#english)

Ứng dụng Windows nhẹ, chạy ngầm ở khay taskbar, giúp khôi phục truy cập Steam khi bị nhà mạng chặn.

Mọi thứ chạy cục bộ trên máy bạn bằng proxy nội bộ và kỹ thuật chia nhỏ gói TLS. Không cần VPN và không phải thay đổi cấu hình DNS.

## Cách hoạt động

1. **Cửa hàng và Cộng đồng Steam**

   App chạy một proxy cục bộ tại `127.0.0.1:8881`, chia gói TLS đầu tiên thành các phần nhỏ. Cách này giúp hệ thống lọc của nhà mạng không nhận diện được tên miền đang truy cập.

   Windows định tuyến lưu lượng qua proxy bằng file PAC, chỉ áp dụng cho các tên miền Steam.

2. **Tải game từ Steam**

   Trình tải của Steam không tuân theo cài đặt proxy của hệ thống, nên app xử lý lưu lượng tải theo cách khác.

   App thêm các tên miền máy chủ tải của Steam vào file hosts của Windows, trỏ về `127.0.0.1`. Sau đó app lắng nghe ở cổng 80 và 443, tự tra địa chỉ IP thật của máy chủ bằng truy vấn DNS riêng, rồi chuyển tiếp kết nối tới máy chủ thật.

   Mã hóa TLS vẫn được giữ nguyên đầu cuối giữa Steam và máy chủ tải thật.

   App cũng theo dõi nhật ký nội dung (content log) của Steam để tự động phát hiện các tên miền máy chủ tải mới.

Cài đặt DNS của hệ thống không bị thay đổi. Chỉ các tên miền liên quan đến Steam bị tác động, và mọi thay đổi sẽ tự động được hoàn tác khi bạn tắt hoặc thoát app.

## Tính năng

- Bật hoặc tắt truy cập Steam chỉ với một cú click từ khay hệ thống
- Khởi động lại Steam ngay từ menu ở khay hệ thống
- Tùy chọn chạy cùng Windows qua Task Scheduler, không hiện UAC khi đăng nhập
- Nhiều ngôn ngữ: English, 中文 và Tiếng Việt
- Tự khôi phục cấu hình proxy và xóa các dòng trong file hosts khi tắt hoặc đóng app
- Có nhật ký hoạt động tích hợp để xử lý sự cố kết nối

## Yêu cầu

- Windows 10 hoặc 11
- .NET Framework 4.x (đã có sẵn trong Windows)
- Quyền Administrator (cần để sửa file hosts và lắng nghe cổng 80 và 443)

## Cảnh báo giả từ antivirus

Một số engine antivirus có thể đánh dấu app bằng các tên phát hiện chung chung như `Trojan.Gen` hoặc `Spy.Gen`.

Đây nhiều khả năng là cảnh báo giả do heuristic. App sửa file hosts của Windows, thay đổi cài đặt proxy hệ thống, yêu cầu quyền quản trị và chưa được ký số. Những hành vi này có thể giống phần mềm độc hại, dù mục đích sử dụng hoàn toàn hợp pháp.

Toàn bộ mã nguồn có trong repo này, bạn có thể tự đọc và tự build file thực thi nếu muốn.

Trước khi thêm ngoại lệ cho antivirus, hãy đối chiếu mã SHA256 của bản bạn tự build với bản release chính thức. Nếu cần thêm ngoại lệ, chỉ giới hạn cho file thực thi hoặc thư mục riêng của app, đừng tắt hoàn toàn bảo vệ của antivirus.

## Miễn trừ trách nhiệm

Công cụ này dành cho mục đích cá nhân, nhằm khôi phục truy cập các dịch vụ mà bạn có quyền sử dụng hợp pháp.

Bạn tự chịu trách nhiệm đảm bảo việc sử dụng phần mềm tuân thủ pháp luật hiện hành và điều khoản dịch vụ liên quan.

Nếu bạn sử dụng thấy hiệu quả, hãy cho mình xin 1 sao vào repo nhé

---

## English

A lightweight Windows app that runs quietly in the system tray and helps restore access to Steam when it's blocked by your ISP.

Everything runs locally on your PC using a local proxy and TLS packet splitting. No VPN or DNS configuration changes required.

### How It Works

1. **Steam Store and Community**

   The app runs a local proxy at `127.0.0.1:8881` that splits the first TLS packet into smaller chunks. This helps prevent ISP filtering systems from identifying the requested domain name.

   Windows routes traffic through the proxy using a PAC file that applies only to Steam domains.

2. **Steam Game Downloads**

   Steam's download client doesn't respect system proxy settings, so the app handles download traffic differently.

   It adds Steam's download server domains to the Windows hosts file, pointing them to `127.0.0.1`. The app then listens on ports 80 and 443, resolves the actual server IP through its own DNS lookup, and forwards the connection to the real server.

   TLS encryption remains end-to-end between Steam and the actual download server.

   The app also monitors Steam's content log to automatically detect new download server domains.

Your system DNS settings are never modified. Only Steam-related domains are affected, and all changes are automatically reverted when you disable or exit the app.

### Features

- Enable or disable Steam access with a single click from the system tray
- Restart Steam directly from the tray menu
- Optional startup with Windows through Task Scheduler, without triggering a UAC prompt at logon
- Multiple language options: English, 中文, and Tiếng Việt
- Automatically restores proxy settings and removes hosts file entries when disabled or closed
- Built-in activity log to help troubleshoot connection issues

### Requirements

- Windows 10 or 11
- .NET Framework 4.x (already included with Windows)
- Administrator privileges (required to modify the hosts file and listen on ports 80 and 443)

### Antivirus False Positives

Some antivirus engines may flag the app with generic detection names such as `Trojan.Gen` or `Spy.Gen`.

These detections are likely heuristic false positives. The app modifies the Windows hosts file, changes system proxy settings, requires administrator privileges, and isn't digitally signed. These behaviors can resemble those of malicious software, even when used for legitimate purposes.

The full source code is available in this repository, so you can review it and build the executable yourself if you prefer.

Before adding an antivirus exclusion, compare the SHA256 hash of your own build with the official release. If an exclusion is necessary, limit it to this app's executable or its specific folder rather than disabling your antivirus protection entirely.

### Disclaimer

This tool is intended for personal use to restore access to services you're legitimately entitled to use.

You're responsible for ensuring that your use of this software complies with all applicable laws and the relevant terms of service.

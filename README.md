# LiteWall

Aplikasi wallpaper animasi untuk Windows yang ringan. Alternatif sederhana untuk Wallpaper Engine dengan pemakaian CPU dan RAM kecil.

## Fitur

- Wallpaper video, GIF, dan gambar, diputar dengan libmpv (decode hardware, tanpa audio).
- Mencari wallpaper dari Wallhaven (gambar), Pexels (video), dan Pixabay (video).
- Filter resolusi otomatis sesuai layar, atau pilih Full HD, 2K, 4K.
- Batas FPS bebas dari 24 sampai 120 (slider di Pengaturan).
- Jeda otomatis saat aplikasi layar penuh, jendela maksimal (opsional), layar terkunci, dan mode baterai.
- Berjalan di tray. Jendela galeri melepas semua thumbnail dari memori saat ditutup.
- Bisa memakai file video atau gambar sendiri.

## Cara pakai

1. Unduh `LiteWall-win-x64.zip` dari halaman Releases, ekstrak, jalankan `LiteWall.exe`.
2. Buka Pengaturan dari tray, tempel API key (lihat di bawah), atur FPS, lalu Simpan.
3. Cari wallpaper, klik thumbnail untuk memasangnya.

Catatan FPS: batas ini hanya menurunkan FPS, tidak menaikkan. Video 30 FPS tetap 30 FPS walau batas diset 120. Pilih 120 hanya berguna untuk video sumber 120 FPS.

## API key gratis

- Wallhaven: opsional. Daftar di wallhaven.cc, key ada di Settings > Account.
- Pexels: wajib untuk video Pexels. Daftar di pexels.com/api.
- Pixabay: wajib untuk video Pixabay. Daftar di pixabay.com, key tampil di pixabay.com/api/docs.

Key disimpan sebagai teks biasa di `%AppData%\LiteWall\settings.json` di komputermu sendiri.

## Build dan rilis lewat GitHub

1. Buat repo baru di GitHub, unggah seluruh isi folder ini.
2. Buka tab Actions, jalankan workflow `build` secara manual untuk uji coba. Hasilnya muncul sebagai artifact.
3. Untuk rilis resmi, buat tag: `git tag v0.1.0` lalu `git push origin v0.1.0`. Workflow membangun dan mengunggah zip ke Releases otomatis.

Workflow mengunduh `libmpv-2.dll` dari build mpv komunitas (shinchiro/mpv-winbuild-cmake) dan menaruhnya di samping `LiteWall.exe`.

Build lokal (butuh .NET 8 SDK dan `libmpv-2.dll` di folder `native/`):

```
dotnet publish LiteWall.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## Batasan versi awal

- Hanya monitor utama.
- Wallpaper interaktif, web, dan 3D belum ada (sengaja, supaya tetap ringan).
- Jika Explorer di-restart, jalankan ulang LiteWall.

## Lisensi dan atribusi

- Konten Pexels dan Pixabay tunduk pada lisensi dan aturan API masing-masing. Cantumkan kredit bila membagikan ulang.
- libmpv memiliki lisensi sendiri (LGPL/GPL tergantung build). Periksa sebelum mendistribusikan aplikasi ini ke orang lain.

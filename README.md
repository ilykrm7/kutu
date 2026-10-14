# Kutu

Windows için küçük bir İsviçre çakısı. Görsel dönüştürür, ekran görüntüsü alır, renk seçer, panoyu hatırlar, yazdığın tetikleyiciyi metne çevirir, alan adı ve IP bakar, çevirir, bağlantıya yükler. Tek bir koyu, yuvarlak pencerede durur; sistem tepsisinde yaşar.

Arayüz Türkçe. Metinler `src/Kutu.Sdk/Resources/Strings.resx` içinde. İngilizce karşılıkları `Strings.en.resx` dosyasında; Ayarlar'dan English seçilince bir sonraki açılışta kullanılır.

## Özellikler

- **Görsel dönüştür** — PNG, JPG, WebP, BMP, ICO, PDF ve HEIC. HEIC, Windows HEIF koduyla okunur (Microsoft Store: HEIF Image Extensions). ImageMagick yok.
- **Toplu boyut ve sıkıştırma** — uzun kenar, genişlik, yükseklik veya yüzde; JPG / WebP / PNG.
- **Ekran görüntüsü** — donmuş ekranda bölge seçimi ve yerinde işaretleme. Kopyala veya kaydet. Yükle kapalıdır (Yakında). Esc vazgeçer.
- **Renk seçici** — HEX, RGB, HSL, kopyala, geçmiş.
- **Pano geçmişi** — son 50 kayıt, arama, sabitleme. Karttaki anahtar kapatır.
- **Metin genişletici** — tetikleyici tam yazılınca hemen genişler (`:adm`, `mail:imza`). Ayrı bir kanca iş parçacığı. Karttaki anahtar kapatır.
- **WHOIS + DNS** — RDAP (`rdap.org`), anahtarsız. A, AAAA, MX, NS, TXT, CNAME.
- **IP itibarı** — AbuseIPDB, VirusTotal, IPQualityScore, OTX, GreyNoise, Shodan, IPinfo (anahtar Ayarlar'da, DPAPI). Anahtarsız: ipwho.is, Spamhaus ZEN, SpamCop, Barracuda ve TOR çıkış listesi.
- **Bağlantıya yükle** — kart soluk, düğme tıklanmaz, ipucu Yakında.
- **Çeviri** — hedef İngilizce. DeepL, sonra Google, sonra MyMemory. Ctrl+Alt+T seçili metni yerinde çevirir.

**Alt+Space** Spotlight benzeri başlatıcı açar: `ss`, `renk`, `whois`, `çevir`.

Kısayollar, Windows ile başlatma ve sağ tık menüsü Ayarlar'dan açılıp kapanır. API anahtarları koda yazılmaz; `%AppData%\Kutu\settings.json` içinde DPAPI (CurrentUser) ile şifrelenir.

## Exe nerede

Her push'ta Actions çıktısı `Kutu-win-x64` tek dosyadır: `Kutu.exe`. .NET Framework 4.8 üzerine kuruludur. Windows 10, Windows 11 ve Windows Server 2019 bu çerçeveyi hazır getirir; ayrı bir çalışma zamanı kurulmaz. Bağımlılıklar ve `kutuwebp.dll` exe'nin içindedir.

Sürüm etiketi (`v*`) itilince aynı dosya GitHub Release'a eklenir. Kurulum yok. İlk açılışta Windows SmartScreen uyarabilir; imzasız bir topluluk aracı olduğu için bu beklenen bir durum.

## Sağ tık menüsü

Ayarlar'daki anahtar iki kayıt yazar, ikisi de kullanıcıya aittir, yönetici istemez:

- Klasik menü: **Kutu ile dönüştür** alt menüsü (JPEG, PNG, WebP, ICO, PDF, BMP). Windows 11'de bu öğe çoğunlukla **Daha fazla seçenek** altındadır.
- Modern menü: aynı alt menü, imzasız seyrek paket (`AllowUnsigned` + `ExternalLocation`) ve `KutuShell.dll`. Paket, çalışan `Kutu.exe` klasörüne yazılır. Klasör yazılamıyorsa yalnız klasik menü kalır. Windows 11 gerekir.

## Geliştirme

```bash
dotnet test tests/Kutu.Core.Tests/Kutu.Core.Tests.csproj -c Release -f net8.0
dotnet test tests/Kutu.Core.Tests/Kutu.Core.Tests.csproj -c Release -f net48
dotnet publish src/Kutu.App/Kutu.App.csproj -c Release -o artifacts
```

Uygulama `net48` hedefler. Çekirdek testler `net48` ve `net8.0` olarak derlenir; Linux'ta `net8.0`, Windows'ta `net48` çalışır.

Çözüm `Kutu.sln`. Her araç `src/Tools/<Ad>` altında kendi projesinde. Yeni araç için:

1. `src/Tools/YeniArac` projesini aç, `IKutuTool` uygula (`Id`, `TitleKey`, `DescriptionKey`, `Symbol`, `Aliases`, `CreateView`).
2. Metin anahtarlarını `Strings.resx` ve `Strings.en.resx` dosyalarına ekle.
3. `src/Kutu.App/Kutu.App.csproj` içine proje başvurusu ve `App.xaml.cs` içindeki diziye bir satır ekle.

v2 için bırakılan yerler: yazı tipi bulucu, OCR, arka plan silme, SSL kontrolü, ekran kaydı, parola / UUID / özet.

## Windows'ta denenmemiş olanlar

Bu depo Linux üzerinde derlenir; pencere, tepsi, genel kısayol, klavye kancası, sağ tık menüsü, DPAPI, WIC/HEIF ve ekran yakalama gerçek Windows'ta doğrulanmalıdır. HEIC, makinede HEIF kodu yoksa çökmez; Microsoft Store'daki HEIF Image Extensions istenir. Çoklu monitörde farklı DPI ölçeklerinde bölge seçimi kayabilir.

Metin genişletici, Kutu'dan daha yetkili (yönetici) pencerelerde tuş görmez.

## English

Kutu is a Windows tray utility: image convert and resize (WIC, including PDF; HEIC via the Windows HEIF codec), in-place annotated screenshots, a color picker, clipboard history, an Espanso-style expander, RDAP/DNS lookup, multi-provider IP reputation, and translation to English (DeepL, then Google, then MyMemory). Upload stays disabled. UI strings live in RESX (Turkish by default, English included). Secrets are DPAPI-encrypted in the user profile. GitHub Actions publishes one `Kutu.exe` for .NET Framework 4.8, which Windows 10, Windows 11, and Server 2019 already include. Dependencies and libwebp are embedded. The Windows 11 top-level context menu is a per-user unsigned sparse package; the classic submenu remains under Show more options. No administrator rights.

## Lisans

[MIT](LICENSE)

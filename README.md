# Kutu

Windows için küçük bir İsviçre çakısı. Görsel dönüştürür, ekran görüntüsü alır, renk seçer, panoyu hatırlar, yazdığın tetikleyiciyi metne çevirir, alan adı ve IP bakar, çevirir, bağlantıya yükler. Tek bir koyu, yuvarlak pencerede durur; sistem tepsisinde yaşar.

Arayüz Türkçe. Metinler `src/Kutu.Sdk/Resources/Strings.resx` içinde. İngilizce karşılıkları `Strings.en.resx` dosyasında; Ayarlar'dan English seçilince bir sonraki açılışta kullanılır.

## Özellikler

- **Görsel dönüştür** — PNG, JPG, WebP, BMP, ICO ve HEIC okuma. Sürükle-bırak. Gezgin sağ tık: **Kutu ile dönüştür** (kullanıcı kaydı, yönetici yok).
- **Toplu boyut ve sıkıştırma** — uzun kenar, genişlik, yükseklik veya yüzde; JPG / WebP / PNG.
- **Ekran görüntüsü** — genel kısayol, bölge seçimi, ok, dikdörtgen, kalem, yazı, bulanıklaştırma, vurgu. Kopyala, kaydet veya bağlantıya yükle.
- **Renk seçici** — HEX, RGB, HSL, kopyala, geçmiş.
- **Pano geçmişi** — son 50 kayıt, arama, sabitleme.
- **Metin genişletici** — `:imza` gibi tetikleyiciler. Düşük seviye klavye kancası, eski pano içeriğini geri koyan Ctrl+V.
- **WHOIS + DNS** — RDAP (`rdap.org`), anahtarsız. A, AAAA, MX, NS, TXT, CNAME.
- **IP itibarı** — AbuseIPDB skoru (anahtar Ayarlar'da) ve ipwho.is ile ülke / ASN.
- **Bağlantıya yükle** — metin veya dosya. Uç ve anahtar Ayarlar'da. Örnek PHP: [`server/`](server/README.md).
- **Çeviri** — DeepL; anahtar yoksa MyMemory yedegi. Panoyu çeviren küçük pencere.

**Alt+Space** Spotlight benzeri başlatıcı açar: `ss`, `renk`, `whois`, `çevir`.

Kısayollar, Windows ile başlatma ve sağ tık menüsü Ayarlar'dan açılıp kapanır. API anahtarları koda yazılmaz; `%AppData%\Kutu\settings.json` içinde DPAPI (CurrentUser) ile şifrelenir.

## Exe nerede

Sürüm etiketi (`v1.0.0` gibi) itilince GitHub Release'a `Kutu.exe` eklenir. Her push'ta Actions çıktısı olarak da durur:

1. Depoda **Actions** → **build** → yeşil **win-x64 single file** işi.
2. **Artifacts** → `Kutu-win-x64` → `Kutu.exe`.

Tek dosya, kendi .NET 8 çalışma zamanını taşır (`win-x64`). Kurulum yok. İlk açılışta Windows SmartScreen uyarabilir; imzasız bir topluluk aracı olduğu için bu beklenen bir durum.

## Geliştirme

```bash
dotnet test tests/Kutu.Core.Tests/Kutu.Core.Tests.csproj -c Release
dotnet publish src/Kutu.App/Kutu.App.csproj -c Release -r win-x64 -o artifacts
```

Çözüm `Kutu.sln`. Her araç `src/Tools/<Ad>` altında kendi projesinde. Yeni araç için:

1. `src/Tools/YeniArac` projesini aç, `IKutuTool` uygula (`Id`, `TitleKey`, `DescriptionKey`, `Symbol`, `Aliases`, `CreateView`).
2. Metin anahtarlarını `Strings.resx` ve `Strings.en.resx` dosyalarına ekle.
3. `src/Kutu.App/Kutu.App.csproj` içine proje başvurusu ve `App.xaml.cs` içindeki diziye bir satır ekle.

v2 için bırakılan yerler: yazı tipi bulucu, OCR, arka plan silme, SSL kontrolü, ekran kaydı, parola / UUID / özet.

## Windows'ta denenmemiş olanlar

Bu depo Linux üzerinde derlenir; pencere, tepsi, genel kısayol, klavye kancası, sağ tık menüsü, DPAPI ve ekran yakalama gerçek Windows'ta doğrulanmalıdır. HEIC, paketteki ImageMagick ile okunur. Çoklu monitörde farklı DPI ölçeklerinde bölge seçimi kayabilir.

Metin genişletici, Kutu'dan daha yetkili (yönetici) pencerelerde tuş görmez. Windows 11 sağ tık menüsünde klasik öğe **Daha fazla seçenek** altında görünebilir.

## English

Kutu is a Windows tray utility: image convert and resize, annotated screenshots, a color picker, clipboard history, an Espanso-style expander, RDAP/DNS lookup, AbuseIPDB plus geo/ASN, upload-to-link, and DeepL translation with a keyless fallback. UI strings live in RESX (Turkish by default, English included). Secrets are DPAPI-encrypted in the user profile. Publish a single self-contained `win-x64` exe; GitHub Actions uploads it on every push and attaches it to Releases for tags named `v*`.

## Lisans

[MIT](LICENSE)

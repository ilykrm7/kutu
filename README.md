# Kutu

Windows için küçük bir İsviçre çakısı. Görsel dönüştürür, ekran görüntüsü alır, renk seçer, panoyu hatırlar, yazdığın tetikleyiciyi metne çevirir, alan adı ve IP bakar, çevirir, bağlantıya yükler. Tek bir koyu, yuvarlak pencerede durur; sistem tepsisinde yaşar.

**İndir:** [Releases](https://github.com/ilykrm7/kutu/releases/latest) sayfasından tek dosya `Kutu.exe` (~9.4 MB). .NET Framework 4.8 üzerinde çalışır; bu çerçeve Windows 10 ve 11 ile hazır gelir. Kurulum ya da ayrı bir çalışma zamanı gerekmez.

Arayüz Türkçe. Metinler `src/Kutu.Sdk/Resources/Strings.resx` içinde. İngilizce karşılıkları `Strings.en.resx` dosyasında; Ayarlar'dan English seçilince bir sonraki açılışta kullanılır.

## Özellikler

- **Görsel dönüştür** — sağ tık menüsünde **Kutu ile dönüştür** (JPEG, PNG, WebP, ICO, PDF, BMP). PNG, JPG, WebP, BMP, ICO, PDF ve HEIC. HEIC, Windows HEIF koduyla okunur (Microsoft Store: HEIF Image Extensions). ImageMagick yok.
- **Toplu boyut ve sıkıştırma** — uzun kenar, genişlik, yükseklik veya yüzde; JPG / WebP / PNG.
- **Ekran görüntüsü** — donmuş ekranda bölge seçimi ve yerinde işaretleme. Kopyala veya kaydet. Yükle kapalıdır (Yakında). Esc vazgeçer.
- **Renk seçici** — HEX, RGB, HSL, kopyala, geçmiş.
- **Pano geçmişi** — Ctrl+Alt+V ile açılır; son 50 kayıt, arama, sabitleme. Karttaki anahtar kapatır.
- **Metin genişletici** — tetikleyici tam yazılınca hemen genişler (`:adm`, `mail:imza`). Ayrı bir kanca iş parçacığı. Karttaki anahtar kapatır.
- **WHOIS + DNS** — RDAP (`rdap.org`), anahtarsız. A, AAAA, MX, NS, TXT, CNAME.
- **IP itibarı** — AbuseIPDB, VirusTotal, IPQualityScore, OTX, GreyNoise, Shodan, IPinfo (anahtar Ayarlar'da, DPAPI). Anahtarsız: ipwho.is, Spamhaus ZEN, SpamCop, Barracuda ve TOR çıkış listesi. Symantec / Broadcom Site Review için herkese açık bir API yok; kart yalnızca tarayıcıda açılan arama bağlantısıdır, skor üretilmez.
- **Toplu IP** — yapıştırılan liste veya txt. Aynı sağlayıcılar ve DNSBL. CSV. “Benim IP'm”.
- **Ping ve port** — gecikme, tek port, aralık, sık kullanılanlar ve **Tüm portlar** (1–65535). Dört kip tek seçimdir; yalnız seçili olan işaretlidir. 3389 ve 22, aralıkta varsa önce taranır. Tablo, kopya ve CSV yalnız açık portları içerir; kapalı ve filtrelenenler yazılmaz. Hiçbiri açık değilse “Açık port bulunamadı” görünür. Açık portta banner ve RDP / HTTP / TLS yoklaması yapılır. İlerleme satırı `x / y · açık n · kalan` olarak kalır.
- **SSL kontrol** — yayıncı, konu, SAN, geçerlilik, kalan gün, zincir, TLS sürümü.
- **Bağlantıya yükle** — kart soluk, düğme tıklanmaz, ipucu Yakında.
- **Çeviri** — Türkçe metin İngilizceye, diğer diller Türkçeye. Yerel ipucu, sonra sağlayıcının algıladığı dil; Türkçeye gidip Türkçe dönerse İngilizceye yeniden sorulur. Önce Google, sonra DeepL, kısa metinde MyMemory. Uzun metin tek POST ile gider; bölünürse paragraf ve cümle sınırında, sırayı ve satır sonlarını koruyarak birleşir. Ctrl+Alt+T seçili metni yerinde çevirir.
- **QR** — oluştur, PNG kopyala veya kaydet. Ekran bölgesi, dosya ve panodan oku.
- **OCR** — ekran bölgesi veya görsel. Windows OCR (Türkçe / İngilizce dil paketi gerekir).
- **Ekran çevirisi** — Ctrl+Alt+E. Önce donmuş ekran görüntüsü, sonra bölge veya tam ekran. Kutu yalnız çevrilen satırın altında durur; hedef dildeki satır gösterilmez. Hedef dil Ayarlar'da, varsayılan Türkçe. Esc kapatır.
- **Parola** — uzunluk, büyük/küçük/rakam/simge, belirsiz karakterleri çıkar. Kriptografik rastgelelik. Varsayılanlar Ayarlar'da.
- **Video indir** — bağlantıdan MP4 veya MP3. yt-dlp ve ffmpeg kuruluma gömülmez. Eksik olanlar için **İndir (boyut)** resmi sürümü `%AppData%\Kutu\tools` altına alır, SHA256 doğrular ve `indirilen / toplam MB · hız · kalan` gösterir. İkisi de kuruluysa İndir gizlenir.
- **Ekran kaydı** — Ctrl+Alt+R. Bölge veya tam ekran, MP4 veya GIF, isteğe bağlı imleç. **Kaydı başlat** ffmpeg kurulana kadar kapalıdır; **Durdur** yalnız kayıt sürerken açıktır. ffmpeg aynı klasöre, yalnız kullanıcı isterse.

Sürüm 1.1 açılışta ve yaklaşık 12 saatte bir GitHub Releases'a bakar. Yeni sürüm varsa pencerenin üstünde ince bir şerit görünür: **Güncelle** ve kapat. Kapatmak o sürümün şeridini gizler; Ayarlar'daki “Yeni sürüm mevcut” satırı kalır. **Güncellemeleri denetle** varsayılan açıktır. **Otomatik güncelle** varsayılan kapalıdır. Ağ hatası sessizdir.

**Alt+Space** Spotlight benzeri başlatıcı açar: `ss`, `renk`, `whois`, `çevir`.

Kısayollar Ayarlar'daki **Kısayollar** bölümünde her araç için ayrı kaydedilir; çakışan veya sistemin reddettiği kombinasyon `kullanımda` görünür. Kart, aracın kendi kısayolunu gösterir, kısayolu yoksa rozet yoktur. Başlatıcı Alt+Space olarak kalır. Windows ile başlatma ve sağ tık menüsü de Ayarlar'dadır. API anahtarları koda yazılmaz; `%AppData%\Kutu\settings.json` içinde DPAPI (CurrentUser) ile şifrelenir.

## Exe nerede

Her push'ta Actions çıktısı `Kutu-win-x64` üç dosyadır: `Kutu.exe`, `Kutu.exe.sha256` ve `Kutu.zip` (zip'in içinde yalnız `Kutu.exe`). .NET Framework 4.8 üzerine kuruludur. Windows 10, Windows 11 ve Windows Server 2019 bu çerçeveyi hazır getirir; ayrı bir çalışma zamanı kurulmaz. Bağımlılıklar ve `kutuwebp.dll` exe'nin içindedir.

Sürüm etiketi (`v*`) itilince bu üç dosya GitHub Release'a eklenir. İndirme için zip yeterlidir. Kurulum yok. İlk açılışta Windows SmartScreen uyarabilir; imzasız bir topluluk aracı olduğu için bu beklenen bir durum. Uygulama içi güncelleme zip'i kullanmaz; `Kutu.exe` ve `Kutu.exe.sha256` dosyalarını `HttpClient` ile indirir, özeti doğrular, çalışan exe'yi `Kutu.old.exe` olarak yeniden adlandırır, yeni dosyayı yerine yazar ve `--cleanup` ile yeniden başlatır.

Derleme sıkıştırmaz ve gizlemez. `kutuwebp.dll` yalnız WebP gerekirken `%LOCALAPPDATA%\Kutu\bin` altına sabit adla yazılır. Windows Defender yine de imzasız bir exe'yi sezgisel olarak işaretleyebilir. Yanlış alarmı [Microsoft'a bildirmek](https://www.microsoft.com/en-us/wdsi/filesubmission) için dosyayı “Software developer” olarak gönderin; algılama adı, dosya sürümü ve bu deponun adresini ekleyin. Açık kaynak imzası için [SignPath](https://signpath.io/) ücretsiz bir kuruluş hesabı verir. [Azure Trusted Signing](https://learn.microsoft.com/azure/trusted-signing/) (eski Artifact Signing) GitHub Actions ile kullanılabilir; kimlik doğrulaması ve ücret ister.

## Sağ tık menüsü

Ayarlar'daki anahtar klasik menüyü yazar. Yönetici istemez. **Kutu ile dönüştür** alt menüsü JPEG, PNG, WebP, ICO, PDF ve BMP içerir. Windows 11'de bu öğe çoğunlukla **Daha fazla seçenek** altındadır.

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

v2 için bırakılan yerler: yazı tipi bulucu, arka plan silme, UUID ve özet.

## Symantec / Broadcom

Broadcom'un 175706 numaralı makalesi Site Review için herkese açık bir API olmadığını yazar. `sitereview.bluecoat.com` otomatik sorgulamayı kabul edilebilir kullanımın dışında sayar. WebPulse uçları lisanslı Security Analytics içindedir. Kutu skor uydurmaz. Her IP için tarayıcıda şu adresi açar: `https://sitereview.bluecoat.com/sitereview.jsp?url=` ve adresi kaçışlar.

## Lisans

MIT. © 2026 Kerim Ekşi · [kerimeksi.com](https://kerimeksi.com)

# Kutu yükleme ucu

Tek dosyalık PHP. Dosyayı (veya metni) saklar ve paylaşılabilir bir adres döner. Anahtar kodda durmaz: sunucuda ortam değişkeni olarak verilir.

## Kurulum

1. `upload.php` dosyasını herkese açık bir dizine kopyala. Örnek: `https://kerimeksi.com/kutu/upload.php`.
2. Yanına yazılabilir bir `uploads/` klasörü aç. Bu depodaki `.htaccess`, Apache'de PHP çalıştırmayı kapatır.
3. Jetonu ayarla. `change-me` bırakılırsa uç 500 döner.

```bash
export KUTU_UPLOAD_TOKEN='uzun-rastgele-jeton'
# isteğe bağlı, dönen adresin kökü:
export KUTU_PUBLIC_BASE='https://kerimeksi.com/kutu'
```

Apache / php-fpm için aynı değişkeni sanal konakta da tanımlayabilirsin. Nginx'te `uploads/` içindeki betikleri çalıştırma:

```nginx
location /kutu/uploads/ {
    location ~* \.(php|phtml|phar)$ { deny all; }
}
```

4. Kutu **Ayarlar** ekranında yükleme adresi olarak `https://kerimeksi.com/kutu/upload.php`, anahtar olarak da aynı jetonu yaz. Anahtar bu Windows kullanıcısında DPAPI ile şifrelenir.

## İstek

`multipart/form-data`, alan adı `file`. Başlık: `X-Api-Key: <jeton>` veya `Authorization: Bearer <jeton>`.

Başarılı cevap:

```json
{"ok": true, "url": "https://kerimeksi.com/kutu/uploads/abc.png"}
```

Üst sınır 15 MB. İzinli uzantılar: png, jpg, jpeg, webp, gif, bmp, txt, md, json, pdf, ico, heic. Metin için `text` alanı da kabul edilir; Kutu metni `note.txt` olarak gönderir.

Dosya adı rastgeledir. Eski dosyaları sen silersin; bu uçta süre dolumu yoktur.

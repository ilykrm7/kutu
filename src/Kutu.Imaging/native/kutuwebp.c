/* libwebp 1.5.0, linked statically (KERNEL32 + msvcrt only). */
#include <stddef.h>
#include <stdint.h>
#include "webp/encode.h"
#include "webp/decode.h"

__declspec(dllexport) int KutuWebpEncode(const uint8_t *bgra, int width, int height, int stride, float quality, uint8_t **output, size_t *size)
{
    uint8_t *bytes = NULL;
    size_t written;
    if (bgra == NULL || width <= 0 || height <= 0 || stride < width * 4 || output == NULL || size == NULL)
        return 0;

    written = WebPEncodeBGRA(bgra, width, height, stride, quality, &bytes);
    if (written == 0 || bytes == NULL)
        return 0;

    *output = bytes;
    *size = written;
    return 1;
}

__declspec(dllexport) int KutuWebpDecode(const uint8_t *data, size_t size, uint8_t **bgra, int *width, int *height)
{
    int w = 0;
    int h = 0;
    uint8_t *pixels;
    if (bgra == NULL || width == NULL || height == NULL)
        return 0;

    *bgra = NULL;
    *width = 0;
    *height = 0;
    if (data == NULL || size == 0)
        return 0;

    pixels = WebPDecodeBGRA(data, size, &w, &h);
    if (pixels == NULL || w <= 0 || h <= 0)
    {
        WebPFree(pixels);
        return 0;
    }

    *bgra = pixels;
    *width = w;
    *height = h;
    return 1;
}

__declspec(dllexport) void KutuWebpFree(uint8_t *ptr)
{
    WebPFree(ptr);
}

/* libwebp 1.5.0, linked statically (KERNEL32 + msvcrt only). */
#include <stddef.h>
#include <stdint.h>
#include "webp/encode.h"

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

__declspec(dllexport) void KutuWebpFree(uint8_t *ptr)
{
    WebPFree(ptr);
}

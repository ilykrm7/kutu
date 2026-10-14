// Tiny Win32 host that lives in %AppData%\Kutu\shell\<version>.
// The sparse package needs an exe in that folder. The real Kutu.exe stays where the user put it.
#define UNICODE
#define _UNICODE
#include <windows.h>
#include <strsafe.h>

static bool ExePath(wchar_t *out, DWORD cch) {
    wchar_t self[MAX_PATH];
    DWORD length = GetModuleFileNameW(nullptr, self, MAX_PATH);
    if (length == 0 || length >= MAX_PATH)
        return false;
    wchar_t *slash = wcsrchr(self, L'\\');
    if (!slash)
        return false;
    *(slash + 1) = 0;
    wchar_t pathFile[MAX_PATH];
    if (FAILED(StringCchCopyW(pathFile, MAX_PATH, self)) || FAILED(StringCchCatW(pathFile, MAX_PATH, L"kutu.path")))
        return false;
    HANDLE file = CreateFileW(pathFile, GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE)
        return false;
    char utf8[1024];
    DWORD read = 0;
    BOOL ok = ReadFile(file, utf8, sizeof(utf8) - 1, &read, nullptr);
    CloseHandle(file);
    if (!ok)
        return false;
    utf8[read] = 0;
    while (read > 0 && (utf8[read - 1] == '\n' || utf8[read - 1] == '\r' || utf8[read - 1] == ' '))
        utf8[--read] = 0;
    if (MultiByteToWideChar(CP_UTF8, 0, utf8, -1, out, static_cast<int>(cch)) <= 0)
        return false;
    return GetFileAttributesW(out) != INVALID_FILE_ATTRIBUTES;
}

int WINAPI WinMain(HINSTANCE, HINSTANCE, LPSTR, int) {
    wchar_t exe[MAX_PATH];
    if (!ExePath(exe, MAX_PATH))
        return 1;
    wchar_t cmd[MAX_PATH + 8];
    if (FAILED(StringCchPrintfW(cmd, MAX_PATH + 8, L"\"%s\"", exe)))
        return 1;
    STARTUPINFOW si{};
    si.cb = sizeof(si);
    PROCESS_INFORMATION pi{};
    if (!CreateProcessW(exe, cmd, nullptr, nullptr, FALSE, 0, nullptr, nullptr, &si, &pi))
        return 1;
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return 0;
}

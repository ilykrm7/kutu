// Per-user Windows 11 context menu: "Kutu ile dönüştür" and format submenu.
#define UNICODE
#define _UNICODE
#include <windows.h>
#include <shlobj.h>
#include <shobjidl.h>
#include <strsafe.h>
#include <new>

void operator delete(void *p) noexcept { if (p) HeapFree(GetProcessHeap(), 0, p); }
void operator delete(void *p, size_t) noexcept { if (p) HeapFree(GetProcessHeap(), 0, p); }

template <class T, class... Args>
static T *Make(Args &&...args) {
    void *mem = HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(T));
    if (!mem)
        return nullptr;
    return new (mem) T(static_cast<Args &&>(args)...);
}

static const CLSID CLSID_KutuConvert = {
    0xb6e1a2c4, 0x7d33, 0x4f1a, {0x9c, 0x58, 0x2e, 0x6a, 0x4b, 0x8d, 0x1f, 0x70}};

static const wchar_t *kFormats[] = {L"jpeg", L"png", L"webp", L"ico", L"pdf", L"bmp"};
static const wchar_t *kTitles[] = {L"JPEG", L"PNG", L"WebP", L"ICO", L"PDF", L"BMP"};
static const int kCount = 7;
static long g_locks = 0;

static wchar_t *AllocString(const wchar_t *text) {
    size_t bytes = (wcslen(text) + 1) * sizeof(wchar_t);
    auto *copy = static_cast<wchar_t *>(CoTaskMemAlloc(bytes));
    if (copy)
        memcpy(copy, text, bytes);
    return copy;
}

static void ExePath(wchar_t *out, DWORD cch) {
    wchar_t dll[MAX_PATH];
    HMODULE module = nullptr;
    GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       reinterpret_cast<LPCWSTR>(&ExePath), &module);
    GetModuleFileNameW(module, dll, MAX_PATH);
    wchar_t *slash = wcsrchr(dll, L'\\');
    if (slash)
        *(slash + 1) = 0;
    wchar_t pathFile[MAX_PATH];
    StringCchCopyW(pathFile, MAX_PATH, dll);
    StringCchCatW(pathFile, MAX_PATH, L"kutu.path");
    HANDLE file = CreateFileW(pathFile, GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file != INVALID_HANDLE_VALUE) {
        char utf8[1024];
        DWORD read = 0;
        ReadFile(file, utf8, sizeof(utf8) - 1, &read, nullptr);
        CloseHandle(file);
        utf8[read] = 0;
        while (read > 0 && (utf8[read - 1] == '\n' || utf8[read - 1] == '\r' || utf8[read - 1] == ' '))
            utf8[--read] = 0;
        MultiByteToWideChar(CP_UTF8, 0, utf8, -1, out, static_cast<int>(cch));
        if (GetFileAttributesW(out) != INVALID_FILE_ATTRIBUTES)
            return;
    }
    StringCchCopyW(out, cch, dll);
    StringCchCatW(out, cch, L"Kutu.exe");
}

static void Launch(const wchar_t *format, IShellItemArray *items) {
    if (!items)
        return;
    DWORD count = 0;
    if (FAILED(items->GetCount(&count)) || count == 0)
        return;
    wchar_t exe[MAX_PATH];
    ExePath(exe, MAX_PATH);
    wchar_t *cmd = static_cast<wchar_t *>(CoTaskMemAlloc(32768 * sizeof(wchar_t)));
    if (!cmd)
        return;
    StringCchPrintfW(cmd, 32768, L"\"%s\" --convert-to %s", exe, format);
    for (DWORD i = 0; i < count && i < 32; i++) {
        IShellItem *item = nullptr;
        if (FAILED(items->GetItemAt(i, &item)) || !item)
            continue;
        LPWSTR path = nullptr;
        if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &path)) && path) {
            StringCchCatW(cmd, 32768, L" \"");
            StringCchCatW(cmd, 32768, path);
            StringCchCatW(cmd, 32768, L"\"");
            CoTaskMemFree(path);
        }
        item->Release();
    }
    STARTUPINFOW si{};
    si.cb = sizeof(si);
    PROCESS_INFORMATION pi{};
    if (CreateProcessW(nullptr, cmd, nullptr, nullptr, FALSE, 0, nullptr, nullptr, &si, &pi)) {
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
    }
    CoTaskMemFree(cmd);
}

class Command final : public IExplorerCommand {
public:
    explicit Command(int index) : refs_(1), index_(index) { InterlockedIncrement(&g_locks); }
    ~Command() { InterlockedDecrement(&g_locks); }

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void **ppv) override {
        if (!ppv)
            return E_POINTER;
        if (riid == IID_IUnknown || riid == IID_IExplorerCommand) {
            *ppv = static_cast<IExplorerCommand *>(this);
            AddRef();
            return S_OK;
        }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&refs_); }
    ULONG STDMETHODCALLTYPE Release() override {
        ULONG n = InterlockedDecrement(&refs_);
        if (n == 0)
            delete this;
        return n;
    }
    HRESULT STDMETHODCALLTYPE GetTitle(IShellItemArray *, LPWSTR *name) override {
        if (!name)
            return E_POINTER;
        *name = AllocString(index_ < 0 ? L"Kutu ile dönüştür" : kTitles[index_]);
        return *name ? S_OK : E_OUTOFMEMORY;
    }
    HRESULT STDMETHODCALLTYPE GetIcon(IShellItemArray *, LPWSTR *icon) override {
        if (!icon)
            return E_POINTER;
        wchar_t exe[MAX_PATH];
        ExePath(exe, MAX_PATH);
        wchar_t spec[MAX_PATH + 8];
        StringCchPrintfW(spec, MAX_PATH + 8, L"%s,0", exe);
        *icon = AllocString(spec);
        return *icon ? S_OK : E_OUTOFMEMORY;
    }
    HRESULT STDMETHODCALLTYPE GetToolTip(IShellItemArray *, LPWSTR *tip) override {
        if (!tip)
            return E_POINTER;
        *tip = AllocString(L"Kutu");
        return *tip ? S_OK : E_OUTOFMEMORY;
    }
    HRESULT STDMETHODCALLTYPE GetCanonicalName(GUID *guid) override {
        if (!guid)
            return E_POINTER;
        *guid = CLSID_KutuConvert;
        if (index_ >= 0)
            guid->Data4[7] = static_cast<unsigned char>(index_ + 1);
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetState(IShellItemArray *, WINBOOL, EXPCMDSTATE *state) override {
        if (!state)
            return E_POINTER;
        *state = ECS_ENABLED;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE Invoke(IShellItemArray *items, IBindCtx *) override {
        if (index_ < 0)
            return E_NOTIMPL;
        Launch(kFormats[index_], items);
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetFlags(EXPCMDFLAGS *flags) override {
        if (!flags)
            return E_POINTER;
        *flags = index_ < 0 ? ECF_HASSUBCOMMANDS : 0;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE EnumSubCommands(IEnumExplorerCommand **ppEnum) override;

private:
    long refs_;
    int index_;
};

class EnumCommands final : public IEnumExplorerCommand {
public:
    EnumCommands() : refs_(1), cursor_(0) { InterlockedIncrement(&g_locks); }
    ~EnumCommands() { InterlockedDecrement(&g_locks); }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void **ppv) override {
        if (!ppv)
            return E_POINTER;
        if (riid == IID_IUnknown || riid == IID_IEnumExplorerCommand) {
            *ppv = static_cast<IEnumExplorerCommand *>(this);
            AddRef();
            return S_OK;
        }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&refs_); }
    ULONG STDMETHODCALLTYPE Release() override {
        ULONG n = InterlockedDecrement(&refs_);
        if (n == 0)
            delete this;
        return n;
    }
    HRESULT STDMETHODCALLTYPE Next(ULONG celt, IExplorerCommand **commands, ULONG *fetched) override {
        ULONG n = 0;
        while (n < celt && cursor_ < kCount) {
            commands[n++] = Make<Command>(cursor_++);
            if (!commands[n - 1])
                return E_OUTOFMEMORY;
        }
        if (fetched)
            *fetched = n;
        return n == celt ? S_OK : S_FALSE;
    }
    HRESULT STDMETHODCALLTYPE Skip(ULONG celt) override {
        cursor_ += static_cast<int>(celt);
        return cursor_ <= kCount ? S_OK : S_FALSE;
    }
    HRESULT STDMETHODCALLTYPE Reset() override {
        cursor_ = 0;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE Clone(IEnumExplorerCommand **ppEnum) override {
        if (!ppEnum)
            return E_POINTER;
        auto *copy = Make<EnumCommands>();
        if (!copy)
            return E_OUTOFMEMORY;
        copy->cursor_ = cursor_;
        *ppEnum = copy;
        return S_OK;
    }

private:
    long refs_;
    int cursor_;
};

HRESULT Command::EnumSubCommands(IEnumExplorerCommand **ppEnum) {
    if (index_ >= 0)
        return E_NOTIMPL;
    if (!ppEnum)
        return E_POINTER;
    *ppEnum = Make<EnumCommands>();
    return *ppEnum ? S_OK : E_OUTOFMEMORY;
}

class Factory final : public IClassFactory {
public:
    Factory() : refs_(1) {}
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void **ppv) override {
        if (!ppv)
            return E_POINTER;
        if (riid == IID_IUnknown || riid == IID_IClassFactory) {
            *ppv = static_cast<IClassFactory *>(this);
            AddRef();
            return S_OK;
        }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&refs_); }
    ULONG STDMETHODCALLTYPE Release() override {
        ULONG n = InterlockedDecrement(&refs_);
        if (n == 0)
            delete this;
        return n;
    }
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown *outer, REFIID riid, void **ppv) override {
        if (outer)
            return CLASS_E_NOAGGREGATION;
        auto *command = Make<Command>(-1);
        if (!command)
            return E_OUTOFMEMORY;
        HRESULT hr = command->QueryInterface(riid, ppv);
        command->Release();
        return hr;
    }
    HRESULT STDMETHODCALLTYPE LockServer(WINBOOL lock) override {
        if (lock)
            InterlockedIncrement(&g_locks);
        else
            InterlockedDecrement(&g_locks);
        return S_OK;
    }

private:
    long refs_;
};

extern "C" __declspec(dllexport) HRESULT STDAPICALLTYPE DllGetClassObject(REFCLSID rclsid, REFIID riid, void **ppv) {
    if (!ppv)
        return E_POINTER;
    *ppv = nullptr;
    if (rclsid != CLSID_KutuConvert)
        return CLASS_E_CLASSNOTAVAILABLE;
    auto *factory = Make<Factory>();
    if (!factory)
        return E_OUTOFMEMORY;
    HRESULT hr = factory->QueryInterface(riid, ppv);
    factory->Release();
    return hr;
}

extern "C" __declspec(dllexport) HRESULT STDAPICALLTYPE DllCanUnloadNow() {
    return g_locks == 0 ? S_OK : S_FALSE;
}

extern "C" BOOL APIENTRY DllMain(HMODULE, DWORD, LPVOID) { return TRUE; }

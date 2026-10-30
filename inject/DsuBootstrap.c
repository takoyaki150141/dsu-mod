#include <windows.h>

typedef wchar_t *DSU_BSTR;

typedef HRESULT (STDMETHODCALLTYPE *PFN_CorBindToRuntimeEx)(LPCWSTR version, LPCWSTR buildFlavor, DWORD flags, REFCLSID clsid, REFIID riid, LPVOID *instance);
typedef DSU_BSTR (STDAPICALLTYPE *PFN_SysAllocString)(const wchar_t *text);
typedef void (STDAPICALLTYPE *PFN_SysFreeString)(DSU_BSTR text);
typedef HRESULT (STDAPICALLTYPE *PFN_CoInitializeEx)(LPVOID reserved, DWORD coInit);
typedef void (STDAPICALLTYPE *PFN_CoUninitialize)(void);

typedef struct ICorRuntimeHostVtbl ICorRuntimeHostVtbl;
typedef struct ICorRuntimeHost {
    ICorRuntimeHostVtbl *lpVtbl;
} ICorRuntimeHost;

struct ICorRuntimeHostVtbl {
    HRESULT (STDMETHODCALLTYPE *QueryInterface)(ICorRuntimeHost *This, REFIID riid, void **ppvObject);
    ULONG (STDMETHODCALLTYPE *AddRef)(ICorRuntimeHost *This);
    ULONG (STDMETHODCALLTYPE *Release)(ICorRuntimeHost *This);
    HRESULT (STDMETHODCALLTYPE *CreateLogicalThreadState)(ICorRuntimeHost *This);
    HRESULT (STDMETHODCALLTYPE *DeleteLogicalThreadState)(ICorRuntimeHost *This);
    HRESULT (STDMETHODCALLTYPE *SwitchInLogicalThreadState)(ICorRuntimeHost *This, DWORD *pFiberCookie);
    HRESULT (STDMETHODCALLTYPE *SwitchOutLogicalThreadState)(ICorRuntimeHost *This, DWORD **pFiberCookie);
    HRESULT (STDMETHODCALLTYPE *LocksHeldByLogicalThread)(ICorRuntimeHost *This, DWORD *pCount);
    HRESULT (STDMETHODCALLTYPE *MapFile)(ICorRuntimeHost *This, HANDLE hFile, HMODULE *phMap);
    HRESULT (STDMETHODCALLTYPE *GetConfiguration)(ICorRuntimeHost *This, void **ppConfiguration);
    HRESULT (STDMETHODCALLTYPE *Start)(ICorRuntimeHost *This);
    HRESULT (STDMETHODCALLTYPE *Stop)(ICorRuntimeHost *This);
    HRESULT (STDMETHODCALLTYPE *CreateDomain)(ICorRuntimeHost *This, LPCWSTR friendlyName, void *identityArray, void **appDomain);
    HRESULT (STDMETHODCALLTYPE *GetDefaultDomain)(ICorRuntimeHost *This, void **appDomain);
    HRESULT (STDMETHODCALLTYPE *EnumDomains)(ICorRuntimeHost *This, void **hEnum);
    HRESULT (STDMETHODCALLTYPE *NextDomain)(ICorRuntimeHost *This, void *hEnum, void **appDomain);
    HRESULT (STDMETHODCALLTYPE *CloseEnum)(ICorRuntimeHost *This, void *hEnum);
    HRESULT (STDMETHODCALLTYPE *CreateDomainEx)(ICorRuntimeHost *This, LPCWSTR friendlyName, void *setup, void *evidence, void **appDomain);
    HRESULT (STDMETHODCALLTYPE *CreateDomainSetup)(ICorRuntimeHost *This, void **appDomainSetup);
    HRESULT (STDMETHODCALLTYPE *CreateEvidence)(ICorRuntimeHost *This, void **evidence);
    HRESULT (STDMETHODCALLTYPE *UnloadDomain)(ICorRuntimeHost *This, void *appDomain);
    HRESULT (STDMETHODCALLTYPE *CurrentDomain)(ICorRuntimeHost *This, void **appDomain);
};

typedef HRESULT (STDMETHODCALLTYPE *PFN_AppDomainExecuteAssembly)(void *This, DSU_BSTR assemblyFile, void *evidence, INT32 *result);

static const CLSID CLSID_CorRuntimeHost_LOCAL = { 0xCB2F6723, 0xAB3A, 0x11D2, { 0x9C, 0x40, 0x00, 0xC0, 0x4F, 0xA3, 0x0A, 0x3E } };
static const IID IID_ICorRuntimeHost_LOCAL = { 0xCB2F6722, 0xAB3A, 0x11D2, { 0x9C, 0x40, 0x00, 0xC0, 0x4F, 0xA3, 0x0A, 0x3E } };
static const IID IID_AppDomain_LOCAL = { 0x05F696DC, 0x2B29, 0x3663, { 0xAD, 0x8B, 0xC4, 0x38, 0x9C, 0xF2, 0xA7, 0x13 } };

typedef struct IUnknownVtbl {
    HRESULT (STDMETHODCALLTYPE *QueryInterface)(void *This, REFIID riid, void **ppvObject);
    ULONG (STDMETHODCALLTYPE *AddRef)(void *This);
    ULONG (STDMETHODCALLTYPE *Release)(void *This);
} IUnknownVtbl;

typedef struct IUnknownLocal {
    IUnknownVtbl *lpVtbl;
} IUnknownLocal;

static void GetBaseDir(HMODULE module, wchar_t *dir)
{
    wchar_t path[MAX_PATH];
    DWORD length = GetModuleFileNameW(module, path, MAX_PATH);
    if (length == 0 || length >= MAX_PATH)
    {
        path[0] = L'\0';
    }
    wcscpy(dir, path);
    wchar_t *slash = wcsrchr(dir, L'\\');
    if (slash != NULL)
    {
        *slash = L'\0';
    }
}

static void LogLine(const wchar_t *dir, const wchar_t *message)
{
    wchar_t path[MAX_PATH * 2];
    wcscpy(path, dir);
    wcscat(path, L"\\bootstrap.log");

    HANDLE file = CreateFileW(path, FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (file == INVALID_HANDLE_VALUE)
    {
        return;
    }

    DWORD written = 0;
    DWORD length = (DWORD)wcslen(message);
    WriteFile(file, message, length * sizeof(wchar_t), &written, NULL);
    WriteFile(file, L"\r\n", 2 * sizeof(wchar_t), &written, NULL);
    CloseHandle(file);
}

static DWORD WINAPI Worker(LPVOID parameter)
{
    HMODULE module = (HMODULE)parameter;
    wchar_t dir[MAX_PATH];
    GetBaseDir(module, dir);

    wchar_t runner[MAX_PATH * 2];
    wchar_t plugin[MAX_PATH * 2];
    wchar_t fullPlugin[MAX_PATH * 2];

    wcscpy(runner, dir);
    wcscat(runner, L"\\DsuInjector.exe");

    wcscpy(plugin, dir);
    wcscat(plugin, L"\\..\\DsuExt.dll");
    GetFullPathNameW(plugin, MAX_PATH * 2, fullPlugin, NULL);
    wcscpy(plugin, fullPlugin);

    LogLine(dir, L"bootstrap start");

    HMODULE ole32 = LoadLibraryW(L"ole32.dll");
    PFN_CoInitializeEx coInitializeEx = NULL;
    PFN_CoUninitialize coUninitialize = NULL;
    if (ole32 != NULL)
    {
        coInitializeEx = (PFN_CoInitializeEx)GetProcAddress(ole32, "CoInitializeEx");
        coUninitialize = (PFN_CoUninitialize)GetProcAddress(ole32, "CoUninitialize");
    }
    if (coInitializeEx != NULL)
    {
        HRESULT coHr = coInitializeEx(NULL, 0);
        wchar_t buffer[256];
        wsprintfW(buffer, L"CoInitializeEx: 0x%08X", (unsigned int)coHr);
        LogLine(dir, buffer);
    }

    HMODULE mscoree = LoadLibraryW(L"mscoree.dll");
    if (mscoree == NULL)
    {
        LogLine(dir, L"LoadLibraryW(mscoree.dll) failed");
        return 1;
    }

    PFN_CorBindToRuntimeEx bindToRuntime = (PFN_CorBindToRuntimeEx)GetProcAddress(mscoree, "CorBindToRuntimeEx");
    if (bindToRuntime == NULL)
    {
        LogLine(dir, L"GetProcAddress(CorBindToRuntimeEx) failed");
        return 2;
    }

    ICorRuntimeHost *host = NULL;
    HRESULT hr = bindToRuntime(L"v4.0.30319", NULL, 0, &CLSID_CorRuntimeHost_LOCAL, &IID_ICorRuntimeHost_LOCAL, (LPVOID *)&host);
    if (FAILED(hr) || host == NULL)
    {
        wchar_t buffer[256];
        wsprintfW(buffer, L"CorBindToRuntimeEx failed: 0x%08X", (unsigned int)hr);
        LogLine(dir, buffer);
        return 3;
    }

    hr = host->lpVtbl->Start(host);
    {
        wchar_t buffer[256];
        wsprintfW(buffer, L"ICorRuntimeHost::Start: 0x%08X", (unsigned int)hr);
        LogLine(dir, buffer);
    }

    void *domain = NULL;
    hr = host->lpVtbl->GetDefaultDomain(host, &domain);
    if (FAILED(hr) || domain == NULL)
    {
        wchar_t buffer[256];
        wsprintfW(buffer, L"GetDefaultDomain failed: 0x%08X", (unsigned int)hr);
        LogLine(dir, buffer);
        host->lpVtbl->Release(host);
        return 4;
    }
    LogLine(dir, L"GetDefaultDomain ok");

    void *appDomain = NULL;
    HRESULT qiHr = ((IUnknownLocal *)domain)->lpVtbl->QueryInterface(domain, &IID_AppDomain_LOCAL, &appDomain);
    {
        wchar_t buffer[256];
        wsprintfW(buffer, L"QueryInterface(_AppDomain): 0x%08X", (unsigned int)qiHr);
        LogLine(dir, buffer);
    }
    if (FAILED(qiHr) || appDomain == NULL)
    {
        host->lpVtbl->Release(host);
        return 6;
    }

    HMODULE oleaut32 = LoadLibraryW(L"oleaut32.dll");
    PFN_SysAllocString sysAllocString = NULL;
    PFN_SysFreeString sysFreeString = NULL;
    if (oleaut32 != NULL)
    {
        sysAllocString = (PFN_SysAllocString)GetProcAddress(oleaut32, "SysAllocString");
        sysFreeString = (PFN_SysFreeString)GetProcAddress(oleaut32, "SysFreeString");
    }
    if (sysAllocString == NULL || sysFreeString == NULL)
    {
        LogLine(dir, L"oleaut32 SysAllocString was not found");
        host->lpVtbl->Release(host);
        return 5;
    }

    DSU_BSTR runnerBstr = sysAllocString(runner);
    LogLine(dir, L"SysAllocString ok");

    void **domainVtable = *(void ***)appDomain;
    PFN_AppDomainExecuteAssembly executeAssembly = (PFN_AppDomainExecuteAssembly)domainVtable[50];
    INT32 result = -1;

    LogLine(dir, L"calling ExecuteAssembly slot 50");
    hr = executeAssembly(appDomain, runnerBstr, NULL, &result);
    sysFreeString(runnerBstr);

    {
        wchar_t buffer[256];
        wsprintfW(buffer, L"ExecuteAssembly: 0x%08X result=%d", (unsigned int)hr, (int)result);
        LogLine(dir, buffer);
    }

    if (oleaut32 != NULL)
    {
        FreeLibrary(oleaut32);
    }
    if (coUninitialize != NULL)
    {
        coUninitialize();
    }
    ((IUnknownLocal *)appDomain)->lpVtbl->Release(appDomain);
    ((IUnknownLocal *)domain)->lpVtbl->Release(domain);
    host->lpVtbl->Release(host);
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID reserved)
{
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(instance);
        HANDLE thread = CreateThread(NULL, 0, Worker, (LPVOID)instance, 0, NULL);
        if (thread != NULL)
        {
            CloseHandle(thread);
        }
    }
    return TRUE;
}


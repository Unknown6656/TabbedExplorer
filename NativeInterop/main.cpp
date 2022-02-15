#include <Windows.h>
#include <shlobj.h>


#define SCRATCH_QCM_FIRST 1
#define SCRATCH_QCM_LAST  0x7FFF

#undef HANDLE_WM_CONTEXTMENU
#define HANDLE_WM_CONTEXTMENU(hwnd, wParam, lParam, fn) ((fn)((hwnd), (HWND)(wParam), GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam)), 0L)



HRESULT GetUIObjectOfFile(HWND hwnd, LPCWSTR pszPath, REFIID riid, void** const ppv)
{
    LPITEMIDLIST pidl;
    SFGAOF sfgao;
    HRESULT hr;

    *ppv = nullptr;

    if (SUCCEEDED(hr = SHParseDisplayName(pszPath, nullptr, &pidl, 0, &sfgao)))
    {
        IShellFolder* psf;
        LPCITEMIDLIST pidlChild;

        if (SUCCEEDED(hr = SHBindToParent(pidl, IID_IShellFolder, (void**)&psf, &pidlChild)))
        {
            hr = psf->GetUIObjectOf(hwnd, 1, &pidlChild, riid, nullptr, ppv);
            psf->Release();
        }

        CoTaskMemFree(pidl);
    }

    return hr;
}


void OnContextMenu(HWND hwnd, HWND hwndContext, int xPos, int yPos)
{
    POINT pt = { xPos, yPos };

    if (pt.x == -1 && pt.y == -1)
    {
        pt.x = pt.y = 0;
        ClientToScreen(hwnd, &pt);
    }

    IContextMenu* pcm;
    IContextMenu2* g_pcm2;
    IContextMenu3* g_pcm3;

    if (SUCCEEDED(GetUIObjectOfFile(hwnd, L"C:\\Windows\\clock.avi", IID_IContextMenu, (void**)&pcm)))
    {
        HMENU hmenu = CreatePopupMenu();

        if (hmenu)
        {
            if (SUCCEEDED(pcm->QueryContextMenu(hmenu, 0, SCRATCH_QCM_FIRST, SCRATCH_QCM_LAST, CMF_NORMAL)))
            {
                pcm->QueryInterface(IID_IContextMenu2, (void**)&g_pcm2);
                pcm->QueryInterface(IID_IContextMenu3, (void**)&g_pcm3);

                int iCmd = TrackPopupMenuEx(hmenu, TPM_RETURNCMD, pt.x, pt.y, hwnd, NULL);

                if (g_pcm2)
                    g_pcm2->Release();

                if (g_pcm3)
                    g_pcm3->Release();

                g_pcm2 = nullptr;
                g_pcm3 = nullptr;

                if (iCmd > 0)
                {
                    CMINVOKECOMMANDINFOEX info = { 0 };

                    info.cbSize = sizeof(info);
                    info.fMask = CMIC_MASK_UNICODE | CMIC_MASK_PTINVOKE;

                    if (GetKeyState(VK_CONTROL) < 0)
                        info.fMask |= CMIC_MASK_CONTROL_DOWN;

                    if (GetKeyState(VK_SHIFT) < 0)
                        info.fMask |= CMIC_MASK_SHIFT_DOWN;

                    info.hwnd = hwnd;
                    info.lpVerb = MAKEINTRESOURCEA(iCmd – SCRATCH_QCM_FIRST);
                    info.lpVerbW = MAKEINTRESOURCEW(iCmd – SCRATCH_QCM_FIRST);
                    info.nShow = SW_SHOWNORMAL;
                    info.ptInvoke = pt;

                    pcm->InvokeCommand((LPCMINVOKECOMMANDINFO)&info);
                }
            }

            DestroyMenu(hmenu);
        }

        pcm->Release();
    }
}



extern "C" __declspec(dllexport) void __cdecl add(int)
{

}

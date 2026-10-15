#pragma once
#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include "resource.h"

inline HICON MediaMergeLoadIconW(HINSTANCE instance, LPCWSTR iconName) {
    if (instance == nullptr && iconName == IDI_APPLICATION) {
        if (HICON appIcon = ::LoadIconW(GetModuleHandleW(nullptr), MAKEINTRESOURCEW(IDI_APP_ICON))) {
            return appIcon;
        }
    }
    return ::LoadIconW(instance, iconName);
}

#define LoadIconW MediaMergeLoadIconW

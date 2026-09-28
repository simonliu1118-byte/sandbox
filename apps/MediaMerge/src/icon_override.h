#pragma once
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

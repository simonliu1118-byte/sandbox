#define UNICODE
#define _UNICODE
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <commctrl.h>
#include <commdlg.h>
#include <shellapi.h>

#include <algorithm>
#include <atomic>
#include <cwchar>
#include <cwctype>
#include <filesystem>
#include <fstream>
#include <memory>
#include <string>
#include <thread>
#include <vector>

#include "resource.h"

#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "comdlg32.lib")
#pragma comment(lib, "shell32.lib")

namespace fs = std::filesystem;

namespace {
constexpr wchar_t kClassName[] = L"MediaMergeMainWindow";
constexpr wchar_t kAppTitle[] = L"MediaMerge";
constexpr UINT WM_MERGE_DONE = WM_APP + 1;

constexpr int IDC_VIDEO_EDIT = 1001;
constexpr int IDC_VIDEO_BROWSE = 1002;
constexpr int IDC_AUDIO_EDIT = 1003;
constexpr int IDC_AUDIO_BROWSE = 1004;
constexpr int IDC_NAME_EDIT = 1005;
constexpr int IDC_START = 1006;
constexpr int IDC_PROGRESS = 1007;
constexpr int IDC_STATUS = 1008;
constexpr int IDC_OPEN_FOLDER = 1009;

HWND g_hwnd = nullptr;
HWND g_videoEdit = nullptr;
HWND g_audioEdit = nullptr;
HWND g_nameEdit = nullptr;
HWND g_startButton = nullptr;
HWND g_progress = nullptr;
HWND g_status = nullptr;
HWND g_openFolder = nullptr;
HFONT g_font = nullptr;
HFONT g_titleFont = nullptr;
HBRUSH g_bgBrush = nullptr;
std::atomic_bool g_busy{false};
std::wstring g_lastOutput;

HMENU ControlId(int id) {
    return reinterpret_cast<HMENU>(static_cast<INT_PTR>(id));
}

struct MergeResult {
    bool ok = false;
    std::wstring message;
    std::wstring outputPath;
};

std::wstring GetText(HWND hwnd) {
    const int len = GetWindowTextLengthW(hwnd);
    if (len <= 0) return L"";
    std::wstring value(static_cast<size_t>(len) + 1, L'\0');
    GetWindowTextW(hwnd, value.data(), len + 1);
    value.resize(static_cast<size_t>(len));
    return value;
}

std::wstring Trim(std::wstring s) {
    auto keep = [](wchar_t c) { return !iswspace(c); };
    s.erase(s.begin(), std::find_if(s.begin(), s.end(), keep));
    s.erase(std::find_if(s.rbegin(), s.rend(), keep).base(), s.end());
    return s;
}

bool ValidFileName(const std::wstring& name) {
    if (name.empty() || name == L"." || name == L"..") return false;
    if (name.find_first_of(L"<>:\"/\\|?*") != std::wstring::npos) return false;
    if (name.back() == L' ' || name.back() == L'.') return false;

    std::wstring upper = name;
    std::transform(upper.begin(), upper.end(), upper.begin(),
                   [](wchar_t c) { return static_cast<wchar_t>(towupper(c)); });
    static constexpr const wchar_t* reserved[] = {
        L"CON", L"PRN", L"AUX", L"NUL",
        L"COM1", L"COM2", L"COM3", L"COM4", L"COM5", L"COM6", L"COM7", L"COM8", L"COM9",
        L"LPT1", L"LPT2", L"LPT3", L"LPT4", L"LPT5", L"LPT6", L"LPT7", L"LPT8", L"LPT9"
    };
    for (const wchar_t* item : reserved) {
        if (upper == item) return false;
    }
    return true;
}

std::wstring Quote(const std::wstring& arg) {
    std::wstring out = L"\"";
    size_t slashes = 0;
    for (wchar_t c : arg) {
        if (c == L'\\') {
            ++slashes;
        } else if (c == L'\"') {
            out.append(slashes * 2 + 1, L'\\');
            out.push_back(L'\"');
            slashes = 0;
        } else {
            out.append(slashes, L'\\');
            slashes = 0;
            out.push_back(c);
        }
    }
    out.append(slashes * 2, L'\\');
    out += L'\"';
    return out;
}

fs::path LocalCacheDir() {
    wchar_t path[32768]{};
    DWORD length = GetEnvironmentVariableW(L"LOCALAPPDATA", path, static_cast<DWORD>(std::size(path)));
    fs::path dir;
    if (length > 0 && length < std::size(path)) {
        dir = fs::path(path) / L"MediaMerge";
    } else {
        wchar_t temp[MAX_PATH]{};
        GetTempPathW(MAX_PATH, temp);
        dir = fs::path(temp) / L"MediaMerge";
    }
    std::error_code ec;
    fs::create_directories(dir, ec);
    return dir;
}

bool EnsureFFmpeg(std::wstring& path, std::wstring& error) {
    HRSRC resource = FindResourceW(nullptr, MAKEINTRESOURCEW(IDR_FFMPEG), RT_RCDATA);
    if (!resource) {
        error = L"找不到內嵌 FFmpeg。";
        return false;
    }
    HGLOBAL loaded = LoadResource(nullptr, resource);
    const DWORD size = SizeofResource(nullptr, resource);
    const void* data = LockResource(loaded);
    if (!data || size == 0) {
        error = L"無法讀取內嵌 FFmpeg。";
        return false;
    }

    const fs::path target = LocalCacheDir() / L"ffmpeg-lgpl.exe";
    std::error_code ec;
    const bool needsWrite = !fs::exists(target, ec) || ec || fs::file_size(target, ec) != size;
    if (needsWrite) {
        const fs::path temp = target.wstring() + L".tmp";
        std::ofstream stream(temp, std::ios::binary | std::ios::trunc);
        if (!stream) {
            error = L"無法建立 FFmpeg 暫存檔。";
            return false;
        }
        stream.write(static_cast<const char*>(data), static_cast<std::streamsize>(size));
        stream.close();
        if (!stream) {
            error = L"FFmpeg 暫存檔寫入失敗。";
            return false;
        }
        fs::remove(target, ec);
        ec.clear();
        fs::rename(temp, target, ec);
        if (ec) {
            error = L"無法準備 FFmpeg 執行檔。";
            return false;
        }
    }
    path = target.wstring();
    return true;
}

struct ProcessResult {
    DWORD code = static_cast<DWORD>(-1);
    std::string text;
};

ProcessResult RunFFmpeg(const std::wstring& exe, const std::vector<std::wstring>& args) {
    ProcessResult result;
    SECURITY_ATTRIBUTES sa{sizeof(sa), nullptr, TRUE};
    HANDLE readPipe = nullptr;
    HANDLE writePipe = nullptr;
    if (!CreatePipe(&readPipe, &writePipe, &sa, 0)) return result;
    SetHandleInformation(readPipe, HANDLE_FLAG_INHERIT, 0);

    std::wstring command = Quote(exe);
    for (const auto& arg : args) command += L" " + Quote(arg);
    std::vector<wchar_t> mutableCommand(command.begin(), command.end());
    mutableCommand.push_back(L'\0');

    STARTUPINFOW si{};
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;
    si.hStdOutput = writePipe;
    si.hStdError = writePipe;
    si.hStdInput = GetStdHandle(STD_INPUT_HANDLE);

    PROCESS_INFORMATION pi{};
    const BOOL started = CreateProcessW(exe.c_str(), mutableCommand.data(), nullptr, nullptr, TRUE,
                                        CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi);
    CloseHandle(writePipe);
    if (!started) {
        CloseHandle(readPipe);
        return result;
    }

    char buffer[4096];
    DWORD count = 0;
    while (ReadFile(readPipe, buffer, sizeof(buffer), &count, nullptr) && count > 0) {
        if (result.text.size() < 1024 * 1024) result.text.append(buffer, count);
    }
    CloseHandle(readPipe);

    WaitForSingleObject(pi.hProcess, INFINITE);
    GetExitCodeProcess(pi.hProcess, &result.code);
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return result;
}

std::wstring ErrorTail(const std::string& input) {
    std::string tail = input;
    if (tail.size() > 1800) tail = tail.substr(tail.size() - 1800);
    if (tail.empty()) return L"FFmpeg 未回傳可讀的錯誤訊息。";
    int chars = MultiByteToWideChar(CP_UTF8, 0, tail.data(), static_cast<int>(tail.size()), nullptr, 0);
    if (chars <= 0) return L"FFmpeg 回傳無法解碼的錯誤訊息。";
    std::wstring wide(static_cast<size_t>(chars), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, tail.data(), static_cast<int>(tail.size()), wide.data(), chars);
    return wide;
}

std::wstring CompatibleAudioCodec(std::wstring ext) {
    std::transform(ext.begin(), ext.end(), ext.begin(),
                   [](wchar_t c) { return static_cast<wchar_t>(towlower(c)); });
    if (ext == L".webm") return L"libopus";
    if (ext == L".avi") return L"libmp3lame";
    return L"aac";
}

std::wstring UniqueOutput(const fs::path& video, const std::wstring& baseName) {
    const fs::path dir = video.parent_path();
    const std::wstring ext = video.extension().wstring();
    fs::path result = dir / (baseName + ext);
    std::error_code ec;
    if (!fs::exists(result, ec)) return result.wstring();
    for (int i = 1; i < 10000; ++i) {
        result = dir / (baseName + L"_" + std::to_wstring(i) + ext);
        ec.clear();
        if (!fs::exists(result, ec)) return result.wstring();
    }
    return (dir / (baseName + L"_new" + ext)).wstring();
}

MergeResult Merge(const std::wstring& video, const std::wstring& audio, std::wstring name) {
    MergeResult result;
    try {
        const fs::path videoPath(video);
        const fs::path audioPath(audio);
        if (!fs::is_regular_file(videoPath)) {
            result.message = L"找不到視訊檔。";
            return result;
        }
        if (!fs::is_regular_file(audioPath)) {
            result.message = L"找不到音訊檔。";
            return result;
        }

        name = Trim(name);
        const std::wstring ext = videoPath.extension().wstring();
        if (!ext.empty() && name.size() > ext.size()) {
            const std::wstring suffix = name.substr(name.size() - ext.size());
            if (_wcsicmp(suffix.c_str(), ext.c_str()) == 0) name = Trim(name.substr(0, name.size() - ext.size()));
        }
        if (!ValidFileName(name)) {
            result.message = L"輸出檔名無效。請勿使用 < > : \" / \\ | ? *，也不要以空白或句點結尾。";
            return result;
        }

        std::wstring ffmpeg;
        if (!EnsureFFmpeg(ffmpeg, result.message)) return result;

        result.outputPath = UniqueOutput(videoPath, name);
        const fs::path output(result.outputPath);
        std::vector<std::wstring> base = {
            L"-hide_banner", L"-loglevel", L"error", L"-y",
            L"-i", video, L"-i", audio,
            L"-map", L"0:v:0", L"-map", L"1:a:0",
            L"-map_metadata", L"0", L"-c:v", L"copy"
        };

        auto copy = base;
        copy.insert(copy.end(), {L"-c:a", L"copy", result.outputPath});
        ProcessResult first = RunFFmpeg(ffmpeg, copy);
        if (first.code == 0 && fs::exists(output)) {
            result.ok = true;
            result.message = L"合併完成。";
            return result;
        }

        std::error_code ec;
        fs::remove(output, ec);
        const std::wstring codec = CompatibleAudioCodec(ext);
        auto retry = base;
        retry.insert(retry.end(), {L"-c:a", codec, L"-b:a", L"192k"});
        if (codec == L"aac") retry.insert(retry.end(), {L"-movflags", L"+faststart"});
        retry.push_back(result.outputPath);
        ProcessResult second = RunFFmpeg(ffmpeg, retry);
        if (second.code == 0 && fs::exists(output)) {
            result.ok = true;
            result.message = L"合併完成（音訊已自動轉成相容格式）。";
            return result;
        }

        fs::remove(output, ec);
        result.outputPath.clear();
        result.message = L"合併失敗。\r\n\r\nFFmpeg 訊息：\r\n" + ErrorTail(second.text.empty() ? first.text : second.text);
        return result;
    } catch (...) {
        result.outputPath.clear();
        result.message = L"合併時發生未預期錯誤。";
        return result;
    }
}

std::wstring ChooseFile(HWND owner, const wchar_t* title, const wchar_t* filter) {
    wchar_t path[32768]{};
    OPENFILENAMEW ofn{};
    ofn.lStructSize = sizeof(ofn);
    ofn.hwndOwner = owner;
    ofn.lpstrTitle = title;
    ofn.lpstrFile = path;
    ofn.nMaxFile = static_cast<DWORD>(std::size(path));
    ofn.lpstrFilter = filter;
    ofn.nFilterIndex = 1;
    ofn.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_EXPLORER;
    return GetOpenFileNameW(&ofn) ? std::wstring(path) : L"";
}

void SetFont(HWND hwnd, HFONT font) {
    SendMessageW(hwnd, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
}

void SetBusy(bool busy) {
    g_busy = busy;
    EnableWindow(g_startButton, !busy);
    EnableWindow(g_videoEdit, !busy);
    EnableWindow(g_audioEdit, !busy);
    EnableWindow(g_nameEdit, !busy);
    EnableWindow(GetDlgItem(g_hwnd, IDC_VIDEO_BROWSE), !busy);
    EnableWindow(GetDlgItem(g_hwnd, IDC_AUDIO_BROWSE), !busy);
    ShowWindow(g_progress, busy ? SW_SHOW : SW_HIDE);
    SendMessageW(g_progress, PBM_SETMARQUEE, busy ? TRUE : FALSE, busy ? 30 : 0);
    ShowWindow(g_openFolder, (!busy && !g_lastOutput.empty()) ? SW_SHOW : SW_HIDE);
}

void StartMerge() {
    if (g_busy) return;
    const std::wstring video = Trim(GetText(g_videoEdit));
    const std::wstring audio = Trim(GetText(g_audioEdit));
    const std::wstring name = Trim(GetText(g_nameEdit));
    if (video.empty() || audio.empty() || name.empty()) {
        MessageBoxW(g_hwnd, L"請先選擇視訊、音訊，並輸入輸出檔名。", kAppTitle, MB_OK | MB_ICONINFORMATION);
        return;
    }

    g_lastOutput.clear();
    SetWindowTextW(g_status, L"正在合併…");
    SetBusy(true);
    std::thread([video, audio, name]() {
        auto* result = new MergeResult(Merge(video, audio, name));
        PostMessageW(g_hwnd, WM_MERGE_DONE, 0, reinterpret_cast<LPARAM>(result));
    }).detach();
}

void OpenOutputFolder() {
    if (g_lastOutput.empty()) return;
    const fs::path folder = fs::path(g_lastOutput).parent_path();
    ShellExecuteW(g_hwnd, L"open", folder.c_str(), nullptr, nullptr, SW_SHOWNORMAL);
}

void CreateUi(HWND hwnd) {
    constexpr DWORD labelStyle = WS_CHILD | WS_VISIBLE | SS_LEFT;
    constexpr DWORD editStyle = WS_CHILD | WS_VISIBLE | WS_TABSTOP | ES_AUTOHSCROLL;
    constexpr DWORD buttonStyle = WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_PUSHBUTTON;

    HWND title = CreateWindowExW(0, L"STATIC", L"音訊＋視訊合併", labelStyle,
                                 24, 20, 420, 34, hwnd, nullptr, nullptr, nullptr);
    SetFont(title, g_titleFont);
    HWND intro = CreateWindowExW(0, L"STATIC", L"選擇一個視訊與一個音訊，輸出會放在視訊同一個資料夾。", labelStyle,
                                 24, 58, 620, 24, hwnd, nullptr, nullptr, nullptr);
    SetFont(intro, g_font);

    HWND videoLabel = CreateWindowExW(0, L"STATIC", L"視訊檔", labelStyle,
                                      24, 100, 100, 24, hwnd, nullptr, nullptr, nullptr);
    SetFont(videoLabel, g_font);
    g_videoEdit = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"", editStyle | ES_READONLY,
                                  24, 126, 520, 31, hwnd, ControlId(IDC_VIDEO_EDIT), nullptr, nullptr);
    SetFont(g_videoEdit, g_font);
    HWND videoButton = CreateWindowExW(0, L"BUTTON", L"選擇…", buttonStyle,
                                       556, 125, 92, 33, hwnd, ControlId(IDC_VIDEO_BROWSE), nullptr, nullptr);
    SetFont(videoButton, g_font);

    HWND audioLabel = CreateWindowExW(0, L"STATIC", L"音訊檔", labelStyle,
                                      24, 174, 100, 24, hwnd, nullptr, nullptr, nullptr);
    SetFont(audioLabel, g_font);
    g_audioEdit = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"", editStyle | ES_READONLY,
                                  24, 200, 520, 31, hwnd, ControlId(IDC_AUDIO_EDIT), nullptr, nullptr);
    SetFont(g_audioEdit, g_font);
    HWND audioButton = CreateWindowExW(0, L"BUTTON", L"選擇…", buttonStyle,
                                       556, 199, 92, 33, hwnd, ControlId(IDC_AUDIO_BROWSE), nullptr, nullptr);
    SetFont(audioButton, g_font);

    HWND nameLabel = CreateWindowExW(0, L"STATIC", L"輸出檔名（不含副檔名）", labelStyle,
                                     24, 248, 260, 24, hwnd, nullptr, nullptr, nullptr);
    SetFont(nameLabel, g_font);
    g_nameEdit = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"", editStyle,
                                 24, 274, 624, 31, hwnd, ControlId(IDC_NAME_EDIT), nullptr, nullptr);
    SetFont(g_nameEdit, g_font);

    g_startButton = CreateWindowExW(0, L"BUTTON", L"開始合併", WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_DEFPUSHBUTTON,
                                    24, 331, 150, 42, hwnd, ControlId(IDC_START), nullptr, nullptr);
    SetFont(g_startButton, g_font);
    g_openFolder = CreateWindowExW(0, L"BUTTON", L"開啟輸出資料夾", buttonStyle,
                                   186, 336, 140, 34, hwnd, ControlId(IDC_OPEN_FOLDER), nullptr, nullptr);
    SetFont(g_openFolder, g_font);
    ShowWindow(g_openFolder, SW_HIDE);

    g_progress = CreateWindowExW(0, PROGRESS_CLASSW, nullptr, WS_CHILD | PBS_MARQUEE,
                                 24, 391, 624, 8, hwnd, ControlId(IDC_PROGRESS), nullptr, nullptr);
    ShowWindow(g_progress, SW_HIDE);
    g_status = CreateWindowExW(0, L"STATIC", L"就緒", labelStyle,
                               24, 414, 624, 26, hwnd, ControlId(IDC_STATUS), nullptr, nullptr);
    SetFont(g_status, g_font);
}

LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
    case WM_CREATE:
        g_hwnd = hwnd;
        CreateUi(hwnd);
        return 0;

    case WM_COMMAND: {
        const int id = LOWORD(wParam);
        if (id == IDC_VIDEO_BROWSE) {
            const wchar_t filter[] = L"視訊檔\0*.mp4;*.mkv;*.mov;*.m4v;*.avi;*.webm;*.ts;*.mts;*.m2ts\0所有檔案\0*.*\0\0";
            std::wstring path = ChooseFile(hwnd, L"選擇視訊檔", filter);
            if (!path.empty()) {
                SetWindowTextW(g_videoEdit, path.c_str());
                try {
                    SetWindowTextW(g_nameEdit, (fs::path(path).stem().wstring() + L"_合併").c_str());
                } catch (...) {}
            }
            return 0;
        }
        if (id == IDC_AUDIO_BROWSE) {
            const wchar_t filter[] = L"音訊檔\0*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.opus;*.wma\0所有檔案\0*.*\0\0";
            std::wstring path = ChooseFile(hwnd, L"選擇音訊檔", filter);
            if (!path.empty()) SetWindowTextW(g_audioEdit, path.c_str());
            return 0;
        }
        if (id == IDC_START) {
            StartMerge();
            return 0;
        }
        if (id == IDC_OPEN_FOLDER) {
            OpenOutputFolder();
            return 0;
        }
        break;
    }

    case WM_MERGE_DONE: {
        std::unique_ptr<MergeResult> result(reinterpret_cast<MergeResult*>(lParam));
        SetBusy(false);
        if (result && result->ok) {
            g_lastOutput = result->outputPath;
            SetWindowTextW(g_status, result->message.c_str());
            ShowWindow(g_openFolder, SW_SHOW);
            const std::wstring text = result->message + L"\r\n\r\n" + result->outputPath;
            MessageBoxW(hwnd, text.c_str(), kAppTitle, MB_OK | MB_ICONINFORMATION);
        } else {
            const std::wstring text = result ? result->message : L"合併失敗。";
            SetWindowTextW(g_status, L"合併失敗");
            MessageBoxW(hwnd, text.c_str(), kAppTitle, MB_OK | MB_ICONERROR);
        }
        return 0;
    }

    case WM_CTLCOLORSTATIC: {
        HDC dc = reinterpret_cast<HDC>(wParam);
        SetBkColor(dc, RGB(248, 250, 252));
        SetTextColor(dc, RGB(31, 41, 55));
        return reinterpret_cast<LRESULT>(g_bgBrush);
    }

    case WM_CLOSE:
        if (g_busy && MessageBoxW(hwnd, L"目前正在合併。確定要直接關閉程式嗎？", kAppTitle,
                                  MB_YESNO | MB_ICONWARNING | MB_DEFBUTTON2) != IDYES) {
            return 0;
        }
        DestroyWindow(hwnd);
        return 0;

    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

} // namespace

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int show) {
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    INITCOMMONCONTROLSEX controls{sizeof(controls), ICC_PROGRESS_CLASS | ICC_STANDARD_CLASSES};
    InitCommonControlsEx(&controls);

    NONCLIENTMETRICSW metrics{sizeof(metrics)};
    SystemParametersInfoW(SPI_GETNONCLIENTMETRICS, sizeof(metrics), &metrics, 0);
    wcscpy_s(metrics.lfMessageFont.lfFaceName, L"Microsoft JhengHei UI");
    metrics.lfMessageFont.lfHeight = -14;
    g_font = CreateFontIndirectW(&metrics.lfMessageFont);

    LOGFONTW titleFont = metrics.lfMessageFont;
    titleFont.lfHeight = -25;
    titleFont.lfWeight = FW_SEMIBOLD;
    g_titleFont = CreateFontIndirectW(&titleFont);
    g_bgBrush = CreateSolidBrush(RGB(248, 250, 252));

    WNDCLASSEXW wc{sizeof(wc)};
    wc.lpfnWndProc = WndProc;
    wc.hInstance = instance;
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wc.hIcon = LoadIconW(nullptr, IDI_APPLICATION);
    wc.hIconSm = LoadIconW(nullptr, IDI_APPLICATION);
    wc.hbrBackground = g_bgBrush;
    wc.lpszClassName = kClassName;
    if (!RegisterClassExW(&wc)) return 1;

    constexpr DWORD style = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX;
    RECT rect{0, 0, 692, 486};
    AdjustWindowRectEx(&rect, style, FALSE, 0);
    const int width = rect.right - rect.left;
    const int height = rect.bottom - rect.top;
    const int x = (GetSystemMetrics(SM_CXSCREEN) - width) / 2;
    const int y = (GetSystemMetrics(SM_CYSCREEN) - height) / 2;

    HWND hwnd = CreateWindowExW(0, kClassName, kAppTitle, style, x, y, width, height,
                                nullptr, nullptr, instance, nullptr);
    if (!hwnd) return 1;
    ShowWindow(hwnd, show);
    UpdateWindow(hwnd);

    MSG msg{};
    while (GetMessageW(&msg, nullptr, 0, 0) > 0) {
        if (!IsDialogMessageW(hwnd, &msg)) {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
    }

    if (g_font) DeleteObject(g_font);
    if (g_titleFont) DeleteObject(g_titleFont);
    if (g_bgBrush) DeleteObject(g_bgBrush);
    return static_cast<int>(msg.wParam);
}

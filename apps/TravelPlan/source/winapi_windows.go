//go:build windows

package main

import (
	"fmt"
	"syscall"
	"unsafe"
)

var (
	user32   = syscall.NewLazyDLL("user32.dll")
	kernel32 = syscall.NewLazyDLL("kernel32.dll")
	gdi32    = syscall.NewLazyDLL("gdi32.dll")
	comctl32 = syscall.NewLazyDLL("comctl32.dll")
	shell32  = syscall.NewLazyDLL("shell32.dll")
	uxtheme  = syscall.NewLazyDLL("uxtheme.dll")

	pRegisterClassExW     = user32.NewProc("RegisterClassExW")
	pCreateWindowExW      = user32.NewProc("CreateWindowExW")
	pDefWindowProcW       = user32.NewProc("DefWindowProcW")
	pShowWindow           = user32.NewProc("ShowWindow")
	pUpdateWindow         = user32.NewProc("UpdateWindow")
	pGetMessageW          = user32.NewProc("GetMessageW")
	pTranslateMessage     = user32.NewProc("TranslateMessage")
	pDispatchMessageW     = user32.NewProc("DispatchMessageW")
	pPostQuitMessage      = user32.NewProc("PostQuitMessage")
	pLoadCursorW          = user32.NewProc("LoadCursorW")
	pLoadImageW           = user32.NewProc("LoadImageW")
	pSendMessageW         = user32.NewProc("SendMessageW")
	pMoveWindow           = user32.NewProc("MoveWindow")
	pGetClientRect        = user32.NewProc("GetClientRect")
	pGetWindowRect        = user32.NewProc("GetWindowRect")
	pMapWindowPoints      = user32.NewProc("MapWindowPoints")
	pGetWindowTextLengthW = user32.NewProc("GetWindowTextLengthW")
	pGetWindowTextW       = user32.NewProc("GetWindowTextW")
	pSetWindowTextW       = user32.NewProc("SetWindowTextW")
	pDestroyWindow        = user32.NewProc("DestroyWindow")
	pEnableWindow         = user32.NewProc("EnableWindow")
	pSetFocus             = user32.NewProc("SetFocus")
	pGetFocus             = user32.NewProc("GetFocus")
	pMessageBoxW          = user32.NewProc("MessageBoxW")
	pCreateMenu           = user32.NewProc("CreateMenu")
	pCreatePopupMenu      = user32.NewProc("CreatePopupMenu")
	pAppendMenuW          = user32.NewProc("AppendMenuW")
	pSetMenu              = user32.NewProc("SetMenu")
	pDrawMenuBar          = user32.NewProc("DrawMenuBar")
	pSetScrollInfo        = user32.NewProc("SetScrollInfo")
	pGetScrollInfo        = user32.NewProc("GetScrollInfo")
	pSetWindowPos         = user32.NewProc("SetWindowPos")
	pSetTimer             = user32.NewProc("SetTimer")
	pKillTimer            = user32.NewProc("KillTimer")
	pInvalidateRect       = user32.NewProc("InvalidateRect")
	pRedrawWindow         = user32.NewProc("RedrawWindow")
	pGetWindowLongPtrW    = user32.NewProc("GetWindowLongPtrW")
	pSetProcessDPIAware   = user32.NewProc("SetProcessDPIAware")
	pBeginPaint           = user32.NewProc("BeginPaint")
	pEndPaint             = user32.NewProc("EndPaint")
	pFillRect             = user32.NewProc("FillRect")
	pDrawTextW            = user32.NewProc("DrawTextW")
	pGetSysColor          = user32.NewProc("GetSysColor")
	pGetSystemMetrics     = user32.NewProc("GetSystemMetrics")
	pGetDC                = user32.NewProc("GetDC")
	pReleaseDC            = user32.NewProc("ReleaseDC")

	pGetModuleHandleW      = kernel32.NewProc("GetModuleHandleW")
	pCreateMutexW          = kernel32.NewProc("CreateMutexW")
	pCloseHandle           = kernel32.NewProc("CloseHandle")
	pGetKeyState           = user32.NewProc("GetKeyState")
	pGetDlgCtrlID          = user32.NewProc("GetDlgCtrlID")
	pIsDialogMessageW      = user32.NewProc("IsDialogMessageW")
	pCreateFontW           = gdi32.NewProc("CreateFontW")
	pDeleteObject          = gdi32.NewProc("DeleteObject")
	pCreateSolidBrush      = gdi32.NewProc("CreateSolidBrush")
	pCreatePen             = gdi32.NewProc("CreatePen")
	pSelectObject          = gdi32.NewProc("SelectObject")
	pSetBkMode             = gdi32.NewProc("SetBkMode")
	pSetTextColor          = gdi32.NewProc("SetTextColor")
	pGetTextExtentPoint32W = gdi32.NewProc("GetTextExtentPoint32W")
	pRoundRect             = gdi32.NewProc("RoundRect")
	pEllipse               = gdi32.NewProc("Ellipse")
	pMoveToEx              = gdi32.NewProc("MoveToEx")
	pLineTo                = gdi32.NewProc("LineTo")
	pGetStockObject        = gdi32.NewProc("GetStockObject")
	pInitCommonControlsEx  = comctl32.NewProc("InitCommonControlsEx")
	pShellExecuteW         = shell32.NewProc("ShellExecuteW")
	pSetWindowTheme        = uxtheme.NewProc("SetWindowTheme")
	pSetWindowSubclass     = comctl32.NewProc("SetWindowSubclass")
	pDefSubclassProc       = comctl32.NewProc("DefSubclassProc")
	pRemoveWindowSubclass  = comctl32.NewProc("RemoveWindowSubclass")
)

const (
	WS_OVERLAPPEDWINDOW = 0x00CF0000
	WS_OVERLAPPED       = 0x00000000
	WS_CAPTION          = 0x00C00000
	WS_SYSMENU          = 0x00080000
	WS_CHILD            = 0x40000000
	WS_VISIBLE          = 0x10000000
	WS_TABSTOP          = 0x00010000
	WS_GROUP            = 0x00020000
	WS_POPUP            = 0x80000000
	WS_BORDER           = 0x00800000
	WS_VSCROLL          = 0x00200000
	WS_HSCROLL          = 0x00100000
	WS_CLIPCHILDREN     = 0x02000000
	WS_EX_CLIENTEDGE    = 0x00000200
	WS_EX_CONTROLPARENT = 0x00010000

	ES_LEFT        = 0x0000
	ES_MULTILINE   = 0x0004
	ES_AUTOVSCROLL = 0x0040
	ES_AUTOHSCROLL = 0x0080
	ES_WANTRETURN  = 0x1000

	BS_PUSHBUTTON      = 0x00000000
	BS_DEFPUSHBUTTON   = 0x00000001
	BS_AUTOCHECKBOX    = 0x00000003
	BS_AUTORADIOBUTTON = 0x00000009
	BS_BITMAP          = 0x00000080
	BS_FLAT            = 0x00008000
	BS_OWNERDRAW       = 0x0000000B

	CBS_DROPDOWN     = 0x0002
	CBS_DROPDOWNLIST = 0x0003
	CBS_AUTOHSCROLL  = 0x0040

	SS_LEFT        = 0x00000000
	SS_CENTER      = 0x00000001
	SS_CENTERIMAGE = 0x00000200
	SS_ETCHEDHORZ  = 0x00000010
	SS_ETCHEDVERT  = 0x00000011

	LVS_REPORT           = 0x0001
	LVS_SINGLESEL        = 0x0004
	LVS_SHOWSELALWAYS    = 0x0008
	LVS_EX_FULLROWSELECT = 0x00000020
	LVS_EX_GRIDLINES     = 0x00000001
	LVS_EX_DOUBLEBUFFER  = 0x00010000

	LVCF_FMT                     = 0x0001
	LVCF_WIDTH                   = 0x0002
	LVCF_TEXT                    = 0x0004
	LVIF_TEXT                    = 0x0001
	LVIF_PARAM                   = 0x0004
	LVM_FIRST                    = 0x1000
	LVM_INSERTCOLUMNW            = LVM_FIRST + 97
	LVM_INSERTITEMW              = LVM_FIRST + 77
	LVM_SETITEMTEXTW             = LVM_FIRST + 116
	LVM_DELETEALLITEMS           = LVM_FIRST + 9
	LVM_SETEXTENDEDLISTVIEWSTYLE = LVM_FIRST + 54
	LVM_GETHEADER                = LVM_FIRST + 31
	LVM_GETITEMCOUNT             = LVM_FIRST + 4
	LVM_GETITEMRECT              = LVM_FIRST + 14
	LVM_GETNEXTITEM              = LVM_FIRST + 12
	LVM_SETITEMSTATE             = LVM_FIRST + 43
	LVM_ENSUREVISIBLE            = LVM_FIRST + 19
	LVM_SETBKCOLOR               = LVM_FIRST + 1
	LVM_SETTEXTBKCOLOR           = LVM_FIRST + 38
	LVM_GETCOLUMNWIDTH           = LVM_FIRST + 29
	LVM_SETCOLUMNWIDTH           = LVM_FIRST + 30
	LVM_GETSUBITEMRECT           = LVM_FIRST + 56
	LVM_SUBITEMHITTEST           = LVM_FIRST + 57
	LVNI_SELECTED                = 0x0002
	LVIR_BOUNDS                  = 0
	LVIS_SELECTED                = 0x0002
	LVIS_FOCUSED                 = 0x0001

	WM_CREATE         = 0x0001
	WM_PAINT          = 0x000F
	WM_ERASEBKGND     = 0x0014
	WM_DRAWITEM       = 0x002B
	WM_CTLCOLORSTATIC = 0x0138
	WM_CTLCOLOREDIT   = 0x0133
	WM_DESTROY        = 0x0002
	WM_SIZE           = 0x0005
	WM_SETFOCUS       = 0x0007
	WM_COMMAND        = 0x0111
	WM_NOTIFY         = 0x004E
	WM_VSCROLL        = 0x0115
	WM_MOUSEWHEEL     = 0x020A
	WM_TIMER          = 0x0113
	WM_CLOSE          = 0x0010
	WM_SETFONT        = 0x0030
	WM_SETREDRAW      = 0x000B
	WM_SETICON        = 0x0080
	WM_KEYDOWN        = 0x0100

	SWP_NOSIZE     = 0x0001
	SWP_NOMOVE     = 0x0002
	SWP_NOACTIVATE = 0x0010
	SWP_SHOWWINDOW = 0x0040

	RDW_INVALIDATE  = 0x0001
	RDW_ERASE       = 0x0004
	RDW_ALLCHILDREN = 0x0080
	RDW_UPDATENOW   = 0x0100
	RDW_FRAME       = 0x0400

	EN_CHANGE      = 0x0300
	EN_KILLFOCUS   = 0x0200
	BN_CLICKED     = 0
	CBN_SELCHANGE  = 1
	CBN_EDITCHANGE = 5
	CBN_KILLFOCUS  = 4

	NM_FIRST           = ^uint32(0) // not used directly
	LVN_FIRST          = -100
	LVN_ITEMCHANGED    = LVN_FIRST - 1
	NM_CLICK           = -2
	NM_DBLCLK          = -3
	NM_CUSTOMDRAW      = -12
	DTN_FIRST          = -760
	DTN_DATETIMECHANGE = DTN_FIRST + 1

	SW_HIDE       = 0
	SW_SHOW       = 5
	SW_SHOWNORMAL = 1
	CW_USEDEFAULT = 0x80000000

	SM_CYHSCROLL = 3

	COLOR_WINDOW        = 5
	COLOR_WINDOWTEXT    = 8
	COLOR_HIGHLIGHT     = 13
	COLOR_HIGHLIGHTTEXT = 14
	COLOR_BTNFACE       = 15
	COLOR_BTNSHADOW     = 16
	COLOR_GRAYTEXT      = 17
	COLOR_BTNTEXT       = 18
	COLOR_BTNHIGHLIGHT  = 20
	COLOR_3DDKSHADOW    = 21
	COLOR_3DLIGHT       = 22
	IDC_ARROW           = 32512
	IMAGE_BITMAP        = 0
	IMAGE_ICON          = 1
	LR_LOADFROMFILE     = 0x0010
	LR_CREATEDIBSECTION = 0x2000
	LR_DEFAULTSIZE      = 0x0040
	ICON_SMALL          = 0
	ICON_BIG            = 1

	MB_OK              = 0x00000000
	MB_OKCANCEL        = 0x00000001
	MB_YESNOCANCEL     = 0x00000003
	MB_YESNO           = 0x00000004
	MB_ICONINFORMATION = 0x00000040
	MB_ICONWARNING     = 0x00000030
	MB_ICONERROR       = 0x00000010
	MB_ICONQUESTION    = 0x00000020
	IDOK               = 1
	IDCANCEL           = 2
	IDYES              = 6
	IDNO               = 7

	MF_STRING    = 0x00000000
	MF_POPUP     = 0x00000010
	MF_SEPARATOR = 0x00000800
	MF_GRAYED    = 0x00000001

	SB_HORZ          = 0
	SB_VERT          = 1
	SB_LINEUP        = 0
	SB_LINEDOWN      = 1
	SB_PAGEUP        = 2
	SB_PAGEDOWN      = 3
	SB_THUMBPOSITION = 4
	SB_THUMBTRACK    = 5
	SB_TOP           = 6
	SB_BOTTOM        = 7
	SIF_RANGE        = 0x0001
	SIF_PAGE         = 0x0002
	SIF_POS          = 0x0004
	SIF_TRACKPOS     = 0x0010
	SIF_ALL          = SIF_RANGE | SIF_PAGE | SIF_POS | SIF_TRACKPOS

	SWP_NOZORDER = 0x0004

	VK_TAB    = 0x09
	VK_RETURN = 0x0D
	VK_ESCAPE = 0x1B
	VK_SHIFT  = 0x10

	EM_LIMITTEXT    = 0x00C5
	EM_SETCUEBANNER = 0x1501
	BM_GETCHECK     = 0x00F0
	BM_SETIMAGE     = 0x00F7
	BM_SETCHECK     = 0x00F1
	BST_CHECKED     = 1
	BST_UNCHECKED   = 0

	CB_RESETCONTENT = 0x014B
	CB_ADDSTRING    = 0x0143
	CB_SETCURSEL    = 0x014E
	CB_GETCURSEL    = 0x0147
	CB_GETLBTEXT    = 0x0148

	DTS_SHORTDATEFORMAT = 0x0000
	DTS_SHOWNONE        = 0x0002
	DTM_FIRST           = 0x1000
	DTM_GETSYSTEMTIME   = DTM_FIRST + 1
	DTM_SETSYSTEMTIME   = DTM_FIRST + 2
	DTM_SETFORMATW      = DTM_FIRST + 50
	GDT_VALID           = 0
	GDT_NONE            = 1

	TTS_ALWAYSTIP      = 0x01
	TTS_NOPREFIX       = 0x02
	TTF_IDISHWND       = 0x0001
	TTF_SUBCLASS       = 0x0010
	TTM_ADDTOOLW       = 0x0432
	TTM_SETMAXTIPWIDTH = 0x0418

	ICC_LISTVIEW_CLASSES = 0x00000001
	ICC_STANDARD_CLASSES = 0x00004000
	ICC_DATE_CLASSES     = 0x00000100

	TRANSPARENT            = 1
	PS_SOLID               = 0
	NULL_BRUSH             = 5
	DT_LEFT                = 0x0000
	DT_CENTER              = 0x0001
	DT_VCENTER             = 0x0004
	DT_WORDBREAK           = 0x0010
	DT_SINGLELINE          = 0x0020
	DT_END_ELLIPSIS        = 0x8000
	DT_NOPREFIX            = 0x0800
	CDDS_PREPAINT          = 0x00000001
	CDDS_POSTPAINT         = 0x00000002
	CDDS_ITEMPREPAINT      = 0x00010001
	CDDS_SUBITEM           = 0x00020000
	CDRF_DODEFAULT         = 0x00000000
	CDRF_SKIPDEFAULT       = 0x00000004
	CDRF_NOTIFYPOSTPAINT   = 0x00000010
	CDRF_NEWFONT           = 0x00000002
	CDRF_NOTIFYITEMDRAW    = 0x00000020
	CDRF_NOTIFYSUBITEMDRAW = 0x00000020
	CDIS_SELECTED          = 0x0001
	ODS_SELECTED           = 0x0001
	ODS_DISABLED           = 0x0004
)

type SYSTEMTIME struct {
	Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds uint16
}
type POINT struct{ X, Y int32 }
type MINMAXINFO struct {
	Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize POINT
}
type SIZE struct{ Cx, Cy int32 }
type LVHITTESTINFO struct {
	Pt       POINT
	Flags    uint32
	IItem    int32
	ISubItem int32
	IGroup   int32
}
type MSG struct {
	Hwnd           uintptr
	Message        uint32
	WParam, LParam uintptr
	Time           uint32
	Pt             POINT
	LPrivate       uint32
}
type RECT struct{ Left, Top, Right, Bottom int32 }
type PAINTSTRUCT struct {
	Hdc         uintptr
	Erase       int32
	RcPaint     RECT
	Restore     int32
	IncUpdate   int32
	RgbReserved [32]byte
}
type DRAWITEMSTRUCT struct {
	CtlType    uint32
	CtlID      uint32
	ItemID     uint32
	ItemAction uint32
	ItemState  uint32
	HwndItem   uintptr
	HDC        uintptr
	RcItem     RECT
	ItemData   uintptr
}
type NMCUSTOMDRAW struct {
	Hdr         NMHDR
	DwDrawStage uint32
	Hdc         uintptr
	Rc          RECT
	DwItemSpec  uintptr
	UItemState  uint32
	LItemlParam uintptr
}
type NMLVCUSTOMDRAW struct {
	Nmcd        NMCUSTOMDRAW
	ClrText     uint32
	ClrTextBk   uint32
	ISubItem    int32
	DwItemType  uint32
	ClrFace     uint32
	IIconEffect int32
	IIconPhase  int32
	IPartId     int32
	IStateId    int32
	RcText      RECT
	UAlign      uint32
}
type WNDCLASSEX struct {
	CbSize                                   uint32
	Style                                    uint32
	LpfnWndProc                              uintptr
	CbClsExtra, CbWndExtra                   int32
	HInstance, HIcon, HCursor, HbrBackground uintptr
	LpszMenuName, LpszClassName              *uint16
	HIconSm                                  uintptr
}
type INITCOMMONCONTROLSEX struct{ DwSize, DwICC uint32 }
type NMHDR struct {
	HwndFrom uintptr
	IdFrom   uintptr
	Code     int32
}
type NMLISTVIEW struct {
	Hdr                            NMHDR
	IItem, ISubItem                int32
	UNewState, UOldState, UChanged uint32
	PtAction                       POINT
	LParam                         uintptr
}
type LVCOLUMN struct {
	Mask                      uint32
	Fmt                       int32
	Cx                        int32
	PszText                   *uint16
	CchTextMax                int32
	ISubItem                  int32
	IImage                    int32
	IOrder                    int32
	CxMin, CxDefault, CxIdeal int32
}
type LVITEM struct {
	Mask             uint32
	IItem, ISubItem  int32
	State, StateMask uint32
	PszText          *uint16
	CchTextMax       int32
	IImage           int32
	LParam           uintptr
	IIndent          int32
	IGroupId         int32
	CColumns         uint32
	PuColumns        *uint32
	PiColFmt         *int32
	IGroup           int32
}
type SCROLLINFO struct {
	CbSize          uint32
	FMask           uint32
	NMin, NMax      int32
	NPage           uint32
	NPos, NTrackPos int32
}
type TOOLINFO struct {
	CbSize     uint32
	UFlags     uint32
	Hwnd       uintptr
	UId        uintptr
	Rect       RECT
	Hinst      uintptr
	LpszText   *uint16
	LParam     uintptr
	LpReserved uintptr
}

func rgb(r, g, b byte) uint32        { return uint32(r) | uint32(g)<<8 | uint32(b)<<16 }
func makeBrush(color uint32) uintptr { r, _, _ := pCreateSolidBrush.Call(uintptr(color)); return r }
func makePen(color uint32, width int) uintptr {
	r, _, _ := pCreatePen.Call(PS_SOLID, uintptr(width), uintptr(color))
	return r
}
func selectObj(hdc, obj uintptr) uintptr { r, _, _ := pSelectObject.Call(hdc, obj); return r }
func fillRectColor(hdc uintptr, r RECT, color uint32) {
	b := makeBrush(color)
	if b != 0 {
		pFillRect.Call(hdc, uintptr(unsafe.Pointer(&r)), b)
		pDeleteObject.Call(b)
	}
}
func drawRoundRect(hdc uintptr, r RECT, radius int, fill, border uint32) {
	b := makeBrush(fill)
	p := makePen(border, 1)
	oldB := selectObj(hdc, b)
	oldP := selectObj(hdc, p)
	pRoundRect.Call(hdc, uintptr(r.Left), uintptr(r.Top), uintptr(r.Right), uintptr(r.Bottom), uintptr(radius), uintptr(radius))
	selectObj(hdc, oldB)
	selectObj(hdc, oldP)
	if b != 0 {
		pDeleteObject.Call(b)
	}
	if p != 0 {
		pDeleteObject.Call(p)
	}
}

func drawLine(hdc uintptr, x1, y1, x2, y2 int, color uint32, width int) {
	p := makePen(color, width)
	if p == 0 {
		return
	}
	old := selectObj(hdc, p)
	pMoveToEx.Call(hdc, uintptr(x1), uintptr(y1), 0)
	pLineTo.Call(hdc, uintptr(x2), uintptr(y2))
	selectObj(hdc, old)
	pDeleteObject.Call(p)
}
func drawEllipse(hdc uintptr, r RECT, fill, border uint32, borderWidth int) {
	b := makeBrush(fill)
	p := makePen(border, borderWidth)
	oldB := selectObj(hdc, b)
	oldP := selectObj(hdc, p)
	pEllipse.Call(hdc, uintptr(r.Left), uintptr(r.Top), uintptr(r.Right), uintptr(r.Bottom))
	selectObj(hdc, oldB)
	selectObj(hdc, oldP)
	if b != 0 {
		pDeleteObject.Call(b)
	}
	if p != 0 {
		pDeleteObject.Call(p)
	}
}

func drawText(hdc uintptr, text string, r RECT, font uintptr, color uint32, flags uint32) {
	old := selectObj(hdc, font)
	pSetBkMode.Call(hdc, TRANSPARENT)
	pSetTextColor.Call(hdc, uintptr(color))
	ws, _ := syscall.UTF16FromString(text)
	if len(ws) > 0 {
		pDrawTextW.Call(hdc, uintptr(unsafe.Pointer(&ws[0])), uintptr(len(ws)-1), uintptr(unsafe.Pointer(&r)), uintptr(flags))
	}
	selectObj(hdc, old)
}

func wstr(s string) *uint16        { p, _ := syscall.UTF16PtrFromString(s); return p }
func loword(v uintptr) uint16      { return uint16(v & 0xffff) }
func hiword(v uintptr) uint16      { return uint16((v >> 16) & 0xffff) }
func signedHiword(v uintptr) int16 { return int16(hiword(v)) }

func createWindow(ex uint32, class, text string, style uint32, x, y, w, h int, parent uintptr, id uintptr, inst uintptr) uintptr {
	r, _, _ := pCreateWindowExW.Call(uintptr(ex), uintptr(unsafe.Pointer(wstr(class))), uintptr(unsafe.Pointer(wstr(text))), uintptr(style), uintptr(x), uintptr(y), uintptr(w), uintptr(h), parent, id, inst, 0)
	return r
}
func send(hwnd uintptr, msg uint32, w, l uintptr) uintptr {
	r, _, _ := pSendMessageW.Call(hwnd, uintptr(msg), w, l)
	return r
}
func setText(hwnd uintptr, s string) { pSetWindowTextW.Call(hwnd, uintptr(unsafe.Pointer(wstr(s)))) }
func getText(hwnd uintptr) string {
	n, _, _ := pGetWindowTextLengthW.Call(hwnd)
	if n == 0 {
		return ""
	}
	b := make([]uint16, n+1)
	pGetWindowTextW.Call(hwnd, uintptr(unsafe.Pointer(&b[0])), n+1)
	return syscall.UTF16ToString(b)
}
func move(hwnd uintptr, x, y, w, h int) {
	pMoveWindow.Call(hwnd, uintptr(x), uintptr(y), uintptr(w), uintptr(h), 1)
}
func msgBox(hwnd uintptr, text, title string, flags uint32) int {
	r, _, _ := pMessageBoxW.Call(hwnd, uintptr(unsafe.Pointer(wstr(text))), uintptr(unsafe.Pointer(wstr(title))), uintptr(flags))
	return int(r)
}
func destroy(hwnd uintptr) {
	if hwnd != 0 {
		pDestroyWindow.Call(hwnd)
	}
}
func enable(hwnd uintptr, on bool) {
	v := uintptr(0)
	if on {
		v = 1
	}
	pEnableWindow.Call(hwnd, v)
}
func setFont(hwnd, font uintptr) { send(hwnd, WM_SETFONT, font, 1) }
func getClient(hwnd uintptr) RECT {
	var r RECT
	pGetClientRect.Call(hwnd, uintptr(unsafe.Pointer(&r)))
	return r
}
func loadIconFile(path string, cx, cy int) uintptr {
	r, _, _ := pLoadImageW.Call(0, uintptr(unsafe.Pointer(wstr(path))), IMAGE_ICON, uintptr(cx), uintptr(cy), LR_LOADFROMFILE)
	return r
}
func loadBitmapFile(path string, cx, cy int) uintptr {
	r, _, _ := pLoadImageW.Call(0, uintptr(unsafe.Pointer(wstr(path))), IMAGE_BITMAP, uintptr(cx), uintptr(cy), LR_LOADFROMFILE|LR_CREATEDIBSECTION)
	return r
}
func setButtonBitmap(hwnd, bmp uintptr) {
	if hwnd != 0 && bmp != 0 {
		send(hwnd, BM_SETIMAGE, IMAGE_BITMAP, bmp)
	}
}
func setCueBanner(hwnd uintptr, text string) {
	if hwnd != 0 {
		send(hwnd, EM_SETCUEBANNER, 1, uintptr(unsafe.Pointer(wstr(text))))
	}
}
func setWindowTheme(hwnd uintptr, theme string) {
	if hwnd == 0 {
		return
	}
	pSetWindowTheme.Call(hwnd, uintptr(unsafe.Pointer(wstr(theme))), 0)
}
func setCheck(hwnd uintptr, checked bool) {
	v := uintptr(BST_UNCHECKED)
	if checked {
		v = BST_CHECKED
	}
	send(hwnd, BM_SETCHECK, v, 0)
}
func isChecked(hwnd uintptr) bool {
	return send(hwnd, BM_GETCHECK, 0, 0) == BST_CHECKED
}
func comboText(hwnd uintptr) string { return getText(hwnd) }
func setDatePickerFormat(hwnd uintptr, format string) {
	if hwnd != 0 {
		send(hwnd, DTM_SETFORMATW, 0, uintptr(unsafe.Pointer(wstr(format))))
	}
}
func setDatePickerNone(hwnd uintptr) {
	if hwnd != 0 {
		send(hwnd, DTM_SETSYSTEMTIME, GDT_NONE, 0)
	}
}
func setDatePickerISO(hwnd uintptr, iso string) {
	if hwnd == 0 || iso == "" {
		setDatePickerNone(hwnd)
		return
	}
	var y, m, d int
	if _, err := fmt.Sscanf(iso, "%d-%d-%d", &y, &m, &d); err != nil {
		setDatePickerNone(hwnd)
		return
	}
	st := SYSTEMTIME{Year: uint16(y), Month: uint16(m), Day: uint16(d)}
	send(hwnd, DTM_SETSYSTEMTIME, GDT_VALID, uintptr(unsafe.Pointer(&st)))
}
func getDatePickerISO(hwnd uintptr) string {
	if hwnd == 0 {
		return ""
	}
	var st SYSTEMTIME
	r := send(hwnd, DTM_GETSYSTEMTIME, 0, uintptr(unsafe.Pointer(&st)))
	if r == GDT_NONE {
		return ""
	}
	return fmt.Sprintf("%04d-%02d-%02d", st.Year, st.Month, st.Day)
}
func shellOpen(path string) {
	pShellExecuteW.Call(0, uintptr(unsafe.Pointer(wstr("open"))), uintptr(unsafe.Pointer(wstr(path))), 0, 0, SW_SHOWNORMAL)
}
func addTooltip(parent, target uintptr, text string, inst uintptr) uintptr {
	tip := createWindow(0, "tooltips_class32", "", WS_POPUP|TTS_ALWAYSTIP|TTS_NOPREFIX, 0, 0, 0, 0, parent, 0, inst)
	if tip == 0 {
		return 0
	}
	ti := TOOLINFO{CbSize: uint32(unsafe.Sizeof(TOOLINFO{})), UFlags: TTF_IDISHWND | TTF_SUBCLASS, Hwnd: parent, UId: target, LpszText: wstr(text)}
	send(tip, TTM_ADDTOOLW, 0, uintptr(unsafe.Pointer(&ti)))
	send(tip, TTM_SETMAXTIPWIDTH, 0, 320)
	return tip
}

func setScroll(hwnd uintptr, max int, page int, pos int) {
	if max < 0 {
		max = 0
	}
	if pos < 0 {
		pos = 0
	}
	if pos > max {
		pos = max
	}
	// SetScrollInfo expects NMax to describe the full scrollable range, not the maximum thumb position.
	// Effective max position becomes NMax-NPage+1, so include the viewport page in NMax.
	rangeMax := max
	if page > 0 {
		rangeMax = max + page - 1
	}
	si := SCROLLINFO{CbSize: uint32(unsafe.Sizeof(SCROLLINFO{})), FMask: SIF_RANGE | SIF_PAGE | SIF_POS, NMin: 0, NMax: int32(rangeMax), NPage: uint32(page), NPos: int32(pos)}
	pSetScrollInfo.Call(hwnd, SB_VERT, uintptr(unsafe.Pointer(&si)), 1)
}
func getScroll(hwnd uintptr) SCROLLINFO {
	si := SCROLLINFO{CbSize: uint32(unsafe.Sizeof(SCROLLINFO{})), FMask: SIF_ALL}
	pGetScrollInfo.Call(hwnd, SB_VERT, uintptr(unsafe.Pointer(&si)))
	return si
}

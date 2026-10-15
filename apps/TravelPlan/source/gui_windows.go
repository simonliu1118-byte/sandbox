//go:build windows

package main

import (
	"crypto/sha256"
	"fmt"
	"log"
	"os"
	"path/filepath"
	"runtime"
	"runtime/debug"
	"sort"
	"strconv"
	"strings"
	"syscall"
	"time"
	"unsafe"
)

const (
	rootClass     = "TravelPlanDesktopWindow"
	viewportClass = "TravelPlanViewportWindow"
	formClass     = "TravelPlanFormContentWindow"
	settingsClass = "TravelPlanSettingsWindow"
	leftWidth     = 410
	toolbarH      = 58

	ID_NEW_TRIP     = 101
	ID_DELETE_TRIP  = 102
	ID_BACKUP       = 103
	ID_EXPORT       = 104
	ID_GSYNC        = 105
	ID_SETTINGS     = 106
	ID_EXIT         = 107
	ID_OPEN_BACKUP  = 108
	ID_OPEN_EXPORTS = 109
	ID_OPEN_LOG     = 110
	ID_ABOUT        = 111
	ID_GLOGIN       = 112
	ID_GLOGOUT      = 113
	ID_SEARCH_INPUT = 150
	ID_TRIPLIST     = 151
	ID_SEARCH_START = 152
	ID_SEARCH_END   = 153
	ID_SEARCH_GO    = 154
	ID_MODIFY_TRIP  = 155
	ID_SAVE_TRIP    = 156

	ID_SETTINGS_DAY              = 301
	ID_SETTINGS_WEEK             = 302
	ID_SETTINGS_YEAR             = 303
	ID_SETTINGS_WORD_PAGE        = 304
	ID_SETTINGS_WORD_UNDATED     = 305
	ID_SETTINGS_WORD_BLANK_MEALS = 306
	ID_SETTINGS_SAVE             = 307
	ID_SETTINGS_CANCEL           = 308
)

type ctrlPos struct {
	hwnd       uintptr
	x, y, w, h int
}

type drawLabelItem struct {
	text       string
	x, y, w, h int
	font       uintptr
	color      uint32
	flags      uint32
}

type drawCardItem struct {
	x, y, w, h int
	accent     uint32
}

type drawSubCardItem struct {
	x, y, w, h int
}
type drawFrameItem struct {
	x, y, w, h int
}

type drawGridItem struct {
	x, y, w, headerH, rowH, rows int
	cols                         []int
}

type drawInputItem struct {
	hwnd       uintptr
	x, y, w, h int
}

type listBinding struct {
	kind            string
	x, y, w, h      int
	tripID, groupID string
	planID          string
	rowIDs          []string
	columns         []string
}

type pooledCtrl struct {
	hwnd uintptr
	id   uint16
	kind string
}
type editBinding struct {
	change func(string)
	blur   func(string)
}
type comboBinding struct {
	fn       func(string)
	blurOnly bool
}
type dateBinding struct{ fn func(string) }

type uiMode int

const (
	modeNoSelection uiMode = iota
	modeView
	modeNew
	modeEdit
)

func (m uiMode) String() string {
	switch m {
	case modeView:
		return "view"
	case modeNew:
		return "new"
	case modeEdit:
		return "edit"
	default:
		return "no-selection"
	}
}

type UI struct {
	store                                                                                       *Server
	inst                                                                                        uintptr
	hwnd                                                                                        uintptr
	right                                                                                       uintptr
	content                                                                                     uintptr
	emptyPane, viewPane, editPane                                                               uintptr
	search                                                                                      uintptr
	searchStart                                                                                 uintptr
	searchEnd                                                                                   uintptr
	searchButton                                                                                uintptr
	searchSep                                                                                   uintptr
	tripList                                                                                    uintptr
	font, fontInput, fontHint, fontBold, fontSmall, fontSection, fontTitle, fontIcon, fontEmoji uintptr
	iconBig, iconSmall, emptyBmp                                                                uintptr
	controlID                                                                                   uint16
	controls                                                                                    []ctrlPos
	editBindings                                                                                map[uint16]editBinding
	comboBindings                                                                               map[uint16]comboBinding
	buttonActions                                                                               map[uint16]func()
	dateBindings                                                                                map[uint16]dateBinding
	buttonStyles                                                                                map[uint16]string
	drawLabels                                                                                  []drawLabelItem
	drawCards                                                                                   []drawCardItem
	drawSubCards                                                                                []drawSubCardItem
	drawFrames                                                                                  []drawFrameItem
	drawGrids                                                                                   []drawGridItem
	drawInputs                                                                                  []drawInputItem
	listBindings                                                                                map[uint16]listBinding
	openCard                                                                                    int
	visibleTripIDs                                                                              []string
	collapsed                                                                                   map[string]bool
	scrollY                                                                                     int
	contentH                                                                                    int
	status                                                                                      uintptr
	statusTimer                                                                                 uintptr
	searchQueryApplied                                                                          string
	searchStartApplied                                                                          string
	searchEndApplied                                                                            string
	lastRightWidth                                                                              int
	rendering                                                                                   bool
	controlPool                                                                                 map[string][]pooledCtrl
	poolUsed                                                                                    map[string]int
	poolCreatedPass                                                                             int
	poolReusedPass                                                                              int
	lastHeartbeat                                                                               time.Time
	mode                                                                                        uiMode
	editing                                                                                     bool
	dirty                                                                                       bool
	selectedTripID                                                                              string
	workingTrip                                                                                 *Trip
	suppressTripSelect                                                                          bool
	btnNew, btnModify, btnSave, btnDelete, btnExport                                            uintptr
	brushWindow, brushCard                                                                      uintptr
	drawEmptyArt                                                                                bool
	planFrameActive                                                                             bool
	panePools                                                                                   map[uintptr]map[string][]pooledCtrl
	panePoolUsed                                                                                map[uintptr]map[string]int
	searchFrame                                                                                 uintptr
	editingTravelerID                                                                           string
	editingMealName                                                                             string
	pendingFocus                                                                                uintptr
	settlingFocus                                                                               bool
	openingCell                                                                                 bool
	cellEditor                                                                                  pooledCtrl
	noteKeys                                                                                    map[uintptr]func(int)
}

var ui *UI
var rootWndProcCB = syscall.NewCallback(rootWndProc)
var viewportWndProcCB = syscall.NewCallback(viewportWndProc)
var formWndProcCB = syscall.NewCallback(formWndProc)
var settingsWndProcCB = syscall.NewCallback(settingsWndProc)
var editSubclassProcCB = syscall.NewCallback(editSubclassProc)
var headerSubclassProcCB = syscall.NewCallback(headerSubclassProc)

type settingsUI struct {
	hwnd, parent                          uintptr
	day, week, year                       uintptr
	wordPage, wordUndated, wordBlankMeals uintptr
}

var settingsWin *settingsUI

const (
	paletteSystem = "system"
	paletteWarm   = "warm"
	paletteLight  = "light"
)

var (
	// V0.8.1 defaults to Windows system colors.  The warm palette remains in
	// applyPalette so we can switch back without reworking individual controls.
	activePalette  = paletteSystem
	clrCanvas      uint32
	clrCard        uint32
	clrInput       uint32
	clrListBase    uint32
	clrCardBorder  uint32
	clrText        uint32
	clrMuted       uint32
	clrCoral       = rgb(231, 117, 88)
	clrCoralDark   = rgb(205, 94, 67)
	clrCoralSoft   = rgb(253, 238, 232)
	clrMint        = rgb(108, 163, 137)
	clrMintSoft    = rgb(235, 246, 240)
	clrGridAlt     uint32
	clrGridSel     uint32
	clrGridSelText uint32
	clrGridLine    uint32
	clrDanger      = rgb(210, 83, 83)
	clrDangerSoft  = rgb(253, 238, 238)
)

func sysColor(index int) uint32 {
	r, _, _ := pGetSysColor.Call(uintptr(index))
	return uint32(r)
}

func applyPalette(mode string) {
	activePalette = mode
	if mode == paletteLight {
		clrCanvas = rgb(244, 246, 248)
		clrCard = rgb(255, 255, 255)
		clrInput = rgb(255, 255, 255)
		clrListBase = rgb(255, 255, 255)
		clrCardBorder = rgb(215, 222, 228)
		clrText = rgb(35, 44, 54)
		clrMuted = rgb(102, 114, 127)
		clrGridAlt = rgb(246, 249, 251)
		clrGridSel = rgb(228, 240, 249)
		clrGridSelText = clrText
		clrGridLine = rgb(222, 228, 234)
		return
	}
	if mode == paletteWarm {
		clrCanvas = rgb(248, 246, 242)
		clrCard = rgb(255, 255, 255)
		clrInput = rgb(255, 255, 255)
		clrListBase = rgb(255, 255, 255)
		clrCardBorder = rgb(229, 222, 214)
		clrText = rgb(52, 48, 45)
		clrMuted = rgb(118, 110, 103)
		clrGridAlt = rgb(239, 246, 249)
		clrGridSel = rgb(254, 232, 222)
		clrGridSelText = clrText
		clrGridLine = rgb(219, 228, 233)
		return
	}

	// Native system-color experiment.  Accent colors stay TravelPlan-specific,
	// but all base surfaces/text/grid colors come from Windows.
	clrCanvas = sysColor(COLOR_BTNFACE)
	clrCard = rgb(248, 246, 242)
	clrInput = sysColor(COLOR_WINDOW)
	clrListBase = sysColor(COLOR_WINDOW)
	clrCardBorder = sysColor(COLOR_BTNSHADOW)
	clrText = sysColor(COLOR_WINDOWTEXT)
	clrMuted = sysColor(COLOR_GRAYTEXT)
	clrGridAlt = rgb(239, 246, 249)
	clrGridSel = sysColor(COLOR_HIGHLIGHT)
	clrGridSelText = sysColor(COLOR_HIGHLIGHTTEXT)
	clrGridLine = sysColor(COLOR_BTNSHADOW)
}

func uiLog(format string, args ...any) {
	if ui != nil && ui.store != nil {
		ui.store.logf(format, args...)
	}
}

func recoverWndProc(where string, hwnd uintptr, msg uint32, ret *uintptr) {
	if v := recover(); v != nil {
		if ui != nil && ui.store != nil {
			ui.store.logf("PANIC callback=%s hwnd=0x%x msg=0x%x value=%v\n%s", where, hwnd, msg, v, debug.Stack())
		}
		r, _, _ := pDefWindowProcW.Call(hwnd, uintptr(msg), 0, 0)
		*ret = r
	}
}

func (u *UI) paintForm(hdc uintptr, rc RECT) {
	fillRectColor(hdc, rc, clrCanvas)
	if u.drawEmptyArt {
		u.drawTravelIllustration(hdc, rc)
	}
	for _, f := range u.drawFrames {
		r := RECT{int32(f.x), int32(f.y), int32(f.x + f.w), int32(f.y + f.h)}
		drawRoundRect(hdc, r, 14, clrCanvas, clrCardBorder)
	}
	for _, c := range u.drawCards {
		r := RECT{int32(c.x), int32(c.y), int32(c.x + c.w), int32(c.y + c.h)}
		drawRoundRect(hdc, r, 16, clrCard, clrCardBorder)
		// Small warm accent keeps the visual language travel-oriented without using corporate blue.
		ar := RECT{int32(c.x + 18), int32(c.y + 17), int32(c.x + 23), int32(c.y + 39)}
		drawRoundRect(hdc, ar, 5, c.accent, c.accent)
	}
	for _, c := range u.drawSubCards {
		r := RECT{int32(c.x), int32(c.y), int32(c.x + c.w), int32(c.y + c.h)}
		drawRoundRect(hdc, r, 12, clrCard, clrCardBorder)
	}
	for _, g := range u.drawGrids {
		// True grid table: painted header band, zebra rows and one-pixel cell borders.
		fillRectColor(hdc, RECT{int32(g.x), int32(g.y), int32(g.x + g.w), int32(g.y + g.headerH)}, clrCanvas)
		for r := 0; r < g.rows; r++ {
			if r%2 == 1 {
				y0 := g.y + g.headerH + r*g.rowH
				fillRectColor(hdc, RECT{int32(g.x + 1), int32(y0 + 1), int32(g.x + g.w - 1), int32(y0 + g.rowH)}, clrGridAlt)
			}
		}
		totalH := g.headerH + g.rows*g.rowH
		line := clrGridLine
		fillRectColor(hdc, RECT{int32(g.x), int32(g.y), int32(g.x + g.w), int32(g.y + 1)}, line)
		fillRectColor(hdc, RECT{int32(g.x), int32(g.y + totalH - 1), int32(g.x + g.w), int32(g.y + totalH)}, line)
		fillRectColor(hdc, RECT{int32(g.x), int32(g.y), int32(g.x + 1), int32(g.y + totalH)}, line)
		fillRectColor(hdc, RECT{int32(g.x + g.w - 1), int32(g.y), int32(g.x + g.w), int32(g.y + totalH)}, line)
		fillRectColor(hdc, RECT{int32(g.x), int32(g.y + g.headerH - 1), int32(g.x + g.w), int32(g.y + g.headerH)}, line)
		for r := 1; r < g.rows; r++ {
			y0 := g.y + g.headerH + r*g.rowH
			fillRectColor(hdc, RECT{int32(g.x), int32(y0), int32(g.x + g.w), int32(y0 + 1)}, line)
		}
		x0 := g.x
		for _, cw := range g.cols[:len(g.cols)-1] {
			x0 += cw
			fillRectColor(hdc, RECT{int32(x0), int32(g.y), int32(x0 + 1), int32(g.y + totalH)}, line)
		}
	}
	for _, in := range u.drawInputs {
		r := RECT{int32(in.x), int32(in.y), int32(in.x + in.w), int32(in.y + in.h)}
		border := clrCardBorder
		if f, _, _ := pGetFocus.Call(); f == in.hwnd {
			border = clrCoral
		}
		drawRoundRect(hdc, r, 5, clrInput, border)
	}
	for _, t := range u.drawLabels {
		r := RECT{int32(t.x), int32(t.y), int32(t.x + t.w), int32(t.y + t.h)}
		drawText(hdc, t.text, r, t.font, t.color, t.flags)
	}
}

func (u *UI) drawTravelIllustration(hdc uintptr, rc RECT) {
	w := int(rc.Right - rc.Left)
	if w < 520 {
		w = 520
	}
	cx := w / 2
	top := 150

	// Soft background halo.
	drawEllipse(hdc, RECT{int32(cx - 175), int32(top - 45), int32(cx + 175), int32(top + 305)}, clrCoralSoft, clrCoralSoft, 1)

	// Folded map / itinerary sheet.
	mapR := RECT{int32(cx - 145), int32(top + 20), int32(cx + 115), int32(top + 225)}
	drawRoundRect(hdc, mapR, 18, clrCard, rgb(221, 211, 201))
	drawLine(hdc, cx-58, top+28, cx-58, top+216, rgb(224, 215, 206), 1)
	drawLine(hdc, cx+28, top+28, cx+28, top+216, rgb(224, 215, 206), 1)

	// Route points and a deliberately simple dotted path.
	drawEllipse(hdc, RECT{int32(cx - 112), int32(top + 68), int32(cx - 94), int32(top + 86)}, clrCoral, clrCoral, 1)
	drawEllipse(hdc, RECT{int32(cx + 69), int32(top + 159), int32(cx + 87), int32(top + 177)}, clrMint, clrMint, 1)
	pts := [][2]int{{cx - 91, top + 84}, {cx - 68, top + 94}, {cx - 43, top + 111}, {cx - 18, top + 126}, {cx + 8, top + 139}, {cx + 34, top + 151}, {cx + 57, top + 160}}
	for _, pt := range pts {
		drawEllipse(hdc, RECT{int32(pt[0]), int32(pt[1]), int32(pt[0] + 5), int32(pt[1] + 5)}, rgb(201, 151, 130), rgb(201, 151, 130), 1)
	}

	// Small pencil crossing the lower-right corner.
	drawLine(hdc, cx+62, top+212, cx+145, top+129, clrMint, 9)
	drawLine(hdc, cx+67, top+217, cx+150, top+134, rgb(77, 116, 99), 2)
	drawLine(hdc, cx+58, top+220, cx+69, top+217, rgb(184, 135, 105), 5)

	// Compact suitcase on the left balances the composition.
	drawRoundRect(hdc, RECT{int32(cx - 195), int32(top + 151), int32(cx - 139), int32(top + 219)}, 10, rgb(255, 248, 243), clrCoral)
	drawRoundRect(hdc, RECT{int32(cx - 180), int32(top + 136), int32(cx - 154), int32(top + 157)}, 7, clrCanvas, clrCoral)
	drawEllipse(hdc, RECT{int32(cx - 187), int32(top + 216), int32(cx - 177), int32(top + 226)}, clrText, clrText, 1)
	drawEllipse(hdc, RECT{int32(cx - 157), int32(top + 216), int32(cx - 147), int32(top + 226)}, clrText, clrText, 1)

	// Quiet helper copy; the illustration remains the focal point.
	drawText(hdc, "選擇左側的旅行計劃", RECT{int32(cx - 210), int32(top + 268), int32(cx + 210), int32(top + 296)}, u.fontBold, clrText, DT_CENTER|DT_VCENTER|DT_SINGLELINE|DT_NOPREFIX)
	drawText(hdc, "或按左上角新增計劃開始規劃旅程", RECT{int32(cx - 240), int32(top + 298), int32(cx + 240), int32(top + 324)}, u.font, clrMuted, DT_CENTER|DT_VCENTER|DT_SINGLELINE|DT_NOPREFIX)
}

func (u *UI) paintSettings(hdc uintptr, rc RECT) {
	fillRectColor(hdc, rc, clrCanvas)
	left := int32(20)
	right := rc.Right - 20
	drawRoundRect(hdc, RECT{left, 18, right, 190}, 14, clrCard, clrCardBorder)
	drawRoundRect(hdc, RECT{left, 204, right, 370}, 14, clrCard, clrCardBorder)
	drawRoundRect(hdc, RECT{36, 34, 41, 58}, 5, clrCoral, clrCoral)
	drawRoundRect(hdc, RECT{36, 220, 41, 244}, 5, clrMint, clrMint)
}

func (u *UI) drawOwnerButton(lParam uintptr) bool {
	if lParam == 0 {
		return false
	}
	d := (*DRAWITEMSTRUCT)(unsafe.Pointer(lParam))
	id := uint16(d.CtlID)
	style := u.buttonStyles[id]
	if style == "" {
		style = "secondary"
	}
	if style == "checkboxOn" || style == "checkboxOff" {
		r := d.RcItem
		fillRectColor(d.HDC, r, clrCard)
		box := RECT{r.Left + 2, r.Top + 4, r.Left + 18, r.Top + 20}
		if style == "checkboxOn" {
			drawRoundRect(d.HDC, box, 4, clrCoral, clrCoral)
			drawText(d.HDC, "✓", box, u.fontSmall, rgb(255, 255, 255), DT_CENTER|DT_VCENTER|DT_SINGLELINE|DT_NOPREFIX)
		} else {
			drawRoundRect(d.HDC, box, 4, clrCard, clrCardBorder)
		}
		tr := RECT{r.Left + 24, r.Top, r.Right, r.Bottom}
		drawText(d.HDC, getText(d.HwndItem), tr, u.font, clrText, DT_LEFT|DT_VCENTER|DT_SINGLELINE|DT_NOPREFIX)
		return true
	}
	fill, border, text := clrCard, clrCardBorder, clrText
	if style == "toolbar" {
		// Keep the toolbar native-looking but visually stronger than ordinary
		// secondary actions: white system-window face, crisp system shadow border.
		fill, border, text = clrInput, sysColor(COLOR_BTNSHADOW), clrText
	}
	switch style {
	case "primary":
		fill, border, text = clrCoral, clrCoral, rgb(255, 255, 255)
	case "danger":
		fill, border, text = clrDangerSoft, rgb(235, 188, 188), clrDanger
	case "add":
		fill, border, text = clrCoralSoft, rgb(244, 184, 166), clrCoralDark
	case "tab":
		fill, border, text = clrCoralSoft, rgb(244, 190, 175), clrCoralDark
	case "ghost":
		fill, border, text = clrCard, clrCardBorder, clrMuted
	}
	if d.ItemState&ODS_DISABLED != 0 {
		fill, border, text = clrCanvas, clrCardBorder, clrMuted
	} else if d.ItemState&ODS_SELECTED != 0 {
		if style == "primary" {
			fill, border = clrCoralDark, clrCoralDark
		} else if style == "add" {
			fill, border = rgb(250, 224, 215), rgb(236, 157, 132)
		} else if style == "toolbar" {
			fill = sysColor(COLOR_3DLIGHT)
		} else {
			fill = clrCanvas
		}
	}
	r := d.RcItem
	drawRoundRect(d.HDC, r, 7, fill, border)
	if style == "toolbar" {
		glyph := ""
		glyphColor := clrCoral
		switch id {
		case ID_NEW_TRIP:
			glyph = "\uE710"
			glyphColor = clrCoral
		case ID_MODIFY_TRIP:
			glyph = "\uE70F"
			glyphColor = rgb(235, 150, 29)
		case ID_SAVE_TRIP:
			glyph = "\uE74E"
			glyphColor = clrMint
		case ID_DELETE_TRIP:
			if u.editing {
				glyph = "\uE711"
				glyphColor = rgb(239, 88, 82)
			} else {
				glyph = "\uE74D"
				glyphColor = rgb(239, 88, 82)
			}
		case ID_EXPORT:
			glyph = ""
			glyphColor = rgb(34, 104, 183)
		}
		if d.ItemState&ODS_DISABLED != 0 {
			glyphColor = clrMuted
		}
		if id == ID_EXPORT {
			wr := RECT{r.Left + 8, r.Top + 7, r.Left + 31, r.Bottom - 7}
			drawRoundRect(d.HDC, wr, 4, rgb(34, 104, 183), rgb(34, 104, 183))
			drawText(d.HDC, "W", wr, u.fontSmall, rgb(255, 255, 255), DT_CENTER|DT_VCENTER|DT_SINGLELINE|DT_NOPREFIX)
		} else if glyph != "" {
			gr := RECT{r.Left + 7, r.Top, r.Left + 33, r.Bottom}
			drawText(d.HDC, glyph, gr, u.fontIcon, glyphColor, DT_CENTER|DT_VCENTER|DT_SINGLELINE|DT_NOPREFIX)
		}
		tr := RECT{r.Left + 35, r.Top, r.Right - 7, r.Bottom}
		drawText(d.HDC, getText(d.HwndItem), tr, u.fontInput, text, DT_LEFT|DT_VCENTER|DT_SINGLELINE|DT_NOPREFIX|DT_END_ELLIPSIS)
		return true
	}
	tr := r
	if d.ItemState&ODS_SELECTED != 0 {
		tr.Top++
		tr.Bottom++
	}
	btnFont := u.font
	if id == ID_SEARCH_GO {
		btnFont = u.fontTitle
	}
	drawText(d.HDC, getText(d.HwndItem), tr, btnFont, text, DT_CENTER|DT_VCENTER|DT_SINGLELINE|DT_NOPREFIX|DT_END_ELLIPSIS)
	return true
}

func (u *UI) customDrawTripList(lParam uintptr) uintptr {
	cd := (*NMLVCUSTOMDRAW)(unsafe.Pointer(lParam))
	stage := cd.Nmcd.DwDrawStage
	switch stage {
	case CDDS_PREPAINT:
		return CDRF_NOTIFYITEMDRAW | CDRF_NOTIFYPOSTPAINT
	case CDDS_ITEMPREPAINT:
		// Ask for every subitem so the zebra color is applied across the full report row.
		return CDRF_NOTIFYSUBITEMDRAW
	case CDDS_ITEMPREPAINT | CDDS_SUBITEM:
		idx := int(cd.Nmcd.DwItemSpec)
		selected := cd.Nmcd.UItemState&CDIS_SELECTED != 0
		switch {
		case selected:
			cd.ClrTextBk = clrGridSel
		case idx%2 == 1:
			cd.ClrTextBk = clrGridAlt
		default:
			cd.ClrTextBk = clrListBase
		}
		if selected {
			cd.ClrText = clrGridSelText
		} else {
			cd.ClrText = clrText
		}
		return CDRF_NEWFONT
	case CDDS_POSTPAINT:
		// Continue the zebra pattern through the unused part of the list as well.
		// This makes the table visibly striped even before the first travel plan is created.
		hdc := cd.Nmcd.Hdc
		client := getClient(u.tripList)
		headerH := 22
		if hdr := send(u.tripList, LVM_GETHEADER, 0, 0); hdr != 0 {
			var wr RECT
			if r, _, _ := pGetWindowRect.Call(hdr, uintptr(unsafe.Pointer(&wr))); r != 0 {
				headerH = int(wr.Bottom - wr.Top)
			}
		}
		count := int(send(u.tripList, LVM_GETITEMCOUNT, 0, 0))
		rowH := 22
		startY := headerH
		if count > 0 {
			last := RECT{Left: LVIR_BOUNDS}
			if send(u.tripList, LVM_GETITEMRECT, uintptr(count-1), uintptr(unsafe.Pointer(&last))) != 0 {
				if h := int(last.Bottom - last.Top); h > 0 {
					rowH = h
				}
				startY = int(last.Bottom)
			}
		}
		if rowH < 18 {
			rowH = 18
		}
		row := count
		for y := startY; y < int(client.Bottom); y += rowH {
			y2 := y + rowH
			if y2 > int(client.Bottom) {
				y2 = int(client.Bottom)
			}
			bg := clrListBase
			if row%2 == 1 {
				bg = clrGridAlt
			}
			fillRectColor(hdc, RECT{0, int32(y), client.Right, int32(y2)}, bg)
			// Repaint the spreadsheet-like grid in the blank area.
			drawLine(hdc, 0, y2-1, int(client.Right), y2-1, clrGridLine, 1)
			row++
		}
		// Extend the *actual* ListView column separators through the unused area.
		// Do not use hard-coded x positions: the Header and ListView own the column
		// widths, and users/Windows DPI can change them.  Drawing at right-1 and
		// overlapping the header bottom by one pixel makes the separator visually
		// continuous from Header -> data rows -> blank zebra area.
		x := 0
		for col := 0; col < 3; col++ {
			cw := int(send(u.tripList, LVM_GETCOLUMNWIDTH, uintptr(col), 0))
			if cw <= 0 {
				break
			}
			x += cw
			sepX := x - 1
			if sepX >= 0 && sepX < int(client.Right) {
				drawLine(hdc, sepX, maxi(0, headerH-1), sepX, int(client.Bottom), clrGridLine, 1)
			}
		}
		return CDRF_DODEFAULT
	}
	return CDRF_DODEFAULT
}

func (u *UI) nextID() uint16 { u.controlID++; return u.controlID }

func (u *UI) selectedTrip() *Trip {
	if u.selectedTripID == "" {
		return nil
	}
	return tripByID(&u.store.data, u.selectedTripID)
}

func (u *UI) activeTrip() *Trip {
	if u.editing && u.workingTrip != nil {
		return u.workingTrip
	}
	return u.selectedTrip()
}

func (u *UI) activePlan() *Plan {
	t := u.activeTrip()
	if t == nil {
		return nil
	}
	if t.ActivePlanID == "" && len(t.Plans) > 0 {
		t.ActivePlanID = t.Plans[0].ID
	}
	return planByID(t, t.ActivePlanID)
}

func (u *UI) setMode(m uiMode) {
	u.mode = m
	u.editing = m == modeNew || m == modeEdit
	uiLog("UI mode=%s selected=%s working=%v dirty=%v", m.String(), u.selectedTripID, u.workingTrip != nil, u.dirty)
}

func (u *UI) markDirty() {
	if !u.editing || u.workingTrip == nil {
		return
	}
	u.dirty = true
	u.refreshToolbarState()
}

// saveNow is the in-memory mutation hook used by editor callbacks.  V0.8.0 keeps
// the working copy completely separate from persisted AppData until explicit Save.
func (u *UI) saveNow() { u.markDirty() }

func (u *UI) queueRender() {
	// Date picker notifications arrive while the user is still choosing a date.
	// Wait for its calendar/focus to close instead of destroying it mid-input.
	if u.cellEditor.hwnd != 0 && strings.HasPrefix(u.cellEditor.kind, "date") {
		return
	}
	if u.hwnd != 0 {
		pSetTimer.Call(u.hwnd, 4, 1, 0)
	}
}

func (u *UI) beginModify() {
	if u.editing {
		return
	}
	src := u.selectedTrip()
	if src == nil {
		return
	}
	copyTrip := cloneTrip(*src)
	u.workingTrip = &copyTrip
	u.dirty = false
	u.scrollY = 0
	u.setMode(modeEdit)
	uiLog("EDIT begin existing trip=%s", u.selectedTripID)
	u.renderRight()
	u.refreshToolbarState()
}

func (u *UI) discardWorkingTrip() {
	u.closeCellEditor()
	u.workingTrip = nil
	u.dirty = false
	u.editingTravelerID = ""
	u.editingMealName = ""
	u.openCard = -1
	if u.selectedTripID == "" {
		u.setMode(modeNoSelection)
	} else {
		u.setMode(modeView)
	}
}

func (u *UI) commitSave(showStatus bool) bool {
	if !u.editing || u.workingTrip == nil {
		return true
	}
	u.settleEditorFocus()
	uiLog("EDIT explicit save begin mode=%s dirty=%v selected=%s working=%s", u.mode.String(), u.dirty, u.selectedTripID, u.workingTrip.ID)

	// Build the persisted state only at the explicit Save boundary.  If disk I/O
	// fails, restore the exact prior AppData so a draft can never leak into the
	// saved list merely because Save was attempted.
	candidate := cloneAppData(u.store.data)
	nextSelected := u.selectedTripID
	working := cloneTrip(*u.workingTrip)
	if err := validateTripForSave(working); err != nil {
		msgBox(u.hwnd, err.Error(), "無法儲存", MB_OK|MB_ICONWARNING)
		return false
	}
	if u.mode == modeNew {
		candidate.Trips = append(candidate.Trips, working)
		nextSelected = working.ID
	} else {
		replaced := false
		for i := range candidate.Trips {
			if candidate.Trips[i].ID == u.selectedTripID {
				candidate.Trips[i] = working
				replaced = true
				break
			}
		}
		if !replaced {
			msgBox(u.hwnd, "找不到要儲存的旅行計劃。", "儲存", MB_OK|MB_ICONERROR)
			return false
		}
	}
	candidate.ActiveTripID = nextSelected
	if err := u.store.saveCandidate(candidate); err != nil {
		msgBox(u.hwnd, "儲存失敗："+err.Error(), "儲存", MB_OK|MB_ICONERROR)
		uiLog("EDIT explicit save failed err=%v", err)
		return false
	}

	u.selectedTripID = nextSelected
	u.closeCellEditor()
	u.workingTrip = nil
	u.dirty = false
	u.setMode(modeView)
	u.scrollY = 0
	u.renderTripList()
	u.renderRight()
	u.refreshToolbarState()
	if showStatus {
		u.flashSaved()
	}
	uiLog("EDIT explicit save complete selected=%s", u.selectedTripID)
	return true
}

// resolvePendingChanges is used before selecting another trip, opening settings,
// backup/export, or closing.  A draft lives only in workingTrip, so discarding no
// longer rolls back the entire application data set.
func (u *UI) resolvePendingChanges() bool {
	if !u.editing {
		return true
	}
	u.settleEditorFocus()
	if !u.dirty {
		u.discardWorkingTrip()
		u.renderTripList()
		u.renderRight()
		u.refreshToolbarState()
		return true
	}
	ans := msgBox(u.hwnd, "目前有尚未儲存的修改。\r\n\r\n要先儲存嗎？", "尚未儲存", MB_YESNOCANCEL|MB_ICONQUESTION)
	if ans == IDCANCEL {
		return false
	}
	if ans == IDYES {
		return u.commitSave(false)
	}
	u.discardWorkingTrip()
	u.renderTripList()
	u.renderRight()
	u.refreshToolbarState()
	uiLog("EDIT pending changes discarded")
	return true
}

func (u *UI) cancelEdit() {
	if !u.editing {
		return
	}
	uiLog("EDIT cancel mode=%s selected=%s working=%v dirty=%v", u.mode.String(), u.selectedTripID, u.workingTrip != nil, u.dirty)
	u.discardWorkingTrip()
	u.scrollY = 0
	u.renderTripList()
	u.renderRight()
	u.refreshToolbarState()
}

func (u *UI) flashSaved() {
	if u.status != 0 {
		setText(u.status, "已儲存")
		pSetTimer.Call(u.hwnd, 1, 1200, 0)
	}
}

func mutationButtonText(text string) bool {
	return strings.HasPrefix(text, "＋") || strings.Contains(text, "新增") || strings.Contains(text, "刪除") ||
		text == "×" || text == "↑" || text == "↓" || text == "←" || text == "→" || strings.Contains(text, "安排到")
}

func (u *UI) applyFormEditability(emptyMode bool) {
	on := u.editing && !emptyMode
	for kind, pool := range u.controlPool {
		used := u.poolUsed[kind]
		if used > len(pool) {
			used = len(pool)
		}
		for i := 0; i < used; i++ {
			h := pool[i].hwnd
			switch {
			case kind == "edit" || kind == "editmulti" || kind == "comboList" || kind == "comboEdit" || kind == "comboCommitList" || kind == "comboCommitEdit" || kind == "dateRequired" || kind == "dateOptional" || kind == "check" || kind == "radio" || kind == "radioGroup" || strings.HasPrefix(kind, "list"):
				enable(h, on)
			case strings.HasPrefix(kind, "button"):
				if mutationButtonText(getText(h)) {
					enable(h, on)
				}
			}
		}
	}
}

func (u *UI) refreshToolbarState() {
	hasTrip := u.activeTrip() != nil
	if u.btnNew != 0 {
		enable(u.btnNew, !u.editing)
	}
	if u.btnModify != 0 {
		enable(u.btnModify, hasTrip && !u.editing)
	}
	if u.btnSave != 0 {
		enable(u.btnSave, u.editing)
	}
	if u.btnDelete != 0 {
		if u.editing {
			setText(u.btnDelete, "取消")
			enable(u.btnDelete, true)
		} else {
			setText(u.btnDelete, "刪除")
			enable(u.btnDelete, hasTrip)
		}
		pInvalidateRect.Call(u.btnDelete, 0, 1)
	}
	if u.btnExport != 0 {
		enable(u.btnExport, hasTrip && !u.editing)
	}
}

func headerSubclassProc(hwnd uintptr, msg uint32, wParam, lParam, subclassID, refData uintptr) uintptr {
	// Keep the native Header control.  Only reinforce its bottom edge after Windows
	// has painted the themed header, because some light themes make that boundary
	// almost invisible.
	if msg == WM_PAINT {
		r, _, _ := pDefSubclassProc.Call(hwnd, uintptr(msg), wParam, lParam)
		hdc, _, _ := pGetDC.Call(hwnd)
		if hdc != 0 {
			rc := getClient(hwnd)
			y := maxi(0, int(rc.Bottom)-1)
			drawLine(hdc, 0, y, int(rc.Right), y, sysColor(COLOR_BTNSHADOW), 1)
			pReleaseDC.Call(hwnd, hdc)
		}
		return r
	}
	r, _, _ := pDefSubclassProc.Call(hwnd, uintptr(msg), wParam, lParam)
	return r
}

func editSubclassProc(hwnd uintptr, msg uint32, wParam, lParam, subclassID, refData uintptr) uintptr {
	if msg == WM_KEYDOWN && wParam == VK_ESCAPE && ui != nil && ui.cellEditor.hwnd == hwnd {
		ui.closeCellEditor()
		pSetFocus.Call(ui.content)
		return 0
	}
	if msg == WM_KEYDOWN && wParam == VK_RETURN {
		// Let the normal EN_KILLFOCUS binding commit the field.  This gives all
		// single-line editors the familiar "Enter to finish" behavior.
		if ui != nil && ui.content != 0 {
			pSetFocus.Call(ui.content)
			return 0
		}
	}
	r, _, _ := pDefSubclassProc.Call(hwnd, uintptr(msg), wParam, lParam)
	return r
}

func rootWndProc(hwnd uintptr, msg uint32, wParam, lParam uintptr) (ret uintptr) {
	defer func() {
		if v := recover(); v != nil {
			if ui != nil && ui.store != nil {
				ui.store.logf("PANIC callback=root hwnd=0x%x msg=0x%x value=%v\n%s", hwnd, msg, v, debug.Stack())
			}
			r, _, _ := pDefWindowProcW.Call(hwnd, uintptr(msg), wParam, lParam)
			ret = r
		}
	}()
	if ui == nil {
		r, _, _ := pDefWindowProcW.Call(hwnd, uintptr(msg), wParam, lParam)
		return r
	}
	switch msg {
	case 0x0024: // WM_GETMINMAXINFO: keep dense editors within the viewport.
		info := (*MINMAXINFO)(unsafe.Pointer(lParam))
		info.MinTrackSize = POINT{1240, 640}
		return 0
	case WM_ERASEBKGND:
		fillRectColor(wParam, getClient(hwnd), clrCanvas)
		return 1
	case WM_SIZE:
		ui.layoutRoot()
		// Reflow the large right-side form only after resize activity settles.
		// V0.4.0 rebuilt hundreds of controls on every WM_SIZE and could become unresponsive.
		pSetTimer.Call(hwnd, 2, 180, 0)
		return 0
	case WM_CTLCOLORSTATIC:
		// Top-level text such as the date-range separator/status must blend into the toolbar canvas.
		pSetBkMode.Call(wParam, TRANSPARENT)
		pSetTextColor.Call(wParam, uintptr(clrMuted))
		if ui.brushWindow != 0 {
			return ui.brushWindow
		}
	case WM_COMMAND:
		id := int(loword(wParam))
		code := hiword(wParam)
		if id == ID_SEARCH_GO && code == BN_CLICKED {
			ui.applySearch()
			return 0
		}
		switch id {
		case ID_NEW_TRIP:
			ui.newTrip()
			return 0
		case ID_MODIFY_TRIP:
			ui.beginModify()
			return 0
		case ID_SAVE_TRIP:
			ui.commitSave(true)
			return 0
		case ID_DELETE_TRIP:
			if ui.editing {
				ui.cancelEdit()
			} else {
				ui.deleteTrip()
			}
			return 0
		case ID_BACKUP:
			if !ui.resolvePendingChanges() {
				return 0
			}
			ui.doBackup()
			return 0
		case ID_EXPORT:
			if ui.editing {
				msgBox(ui.hwnd, "請先儲存或取消目前編輯，再匯出 Word。", "匯出 Word", MB_OK|MB_ICONINFORMATION)
				return 0
			}
			ui.doExport()
			return 0
		case ID_OPEN_BACKUP:
			ui.store.openFolder("backup")
			return 0
		case ID_OPEN_EXPORTS:
			ui.store.openFolder("exports")
			return 0
		case ID_OPEN_LOG:
			ui.store.openFolder("log")
			return 0
		case ID_SETTINGS:
			if !ui.resolvePendingChanges() {
				return 0
			}
			ui.settingsDialog()
			return 0
		case ID_ABOUT:
			msgBox(hwnd, "TravelPlan V"+Version+"\r\nWindows x64 Desktop Portable", "關於 TravelPlan", MB_OK|MB_ICONINFORMATION)
			return 0
		case ID_GLOGIN:
			if !ui.resolvePendingChanges() {
				return 0
			}
			ui.googleLogin()
			return 0
		case ID_GSYNC:
			if !ui.resolvePendingChanges() {
				return 0
			}
			ui.googleSync()
			return 0
		case ID_GLOGOUT:
			if !ui.resolvePendingChanges() {
				return 0
			}
			ui.googleLogout()
			return 0
		case ID_EXIT:
			if !ui.resolvePendingChanges() {
				return 0
			}
			pDestroyWindow.Call(hwnd)
			return 0
		}
	case WM_NOTIFY:
		hdr := (*NMHDR)(unsafe.Pointer(lParam))
		if hdr.IdFrom == ID_TRIPLIST && hdr.Code == NM_CUSTOMDRAW {
			return ui.customDrawTripList(lParam)
		}
		if hdr.IdFrom == ID_TRIPLIST && hdr.Code == LVN_ITEMCHANGED && !ui.suppressTripSelect {
			nm := (*NMLISTVIEW)(unsafe.Pointer(lParam))
			if nm.IItem >= 0 && (nm.UNewState&LVIS_SELECTED) != 0 && int(nm.IItem) < len(ui.visibleTripIDs) {
				id := ui.visibleTripIDs[nm.IItem]
				if ui.selectedTripID != id || ui.mode != modeView {
					oldID := ui.selectedTripID
					if !ui.resolvePendingChanges() {
						ui.selectedTripID = oldID
						ui.store.data.ActiveTripID = oldID
						ui.suppressTripSelect = true
						ui.renderTripList()
						ui.suppressTripSelect = false
						return 0
					}
					uiLog("ACTION select plan id=%s", id)
					ui.selectedTripID = id
					ui.store.data.ActiveTripID = id
					ui.workingTrip = nil
					ui.dirty = false
					ui.setMode(modeView)
					ui.scrollY = 0
					ui.suppressTripSelect = true
					ui.renderTripList()
					ui.suppressTripSelect = false
					ui.renderRight()
					ui.refreshToolbarState()
				}
			}
		}
	case WM_DRAWITEM:
		if ui.drawOwnerButton(lParam) {
			return 1
		}
	case WM_TIMER:
		if wParam == 1 {
			pKillTimer.Call(hwnd, 1)
			if ui.status != 0 {
				setText(ui.status, "")
			}
		}
		if wParam == 2 {
			pKillTimer.Call(hwnd, 2)
			r := getClient(ui.right)
			width := int(r.Right)
			if width > 0 && width != ui.lastRightWidth {
				uiLog("UI resize settled rightWidth=%d previous=%d; stable reflow begin", width, ui.lastRightWidth)
				ui.lastRightWidth = width
				ui.renderRight()
			}
		}
		if wParam == 3 {
			ui.lastHeartbeat = time.Now()
			uiLog("APP heartbeat mode=%s selected=%s controls=%d scroll=%d contentH=%d", ui.mode.String(), ui.selectedTripID, len(ui.controls), ui.scrollY, ui.contentH)
		}
		if wParam == 4 {
			pKillTimer.Call(hwnd, 4)
			ui.renderRight()
		}
		return 0
	case WM_CLOSE:
		uiLog("APP close requested")
		if !ui.resolvePendingChanges() {
			return 0
		}
		pDestroyWindow.Call(hwnd)
		return 0
	case WM_DESTROY:
		uiLog("APP main window destroyed")
		if ui.brushWindow != 0 {
			pDeleteObject.Call(ui.brushWindow)
			ui.brushWindow = 0
		}
		if ui.brushCard != 0 {
			pDeleteObject.Call(ui.brushCard)
			ui.brushCard = 0
		}
		if ui.emptyBmp != 0 {
			pDeleteObject.Call(ui.emptyBmp)
			ui.emptyBmp = 0
		}
		pPostQuitMessage.Call(0)
		return 0
	}
	r, _, _ := pDefWindowProcW.Call(hwnd, uintptr(msg), wParam, lParam)
	return r
}

func viewportWndProc(hwnd uintptr, msg uint32, wParam, lParam uintptr) (ret uintptr) {
	defer func() {
		if v := recover(); v != nil {
			if ui != nil && ui.store != nil {
				ui.store.logf("PANIC callback=viewport hwnd=0x%x msg=0x%x value=%v\n%s", hwnd, msg, v, debug.Stack())
			}
			r, _, _ := pDefWindowProcW.Call(hwnd, uintptr(msg), wParam, lParam)
			ret = r
		}
	}()
	if ui == nil {
		r, _, _ := pDefWindowProcW.Call(hwnd, uintptr(msg), wParam, lParam)
		return r
	}
	switch msg {
	case WM_VSCROLL:
		si := getScroll(hwnd)
		pos := int(si.NPos)
		code := int(loword(wParam))
		switch code {
		case SB_LINEUP:
			pos -= 42
		case SB_LINEDOWN:
			pos += 42
		case SB_PAGEUP:
			pos -= int(si.NPage)
		case SB_PAGEDOWN:
			pos += int(si.NPage)
		case SB_THUMBTRACK, SB_THUMBPOSITION:
			pos = int(si.NTrackPos)
		case SB_TOP:
			pos = 0
		case SB_BOTTOM:
			pos = int(si.NMax)
		}
		ui.setScroll(pos)
		return 0
	case WM_MOUSEWHEEL:
		delta := int(signedHiword(wParam))
		ui.setScroll(ui.scrollY - delta/3)
		return 0
	case WM_SIZE:
		r := getClient(hwnd)
		setScroll(hwnd, maxi(0, ui.contentH-int(r.Bottom)), int(r.Bottom), ui.scrollY)
		if ui.content != 0 {
			move(ui.content, 0, -ui.scrollY, maxi(700, int(r.Right)), maxi(ui.contentH, int(r.Bottom)))
		}
		return 0
	}
	r, _, _ := pDefWindowProcW.Call(hwnd, uintptr(msg), wParam, lParam)
	return r
}

func formWndProc(hwnd uintptr, msg uint32, wParam, lParam uintptr) (ret uintptr) {
	defer func() {
		if v := recover(); v != nil {
			if ui != nil && ui.store != nil {
				ui.store.logf("PANIC callback=form hwnd=0x%x msg=0x%x value=%v\n%s", hwnd, msg, v, debug.Stack())
			}
			r, _, _ := pDefWindowProcW.Call(hwnd, uintptr(msg), wParam, lParam)
			ret = r
		}
	}()
	if ui == nil {
		r, _, _ := pDefWindowProcW.Call(hwnd, uintptr(msg), wParam, lParam)
		return r
	}
	switch msg {
	case WM_ERASEBKGND:
		return 1
	case WM_PAINT:
		var ps PAINTSTRUCT
		hdc, _, _ := pBeginPaint.Call(hwnd, uintptr(unsafe.Pointer(&ps)))
		if hdc != 0 {
			ui.paintForm(hdc, getClient(hwnd))
		}
		pEndPaint.Call(hwnd, uintptr(unsafe.Pointer(&ps)))
		return 0
	case WM_DRAWITEM:
		if ui.drawOwnerButton(lParam) {
			return 1
		}
	case WM_NOTIFY:
		hdr := (*NMHDR)(unsafe.Pointer(lParam))
		if hdr.HwndFrom == ui.cellEditor.hwnd && (hdr.Code == -753 || hdr.Code == -8) && !ui.rendering {
			pSetTimer.Call(ui.hwnd, 4, 1, 0) // DTN_CLOSEUP / NM_KILLFOCUS
		}
		if _, ok := ui.listBindings[uint16(hdr.IdFrom)]; ok {
			if hdr.Code == NM_CUSTOMDRAW {
				return ui.customDrawDataList(lParam)
			}
			if hdr.Code == NM_DBLCLK && !ui.rendering {
				nm := (*NMLISTVIEW)(unsafe.Pointer(lParam))
				item, sub := listHitTest(hdr.HwndFrom, nm.PtAction)
				uiLog("UI list dblclick id=%d item=%d sub=%d", hdr.IdFrom, item, sub)
				ui.openDataListCell(uint16(hdr.IdFrom), hdr.HwndFrom, item, sub)
				return 0
			}
			if hdr.Code == NM_CLICK && !ui.rendering {
				nm := (*NMLISTVIEW)(unsafe.Pointer(lParam))
				item, sub := listHitTest(hdr.HwndFrom, nm.PtAction)
				if ui.handleDataListClick(uint16(hdr.IdFrom), hdr.HwndFrom, item, sub, nm.PtAction) {
					return 0
				}
			}
		}
		if hdr.Code == DTN_DATETIMECHANGE && !ui.rendering {
			if b, ok := ui.dateBindings[uint16(hdr.IdFrom)]; ok && b.fn != nil {
				b.fn(getDatePickerISO(hdr.HwndFrom))
				ui.saveNow()
				return 0
			}
		}
	case WM_COMMAND:
		if ui.rendering {
			return 0
		}
		id := loword(wParam)
		code := hiword(wParam)
		if a, ok := ui.buttonActions[id]; ok && code == BN_CLICKED {
			uiLog("UI button id=%d", id)
			a()
			return 0
		}
		if b, ok := ui.editBindings[id]; ok {
			ctrl := lParam
			if code == EN_CHANGE && b.change != nil {
				b.change(getText(ctrl))
				ui.saveNow()
			}
			if code == EN_KILLFOCUS && b.blur != nil {
				b.blur(getText(ctrl))
				ui.saveNow()
			}
			return 0
		}
		if b, ok := ui.comboBindings[id]; ok {
			if b.blurOnly && code == CBN_KILLFOCUS {
				b.fn(getText(lParam))
				ui.saveNow()
				return 0
			}
			if !b.blurOnly && (code == CBN_EDITCHANGE || code == CBN_SELCHANGE) {
				b.fn(getText(lParam))
				ui.saveNow()
				return 0
			}
		}
	case WM_MOUSEWHEEL:
		// Forward wheel messages received over child controls to the stable viewport.
		if ui.right != 0 {
			send(ui.right, WM_MOUSEWHEEL, wParam, lParam)
			return 0
		}
	}
	r, _, _ := pDefWindowProcW.Call(hwnd, uintptr(msg), wParam, lParam)
	return r
}

func settingsWndProc(hwnd uintptr, msg uint32, wParam, lParam uintptr) (ret uintptr) {
	defer func() {
		if v := recover(); v != nil {
			uiLog("PANIC callback=settings msg=0x%x value=%v\n%s", msg, v, debug.Stack())
			r, _, _ := pDefWindowProcW.Call(hwnd, uintptr(msg), wParam, lParam)
			ret = r
		}
	}()
	if ui == nil {
		r, _, _ := pDefWindowProcW.Call(hwnd, uintptr(msg), wParam, lParam)
		return r
	}
	switch msg {
	case WM_ERASEBKGND:
		return 1
	case WM_PAINT:
		var ps PAINTSTRUCT
		hdc, _, _ := pBeginPaint.Call(hwnd, uintptr(unsafe.Pointer(&ps)))
		if hdc != 0 {
			ui.paintSettings(hdc, getClient(hwnd))
		}
		pEndPaint.Call(hwnd, uintptr(unsafe.Pointer(&ps)))
		return 0
	case WM_CTLCOLORSTATIC:
		// Settings labels are painted as part of the white cards; never show classic STATIC gray backing.
		pSetBkMode.Call(wParam, TRANSPARENT)
		pSetTextColor.Call(wParam, uintptr(clrText))
		if ui.brushCard != 0 {
			return ui.brushCard
		}
		b, _, _ := pGetStockObject.Call(NULL_BRUSH)
		return b
	case WM_DRAWITEM:
		if ui.drawOwnerButton(lParam) {
			return 1
		}
	case WM_COMMAND:
		id := int(loword(wParam))
		if hiword(wParam) == BN_CLICKED {
			switch id {
			case ID_SETTINGS_SAVE:
				if settingsWin != nil {
					candidate := cloneAppData(ui.store.data)
					st := &candidate.Settings
					st.DayFormat = comboText(settingsWin.day)
					st.WeekFormat = comboText(settingsWin.week)
					switch comboText(settingsWin.year) {
					case "永遠顯示年份":
						st.YearMode = "always"
					case "永不顯示年份":
						st.YearMode = "never"
					default:
						st.YearMode = "auto"
					}
					st.WordDayPage = isChecked(settingsWin.wordPage)
					st.WordUndated = isChecked(settingsWin.wordUndated)
					st.WordBlankMeals = isChecked(settingsWin.wordBlankMeals)
					if err := ui.store.saveCandidate(candidate); err != nil {
						msgBox(hwnd, "設定儲存失敗："+err.Error(), "設定", MB_OK|MB_ICONERROR)
						return 0
					}
					uiLog("SETTINGS saved day=%q week=%q year=%q wordPage=%v wordUndated=%v blankMeals=%v",
						st.DayFormat, st.WeekFormat, st.YearMode, st.WordDayPage, st.WordUndated, st.WordBlankMeals)
					ui.renderRight()
				}
				pDestroyWindow.Call(hwnd)
				return 0
			case ID_SETTINGS_CANCEL:
				pDestroyWindow.Call(hwnd)
				return 0
			}
		}
	case WM_CLOSE:
		pDestroyWindow.Call(hwnd)
		return 0
	case WM_DESTROY:
		if settingsWin != nil && settingsWin.parent != 0 {
			enable(settingsWin.parent, true)
			pSetFocus.Call(settingsWin.parent)
		}
		settingsWin = nil
		uiLog("SETTINGS closed")
		return 0
	}
	r, _, _ := pDefWindowProcW.Call(hwnd, uintptr(msg), wParam, lParam)
	return r
}

func (u *UI) registerClasses() error {
	uiLog("UI registerClasses begin")
	pSetProcessDPIAware.Call()
	hcur, _, _ := pLoadCursorW.Call(0, IDC_ARROW)
	bg := uintptr(COLOR_BTNFACE + 1)
	rootName := wstr(rootClass)
	viewportName := wstr(viewportClass)
	formName := wstr(formClass)
	settingsName := wstr(settingsClass)
	wc := WNDCLASSEX{CbSize: uint32(unsafe.Sizeof(WNDCLASSEX{})), LpfnWndProc: rootWndProcCB, HInstance: u.inst, HCursor: hcur, HbrBackground: bg, LpszClassName: rootName, HIcon: u.iconBig, HIconSm: u.iconSmall}
	if r, _, e := pRegisterClassExW.Call(uintptr(unsafe.Pointer(&wc))); r == 0 {
		return fmt.Errorf("register root: %v", e)
	}
	wcViewport := WNDCLASSEX{CbSize: uint32(unsafe.Sizeof(WNDCLASSEX{})), LpfnWndProc: viewportWndProcCB, HInstance: u.inst, HCursor: hcur, HbrBackground: bg, LpszClassName: viewportName}
	if r, _, e := pRegisterClassExW.Call(uintptr(unsafe.Pointer(&wcViewport))); r == 0 {
		return fmt.Errorf("register viewport: %v", e)
	}
	wc2 := WNDCLASSEX{CbSize: uint32(unsafe.Sizeof(WNDCLASSEX{})), LpfnWndProc: formWndProcCB, HInstance: u.inst, HCursor: hcur, HbrBackground: bg, LpszClassName: formName}
	if r, _, e := pRegisterClassExW.Call(uintptr(unsafe.Pointer(&wc2))); r == 0 {
		return fmt.Errorf("register form: %v", e)
	}
	wc3 := WNDCLASSEX{CbSize: uint32(unsafe.Sizeof(WNDCLASSEX{})), LpfnWndProc: settingsWndProcCB, HInstance: u.inst, HCursor: hcur, HbrBackground: bg, LpszClassName: settingsName, HIcon: u.iconBig, HIconSm: u.iconSmall}
	if r, _, e := pRegisterClassExW.Call(uintptr(unsafe.Pointer(&wc3))); r == 0 {
		return fmt.Errorf("register settings: %v", e)
	}
	ic := INITCOMMONCONTROLSEX{DwSize: uint32(unsafe.Sizeof(INITCOMMONCONTROLSEX{})), DwICC: ICC_LISTVIEW_CLASSES | ICC_STANDARD_CLASSES | ICC_DATE_CLASSES}
	pInitCommonControlsEx.Call(uintptr(unsafe.Pointer(&ic)))
	uiLog("UI registerClasses complete")
	return nil
}

func createFont(height int, weight int, face string) uintptr {
	r, _, _ := pCreateFontW.Call(uintptr(height), 0, 0, 0, uintptr(weight), 0, 0, 0, 1, 0, 0, 5, 0, uintptr(unsafe.Pointer(wstr(face))))
	return r
}

func measureTextWidth(hwnd, font uintptr, text string) int {
	if text == "" {
		return 0
	}
	hdc, _, _ := pGetDC.Call(hwnd)
	if hdc == 0 {
		return len([]rune(text)) * 12
	}
	defer pReleaseDC.Call(hwnd, hdc)
	old, _, _ := pSelectObject.Call(hdc, font)
	if old != 0 {
		defer pSelectObject.Call(hdc, old)
	}
	ws := syscall.StringToUTF16(text)
	if len(ws) == 0 {
		return 0
	}
	var sz SIZE
	r, _, _ := pGetTextExtentPoint32W.Call(hdc, uintptr(unsafe.Pointer(&ws[0])), uintptr(len(ws)-1), uintptr(unsafe.Pointer(&sz)))
	if r == 0 {
		return len([]rune(text)) * 12
	}
	return int(sz.Cx)
}

func raiseChild(hwnd uintptr) {
	if hwnd == 0 {
		return
	}
	pSetWindowPos.Call(hwnd, 0, 0, 0, 0, 0, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE|SWP_SHOWWINDOW)
}

func listHitTest(hwnd uintptr, pt POINT) (int, int) {
	ht := LVHITTESTINFO{Pt: pt, IItem: -1, ISubItem: -1}
	r := send(hwnd, LVM_SUBITEMHITTEST, 0, uintptr(unsafe.Pointer(&ht)))
	item := int(int32(r))
	if item < 0 {
		item = int(ht.IItem)
	}
	return item, int(ht.ISubItem)
}

func (u *UI) createMain() error {
	uiLog("UI createMain begin")
	applyPalette(paletteLight)
	uiLog("UI palette=%s", activePalette)
	u.font = createFont(-13, 400, "Segoe UI")
	u.fontInput = createFont(-14, 400, "Segoe UI")
	u.fontHint = createFont(-17, 400, "Segoe UI")
	u.fontBold = createFont(-14, 600, "Segoe UI")
	u.fontSection = createFont(-18, 600, "Segoe UI")
	u.fontTitle = createFont(-20, 600, "Segoe UI")
	u.fontSmall = createFont(-12, 400, "Segoe UI")
	u.fontIcon = createFont(-17, 400, "Segoe MDL2 Assets")
	u.fontEmoji = createFont(-14, 400, "Segoe UI Emoji")
	u.brushWindow = makeBrush(clrCanvas)
	u.brushCard = makeBrush(clrCard)
	u.emptyBmp = loadBitmapFile(filepath.Join(u.store.root, "assets", "empty_state.bmp"), 220, 220)
	r, _, _ := pCreateWindowExW.Call(0, uintptr(unsafe.Pointer(wstr(rootClass))), uintptr(unsafe.Pointer(wstr("TravelPlan  V"+Version))), WS_OVERLAPPEDWINDOW|WS_CLIPCHILDREN, 100, 60, 1520, 940, 0, 0, u.inst, 0)
	if r == 0 {
		return fmt.Errorf("CreateWindowEx failed")
	}
	u.hwnd = r
	if u.iconBig != 0 {
		send(u.hwnd, WM_SETICON, ICON_BIG, u.iconBig)
	}
	if u.iconSmall != 0 {
		send(u.hwnd, WM_SETICON, ICON_SMALL, u.iconSmall)
	}
	u.createMenu()
	uiLog("UI menu created")

	toolbarButtons := []struct {
		id        int
		text, tip string
		w         int
	}{
		{ID_NEW_TRIP, "新增", "新增計劃", 78},
		{ID_MODIFY_TRIP, "修改", "修改計劃", 78},
		{ID_SAVE_TRIP, "儲存", "儲存", 78},
		{ID_DELETE_TRIP, "刪除", "刪除計劃／編輯時取消", 78},
	}
	x := 12
	makeToolbarButton := func(id int, text, tip string, width int) uintptr {
		h := createWindow(0, "BUTTON", text, WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_OWNERDRAW, x, 8, width, 40, u.hwnd, uintptr(id), u.inst)
		setFont(h, u.fontInput)
		u.buttonStyles[uint16(id)] = "toolbar"
		addTooltip(u.hwnd, h, tip, u.inst)
		return h
	}
	for _, b := range toolbarButtons {
		h := makeToolbarButton(b.id, b.text, b.tip, b.w)
		switch b.id {
		case ID_NEW_TRIP:
			u.btnNew = h
		case ID_MODIFY_TRIP:
			u.btnModify = h
		case ID_SAVE_TRIP:
			u.btnSave = h
		case ID_DELETE_TRIP:
			u.btnDelete = h
		}
		x += b.w + 6
	}
	// Separate record-edit actions from document/export actions.
	createWindow(0, "STATIC", "", WS_CHILD|WS_VISIBLE|SS_ETCHEDVERT, x+4, 12, 2, 32, u.hwnd, 0, u.inst)
	x += 18
	u.btnExport = makeToolbarButton(ID_EXPORT, "Word 匯出", "匯出 Word", 132)
	x += 138
	u.status = createWindow(0, "STATIC", "", WS_CHILD|WS_VISIBLE|SS_LEFT, 0, 17, 130, 24, u.hwnd, 190, u.inst)
	setFont(u.status, u.fontSmall)

	// Compact two-row search panel. The right edges of the text/date filters align, and the
	// tall rounded 查詢 button forms a separate action column.
	const searchX = 12
	const searchFieldW = 306
	const searchButtonX = 328
	const searchButtonW = 70
	const searchRowH = 24
	const searchTop = toolbarH + 10
	u.searchFrame = 0
	searchEditH := naturalEditHeight(u.hwnd, u.fontSmall)
	u.search = createWindow(0, "EDIT", "", WS_CHILD|WS_VISIBLE|WS_TABSTOP|WS_BORDER|ES_AUTOHSCROLL, searchX, searchTop+2, searchFieldW, searchEditH, u.hwnd, ID_SEARCH_INPUT, u.inst)
	setFont(u.search, u.fontSmall)
	setCueBanner(u.search, "查詢計劃名稱、旅伴")
	setWindowTheme(u.search, "Explorer")
	pSetWindowSubclass.Call(u.search, editSubclassProcCB, 1, ID_SEARCH_INPUT)
	u.searchButton = createWindow(0, "BUTTON", "查詢", WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_OWNERDRAW, searchButtonX, searchTop, searchButtonW, 54, u.hwnd, ID_SEARCH_GO, u.inst)
	setFont(u.searchButton, u.fontSmall)
	u.buttonStyles[ID_SEARCH_GO] = "primary"

	// Blank-capable native Windows date pickers; no redundant 起日／訖日 labels.
	dateY := searchTop + 30
	u.searchStart = createWindow(0, "SysDateTimePick32", "", WS_CHILD|WS_VISIBLE|WS_TABSTOP|DTS_SHORTDATEFORMAT|DTS_SHOWNONE, searchX, dateY, 141, searchRowH, u.hwnd, ID_SEARCH_START, u.inst)
	setFont(u.searchStart, u.fontSmall)
	setDatePickerFormat(u.searchStart, "yyyy/MM/dd")
	setDatePickerNone(u.searchStart)
	setWindowTheme(u.searchStart, "Explorer")
	u.searchSep = createWindow(0, "STATIC", "～", WS_CHILD|WS_VISIBLE|SS_CENTER|SS_CENTERIMAGE, 153, dateY, 24, searchRowH, u.hwnd, 0, u.inst)
	setFont(u.searchSep, u.fontSmall)
	u.searchEnd = createWindow(0, "SysDateTimePick32", "", WS_CHILD|WS_VISIBLE|WS_TABSTOP|DTS_SHORTDATEFORMAT|DTS_SHOWNONE, 177, dateY, 141, searchRowH, u.hwnd, ID_SEARCH_END, u.inst)
	setFont(u.searchEnd, u.fontSmall)
	setDatePickerFormat(u.searchEnd, "yyyy/MM/dd")
	setDatePickerNone(u.searchEnd)
	setWindowTheme(u.searchEnd, "Explorer")

	u.tripList = createWindow(0, "SysListView32", "", WS_CHILD|WS_VISIBLE|WS_BORDER|LVS_REPORT|LVS_SINGLESEL|LVS_SHOWSELALWAYS, 12, toolbarH+76, leftWidth-24, 600, u.hwnd, ID_TRIPLIST, u.inst)
	setFont(u.tripList, u.fontSmall)
	send(u.tripList, LVM_SETEXTENDEDLISTVIEWSTYLE, 0, LVS_EX_FULLROWSELECT|LVS_EX_DOUBLEBUFFER|LVS_EX_GRIDLINES)
	setWindowTheme(u.tripList, "Explorer")
	u.initTripColumns()
	if hdr := send(u.tripList, LVM_GETHEADER, 0, 0); hdr != 0 {
		setFont(hdr, u.fontSmall)
		pSetWindowSubclass.Call(hdr, headerSubclassProcCB, 1, 0)
	}

	// V0.8.0: the right side has three independent panes.  Welcome, read-only view and
	// editor controls no longer share the same child-window lifecycle.  Only one pane is
	// visible at a time; switching modes is Hide/Show rather than destructive recycling.
	u.right = createWindow(WS_EX_CONTROLPARENT, viewportClass, "", WS_CHILD|WS_VISIBLE|WS_VSCROLL|WS_CLIPCHILDREN, leftWidth, toolbarH, 900, 700, u.hwnd, 180, u.inst)
	u.emptyPane = createWindow(WS_EX_CONTROLPARENT, formClass, "", WS_CHILD|WS_CLIPCHILDREN, 0, 0, 900, 900, u.right, 181, u.inst)
	u.viewPane = createWindow(WS_EX_CONTROLPARENT, formClass, "", WS_CHILD|WS_CLIPCHILDREN, 0, 0, 900, 900, u.right, 182, u.inst)
	u.editPane = createWindow(WS_EX_CONTROLPARENT, formClass, "", WS_CHILD|WS_CLIPCHILDREN, 0, 0, 900, 900, u.right, 183, u.inst)
	u.content = u.emptyPane
	u.panePools = map[uintptr]map[string][]pooledCtrl{}
	u.panePoolUsed = map[uintptr]map[string]int{}
	u.activatePanePool(u.content)
	u.setMode(modeNoSelection)
	u.layoutRoot()
	u.renderTripList()
	u.renderRight()
	u.refreshToolbarState()
	initialRight := getClient(u.right)
	u.lastRightWidth = int(initialRight.Right)
	uiLog("UI initial right width recorded=%d", u.lastRightWidth)
	pShowWindow.Call(u.hwnd, SW_SHOW)
	pUpdateWindow.Call(u.hwnd)
	// Heartbeat lets us distinguish a native process crash from a clean message-loop exit.
	pSetTimer.Call(u.hwnd, 3, 15000, 0)
	uiLog("UI createMain complete hwnd=0x%x viewport=0x%x empty=0x%x view=0x%x edit=0x%x", u.hwnd, u.right, u.emptyPane, u.viewPane, u.editPane)
	return nil
}

func (u *UI) createMenu() {
	bar, _, _ := pCreateMenu.Call()
	file, _, _ := pCreatePopupMenu.Call()
	export, _, _ := pCreatePopupMenu.Call()
	gd, _, _ := pCreatePopupMenu.Call()
	help, _, _ := pCreatePopupMenu.Call()
	add := func(m uintptr, id int, text string) {
		pAppendMenuW.Call(m, MF_STRING, uintptr(id), uintptr(unsafe.Pointer(wstr(text))))
	}
	add(file, ID_NEW_TRIP, "新增計劃")
	add(file, ID_MODIFY_TRIP, "修改計劃")
	add(file, ID_SAVE_TRIP, "儲存")
	add(file, ID_DELETE_TRIP, "刪除計劃")
	pAppendMenuW.Call(file, MF_SEPARATOR, 0, 0)
	add(file, ID_BACKUP, "立即備份")
	add(file, ID_OPEN_BACKUP, "開啟備份資料夾")
	pAppendMenuW.Call(file, MF_SEPARATOR, 0, 0)
	add(file, ID_EXIT, "結束")
	add(export, ID_EXPORT, "匯出目前方案 Word")
	add(export, ID_OPEN_EXPORTS, "開啟匯出資料夾")
	add(gd, ID_GLOGIN, "登入 Google Drive")
	add(gd, ID_GSYNC, "立即同步")
	add(gd, ID_GLOGOUT, "登出")
	add(help, ID_OPEN_LOG, "開啟 Log 資料夾")
	add(help, ID_ABOUT, "關於 TravelPlan")
	pAppendMenuW.Call(bar, MF_POPUP, file, uintptr(unsafe.Pointer(wstr("檔案"))))
	pAppendMenuW.Call(bar, MF_POPUP, export, uintptr(unsafe.Pointer(wstr("匯出"))))
	pAppendMenuW.Call(bar, MF_POPUP, gd, uintptr(unsafe.Pointer(wstr("Google Drive"))))
	pAppendMenuW.Call(bar, MF_STRING, ID_SETTINGS, uintptr(unsafe.Pointer(wstr("設定"))))
	pAppendMenuW.Call(bar, MF_POPUP, help, uintptr(unsafe.Pointer(wstr("說明"))))
	pSetMenu.Call(u.hwnd, bar)
	pDrawMenuBar.Call(u.hwnd)
}

func (u *UI) initTripColumns() {
	cols := []struct {
		text string
		w    int
	}{{"開始日期", 88}, {"結束日期", 88}, {"旅行計劃", 132}, {"旅伴", 78}}
	for i, c := range cols {
		cw := wstr(c.text)
		col := LVCOLUMN{Mask: LVCF_TEXT | LVCF_WIDTH, Cx: int32(c.w), PszText: cw}
		send(u.tripList, LVM_INSERTCOLUMNW, uintptr(i), uintptr(unsafe.Pointer(&col)))
	}
}
func (u *UI) layoutRoot() {
	if u.hwnd == 0 {
		return
	}
	r := getClient(u.hwnd)
	w := int(r.Right)
	h := int(r.Bottom)
	searchTop := toolbarH + 8
	dateY := searchTop + 30
	if u.searchFrame != 0 {
		move(u.searchFrame, 12, searchTop, 306, 27)
	}
	move(u.search, 12, searchTop+2, 306, naturalEditHeight(u.hwnd, u.fontSmall))
	move(u.searchButton, 328, searchTop, 70, 61)
	move(u.searchStart, 12, dateY, 141, 27)
	move(u.searchSep, 153, dateY, 24, 27)
	move(u.searchEnd, 177, dateY, 141, 27)
	move(u.tripList, 12, toolbarH+76, leftWidth-24, maxi(100, h-toolbarH-88))
	move(u.right, leftWidth, toolbarH, maxi(100, w-leftWidth), maxi(100, h-toolbarH))
	move(u.status, maxi(0, w-145), 17, 132, 24)
}

func (u *UI) renderTripList() {
	if u.tripList == 0 {
		return
	}
	previousSuppress := u.suppressTripSelect
	u.suppressTripSelect = true
	defer func() { u.suppressTripSelect = previousSuppress }()
	send(u.tripList, LVM_DELETEALLITEMS, 0, 0)
	q := u.searchQueryApplied
	startFilter := u.searchStartApplied
	endFilter := u.searchEndApplied
	uiLog("SEARCH render list query=%q start=%q end=%q total=%d", q, startFilter, endFilter, len(u.store.data.Trips))
	trips := make([]Trip, len(u.store.data.Trips))
	copy(trips, u.store.data.Trips)
	today := time.Now().Format("2006-01-02")
	sort.SliceStable(trips, func(i, j int) bool {
		a, b := trips[i], trips[j]
		af, bf := a.StartDate >= today, b.StartDate >= today
		if af != bf {
			return af
		}
		if af {
			return a.StartDate < b.StartDate
		}
		return a.StartDate > b.StartDate
	})
	u.visibleTripIDs = nil
	row := 0
	for _, t := range trips {
		if !tripMatchesFilter(t, q, startFilter, endFilter) {
			continue
		}
		u.visibleTripIDs = append(u.visibleTripIDs, t.ID)
		vals := []string{displayDate(t.StartDate), displayDate(t.EndDate), t.Title, t.Companions}
		p := wstr(vals[0])
		it := LVITEM{Mask: LVIF_TEXT, IItem: int32(row), PszText: p}
		send(u.tripList, LVM_INSERTITEMW, 0, uintptr(unsafe.Pointer(&it)))
		for s := 1; s < 4; s++ {
			pp := wstr(vals[s])
			li := LVITEM{ISubItem: int32(s), PszText: pp}
			send(u.tripList, LVM_SETITEMTEXTW, uintptr(row), uintptr(unsafe.Pointer(&li)))
		}
		if u.mode != modeNew && t.ID == u.selectedTripID {
			state := LVITEM{StateMask: LVIS_SELECTED | LVIS_FOCUSED, State: LVIS_SELECTED | LVIS_FOCUSED}
			send(u.tripList, LVM_SETITEMSTATE, uintptr(row), uintptr(unsafe.Pointer(&state)))
		}
		row++
	}
}

func (u *UI) applySearch() {
	q := strings.TrimSpace(getText(u.search))
	start := getDatePickerISO(u.searchStart)
	end := getDatePickerISO(u.searchEnd)
	if start != "" && end != "" && start > end {
		uiLog("SEARCH rejected start-after-end start=%s end=%s", start, end)
		msgBox(u.hwnd, "日期區間的開始日不可晚於結束日。", "日期查詢", MB_OK|MB_ICONWARNING)
		return
	}
	u.searchQueryApplied = q
	u.searchStartApplied = start
	u.searchEndApplied = end
	uiLog("SEARCH apply query=%q start=%q end=%q", q, start, end)
	u.renderTripList()
}

func (u *UI) activatePanePool(pane uintptr) {
	if pane == 0 {
		return
	}
	if u.panePools == nil {
		u.panePools = map[uintptr]map[string][]pooledCtrl{}
	}
	if u.panePoolUsed == nil {
		u.panePoolUsed = map[uintptr]map[string]int{}
	}
	if u.panePools[pane] == nil {
		u.panePools[pane] = map[string][]pooledCtrl{}
	}
	if u.panePoolUsed[pane] == nil {
		u.panePoolUsed[pane] = map[string]int{}
	}
	u.controlPool = u.panePools[pane]
	u.poolUsed = u.panePoolUsed[pane]
}

func (u *UI) paneForMode() uintptr {
	switch u.mode {
	case modeNew, modeEdit:
		return u.editPane
	case modeView:
		if u.selectedTrip() != nil {
			return u.viewPane
		}
		return u.emptyPane
	default:
		return u.emptyPane
	}
}

func (u *UI) switchRightPane(pane uintptr) {
	if pane == 0 {
		return
	}
	for _, p := range []uintptr{u.emptyPane, u.viewPane, u.editPane} {
		if p != 0 && p != pane {
			pShowWindow.Call(p, SW_HIDE)
		}
	}
	u.content = pane
	u.activatePanePool(pane)
}

func (u *UI) clearForm() {
	uiLog("UI resetForm begin activeControls=%d pooledKinds=%d", len(u.controls), len(u.controlPool))
	// Each right-side pane owns an independent control pool in V0.8.0.  We only hide
	// controls belonging to the pane being rebuilt; controls in the other two panes are
	// untouched.  In particular, the welcome bitmap stays attached to its STATIC control
	// while the editor is visible, so Cancel can reveal it again without reconstructing it.
	for _, pool := range u.controlPool {
		for _, pc := range pool {
			if pc.hwnd != 0 {
				pShowWindow.Call(pc.hwnd, SW_HIDE)
			}
		}
	}
	// Clear event bindings, then let renderRight refill the same HWNDs with the newly selected
	// trip.  This keeps the stable control pool while guaranteeing a clean visual frame.
	u.editBindings = map[uint16]editBinding{}
	u.comboBindings = map[uint16]comboBinding{}
	u.buttonActions = map[uint16]func(){}
	u.dateBindings = map[uint16]dateBinding{}
	u.buttonStyles = map[uint16]string{ID_SEARCH_GO: "primary", ID_SETTINGS_SAVE: "primary", ID_SETTINGS_CANCEL: "secondary", ID_NEW_TRIP: "toolbar", ID_MODIFY_TRIP: "toolbar", ID_SAVE_TRIP: "toolbar", ID_DELETE_TRIP: "toolbar", ID_EXPORT: "toolbar"}
	u.drawLabels = nil
	u.drawCards = nil
	u.drawSubCards = nil
	u.drawFrames = nil
	u.drawGrids = nil
	u.drawInputs = nil
	u.listBindings = map[uint16]listBinding{}
	u.noteKeys = map[uintptr]func(int){}
	u.drawEmptyArt = false
	u.openCard = -1
	u.controls = nil
	if u.poolUsed == nil {
		u.poolUsed = map[string]int{}
	}
	for k := range u.poolUsed {
		u.poolUsed[k] = 0
	}
	u.poolCreatedPass = 0
	u.poolReusedPass = 0
	uiLog("UI resetForm complete; HWNDs retained for reuse")
}

func (u *UI) takePooled(kind string, create func(id uint16) uintptr) (uintptr, uint16, bool) {
	// Cell editors have their own lifetime and never become form-pool entries.
	if u.openingCell {
		id := uint16(1901) // Reserved slot; repeated cell edits cannot exhaust uint16 IDs.
		hw := create(id)
		u.cellEditor = pooledCtrl{hwnd: hw, id: id, kind: kind}
		return hw, id, true
	}
	if u.controlPool == nil {
		u.controlPool = map[string][]pooledCtrl{}
	}
	if u.poolUsed == nil {
		u.poolUsed = map[string]int{}
	}
	idx := u.poolUsed[kind]
	u.poolUsed[kind] = idx + 1
	pool := u.controlPool[kind]
	if idx < len(pool) {
		pc := pool[idx]
		pShowWindow.Call(pc.hwnd, SW_SHOW)
		enable(pc.hwnd, true)
		u.poolReusedPass++
		return pc.hwnd, pc.id, false
	}
	id := u.nextID()
	hw := create(id)
	pc := pooledCtrl{hwnd: hw, id: id, kind: kind}
	u.controlPool[kind] = append(pool, pc)
	u.poolCreatedPass++
	return hw, id, true
}

func (u *UI) disableActiveFormInputs() {
	for kind, pool := range u.controlPool {
		if kind == "label" || kind == "line" {
			continue
		}
		used := u.poolUsed[kind]
		if used > len(pool) {
			used = len(pool)
		}
		for i := 0; i < used; i++ {
			enable(pool[i].hwnd, false)
		}
	}
}
func (u *UI) addCtrl(hwnd uintptr, x, y, w, h int) {
	u.controls = append(u.controls, ctrlPos{hwnd, x, y, w, h})
	move(hwnd, x, y, w, h)
}
func (u *UI) addBitmap(bmp uintptr, x, y, w, h int) uintptr {
	hw, _, _ := u.takePooled("bitmap", func(id uint16) uintptr {
		return createWindow(0, "STATIC", "", WS_CHILD|WS_VISIBLE|0x000E, x, y, w, h, u.content, uintptr(id), u.inst)
	})
	if bmp != 0 {
		send(hw, 0x0172, IMAGE_BITMAP, bmp)
	}
	u.addCtrl(hw, x, y, w, h)
	return hw
}

func (u *UI) addLabel(text string, x, y, w, h int, bold bool) uintptr {
	font := u.font
	color := clrMuted
	if bold {
		font = u.fontBold
		color = clrText
	}
	u.drawLabels = append(u.drawLabels, drawLabelItem{text: text, x: x, y: y, w: w, h: h, font: font, color: color, flags: DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX | DT_END_ELLIPSIS})
	return 0
}
func (u *UI) addLine(y int, w int) {
	// V0.6.1 uses whitespace and painted cards instead of classic etched separators.
}

func naturalEditHeight(hwnd, font uintptr) int {
	// A single-line Win32 EDIT has no vertical-centering style.  The most native
	// approach is to keep the control close to the font's own natural height.
	// GetTextExtentPoint32W follows the selected HFONT and therefore also tracks DPI.
	hdc, _, _ := pGetDC.Call(hwnd)
	if hdc == 0 {
		return 22
	}
	defer pReleaseDC.Call(hwnd, hdc)
	old, _, _ := pSelectObject.Call(hdc, font)
	if old != 0 {
		defer pSelectObject.Call(hdc, old)
	}
	ws := syscall.StringToUTF16("國Hg")
	var sz SIZE
	if len(ws) > 1 {
		if r, _, _ := pGetTextExtentPoint32W.Call(hdc, uintptr(unsafe.Pointer(&ws[0])), uintptr(len(ws)-1), uintptr(unsafe.Pointer(&sz))); r != 0 && sz.Cy > 0 {
			// 6px total breathing room including the native 1px border is enough to
			// look centered without turning the field into a tall white box.
			return maxi(20, int(sz.Cy)+6)
		}
	}
	return 22
}

func (u *UI) addEditWithFont(text string, x, y, w, h int, multi bool, font uintptr, onChange, onBlur func(string)) uintptr {
	kind := "edit"
	style := uint32(WS_CHILD | WS_VISIBLE | WS_TABSTOP | WS_BORDER | ES_AUTOHSCROLL)
	actualX, actualY, actualW, actualH := x, y, w, h
	if multi {
		kind = "editmulti"
		style = WS_CHILD | WS_VISIBLE | WS_TABSTOP | WS_BORDER | ES_MULTILINE | ES_AUTOVSCROLL | ES_WANTRETURN
	} else {
		actualH = naturalEditHeight(u.content, font)
		if h > actualH {
			actualY = y + (h-actualH)/2
		}
	}
	hnd, id, fresh := u.takePooled(kind, func(id uint16) uintptr {
		return createWindow(0, "EDIT", "", style, actualX, actualY, actualW, actualH, u.content, uintptr(id), u.inst)
	})
	setFont(hnd, font)
	setWindowTheme(hnd, "Explorer")
	setText(hnd, text)
	if fresh && !multi {
		pSetWindowSubclass.Call(hnd, editSubclassProcCB, 1, uintptr(id))
	}
	u.editBindings[id] = editBinding{onChange, onBlur}
	u.addCtrl(hnd, actualX, actualY, actualW, actualH)
	return hnd
}

func (u *UI) addEdit(text string, x, y, w, h int, multi bool, onChange, onBlur func(string)) uintptr {
	return u.addEditWithFont(text, x, y, w, h, multi, u.fontInput, onChange, onBlur)
}

func (u *UI) addEditCue(text, cue string, x, y, w, h int, multi bool, onChange, onBlur func(string)) uintptr {
	hw := u.addEdit(text, x, y, w, h, multi, onChange, onBlur)
	if cue != "" {
		setCueBanner(hw, cue)
	}
	return hw
}
func (u *UI) addTitleEditCue(text, cue string, x, y, w, h int, onChange func(string)) uintptr {
	hw := u.addEditWithFont(text, x, y, w, h, false, u.fontTitle, onChange, nil)
	if cue != "" {
		setCueBanner(hw, cue)
	}
	return hw
}

func (u *UI) addButton(text string, x, y, w, h int, fn func()) uintptr {
	return u.addButtonKind("button", text, x, y, w, h, fn)
}
func buttonVisualStyle(text, kind string) string {
	if kind == "tab" || strings.HasPrefix(text, "● ") {
		return "tab"
	}
	if strings.Contains(text, "刪除") || text == "×" {
		return "danger"
	}
	if strings.HasPrefix(text, "＋") || strings.Contains(text, "新增") {
		return "add"
	}
	if text == "↑" || text == "↓" || text == "←" || text == "→" {
		return "ghost"
	}
	return "secondary"
}

func (u *UI) addButtonKind(kind, text string, x, y, w, h int, fn func()) uintptr {
	hw, id, _ := u.takePooled(kind, func(id uint16) uintptr {
		return createWindow(0, "BUTTON", "", WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_OWNERDRAW, x, y, w, h, u.content, uintptr(id), u.inst)
	})
	setText(hw, text)
	setFont(hw, u.font)
	u.buttonActions[id] = fn
	u.buttonStyles[id] = buttonVisualStyle(text, kind)
	u.addCtrl(hw, x, y, w, h)
	return hw
}
func (u *UI) addCheck(text string, checked bool, x, y, w, h int, fn func(bool)) uintptr {
	hw, id, _ := u.takePooled("check", func(id uint16) uintptr {
		return createWindow(0, "BUTTON", "", WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_OWNERDRAW, x, y, w, h, u.content, uintptr(id), u.inst)
	})
	setText(hw, text)
	setFont(hw, u.font)
	if checked {
		u.buttonStyles[id] = "checkboxOn"
	} else {
		u.buttonStyles[id] = "checkboxOff"
	}
	u.buttonActions[id] = func() { fn(!checked) }
	u.addCtrl(hw, x, y, w, h)
	return hw
}

func (u *UI) addCombo(text string, items []string, x, y, w, h int, editable bool, fn func(string)) uintptr {
	kind := "comboList"
	style := uint32(WS_CHILD | WS_VISIBLE | WS_TABSTOP | WS_VSCROLL | CBS_DROPDOWNLIST)
	if editable {
		kind = "comboEdit"
		style = WS_CHILD | WS_VISIBLE | WS_TABSTOP | WS_VSCROLL | CBS_DROPDOWN | CBS_AUTOHSCROLL
	}
	hw, id, _ := u.takePooled(kind, func(id uint16) uintptr {
		return createWindow(0, "COMBOBOX", "", style, x, y, w, h, u.content, uintptr(id), u.inst)
	})
	setFont(hw, u.fontInput)
	setWindowTheme(hw, "Explorer")
	send(hw, CB_RESETCONTENT, 0, 0)
	selected := -1
	for i, it := range items {
		send(hw, CB_ADDSTRING, 0, uintptr(unsafe.Pointer(wstr(it))))
		if it == text {
			selected = i
		}
	}
	if editable {
		setText(hw, text)
	} else if selected >= 0 {
		send(hw, CB_SETCURSEL, uintptr(selected), 0)
	} else {
		send(hw, CB_SETCURSEL, ^uintptr(0), 0)
	}
	u.comboBindings[id] = comboBinding{fn: fn}
	u.addCtrl(hw, x, y, w, h)
	return hw
}
func (u *UI) addRadio(text string, checked bool, group bool, x, y, w, h int, fn func()) uintptr {
	kind := "radio"
	if group {
		kind = "radioGroup"
	}
	hw, id, _ := u.takePooled(kind, func(id uint16) uintptr {
		style := uint32(WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_AUTORADIOBUTTON)
		if group {
			style |= WS_GROUP
		}
		return createWindow(0, "BUTTON", "", style, x, y, w, h, u.content, uintptr(id), u.inst)
	})
	setText(hw, text)
	setFont(hw, u.font)
	setWindowTheme(hw, "Explorer")
	if checked {
		send(hw, BM_SETCHECK, BST_CHECKED, 0)
	} else {
		send(hw, BM_SETCHECK, BST_UNCHECKED, 0)
	}
	u.buttonActions[id] = fn
	u.addCtrl(hw, x, y, w, h)
	return hw
}
func (u *UI) addButtonTip(text, tip string, x, y, w, h int, fn func()) uintptr {
	kind := "buttonTip:" + tip
	hw, _, fresh := u.takePooled(kind, func(id uint16) uintptr {
		return createWindow(0, "BUTTON", "", WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_OWNERDRAW, x, y, w, h, u.content, uintptr(id), u.inst)
	})
	// Recover the fixed id from the pooled entry used for this kind.
	idx := u.poolUsed[kind] - 1
	id := u.controlPool[kind][idx].id
	setText(hw, text)
	setFont(hw, u.font)
	u.buttonActions[id] = fn
	u.buttonStyles[id] = buttonVisualStyle(text, kind)
	if fresh {
		addTooltip(u.hwnd, hw, tip, u.inst)
	}
	u.addCtrl(hw, x, y, w, h)
	return hw
}
func (u *UI) addDatePicker(value string, x, y, w, h int, allowBlank bool, fn func(string)) uintptr {
	kind := "dateRequired"
	style := uint32(WS_CHILD | WS_VISIBLE | WS_TABSTOP | DTS_SHORTDATEFORMAT)
	if allowBlank {
		kind = "dateOptional"
		style |= DTS_SHOWNONE
	}
	actualY, actualH := y, h
	if actualH > 24 {
		actualY += (actualH - 24) / 2
		actualH = 24
	}
	hw, id, _ := u.takePooled(kind, func(id uint16) uintptr {
		return createWindow(0, "SysDateTimePick32", "", style, x, actualY, w, actualH, u.content, uintptr(id), u.inst)
	})
	setFont(hw, u.fontInput)
	setDatePickerFormat(hw, "yyyy/MM/dd")
	if value == "" {
		setDatePickerNone(hw)
	} else {
		setDatePickerISO(hw, value)
	}
	setWindowTheme(hw, "Explorer")
	u.dateBindings[id] = dateBinding{fn: fn}
	u.addCtrl(hw, x, actualY, w, actualH)
	return hw
}

func (u *UI) addComboCommit(text string, items []string, x, y, w, h int, editable bool, fn func(string)) uintptr {
	kind := "comboCommitList"
	style := uint32(WS_CHILD | WS_VISIBLE | WS_TABSTOP | WS_VSCROLL | CBS_DROPDOWNLIST)
	if editable {
		kind = "comboCommitEdit"
		style = WS_CHILD | WS_VISIBLE | WS_TABSTOP | WS_VSCROLL | CBS_DROPDOWN | CBS_AUTOHSCROLL
	}
	hw, id, _ := u.takePooled(kind, func(id uint16) uintptr {
		return createWindow(0, "COMBOBOX", "", style, x, y, w, h, u.content, uintptr(id), u.inst)
	})
	setFont(hw, u.fontInput)
	setWindowTheme(hw, "Explorer")
	send(hw, CB_RESETCONTENT, 0, 0)
	selected := -1
	for i, it := range items {
		send(hw, CB_ADDSTRING, 0, uintptr(unsafe.Pointer(wstr(it))))
		if it == text {
			selected = i
		}
	}
	if editable {
		setText(hw, text)
	} else if selected >= 0 {
		send(hw, CB_SETCURSEL, uintptr(selected), 0)
	}
	u.comboBindings[id] = comboBinding{fn: fn, blurOnly: editable}
	u.addCtrl(hw, x, y, w, h)
	return hw
}

func measuredDataListHeight(hwnd uintptr, rows int, hasHScroll bool) int {
	headerH := 24
	if hdr := send(hwnd, LVM_GETHEADER, 0, 0); hdr != 0 {
		var wr RECT
		if r, _, _ := pGetWindowRect.Call(hdr, uintptr(unsafe.Pointer(&wr))); r != 0 {
			if h := int(wr.Bottom - wr.Top); h > 0 {
				headerH = h
			}
		}
	}
	rowH := 24
	if rows > 0 {
		r := RECT{Left: LVIR_BOUNDS}
		if send(hwnd, LVM_GETITEMRECT, 0, uintptr(unsafe.Pointer(&r))) != 0 {
			if h := int(r.Bottom - r.Top); h > 0 {
				rowH = h
			}
		}
	}
	extra := 6 // native border + one safety pixel so the last row never clips
	if hasHScroll {
		if sh, _, _ := pGetSystemMetrics.Call(SM_CYHSCROLL); sh > 0 {
			extra += int(sh)
		} else {
			extra += 17
		}
	}
	return headerH + rowH*rows + extra
}

func sumInts(v []int) int {
	t := 0
	for _, x := range v {
		t += x
	}
	return t
}

func distributeExtra(widths, preferred, flex []int, extra int) {
	if extra <= 0 {
		return
	}
	// First restore useful preferred widths before stretching flexible text columns.
	for i := range widths {
		if extra <= 0 {
			break
		}
		want := preferred[i] - widths[i]
		if want <= 0 {
			continue
		}
		add := mini(want, extra)
		widths[i] += add
		extra -= add
	}
	if extra <= 0 {
		return
	}
	totalWeight := 0
	for _, w := range flex {
		if w > 0 {
			totalWeight += w
		}
	}
	if totalWeight == 0 {
		widths[len(widths)-1] += extra
		return
	}
	remaining := extra
	for i, weight := range flex {
		if weight <= 0 {
			continue
		}
		add := extra * weight / totalWeight
		widths[i] += add
		remaining -= add
	}
	for i := range widths {
		if remaining <= 0 {
			break
		}
		if flex[i] > 0 {
			widths[i]++
			remaining--
		}
	}
}

func (u *UI) dataListWidths(headers []string, preferred, flex []int, rows [][]string, available int) ([]int, bool) {
	widths := make([]int, len(headers))
	for i, h := range headers {
		// Minimum width is driven by the actual visible text, so resizing never turns
		// saved values into ellipses merely to avoid a horizontal scrollbar.
		mw := measureTextWidth(u.content, u.fontInput, h) + 18
		for _, row := range rows {
			if i < len(row) {
				cw := measureTextWidth(u.content, u.fontInput, row[i]) + 18
				if cw > mw {
					mw = cw
				}
			}
		}
		if i == len(headers)-1 {
			mw = maxi(mw, 42)
		}
		widths[i] = maxi(42, mw)
	}
	minTotal := sumInts(widths)
	hasHScroll := minTotal > available-4
	if !hasHScroll {
		distributeExtra(widths, preferred, flex, maxi(0, available-4-minTotal))
	}
	return widths, hasHScroll
}

func (u *UI) addDataList(kind string, headers []string, preferred []int, flex []int, rows [][]string, x, y, w, h int, bind listBinding) (uintptr, int) {
	hw, id, fresh := u.takePooled(kind, func(id uint16) uintptr {
		return createWindow(0, "SysListView32", "", WS_CHILD|WS_VISIBLE|WS_TABSTOP|WS_BORDER|LVS_REPORT|LVS_SINGLESEL, x, y, w, h, u.content, uintptr(id), u.inst)
	})
	setFont(hw, u.fontInput)
	setWindowTheme(hw, "Explorer")
	if hdr := send(hw, LVM_GETHEADER, 0, 0); hdr != 0 {
		setFont(hdr, u.fontInput)
	}
	send(hw, LVM_SETEXTENDEDLISTVIEWSTYLE, 0, LVS_EX_FULLROWSELECT|LVS_EX_DOUBLEBUFFER|LVS_EX_GRIDLINES)
	widths, hasHScroll := u.dataListWidths(headers, preferred, flex, rows, w)
	if fresh {
		for i, title := range headers {
			cw := wstr(title)
			col := LVCOLUMN{Mask: LVCF_TEXT | LVCF_WIDTH, Cx: int32(widths[i]), PszText: cw}
			send(hw, LVM_INSERTCOLUMNW, uintptr(i), uintptr(unsafe.Pointer(&col)))
		}
	} else {
		for i, cw := range widths {
			send(hw, LVM_SETCOLUMNWIDTH, uintptr(i), uintptr(cw))
		}
	}
	send(hw, LVM_DELETEALLITEMS, 0, 0)
	for ri, row := range rows {
		if len(row) == 0 {
			continue
		}
		it := LVITEM{Mask: LVIF_TEXT, IItem: int32(ri), PszText: wstr(row[0])}
		send(hw, LVM_INSERTITEMW, 0, uintptr(unsafe.Pointer(&it)))
		for ci := 1; ci < len(row); ci++ {
			li := LVITEM{ISubItem: int32(ci), PszText: wstr(row[ci])}
			send(hw, LVM_SETITEMTEXTW, uintptr(ri), uintptr(unsafe.Pointer(&li)))
		}
	}
	actualH := measuredDataListHeight(hw, len(rows), hasHScroll)
	bind.x, bind.y, bind.w, bind.h = x, y, w, actualH
	bind.columns = append([]string(nil), headers...)
	if u.listBindings == nil {
		u.listBindings = map[uint16]listBinding{}
	}
	u.listBindings[id] = bind
	u.addCtrl(hw, x, y, w, actualH)
	return hw, actualH
}

func (u *UI) customDrawDataList(lParam uintptr) uintptr {
	cd := (*NMLVCUSTOMDRAW)(unsafe.Pointer(lParam))
	switch cd.Nmcd.DwDrawStage {
	case CDDS_PREPAINT:
		return CDRF_NOTIFYITEMDRAW
	case CDDS_ITEMPREPAINT:
		cd.Nmcd.UItemState &^= CDIS_SELECTED | 0x10 | 0x40
		return CDRF_NOTIFYSUBITEMDRAW
	case CDDS_ITEMPREPAINT | CDDS_SUBITEM:
		cd.Nmcd.UItemState &^= CDIS_SELECTED | 0x10 | 0x40
		idx := int(cd.Nmcd.DwItemSpec)
		// These data lists are editors, not selection-driven lists.  Keep the native
		// ListView/grid behavior but do not leave a dark full-row selection painted
		// over the editable data; double-click/cell actions remain the interaction.
		bg := clrListBase
		text := clrText
		if idx%2 == 1 {
			bg = clrGridAlt
		}
		cd.ClrTextBk = bg
		cd.ClrText = text

		// The operation column is still part of the native ListView; only the tiny
		// delete affordance is custom-painted so it remains crisp at 18-20 px.
		if b, ok := u.listBindings[uint16(cd.Nmcd.Hdr.IdFrom)]; ok && int(cd.ISubItem) == len(b.columns)-1 {
			deletable := (b.kind == "hotel" && len(b.rowIDs) > 1) || (b.kind == "flight" && len(b.rowIDs) > 2 && idx > 0 && idx < len(b.rowIDs)-1)
			if deletable {
				r := cd.Nmcd.Rc
				fillRectColor(cd.Nmcd.Hdc, r, bg)
				bw, bh := int32(20), int32(18)
				cx := (r.Left + r.Right) / 2
				cy := (r.Top + r.Bottom) / 2
				br := RECT{cx - bw/2, cy - bh/2, cx + bw/2, cy + bh/2}
				drawRoundRect(cd.Nmcd.Hdc, br, 4, clrDangerSoft, clrDanger)
				drawText(cd.Nmcd.Hdc, "×", br, u.font, clrDanger, DT_CENTER|DT_VCENTER|DT_SINGLELINE|DT_NOPREFIX)
				// Recreate the two grid edges that default painting would otherwise draw.
				drawLine(cd.Nmcd.Hdc, int(r.Right)-1, int(r.Top), int(r.Right)-1, int(r.Bottom), clrGridLine, 1)
				drawLine(cd.Nmcd.Hdc, int(r.Left), int(r.Bottom)-1, int(r.Right), int(r.Bottom)-1, clrGridLine, 1)
				return CDRF_SKIPDEFAULT
			}
		}
		return CDRF_NEWFONT
	}
	return CDRF_DODEFAULT
}

func listSubItemRect(hwnd uintptr, item, sub int) (RECT, bool) {
	r := RECT{Left: LVIR_BOUNDS, Top: int32(sub)}
	ok := send(hwnd, LVM_GETSUBITEMRECT, uintptr(item), uintptr(unsafe.Pointer(&r))) != 0
	if ok && sub == 0 {
		// LVIR_BOUNDS for subitem zero spans the entire row, not column zero.
		r.Right = r.Left + int32(send(hwnd, LVM_GETCOLUMNWIDTH, 0, 0))
	}
	return r, ok
}

// Run blur commits before Save or a layout rebuild, without allowing a nested
// render to recycle the controls currently dispatching their notifications.
func (u *UI) settleEditorFocus() {
	if u.settlingFocus || u.rendering || !u.editing {
		return
	}
	u.settlingFocus = true
	defer func() { u.settlingFocus = false }()
	pSetFocus.Call(u.content)
}

func (u *UI) closeCellEditor() {
	pc := u.cellEditor
	u.cellEditor = pooledCtrl{}
	if pc.hwnd == 0 {
		return
	}
	// Remove callbacks before destruction can send EN_KILLFOCUS.
	delete(u.editBindings, pc.id)
	delete(u.comboBindings, pc.id)
	delete(u.dateBindings, pc.id)
	for i := len(u.controls) - 1; i >= 0; i-- {
		if u.controls[i].hwnd == pc.hwnd {
			u.controls = append(u.controls[:i], u.controls[i+1:]...)
		}
	}
	destroy(pc.hwnd)
}

func (u *UI) openDataListCell(id uint16, hwnd uintptr, item, sub int) {
	if !u.editing || u.workingTrip == nil || u.rendering {
		return
	}
	u.settleEditorFocus()
	u.closeCellEditor()
	u.openingCell = true
	// A previous blur may have queued a reflow; do not let it hide the new editor.
	pKillTimer.Call(u.hwnd, 4)
	defer func() { u.openingCell = false }()
	b, ok := u.listBindings[id]
	if !ok || item < 0 || item >= len(b.rowIDs) || sub < 0 || sub >= len(b.columns) {
		return
	}
	r, ok := listSubItemRect(hwnd, item, sub)
	if !ok {
		uiLog("UI list editor rect failed id=%d item=%d sub=%d", id, item, sub)
		return
	}
	pMapWindowPoints.Call(hwnd, u.content, uintptr(unsafe.Pointer(&r)), 2)
	x, y := int(r.Left)+1, int(r.Top)+1
	w, h := int(r.Right-r.Left)-2, int(r.Bottom-r.Top)-2
	uiLog("UI list editor open kind=%s id=%d item=%d sub=%d cell=%dx%d at=%d,%d", b.kind, id, item, sub, w, h, x, y)
	defer pInvalidateRect.Call(u.content, 0, 1)
	if w < 24 || h < 10 {
		uiLog("UI list editor skipped small cell kind=%s item=%d sub=%d cell=%dx%d", b.kind, item, sub, w, h)
		return
	}
	rowID := b.rowIDs[item]
	if b.kind == "flight" {
		t := u.activeTrip()
		if t == nil || t.ID != b.tripID {
			return
		}
		seg := findSeg(t, b.groupID, rowID)
		if seg == nil || sub >= 8 {
			return
		}
		commitText := func(current string, setter func(string)) {
			hw := u.addEditCue(current, "", x, y, w, h, false, nil, func(v string) {
				setter(v)
				u.saveNow()
				u.queueRender()
			})
			raiseChild(hw)
			pSetFocus.Call(hw)
		}
		switch sub {
		case 0:
			commitText(seg.Label, func(v string) {
				if s := findSeg(t, b.groupID, rowID); s != nil {
					s.Label = v
				}
			})
		case 1:
			hw := u.addDatePicker(seg.Date, x, y, w, h, false, func(v string) {
				if s := findSeg(t, b.groupID, rowID); s != nil {
					s.Date = v
				}
				u.queueRender()
			})
			raiseChild(hw)
			pSetFocus.Call(hw)
		case 2:
			commitText(seg.FlightNo, func(v string) {
				if s := findSeg(t, b.groupID, rowID); s != nil {
					s.FlightNo = v
				}
			})
		case 3:
			commitText(seg.Airline, func(v string) {
				if s := findSeg(t, b.groupID, rowID); s != nil {
					s.Airline = v
				}
			})
		case 4:
			hw := u.addComboCommit(seg.From, airportChoices(), x, y, w, 180, true, func(v string) {
				if s := findSeg(t, b.groupID, rowID); s != nil {
					s.From = v
				}
				u.queueRender()
			})
			raiseChild(hw)
			pSetFocus.Call(hw)
		case 5:
			current := seg.Depart
			commitText(current, func(v string) {
				if nv, ok := normalizeTimeInput(v); ok {
					if s := findSeg(t, b.groupID, rowID); s != nil {
						s.Depart = nv
					}
					u.queueRender()
				} else {
					msgBox(u.hwnd, "時間格式錯誤，請使用 24 小時制，例如 0930 或 09:30。", "時間錯誤", MB_OK|MB_ICONWARNING)
				}
			})
		case 6:
			hw := u.addComboCommit(seg.To, airportChoices(), x, y, w, 180, true, func(v string) {
				if s := findSeg(t, b.groupID, rowID); s != nil {
					s.To = v
				}
				u.queueRender()
			})
			raiseChild(hw)
			pSetFocus.Call(hw)
		case 7:
			current := flightArrivalDisplay(*seg)
			commitText(current, func(v string) {
				if nv, mode, ok := parseArrivalEditorValue(v); ok {
					if s := findSeg(t, b.groupID, rowID); s != nil {
						s.Arrive = nv
						if mode != nil {
							s.OffsetMode = *mode
						}
					}
					u.queueRender()
				} else {
					msgBox(u.hwnd, "時間格式錯誤，請使用 24 小時制，例如 1530、15:30；如需手動跨日可輸入 15:30 (+1)。", "時間錯誤", MB_OK|MB_ICONWARNING)
				}
			})
		}
		return
	}
	if b.kind == "hotel" {
		t := u.activeTrip()
		if t == nil || t.ID != b.tripID {
			return
		}
		p := planByID(t, b.planID)
		if p == nil {
			return
		}
		hotel := hotelByID(p, rowID)
		if hotel == nil || sub >= 9 {
			return
		}
		commitText := func(current string, setter func(string)) {
			hw := u.addEditCue(current, "", x, y, w, h, false, nil, func(v string) { setter(v); u.saveNow(); u.queueRender() })
			raiseChild(hw)
			pSetFocus.Call(hw)
		}
		switch sub {
		case 0:
			commitText(hotel.Name, func(v string) {
				if hh := hotelByID(p, rowID); hh != nil {
					hh.Name = v
				}
			})
		case 1:
			hw := u.addDatePicker(hotel.CheckIn, x, y, w, h, false, func(v string) {
				if hh := hotelByID(p, rowID); hh != nil {
					hh.CheckIn = v
				}
				u.queueRender()
			})
			raiseChild(hw)
			pSetFocus.Call(hw)
		case 2:
			hw := u.addDatePicker(hotel.CheckOut, x, y, w, h, false, func(v string) {
				if hh := hotelByID(p, rowID); hh != nil {
					hh.CheckOut = v
				}
				u.queueRender()
			})
			raiseChild(hw)
			pSetFocus.Call(hw)
		case 3:
			commitText(roomsText(hotel.Rooms), func(v string) {
				if hh := hotelByID(p, rowID); hh != nil {
					hh.Rooms = parseRoomsSummary(v)
				}
			})
		case 4:
			hw := u.addComboCommit(hotel.Breakfast, []string{"", "含早餐", "不含早餐"}, x, y, w, 120, false, func(v string) {
				if hh := hotelByID(p, rowID); hh != nil {
					hh.Breakfast = v
				}
				u.queueRender()
			})
			raiseChild(hw)
			pSetFocus.Call(hw)
		case 5:
			hw := u.addComboCommit(hotel.Currency, []string{"TWD", "JPY", "USD", "EUR"}, x, y, w, 120, false, func(v string) {
				if hh := hotelByID(p, rowID); hh != nil {
					hh.Currency = v
				}
				u.queueRender()
			})
			raiseChild(hw)
			pSetFocus.Call(hw)
		case 6:
			commitText(hotel.Price, func(v string) {
				if hh := hotelByID(p, rowID); hh != nil {
					hh.Price = v
				}
			})
		case 7:
			commitText(hotel.TWDPrice, func(v string) {
				if hh := hotelByID(p, rowID); hh != nil {
					hh.TWDPrice = v
				}
			})
		case 8:
			commitText(hotel.Note, func(v string) {
				if hh := hotelByID(p, rowID); hh != nil {
					hh.Note = v
				}
			})
		}
	}
}

func (u *UI) handleDataListClick(id uint16, hwnd uintptr, item, sub int, pt POINT) bool {
	b, ok := u.listBindings[id]
	if !ok || item < 0 || item >= len(b.rowIDs) {
		return false
	}
	if sub == len(b.columns)-1 {
		r, ok := listSubItemRect(hwnd, item, sub)
		if !ok {
			return false
		}
		cx := (r.Left + r.Right) / 2
		cy := (r.Top + r.Bottom) / 2
		button := RECT{cx - 10, cy - 9, cx + 10, cy + 9}
		if pt.X < button.Left || pt.X > button.Right || pt.Y < button.Top || pt.Y > button.Bottom {
			return false
		}
	}
	if b.kind == "flight" && sub == len(b.columns)-1 {
		t := u.activeTrip()
		if t == nil || t.ID != b.tripID {
			return true
		}
		g := flightGroupByID(t, b.groupID)
		if g == nil || len(g.Segments) <= 2 || item == 0 || item == len(g.Segments)-1 {
			return true
		}
		rowID := b.rowIDs[item]
		for i := range g.Segments {
			if g.Segments[i].ID == rowID {
				g.Segments = append(g.Segments[:i], g.Segments[i+1:]...)
				break
			}
		}
		u.saveNow()
		u.renderRight()
		return true
	}
	if b.kind == "hotel" && sub == len(b.columns)-1 {
		t := u.activeTrip()
		if t == nil || t.ID != b.tripID {
			return true
		}
		p := planByID(t, b.planID)
		if p == nil || len(p.Hotels) <= 1 {
			return true
		}
		rowID := b.rowIDs[item]
		for i := range p.Hotels {
			if p.Hotels[i].ID == rowID {
				p.Hotels = append(p.Hotels[:i], p.Hotels[i+1:]...)
				break
			}
		}
		u.saveNow()
		u.renderRight()
		return true
	}
	return false
}

func (u *UI) closeOpenCard(bottom int) {
	if u.openCard >= 0 && u.openCard < len(u.drawCards) {
		c := &u.drawCards[u.openCard]
		if bottom < c.y+76 {
			bottom = c.y + 76
		}
		c.h = bottom - c.y
	}
	u.openCard = -1
}

func (u *UI) section(title string, y int, w int) int {
	u.closeOpenCard(y - 10)
	// Section accents follow the agreed red → orange → yellow → green → blue rhythm.
	accent := rgb(235, 76, 85)
	switch title {
	case "航班":
		accent = rgb(239, 119, 70)
	case "住宿":
		accent = rgb(239, 175, 20)
	case "每日行程":
		accent = rgb(76, 170, 118)
	case "旅程備註":
		accent = rgb(48, 119, 190)
	}
	cardX := 18
	cardW := w - 36
	if u.planFrameActive {
		cardX = 28
		cardW = w - 56
	}
	u.drawCards = append(u.drawCards, drawCardItem{x: cardX, y: y, w: cardW, h: 90, accent: accent})
	u.openCard = len(u.drawCards) - 1
	glyph := ""
	switch title {
	case "航班":
		glyph = "\uE709"
	case "住宿":
		glyph = "\uE80F"
	case "每日行程":
		glyph = "\uE787"
	case "旅程備註":
		glyph = "\uE8A5"
	}
	titleX := cardX + 32
	if glyph != "" {
		u.drawLabels = append(u.drawLabels, drawLabelItem{text: glyph, x: cardX + 28, y: y + 12, w: 26, h: 28, font: u.fontIcon, color: accent, flags: DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX})
		titleX = cardX + 62
	}
	u.drawLabels = append(u.drawLabels, drawLabelItem{text: title, x: titleX, y: y + 13, w: cardW - (titleX - cardX) - 24, h: 28, font: u.fontSection, color: clrText, flags: DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX})
	return y + 54
}

func (u *UI) addSubCard(x, y, w, h int) {
	if w > 0 && h > 0 {
		u.drawSubCards = append(u.drawSubCards, drawSubCardItem{x: x, y: y, w: w, h: h})
	}
}

func (u *UI) finishCards(bottom int) {
	u.closeOpenCard(bottom)
}

func (u *UI) addEmoji(text string, x, y, w, h int) {
	u.drawLabels = append(u.drawLabels, drawLabelItem{text: text, x: x, y: y, w: w, h: h, font: u.fontEmoji, color: clrText, flags: DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX})
}

func (u *UI) addViewText(text string, x, y, w, h int, bold bool, muted bool, wrap bool) {
	font := u.font
	color := clrText
	if bold {
		font = u.fontBold
	}
	if muted {
		color = clrMuted
	}
	flags := uint32(DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX | DT_END_ELLIPSIS)
	if wrap {
		flags = DT_LEFT | DT_WORDBREAK | DT_NOPREFIX
	}
	u.drawLabels = append(u.drawLabels, drawLabelItem{text: text, x: x, y: y, w: w, h: h, font: font, color: color, flags: flags})
}

func (u *UI) addViewField(label, value string, x, y, w int) {
	if strings.TrimSpace(value) == "" {
		value = "—"
	}
	u.addViewText(label, x, y, w, 18, false, true, false)
	u.addViewText(value, x, y+20, w, 24, false, false, false)
}

func (u *UI) addWrappedView(text string, x, y, w int) int {
	h := 24
	hdc, _, _ := pGetDC.Call(u.content)
	if hdc != 0 {
		old := selectObj(hdc, u.font)
		r := RECT{Right: int32(maxi(1, w))}
		ws := syscall.StringToUTF16(text)
		pDrawTextW.Call(hdc, uintptr(unsafe.Pointer(&ws[0])), uintptr(len(ws)-1), uintptr(unsafe.Pointer(&r)), DT_LEFT|DT_WORDBREAK|DT_NOPREFIX|0x400)
		h = maxi(h, int(r.Bottom-r.Top)+4)
		selectObj(hdc, old)
		pReleaseDC.Call(u.content, hdc)
	}
	u.addViewText(text, x, y, w, h, false, false, true)
	return h + 4
}

func (u *UI) renderEmptyState(W int) {
	u.scrollY = 0
	u.drawEmptyArt = false
	r := getClient(u.right)
	h := maxi(620, int(r.Bottom))
	cx := W / 2
	y := maxi(80, h/2-170)
	u.addBitmap(u.emptyBmp, cx-110, y, 220, 220)
	if u.emptyBmp == 0 {
		// Missing release assets must still leave a useful, intentional welcome page.
		u.drawLabels = append(u.drawLabels, drawLabelItem{text: "TravelPlan", x: cx - 180, y: y + 90, w: 360, h: 48, font: u.fontTitle, color: clrCoralDark, flags: DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX})
	}
	u.drawLabels = append(u.drawLabels, drawLabelItem{text: "選擇左側的旅行計劃", x: cx - 220, y: y + 236, w: 440, h: 30, font: u.fontHint, color: clrText, flags: DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX})
	u.drawLabels = append(u.drawLabels, drawLabelItem{text: "或按左上角「新增」開始規劃旅程", x: cx - 240, y: y + 270, w: 480, h: 30, font: u.fontHint, color: clrText, flags: DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX})
	u.contentH = h
	u.updateScroll()
	pInvalidateRect.Call(u.content, 0, 1)
	u.refreshToolbarState()
	uiLog("UI renderRight mode=empty-state contentH=%d", u.contentH)
}

func (u *UI) renderTripView(t *Trip, W int) {
	margin := 38
	gap := 18
	y := 28
	innerW := W - 2*margin

	// Document title: large enough to identify the trip, but no input-frame chrome in view mode.
	title := strings.TrimSpace(t.Title)
	if title == "" {
		title = "未命名旅行計劃"
	}
	u.drawLabels = append(u.drawLabels, drawLabelItem{text: title, x: margin, y: y, w: innerW, h: 36, font: u.fontSection, color: clrText, flags: DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX | DT_END_ELLIPSIS})
	y += 50

	y = u.section("基本資料", y, W)
	col := (innerW - 2*gap) / 3
	dateText := "—"
	if t.StartDate != "" || t.EndDate != "" {
		if t.SingleDay || t.EndDate == "" || t.StartDate == t.EndDate {
			dateText = displayDate(t.StartDate)
		} else {
			dateText = displayDate(t.StartDate) + " ～ " + displayDate(t.EndDate)
		}
	}
	place := strings.Trim(strings.TrimSpace(t.Country+"  "+t.Cities), " ")
	u.addViewField("日期", dateText, margin, y, col)
	u.addViewField("國家／城市地區", place, margin+col+gap, y, col)
	u.addViewField("旅伴", t.Companions, margin+2*(col+gap), y, col)
	y += 55
	u.addViewText("主要目的", margin, y, innerW, 18, false, true, false)
	purpose := strings.TrimSpace(t.Purpose)
	if purpose == "" {
		purpose = "—"
	}
	y += 21 + u.addWrappedView(purpose, margin, y+21, innerW) + 13

	y = u.section("航班", y, W)
	if len(t.Flights) == 0 {
		u.addViewText("尚無航班資料", margin, y, innerW, 24, false, true, false)
		y += 34
	} else {
		for _, g := range t.Flights {
			traveler := strings.TrimSpace(g.Traveler)
			if traveler == "" {
				traveler = "旅客"
			}
			u.addViewText(traveler, margin, y, innerW, 24, true, false, false)
			y += 28
			for _, seg := range g.Segments {
				parts := []string{}
				if strings.TrimSpace(seg.Label) != "" {
					parts = append(parts, strings.TrimSpace(seg.Label))
				}
				dt := strings.TrimSpace(displayDate(seg.Date))
				if strings.TrimSpace(seg.Depart) != "" || strings.TrimSpace(seg.Arrive) != "" {
					times := strings.TrimSpace(seg.Depart)
					arr := flightArrivalDisplay(seg)
					if times != "" && arr != "" {
						times += "–" + arr
					} else if arr != "" {
						times = arr
					}
					if dt != "" && times != "" {
						dt += " " + times
					} else if dt == "" {
						dt = times
					}
				}
				if dt != "" {
					parts = append(parts, dt)
				}
				if strings.TrimSpace(seg.Airline) != "" {
					parts = append(parts, strings.TrimSpace(seg.Airline))
				}
				if strings.TrimSpace(seg.FlightNo) != "" {
					parts = append(parts, strings.TrimSpace(seg.FlightNo))
				}
				route := strings.TrimSpace(seg.From)
				if route != "" && strings.TrimSpace(seg.To) != "" {
					route += " → " + strings.TrimSpace(seg.To)
				} else if route == "" {
					route = strings.TrimSpace(seg.To)
				}
				if route != "" {
					parts = append(parts, route)
				}
				line := strings.Join(parts, "  │  ")
				if strings.TrimSpace(line) == "" {
					line = "—"
				}
				u.addViewText(line, margin+12, y, innerW-12, 24, false, false, false)
				y += 27
			}
			y += 8
		}
	}

	p := u.activePlan()
	if len(t.Plans) > 1 {
		y = u.section("旅行方案", y, W)
		x := margin
		for i := range t.Plans {
			pid := t.Plans[i].ID
			name := t.Plans[i].Name
			if name == "" {
				name = fmt.Sprintf("方案%d", i+1)
			}
			label := name
			if pid == t.ActivePlanID {
				label = "● " + label
			}
			u.addButtonKind("tab", label, x, y, 112, 28, func() {
				t.ActivePlanID = pid
				u.scrollY = 0
				u.renderRight()
			})
			x += 120
		}
		y += 42
		p = u.activePlan()
	}

	if !t.SingleDay {
		y = u.section("住宿", y, W)
		if p == nil || len(p.Hotels) == 0 {
			u.addViewText("尚無住宿資料", margin, y, innerW, 24, false, true, false)
			y += 34
		} else {
			for _, h := range p.Hotels {
				name := strings.TrimSpace(h.Name)
				if name == "" {
					name = "未命名住宿"
				}
				date := ""
				if h.CheckIn != "" || h.CheckOut != "" {
					date = displayDate(h.CheckIn) + " ～ " + displayDate(h.CheckOut)
				}
				line := name
				if date != "" {
					line += "    " + date
				}
				if rooms := roomsText(h.Rooms); rooms != "" {
					line += "    " + rooms
				}
				u.addViewText(line, margin, y, innerW, 25, false, false, false)
				y += 29
			}
		}
	}

	y = u.section("每日行程", y, W)
	if p == nil || len(p.Days) == 0 {
		u.addViewText("尚無每日行程", margin, y, innerW, 24, false, true, false)
		y += 34
	} else {
		idx := 0
		for _, d := range p.Days {
			if t.SingleDay && d.Date != t.StartDate {
				continue
			}
			titleIndex := idx
			if t.SingleDay {
				titleIndex = 0
			}
			title := dayTitle(d, titleIndex, *t, u.store.data.Settings)
			if d.Date != "" {
				idx++
			}
			focus := strings.TrimSpace(d.Focus)
			if focus != "" {
				title += "    " + focus
			}
			u.addViewText(title, margin, y, innerW, 25, true, false, false)
			y += 27
			for _, it := range d.Items {
				for _, line := range itemText(it) {
					if strings.TrimSpace(line) != "" {
						y += u.addWrappedView(line, margin+18, y, innerW-18)
					}
				}
			}
			for _, meal := range d.Meals {
				if strings.TrimSpace(meal.Text) != "" {
					y += u.addWrappedView(meal.Name+"："+meal.Text, margin+18, y, innerW-18)
				}
			}
			if !t.SingleDay {
				for _, hotel := range hotelForDate(*p, d.Date) {
					y += u.addWrappedView("住宿："+hotel.Name, margin+18, y, innerW-18)
				}
			}
			if strings.TrimSpace(d.DailyNote) != "" {
				y += u.addWrappedView("備註："+d.DailyNote, margin+18, y, innerW-18)
			}
			y += 7
		}
	}

	if strings.TrimSpace(t.OverallNotes) != "" {
		y = u.section("旅程備註", y, W)
		y += u.addWrappedView(t.OverallNotes, margin, y, innerW) + 16
	}

	u.finishCards(y + 8)
	u.contentH = y + 34
	u.updateScroll()
	pInvalidateRect.Call(u.content, 0, 1)
	u.refreshToolbarState()
	uiLog("UI renderRight mode=view trip=%s contentH=%d", t.ID, u.contentH)
}

func parseRoomsSummary(s string) []Room {
	s = strings.ReplaceAll(strings.TrimSpace(s), " ", "")
	s = strings.ReplaceAll(s, "+", "＋")
	if s == "" {
		return []Room{{People: 2, Count: 1}}
	}
	parts := strings.Split(s, "＋")
	out := []Room{}
	for _, part := range parts {
		part = strings.TrimSpace(part)
		if part == "" {
			continue
		}
		people := 0
		count := 1
		if i := strings.Index(part, "人房"); i >= 0 {
			people, _ = strconv.Atoi(part[:i])
			rest := part[i+len("人房"):]
			rest = strings.TrimPrefix(rest, "×")
			rest = strings.TrimSuffix(rest, "房")
			if rest != "" {
				if v, err := strconv.Atoi(rest); err == nil && v > 0 {
					count = v
				}
			}
		}
		if people > 0 {
			out = append(out, Room{People: people, Count: count})
		}
	}
	if len(out) == 0 {
		return []Room{{People: 2, Count: 1}}
	}
	return out
}

func limitTravelerName(s string) string {
	var out []rune
	width := 0
	for _, r := range []rune(strings.TrimSpace(s)) {
		w := 1
		if r > 127 {
			w = 2
		}
		if width+w > 20 {
			break
		}
		out = append(out, r)
		width += w
	}
	return string(out)
}

func effectiveFlightOffset(seg FlightSegment) int {
	mode := strings.TrimSpace(seg.OffsetMode)
	if mode != "" && mode != "auto" {
		if n, err := strconv.Atoi(mode); err == nil {
			return n
		}
	}
	n, err := inferFlightOffset(seg.Date, seg.Depart, seg.Arrive, seg.From, seg.To)
	if err != nil {
		return 0
	}
	return n
}

func flightArrivalDisplay(seg FlightSegment) string {
	v := strings.TrimSpace(seg.Arrive)
	if v == "" {
		return ""
	}
	off := effectiveFlightOffset(seg)
	if off == 0 {
		return v
	}
	return fmt.Sprintf("%s (%+d)", v, off)
}

func parseArrivalEditorValue(v string) (string, *string, bool) {
	s := strings.TrimSpace(v)
	if s == "" {
		return "", nil, true
	}
	var mode *string
	base := s
	// Optional manual day offset can be typed directly after the arrival time,
	// e.g. "0730 (+1)" or "07:30 -1".  If omitted, the existing auto/manual
	// offset mode is preserved.
	if strings.HasSuffix(base, ")") {
		if i := strings.LastIndex(base, "("); i >= 0 {
			tok := strings.TrimSpace(base[i+1 : len(base)-1])
			if n, err := strconv.Atoi(tok); err == nil && n >= -1 && n <= 2 {
				m := strconv.Itoa(n)
				if n > 0 {
					m = fmt.Sprintf("+%d", n)
				}
				mode = &m
				base = strings.TrimSpace(base[:i])
			}
		}
	} else {
		parts := strings.Fields(base)
		if len(parts) > 1 {
			tok := parts[len(parts)-1]
			if n, err := strconv.Atoi(tok); err == nil && n >= -1 && n <= 2 && (strings.HasPrefix(tok, "+") || strings.HasPrefix(tok, "-")) {
				m := strconv.Itoa(n)
				if n > 0 {
					m = fmt.Sprintf("+%d", n)
				}
				mode = &m
				base = strings.TrimSpace(strings.Join(parts[:len(parts)-1], " "))
			}
		}
	}
	nv, ok := normalizeTimeInput(base)
	return nv, mode, ok
}

func planHasUserContent(p *Plan) bool {
	if p == nil {
		return false
	}
	for _, h := range p.Hotels {
		if strings.TrimSpace(h.Name) != "" || strings.TrimSpace(h.Breakfast) != "" ||
			strings.TrimSpace(h.Price) != "" || strings.TrimSpace(h.TWDPrice) != "" ||
			strings.TrimSpace(h.Note) != "" {
			return true
		}
		if len(h.Rooms) > 1 || (len(h.Rooms) == 1 && (h.Rooms[0].People != 2 || h.Rooms[0].Count != 1)) {
			return true
		}
	}
	for _, d := range p.Days {
		if strings.TrimSpace(d.Focus) != "" || strings.TrimSpace(d.DailyNote) != "" || len(d.Items) > 0 {
			return true
		}
		for _, m := range d.Meals {
			if strings.TrimSpace(m.Text) != "" || (!m.Fixed && strings.TrimSpace(m.Name) != "") {
				return true
			}
		}
	}
	return false
}

func (u *UI) addAlternativePlan(t *Trip) {
	if t == nil {
		return
	}
	p := u.activePlan()
	if !planHasUserContent(p) {
		addBlankPlan(t, "")
	} else {
		ans := msgBox(u.hwnd, "要複製目前方案嗎？\r\n\r\n【是】複製目前方案\r\n【否】建立空白方案", "新增方案", MB_YESNOCANCEL|MB_ICONQUESTION)
		if ans == IDYES {
			addBlankPlan(t, t.ActivePlanID)
		} else if ans == IDNO {
			addBlankPlan(t, "")
		} else {
			return
		}
	}
	u.saveNow()
	u.renderRight()
}

func (u *UI) deleteAlternativePlan(t *Trip) {
	if t == nil || len(t.Plans) <= 1 {
		return
	}
	if msgBox(u.hwnd, "確定刪除目前方案？", "刪除方案", MB_YESNO|MB_ICONWARNING) != IDYES {
		return
	}
	pid := t.ActivePlanID
	for i := range t.Plans {
		if t.Plans[i].ID == pid {
			t.Plans = append(t.Plans[:i], t.Plans[i+1:]...)
			break
		}
	}
	t.ActivePlanID = t.Plans[0].ID
	u.saveNow()
	u.renderRight()
}

func (u *UI) renderRight() {
	uiLog("UI renderRight begin mode=%s selected=%s working=%v scroll=%d rendering=%v", u.mode.String(), u.selectedTripID, u.workingTrip != nil, u.scrollY, u.rendering)
	if u.right == 0 {
		uiLog("UI renderRight skipped viewport missing")
		return
	}
	if u.rendering || u.settlingFocus {
		uiLog("UI renderRight skipped recursive call")
		return
	}
	u.settleEditorFocus()
	u.closeCellEditor()

	// Normalize impossible states before choosing a pane.  Persisted trips and editor
	// drafts are deliberately separate from V0.8.0 onward.
	if (u.mode == modeNew || u.mode == modeEdit) && u.workingTrip == nil {
		if u.selectedTrip() != nil {
			u.setMode(modeView)
		} else {
			u.setMode(modeNoSelection)
		}
	}
	if u.mode == modeView && u.selectedTrip() == nil {
		u.setMode(modeNoSelection)
	}

	u.rendering = true
	pane := u.paneForMode()
	u.switchRightPane(pane)
	pShowWindow.Call(u.content, SW_HIDE)
	defer func() {
		// Scrolling moves only the active pane.  RedrawWindow explicitly repaints child
		// HWNDs (EDIT/ListView/DatePicker/STATIC), which avoids the white placeholders
		// caused by the old WM_SETREDRAW suppression strategy.
		u.applyScroll()
		pShowWindow.Call(u.content, SW_SHOW)
		pRedrawWindow.Call(u.content, 0, 0, RDW_INVALIDATE|RDW_ERASE|RDW_FRAME|RDW_ALLCHILDREN|RDW_UPDATENOW)
		if u.right != 0 {
			pRedrawWindow.Call(u.right, 0, 0, RDW_INVALIDATE|RDW_ALLCHILDREN|RDW_UPDATENOW)
		}
		u.rendering = false
		if u.pendingFocus != 0 {
			pSetFocus.Call(u.pendingFocus)
			u.pendingFocus = 0
		}
		uiLog("UI renderRight leave mode=%s pane=0x%x controls=%d contentH=%d poolCreated=%d poolReused=%d", u.mode.String(), u.content, len(u.controls), u.contentH, u.poolCreatedPass, u.poolReusedPass)
	}()

	r := getClient(u.right)
	W := maxi(700, int(r.Right))
	u.clearForm()
	u.planFrameActive = false
	t := u.activeTrip()

	if u.mode == modeNoSelection || t == nil {
		u.renderEmptyState(W)
		return
	}
	if u.mode == modeView {
		u.renderTripView(t, W)
		return
	}

	emptyMode := false
	y := 20
	uiLog("UI renderRight mode=edit trip=%s title=%q width=%d", t.ID, t.Title, W)
	uiLog("UI renderRight section=basic")
	margin := 38

	// The trip title is the document title, not a basic-information label.
	u.addTitleEditCue(t.Title, "旅行計劃標題（例如：大阪快閃之旅）", 18, y, W-36, 38, func(s string) { t.Title = s })
	y += 52

	// Compact basic information: one dense row for dates/single-day/place/companions,
	// followed by one full-width purpose row.  The add-plan action remains on the
	// section title line at the upper-right.
	y = u.section("基本資料", y, W)
	if len(t.Plans) == 1 {
		u.addButton("＋新增方案", W-margin-104, y-43, 96, 28, func() { u.addAlternativePlan(t) })
	}
	innerW := W - 2*margin
	rowGap := 10
	checkW := 96
	dateW := 150
	// On narrower windows, dates yield some space before the free-text columns do.
	remaining := innerW - 2*dateW - checkW - 5*rowGap
	if remaining < 330 {
		reduce := (330 - remaining + 1) / 2
		dateW = maxi(118, dateW-reduce)
		remaining = innerW - 2*dateW - checkW - 5*rowGap
	}
	textCol := maxi(78, remaining/3)
	lastCol := maxi(78, remaining-2*textCol)
	xStart := margin
	xEnd := xStart + dateW + rowGap
	xCheck := xEnd + dateW + rowGap
	xCountry := xCheck + checkW + rowGap
	xCities := xCountry + textCol + rowGap
	xComp := xCities + textCol + rowGap

	u.addLabel("開始日期", xStart, y, dateW, 16, false)
	u.addLabel("結束日期", xEnd, y, dateW, 16, false)
	u.addLabel("國家", xCountry, y, textCol, 16, false)
	u.addLabel("城市／地區", xCities, y, textCol, 16, false)
	u.addLabel("旅伴", xComp, y, lastCol, 16, false)
	y += 19
	u.addDatePicker(t.StartDate, xStart, y, dateW, 28, false, func(s string) {
		t.StartDate = s
		syncTripDates(t)
		u.renderRight()
	})
	endPicker := u.addDatePicker(t.EndDate, xEnd, y, dateW, 28, false, func(s string) {
		t.EndDate = s
		syncTripDates(t)
		u.renderRight()
	})
	u.addCheck("單日旅行", t.SingleDay, xCheck, y+2, checkW, 24, func(v bool) {
		t.SingleDay = v
		syncTripDates(t)
		u.saveNow()
		u.renderRight()
	})
	u.addEditCue(t.Country, "例如：日本", xCountry, y, textCol, 28, false, func(s string) { t.Country = s }, nil)
	u.addEditCue(t.Cities, "例如：大阪、京都", xCities, y, textCol, 28, false, func(s string) { t.Cities = s }, nil)
	u.addEditCue(t.Companions, "例如：2人、家人", xComp, y, lastCol, 28, false, func(s string) { t.Companions = s }, nil)
	y += 38
	u.addLabel("主要目的", margin, y, innerW, 16, false)
	y += 19
	u.addEditCue(t.Purpose, "例如：旅遊、購物、美食", margin, y, innerW, 28, false, func(s string) { t.Purpose = s }, nil)
	y += 52
	uiLog("UI renderRight section=flights groups=%d", len(t.Flights))
	// Flights use actual Windows ListView report controls.  The list itself owns
	// rows/gridlines/selection; double-clicking a cell opens a temporary editor.
	y = u.section("航班", y, W)
	u.addButton("＋新增旅客", W-margin-112, y-43, 104, 28, func() { addFlightGroup(t); u.saveNow(); u.renderRight() })
	for gi := range t.Flights {
		g := &t.Flights[gi]
		gid := g.ID
		if strings.TrimSpace(g.Traveler) == "" {
			g.Traveler = fmt.Sprintf("旅客%d", gi+1)
		}
		groupStart := y
		if u.editingTravelerID == gid {
			hw := u.addEditCue(g.Traveler, "旅客名稱", margin, y, 200, 28, false, func(v string) {
				if gg := flightGroupByID(t, gid); gg != nil {
					gg.Traveler = limitTravelerName(v)
				}
			}, func(v string) {
				if gg := flightGroupByID(t, gid); gg != nil {
					v = limitTravelerName(v)
					if strings.TrimSpace(v) == "" {
						v = fmt.Sprintf("旅客%d", gi+1)
					}
					gg.Traveler = v
				}
				u.editingTravelerID = ""
				u.renderRight()
			})
			u.pendingFocus = hw
		} else {
			name := g.Traveler
			nameW := maxi(36, measureTextWidth(u.content, u.font, name))
			u.drawLabels = append(u.drawLabels, drawLabelItem{text: name, x: margin, y: y + 2, w: nameW + 2, h: 24, font: u.font, color: clrText, flags: DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX})
			editX := margin + nameW + 6
			u.addButtonKind("ghost", "✎", editX, y+4, 20, 20, func() { u.editingTravelerID = gid; u.renderRight() })
			u.addViewText("雙擊儲存格即可編輯", editX+28, y+3, 170, 22, false, true, false)
		}

		u.addButton("＋新增航段", W-margin-198, y, 104, 28, func() {
			if gg := flightGroupByID(t, gid); gg != nil {
				addMiddleFlightSegment(gg)
			}
			u.saveNow()
			u.renderRight()
		})
		if len(t.Flights) > 1 {
			u.addButton("刪除旅客", W-margin-88, y, 88, 28, func() {
				for i := range t.Flights {
					if t.Flights[i].ID == gid {
						t.Flights = append(t.Flights[:i], t.Flights[i+1:]...)
						break
					}
				}
				u.saveNow()
				u.renderRight()
			})
		}
		y += 36
		headers := []string{"航段", "日期", "班號", "航空公司", "出發機場", "起飛", "抵達機場", "抵達", "操作"}
		widths := []int{62, 92, 72, 104, 150, 62, 150, 84, 46}
		flex := []int{0, 0, 0, 1, 2, 0, 2, 0, 0}
		rows := make([][]string, 0, len(g.Segments))
		rowIDs := make([]string, 0, len(g.Segments))
		for si := range g.Segments {
			seg := g.Segments[si]
			op := ""
			if len(g.Segments) > 2 && si > 0 && si < len(g.Segments)-1 {
				op = "×"
			}
			rows = append(rows, []string{seg.Label, displayDate(seg.Date), seg.FlightNo, seg.Airline, seg.From, seg.Depart, seg.To, flightArrivalDisplay(seg), op})
			rowIDs = append(rowIDs, seg.ID)
		}
		_, listH := u.addDataList("listFlight", headers, widths, flex, rows, margin, y, W-2*margin, 160, listBinding{kind: "flight", tripID: t.ID, groupID: gid, rowIDs: rowIDs})
		y += listH + 22
		u.addSubCard(margin-10, groupStart-8, W-2*margin+20, y-groupStart-4)
	}
	if len(t.Flights) == 0 {
		y += 18
	}
	// Plans. One plan stays visually silent. From the second plan onward, tabs live in
	// their own outer frame that contains accommodation, daily itinerary and trip notes.
	p := u.activePlan()
	planFrameIndex := -1
	u.planFrameActive = false
	if len(t.Plans) > 1 {
		// Explicitly close the flight card so the plan frame never looks attached to it.
		u.closeOpenCard(y - 10)
		y += 16
		frameY := y
		u.drawFrames = append(u.drawFrames, drawFrameItem{x: 18, y: frameY, w: W - 36, h: 160})
		planFrameIndex = len(u.drawFrames) - 1
		xx := margin
		for i := range t.Plans {
			pid := t.Plans[i].ID
			name := strings.TrimSpace(t.Plans[i].Name)
			if name == "" {
				name = fmt.Sprintf("方案%d", i+1)
			}
			label := name
			kind := "button"
			if pid == t.ActivePlanID {
				label = "● " + label
				kind = "tab"
			}
			u.addButtonKind(kind, label, xx, y+10, 112, 30, func() {
				t.ActivePlanID = pid
				u.saveNow()
				u.scrollY = 0
				u.renderRight()
			})
			xx += 118
		}
		u.addButton("＋新增方案", W-margin-194, y+10, 96, 30, func() { u.addAlternativePlan(t) })
		u.addButtonKind("danger", "刪除方案", W-margin-92, y+10, 84, 30, func() { u.deleteAlternativePlan(t) })
		y += 58
		u.planFrameActive = true
		p = u.activePlan()
	}
	if p == nil {
		u.finishCards(y + 36)
		u.planFrameActive = false
		u.contentH = y + 80
		pInvalidateRect.Call(u.content, 0, 1)
		u.updateScroll()
		return
	}
	// Accommodation is hidden entirely for single-day trips.  Multi-day
	// accommodation is a real ListView; double-click a cell to edit it.
	if !t.SingleDay {
		y = u.section("住宿", y, W)
		u.addButton("＋新增住宿", W-margin-110, y-44, 100, 28, func() {
			p.Hotels = append(p.Hotels, Hotel{ID: randID("h"), CheckIn: t.StartDate, CheckOut: t.EndDate, Rooms: []Room{{People: 2, Count: 1}}, Currency: "TWD"})
			u.saveNow()
			u.renderRight()
		})
		if len(p.Hotels) > 0 {
			headers := []string{"名稱", "入住日期", "退房日期", "房型", "早餐", "幣別", "金額", "台幣金額", "備註", "操作"}
			widths := []int{180, 92, 92, 110, 82, 64, 92, 92, 160, 46}
			flex := []int{2, 0, 0, 1, 0, 0, 0, 0, 3, 0}
			rows := make([][]string, 0, len(p.Hotels))
			rowIDs := make([]string, 0, len(p.Hotels))
			for _, h := range p.Hotels {
				op := ""
				if len(p.Hotels) > 1 {
					op = "×"
				}
				rows = append(rows, []string{h.Name, displayDate(h.CheckIn), displayDate(h.CheckOut), roomsText(h.Rooms), h.Breakfast, h.Currency, h.Price, h.TWDPrice, h.Note, op})
				rowIDs = append(rowIDs, h.ID)
			}
			_, listH := u.addDataList("listHotel", headers, widths, flex, rows, margin, y, W-2*margin, 140, listBinding{kind: "hotel", tripID: t.ID, planID: p.ID, rowIDs: rowIDs})
			y += listH + 26
		} else {
			y += 18
		}
	}
	uiLog("UI renderRight section=daily days=%d", len(p.Days))
	y = u.section("每日行程", y, W)
	for di := range p.Days {
		d := &p.Days[di]
		// Single-day mode is intentionally non-destructive: other saved days remain
		// in the plan but the editor reads only the trip's start-date day.
		if t.SingleDay && d.Date != t.StartDate {
			continue
		}
		did := d.ID
		dayStart := y
		dayIndex := datedIndex(p, did)
		if t.SingleDay {
			dayIndex = 0
		}
		title := dayTitle(*d, dayIndex, *t, u.store.data.Settings)
		collapsed := u.collapsed[did]
		marker := "▼"
		if collapsed {
			marker = "▶"
		}
		u.addButtonKind("ghost", marker, margin, y, 32, 28, func() { u.collapsed[did] = !u.collapsed[did]; u.renderRight() })
		u.drawLabels = append(u.drawLabels, drawLabelItem{text: title, x: margin + 40, y: y + 2, w: 170, h: 24, font: u.fontBold, color: clrText, flags: DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX | DT_END_ELLIPSIS})
		u.addEditCue(d.Focus, "當日重點", margin+214, y, maxi(260, W-margin-(margin+214)-118), 28, false, func(v string) {
			if dd := dayByID(p, did); dd != nil {
				r := []rune(v)
				if len(r) > 20 {
					v = string(r[:20])
				}
				dd.Focus = v
			}
		}, nil)
		u.addButtonKind("ghost", "↑", W-margin-108, y, 30, 28, func() {
			if !t.SingleDay {
				moveDayContents(p, did, -1)
				u.saveNow()
				u.renderRight()
			}
		})
		u.addButtonKind("ghost", "↓", W-margin-74, y, 30, 28, func() {
			if !t.SingleDay {
				moveDayContents(p, did, 1)
				u.saveNow()
				u.renderRight()
			}
		})
		if d.Date == "" {
			u.addButton("×", W-margin-40, y, 30, 28, func() {
				for i := range p.Days {
					if p.Days[i].ID == did {
						p.Days = append(p.Days[:i], p.Days[i+1:]...)
						break
					}
				}
				u.saveNow()
				u.renderRight()
			})
		}
		y += 38
		if collapsed {
			u.addSubCard(margin-10, dayStart-8, W-2*margin+20, 54)
			// Keep enough bottom spacing for the parent Daily card to close below the
			// collapsed sub-card instead of cutting through its rounded border.
			y += 18
			continue
		}

		// Heterogeneous itinerary rows: itinerary / transport / free text each get a
		// purpose-built editor rather than being forced into one generic grid.
		for ii := range d.Items {
			it := &d.Items[ii]
			iid := it.ID
			u.addViewText("⋮⋮", margin, y+3, 24, 24, false, true, false)
			actionX := W - margin - 144
			switch it.Type {
			case "transport":
				if len(it.Transport) == 0 {
					it.Transport = []TransportSegment{{}}
				}
				for si := range it.Transport {
					segIndex := si
					seg := it.Transport[si]
					x0 := margin + 28
					avail := actionX - x0 - 8
					wTime, wPlat := 86, 62
					wStation := maxi(130, avail*22/100)
					wService := maxi(130, avail*22/100)
					wDest := avail - wTime - wPlat - wStation - wService - 16
					if wDest < 120 {
						wDest = 120
					}
					commitTransportTime := func(current string, setter func(*TransportSegment, string)) func(string) {
						return func(v string) {
							nv, ok := normalizeTimeInput(v)
							if !ok {
								msgBox(u.hwnd, "時間格式錯誤，請使用 24 小時制，例如 1234 或 12:34。", "時間錯誤", MB_OK|MB_ICONWARNING)
								u.renderRight()
								return
							}
							if x := itemByID(dayByID(p, did), iid); x != nil && segIndex < len(x.Transport) {
								setter(&x.Transport[segIndex], nv)
							}
							u.queueRender()
						}
					}
					u.addEditCue(seg.DepartTime, "出發 HH:MM", x0, y, wTime, 28, false, nil, commitTransportTime(seg.DepartTime, func(s *TransportSegment, v string) { s.DepartTime = v }))
					stationCue := "出發站"
					if si > 0 {
						stationCue = "轉乘站（空白沿用上一抵達站）"
					}
					u.addEditCue(seg.DepartStation, stationCue, x0+wTime+4, y, wStation, 28, false, func(v string) {
						if x := itemByID(dayByID(p, did), iid); x != nil && segIndex < len(x.Transport) {
							x.Transport[segIndex].DepartStation = v
						}
					}, nil)
					u.addEditCue(seg.DepartPlatform, "月台", x0+wTime+wStation+8, y, wPlat, 28, false, func(v string) {
						if x := itemByID(dayByID(p, did), iid); x != nil && segIndex < len(x.Transport) {
							x.Transport[segIndex].DepartPlatform = v
						}
					}, nil)
					u.addEditCue(seg.Service, "路線／車種", x0+wTime+wStation+wPlat+12, y, wService, 28, false, func(v string) {
						if x := itemByID(dayByID(p, did), iid); x != nil && segIndex < len(x.Transport) {
							x.Transport[segIndex].Service = v
						}
					}, nil)
					u.addEditCue(seg.Destination, "往／行先", x0+wTime+wStation+wPlat+wService+16, y, wDest, 28, false, func(v string) {
						if x := itemByID(dayByID(p, did), iid); x != nil && segIndex < len(x.Transport) {
							x.Transport[segIndex].Destination = v
						}
					}, nil)
					y += 32
					u.addViewText("→", x0+12, y+2, 30, 24, true, false, false)
					u.addEditCue(seg.ArriveTime, "抵達 HH:MM", x0+42, y, wTime, 28, false, nil, commitTransportTime(seg.ArriveTime, func(s *TransportSegment, v string) { s.ArriveTime = v }))
					u.addEditCue(seg.ArriveStation, "抵達站", x0+42+wTime+4, y, wStation, 28, false, func(v string) {
						if x := itemByID(dayByID(p, did), iid); x != nil && segIndex < len(x.Transport) {
							x.Transport[segIndex].ArriveStation = v
						}
					}, nil)
					u.addEditCue(seg.ArrivePlatform, "月台", x0+42+wTime+wStation+8, y, wPlat, 28, false, func(v string) {
						if x := itemByID(dayByID(p, did), iid); x != nil && segIndex < len(x.Transport) {
							x.Transport[segIndex].ArrivePlatform = v
						}
					}, nil)
					y += 34
				}
				u.addButton("＋轉乘", margin+28, y, 70, 26, func() {
					if x := itemByID(dayByID(p, did), iid); x != nil {
						x.Transport = append(x.Transport, TransportSegment{})
					}
					u.saveNow()
					u.renderRight()
				})
				u.addButton("＋備註", actionX, y, 64, 26, func() {
					if x := itemByID(dayByID(p, did), iid); x != nil {
						x.Notes = append(x.Notes, ItemNote{ID: randID("n"), Level: 1})
					}
					u.saveNow()
					u.renderRight()
				})
				u.addButtonKind("ghost", "↑", actionX+68, y, 28, 26, func() { moveItemByID(dayByID(p, did), iid, -1); u.saveNow(); u.renderRight() })
				u.addButtonKind("ghost", "↓", actionX+100, y, 28, 26, func() { moveItemByID(dayByID(p, did), iid, 1); u.saveNow(); u.renderRight() })
				u.addButton("×", actionX+132, y, 28, 26, func() { removeItem(dayByID(p, did), iid); u.saveNow(); u.renderRight() })
				y += 34
			case "text":
				u.addEditCue(it.Text, "今天記得要去取票……", margin+28, y, actionX-(margin+28)-8, 28, false, func(v string) {
					if x := itemByID(dayByID(p, did), iid); x != nil {
						x.Text = v
					}
				}, nil)
				u.addButton("＋備註", actionX, y, 64, 28, func() {
					if x := itemByID(dayByID(p, did), iid); x != nil {
						x.Notes = append(x.Notes, ItemNote{ID: randID("n"), Level: 1})
					}
					u.saveNow()
					u.renderRight()
				})
				u.addButtonKind("ghost", "↑", actionX+68, y, 28, 28, func() { moveItemByID(dayByID(p, did), iid, -1); u.saveNow(); u.renderRight() })
				u.addButtonKind("ghost", "↓", actionX+100, y, 28, 28, func() { moveItemByID(dayByID(p, did), iid, 1); u.saveNow(); u.renderRight() })
				u.addButton("×", actionX+132, y, 28, 28, func() { removeItem(dayByID(p, did), iid); u.saveNow(); u.renderRight() })
				y += 36
			default: // itinerary
				current := it.Time
				u.addEditCue(current, "HH:MM", margin+28, y, 78, 28, false, nil, func(v string) {
					nv, ok := normalizeTimeInput(v)
					if !ok {
						msgBox(u.hwnd, "時間格式錯誤，請使用 24 小時制，例如 1234 或 12:34。", "時間錯誤", MB_OK|MB_ICONWARNING)
						u.renderRight()
						return
					}
					if x := itemByID(dayByID(p, did), iid); x != nil {
						x.Time = nv
					}
					u.queueRender()
				})
				u.addEditCue(it.Text, "行程內容", margin+112, y, actionX-(margin+112)-8, 28, false, func(v string) {
					if x := itemByID(dayByID(p, did), iid); x != nil {
						x.Text = v
					}
				}, nil)
				u.addButton("＋備註", actionX, y, 64, 28, func() {
					if x := itemByID(dayByID(p, did), iid); x != nil {
						x.Notes = append(x.Notes, ItemNote{ID: randID("n"), Level: 1})
					}
					u.saveNow()
					u.renderRight()
				})
				u.addButtonKind("ghost", "↑", actionX+68, y, 28, 28, func() { moveItemByID(dayByID(p, did), iid, -1); u.saveNow(); u.renderRight() })
				u.addButtonKind("ghost", "↓", actionX+100, y, 28, 28, func() { moveItemByID(dayByID(p, did), iid, 1); u.saveNow(); u.renderRight() })
				u.addButton("×", actionX+132, y, 28, 28, func() { removeItem(dayByID(p, did), iid); u.saveNow(); u.renderRight() })
				y += 36
			}

			// Nested notes remain attached to their parent item and retain the three-level model.
			if x := itemByID(dayByID(p, did), iid); x != nil {
				for ni := range x.Notes {
					noteID := x.Notes[ni].ID
					level := x.Notes[ni].Level
					if level < 1 {
						level = 1
					}
					if level > 3 {
						level = 3
					}
					indent := (level - 1) * 24
					u.addViewText("↳", margin+40+indent, y+2, 22, 24, false, true, false)
					noteHWND := u.addEditCue(x.Notes[ni].Text, "備註", margin+64+indent, y, actionX-(margin+64+indent)-8, 28, false, func(v string) {
						if z := itemByID(dayByID(p, did), iid); z != nil {
							for k := range z.Notes {
								if z.Notes[k].ID == noteID {
									z.Notes[k].Text = v
									break
								}
							}
						}
					}, nil)
					u.noteKeys[noteHWND] = func(delta int) {
						changeNoteLevel(itemByID(dayByID(p, did), iid), noteID, delta)
						u.saveNow()
						u.queueRender()
					}
					u.addButtonTip("←", "減少縮排（Shift+Tab）", actionX, y, 28, 28, func() {
						if z := itemByID(dayByID(p, did), iid); z != nil {
							for k := range z.Notes {
								if z.Notes[k].ID == noteID && z.Notes[k].Level > 1 {
									z.Notes[k].Level--
									break
								}
							}
						}
						u.saveNow()
						u.renderRight()
					})
					u.addButtonTip("→", "增加縮排（Tab）", actionX+32, y, 28, 28, func() {
						if z := itemByID(dayByID(p, did), iid); z != nil {
							for k := range z.Notes {
								if z.Notes[k].ID == noteID && z.Notes[k].Level < 3 {
									z.Notes[k].Level++
									break
								}
							}
						}
						u.saveNow()
						u.renderRight()
					})
					u.addButtonKind("ghost", "↑", actionX+64, y, 28, 28, func() { moveNoteByID(itemByID(dayByID(p, did), iid), noteID, -1); u.saveNow(); u.renderRight() })
					u.addButtonKind("ghost", "↓", actionX+96, y, 28, 28, func() { moveNoteByID(itemByID(dayByID(p, did), iid), noteID, 1); u.saveNow(); u.renderRight() })
					u.addButton("×", actionX+128, y, 28, 28, func() { removeNote(itemByID(dayByID(p, did), iid), noteID); u.saveNow(); u.renderRight() })
					y += 32
				}
			}
			y += 4
		}

		u.addButton("＋行程", margin+28, y, 72, 28, func() {
			if dd := dayByID(p, did); dd != nil {
				dd.Items = append(dd.Items, Item{ID: randID("i"), Type: "itinerary"})
			}
			u.saveNow()
			u.renderRight()
		})
		u.addButton("＋交通", margin+106, y, 72, 28, func() {
			if dd := dayByID(p, did); dd != nil {
				dd.Items = append(dd.Items, Item{ID: randID("i"), Type: "transport", Transport: []TransportSegment{{}}})
			}
			u.saveNow()
			u.renderRight()
		})
		u.addButton("＋文字", margin+184, y, 72, 28, func() {
			if dd := dayByID(p, did); dd != nil {
				dd.Items = append(dd.Items, Item{ID: randID("i"), Type: "text"})
			}
			u.saveNow()
			u.renderRight()
		})
		y += 42

		dd := dayByID(p, did)
		if dd != nil && len(dd.Meals) == 0 {
			dd.Meals = fixedMeals()
		}
		if dd != nil {
			for mi := range dd.Meals {
				m := dd.Meals[mi]
				mid := m.ID
				u.addEmoji("🍴", margin+28, y+2, 24, 26)
				nameX := margin + 54
				if u.editingMealName == mid {
					hw := u.addEditCue(m.Name, "餐別", nameX, y, 86, 28, false, func(v string) {
						if z := dayByID(p, did); z != nil {
							for k := range z.Meals {
								if z.Meals[k].ID == mid {
									z.Meals[k].Name = v
									break
								}
							}
						}
					}, func(v string) {
						v = strings.TrimSpace(v)
						if v == "" {
							v = "餐飲"
						}
						if z := dayByID(p, did); z != nil {
							for k := range z.Meals {
								if z.Meals[k].ID == mid {
									z.Meals[k].Name = v
									break
								}
							}
						}
						u.editingMealName = ""
						u.renderRight()
					})
					u.pendingFocus = hw
				} else {
					name := strings.TrimSpace(m.Name)
					if name == "" {
						name = "餐飲"
					}
					nameW := maxi(28, measureTextWidth(u.content, u.font, name))
					u.addViewText(name, nameX, y+2, nameW+2, 24, false, false, false)
					editX := nameX + nameW + 5
					u.addButtonKind("ghost", "✎", editX, y+4, 20, 20, func() { u.editingMealName = mid; u.renderRight() })
					u.addViewText("：", editX+22, y+2, 12, 24, false, false, false)
				}
				contentX := nameX + 122
				if u.editingMealName != mid {
					name := strings.TrimSpace(m.Name)
					if name == "" {
						name = "餐飲"
					}
					contentX = nameX + maxi(28, measureTextWidth(u.content, u.font, name)) + 39
				}
				delW := 0
				if !m.Fixed {
					delW = 34
				}
				u.addEditCue(m.Text, "餐廳/特色菜/預定套餐", contentX, y, W-margin-contentX-delW, 28, false, func(v string) {
					if z := dayByID(p, did); z != nil {
						for k := range z.Meals {
							if z.Meals[k].ID == mid {
								z.Meals[k].Text = v
								break
							}
						}
					}
				}, nil)
				if !m.Fixed {
					u.addButton("×", W-margin-30, y, 30, 28, func() {
						if z := dayByID(p, did); z != nil {
							for k := range z.Meals {
								if z.Meals[k].ID == mid {
									z.Meals = append(z.Meals[:k], z.Meals[k+1:]...)
									break
								}
							}
						}
						u.saveNow()
						u.renderRight()
					})
				}
				y += 32
			}
			u.addButton("＋餐飲", margin+54, y, 70, 28, func() {
				if z := dayByID(p, did); z != nil {
					mid := randID("m")
					z.Meals = append(z.Meals, Meal{ID: mid, Name: "", Fixed: false})
					u.editingMealName = mid
				}
				u.saveNow()
				u.renderRight()
			})
			y += 44
		}

		if !t.SingleDay {
			stays := hotelForDate(*p, d.Date)
			names := []string{}
			for _, hh := range stays {
				if strings.TrimSpace(hh.Name) != "" {
					names = append(names, hh.Name)
				}
			}
			stay := "—"
			if len(names) > 0 {
				stay = strings.Join(names, "、")
			}
			u.addEmoji("🛏", margin+28, y+2, 24, 26)
			u.addViewText("住宿：", margin+52, y+2, 66, 26, false, false, false)
			u.addViewText(stay, margin+116, y+2, W-margin-(margin+116), 26, false, true, false)
			y += 34
		}
		u.addEmoji("📝", margin+28, y+2, 24, 26)
		u.addViewText("備註：", margin+52, y+2, 66, 26, false, false, false)
		u.addEditCue(d.DailyNote, "今天還有什麼要記得的？", margin+116, y, W-margin-(margin+116), 30, false, func(v string) {
			if z := dayByID(p, did); z != nil {
				z.DailyNote = v
			}
		}, nil)
		y += 48

		u.addSubCard(margin-10, dayStart-8, W-2*margin+20, y-dayStart+8)
		y += 18
	}
	uiLog("UI renderRight section=journeyNotes")
	y = u.section("旅程備註", y, W)
	u.addEdit(t.OverallNotes, margin, y, W-2*margin, 90, true, func(s string) { t.OverallNotes = s }, nil)
	y += 114
	u.finishCards(y + 8)
	if planFrameIndex >= 0 && planFrameIndex < len(u.drawFrames) {
		u.drawFrames[planFrameIndex].h = maxi(160, y-u.drawFrames[planFrameIndex].y+10)
	}
	u.planFrameActive = false
	u.contentH = y + 30
	pInvalidateRect.Call(u.content, 0, 1)
	u.updateScroll()
	u.applyFormEditability(emptyMode)
	// Single-day mode keeps EndDate unchanged but locks the picker.  The general
	// editability pass enables date controls first, so enforce the lock afterwards.
	if t.SingleDay && endPicker != 0 {
		enable(endPicker, false)
	}
	u.refreshToolbarState()
	uiLog("UI renderRight complete contentH=%d controls=%d empty=%v editing=%v dirty=%v poolCreated=%d poolReused=%d", u.contentH, len(u.controls), emptyMode, u.editing, u.dirty, u.poolCreatedPass, u.poolReusedPass)
}

func (u *UI) updateScroll() {
	r := getClient(u.right)
	max := maxi(0, u.contentH-int(r.Bottom))
	if u.scrollY > max {
		u.scrollY = max
	}
	setScroll(u.right, max, int(r.Bottom), u.scrollY)
	u.applyScroll()
}
func (u *UI) setScroll(pos int) {
	r := getClient(u.right)
	max := maxi(0, u.contentH-int(r.Bottom))
	if pos < 0 {
		pos = 0
	}
	if pos > max {
		pos = max
	}
	if pos == u.scrollY {
		return
	}
	old := u.scrollY
	u.scrollY = pos
	setScroll(u.right, max, int(r.Bottom), pos)
	u.applyScroll()
	uiLog("UI scroll old=%d new=%d max=%d", old, pos, max)
}
func (u *UI) applyScroll() {
	if u.content == 0 || u.right == 0 {
		return
	}
	r := getClient(u.right)
	move(u.content, 0, -u.scrollY, maxi(700, int(r.Right)), maxi(u.contentH, int(r.Bottom)))
}

func (u *UI) newTrip() {
	uiLog("ACTION 新增計劃")
	if !u.resolvePendingChanges() {
		return
	}
	t := newTripModel()
	u.workingTrip = &t
	u.dirty = true
	u.scrollY = 0
	u.setMode(modeNew)
	// A new trip is only a working copy. It is absent from store.data.Trips and therefore
	// cannot appear in the left list until explicit Save succeeds.
	u.renderTripList()
	u.renderRight()
	u.refreshToolbarState()
}
func (u *UI) deleteTrip() {
	uiLog("ACTION 刪除計劃 requested")
	if !u.resolvePendingChanges() {
		return
	}
	t := u.activeTrip()
	if t == nil {
		return
	}
	name := t.Title
	if name == "" {
		name = "（未命名計劃）"
	}
	if msgBox(u.hwnd, "確定刪除「"+name+"」？\r\n所有航班、住宿與每日行程都會一併刪除。", "刪除計劃", MB_YESNO|MB_ICONWARNING) != IDYES {
		return
	}
	id := t.ID
	candidate := cloneAppData(u.store.data)
	for i := range candidate.Trips {
		if candidate.Trips[i].ID == id {
			candidate.Trips = append(candidate.Trips[:i], candidate.Trips[i+1:]...)
			break
		}
	}
	candidate.ActiveTripID = ""
	if err := u.store.saveCandidate(candidate); err != nil {
		msgBox(u.hwnd, "刪除未儲存："+err.Error(), "刪除計劃", MB_OK|MB_ICONERROR)
		return
	}
	u.selectedTripID = ""
	u.store.data.ActiveTripID = ""
	u.workingTrip = nil
	u.dirty = false
	u.setMode(modeNoSelection)
	u.scrollY = 0
	u.renderTripList()
	u.renderRight()
	u.refreshToolbarState()
}
func (u *UI) doBackup() {
	uiLog("ACTION 備份")
	p, e := u.store.backup()
	if e != nil {
		msgBox(u.hwnd, "備份失敗："+e.Error(), "備份", MB_OK|MB_ICONERROR)
		return
	}
	msgBox(u.hwnd, "備份完成：\r\n"+p, "備份", MB_OK|MB_ICONINFORMATION)
}
func (u *UI) doExport() {
	uiLog("ACTION 匯出 Word requested")
	t := u.activeTrip()
	p := u.activePlan()
	if t == nil || p == nil {
		return
	}
	path, e := u.store.exportWord(t.ID, p.ID)
	if e != nil {
		msgBox(u.hwnd, "匯出失敗："+e.Error(), "匯出 Word", MB_OK|MB_ICONERROR)
		return
	}
	if msgBox(u.hwnd, "Word 已輸出：\r\n"+path+"\r\n\r\n要開啟匯出資料夾嗎？", "匯出 Word", MB_YESNO|MB_ICONINFORMATION) == IDYES {
		u.store.openFolder("exports")
	}
}

func (u *UI) settingsDialog() {
	if settingsWin != nil && settingsWin.hwnd != 0 {
		pShowWindow.Call(settingsWin.hwnd, SW_SHOW)
		pSetFocus.Call(settingsWin.hwnd)
		return
	}
	uiLog("SETTINGS open")
	st := u.store.data.Settings
	w := createWindow(0, settingsClass, "設定", WS_OVERLAPPED|WS_CAPTION|WS_SYSMENU|WS_VISIBLE, 500, 160, 620, 520, u.hwnd, 0, u.inst)
	if w == 0 {
		msgBox(u.hwnd, "無法建立設定視窗。", "設定", MB_OK|MB_ICONERROR)
		return
	}
	if u.iconBig != 0 {
		send(w, WM_SETICON, ICON_BIG, u.iconBig)
	}
	if u.iconSmall != 0 {
		send(w, WM_SETICON, ICON_SMALL, u.iconSmall)
	}
	enable(u.hwnd, false)
	sw := &settingsUI{hwnd: w, parent: u.hwnd}
	settingsWin = sw

	label := func(text string, x, y, cw, ch int, bold bool) uintptr {
		h := createWindow(0, "STATIC", text, WS_CHILD|WS_VISIBLE|SS_LEFT, x, y, cw, ch, w, 0, u.inst)
		if bold {
			setFont(h, u.fontBold)
		} else {
			setFont(h, u.font)
		}
		return h
	}
	combo := func(id int, x, y, cw int, items []string, current string) uintptr {
		h := createWindow(0, "COMBOBOX", "", WS_CHILD|WS_VISIBLE|WS_TABSTOP|WS_VSCROLL|CBS_DROPDOWNLIST, x, y, cw, 220, w, uintptr(id), u.inst)
		setFont(h, u.font)
		setWindowTheme(h, "Explorer")
		sel := -1
		for i, it := range items {
			send(h, CB_ADDSTRING, 0, uintptr(unsafe.Pointer(wstr(it))))
			if it == current {
				sel = i
			}
		}
		if sel >= 0 {
			send(h, CB_SETCURSEL, uintptr(sel), 0)
		}
		return h
	}
	check := func(id int, text string, checked bool, x, y, cw int) uintptr {
		h := createWindow(0, "BUTTON", text, WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_AUTOCHECKBOX, x, y, cw, 28, w, uintptr(id), u.inst)
		setFont(h, u.font)
		setWindowTheme(h, "Explorer")
		setCheck(h, checked)
		return h
	}

	label("顯示格式", 52, 34, 200, 28, true)
	label("每日編號", 52, 78, 110, 24, false)
	sw.day = combo(ID_SETTINGS_DAY, 176, 72, 210, []string{"第一天", "第1天", "Day1"}, st.DayFormat)
	label("星期格式", 52, 116, 110, 24, false)
	sw.week = combo(ID_SETTINGS_WEEK, 176, 110, 210, []string{"（一）", "週一", "星期一"}, st.WeekFormat)
	label("年份顯示", 52, 154, 110, 24, false)
	yearText := "跨年時顯示年份"
	if st.YearMode == "always" {
		yearText = "永遠顯示年份"
	}
	if st.YearMode == "never" {
		yearText = "永不顯示年份"
	}
	sw.year = combo(ID_SETTINGS_YEAR, 176, 148, 230, []string{"跨年時顯示年份", "永遠顯示年份", "永不顯示年份"}, yearText)

	label("Word 匯出", 52, 220, 200, 28, true)
	sw.wordPage = check(ID_SETTINGS_WORD_PAGE, "每天另起新頁", st.WordDayPage, 52, 264, 220)
	sw.wordUndated = check(ID_SETTINGS_WORD_UNDATED, "輸出無日期暫存", st.WordUndated, 52, 300, 220)
	sw.wordBlankMeals = check(ID_SETTINGS_WORD_BLANK_MEALS, "顯示空白的早餐／午餐／晚餐", st.WordBlankMeals, 52, 336, 320)

	save := createWindow(0, "BUTTON", "儲存", WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_OWNERDRAW, 404, 414, 90, 36, w, ID_SETTINGS_SAVE, u.inst)
	cancel := createWindow(0, "BUTTON", "取消", WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_OWNERDRAW, 504, 414, 90, 36, w, ID_SETTINGS_CANCEL, u.inst)
	setFont(save, u.font)
	setFont(cancel, u.font)
	u.buttonStyles[ID_SETTINGS_SAVE] = "primary"
	u.buttonStyles[ID_SETTINGS_CANCEL] = "secondary"
	pShowWindow.Call(w, SW_SHOW)
	pUpdateWindow.Call(w)
}
func (u *UI) googleLogin() {
	msgBox(u.hwnd, "Google Drive 只有在你主動按登入時才會開啟瀏覽器。\r\n\r\n桌面版 OAuth 設定介面正在重新接線；正常啟動不會開啟 Edge 或 localhost。", "Google Drive", MB_OK|MB_ICONINFORMATION)
}
func (u *UI) googleSync() {
	msgBox(u.hwnd, "Google Drive 同步介面正在重新接回桌面版。\r\n本機資料與 Word 匯出不受影響。", "Google Drive", MB_OK|MB_ICONINFORMATION)
}
func (u *UI) googleLogout() {
	_, tok := u.store.oauthPaths()
	_ = os.Remove(tok)
	msgBox(u.hwnd, "已清除本機 Google Drive 授權。", "Google Drive", MB_OK|MB_ICONINFORMATION)
}

func airportChoices() []string {
	out := make([]string, 0, len(airports))
	for _, a := range airports {
		out = append(out, a.IATA+" "+a.Name)
	}
	return out
}
func findSeg(t *Trip, gid, sid string) *FlightSegment {
	g := flightGroupByID(t, gid)
	if g == nil {
		return nil
	}
	for i := range g.Segments {
		if g.Segments[i].ID == sid {
			return &g.Segments[i]
		}
	}
	return nil
}
func removeItem(d *Day, id string) {
	if d == nil {
		return
	}
	for i := range d.Items {
		if d.Items[i].ID == id {
			d.Items = append(d.Items[:i], d.Items[i+1:]...)
			return
		}
	}
}
func moveItemByID(d *Day, id string, delta int) {
	if d == nil {
		return
	}
	for i := range d.Items {
		if d.Items[i].ID == id {
			j := i + delta
			if j >= 0 && j < len(d.Items) {
				d.Items[i], d.Items[j] = d.Items[j], d.Items[i]
			}
			return
		}
	}
}
func changeNoteLevel(it *Item, id string, delta int) {
	if it == nil {
		return
	}
	for i := range it.Notes {
		if it.Notes[i].ID == id {
			it.Notes[i].Level += delta
			if it.Notes[i].Level < 1 {
				it.Notes[i].Level = 1
			}
			if it.Notes[i].Level > 3 {
				it.Notes[i].Level = 3
			}
			return
		}
	}
}
func moveNoteByID(it *Item, id string, delta int) {
	if it == nil {
		return
	}
	for i := range it.Notes {
		if it.Notes[i].ID == id {
			j := i + delta
			if j >= 0 && j < len(it.Notes) {
				it.Notes[i], it.Notes[j] = it.Notes[j], it.Notes[i]
			}
			return
		}
	}
}
func removeNote(it *Item, id string) {
	if it == nil {
		return
	}
	for i := range it.Notes {
		if it.Notes[i].ID == id {
			it.Notes = append(it.Notes[:i], it.Notes[i+1:]...)
			return
		}
	}
}
func datedIndex(p *Plan, id string) int {
	n := 0
	for _, d := range p.Days {
		if d.Date != "" {
			if d.ID == id {
				return n
			}
			n++
		}
	}
	return 0
}
func mini(a, b int) int {
	if a < b {
		return a
	}
	return b
}
func maxi(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func runDesktop() (err error) {
	runtime.LockOSThread()
	defer runtime.UnlockOSThread()
	root := executableRoot()
	logDir := filepath.Join(root, "log")
	_ = os.MkdirAll(logDir, 0755)
	logPath := filepath.Join(logDir, "app("+time.Now().Format("20060102-150405")+").log")
	lf, openErr := os.OpenFile(logPath, os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0644)
	if openErr != nil {
		logPath = filepath.Join(root, "TravelPlan_emergency.log")
		lf, _ = os.OpenFile(logPath, os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0644)
	}
	if lf != nil {
		defer lf.Close()
	}
	s := &Server{root: root}
	if lf != nil {
		s.logger = logNew(lf)
	}
	s.logf("============================================================")
	s.logf("APP start name=%s version=V%s root=%s log=%s", AppName, Version, root, logPath)
	// One writer per portable data directory. Different copies remain independent.
	key := sha256.Sum256([]byte(strings.ToLower(filepath.Clean(root))))
	mutex, _, mutexErr := pCreateMutexW.Call(0, 0, uintptr(unsafe.Pointer(wstr(fmt.Sprintf("Local\\TravelPlan_%x", key)))))
	if mutex == 0 {
		return fmt.Errorf("無法建立資料目錄使用鎖：%v", mutexErr)
	}
	defer pCloseHandle.Call(mutex)
	if mutexErr == syscall.Errno(183) {
		return fmt.Errorf("此資料夾的 TravelPlan 已在執行，請使用已開啟的視窗")
	}
	defer func() {
		if v := recover(); v != nil {
			s.logPanic("runDesktop", v)
			err = fmt.Errorf("unexpected panic: %v", v)
		}
		s.logf("APP runDesktop exit err=%v", err)
	}()

	s.logf("STARTUP step=ensureDirs begin")
	if err = s.ensureDirs(); err != nil {
		s.logf("STARTUP step=ensureDirs failed err=%v", err)
		return err
	}
	s.logf("STARTUP step=loadData begin")
	if err = s.load(); err != nil {
		s.logf("STARTUP step=loadData failed err=%v", err)
		return err
	}
	// A new session starts with no plan selected. Selection is explicit from the left list.
	// This is intentionally not persisted, so opening the app never forces an old plan into view.
	s.data.ActiveTripID = ""
	s.logf("STARTUP selection reset; waiting for explicit plan selection")
	s.logf("STARTUP step=getModuleHandle begin")
	inst, _, _ := pGetModuleHandleW.Call(0)
	s.logf("STARTUP step=getModuleHandle complete inst=0x%x", inst)
	u := &UI{store: s, inst: inst, controlID: 2000, editBindings: map[uint16]editBinding{}, comboBindings: map[uint16]comboBinding{}, buttonActions: map[uint16]func(){}, dateBindings: map[uint16]dateBinding{}, buttonStyles: map[uint16]string{}, collapsed: map[string]bool{}, controlPool: map[string][]pooledCtrl{}, poolUsed: map[string]int{}, listBindings: map[uint16]listBinding{}, openCard: -1}
	ui = u
	s.logf("STARTUP step=loadIcons begin")
	iconPath := filepath.Join(root, "assets", "TravelPlan.ico")
	u.iconBig = loadIconFile(iconPath, 64, 64)
	u.iconSmall = loadIconFile(iconPath, 24, 24)
	s.logf("STARTUP step=loadIcons complete big=0x%x small=0x%x path=%s", u.iconBig, u.iconSmall, iconPath)
	s.logf("STARTUP step=registerClasses begin")
	if err = u.registerClasses(); err != nil {
		s.logf("STARTUP step=registerClasses failed err=%v", err)
		return err
	}
	s.logf("STARTUP step=createMain begin")
	if err = u.createMain(); err != nil {
		s.logf("STARTUP step=createMain failed err=%v", err)
		return err
	}
	s.logf("STARTUP complete; entering message loop")
	var m MSG
	for {
		r, _, getErr := pGetMessageW.Call(uintptr(unsafe.Pointer(&m)), 0, 0, 0)
		if int32(r) <= 0 {
			s.logf("MESSAGE loop exit result=%d err=%v", int32(r), getErr)
			break
		}
		if m.Message == WM_KEYDOWN && m.WParam == VK_TAB {
			if fn := u.noteKeys[m.Hwnd]; fn != nil {
				delta := 1
				shift, _, _ := pGetKeyState.Call(VK_SHIFT)
				if shift&0x8000 != 0 {
					delta = -1
				}
				fn(delta)
				continue
			}
		}
		if m.Message == WM_KEYDOWN && (m.WParam == VK_RETURN || m.WParam == VK_ESCAPE) {
			if m.WParam == VK_RETURN && u.cellEditor.hwnd != 0 && !strings.HasPrefix(u.cellEditor.kind, "edit") {
				pSetFocus.Call(u.content)
				pSetTimer.Call(u.hwnd, 4, 1, 0)
				continue
			}
			id, _, _ := pGetDlgCtrlID.Call(m.Hwnd)
			if _, ok := u.editBindings[uint16(id)]; ok {
				style, _, _ := pGetWindowLongPtrW.Call(m.Hwnd, ^uintptr(15)) // GWL_STYLE = -16
				if style&ES_MULTILINE == 0 {
					send(m.Hwnd, WM_KEYDOWN, m.WParam, m.LParam)
					continue
				}
			}
		}
		dialog := u.hwnd
		if settingsWin != nil {
			dialog = settingsWin.hwnd
		}
		if handled, _, _ := pIsDialogMessageW.Call(dialog, uintptr(unsafe.Pointer(&m))); handled != 0 {
			continue
		}
		pTranslateMessage.Call(uintptr(unsafe.Pointer(&m)))
		pDispatchMessageW.Call(uintptr(unsafe.Pointer(&m)))
	}
	return nil
}
func logNew(f *os.File) *log.Logger { return log.New(f, "", log.Ldate|log.Ltime|log.Lmicroseconds) }

func main() {
	if err := runDesktop(); err != nil {
		msgBox(0, "TravelPlan 啟動失敗：\r\n"+err.Error(), "TravelPlan", MB_OK|MB_ICONERROR)
	}
}

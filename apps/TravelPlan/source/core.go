package main

import (
	"archive/zip"
	"bytes"
	"crypto/rand"
	"encoding/json"
	"encoding/xml"
	"errors"
	"fmt"
	"io"
	"log"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"runtime/debug"
	"sort"
	"strconv"
	"strings"
	"sync"
	"time"
	_ "time/tzdata"
)

const (
	AppName = "TravelPlan"
	Version = "0.8.5"
)

type Airport struct{ IATA, ICAO, Name, City, Country, TZ string }

var airports = []Airport{
	{"TPE", "RCTP", "桃園國際機場", "桃園", "台灣", "Asia/Taipei"}, {"TSA", "RCSS", "台北松山機場", "台北", "台灣", "Asia/Taipei"}, {"KHH", "RCKH", "高雄國際機場", "高雄", "台灣", "Asia/Taipei"},
	{"NRT", "RJAA", "成田國際機場", "東京", "日本", "Asia/Tokyo"}, {"HND", "RJTT", "羽田機場", "東京", "日本", "Asia/Tokyo"}, {"KIX", "RJBB", "關西國際機場", "大阪", "日本", "Asia/Tokyo"}, {"ITM", "RJOO", "大阪國際機場（伊丹）", "大阪", "日本", "Asia/Tokyo"}, {"NGO", "RJGG", "中部國際機場", "名古屋", "日本", "Asia/Tokyo"}, {"FUK", "RJFF", "福岡機場", "福岡", "日本", "Asia/Tokyo"}, {"CTS", "RJCC", "新千歲機場", "札幌", "日本", "Asia/Tokyo"}, {"SDJ", "RJSS", "仙台機場", "仙台", "日本", "Asia/Tokyo"}, {"OKA", "ROAH", "那霸機場", "沖繩", "日本", "Asia/Tokyo"},
	{"ICN", "RKSI", "仁川國際機場", "首爾", "韓國", "Asia/Seoul"}, {"GMP", "RKSS", "金浦國際機場", "首爾", "韓國", "Asia/Seoul"}, {"HKG", "VHHH", "香港國際機場", "香港", "香港", "Asia/Hong_Kong"}, {"SIN", "WSSS", "新加坡樟宜機場", "新加坡", "新加坡", "Asia/Singapore"},
	{"BKK", "VTBS", "蘇凡納布機場", "曼谷", "泰國", "Asia/Bangkok"}, {"DMK", "VTBD", "廊曼國際機場", "曼谷", "泰國", "Asia/Bangkok"}, {"KUL", "WMKK", "吉隆坡國際機場", "吉隆坡", "馬來西亞", "Asia/Kuala_Lumpur"},
	{"MXP", "LIMC", "米蘭馬爾彭薩機場", "米蘭", "義大利", "Europe/Rome"}, {"LIN", "LIML", "米蘭利納特機場", "米蘭", "義大利", "Europe/Rome"}, {"FCO", "LIRF", "羅馬菲烏米奇諾機場", "羅馬", "義大利", "Europe/Rome"}, {"ZRH", "LSZH", "蘇黎世機場", "蘇黎世", "瑞士", "Europe/Zurich"}, {"GVA", "LSGG", "日內瓦機場", "日內瓦", "瑞士", "Europe/Zurich"}, {"CDG", "LFPG", "巴黎戴高樂機場", "巴黎", "法國", "Europe/Paris"}, {"ORY", "LFPO", "巴黎奧利機場", "巴黎", "法國", "Europe/Paris"}, {"LHR", "EGLL", "倫敦希斯洛機場", "倫敦", "英國", "Europe/London"}, {"LGW", "EGKK", "倫敦蓋威克機場", "倫敦", "英國", "Europe/London"}, {"FRA", "EDDF", "法蘭克福機場", "法蘭克福", "德國", "Europe/Berlin"}, {"MUC", "EDDM", "慕尼黑機場", "慕尼黑", "德國", "Europe/Berlin"}, {"AMS", "EHAM", "阿姆斯特丹史基浦機場", "阿姆斯特丹", "荷蘭", "Europe/Amsterdam"}, {"VIE", "LOWW", "維也納國際機場", "維也納", "奧地利", "Europe/Vienna"},
	{"LAX", "KLAX", "洛杉磯國際機場", "洛杉磯", "美國", "America/Los_Angeles"}, {"SFO", "KSFO", "舊金山國際機場", "舊金山", "美國", "America/Los_Angeles"}, {"SEA", "KSEA", "西雅圖塔科馬國際機場", "西雅圖", "美國", "America/Los_Angeles"}, {"JFK", "KJFK", "甘迺迪國際機場", "紐約", "美國", "America/New_York"}, {"EWR", "KEWR", "紐華克自由國際機場", "紐約", "美國", "America/New_York"}, {"BOS", "KBOS", "波士頓洛根國際機場", "波士頓", "美國", "America/New_York"}, {"ORD", "KORD", "芝加哥歐海爾國際機場", "芝加哥", "美國", "America/Chicago"}, {"YVR", "CYVR", "溫哥華國際機場", "溫哥華", "加拿大", "America/Vancouver"}, {"YYZ", "CYYZ", "多倫多皮爾遜國際機場", "多倫多", "加拿大", "America/Toronto"},
}

type Settings struct {
	DayFormat      string `json:"dayFormat"`
	WeekFormat     string `json:"weekFormat"`
	YearMode       string `json:"yearMode"`
	WordDayPage    bool   `json:"wordDayPage"`
	WordUndated    bool   `json:"wordUndated"`
	WordBlankMeals bool   `json:"wordBlankMeals"`
}
type AppData struct {
	Version      string   `json:"version"`
	Settings     Settings `json:"settings"`
	Trips        []Trip   `json:"trips"`
	ActiveTripID string   `json:"activeTripId"`
}
type Trip struct {
	ID           string        `json:"id"`
	Title        string        `json:"title"`
	StartDate    string        `json:"startDate"`
	EndDate      string        `json:"endDate"`
	SingleDay    bool          `json:"singleDay"`
	Companions   string        `json:"companions"`
	Country      string        `json:"country"`
	Cities       string        `json:"cities"`
	Purpose      string        `json:"purpose"`
	Flights      []FlightGroup `json:"flights"`
	Plans        []Plan        `json:"plans"`
	ActivePlanID string        `json:"activePlanId"`
	OverallNotes string        `json:"overallNotes"`
}
type FlightGroup struct {
	ID       string          `json:"id"`
	Traveler string          `json:"traveler"`
	Segments []FlightSegment `json:"segments"`
}
type FlightSegment struct {
	ID         string `json:"id"`
	Label      string `json:"label"`
	Date       string `json:"date"`
	FlightNo   string `json:"flightNo"`
	Airline    string `json:"airline"`
	From       string `json:"from"`
	To         string `json:"to"`
	Depart     string `json:"depart"`
	Arrive     string `json:"arrive"`
	OffsetMode string `json:"offsetMode"`
}
type Plan struct {
	ID     string  `json:"id"`
	Name   string  `json:"name"`
	Hotels []Hotel `json:"hotels"`
	Days   []Day   `json:"days"`
}
type Hotel struct {
	ID        string `json:"id"`
	CheckIn   string `json:"checkIn"`
	CheckOut  string `json:"checkOut"`
	Name      string `json:"name"`
	Rooms     []Room `json:"rooms"`
	Breakfast string `json:"breakfast"`
	Currency  string `json:"currency"`
	Price     string `json:"price"`
	TWDPrice  string `json:"twdPrice"`
	Note      string `json:"note"`
}
type Room struct {
	People int `json:"people"`
	Count  int `json:"count"`
}
type Day struct {
	ID           string `json:"id"`
	Date         string `json:"date"`
	OriginalDate string `json:"originalDate"`
	Focus        string `json:"focus"`
	Items        []Item `json:"items"`
	Meals        []Meal `json:"meals"`
	DailyNote    string `json:"dailyNote"`
}
type Item struct {
	ID        string             `json:"id"`
	Type      string             `json:"type"`
	Time      string             `json:"time"`
	Text      string             `json:"text"`
	Transport []TransportSegment `json:"transport"`
	Notes     []ItemNote         `json:"notes"`
}
type TransportSegment struct {
	DepartTime     string `json:"departTime"`
	DepartStation  string `json:"departStation"`
	DepartPlatform string `json:"departPlatform"`
	Service        string `json:"service"`
	Destination    string `json:"destination"`
	ArriveTime     string `json:"arriveTime"`
	ArriveStation  string `json:"arriveStation"`
	ArrivePlatform string `json:"arrivePlatform"`
}
type ItemNote struct {
	ID    string `json:"id"`
	Text  string `json:"text"`
	Level int    `json:"level"`
}
type Meal struct {
	ID    string `json:"id"`
	Name  string `json:"name"`
	Text  string `json:"text"`
	Fixed bool   `json:"fixed"`
}

type Server struct {
	mu         sync.Mutex
	root       string
	data       AppData
	port       int
	logger     *log.Logger
	oauthState string
}

func (s *Server) logf(format string, args ...any) {
	if s != nil && s.logger != nil {
		s.logger.Printf(format, args...)
	}
}

func (s *Server) logPanic(where string, v any) {
	if s == nil || s.logger == nil {
		return
	}
	s.logger.Printf("PANIC [%s]: %v\n%s", where, v, debug.Stack())
}

func cloneAppData(src AppData) AppData {
	b, _ := json.Marshal(src)
	var dst AppData
	_ = json.Unmarshal(b, &dst)
	return dst
}

func cloneTrip(src Trip) Trip {
	b, _ := json.Marshal(src)
	var dst Trip
	_ = json.Unmarshal(b, &dst)
	return dst
}

func randID(prefix string) string {
	b := make([]byte, 6)
	_, _ = rand.Read(b)
	return fmt.Sprintf("%s%x", prefix, b)
}
func defaultData() AppData {
	return AppData{Version: Version, Settings: Settings{DayFormat: "第一天", WeekFormat: "（一）", YearMode: "auto", WordDayPage: false, WordUndated: true, WordBlankMeals: false}, Trips: []Trip{}}
}

func executableRoot() string {
	p, err := os.Executable()
	if err != nil {
		p, _ = os.Getwd()
	}
	return filepath.Dir(p)
}
func (s *Server) paths() (string, string, string, string) {
	return filepath.Join(s.root, "data"), filepath.Join(s.root, "exports"), filepath.Join(s.root, "backup"), filepath.Join(s.root, "log")
}
func (s *Server) ensureDirs() error {
	s.logf("DIR ensure begin root=%s", s.root)
	a, b, c, d := s.paths()
	for _, p := range []string{a, b, c, d} {
		if err := os.MkdirAll(p, 0755); err != nil {
			return err
		}
	}
	s.logf("DIR ensure complete")
	return nil
}
func (s *Server) dataPath() string {
	d, _, _, _ := s.paths()
	return filepath.Join(d, "travelplan.json")
}
func (s *Server) load() error {
	s.logf("DATA load begin path=%s", s.dataPath())
	s.mu.Lock()
	defer s.mu.Unlock()
	b, err := os.ReadFile(s.dataPath())
	if errors.Is(err, os.ErrNotExist) {
		s.logf("DATA file not found; creating defaults")
		s.data = defaultData()
		err = s.saveLocked()
		s.logf("DATA default create result err=%v", err)
		return err
	}
	if err != nil {
		s.logf("DATA read failed err=%v", err)
		return err
	}
	s.logf("DATA read bytes=%d", len(b))
	loaded := defaultData()
	if err = json.Unmarshal(b, &loaded); err != nil {
		return err
	}
	s.data = loaded
	if s.data.Version == "" {
		s.data.Version = Version
	}
	s.logf("DATA load complete trips=%d activeTrip=%s storedVersion=%s", len(s.data.Trips), s.data.ActiveTripID, s.data.Version)
	return nil
}
func (s *Server) saveLocked() error {
	return s.saveCandidateLocked(s.data)
}

// Publish the new in-memory state only after the file has been replaced.
func (s *Server) saveCandidateLocked(candidate AppData) error {
	s.logf("DATA save begin trips=%d activeTrip=%s", len(s.data.Trips), s.data.ActiveTripID)
	candidate.Version = Version
	b, err := json.MarshalIndent(candidate, "", "  ")
	if err != nil {
		return err
	}
	f, err := os.CreateTemp(filepath.Dir(s.dataPath()), ".travelplan-*.tmp")
	if err != nil {
		return err
	}
	tmp := f.Name()
	defer os.Remove(tmp)
	if _, err = f.Write(b); err != nil {
		f.Close()
		return err
	}
	if err = f.Sync(); err != nil {
		f.Close()
		return err
	}
	if err = f.Close(); err != nil {
		return err
	}
	err = os.Rename(tmp, s.dataPath())
	if err == nil {
		s.data = candidate
	}
	s.logf("DATA save complete bytes=%d err=%v", len(b), err)
	return err
}
func (s *Server) saveCandidate(candidate AppData) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	return s.saveCandidateLocked(candidate)
}
func (s *Server) save() error { s.mu.Lock(); defer s.mu.Unlock(); return s.saveLocked() }
func (s *Server) backup() (string, error) {
	s.logf("BACKUP begin")
	s.mu.Lock()
	defer s.mu.Unlock()
	_, _, bd, _ := s.paths()
	b, err := json.MarshalIndent(s.data, "", "  ")
	if err != nil {
		return "", err
	}
	f, err := os.CreateTemp(bd, "TravelPlan_backup_"+time.Now().Format("20060102_150405")+"_*.json")
	if err != nil {
		return "", err
	}
	path := f.Name()
	_, writeErr := f.Write(b)
	err = errors.Join(writeErr, f.Sync(), f.Close())
	if err != nil {
		os.Remove(path)
		return "", err
	}
	s.logf("BACKUP complete path=%s err=%v", path, err)
	return path, err
}

func airportByValue(v string) (Airport, bool) {
	u := strings.ToUpper(strings.TrimSpace(v))
	if i := strings.Index(u, "｜"); i >= 0 {
		u = strings.TrimSpace(u[:i])
	}
	if len(u) > 3 {
		f := strings.Fields(u)
		if len(f) > 0 {
			u = f[0]
		}
	}
	for _, a := range airports {
		if a.IATA == u {
			return a, true
		}
	}
	return Airport{}, false
}
func parseDateTimeInTZ(dateS, timeS, tz string) (time.Time, error) {
	d, err := time.Parse("2006-01-02", dateS)
	if err != nil {
		return time.Time{}, err
	}
	p, err := time.Parse("15:04", timeS)
	if err != nil {
		return time.Time{}, err
	}
	loc, err := time.LoadLocation(tz)
	if err != nil {
		return time.Time{}, err
	}
	return time.Date(d.Year(), d.Month(), d.Day(), p.Hour(), p.Minute(), 0, 0, loc), nil
}
func inferFlightOffset(dateS, depart, arrive, fromV, toV string) (int, error) {
	fa, ok := airportByValue(fromV)
	if !ok {
		return 0, errors.New("unknown departure airport")
	}
	ta, ok := airportByValue(toV)
	if !ok {
		return 0, errors.New("unknown arrival airport")
	}
	dt, err := parseDateTimeInTZ(dateS, depart, fa.TZ)
	if err != nil {
		return 0, err
	}
	bestOffset := 0
	bestDur := time.Duration(1<<63 - 1)
	found := false
	for _, off := range []int{-1, 0, 1, 2} {
		base, err := parseDateTimeInTZ(dateS, arrive, ta.TZ)
		if err != nil {
			return 0, err
		}
		at := base.AddDate(0, 0, off)
		dur := at.Sub(dt)
		if dur >= 30*time.Minute && dur <= 30*time.Hour && dur < bestDur {
			bestDur = dur
			bestOffset = off
			found = true
		}
	}
	if !found {
		return 0, errors.New("unable to infer")
	}
	return bestOffset, nil
}
func escapeXML(s string) string {
	var b bytes.Buffer
	_ = xml.EscapeText(&b, []byte(s))
	return b.String()
}
func wordP(text string, bold bool, size int) string {
	rpr := ""
	if bold {
		rpr += "<w:b/>"
	}
	if size > 0 {
		rpr += fmt.Sprintf("<w:sz w:val=\"%d\"/><w:szCs w:val=\"%d\"/>", size, size)
	}
	return "<w:p><w:r><w:rPr>" + rpr + "</w:rPr><w:t xml:space=\"preserve\">" + escapeXML(text) + "</w:t></w:r></w:p>"
}
func cell(text string) string {
	return "<w:tc><w:tcPr><w:tcW w:w=\"0\" w:type=\"auto\"/></w:tcPr>" + wordP(text, false, 20) + "</w:tc>"
}
func table(rows [][]string) string {
	var b strings.Builder
	b.WriteString("<w:tbl><w:tblPr><w:tblBorders><w:top w:val=\"single\" w:sz=\"4\"/><w:left w:val=\"single\" w:sz=\"4\"/><w:bottom w:val=\"single\" w:sz=\"4\"/><w:right w:val=\"single\" w:sz=\"4\"/><w:insideH w:val=\"single\" w:sz=\"4\"/><w:insideV w:val=\"single\" w:sz=\"4\"/></w:tblBorders></w:tblPr>")
	for _, r := range rows {
		b.WriteString("<w:tr>")
		for _, c := range r {
			b.WriteString(cell(c))
		}
		b.WriteString("</w:tr>")
	}
	b.WriteString("</w:tbl>")
	return b.String()
}
func roomsText(rs []Room) string {
	parts := []string{}
	for _, r := range rs {
		if r.People <= 0 {
			continue
		}
		if r.Count <= 1 {
			parts = append(parts, fmt.Sprintf("%d人房", r.People))
		} else {
			parts = append(parts, fmt.Sprintf("%d人房×%d房", r.People, r.Count))
		}
	}
	return strings.Join(parts, "＋")
}
func dayTitle(d Day, index int, t Trip, st Settings) string {
	if d.Date == "" {
		if d.OriginalDate != "" {
			original := d.OriginalDate
			if dt, err := parseISODate(original); err == nil {
				original = dt.Format("01/02")
			}
			return "無日期（原 " + original + "）"
		}
		return "無日期"
	}
	dt, _ := time.Parse("2006-01-02", d.Date)
	day := fmt.Sprintf("第%d天", index+1)
	if st.DayFormat == "第一天" {
		zh := []string{"一", "二", "三", "四", "五", "六", "七", "八", "九", "十", "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九", "二十", "二十一", "二十二", "二十三", "二十四", "二十五", "二十六", "二十七", "二十八", "二十九", "三十"}
		if index >= 0 && index < len(zh) {
			day = "第" + zh[index] + "天"
		}
	} else if st.DayFormat == "Day1" {
		day = fmt.Sprintf("Day%d", index+1)
	}
	weeks := []string{"日", "一", "二", "三", "四", "五", "六"}
	wk := weeks[int(dt.Weekday())]
	switch st.WeekFormat {
	case "星期一":
		wk = "星期" + wk
	case "週一":
		wk = "週" + wk
	default:
		wk = "（" + wk + "）"
	}
	dateTxt := dt.Format("01/02")
	cross := false
	if t.StartDate != "" && t.EndDate != "" && len(t.StartDate) >= 4 && len(t.EndDate) >= 4 {
		cross = t.StartDate[:4] != t.EndDate[:4]
	}
	if st.YearMode == "always" || (st.YearMode != "never" && cross) {
		dateTxt = dt.Format("2006/01/02")
	}
	return day + " " + dateTxt + wk
}
func hotelForDate(p Plan, dateS string) []Hotel {
	out := []Hotel{}
	if dateS == "" {
		return out
	}
	for _, h := range p.Hotels {
		if h.CheckIn != "" && h.CheckOut != "" && dateS >= h.CheckIn && dateS < h.CheckOut {
			out = append(out, h)
		}
	}
	return out
}
func itemText(it Item) []string {
	lines := []string{}
	switch it.Type {
	case "itinerary":
		if it.Time != "" {
			lines = append(lines, it.Time+"  "+it.Text)
		} else {
			lines = append(lines, it.Text)
		}
	case "text":
		lines = append(lines, it.Text)
	case "transport":
		for i, s := range it.Transport {
			depStation := s.DepartStation
			if i > 0 && depStation == "" {
				depStation = it.Transport[i-1].ArriveStation
			}
			dep := s.DepartTime + " " + depStation
			if s.DepartPlatform != "" {
				dep += "（" + s.DepartPlatform + "）"
			}
			if i == 0 {
				lines = append(lines, strings.TrimSpace(dep))
			} else if depStation != it.Transport[i-1].ArriveStation {
				lines = append(lines, strings.TrimSpace(dep))
			}
			svc := strings.TrimSpace(s.Service)
			if s.Destination != "" {
				if svc != "" {
					svc += "｜"
				}
				svc += s.Destination
			}
			if svc != "" {
				lines = append(lines, svc)
			}
			off := ""
			if s.DepartTime != "" && s.ArriveTime != "" && s.ArriveTime < s.DepartTime {
				off = " +1"
			}
			arr := strings.TrimSpace("→ " + s.ArriveTime + off + " " + s.ArriveStation)
			if s.ArrivePlatform != "" {
				arr += "（" + s.ArrivePlatform + "）"
			}
			if i+1 < len(it.Transport) && it.Transport[i+1].DepartTime != "" && it.Transport[i+1].DepartStation == "" {
				arr += " " + it.Transport[i+1].DepartTime
			}
			lines = append(lines, arr)
		}
	}
	for _, n := range it.Notes {
		prefix := strings.Repeat("　", max(1, n.Level))
		lines = append(lines, prefix+"• "+n.Text)
	}
	return lines
}
func findTrip(d AppData, id string) (Trip, bool) {
	for _, t := range d.Trips {
		if t.ID == id {
			return t, true
		}
	}
	return Trip{}, false
}
func findPlan(t Trip, id string) (Plan, bool) {
	for _, p := range t.Plans {
		if p.ID == id {
			return p, true
		}
	}
	return Plan{}, false
}
func (s *Server) exportWord(tripID, planID string) (result string, resultErr error) {
	s.mu.Lock()
	d := cloneAppData(s.data)
	s.mu.Unlock()
	t, ok := findTrip(d, tripID)
	if !ok {
		return "", errors.New("trip not found")
	}
	p, ok := findPlan(t, planID)
	if !ok {
		return "", errors.New("plan not found")
	}
	_, ed, _, _ := s.paths()
	safe := strings.NewReplacer("/", "-", "\\", "-", ":", "-", "*", "-", "?", "-", "\"", "-", "<", "-", ">", "-", "|", "-").Replace(t.Title)
	if safe == "" {
		safe = "旅行計劃"
	}
	safePlan := strings.NewReplacer("/", "-", "\\", "-", ":", "-", "*", "-", "?", "-", "\"", "-", "<", "-", ">", "-", "|", "-").Replace(p.Name)
	path := filepath.Join(ed, safe+"_"+safePlan+".docx")
	f, err := os.CreateTemp(ed, ".word-*.tmp")
	if err != nil {
		return "", err
	}
	tmp := f.Name()
	zw := zip.NewWriter(f)
	var writeErr error
	defer func() {
		zipErr := zw.Close()
		syncErr := f.Sync()
		closeErr := f.Close()
		resultErr = errors.Join(resultErr, writeErr, zipErr, syncErr, closeErr)
		if resultErr == nil {
			resultErr = os.Rename(tmp, path)
		}
		if resultErr != nil {
			result = ""
		}
		os.Remove(tmp)
	}()
	add := func(name, content string) error {
		if writeErr != nil {
			return writeErr
		}
		w, e := zw.Create(name)
		if e != nil {
			writeErr = e
			return e
		}
		_, e = io.WriteString(w, content)
		writeErr = e
		return e
	}
	_ = add("[Content_Types].xml", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/></Types>`)
	_ = add("_rels/.rels", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>`)
	_ = add("word/_rels/document.xml.rels", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"></Relationships>`)
	_ = add("word/styles.xml", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:rPr><w:rFonts w:eastAsia="Microsoft JhengHei"/></w:rPr></w:style></w:styles>`)
	var b strings.Builder
	b.WriteString(`<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>`)
	b.WriteString(wordP(t.Title, true, 36))
	b.WriteString(wordP(t.StartDate+func() string {
		if t.SingleDay || t.EndDate == "" || t.EndDate == t.StartDate {
			return ""
		}
		return " ～ " + t.EndDate
	}(), false, 22))
	summary := [][]string{{"旅伴", t.Companions}, {"國家", t.Country}, {"城市／地區", t.Cities}, {"主要目的", t.Purpose}, {"方案", p.Name}}
	b.WriteString(table(summary))
	if len(t.Flights) > 0 {
		b.WriteString(wordP("航班", true, 28))
		for _, fg := range t.Flights {
			b.WriteString(wordP("旅客："+fg.Traveler, true, 22))
			rows := [][]string{{"航段", "日期", "航班", "航司", "出發", "時間", "抵達", "時間"}}
			for _, fs := range fg.Segments {
				offText := ""
				off := 0
				if fs.OffsetMode != "" && fs.OffsetMode != "auto" {
					off, _ = strconv.Atoi(fs.OffsetMode)
				} else {
					off, _ = inferFlightOffset(fs.Date, fs.Depart, fs.Arrive, fs.From, fs.To)
				}
				if off != 0 {
					offText = fmt.Sprintf(" %+d", off)
				}
				rows = append(rows, []string{fs.Label, fs.Date, fs.FlightNo, fs.Airline, fs.From, fs.Depart, fs.To, fs.Arrive + offText})
			}
			b.WriteString(table(rows))
		}
	}
	if !t.SingleDay && len(p.Hotels) > 0 {
		b.WriteString(wordP("住宿", true, 28))
		rows := [][]string{{"Check-in", "Check-out", "旅館", "房型", "早餐", "價格", "備註"}}
		for _, h := range p.Hotels {
			price := h.Currency + " " + h.Price
			if h.Currency != "TWD" && h.TWDPrice != "" {
				price += " = TWD " + h.TWDPrice
			}
			rows = append(rows, []string{h.CheckIn, h.CheckOut, h.Name, roomsText(h.Rooms), h.Breakfast, price, h.Note})
		}
		b.WriteString(table(rows))
	}
	datedIndex := 0
	printedDays := 0
	for _, dy := range p.Days {
		if t.SingleDay && dy.Date != t.StartDate {
			continue
		}
		if dy.Date == "" && !d.Settings.WordUndated {
			continue
		}
		if d.Settings.WordDayPage && printedDays > 0 {
			b.WriteString(`<w:p><w:r><w:br w:type="page"/></w:r></w:p>`)
		}
		printedDays++
		if dy.Date != "" {
			datedIndex++
		}
		idx := datedIndex - 1
		if idx < 0 {
			idx = 0
		}
		b.WriteString(wordP(dayTitle(dy, idx, t, d.Settings), true, 28))
		if dy.Focus != "" {
			b.WriteString(wordP("當日重點："+dy.Focus, true, 22))
		}
		for _, it := range dy.Items {
			for _, ln := range itemText(it) {
				if strings.TrimSpace(ln) != "" {
					b.WriteString(wordP(ln, false, 20))
				}
			}
		}
		mealLines := []string{}
		for _, m := range dy.Meals {
			if m.Text != "" || d.Settings.WordBlankMeals {
				mealLines = append(mealLines, m.Name+"："+m.Text)
			}
		}
		if len(mealLines) > 0 {
			b.WriteString(wordP("餐飲", true, 22))
			for _, ln := range mealLines {
				b.WriteString(wordP(ln, false, 20))
			}
		}
		if !t.SingleDay && dy.Date != "" {
			hs := hotelForDate(p, dy.Date)
			if len(hs) > 0 {
				names := []string{}
				for _, h := range hs {
					names = append(names, h.Name)
				}
				b.WriteString(wordP("住宿："+strings.Join(names, "、"), false, 20))
			}
		}
		if dy.DailyNote != "" {
			b.WriteString(wordP("每日備註："+dy.DailyNote, false, 20))
		}
	}
	if t.OverallNotes != "" {
		b.WriteString(wordP("旅程備註", true, 28))
		b.WriteString(wordP(t.OverallNotes, false, 20))
	}
	b.WriteString(`<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="1134" w:bottom="1134" w:left="1134"/></w:sectPr></w:body></w:document>`)
	if err := add("word/document.xml", b.String()); err != nil {
		return "", err
	}
	return path, nil
}

// Google Drive desktop integration remains intentionally disconnected in V0.4.1.
// Normal application startup never opens a browser or starts a localhost server.
func (s *Server) oauthPaths() (string, string) {
	d, _, _, _ := s.paths()
	return filepath.Join(d, "google_credentials.json"), filepath.Join(d, "google_token.json")
}

func (s *Server) openFolder(kind string) {
	s.logf("FOLDER open requested kind=%s", kind)
	_, ed, bd, ld := s.paths()
	p := s.root
	switch kind {
	case "exports":
		p = ed
	case "backup":
		p = bd
	case "log":
		p = ld
	}
	if runtime.GOOS == "windows" {
		_ = exec.Command("explorer.exe", p).Start()
	}
}

func tripMatchesFilter(t Trip, query, startFilter, endFilter string) bool {
	q := strings.ToLower(strings.TrimSpace(query))
	if q != "" {
		hay := strings.ToLower(strings.Join([]string{t.Title, t.Companions}, " "))
		if !strings.Contains(hay, q) {
			return false
		}
	}

	startFilter = normalizeDate(startFilter)
	endFilter = normalizeDate(endFilter)
	if startFilter == "" && endFilter == "" {
		return true
	}

	ts := normalizeDate(t.StartDate)
	te := normalizeDate(t.EndDate)
	if t.SingleDay {
		te = ts
	}
	if te == "" {
		te = ts
	}
	if ts == "" {
		ts = te
	}
	if ts == "" || te == "" {
		return false
	}
	if _, err := parseISODate(ts); err != nil {
		return false
	}
	if _, err := parseISODate(te); err != nil {
		return false
	}
	// Overlap semantics: the plan is included when at least one day of the
	// plan falls in the requested range. Open-ended filters use the same rule.
	if startFilter != "" && te < startFilter {
		return false
	}
	if endFilter != "" && ts > endFilter {
		return false
	}
	return true
}

func newTripModel() Trip {
	today := time.Now().Format("2006-01-02")
	p := Plan{
		ID:   randID("p"),
		Name: "方案1",
		Hotels: []Hotel{{
			ID:       randID("h"),
			CheckIn:  today,
			CheckOut: today,
			Rooms:    []Room{{People: 2, Count: 1}},
			Currency: "TWD",
		}},
		Days: []Day{},
	}
	t := Trip{
		ID:           randID("t"),
		StartDate:    today,
		EndDate:      today,
		Plans:        []Plan{p},
		ActivePlanID: p.ID,
		Flights:      []FlightGroup{},
	}
	addFlightGroup(&t)
	syncTripDates(&t)
	return t
}

func fixedMeals() []Meal {
	return []Meal{
		{ID: randID("m"), Name: "早餐", Fixed: true},
		{ID: randID("m"), Name: "午餐", Fixed: true},
		{ID: randID("m"), Name: "晚餐", Fixed: true},
	}
}

func dayEmpty(d Day) bool {
	if strings.TrimSpace(d.Focus) != "" || strings.TrimSpace(d.DailyNote) != "" || len(d.Items) > 0 {
		return false
	}
	for _, m := range d.Meals {
		name := strings.TrimSpace(m.Name)
		customName := name != "" && (!m.Fixed || (name != "早餐" && name != "午餐" && name != "晚餐"))
		if strings.TrimSpace(m.Text) != "" || customName {
			return false
		}
	}
	return true
}

func parseISODate(s string) (time.Time, error) {
	s = strings.TrimSpace(strings.ReplaceAll(s, "/", "-"))
	return time.Parse("2006-01-02", s)
}

func displayDate(s string) string {
	if s == "" {
		return ""
	}
	if t, err := parseISODate(s); err == nil {
		return t.Format("2006/01/02")
	}
	return s
}

func normalizeDate(s string) string {
	if s == "" {
		return ""
	}
	if t, err := parseISODate(s); err == nil {
		return t.Format("2006-01-02")
	}
	return strings.TrimSpace(s)
}

func normalizeTimeInput(s string) (string, bool) {
	s = strings.TrimSpace(s)
	if s == "" {
		return "", true
	}
	if strings.Contains(s, ":") {
		parts := strings.Split(s, ":")
		if len(parts) != 2 || len(parts[0]) < 1 || len(parts[0]) > 2 || len(parts[1]) != 2 {
			return s, false
		}
	}
	digits := strings.ReplaceAll(s, ":", "")
	if len(digits) == 3 {
		digits = "0" + digits
	}
	if len(digits) != 4 {
		return s, false
	}
	for _, r := range digits {
		if r < '0' || r > '9' {
			return s, false
		}
	}
	hh, _ := strconv.Atoi(digits[:2])
	mm, _ := strconv.Atoi(digits[2:])
	if hh < 0 || hh > 23 || mm < 0 || mm > 59 {
		return s, false
	}
	return fmt.Sprintf("%02d:%02d", hh, mm), true
}

func normalizeTime(s string) string {
	if v, ok := normalizeTimeInput(s); ok {
		return v
	}
	return strings.TrimSpace(s)
}

func validateTripForSave(t Trip) error {
	start, err := parseISODate(t.StartDate)
	if err != nil {
		return errors.New("開始日期不合法，請重新選擇日期")
	}
	if !t.SingleDay {
		end, err := parseISODate(t.EndDate)
		if err != nil {
			return errors.New("結束日期不合法，請重新選擇日期")
		}
		if end.Before(start) {
			return errors.New("結束日期不可早於開始日期")
		}
	}
	return nil
}

func syncTripDates(t *Trip) {
	if t == nil {
		return
	}
	st, e1 := parseISODate(t.StartDate)
	if e1 != nil {
		return
	}
	// Single-day mode is a presentation/editing mode, not a destructive date-range
	// rewrite.  Keep EndDate untouched so unchecking SingleDay can restore the prior
	// range immediately.  We only guarantee that the start-date day exists; other
	// dated/undated data stays preserved but hidden while SingleDay is enabled.
	if t.SingleDay {
		ds := st.Format("2006-01-02")
		for pi := range t.Plans {
			p := &t.Plans[pi]
			found := false
			for _, d := range p.Days {
				if d.Date == ds {
					found = true
					break
				}
			}
			if !found {
				p.Days = append(p.Days, Day{ID: randID("d"), Date: ds, Meals: fixedMeals()})
			}
			sort.SliceStable(p.Days, func(i, j int) bool {
				ai, aj := p.Days[i], p.Days[j]
				ki := ai.Date
				if ki == "" {
					ki = ai.OriginalDate + "~"
				}
				kj := aj.Date
				if kj == "" {
					kj = aj.OriginalDate + "~"
				}
				return ki < kj
			})
		}
		return
	}
	en, e2 := parseISODate(t.EndDate)
	if e2 != nil || en.Before(st) {
		return
	}
	wanted := map[string]bool{}
	for d := st; !d.After(en); d = d.AddDate(0, 0, 1) {
		wanted[d.Format("2006-01-02")] = true
	}
	for pi := range t.Plans {
		p := &t.Plans[pi]
		existing := map[string]int{}
		out := make([]Day, 0, len(p.Days)+int(en.Sub(st).Hours()/24)+1)
		// existing dated days that remain
		for _, d := range p.Days {
			if d.Date != "" && wanted[d.Date] {
				existing[d.Date] = len(out)
				out = append(out, d)
			} else if d.Date != "" && !wanted[d.Date] {
				if !dayEmpty(d) {
					d.OriginalDate = d.Date
					d.Date = ""
					out = append(out, d)
				}
			} else {
				out = append(out, d)
			}
		}
		// add missing dated days
		for d := st; !d.After(en); d = d.AddDate(0, 0, 1) {
			ds := d.Format("2006-01-02")
			found := false
			for _, x := range out {
				if x.Date == ds {
					found = true
					break
				}
			}
			if !found {
				out = append(out, Day{ID: randID("d"), Date: ds, Meals: fixedMeals()})
			}
		}
		// order dated by date, keep undated at positions by original date approximation: dated/undated mixed sorted key
		sort.SliceStable(out, func(i, j int) bool {
			ai, aj := out[i], out[j]
			ki := ai.Date
			if ki == "" {
				ki = ai.OriginalDate + "~"
			}
			kj := aj.Date
			if kj == "" {
				kj = aj.OriginalDate + "~"
			}
			return ki < kj
		})
		p.Days = out
	}
}

func tripByID(d *AppData, id string) *Trip {
	for i := range d.Trips {
		if d.Trips[i].ID == id {
			return &d.Trips[i]
		}
	}
	return nil
}
func planByID(t *Trip, id string) *Plan {
	if t == nil {
		return nil
	}
	for i := range t.Plans {
		if t.Plans[i].ID == id {
			return &t.Plans[i]
		}
	}
	return nil
}
func dayByID(p *Plan, id string) *Day {
	if p == nil {
		return nil
	}
	for i := range p.Days {
		if p.Days[i].ID == id {
			return &p.Days[i]
		}
	}
	return nil
}

// Move the user content, never the date-derived accommodation or date identity.
func moveDayContents(p *Plan, id string, delta int) {
	if p == nil {
		return
	}
	for i := range p.Days {
		if p.Days[i].ID != id {
			continue
		}
		j := i + delta
		if j < 0 || j >= len(p.Days) {
			return
		}
		a, b := &p.Days[i], &p.Days[j]
		a.Focus, b.Focus = b.Focus, a.Focus
		a.Items, b.Items = b.Items, a.Items
		a.Meals, b.Meals = b.Meals, a.Meals
		a.DailyNote, b.DailyNote = b.DailyNote, a.DailyNote
		return
	}
}
func flightGroupByID(t *Trip, id string) *FlightGroup {
	if t == nil {
		return nil
	}
	for i := range t.Flights {
		if t.Flights[i].ID == id {
			return &t.Flights[i]
		}
	}
	return nil
}
func hotelByID(p *Plan, id string) *Hotel {
	if p == nil {
		return nil
	}
	for i := range p.Hotels {
		if p.Hotels[i].ID == id {
			return &p.Hotels[i]
		}
	}
	return nil
}
func itemByID(d *Day, id string) *Item {
	if d == nil {
		return nil
	}
	for i := range d.Items {
		if d.Items[i].ID == id {
			return &d.Items[i]
		}
	}
	return nil
}

func addFlightGroup(t *Trip) string {
	if t == nil {
		return ""
	}
	departDate := t.StartDate
	returnDate := t.EndDate
	if departDate == "" {
		departDate = time.Now().Format("2006-01-02")
	}
	if returnDate == "" {
		returnDate = departDate
	}
	g := FlightGroup{ID: randID("f"), Traveler: fmt.Sprintf("旅客%d", len(t.Flights)+1), Segments: []FlightSegment{
		{ID: randID("s"), Label: "去程", Date: departDate, OffsetMode: "auto"},
		{ID: randID("s"), Label: "回程", Date: returnDate, OffsetMode: "auto"},
	}}
	t.Flights = append(t.Flights, g)
	return g.ID
}

func addMiddleFlightSegment(g *FlightGroup) string {
	if g == nil {
		return ""
	}
	n := len(g.Segments)
	label := fmt.Sprintf("航段%d", n)
	s := FlightSegment{ID: randID("s"), Label: label, OffsetMode: "auto"}
	if n <= 1 {
		g.Segments = append(g.Segments, s)
	} else {
		idx := n - 1
		g.Segments = append(g.Segments, FlightSegment{})
		copy(g.Segments[idx+1:], g.Segments[idx:])
		g.Segments[idx] = s
	}
	return s.ID
}

func addBlankPlan(t *Trip, copyFrom string) string {
	if t == nil {
		return ""
	}
	name := fmt.Sprintf("方案%d", len(t.Plans)+1)
	p := Plan{ID: randID("p"), Name: name, Hotels: []Hotel{}, Days: []Day{}}
	if copyFrom == "" && !t.SingleDay {
		p.Hotels = []Hotel{{
			ID: randID("h"), CheckIn: t.StartDate, CheckOut: t.EndDate,
			Rooms: []Room{{People: 2, Count: 1}}, Currency: "TWD",
		}}
	}
	if copyFrom != "" {
		if src := planByID(t, copyFrom); src != nil {
			b, _ := json.Marshal(src)
			_ = json.Unmarshal(b, &p)
			p.ID = randID("p")
			p.Name = name
			for i := range p.Hotels {
				p.Hotels[i].ID = randID("h")
			}
			for i := range p.Days {
				p.Days[i].ID = randID("d")
				for j := range p.Days[i].Items {
					p.Days[i].Items[j].ID = randID("i")
					for k := range p.Days[i].Items[j].Notes {
						p.Days[i].Items[j].Notes[k].ID = randID("n")
					}
				}
				for j := range p.Days[i].Meals {
					p.Days[i].Meals[j].ID = randID("m")
				}
			}
		}
	}
	t.Plans = append(t.Plans, p)
	t.ActivePlanID = p.ID
	syncTripDates(t)
	return p.ID
}

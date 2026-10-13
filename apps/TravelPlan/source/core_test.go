package main

import (
	"archive/zip"
	"encoding/json"
	"io"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func TestCloneTripDoesNotShareNestedDraftData(t *testing.T) {
	original := newTripModel()
	original.Plans[0].Days[0].Items = []Item{{Notes: []ItemNote{{Text: "original"}}, Transport: []TransportSegment{{Service: "train"}}}}
	draft := cloneTrip(original)
	draft.Plans[0].Days[0].Items[0].Notes[0].Text = "changed"
	draft.Plans[0].Days[0].Items[0].Transport[0].Service = "bus"
	draft.Plans[0].Hotels[0].Rooms[0].Count = 9
	draft.Flights[0].Segments[0].Airline = "changed"
	if original.Plans[0].Days[0].Items[0].Notes[0].Text != "original" || original.Plans[0].Days[0].Items[0].Transport[0].Service != "train" || original.Plans[0].Hotels[0].Rooms[0].Count != 1 || original.Flights[0].Segments[0].Airline != "" {
		t.Fatal("draft mutated persisted data")
	}
}

func TestCandidateSaveFailurePreservesMemoryAndCleansTemporaryFile(t *testing.T) {
	s := &Server{root: t.TempDir(), data: defaultData()}
	if err := s.ensureDirs(); err != nil {
		t.Fatal(err)
	}
	// A directory at the target forces rename to fail even when running as root.
	if err := os.Mkdir(s.dataPath(), 0755); err != nil {
		t.Fatal(err)
	}
	candidate := cloneAppData(s.data)
	candidate.Trips = []Trip{newTripModel()}
	if err := s.saveCandidate(candidate); err == nil {
		t.Fatal("expected failure")
	}
	if len(s.data.Trips) != 0 {
		t.Fatal("failed save published draft")
	}
	files, _ := filepath.Glob(filepath.Join(s.root, "data", ".travelplan-*.tmp"))
	if len(files) != 0 {
		t.Fatal("temporary files leaked", files)
	}
}

func TestCandidateSaveAndReload(t *testing.T) {
	s := &Server{root: t.TempDir(), data: defaultData()}
	if err := s.ensureDirs(); err != nil {
		t.Fatal(err)
	}
	for i := 0; i < 2; i++ {
		candidate := cloneAppData(s.data)
		candidate.Trips = append(candidate.Trips, newTripModel())
		if err := s.saveCandidate(candidate); err != nil {
			t.Fatal(err)
		}
	}
	loaded := &Server{root: s.root}
	if err := loaded.load(); err != nil {
		t.Fatal(err)
	}
	if len(loaded.data.Trips) != 2 || loaded.data.Version != Version {
		t.Fatal("bad reload", loaded.data)
	}
}

func TestLoadDefaultsAndMalformedDataPreservation(t *testing.T) {
	s := &Server{root: t.TempDir(), data: defaultData()}
	if err := s.ensureDirs(); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(s.dataPath(), []byte(`{"settings":{"dayFormat":"Day1"},"trips":[]}`), 0644); err != nil {
		t.Fatal(err)
	}
	if err := s.load(); err != nil {
		t.Fatal(err)
	}
	if s.data.Settings.DayFormat != "Day1" || !s.data.Settings.WordUndated || s.data.Settings.WeekFormat == "" {
		t.Fatal("missing defaults")
	}
	before, _ := json.Marshal(s.data)
	if err := os.WriteFile(s.dataPath(), []byte(`{"trips":[`), 0644); err != nil {
		t.Fatal(err)
	}
	if err := s.load(); err == nil {
		t.Fatal("expected parse failure")
	}
	after, _ := json.Marshal(s.data)
	if string(before) != string(after) {
		t.Fatal("failed load changed live state")
	}
}

func TestMoveDayContentsKeepsDateIdentity(t *testing.T) {
	p := Plan{Days: []Day{{ID: "a", Date: "2027-01-01", Focus: "A", DailyNote: "note", Meals: []Meal{{Text: "food"}}, Items: []Item{{Text: "activity"}}}, {ID: "b", Date: "2027-01-02", Focus: "B"}}}
	moveDayContents(&p, "a", -1)
	if p.Days[0].Focus != "A" {
		t.Fatal("boundary move")
	}
	moveDayContents(&p, "a", 1)
	if p.Days[0].ID != "a" || p.Days[0].Date != "2027-01-01" || p.Days[0].Focus != "B" || p.Days[1].Meals[0].Text != "food" || p.Days[1].DailyNote != "note" || p.Days[1].Items[0].Text != "activity" {
		t.Fatal("bad content move", p)
	}
}

func TestShortOriginalDateCannotPanic(t *testing.T) {
	if got := dayTitle(Day{OriginalDate: "bad"}, 0, Trip{}, Settings{}); got != "無日期（原 bad）" {
		t.Fatal(got)
	}
}

func TestSingleDayExportHidesAllAccommodationAndOtherDays(t *testing.T) {
	s := &Server{root: t.TempDir(), data: defaultData()}
	if err := s.ensureDirs(); err != nil {
		t.Fatal(err)
	}
	tr := newTripModel()
	tr.StartDate, tr.EndDate = "2027-03-10", "2027-03-12"
	syncTripDates(&tr)
	p := &tr.Plans[0]
	p.Name = "方案/一"
	p.Hotels[0].Name = "HIDDEN-HOTEL"
	p.Hotels[0].CheckIn, p.Hotels[0].CheckOut = tr.StartDate, tr.EndDate
	p.Days[0].Focus, p.Days[1].Focus = "VISIBLE-DAY", "HIDDEN-DAY"
	tr.SingleDay = true
	s.data.Trips = []Trip{tr}
	s.data.Settings.WordDayPage = true
	path, err := s.exportWord(tr.ID, p.ID)
	if err != nil {
		t.Fatal(err)
	}
	z, err := zip.OpenReader(path)
	if err != nil {
		t.Fatal(err)
	}
	defer z.Close()
	found := false
	for _, f := range z.File {
		r, err := f.Open()
		if err != nil {
			t.Fatal(err)
		}
		b, err := io.ReadAll(r)
		r.Close()
		if err != nil {
			t.Fatal(err)
		}
		if f.Name == "word/document.xml" {
			found = true
			x := string(b)
			if !strings.Contains(x, "VISIBLE-DAY") || strings.Contains(x, "HIDDEN-HOTEL") || strings.Contains(x, "HIDDEN-DAY") || strings.Contains(x, `w:type="page"`) {
				t.Fatal("incorrect single-day export", x)
			}
		}
	}
	if !found {
		t.Fatal("missing document")
	}
}

func TestDateShrinkKeepsUndated(t *testing.T) {
	tr := newTripModel()
	tr.StartDate = "2027-01-19"
	tr.EndDate = "2027-01-21"
	syncTripDates(&tr)
	p := planByID(&tr, tr.ActivePlanID)
	if len(p.Days) != 3 {
		t.Fatalf("days=%d", len(p.Days))
	}
	p.Days[0].Focus = "東京"
	tr.StartDate = "2027-01-20"
	tr.EndDate = "2027-01-22"
	syncTripDates(&tr)
	var undated *Day
	has22 := false
	for i := range p.Days {
		d := &p.Days[i]
		if d.Date == "" && d.OriginalDate == "2027-01-19" {
			undated = d
		}
		if d.Date == "2027-01-22" {
			has22 = true
		}
	}
	if undated == nil || undated.Focus != "東京" {
		t.Fatalf("undated data lost: %#v", undated)
	}
	if !has22 {
		t.Fatal("missing new 01/22")
	}
}

func TestNewTripDefaults(t *testing.T) {
	tr := newTripModel()
	if tr.StartDate == "" || tr.EndDate == "" || tr.StartDate != tr.EndDate {
		t.Fatalf("new trip dates invalid: %q %q", tr.StartDate, tr.EndDate)
	}
	if len(tr.Flights) != 1 {
		t.Fatalf("expected one default traveler, got %d", len(tr.Flights))
	}
	if len(tr.Flights[0].Segments) != 2 {
		t.Fatalf("expected outbound and return segments, got %d", len(tr.Flights[0].Segments))
	}
	p := planByID(&tr, tr.ActivePlanID)
	if p == nil {
		t.Fatal("missing default plan")
	}
	if len(p.Hotels) != 1 {
		t.Fatalf("expected one default accommodation row, got %d", len(p.Hotels))
	}
	if len(p.Days) != 1 || p.Days[0].Date != tr.StartDate {
		t.Fatalf("expected today's daily block, got %#v", p.Days)
	}
}

func TestFlightOffsetTPELAXSameDay(t *testing.T) {
	off, err := inferFlightOffset("2027-01-19", "22:40", "16:30", "TPE", "LAX")
	if err != nil {
		t.Fatal(err)
	}
	if off != 0 {
		t.Fatalf("expected same day, got %d", off)
	}
}

func TestExportDocx(t *testing.T) {
	root := t.TempDir()
	s := &Server{root: root, data: defaultData()}
	if err := s.ensureDirs(); err != nil {
		t.Fatal(err)
	}
	tr := newTripModel()
	tr.Title = "測試旅行"
	tr.StartDate = "2027-01-19"
	tr.EndDate = "2027-01-20"
	syncTripDates(&tr)
	p := planByID(&tr, tr.ActivePlanID)
	p.Days[0].Items = append(p.Days[0].Items, Item{ID: randID("i"), Type: "itinerary", Time: "09:00", Text: "出發"})
	s.data.Trips = []Trip{tr}
	s.data.ActiveTripID = tr.ID
	path, err := s.exportWord(tr.ID, p.ID)
	if err != nil {
		t.Fatal(err)
	}
	st, err := os.Stat(path)
	if err != nil {
		t.Fatal(err)
	}
	if st.Size() < 1000 {
		t.Fatalf("docx too small %d", st.Size())
	}
	if filepath.Ext(path) != ".docx" {
		t.Fatal(path)
	}
}

func TestTripMatchesFilter(t *testing.T) {
	tr := Trip{Title: "東京滑雪計劃", Companions: "Simon、Amy", StartDate: "2016-01-20", EndDate: "2016-01-25"}
	cases := []struct {
		name, q, start, end string
		want                bool
	}{
		{"query-title", "東京", "", "", true},
		{"query-companion", "amy", "", "", true},
		{"query-and-date", "東京", "2016/01/21", "2016/02/01", true},
		{"query-and-date-no-query-match", "大阪", "2016/01/21", "2016/02/01", false},
		{"range-overlap", "", "2016/01/01", "2016/02/01", true},
		{"range-before", "", "2016/02/01", "2016/02/10", false},
		{"start-only", "", "2016/01/25", "", true},
		{"start-only-after", "", "2016/01/26", "", false},
		{"end-only", "", "", "2016/01/20", true},
		{"end-only-before", "", "", "2016/01/19", false},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			if got := tripMatchesFilter(tr, tc.q, tc.start, tc.end); got != tc.want {
				t.Fatalf("got=%v want=%v", got, tc.want)
			}
		})
	}
}

func TestDayTitleYearModeNever(t *testing.T) {
	tr := Trip{StartDate: "2026-12-31", EndDate: "2027-01-02"}
	d := Day{Date: "2027-01-01"}
	title := dayTitle(d, 1, tr, Settings{DayFormat: "第1天", WeekFormat: "（一）", YearMode: "never"})
	if strings.Contains(title, "2027/") {
		t.Fatalf("year should be hidden in never mode: %q", title)
	}
}

func TestNormalizeTimeInput(t *testing.T) {
	cases := []struct {
		in, want string
		ok       bool
	}{
		{"1234", "12:34", true},
		{"930", "09:30", true},
		{"09:05", "09:05", true},
		{"1599", "1599", false},
		{"2460", "2460", false},
		{"1:2:3:4", "1:2:3:4", false},
		{"12:3", "12:3", false},
		{"9:30", "09:30", true},
		{"", "", true},
	}
	for _, tc := range cases {
		got, ok := normalizeTimeInput(tc.in)
		if got != tc.want || ok != tc.ok {
			t.Fatalf("normalizeTimeInput(%q)=(%q,%v), want (%q,%v)", tc.in, got, ok, tc.want, tc.ok)
		}
	}
}

func TestTravelerDefaultsAndBlankPlan(t *testing.T) {
	tr := newTripModel()
	if tr.Flights[0].Traveler != "旅客1" {
		t.Fatalf("first traveler=%q", tr.Flights[0].Traveler)
	}
	addFlightGroup(&tr)
	if tr.Flights[1].Traveler != "旅客2" {
		t.Fatalf("second traveler=%q", tr.Flights[1].Traveler)
	}
	pid := addBlankPlan(&tr, "")
	p := planByID(&tr, pid)
	if p == nil || len(p.Hotels) != 1 {
		t.Fatalf("blank multi-day plan should start with one hotel row: %#v", p)
	}
}

func TestSingleDayKeepsEndDateAndOnlyEnsuresStartDay(t *testing.T) {
	tr := newTripModel()
	tr.StartDate = "2027-03-10"
	tr.EndDate = "2027-03-12"
	syncTripDates(&tr)
	p := planByID(&tr, tr.ActivePlanID)
	if p == nil || len(p.Days) != 3 {
		t.Fatalf("expected 3 days before single-day mode, got %#v", p)
	}
	tr.SingleDay = true
	syncTripDates(&tr)
	if tr.EndDate != "2027-03-12" {
		t.Fatalf("single-day mode must not rewrite end date, got %q", tr.EndDate)
	}
	foundStart := false
	for _, d := range p.Days {
		if d.Date == tr.StartDate {
			foundStart = true
			break
		}
	}
	if !foundStart {
		t.Fatal("single-day mode must retain/create start-date day")
	}
	// Leaving single-day mode should restore the original date-range behavior.
	tr.SingleDay = false
	syncTripDates(&tr)
	count := 0
	for _, d := range p.Days {
		if d.Date >= "2027-03-10" && d.Date <= "2027-03-12" {
			count++
		}
	}
	if count != 3 {
		t.Fatalf("expected 3 dated days after leaving single-day mode, got %d: %#v", count, p.Days)
	}
}

func TestSingleDayDateFilterUsesStartDateOnly(t *testing.T) {
	tr := Trip{Title: "單日旅行", StartDate: "2027-03-10", EndDate: "2027-03-15", SingleDay: true}
	if tripMatchesFilter(tr, "", "2027-03-11", "2027-03-15") {
		t.Fatal("single-day trip must not match dates after its start date")
	}
	if !tripMatchesFilter(tr, "", "2027-03-10", "2027-03-10") {
		t.Fatal("single-day trip should match its start date")
	}
}

func TestRenamedFixedMealIsUserContent(t *testing.T) {
	d := Day{Meals: fixedMeals()}
	if !dayEmpty(d) {
		t.Fatal("default meals should be empty")
	}
	d.Meals[0].Name = "早午餐"
	if dayEmpty(d) {
		t.Fatal("renamed meal must survive date shrink")
	}
}

func TestRepeatedBackupsDoNotOverwrite(t *testing.T) {
	s := &Server{root: t.TempDir(), data: defaultData()}
	if err := s.ensureDirs(); err != nil {
		t.Fatal(err)
	}
	a, err := s.backup()
	if err != nil {
		t.Fatal(err)
	}
	b, err := s.backup()
	if err != nil {
		t.Fatal(err)
	}
	if a == b {
		t.Fatal("backup overwritten")
	}
	if _, err := os.Stat(a); err != nil {
		t.Fatal(err)
	}
}

func TestSaveDateValidation(t *testing.T) {
	tr := Trip{StartDate: "2027-03-12", EndDate: "2027-03-10"}
	if validateTripForSave(tr) == nil {
		t.Fatal("reversed dates accepted")
	}
	tr.SingleDay = true
	if err := validateTripForSave(tr); err != nil {
		t.Fatal("single-day must ignore retained end date", err)
	}
	tr.StartDate = "bad"
	if validateTripForSave(tr) == nil {
		t.Fatal("bad start accepted")
	}
}

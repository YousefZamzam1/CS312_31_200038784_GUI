using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace SmartCampus;

public sealed class MainForm : Form
{
    private static readonly Color Canvas = Theme.Canvas;
    private static readonly Color Ink = Theme.Ink;
    private static readonly Color Muted = Theme.Muted;
    private static readonly Color Blue = Theme.Accent;
    private static readonly Color Green = Theme.Green;
    private readonly ICampusRepository _repository;
    private readonly CampusServer _server = new();
    private readonly Panel _sidebar = new();
    private readonly Panel _content = new();
    private readonly Panel _topbar = new();
    private readonly Label _pageTitle = new();
    private readonly Label _clockLabel = new();
    private readonly Label _serverBadge = new();
    private readonly Label _toast = new();
    private readonly System.Windows.Forms.Timer _clock = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _toastTimer = new() { Interval = 2600 };
    private readonly System.Windows.Forms.Timer _navigationTimer = new() { Interval = 15 };
    private readonly System.Windows.Forms.Timer _motionTimer = new() { Interval = 16 };
    private readonly System.Windows.Forms.Timer _serverPulseTimer = new() { Interval = 850 };
    private readonly HttpClient _http = new();
    private readonly Dictionary<Control, (Point Target, int Index)> _entranceItems = [];
    private readonly List<(Label Label, int Target, string Format)> _counterItems = [];
    private int _navWidth = 278;
    private bool _navCollapsed;
    private string _currentPage = "Dashboard";
    private string _search = "";
    private DataGridView? _studentGrid;
    private DateTime _motionStarted;
    private readonly List<CampusRoom> _managedRooms = [];
    private readonly List<(DateTime Date, TimeSpan Start, TimeSpan End)> _roomSlots = [];
    private readonly List<(string Room, DateTime Date, TimeSpan Start, TimeSpan End)> _roomBlocks = [];

    public MainForm()
    {
        _repository = CreateRepository();
        Text = "Integrated Smart Campus | Campus Administration";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1120, 720);
        Size = new Size(1440, 900);
        BackColor = Canvas;
        Font = new Font("Segoe UI", 11F);
        DoubleBuffered = true;
        BuildShell();
        BuildSidebar();
        _server.ClientConnected += _ => BeginInvoke(ShowServerStatus);
        try { _server.Start(); }
        catch (HttpListenerException) { }
        ShowServerStatus();
        _clock.Tick += (_, _) => UpdateClock(); _clock.Start(); UpdateClock();
        _toastTimer.Tick += (_, _) => { _toast.Visible = false; _toastTimer.Stop(); };
        _navigationTimer.Tick += (_, _) => AnimateNavigation();
        _motionTimer.Tick += (_, _) => AnimatePageEntrance();
        _serverPulseTimer.Tick += (_, _) =>
        {
            if (_server.IsRunning) _serverBadge.BackColor = _serverBadge.BackColor == Theme.SoftGreen ? Color.FromArgb(24, 70, 56) : Theme.SoftGreen;
        };
        _serverPulseTimer.Start();
    }

    private static ICampusRepository CreateRepository()
    {
        var connectionString = Environment.GetEnvironmentVariable("SMARTCAMPUS_SQL");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            try
            {
                var repository = new SqlServerCampusRepository(connectionString);
                repository.EnsureConnection();
                return repository;
            }
            catch (Exception)
            {
                MessageBox.Show("SQL Server connection failed. The application will open in demo mode. Check SMARTCAMPUS_SQL and run Database\\schema.sql.", "Database connection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        return new DemoCampusRepository();
    }

    private void BuildShell()
    {
        var shell = new Panel { Dock = DockStyle.Fill, BackColor = Canvas }; Controls.Add(shell);
        _sidebar.Dock = DockStyle.Left; _sidebar.Width = _navWidth; _sidebar.BackColor = Theme.Sidebar; _sidebar.Padding = new Padding(20, 18, 16, 18); shell.Controls.Add(_sidebar);
        _topbar.Dock = DockStyle.Top; _topbar.Height = 96; _topbar.BackColor = Theme.Sidebar; _topbar.Padding = new Padding(30, 0, 30, 0); shell.Controls.Add(_topbar);
        var body = new Panel { BackColor = Canvas, Padding = new Padding(34, 30, 34, 28) }; shell.Controls.Add(body);
        void LayoutBody()
        {
            body.Bounds = new Rectangle(_sidebar.Width, _topbar.Height, Math.Max(0, shell.ClientSize.Width - _sidebar.Width), Math.Max(0, shell.ClientSize.Height - _topbar.Height));
        }
        shell.Resize += (_, _) => LayoutBody();
        LayoutBody();
        _content.Dock = DockStyle.Fill; _content.BackColor = Canvas; _content.AutoScroll = true; body.Controls.Add(_content);
        var menu = MakeButton("☰", Theme.Surface, Ink, 46, 46); menu.Location = new Point(22, 24); menu.Anchor = AnchorStyles.Left | AnchorStyles.Top; menu.Click += (_, _) => ToggleNavigation(); _topbar.Controls.Add(menu);
        _pageTitle.Font = new Font("Segoe UI Semibold", 23, FontStyle.Bold); _pageTitle.ForeColor = Ink; _pageTitle.AutoSize = true; _pageTitle.Location = new Point(84, 25); _topbar.Controls.Add(_pageTitle);
        _clockLabel.AutoSize = true; _clockLabel.ForeColor = Muted; _clockLabel.Font = new Font("Segoe UI", 10); _clockLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right; _clockLabel.Location = new Point(Width - 440, 33); _topbar.Controls.Add(_clockLabel);
        _serverBadge.AutoSize = true; _serverBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right; _serverBadge.Location = new Point(Width - 235, 25); _serverBadge.Padding = new Padding(12, 9, 12, 9); _serverBadge.BackColor = Theme.SoftGreen; _serverBadge.ForeColor = Green; _topbar.Controls.Add(_serverBadge);
        var profile = new Label { Text = "AM", TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold), ForeColor = Ink, BackColor = Theme.SoftAccent, Size = new Size(42, 42), Location = new Point(Width - 82, 22), Anchor = AnchorStyles.Top | AnchorStyles.Right }; _topbar.Controls.Add(profile);
        _toast.Visible = false; _toast.AutoSize = true; _toast.Padding = new Padding(16, 12, 16, 12); _toast.BackColor = Theme.SoftAccent; _toast.ForeColor = Ink; _toast.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold); _toast.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; _toast.Location = new Point(Width - 370, Height - 112); Controls.Add(_toast); _toast.BringToFront();
        Resize += (_, _) => { _toast.Location = new Point(ClientSize.Width - _toast.Width - 28, ClientSize.Height - _toast.Height - 28); };
        Shown += (_, _) => ShowPage("Dashboard");
    }

    private void BuildSidebar()
    {
        _sidebar.Controls.Clear();
        var brand = new Label { Text = "✳", ForeColor = Color.Black, BackColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 25, FontStyle.Bold), Size = new Size(48, 48), Location = new Point(20, 20) }; _sidebar.Controls.Add(brand);
        if (_navWidth > 150)
        {
            AddText(_sidebar, "SC", 82, 15, 150, 42, 18, Theme.Ink, FontStyle.Bold);
            AddText(_sidebar, "CAMPUS", 84, 58, 150, 24, 11, Muted, FontStyle.Bold);
        }
        var y = 112;
        if (_navWidth > 150) { AddText(_sidebar, "WORKSPACE", 18, y, 220, 22, 10, Muted, FontStyle.Bold); y += 32; }
        foreach (var (name, icon) in new[] { ("Dashboard", "◫"), ("Students", "♙"), ("Attendance", "◷"), ("Rooms", "▦"), ("Announcements", "◉"), ("Server & API", "⇄") })
        {
            var selected = name == _currentPage;
            var button = new Button { Text = _navWidth > 150 ? $"   {icon}     {name}" : icon, TextAlign = _navWidth > 150 ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Regular), ForeColor = selected ? Color.White : Ink, BackColor = selected ? Theme.Accent : Theme.Sidebar, Location = new Point(12, y), Size = new Size(_navWidth - 32, 48), Cursor = Cursors.Hand, Tag = name };
            button.FlatAppearance.BorderSize = 0; button.FlatAppearance.MouseOverBackColor = Theme.Header; button.Click += (_, _) => ShowPage((string)button.Tag!); _sidebar.Controls.Add(button); y += 54;
        }
        if (_navWidth > 150)
        {
            y += 7; AddText(_sidebar, "SYSTEM", 18, y, 220, 22, 10, Muted, FontStyle.Bold); y += 32;
            var settings = new Button { Text = "   ⚙     Settings", TextAlign = ContentAlignment.MiddleLeft, FlatStyle = FlatStyle.Flat, ForeColor = Muted, BackColor = Theme.Sidebar, Location = new Point(12, y), Size = new Size(_navWidth - 32, 48), Cursor = Cursors.Hand }; settings.FlatAppearance.BorderSize = 0; settings.Click += (_, _) => ShowToast("Settings are ready for course-specific configuration."); _sidebar.Controls.Add(settings);
            var card = new Panel { Location = new Point(15, _sidebar.Height - 123), Size = new Size(_navWidth - 35, 91), BackColor = Theme.Surface, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom }; _sidebar.Controls.Add(card);
            AddText(card, "◉   CAMPUS SERVER", 11, 11, 190, 18, 8, Blue, FontStyle.Bold); AddText(card, $"Listening on :{_server.Port}", 11, 36, 195, 20, 9, Ink);
        }
    }

    private void ToggleNavigation() { _navCollapsed = !_navCollapsed; _navigationTimer.Start(); }
    private void AnimateNavigation()
    {
        var target = _navCollapsed ? 78 : 278; _navWidth += Math.Sign(target - _navWidth) * Math.Min(22, Math.Abs(target - _navWidth)); _sidebar.Width = _navWidth;
        if (_navWidth == target) { _navigationTimer.Stop(); BuildSidebar(); }
        else BuildSidebar();
    }

    private void ShowPage(string page)
    {
        _currentPage = page; _pageTitle.Text = page; _counterItems.Clear(); BuildSidebar(); _content.SuspendLayout(); _content.Controls.Clear();
        switch (page)
        {
            case "Dashboard": BuildOverview(); break;
            case "Students": BuildStudents(); break;
            case "Attendance": BuildAttendance(); break;
            case "Rooms": BuildRooms(); break;
            case "Announcements": BuildNotifications(); break;
            case "Server & API": BuildNetwork(); break;
        }
        Theme.Apply(_content);
        _content.ResumeLayout(true);
        StartPageEntrance();
    }

    private void StartPageEntrance()
    {
        _motionTimer.Stop();
        _entranceItems.Clear();
        _motionStarted = DateTime.UtcNow;
        var index = 0;
        foreach (Control control in _content.Controls)
        {
            if (!control.Visible) continue;
            var target = control.Location;
            _entranceItems[control] = (target, index++);
            control.Location = new Point(target.X, target.Y + 18);
        }
        foreach (var counter in _counterItems) counter.Label.Text = "0";
        _motionTimer.Start();
    }

    private void AnimatePageEntrance()
    {
        var elapsed = (DateTime.UtcNow - _motionStarted).TotalMilliseconds;
        foreach (var (control, item) in _entranceItems)
        {
            if (control.IsDisposed) continue;
            var progress = Math.Clamp((elapsed - item.Index * 42) / 300d, 0d, 1d);
            var eased = 1d - Math.Pow(1d - progress, 3d);
            control.Location = new Point(item.Target.X, item.Target.Y + (int)Math.Round(18 * (1d - eased)));
        }
        for (var i = 0; i < _counterItems.Count; i++)
        {
            var item = _counterItems[i];
            var progress = Math.Clamp((elapsed - i * 110) / 850d, 0d, 1d);
            var current = (int)Math.Round(item.Target * (1d - Math.Pow(1d - progress, 3d)));
            item.Label.Text = item.Format switch
            {
                "percent" => $"{current / 10d:0.0}%",
                "rooms" => $"{current} / 34",
                _ => current.ToString("N0")
            };
        }
        if (elapsed >= _entranceItems.Count * 42 + 320 && elapsed >= _counterItems.Count * 110 + 900)
        {
            foreach (var (control, item) in _entranceItems) if (!control.IsDisposed) control.Location = item.Target;
            _motionTimer.Stop();
        }
    }

    private void BuildOverview()
    {
        AddText(_content, "Dashboard", 0, 0, 520, 48, 27, Ink, FontStyle.Bold);
        _counterItems.Clear();
        var cards = new[] { ("TOTAL STUDENTS", 1284, "N0", "↑ 8.2%", "♙", Theme.Accent), ("ON CAMPUS", 936, "N0", "72.9% today", "⌂", Theme.Green), ("ATTENDANCE", 948, "percent", "↑ 2.4%", "◷", Theme.Amber), ("ACTIVE ROOMS", 28, "rooms", "6 available", "▦", Theme.Purple) };
        int gap = 16, cardWidth = Math.Max(188, (_content.ClientSize.Width - 3 * gap - 4) / 4); int x = 0;
        foreach (var c in cards) { var panel = MakeCard(x, 116, cardWidth, 132); _content.Controls.Add(panel); AddText(panel, c.Item1, 16, 14, cardWidth - 32, 18, 8, Muted, FontStyle.Bold); var valueLabel = AddText(panel, "0", 15, 39, cardWidth - 85, 51, 22, Ink, FontStyle.Bold); _counterItems.Add((valueLabel, c.Item2, c.Item3)); AddText(panel, c.Item4, 16, 94, cardWidth - 32, 24, 8, Green, FontStyle.Bold); var glyph = new Label { Text = c.Item5, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = c.Item6, BackColor = Theme.SoftAccent, Size = new Size(38,38), Location = new Point(cardWidth - 55, 38) }; panel.Controls.Add(glyph); x += cardWidth + gap; }
        var chart = MakeCard(0, 269, Math.Max(400, (int)(_content.ClientSize.Width * .62)), 300); _content.Controls.Add(chart); AddText(chart, "Weekly attendance", 20, 17, 250, 23, 12, Ink, FontStyle.Bold); AddText(chart, "Weekly check-ins across all departments", 20, 43, 300, 20, 8, Muted); DrawAttendanceChart(chart);
        var activity = MakeCard(chart.Right + 16, 269, Math.Max(260, _content.ClientSize.Width - chart.Right - 16 - 4), 300); _content.Controls.Add(activity); AddText(activity, "Recent activity", 18, 17, 230, 23, 12, Ink, FontStyle.Bold); AddText(activity, "LATEST ACTIVITY", 18, 48, 230, 17, 8, Muted, FontStyle.Bold);
        var items = new[] { ("Mariam Hassan", "Checked in · CS Lab 204", "09:04 AM", Blue), ("Room 301", "Now available for booking", "08:52 AM", Green), ("Omar Khaled", "Arrived late · Database Systems", "08:41 AM", Theme.Amber), ("New announcement", "Midterm schedule published", "Yesterday", Theme.Purple) }; int iy = 75;
        foreach (var i in items) { var dot = new Panel { BackColor = i.Item4, Size = new Size(8,8), Location = new Point(19, iy + 7) }; activity.Controls.Add(dot); AddText(activity, i.Item1, 36, iy, Math.Max(120, activity.Width - 132), 24, 10, Ink, FontStyle.Bold); AddText(activity, i.Item2, 36, iy + 24, Math.Max(120, activity.Width - 54), 24, 9, Muted); AddText(activity, i.Item3, activity.Width - 92, iy, 82, 24, 9, Muted); iy += 52; }
        var lowerY = 590; AddText(_content, "Today’s attendance", 1, lowerY, 270, 24, 13, Ink, FontStyle.Bold); AddText(_content, "LIVE", Math.Max(360, _content.ClientSize.Width - 47), lowerY + 3, 40, 18, 8, Green, FontStyle.Bold);
        var table = MakeGrid(0, lowerY + 34, _content.ClientSize.Width - 6, 231); _content.Controls.Add(table); table.Columns.Add("Student", "STUDENT"); table.Columns.Add("Course", "COURSE"); table.Columns.Add("Time", "CHECK-IN"); table.Columns.Add("Status", "STATUS"); table.Columns[0].Width = 230; table.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; table.Columns[2].Width = 145; table.Columns[3].Width = 140; foreach (var a in _repository.GetAttendance()) table.Rows.Add(a.Student, a.Course, a.Time, a.Status); StyleStatusColumn(table, 3);
    }

    private void DrawAttendanceChart(Panel panel)
    {
        var graph = new Panel { Location = new Point(19, 74), Size = new Size(panel.Width - 38, 205), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BackColor = Theme.Surface }; graph.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            int w = graph.ClientSize.Width - 51, h = graph.ClientSize.Height - 31, ox = 37, oy = 5;
            for (int n = 0; n < 4; n++) { var yy = oy + n * h / 3; using var pen = new Pen(Theme.Line, 1); e.Graphics.DrawLine(pen, ox, yy, ox + w, yy); using var brush = new SolidBrush(Muted); e.Graphics.DrawString($"{100 - n * 25}%", Font, brush, 0, yy - 8); }
            float[] values = [73, 82, 78, 91, 87, 96, 93]; string[] days = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]; var points = new PointF[values.Length];
            for (int i = 0; i < values.Length; i++) { points[i] = new PointF(ox + 5 + i * (w - 10) / 6f, oy + h * (1 - values[i] / 110f)); using var b = new SolidBrush(Muted); e.Graphics.DrawString(days[i], Font, b, points[i].X - 13, oy + h + 6); }
            using var curve = new Pen(Blue, 3); e.Graphics.DrawCurve(curve, points, .25f); foreach (var point in points) { e.Graphics.FillEllipse(Brushes.White, point.X - 4, point.Y - 4, 8, 8); using var p = new Pen(Blue, 2); e.Graphics.DrawEllipse(p, point.X - 4, point.Y - 4, 8, 8); }
        }; panel.Controls.Add(graph);
    }

    private void BuildStudents()
    {
        AddText(_content, "Students", 0, 0, 420, 48, 27, Ink, FontStyle.Bold);
        var add = MakeButton("＋  Add student", Blue, Color.White, 150, 40); add.Location = new Point(_content.ClientSize.Width - 155, 5); add.Anchor = AnchorStyles.Top | AnchorStyles.Right; add.Click += (_, _) => OpenStudentDialog(null); _content.Controls.Add(add);
        var search = new TextBox { PlaceholderText = "⌕   Search name, ID, department or email...", Font = new Font("Segoe UI", 12), BorderStyle = BorderStyle.FixedSingle, Location = new Point(0, 77), Size = new Size(395, 39) }; search.TextChanged += (_, _) => { _search = search.Text; RefreshStudentGrid(); }; _content.Controls.Add(search);
        var panel = MakeCard(0, 131, _content.ClientSize.Width - 5, 522); _content.Controls.Add(panel); _studentGrid = MakeGrid(15, 54, panel.Width - 30, panel.Height - 70); panel.Controls.Add(_studentGrid); _studentGrid.Columns.Add("Id", "STUDENT ID"); _studentGrid.Columns.Add("Name", "FULL NAME"); _studentGrid.Columns.Add("Department", "DEPARTMENT"); _studentGrid.Columns.Add("Email", "EMAIL ADDRESS"); _studentGrid.Columns.Add("Year", "YEAR"); _studentGrid.Columns.Add("Status", "STATUS");
        var actions = new DataGridViewButtonColumn { Name = "Edit", HeaderText = "ACTIONS", Text = "Edit  ·  Delete", UseColumnTextForButtonValue = true, Width = 115 }; _studentGrid.Columns.Add(actions);
        _studentGrid.Columns[0].Width = 100; _studentGrid.Columns[1].Width = 175; _studentGrid.Columns[2].Width = 165; _studentGrid.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; _studentGrid.Columns[4].Width = 84; _studentGrid.Columns[5].Width = 100; _studentGrid.CellContentClick += StudentAction; RefreshStudentGrid();
    }
    private void RefreshStudentGrid() { if (_studentGrid is null || _studentGrid.IsDisposed) return; _studentGrid.Rows.Clear(); foreach (var s in _repository.GetStudents().Where(s => string.Join(" ", s.Id, s.Name, s.Department, s.Email).Contains(_search, StringComparison.OrdinalIgnoreCase))) _studentGrid.Rows.Add(s.Id, s.Name, s.Department, s.Email, s.Year, s.Status, "Edit  ·  Delete"); }
    private void StudentAction(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 6 || _studentGrid is null) return;
        var id = Convert.ToInt32(_studentGrid.Rows[e.RowIndex].Cells[0].Value); var student = _repository.GetStudents().First(x => x.Id == id);
        var choice = MessageBox.Show($"Edit {student.Name}?\nChoose No to delete this record.", "Student actions", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (choice == DialogResult.Yes) OpenStudentDialog(student); else if (choice == DialogResult.No) { _repository.Delete(id); RefreshStudentGrid(); ShowToast("Student record deleted."); }
    }
    private void OpenStudentDialog(Student? existing)
    {
        using var dialog = new Form { Text = existing is null ? "Add student" : "Edit student", StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ClientSize = new Size(420, 450), BackColor = Theme.Surface, Font = Font };
        var name = AddInput(dialog, "FULL NAME", 25, existing?.Name ?? ""); var dept = AddInput(dialog, "DEPARTMENT", 95, existing?.Department ?? "Computer Science"); var email = AddInput(dialog, "EMAIL ADDRESS", 165, existing?.Email ?? ""); var year = AddInput(dialog, "YEAR", 235, existing?.Year ?? "Year 1");
        var error = new ErrorProvider(); var save = MakeButton("Save student", Blue, Color.White, 370, 42); save.Location = new Point(25, 377); dialog.Controls.Add(save); save.Click += (_, _) =>
        {
            error.Clear(); bool valid = true;
            if (string.IsNullOrWhiteSpace(name.Text)) { error.SetError(name, "Name is required."); valid = false; }
            if (!email.Text.Contains('@') || !email.Text.Contains('.')) { error.SetError(email, "Enter a valid email address."); valid = false; }
            if (!valid) return;
            var id = existing?.Id ?? (_repository.GetStudents().Select(s => s.Id).DefaultIfEmpty(24000).Max() + 1);
            _repository.Save(new Student { Id = id, Name = name.Text.Trim(), Department = dept.Text.Trim(), Email = email.Text.Trim(), Year = year.Text.Trim(), Status = existing?.Status ?? "Active" }); dialog.DialogResult = DialogResult.OK; dialog.Close();
        };
        Theme.Apply(dialog); if (dialog.ShowDialog(this) == DialogResult.OK) { RefreshStudentGrid(); ShowToast(existing is null ? "Student added successfully." : "Student updated successfully."); }
    }
    private TextBox AddInput(Control parent, string title, int y, string value) { var label = new Label { Text = title, Location = new Point(25, y), Size = new Size(350, 18), ForeColor = Muted, Font = new Font("Segoe UI", 8, FontStyle.Bold) }; parent.Controls.Add(label); var input = new TextBox { Text = value, Location = new Point(25, y + 22), Size = new Size(370, 32), Font = new Font("Segoe UI", 10) }; parent.Controls.Add(input); return input; }

    private void BuildAttendance()
    {
        AddText(_content, "Attendance", 0, 0, 520, 48, 27, Ink, FontStyle.Bold);
        var summary = new[] { ("PRESENT", "936", Green), ("LATE", "42", Theme.Amber), ("ABSENT", "51", Theme.Red) }; int x = 0;
        foreach (var s in summary) { var p = MakeCard(x, 78, 210, 94); _content.Controls.Add(p); AddText(p, s.Item1, 17, 14, 160, 17, 8, Muted, FontStyle.Bold); AddText(p, s.Item2, 16, 42, 165, 48, 22, s.Item3, FontStyle.Bold); x += 226; }
        var grid = MakeGrid(0, 194, _content.ClientSize.Width - 5, 420); _content.Controls.Add(grid); foreach (var c in new[] { "STUDENT", "COURSE", "CHECK-IN TIME", "STATUS" }) grid.Columns.Add(c, c); grid.Columns[0].Width = 250; grid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; grid.Columns[2].Width = 170; grid.Columns[3].Width = 150; foreach (var a in _repository.GetAttendance()) grid.Rows.Add(a.Student, a.Course, a.Time, a.Status); StyleStatusColumn(grid, 3);
    }
    private void BuildRooms()
    {
        if (_managedRooms.Count == 0) _managedRooms.AddRange(_repository.GetRooms());
        if (_roomSlots.Count == 0)
        {
            _roomSlots.Add((DateTime.Today, TimeSpan.FromHours(9), TimeSpan.FromHours(11)));
            _roomSlots.Add((DateTime.Today, TimeSpan.FromHours(12), TimeSpan.FromHours(14)));
        }
        AddText(_content, "Rooms", 0, 0, 420, 48, 27, Ink, FontStyle.Bold);
        var openWindow = MakeButton("Open Resources window ↗", Theme.SoftAccent, Ink, 215, 38); openWindow.Location = new Point(_content.ClientSize.Width - 225, 4); openWindow.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        openWindow.Click += (_, _) => { using (var win = new ResourcesForm(_repository, _managedRooms, _roomSlots, _roomBlocks)) win.ShowDialog(this); ShowPage("Rooms"); };
        _content.Controls.Add(openWindow);

        var roomCard = MakeCard(0, 76, _content.ClientSize.Width - 8, 276); _content.Controls.Add(roomCard);
        AddText(roomCard, "01  ·  ROOMS MANAGEMENT", 20, 17, 360, 20, 9, Blue, FontStyle.Bold);
        AddText(roomCard, "Room name", 22, 52, 180, 18, 8, Muted, FontStyle.Bold);
        AddText(roomCard, "Building / location", 222, 52, 220, 18, 8, Muted, FontStyle.Bold);
        AddText(roomCard, "Capacity", 452, 52, 100, 18, 8, Muted, FontStyle.Bold);
        var roomName = new TextBox { Location = new Point(22, 78), Size = new Size(185, 34), Font = new Font("Segoe UI", 11), PlaceholderText = "e.g. Lecture Hall B" };
        var building = new TextBox { Location = new Point(222, 78), Size = new Size(210, 34), Font = new Font("Segoe UI", 11), PlaceholderText = "e.g. Main building · Floor 2" };
        var capacity = new NumericUpDown { Location = new Point(452, 78), Size = new Size(95, 34), Minimum = 1, Maximum = 1000, Value = 30, Font = new Font("Segoe UI", 9) };
        roomCard.Controls.AddRange([roomName, building, capacity]);
        var addRoom = MakeButton("＋  Add room", Blue, Color.White, 125, 35); addRoom.Location = new Point(565, 77); addRoom.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        var removeRoom = MakeButton("Remove selected", Theme.SoftRed, Theme.Red, 145, 35);
        removeRoom.Location = new Point(700, 77); removeRoom.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        var roomGrid = MakeGrid(22, 125, roomCard.Width - 44, 132); roomGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        roomGrid.Columns.Add("Room", "ROOM NAME"); roomGrid.Columns.Add("Building", "BUILDING / LOCATION"); roomGrid.Columns.Add("Capacity", "CAPACITY"); roomGrid.Columns.Add("State", "STATUS");
        roomGrid.Columns[0].Width = 210; roomGrid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; roomGrid.Columns[2].Width = 110; roomGrid.Columns[3].Width = 130;
        roomCard.Controls.AddRange([addRoom, removeRoom, roomGrid]);

        var slotCard = MakeCard(0, 368, _content.ClientSize.Width - 8, 226); _content.Controls.Add(slotCard);
        AddText(slotCard, "02  ·  DATES & TIME SLOTS", 20, 17, 360, 20, 9, Blue, FontStyle.Bold);
        AddText(slotCard, "Available date", 22, 52, 180, 18, 8, Muted, FontStyle.Bold);
        AddText(slotCard, "Start time", 222, 52, 150, 18, 8, Muted, FontStyle.Bold);
        AddText(slotCard, "End time", 392, 52, 150, 18, 8, Muted, FontStyle.Bold);
        var slotDate = new DateTimePicker { Location = new Point(22, 78), Size = new Size(180, 34), Format = DateTimePickerFormat.Short, MinDate = DateTime.Today, Font = new Font("Segoe UI", 9) };
        var slotStart = new DateTimePicker { Location = new Point(222, 78), Size = new Size(150, 34), Format = DateTimePickerFormat.Time, ShowUpDown = true, Font = new Font("Segoe UI", 9), Value = DateTime.Today.AddHours(9) };
        var slotEnd = new DateTimePicker { Location = new Point(392, 78), Size = new Size(150, 34), Format = DateTimePickerFormat.Time, ShowUpDown = true, Font = new Font("Segoe UI", 9), Value = DateTime.Today.AddHours(11) };
        var addSlot = MakeButton("＋  Add date & slot", Blue, Color.White, 170, 35); addSlot.Location = new Point(565, 77); addSlot.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        slotCard.Controls.AddRange([slotDate, slotStart, slotEnd, addSlot]);
        var slotGrid = MakeGrid(22, 125, slotCard.Width - 44, 88); slotGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        slotGrid.Columns.Add("Date", "DATE"); slotGrid.Columns.Add("Start", "START TIME"); slotGrid.Columns.Add("End", "END TIME"); slotGrid.Columns[0].Width = 210; slotGrid.Columns[1].Width = 190; slotGrid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; slotCard.Controls.Add(slotGrid);

        var blockCard = MakeCard(0, 610, _content.ClientSize.Width - 8, 238); _content.Controls.Add(blockCard);
        AddText(blockCard, "03  ·  ROOM UNAVAILABILITY", 20, 17, 360, 20, 9, Blue, FontStyle.Bold);
        AddText(blockCard, "Select room", 22, 52, 190, 18, 8, Muted, FontStyle.Bold);
        AddText(blockCard, "Select date", 232, 52, 180, 18, 8, Muted, FontStyle.Bold);
        AddText(blockCard, "Select time slot", 432, 52, 260, 18, 8, Muted, FontStyle.Bold);
        var roomPicker = new ComboBox { Location = new Point(22, 78), Size = new Size(190, 34), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9) };
        var blockDate = new DateTimePicker { Location = new Point(232, 78), Size = new Size(180, 34), Format = DateTimePickerFormat.Short, MinDate = DateTime.Today, Font = new Font("Segoe UI", 9) };
        var blockSlot = new ComboBox { Location = new Point(432, 78), Size = new Size(250, 34), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9) };
        var markUnavailable = MakeButton("Mark room as unavailable", Theme.SoftRed, Theme.Red, 205, 35); markUnavailable.Location = new Point(700, 77); markUnavailable.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        blockCard.Controls.AddRange([roomPicker, blockDate, blockSlot, markUnavailable]);
        var blockGrid = MakeGrid(22, 125, blockCard.Width - 44, 100); blockGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        blockGrid.Columns.Add("Room", "ROOM"); blockGrid.Columns.Add("Date", "DATE"); blockGrid.Columns.Add("Slot", "UNAVAILABLE TIME SLOT"); blockGrid.Columns[0].Width = 210; blockGrid.Columns[1].Width = 190; blockGrid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; blockCard.Controls.Add(blockGrid);

        void RefreshTables()
        {
            roomGrid.Rows.Clear(); foreach (var r in _managedRooms) roomGrid.Rows.Add(r.Name, r.Building, r.Capacity, r.State);
            slotGrid.Rows.Clear(); foreach (var s in _roomSlots) slotGrid.Rows.Add(s.Date.ToString("dd/MM/yyyy"), DateTime.Today.Add(s.Start).ToString("hh:mm tt"), DateTime.Today.Add(s.End).ToString("hh:mm tt"));
            blockGrid.Rows.Clear(); foreach (var b in _roomBlocks) blockGrid.Rows.Add(b.Room, b.Date.ToString("dd/MM/yyyy"), $"{DateTime.Today.Add(b.Start):hh:mm tt} - {DateTime.Today.Add(b.End):hh:mm tt}");
            var selected = roomPicker.SelectedItem?.ToString(); roomPicker.Items.Clear(); foreach (var r in _managedRooms) roomPicker.Items.Add(r.Name); if (selected is not null && roomPicker.Items.Contains(selected)) roomPicker.SelectedItem = selected; else if (roomPicker.Items.Count > 0) roomPicker.SelectedIndex = 0;
            var date = blockDate.Value.Date; var oldSlot = blockSlot.SelectedItem?.ToString(); blockSlot.Items.Clear();
            foreach (var slot in _roomSlots.Where(s => s.Date.Date == date)) blockSlot.Items.Add($"{DateTime.Today.Add(slot.Start):hh:mm tt} - {DateTime.Today.Add(slot.End):hh:mm tt}");
            if (oldSlot is not null && blockSlot.Items.Contains(oldSlot)) blockSlot.SelectedItem = oldSlot; else if (blockSlot.Items.Count > 0) blockSlot.SelectedIndex = 0;
        }
        addRoom.Click += (_, _) =>
        {
            var name = roomName.Text.Trim(); if (name.Length == 0 || building.Text.Trim().Length == 0) { ShowToast("Enter the room name and building."); return; }
            if (_managedRooms.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) { ShowToast("A room with this name already exists."); return; }
            var newRoom = new CampusRoom(name, building.Text.Trim(), (int)capacity.Value, "Available");
            try { _repository.SaveRoom(newRoom); _managedRooms.Add(newRoom); roomName.Clear(); building.Clear(); RefreshTables(); ShowToast("Room added successfully."); }
            catch (Exception ex) { ShowToast("Could not save room: " + ex.Message); }
        };
        removeRoom.Click += (_, _) =>
        {
            if (roomGrid.CurrentRow is null) { ShowToast("Select a room to remove."); return; }
            var name = Convert.ToString(roomGrid.CurrentRow.Cells[0].Value) ?? "";
            try { _repository.DeleteRoom(name); _managedRooms.RemoveAll(r => r.Name == name); _roomBlocks.RemoveAll(b => b.Room == name); RefreshTables(); ShowToast("Selected room removed."); }
            catch (Exception ex) { ShowToast("Could not remove room: " + ex.Message); }
        };
        addSlot.Click += (_, _) =>
        {
            var start = slotStart.Value.TimeOfDay; var end = slotEnd.Value.TimeOfDay;
            if (end <= start) { ShowToast("End time must be later than start time."); return; }
            if (_roomSlots.Any(s => s.Date.Date == slotDate.Value.Date && start < s.End && end > s.Start)) { ShowToast("This time overlaps an existing slot."); return; }
            _roomSlots.Add((slotDate.Value.Date, start, end)); _roomSlots.Sort((a, b) => a.Date != b.Date ? a.Date.CompareTo(b.Date) : a.Start.CompareTo(b.Start)); RefreshTables(); ShowToast("Date and time slot added.");
        };
        blockDate.ValueChanged += (_, _) => RefreshTables();
        markUnavailable.Click += (_, _) =>
        {
            if (roomPicker.SelectedItem is null || blockSlot.SelectedIndex < 0) { ShowToast("Choose a room and an available time slot."); return; }
            var room = roomPicker.SelectedItem.ToString()!; var slotText = blockSlot.SelectedItem!.ToString()!;
            var slot = _roomSlots.First(s => s.Date.Date == blockDate.Value.Date && $"{DateTime.Today.Add(s.Start):hh:mm tt} - {DateTime.Today.Add(s.End):hh:mm tt}" == slotText);
            if (_roomBlocks.Any(b => b.Room == room && b.Date.Date == blockDate.Value.Date && b.Start == slot.Start && b.End == slot.End)) { ShowToast("This room is already unavailable in that slot."); return; }
            _roomBlocks.Add((room, blockDate.Value.Date, slot.Start, slot.End)); RefreshTables(); ShowToast("Room marked as unavailable.");
        };
        RefreshTables();
    }
    private void BuildNotifications()
    {
        AddText(_content, "Announcements", 0, 0, 520, 48, 27, Ink, FontStyle.Bold); var card = MakeCard(0, 80, Math.Min(690, _content.ClientSize.Width - 5), 390); _content.Controls.Add(card);
        AddText(card, "Create announcement", 22, 20, 300, 24, 12, Ink, FontStyle.Bold); AddText(card, "TITLE", 23, 66, 540, 18, 8, Muted, FontStyle.Bold); var title = new TextBox { Location = new Point(23, 88), Size = new Size(card.Width - 46, 38), Font = new Font("Segoe UI", 10), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, PlaceholderText = "e.g. Midterm schedule published" }; card.Controls.Add(title);
        AddText(card, "MESSAGE", 23, 143, 540, 18, 8, Muted, FontStyle.Bold); var message = new TextBox { Location = new Point(23, 165), Size = new Size(card.Width - 46, 105), Font = new Font("Segoe UI", 10), Multiline = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, PlaceholderText = "Write a clear message for students and staff..." }; card.Controls.Add(message);
        var publish = MakeButton("Publish notification", Blue, Color.White, 175, 42); publish.Location = new Point(23, 299); publish.Click += async (_, _) => { if (string.IsNullOrWhiteSpace(title.Text) || string.IsNullOrWhiteSpace(message.Text)) { ShowToast("Enter both a title and a message."); return; } publish.Enabled = false; try { var n = new CampusNotice(title.Text.Trim(), message.Text.Trim(), DateTime.Now); _server.Publish(n); using var body = new StringContent(JsonSerializer.Serialize(n), Encoding.UTF8, "application/json"); try { await _http.PostAsync($"http://127.0.0.1:{_server.Port}/notices", body); } catch (HttpRequestException) { } ShowToast("Notification published to the campus server."); title.Clear(); message.Clear(); } finally { publish.Enabled = true; } }; card.Controls.Add(publish);
        int iy = 493; AddText(_content, "Recent notifications", 0, iy, 360, 24, 13, Ink, FontStyle.Bold); foreach (var n in _server.Notices) { var p = MakeCard(0, iy + 34, Math.Min(690, _content.ClientSize.Width - 5), 85); _content.Controls.Add(p); AddText(p, n.Title, 16, 13, p.Width - 35, 20, 10, Ink, FontStyle.Bold); AddText(p, n.Message, 16, 38, p.Width - 35, 19, 8, Muted); iy += 98; }
    }
    private void BuildNetwork()
    {
        AddText(_content, "Server & API", 0, 0, 520, 48, 27, Ink, FontStyle.Bold);
        var status = MakeCard(0, 78, 470, 165); _content.Controls.Add(status); AddText(status, "SERVER STATUS", 20, 18, 300, 18, 8, Muted, FontStyle.Bold); AddText(status, _server.IsRunning ? "●  Online" : "○  Offline", 20, 49, 300, 30, 17, _server.IsRunning ? Green : Theme.Red, FontStyle.Bold); AddText(status, $"Listening on  http://127.0.0.1:{_server.Port}/", 20, 92, 410, 21, 9, Ink);
        var toggle = MakeButton(_server.IsRunning ? "Stop server" : "Start server", _server.IsRunning ? Theme.SoftRed : Blue, _server.IsRunning ? Theme.Red : Color.White, 130, 36); toggle.Location = new Point(20, 119); toggle.Click += (_, _) => { if (_server.IsRunning) _server.Stop(); else _server.Start(); ShowPage("Server & API"); }; status.Controls.Add(toggle);
        var api = MakeCard(486, 78, Math.Max(330, _content.ClientSize.Width - 491), 165); _content.Controls.Add(api); AddText(api, "AVAILABLE ENDPOINTS", 18, 18, 300, 18, 8, Muted, FontStyle.Bold); AddText(api, "GET   /health", 18, 51, 250, 20, 10, Blue, FontStyle.Bold); AddText(api, "GET   /notices", 18, 80, 250, 20, 10, Blue, FontStyle.Bold); AddText(api, "POST /notices", 18, 109, 250, 20, 10, Blue, FontStyle.Bold);
        var demo = MakeCard(0, 260, Math.Min(680, _content.ClientSize.Width - 5), 255); _content.Controls.Add(demo); AddText(demo, "Send a test message", 20, 18, 300, 24, 12, Ink, FontStyle.Bold); AddText(demo, "This client sends a JSON notification to the local server using HTTP over TCP.", 20, 49, demo.Width - 40, 40, 9, Muted); var button = MakeButton("Send test notification", Blue, Color.White, 190, 42); button.Location = new Point(20, 107); button.Click += async (_, _) => { var n = new CampusNotice("Client test", "Message delivered from the dashboard client to the campus server.", DateTime.Now); using var body = new StringContent(JsonSerializer.Serialize(n), Encoding.UTF8, "application/json"); try { var response = await _http.PostAsync($"http://127.0.0.1:{_server.Port}/notices", body); ShowToast(response.IsSuccessStatusCode ? "Client message received by server." : "Server returned an error."); } catch (Exception ex) { ShowToast("Client could not connect: " + ex.Message); } }; demo.Controls.Add(button);
        var help = MakeButton("Open API health check ↗", Theme.SoftAccent, Ink, 195, 38); help.Location = new Point(20, 164); help.Click += async (_, _) => { try { var json = await _http.GetStringAsync($"http://127.0.0.1:{_server.Port}/health"); MessageBox.Show(json, "Server health check"); } catch (Exception ex) { MessageBox.Show(ex.Message, "Connection error"); } }; demo.Controls.Add(help);
    }

    private static DataGridView MakeGrid(int x, int y, int width, int height)
    {
        var grid = new DataGridView { Location = new Point(x, y), Size = new Size(width, height), BackgroundColor = Theme.Surface, BorderStyle = BorderStyle.None, GridColor = Theme.Line, AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, EnableHeadersVisualStyles = false, Font = new Font("Segoe UI", 10.5F), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Header; grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted; grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold); grid.ColumnHeadersHeight = 46; grid.DefaultCellStyle.ForeColor = Ink; grid.DefaultCellStyle.SelectionBackColor = Theme.SoftAccent; grid.DefaultCellStyle.SelectionForeColor = Ink; grid.DefaultCellStyle.Padding = new Padding(8, 0, 4, 0); grid.RowTemplate.Height = 48; grid.AlternatingRowsDefaultCellStyle.BackColor = Theme.RowAlt; Theme.StyleGrid(grid); return grid;
    }
    private static void StyleStatusColumn(DataGridView grid, int index) { if (grid.Columns.Count > index) grid.Columns[index].DefaultCellStyle.Font = new Font("Segoe UI Semibold", 8, FontStyle.Bold); }
    private static Panel MakeCard(int x, int y, int width, int height) { var p = new Panel { Location = new Point(x, y), Size = new Size(Math.Max(100, width), height), BackColor = Theme.Surface, Padding = new Padding(1) }; p.Paint += (_, e) => { using var pen = new Pen(Theme.Line); e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1); }; return p; }
    private static Button MakeButton(string text, Color back, Color fore, int width, int height) { var b = new Button { Text = text, Size = new Size(width, height), BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold), Cursor = Cursors.Hand }; b.FlatAppearance.BorderSize = 0; return b; }
    private static Label AddText(Control parent, string text, int x, int y, int width, int height, float size, Color color, FontStyle style = FontStyle.Regular) { var readableSize = Math.Max(11F, size * 1.22F); var readableHeight = Math.Max(height, (int)Math.Ceiling(readableSize * 1.75F) + 4); var l = new Label { Text = text, Location = new Point(x, y), Size = new Size(Math.Max(20, width), readableHeight), Font = new Font("Segoe UI", readableSize, style), ForeColor = color, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, UseCompatibleTextRendering = true }; parent.Controls.Add(l); return l; }
    private void ShowToast(string message) { _toast.Text = "✓   " + message; _toast.Visible = true; _toast.Location = new Point(ClientSize.Width - _toast.Width - 28, ClientSize.Height - _toast.Height - 28); _toast.BringToFront(); _toastTimer.Stop(); _toastTimer.Start(); }
    private void UpdateClock() => _clockLabel.Text = DateTime.Now.ToString("ddd, MMM dd  ·  hh:mm tt");
    private void ShowServerStatus() { if (IsDisposed) return; _serverBadge.Text = _server.IsRunning ? $"●  Server online  ·  :{_server.Port}" : "○  Server offline"; _serverBadge.ForeColor = _server.IsRunning ? Green : Theme.Red; }
    protected override void OnFormClosed(FormClosedEventArgs e) { _clock.Stop(); _http.Dispose(); _server.Dispose(); base.OnFormClosed(e); }
}


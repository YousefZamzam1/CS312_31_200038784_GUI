using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace SmartCampus;





public sealed class ResourcesForm : Form
{
    private readonly ICampusRepository _repository;
    private readonly List<CampusRoom> _rooms;
    private readonly List<(DateTime Date, TimeSpan Start, TimeSpan End)> _slots;
    private readonly List<(string Room, DateTime Date, TimeSpan Start, TimeSpan End)> _blocks;

    private readonly DataGridView _roomGrid = NewGrid();
    private readonly DataGridView _slotGrid = NewGrid();
    private readonly DataGridView _blockGrid = NewGrid();
    private readonly TextBox _roomName = new();
    private readonly NumericUpDown _capacity = new();
    private readonly DateTimePicker _slotDate = new();
    private readonly DateTimePicker _slotStart = new();
    private readonly DateTimePicker _slotEnd = new();
    private readonly ComboBox _roomPicker = new();
    private readonly ComboBox _datePicker = new();
    private readonly ComboBox _slotPicker = new();

    private sealed record SlotItem(TimeSpan Start, TimeSpan End)
    {
        public override string ToString() => $"{Fmt(Start)} - {Fmt(End)}";
    }

    public ResourcesForm(
        ICampusRepository repository,
        List<CampusRoom> rooms,
        List<(DateTime Date, TimeSpan Start, TimeSpan End)> slots,
        List<(string Room, DateTime Date, TimeSpan Start, TimeSpan End)> blocks)
    {
        _repository = repository; _rooms = rooms; _slots = slots; _blocks = blocks;

        Text = "Resources Management";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(700, 790);
        MinimumSize = new Size(640, 720);
        BackColor = Theme.Canvas;
        ForeColor = Theme.Ink;
        Font = new Font("Segoe UI", 10F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(14), BackColor = Theme.Canvas
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.4F));
        Controls.Add(layout);

        layout.Controls.Add(BuildRoomsGroup(), 0, 0);
        layout.Controls.Add(BuildSlotsGroup(), 0, 1);
        layout.Controls.Add(BuildBlocksGroup(), 0, 2);

        Theme.Apply(this);
        RefreshAll();
    }

    
    private GroupBox BuildRoomsGroup()
    {
        var g = NewGroup("Rooms Management");
        AddLabel(g, "Room Name", 14, 26);
        AddLabel(g, "Capacity", 204, 26);
        _roomName.SetBounds(14, 46, 175, 28); _roomName.PlaceholderText = "e.g. Room A";
        _capacity.SetBounds(204, 46, 90, 28); _capacity.Minimum = 1; _capacity.Maximum = 1000; _capacity.Value = 30;
        var add = NewButton("Add Room", Theme.Accent, Color.White, 312, 44, 110);
        var remove = NewButton("Remove Selected Room", Theme.SoftRed, Theme.Red, 432, 44, 190);
        _roomGrid.SetBounds(14, 88, g.Width - 28, g.Height - 100);
        _roomGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _roomGrid.Columns.Add("Room", "Room Name"); _roomGrid.Columns.Add("Capacity", "Capacity");
        _roomGrid.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _roomGrid.Columns[1].Width = 140;
        g.Controls.AddRange([_roomName, _capacity, add, remove, _roomGrid]);

        add.Click += (_, _) =>
        {
            var name = _roomName.Text.Trim();
            if (name.Length == 0) { Warn("Enter the room name."); return; }
            if (_rooms.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) { Warn("A room with this name already exists."); return; }
            var room = new CampusRoom(name, "Main campus", (int)_capacity.Value, "Available");
            try { _repository.SaveRoom(room); _rooms.Add(room); _roomName.Clear(); RefreshAll(); }
            catch (Exception ex) { Warn("Could not save room: " + ex.Message); }
        };
        remove.Click += (_, _) =>
        {
            if (_roomGrid.CurrentRow is null) { Warn("Select a room to remove."); return; }
            var name = Convert.ToString(_roomGrid.CurrentRow.Cells[0].Value) ?? "";
            try { _repository.DeleteRoom(name); _rooms.RemoveAll(r => r.Name == name); _blocks.RemoveAll(b => b.Room == name); RefreshAll(); }
            catch (Exception ex) { Warn("Could not remove room: " + ex.Message); }
        };
        return g;
    }

    
    private GroupBox BuildSlotsGroup()
    {
        var g = NewGroup("Dates & Time Slots");
        AddLabel(g, "Date", 14, 26);
        AddLabel(g, "Start Time", 184, 26);
        AddLabel(g, "End Time", 314, 26);
        _slotDate.SetBounds(14, 46, 155, 28); _slotDate.Format = DateTimePickerFormat.Short; _slotDate.MinDate = DateTime.Today;
        _slotStart.SetBounds(184, 46, 115, 28); _slotStart.Format = DateTimePickerFormat.Time; _slotStart.ShowUpDown = true; _slotStart.Value = DateTime.Today.AddHours(9);
        _slotEnd.SetBounds(314, 46, 115, 28); _slotEnd.Format = DateTimePickerFormat.Time; _slotEnd.ShowUpDown = true; _slotEnd.Value = DateTime.Today.AddHours(11);
        var add = NewButton("Add Date & Slot", Theme.Accent, Color.White, 446, 44, 176);
        _slotGrid.SetBounds(14, 88, g.Width - 28, g.Height - 100);
        _slotGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _slotGrid.Columns.Add("Date", "Date"); _slotGrid.Columns.Add("Start", "Start Time"); _slotGrid.Columns.Add("End", "End Time");
        _slotGrid.Columns[0].Width = 170; _slotGrid.Columns[1].Width = 150; _slotGrid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        g.Controls.AddRange([_slotDate, _slotStart, _slotEnd, add, _slotGrid]);

        add.Click += (_, _) =>
        {
            var date = _slotDate.Value.Date; var start = _slotStart.Value.TimeOfDay; var end = _slotEnd.Value.TimeOfDay;
            if (end <= start) { Warn("End time must be later than start time."); return; }
            if (_slots.Any(s => s.Date.Date == date && start < s.End && end > s.Start)) { Warn("This time overlaps an existing slot."); return; }
            _slots.Add((date, start, end));
            _slots.Sort((a, b) => a.Date != b.Date ? a.Date.CompareTo(b.Date) : a.Start.CompareTo(b.Start));
            RefreshAll();
        };
        return g;
    }

    
    private GroupBox BuildBlocksGroup()
    {
        var g = NewGroup("Room Unavailability");
        AddLabel(g, "Select Room", 14, 24);
        AddLabel(g, "Select Date", 194, 24);
        AddLabel(g, "Select Slot", 374, 24);
        _roomPicker.SetBounds(14, 42, 165, 28); _roomPicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _datePicker.SetBounds(194, 42, 165, 28); _datePicker.DropDownStyle = ComboBoxStyle.DropDownList; _datePicker.FormattingEnabled = true;
        _datePicker.Format += (_, e) => { if (e.ListItem is DateTime d) e.Value = d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture); };
        _slotPicker.SetBounds(374, 42, 200, 28); _slotPicker.DropDownStyle = ComboBoxStyle.DropDownList;
        var mark = NewButton("Mark Room as Unavailable", Theme.SoftRed, Theme.Red, 14, 78, 220);
        _blockGrid.SetBounds(14, 118, g.Width - 28, g.Height - 130);
        _blockGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _blockGrid.Columns.Add("Room", "Room"); _blockGrid.Columns.Add("Date", "Date"); _blockGrid.Columns.Add("Slot", "Slot");
        _blockGrid.Columns[0].Width = 160; _blockGrid.Columns[1].Width = 150; _blockGrid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        g.Controls.AddRange([_roomPicker, _datePicker, _slotPicker, mark, _blockGrid]);

        _datePicker.SelectedIndexChanged += (_, _) => RefreshSlotPicker();
        mark.Click += (_, _) =>
        {
            if (_roomPicker.SelectedItem is not string room || _datePicker.SelectedItem is not DateTime date || _slotPicker.SelectedItem is not SlotItem slot)
            { Warn("Choose a room, a date and a time slot first."); return; }
            if (_blocks.Any(b => b.Room == room && b.Date.Date == date.Date && b.Start == slot.Start && b.End == slot.End))
            { Warn("This room is already unavailable in that slot."); return; }
            _blocks.Add((room, date.Date, slot.Start, slot.End));
            RefreshAll();
        };
        return g;
    }

    
    private void RefreshAll()
    {
        _roomGrid.Rows.Clear();
        foreach (var r in _rooms) _roomGrid.Rows.Add(r.Name, r.Capacity);

        _slotGrid.Rows.Clear();
        foreach (var s in _slots) _slotGrid.Rows.Add(s.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), Fmt(s.Start), Fmt(s.End));

        _blockGrid.Rows.Clear();
        foreach (var b in _blocks) _blockGrid.Rows.Add(b.Room, b.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), $"{Fmt(b.Start)} - {Fmt(b.End)}");

        var oldRoom = _roomPicker.SelectedItem as string;
        _roomPicker.Items.Clear();
        foreach (var r in _rooms) _roomPicker.Items.Add(r.Name);
        if (oldRoom is not null && _roomPicker.Items.Contains(oldRoom)) _roomPicker.SelectedItem = oldRoom;
        else if (_roomPicker.Items.Count > 0) _roomPicker.SelectedIndex = 0;

        var oldDate = _datePicker.SelectedItem as DateTime?;
        _datePicker.Items.Clear();
        foreach (var d in _slots.Select(s => s.Date.Date).Distinct().OrderBy(d => d)) _datePicker.Items.Add(d);
        if (oldDate is not null && _datePicker.Items.Contains(oldDate.Value)) _datePicker.SelectedItem = oldDate.Value;
        else if (_datePicker.Items.Count > 0) _datePicker.SelectedIndex = 0;

        RefreshSlotPicker();
    }

    private void RefreshSlotPicker()
    {
        var old = _slotPicker.SelectedItem as SlotItem;
        _slotPicker.Items.Clear();
        if (_datePicker.SelectedItem is DateTime date)
            foreach (var s in _slots.Where(s => s.Date.Date == date.Date).OrderBy(s => s.Start)) _slotPicker.Items.Add(new SlotItem(s.Start, s.End));
        if (old is not null && _slotPicker.Items.Contains(old)) _slotPicker.SelectedItem = old;
        else if (_slotPicker.Items.Count > 0) _slotPicker.SelectedIndex = 0;
    }

    
    private static string Fmt(TimeSpan t) => DateTime.Today.Add(t).ToString("hh:mm tt", CultureInfo.InvariantCulture);

    private void Warn(string message) => MessageBox.Show(this, message, "Resources Management", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private static GroupBox NewGroup(string title) => new()
    {
        Text = title, Size = new Size(670, 240), Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8), ForeColor = Theme.Ink, BackColor = Theme.Surface, Padding = new Padding(4)
    };

    private static void AddLabel(Control parent, string text, int x, int y) =>
        parent.Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = Theme.Muted, BackColor = Color.Transparent });

    private static Button NewButton(string text, Color back, Color fore, int x, int y, int width)
    {
        var b = new Button
        {
            Text = text, Location = new Point(x, y), Size = new Size(width, 32), BackColor = back, ForeColor = fore,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold)
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private static DataGridView NewGrid()
    {
        var g = new DataGridView
        {
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true, RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false, ColumnHeadersHeight = 32, Font = new Font("Segoe UI", 10F)
        };
        g.RowTemplate.Height = 34;
        Theme.StyleGrid(g);
        return g;
    }
}

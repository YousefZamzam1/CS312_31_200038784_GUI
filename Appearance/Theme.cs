using System.Drawing;
using System.Windows.Forms;

namespace SmartCampus;

internal static class Theme
{
    public static readonly Color Canvas = Color.FromArgb(250, 250, 250);
    public static readonly Color Sidebar = Color.White;
    public static readonly Color Surface = Color.White;
    public static readonly Color Input = Color.White;
    public static readonly Color Header = Color.FromArgb(242, 242, 242);
    public static readonly Color RowAlt = Color.FromArgb(248, 248, 248);
    public static readonly Color Line = Color.FromArgb(210, 210, 210);
    public static readonly Color Ink = Color.FromArgb(20, 20, 20);
    public static readonly Color Muted = Color.FromArgb(85, 85, 85);
    public static readonly Color Accent = Color.FromArgb(20, 20, 20);
    public static readonly Color SoftAccent = Color.FromArgb(232, 232, 232);
    public static readonly Color Green = Color.FromArgb(35, 35, 35);
    public static readonly Color SoftGreen = Color.FromArgb(235, 235, 235);
    public static readonly Color Amber = Color.FromArgb(75, 75, 75);
    public static readonly Color Purple = Color.FromArgb(100, 100, 100);
    public static readonly Color Red = Color.FromArgb(45, 45, 45);
    public static readonly Color SoftRed = Color.FromArgb(235, 235, 235);

    public static void StyleGrid(DataGridView g)
    {
        g.BackgroundColor = Surface; g.GridColor = Line; g.BorderStyle = BorderStyle.None;
        g.EnableHeadersVisualStyles = false; g.RowHeadersVisible = false;
        var h = g.ColumnHeadersDefaultCellStyle;
        h.BackColor = Header; h.ForeColor = Ink; h.SelectionBackColor = Header; h.SelectionForeColor = Ink;
        var d = g.DefaultCellStyle;
        d.BackColor = Surface; d.ForeColor = Ink; d.SelectionBackColor = SoftAccent; d.SelectionForeColor = Ink;
        var a = g.AlternatingRowsDefaultCellStyle;
        a.BackColor = RowAlt; a.ForeColor = Ink; a.SelectionBackColor = SoftAccent; a.SelectionForeColor = Ink;
        foreach (DataGridViewColumn col in g.Columns)
            if (col is DataGridViewButtonColumn b) { b.FlatStyle = FlatStyle.Flat; b.DefaultCellStyle.BackColor = SoftAccent; b.DefaultCellStyle.ForeColor = Ink; }
    }

    public static void Apply(Control root)
    {
        foreach (Control c in root.Controls)
        {
            switch (c)
            {
                case TextBox t: t.BackColor = Input; t.ForeColor = Ink; t.BorderStyle = BorderStyle.FixedSingle; break;
                case NumericUpDown n: n.BackColor = Input; n.ForeColor = Ink; n.BorderStyle = BorderStyle.FixedSingle; break;
                case ComboBox cb: cb.BackColor = Input; cb.ForeColor = Ink; cb.FlatStyle = FlatStyle.Flat; break;
                case DateTimePicker dp:
                    dp.CalendarMonthBackground = Input; dp.CalendarForeColor = Ink; dp.CalendarTitleBackColor = Accent;
                    dp.CalendarTitleForeColor = Color.Black; dp.CalendarTrailingForeColor = Muted; break;
                case DataGridView g: StyleGrid(g); break;
                case GroupBox gb: gb.ForeColor = Ink; break;
            }
            Apply(c);
        }
    }
}

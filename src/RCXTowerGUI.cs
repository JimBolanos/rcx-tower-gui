// ============================================================================
//  RCX Tower GUI - native edition
//  Control panel for LEGO RCX: wraps rcx_tool.py (python) and nqc.exe
//  Same engine as the original PowerShell tool, Scratch-style skin,
//  compiled as a single self-contained .NET Framework 4.x executable.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("RCX Tower GUI")]
[assembly: AssemblyProduct("RCX Tower GUI")]
[assembly: AssemblyDescription("Control panel for LEGO RCX - NQC compiler + IR Tower")]
[assembly: AssemblyCompany("Jim Bolanos")]
[assembly: AssemblyCopyright("Copyright (c) Jim Bolanos 2026")]
[assembly: AssemblyVersion("3.2.0.0")]
[assembly: AssemblyFileVersion("3.2.0.0")]

// ---------------------------------------------------------------------------
// Palette
// ---------------------------------------------------------------------------
internal static class Theme
{
    public static Color Hex(string s)
    {
        return Color.FromArgb(
            Convert.ToInt32(s.Substring(1, 2), 16),
            Convert.ToInt32(s.Substring(3, 2), 16),
            Convert.ToInt32(s.Substring(5, 2), 16));
    }

    public static readonly Color Bg = Hex("#EDF1F7");
    public static readonly Color Card = Hex("#FFFFFF");
    public static readonly Color CardEdge = Hex("#DCE4EF");
    public static readonly Color Ink = Hex("#3B4160");
    public static readonly Color InkSoft = Hex("#79819B");
    public static readonly Color Purple = Hex("#855CD6");
    public static readonly Color PurpleLt = Hex("#9B78E0");
    public static readonly Color PurpleTxt = Hex("#E4D9FB");
    public static readonly Color Blue = Hex("#4C97FF");
    public static readonly Color Green = Hex("#4CBF56");
    public static readonly Color Orange = Hex("#FFAB19");
    public static readonly Color Red = Hex("#EC5959");
    public static readonly Color Teal = Hex("#0FBD8C");
    public static readonly Color Chip = Hex("#E6ECF6");
    public static readonly Color Field = Hex("#F6F8FC");
    public static readonly Color Console = Hex("#1E1B2E");
    public static readonly Color Disabled = Hex("#E3E8F1");
    public static readonly Color DisText = Hex("#A8B0C2");

    // log colours
    public static readonly Color LogOut = Hex("#90EE90");
    public static readonly Color LogCmd = Hex("#FFDC5E");
    public static readonly Color LogErr = Hex("#FF7B6B");
    public static readonly Color LogInfo = Hex("#7FDBFF");
    public static readonly Color LogDim = Hex("#B9BECF");
    public static readonly Color LogWarn = Hex("#FFAB19");

    public static Font F(float px, FontStyle style)
    {
        return new Font(UiFamily, px, style, GraphicsUnit.Pixel);
    }

    public static Font Mono(float px)
    {
        return new Font(MonoFamily, px, FontStyle.Regular, GraphicsUnit.Pixel);
    }

    private static string _ui, _mono;

    public static string UiFamily
    {
        get
        {
            if (_ui == null) _ui = PickFamily(new[] { "Segoe UI", "Tahoma", "Verdana", "DejaVu Sans", "Arial" });
            return _ui;
        }
    }

    public static string MonoFamily
    {
        get
        {
            if (_mono == null) _mono = PickFamily(new[] { "Consolas", "Lucida Console", "DejaVu Sans Mono", "Courier New" });
            return _mono;
        }
    }

    private static string PickFamily(string[] names)
    {
        foreach (string n in names)
        {
            try
            {
                using (FontFamily ff = new FontFamily(n)) { return n; }
            }
            catch { }
        }
        return FontFamily.GenericSansSerif.Name;
    }

    public static Color Mix(Color c, double factor)
    {
        double r, g, b;
        if (factor >= 0)
        {
            r = c.R + (255 - c.R) * factor;
            g = c.G + (255 - c.G) * factor;
            b = c.B + (255 - c.B) * factor;
        }
        else
        {
            double f = 1 + factor;
            r = c.R * f; g = c.G * f; b = c.B * f;
        }
        return Color.FromArgb(Clamp(r), Clamp(g), Clamp(b));
    }

    private static int Clamp(double v)
    {
        int i = (int)Math.Round(v);
        return i < 0 ? 0 : (i > 255 ? 255 : i);
    }

    public static double Lum(Color c)
    {
        return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
    }

    public static GraphicsPath Rounded(Rectangle r, int radius)
    {
        GraphicsPath p = new GraphicsPath();
        int d = radius * 2;
        if (d <= 0 || d > r.Width || d > r.Height) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

// ---------------------------------------------------------------------------
// Flat rounded button
// ---------------------------------------------------------------------------
internal class FlatButton : Control
{
    private Color _base, _hover, _down, _edge, _fore;
    private bool _isHover, _isDown;

    public int Radius = 10;

    public FlatButton(string text, Color back, Color fore, float fontPx, FontStyle style)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
               | ControlStyles.SupportsTransparentBackColor, true);
        Text = text;
        _base = back;
        _fore = fore;
        Font = Theme.F(fontPx, style);
        Cursor = Cursors.Hand;
        TabStop = false;

        if (Theme.Lum(back) > 0.78)
        {
            _hover = Theme.Mix(back, -0.05);
            _down = Theme.Mix(back, -0.13);
            _edge = Theme.Mix(back, -0.12);
        }
        else
        {
            _hover = Theme.Mix(back, 0.12);
            _down = Theme.Mix(back, -0.12);
            _edge = Theme.Mix(back, -0.10);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color fill, txt, edge;
        if (!Enabled) { fill = Theme.Disabled; txt = Theme.DisText; edge = Theme.Disabled; }
        else if (_isDown) { fill = _down; txt = _fore; edge = _edge; }
        else if (_isHover) { fill = _hover; txt = _fore; edge = _edge; }
        else { fill = _base; txt = _fore; edge = _edge; }

        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = Theme.Rounded(r, Radius))
        using (SolidBrush b = new SolidBrush(fill))
        using (Pen pen = new Pen(edge, 1f))
        {
            g.FillPath(b, p);
            g.DrawPath(pen, p);
        }

        TextRenderer.DrawText(g, Text, Font, new Rectangle(6, 0, Width - 12, Height), txt,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    protected override void OnMouseEnter(EventArgs e) { _isHover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _isHover = false; _isDown = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _isDown = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _isDown = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
}

// ---------------------------------------------------------------------------
// Rounded white card with a title
// ---------------------------------------------------------------------------
internal class Card : Panel
{
    private readonly string _title;
    private readonly Font _titleFont;

    public Card(string title, int x, int y, int w, int h)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        _title = title;
        _titleFont = Theme.F(13f, FontStyle.Bold);
        Location = new Point(x, y);
        Size = new Size(w, h);
        BackColor = Theme.Card;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = Theme.Rounded(r, 14))
        using (SolidBrush b = new SolidBrush(Theme.Card))
        using (Pen pen = new Pen(Theme.CardEdge, 1f))
        {
            g.FillPath(b, p);
            g.DrawPath(pen, p);
        }
        if (!string.IsNullOrEmpty(_title))
        {
            TextRenderer.DrawText(g, _title, _titleFont, new Rectangle(18, 10, Width - 36, 20), Theme.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}

// ---------------------------------------------------------------------------
// Rounded text field
// ---------------------------------------------------------------------------
internal class InputBox : Panel
{
    public readonly TextBox Box = new TextBox();
    private bool _focused;

    public InputBox(int x, int y, int w, int h, string value)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Location = new Point(x, y);
        Size = new Size(w, h);
        BackColor = Theme.Card;

        Box.BorderStyle = BorderStyle.None;
        Box.Font = Theme.F(13f, FontStyle.Regular);
        Box.BackColor = Theme.Field;
        Box.ForeColor = Theme.Ink;
        Box.Text = value ?? "";
        Box.Width = w - 26;
        Box.Location = new Point(13, Math.Max(2, (h - Box.PreferredHeight) / 2 + 1));
        Box.Enter += delegate { _focused = true; Invalidate(); };
        Box.Leave += delegate { _focused = false; Invalidate(); };
        Controls.Add(Box);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Box.Width = Math.Max(20, Width - 26);
        Box.Location = new Point(13, Math.Max(2, (Height - Box.PreferredHeight) / 2 + 1));
    }

    public string Value
    {
        get { return Box.Text; }
        set { Box.Text = value; }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = Theme.Rounded(r, 9))
        using (SolidBrush b = new SolidBrush(Theme.Field))
        using (Pen pen = new Pen(_focused ? Theme.Blue : Theme.CardEdge, _focused ? 2f : 1f))
        {
            g.FillPath(b, p);
            g.DrawPath(pen, p);
        }
    }
}

// ---------------------------------------------------------------------------
// Purple header
// ---------------------------------------------------------------------------
internal class HeaderBar : Panel
{
    private readonly Font _f1 = Theme.F(22f, FontStyle.Bold);
    private readonly Font _f2 = Theme.F(12f, FontStyle.Regular);

    public HeaderBar(int w, int h)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Location = new Point(0, 0);
        Size = new Size(w, h);
        BackColor = Theme.Purple;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Purple);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (GraphicsPath tile = Theme.Rounded(new Rectangle(18, 12, 44, 44), 12))
        using (SolidBrush white = new SolidBrush(Color.White))
            g.FillPath(white, tile);

        using (SolidBrush pb = new SolidBrush(Theme.Purple))
        {
            using (GraphicsPath brick = Theme.Rounded(new Rectangle(25, 32, 30, 14), 3))
                g.FillPath(pb, brick);
            for (int i = 0; i < 3; i++)
                using (GraphicsPath stud = Theme.Rounded(new Rectangle(26 + i * 10, 25, 8, 9), 2))
                    g.FillPath(pb, stud);
        }
        using (SolidBrush ob = new SolidBrush(Theme.Orange))
            g.FillEllipse(ob, 36, 48, 8, 8);

        TextRenderer.DrawText(g, "RCX Tower", _f1, new Rectangle(74, 10, 420, 28), Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, "LEGO RCX control panel  -  NQC compiler + IR Tower", _f2,
            new Rectangle(76, 37, 520, 20), Theme.PurpleTxt,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

// ---------------------------------------------------------------------------
// Status bar with a state dot
// ---------------------------------------------------------------------------
internal class StatusBar : Panel
{
    public Color Dot = Theme.Green;
    private readonly Label _label = new Label();

    public StatusBar(int x, int y, int w, int h)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Location = new Point(x, y);
        Size = new Size(w, h);
        BackColor = Theme.Bg;

        _label.Text = "Ready.";
        _label.Font = Theme.F(12f, FontStyle.Regular);
        _label.ForeColor = Theme.InkSoft;
        _label.BackColor = Color.Transparent;
        _label.Location = new Point(37, 7);
        _label.Size = new Size(w - 60, 18);
        Controls.Add(_label);
    }

    public void Set(string text, Color dot)
    {
        _label.Text = text;
        Dot = dot;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _label.Size = new Size(Math.Max(20, Width - 60), 18);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (SolidBrush b = new SolidBrush(Dot))
            e.Graphics.FillEllipse(b, 18, 10, 11, 11);
    }
}

// ---------------------------------------------------------------------------
// Main window
// ---------------------------------------------------------------------------
internal class MainForm : Form
{
    // ---- state -------------------------------------------------------
    private string _baseDir;
    private string _pythonExe;
    private string _nqcExe;

    private Process _proc;
    private bool _runFinished = true;
    private DateTime _runStart;
    private int _timeoutSeconds = 8;
    private readonly System.Windows.Forms.Timer _watchdog = new System.Windows.Forms.Timer();

    // ---- controls ----------------------------------------------------
    private InputBox _txtProject, _txtNqc, _txtFirmware, _txtDownload;
    private FlatButton _btnProjectBrowse, _btnTower, _btnPing, _btnInfo, _btnClearLog, _btnCancel;
    private FlatButton _btnNqcBrowse, _btnNqcCompile, _btnFirmwareBrowse, _btnFirmware;
    private FlatButton _btnDownloadBrowse, _btnDownload, _btnAbout;
    private RichTextBox _log;
    private StatusBar _status;
    private HeaderBar _header;
    private ConsolePanel _logWrap;
    private Card _cardProject, _cardActions, _cardNqc, _cardFirmware, _cardDownload, _cardLog;

    // smallest usable client area
    private const int MinClientW = 560;
    private const int MinClientH = 700;

    public MainForm()
    {
        ResolvePaths();
        BuildUi();
        HookEvents();

        Log("Panel ready. Project folder: " + _baseDir, Theme.LogInfo);
        Log("Python: " + _pythonExe + "   |   NQC: " + _nqcExe, Theme.LogInfo);
        Log("Reminder: Tower must be connected and (for firmware/download) the RCX must be "
          + "powered on and pointing at the tower.", Theme.LogInfo);

        Shown += delegate
        {
            // run once the layout has settled, so nothing steals the focus ring back
            try
            {
                BeginInvoke(new Action(delegate
                {
                    try { _txtProject.Box.Select(0, 0); ActiveControl = null; }
                    catch { }
                }));
            }
            catch { }
        };

        _watchdog.Interval = 500;
        _watchdog.Tick += Watchdog_Tick;
        _watchdog.Start();
    }

    // ==================================================================
    // Paths
    // ==================================================================
    private void ResolvePaths()
    {
        try
        {
            string exe = Assembly.GetExecutingAssembly().Location;
            _baseDir = string.IsNullOrEmpty(exe)
                ? Directory.GetCurrentDirectory()
                : Path.GetDirectoryName(exe);
        }
        catch { _baseDir = Directory.GetCurrentDirectory(); }

        if (string.IsNullOrEmpty(_baseDir)) _baseDir = Directory.GetCurrentDirectory();

        _pythonExe = FindTool(new[] { "python.exe", Path.Combine("python", "python.exe"), "python3.exe" }, "python");
        _nqcExe = FindTool(new[] { "nqc.exe", Path.Combine("nqc", "nqc.exe"), Path.Combine("bin", "nqc.exe") }, "nqc");
    }

    private string FindTool(string[] candidates, string fallback)
    {
        foreach (string c in candidates)
        {
            try
            {
                string p = Path.Combine(_baseDir, c);
                if (File.Exists(p)) return p;
            }
            catch { }
        }
        return fallback;
    }

    // ==================================================================
    // UI
    // ==================================================================
    private void BuildUi()
    {
        Text = "RCX Tower - Control Panel";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        ClientSize = StartingSize();
        BackColor = Theme.Bg;
        Font = Theme.F(13f, FontStyle.Regular);
        try
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("rcx.ico"))
                if (s != null) Icon = new Icon(s);
        }
        catch { }

        _header = new HeaderBar(800, 68);
        Controls.Add(_header);

        _btnAbout = MakeButton(_header, "About", Theme.PurpleLt, Color.White, 692, 18, 90, 32, 16, 13f);

        // ---- project folder
        _cardProject = AddCard("Project folder (rcx_tool.py)", 16, 80, 768, 92);
        _txtProject = AddInput(_cardProject, 18, 38, 610, 38, _baseDir);
        _btnProjectBrowse = MakeButton(_cardProject, "Browse", Theme.Chip, Theme.Ink, 640, 38, 110, 38, 10, 13f);

        // ---- quick actions
        _cardActions = AddCard("Quick actions", 16, 182, 768, 138);
        _btnTower = MakeButton(_cardActions, "Tower", Theme.Blue, Color.White, 18, 38, 174, 40, 10, 14f);
        _btnPing = MakeButton(_cardActions, "Ping", Theme.Teal, Color.White, 204, 38, 174, 40, 10, 14f);
        _btnInfo = MakeButton(_cardActions, "Info", Theme.Purple, Color.White, 390, 38, 174, 40, 10, 14f);
        _btnClearLog = MakeButton(_cardActions, "Clear log", Theme.Chip, Theme.Ink, 576, 38, 174, 40, 10, 14f);
        _btnCancel = MakeButton(_cardActions, "Force release / Cancel", Theme.Red, Color.White, 18, 88, 732, 36, 10, 13f);
        _btnCancel.Enabled = false;

        // ---- nqc
        _cardNqc = AddCard("NQC compiler  (.nqc  ->  .rcx)", 16, 330, 768, 92);
        _txtNqc = AddInput(_cardNqc, 18, 38, 462, 38, "");
        _btnNqcBrowse = MakeButton(_cardNqc, "...", Theme.Chip, Theme.Ink, 490, 38, 50, 38, 10, 15f);
        _btnNqcCompile = MakeButton(_cardNqc, "Compile & Download", Theme.Green, Color.White, 552, 38, 198, 38, 10, 13f);

        // ---- firmware
        _cardFirmware = AddCard("Upload firmware (.lgo)", 16, 432, 768, 92);
        _txtFirmware = AddInput(_cardFirmware, 18, 38, 462, 38, Path.Combine(_baseDir, "firm0332.lgo"));
        _btnFirmwareBrowse = MakeButton(_cardFirmware, "...", Theme.Chip, Theme.Ink, 490, 38, 50, 38, 10, 15f);
        _btnFirmware = MakeButton(_cardFirmware, "Send firmware", Theme.Orange, Color.White, 552, 38, 198, 38, 10, 13f);

        // ---- download
        _cardDownload = AddCard("Download program (.rcx / .hex)", 16, 534, 768, 92);
        _txtDownload = AddInput(_cardDownload, 18, 38, 462, 38, "");
        _btnDownloadBrowse = MakeButton(_cardDownload, "...", Theme.Chip, Theme.Ink, 490, 38, 50, 38, 10, 15f);
        _btnDownload = MakeButton(_cardDownload, "Download", Theme.Blue, Color.White, 552, 38, 198, 38, 10, 13f);

        // ---- console
        _cardLog = AddCard("Console", 16, 636, 768, 206);
        _logWrap = new ConsolePanel(18, 38, 732, 154);
        _cardLog.Controls.Add(_logWrap);

        _log = new RichTextBox();
        _log.ScrollBars = RichTextBoxScrollBars.Vertical;
        _log.ReadOnly = true;
        _log.BorderStyle = BorderStyle.None;
        _log.Font = Theme.Mono(13f);
        _log.Location = new Point(12, 12);
        _log.Size = new Size(708, 130);
        _log.BackColor = Theme.Console;
        _log.ForeColor = Theme.LogOut;
        _log.TabStop = false;
        _logWrap.Controls.Add(_log);

        // ---- status
        _status = new StatusBar(0, 850, 800, 30);
        Controls.Add(_status);
    }

    private Card AddCard(string title, int x, int y, int w, int h)
    {
        Card c = new Card(title, x, y, w, h);
        Controls.Add(c);
        return c;
    }

    private InputBox AddInput(Control parent, int x, int y, int w, int h, string value)
    {
        InputBox i = new InputBox(x, y, w, h, value);
        parent.Controls.Add(i);
        return i;
    }

    private FlatButton MakeButton(Control parent, string text, Color back, Color fore,
                                  int x, int y, int w, int h, int radius, float fontPx)
    {
        FlatButton b = new FlatButton(text, back, fore, fontPx, FontStyle.Bold);
        b.Location = new Point(x, y);
        b.Size = new Size(w, h);
        b.Radius = radius;
        b.BackColor = parent.BackColor;
        parent.Controls.Add(b);
        return b;
    }

    // ==================================================================
    // Responsive layout
    // ==================================================================

    // Default size on a first run, never bigger than the screen.
    private Size StartingSize()
    {
        Size saved = LoadSavedSize();
        if (saved != Size.Empty) return saved;

        int w = 800, h = 880;
        try
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            w = Math.Min(w, wa.Width - 40);
            h = Math.Min(h, wa.Height - 40);
        }
        catch { }
        return new Size(Math.Max(MinClientW, w), Math.Max(MinClientH, h));
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // MinimumSize is the whole window, so add the border + title bar
        int chromeW = Size.Width - ClientSize.Width;
        int chromeH = Size.Height - ClientSize.Height;
        MinimumSize = new Size(MinClientW + chromeW, MinClientH + chromeH);
        Relayout();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_status != null) Relayout();
    }

    // Everything is positioned from the current client size. Below 800px tall
    // the rows switch to a tighter set of metrics so the window can get small
    // without the console disappearing.
    private void Relayout()
    {
        int W = ClientSize.Width;
        int H = ClientSize.Height;
        if (W < 100 || H < 100) return;

        bool dense = H < 800;

        const int pad = 16;
        const int inner = 18;
        int cardW = Math.Max(320, W - pad * 2);
        int contentW = cardW - inner * 2;

        int headerH = 68;
        int statusH = 30;
        int gap = dense ? 8 : 10;
        int contentY = dense ? 30 : 38;     // first line inside a card
        int inputH = dense ? 34 : 38;
        int botPad = dense ? 12 : 16;
        int rowH = contentY + inputH + botPad;

        int actBtnH = dense ? 36 : 40;
        int cancelH = dense ? 32 : 36;
        int actH = contentY + actBtnH + gap + cancelH + (botPad - 2);

        int actionW = dense ? 170 : 198;    // "Compile & Download" etc.
        int dotsW = dense ? 46 : 50;

        SuspendLayout();

        _header.SetBounds(0, 0, W, headerH);
        _btnAbout.SetBounds(W - pad - 90, (headerH - 32) / 2, 90, 32);

        int y = headerH + 12;

        // ---- project folder
        _cardProject.SetBounds(pad, y, cardW, rowH);
        int browseW = 110;
        _txtProject.SetBounds(inner, contentY, Math.Max(120, contentW - browseW - 12), inputH);
        _btnProjectBrowse.SetBounds(inner + contentW - browseW, contentY, browseW, inputH);
        y += rowH + gap;

        // ---- quick actions
        _cardActions.SetBounds(pad, y, cardW, actH);
        int bw = (contentW - 3 * 12) / 4;
        _btnTower.SetBounds(inner, contentY, bw, actBtnH);
        _btnPing.SetBounds(inner + (bw + 12), contentY, bw, actBtnH);
        _btnInfo.SetBounds(inner + 2 * (bw + 12), contentY, bw, actBtnH);
        _btnClearLog.SetBounds(inner + 3 * (bw + 12), contentY, contentW - 3 * (bw + 12), actBtnH);
        _btnCancel.SetBounds(inner, contentY + actBtnH + gap, contentW, cancelH);
        y += actH + gap;

        // ---- the three file rows
        y = FileRow(_cardNqc, _txtNqc, _btnNqcBrowse, _btnNqcCompile,
                    pad, y, cardW, rowH, contentY, inputH, contentW, inner, actionW, dotsW) + gap;
        y = FileRow(_cardFirmware, _txtFirmware, _btnFirmwareBrowse, _btnFirmware,
                    pad, y, cardW, rowH, contentY, inputH, contentW, inner, actionW, dotsW) + gap;
        y = FileRow(_cardDownload, _txtDownload, _btnDownloadBrowse, _btnDownload,
                    pad, y, cardW, rowH, contentY, inputH, contentW, inner, actionW, dotsW) + gap;

        // ---- console takes whatever height is left
        int logH = Math.Max(80, H - statusH - 8 - y);
        _cardLog.SetBounds(pad, y, cardW, logH);
        int wrapH = Math.Max(36, logH - contentY - botPad);
        _logWrap.SetBounds(inner, contentY, contentW, wrapH);
        int ip = dense ? 10 : 12;
        _log.SetBounds(ip, ip, Math.Max(40, contentW - ip * 2), Math.Max(20, wrapH - ip * 2));

        _status.SetBounds(0, H - statusH, W, statusH);

        ResumeLayout(true);
        Invalidate(true);
    }

    private int FileRow(Card card, InputBox input, FlatButton dots, FlatButton action,
                        int pad, int y, int cardW, int rowH, int contentY, int inputH,
                        int contentW, int inner, int actionW, int dotsW)
    {
        card.SetBounds(pad, y, cardW, rowH);
        int inputW = Math.Max(100, contentW - actionW - dotsW - 22);
        input.SetBounds(inner, contentY, inputW, inputH);
        dots.SetBounds(inner + inputW + 10, contentY, dotsW, inputH);
        action.SetBounds(inner + contentW - actionW, contentY, actionW, inputH);
        return y + rowH;
    }

    // ==================================================================
    // Remember the window size between runs
    // ==================================================================
    private static string SettingsFile()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RCXTowerGUI");
        return Path.Combine(dir, "window.txt");
    }

    private Size LoadSavedSize()
    {
        try
        {
            string f = SettingsFile();
            if (!File.Exists(f)) return Size.Empty;
            // NOTE: use the char[] overload. Split(char) only exists on newer
            // runtimes and throws MissingMethodException on .NET Framework.
            string[] parts = File.ReadAllText(f).Trim().Split(new char[] { ',' });
            if (parts.Length < 2) return Size.Empty;
            int w = int.Parse(parts[0]);
            int h = int.Parse(parts[1]);
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            if (w < MinClientW || h < MinClientH) return Size.Empty;
            if (w > wa.Width || h > wa.Height) return Size.Empty;
            return new Size(w, h);
        }
        catch { return Size.Empty; }
    }

    private void SaveSize()
    {
        try
        {
            if (WindowState != FormWindowState.Normal) return;
            string f = SettingsFile();
            Directory.CreateDirectory(Path.GetDirectoryName(f));
            File.WriteAllText(f, ClientSize.Width + "," + ClientSize.Height);
        }
        catch { }
    }

    // ==================================================================
    // Events
    // ==================================================================
    private void HookEvents()
    {
        _btnProjectBrowse.Click += delegate
        {
            using (FolderBrowserDialog f = new FolderBrowserDialog())
            {
                f.Description = "Select the folder containing rcx_tool.py";
                try { f.SelectedPath = _txtProject.Value; }
                catch { }
                if (f.ShowDialog(this) == DialogResult.OK) _txtProject.Value = f.SelectedPath;
            }
        };

        _btnTower.Click += delegate { RunTool(new[] { "tower" }, 8); };
        _btnPing.Click += delegate { RunTool(new[] { "ping" }, 8); };
        _btnInfo.Click += delegate { RunTool(new[] { "info" }, 8); };

        _btnNqcBrowse.Click += delegate
        {
            string f = PickFile("Select NQC source file", "NQC source (*.nqc)|*.nqc|All files (*.*)|*.*");
            if (f != null) _txtNqc.Value = f;
        };

        _btnFirmwareBrowse.Click += delegate
        {
            string f = PickFile("Select firmware file (.lgo)", "LEGO firmware (*.lgo)|*.lgo|All files (*.*)|*.*");
            if (f != null) _txtFirmware.Value = f;
        };

        _btnDownloadBrowse.Click += delegate
        {
            string f = PickFile("Select program to download (.rcx or .hex)",
                                "RCX programs (*.rcx;*.hex)|*.rcx;*.hex|All files (*.*)|*.*");
            if (f != null) _txtDownload.Value = f;
        };

        _btnNqcCompile.Click += delegate { CompileAndDownload(); };

        _btnFirmware.Click += delegate
        {
            string f = _txtFirmware.Value.Trim();
            if (f.Length == 0 || !File.Exists(f))
            {
                MessageBox.Show(this, "Please select a valid firmware file (.lgo).", "Missing file",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // firmware transfer is slow: long leash before the watchdog releases
            RunTool(new[] { "firmware", f }, 180);
        };

        _btnDownload.Click += delegate
        {
            string f = _txtDownload.Value.Trim();
            if (f.Length == 0 || !File.Exists(f))
            {
                MessageBox.Show(this, "Please select a valid file (.rcx or .hex) to download.", "Missing file",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            RunTool(new[] { "download", f }, 8);
        };

        _btnCancel.Click += delegate
        {
            if (_proc != null && !_runFinished)
            {
                try
                {
                    if (!_proc.HasExited)
                    {
                        KillTree(_proc);
                        Log("Kill signal sent to Python process.", Theme.LogWarn);
                    }
                }
                catch (Exception ex) { Log("Could not close process: " + ex.Message, Theme.LogWarn); }
            }
            CompleteRun("Manually released");
            // last-resort unstick: whatever happened, the panel stays usable
            SetButtonsEnabled(true);
            SetStatus("Ready.", Theme.Green);
        };

        _btnClearLog.Click += delegate { _log.Clear(); };

        _btnAbout.Click += delegate
        {
            string t = "RCX Tower GUI  v3.2" + Environment.NewLine + Environment.NewLine
                     + "Created by Jim Bolanos, 2026" + Environment.NewLine + Environment.NewLine
                     + "Folder : " + _baseDir + Environment.NewLine
                     + "Python : " + _pythonExe + Environment.NewLine
                     + "NQC    : " + _nqcExe;
            MessageBox.Show(this, t, "About RCX Tower GUI", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        FormClosing += delegate
        {
            SaveSize();
            _watchdog.Stop();
            try { if (_proc != null && !_proc.HasExited) KillTree(_proc); }
            catch { }
        };
    }

    private string PickFile(string title, string filter)
    {
        using (OpenFileDialog d = new OpenFileDialog())
        {
            d.Title = title;
            d.Filter = filter;
            try
            {
                string dir = _txtProject.Value.Trim();
                if (Directory.Exists(dir)) d.InitialDirectory = dir;
            }
            catch { }
            return d.ShowDialog(this) == DialogResult.OK ? d.FileName : null;
        }
    }

    // ==================================================================
    // Logging / status
    // ==================================================================
    private void Log(string text, Color color)
    {
        if (_log.InvokeRequired)
        {
            try { _log.BeginInvoke(new Action<string, Color>(Log), text, color); }
            catch { }
            return;
        }
        int start = _log.TextLength;
        _log.AppendText(text + Environment.NewLine);
        _log.Select(start, _log.TextLength - start);
        _log.SelectionColor = color;
        _log.Select(_log.TextLength, 0);
        _log.ScrollToCaret();
    }

    private void SetStatus(string text, Color dot)
    {
        if (_status.InvokeRequired)
        {
            try { _status.BeginInvoke(new Action<string, Color>(SetStatus), text, dot); }
            catch { }
            return;
        }
        _status.Set(text, dot);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action<bool>(SetButtonsEnabled), enabled); }
            catch { }
            return;
        }
        FlatButton[] all = new[]
        {
            _btnTower, _btnPing, _btnInfo, _btnFirmware, _btnDownload,
            _btnFirmwareBrowse, _btnDownloadBrowse, _btnProjectBrowse,
            _btnNqcCompile, _btnNqcBrowse
        };
        foreach (FlatButton b in all) b.Enabled = enabled;
        _btnCancel.Enabled = !enabled;
    }

    // ==================================================================
    // rcx_tool.py runner
    // ==================================================================
    private static string Quote(string s)
    {
        return "\"" + s + "\"";
    }

    private void RunTool(string[] toolArgs, int timeoutSeconds)
    {
        if (!_runFinished)
        {
            MessageBox.Show(this, "A command is already running.", "Wait",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string projectPath = _txtProject.Value.Trim();
        string rcxScript = Path.Combine(projectPath, "rcx_tool.py");
        if (!File.Exists(rcxScript))
        {
            MessageBox.Show(this, "rcx_tool.py not found at:" + Environment.NewLine + projectPath,
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            // we may have been called from the compile chain, which already
            // disabled the buttons - never leave the window stuck
            SetButtonsEnabled(true);
            SetStatus("Ready.", Theme.Green);
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("-u ").Append(Quote(rcxScript));
        StringBuilder shown = new StringBuilder();
        foreach (string a in toolArgs)
        {
            string q = a.IndexOf(' ') >= 0 ? Quote(a) : a;
            sb.Append(' ').Append(q);
            shown.Append(' ').Append(q);
        }

        _timeoutSeconds = timeoutSeconds;
        _runFinished = false;
        _runStart = DateTime.Now;
        SetButtonsEnabled(false);
        SetStatus("Executing: python rcx_tool.py" + shown + " ...", Theme.Orange);
        Log(">> python rcx_tool.py" + shown, Theme.LogCmd);

        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = _pythonExe;
        psi.Arguments = sb.ToString();
        psi.WorkingDirectory = projectPath;
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.StandardOutputEncoding = Encoding.UTF8;
        psi.StandardErrorEncoding = Encoding.UTF8;
        psi.CreateNoWindow = true;

        Process p = new Process();
        p.StartInfo = psi;
        p.EnableRaisingEvents = true;
        p.OutputDataReceived += delegate (object s, DataReceivedEventArgs e)
        {
            if (e.Data != null) Log(e.Data, Theme.LogOut);
        };
        p.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e)
        {
            if (e.Data != null) Log(e.Data, Theme.LogErr);
        };
        p.Exited += delegate
        {
            int code = 0;
            try { code = p.ExitCode; }
            catch { }
            CompleteRunAsync("Process finished (code " + code + ")");
        };

        _proc = p;
        try
        {
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            Log("Error starting Python: " + ex.Message, Theme.LogErr);
            Log("Tried to run: " + _pythonExe, Theme.LogErr);
            CompleteRun("Error during startup");
        }
    }

    private void CompleteRunAsync(string reason)
    {
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action<string>(CompleteRun), reason); }
            catch { }
        }
        else CompleteRun(reason);
    }

    private void CompleteRun(string reason)
    {
        if (_runFinished) return;
        _runFinished = true;
        Log("-- " + reason + " --", Theme.LogDim);
        SetStatus("Ready.", Theme.Green);
        SetButtonsEnabled(true);
        if (_proc != null)
        {
            try { _proc.Dispose(); }
            catch { }
            _proc = null;
        }
    }

    private static void KillTree(Process p)
    {
        // .NET Framework has no Kill(true): use taskkill to take the children too
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("taskkill", "/PID " + p.Id + " /T /F");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            using (Process k = Process.Start(psi)) { k.WaitForExit(4000); }
        }
        catch { }
        try { if (!p.HasExited) p.Kill(); }
        catch { }
    }

    private void Watchdog_Tick(object sender, EventArgs e)
    {
        if (_runFinished || _proc == null) return;
        try
        {
            if (_proc.HasExited)
            {
                CompleteRun("Process finished (detected by watchdog)");
                return;
            }
        }
        catch
        {
            CompleteRun("Process finished");
            return;
        }
        if ((DateTime.Now - _runStart).TotalSeconds >= _timeoutSeconds)
        {
            Log("Command timed out. Closing automatically...", Theme.LogWarn);
            try { if (!_proc.HasExited) KillTree(_proc); }
            catch { }
            CompleteRun("Released due to timeout");
        }
    }

    // ==================================================================
    // NQC compiler
    // ==================================================================
    private void CompileAndDownload()
    {
        string nqcFile = _txtNqc.Value.Trim();
        if (nqcFile.Length == 0)
        {
            MessageBox.Show(this, "Please select a .nqc file first.", "No file selected",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!File.Exists(nqcFile))
        {
            MessageBox.Show(this, "The .nqc file was not found at:" + Environment.NewLine + nqcFile,
                "File not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (!_runFinished)
        {
            MessageBox.Show(this, "A command is already running.", "Wait",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetButtonsEnabled(false);
        string full = Path.GetFullPath(nqcFile);
        Log("Compiling NQC: " + Path.GetFileName(full) + " ...", Theme.LogCmd);
        SetStatus("Compiling NQC...", Theme.Orange);

        ThreadPool.QueueUserWorkItem(delegate
        {
            string rcxPath = RunNqc(full);
            try
            {
                BeginInvoke(new Action(delegate
                {
                    if (rcxPath != null)
                    {
                        Log("Starting automatic download of compiled binary...", Theme.LogInfo);
                        RunTool(new[] { "download", rcxPath }, 8);
                    }
                    else
                    {
                        SetButtonsEnabled(true);
                        SetStatus("Compilation failed.", Theme.Red);
                    }
                }));
            }
            catch { }
        });
    }

    // returns the generated .rcx path, or null on failure
    private string RunNqc(string nqcPath)
    {
        string dir = Path.GetDirectoryName(nqcPath);
        string name = Path.GetFileNameWithoutExtension(nqcPath);
        string rcxPath = Path.Combine(dir, name + ".rcx");

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = _nqcExe;
            psi.Arguments = "-Trcx2 " + Quote(nqcPath);   // nqc -Trcx2 "filename"
            psi.WorkingDirectory = dir;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            string stdout, stderr;
            int code;
            using (Process p = Process.Start(psi))
            {
                stdout = p.StandardOutput.ReadToEnd();
                stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                code = p.ExitCode;
            }

            foreach (string line in SplitLines(stdout)) Log(line, Theme.LogOut);
            foreach (string line in SplitLines(stderr)) Log(line, Theme.LogErr);

            if (code == 0 && File.Exists(rcxPath))
            {
                Log("Compilation successful: " + rcxPath, Theme.LogOut);
                return rcxPath;
            }

            Log("Compilation failed or .rcx not generated. Exit code: " + code + ".", Theme.LogErr);
            return null;
        }
        catch (Exception ex)
        {
            Log("Critical error running nqc: " + ex.Message, Theme.LogErr);
            Log("Ensure nqc.exe is in the application folder or in the system PATH.", Theme.LogErr);
            return null;
        }
    }

    private static IEnumerable<string> SplitLines(string s)
    {
        if (string.IsNullOrEmpty(s)) yield break;
        foreach (string line in s.Replace("\r\n", "\n").Split(new char[] { '\n' }))
            if (line.Trim().Length > 0) yield return line;
    }
}

// ---------------------------------------------------------------------------
// Dark rounded console holder
// ---------------------------------------------------------------------------
internal class ConsolePanel : Panel
{
    public ConsolePanel(int x, int y, int w, int h)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Location = new Point(x, y);
        Size = new Size(w, h);
        BackColor = Theme.Card;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Card);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath p = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 12))
        using (SolidBrush b = new SolidBrush(Theme.Console))
            g.FillPath(b, p);
    }
}

// ---------------------------------------------------------------------------
// Entry point
// ---------------------------------------------------------------------------
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
        {
            ShowFatal(e.ExceptionObject as Exception);
        };
        Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e)
        {
            ShowFatal(e.Exception);
        };

        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            ShowFatal(ex);
        }
    }

    private static void ShowFatal(Exception ex)
    {
        string msg = ex == null ? "Unknown error." : ex.ToString();
        try
        {
            string logPath = Path.Combine(Path.GetTempPath(), "RCXTowerGUI-error.txt");
            File.WriteAllText(logPath, DateTime.Now + Environment.NewLine + msg);
            msg += Environment.NewLine + Environment.NewLine + "Saved to: " + logPath;
        }
        catch { }
        try
        {
            MessageBox.Show(msg, "RCX Tower GUI - error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch { }
    }
}

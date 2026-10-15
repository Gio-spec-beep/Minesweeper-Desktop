using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace MinesDesktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new GameForm());
    }
}

internal sealed class GameForm : Form
{
    private enum CellState { Hidden, Revealed, Flagged }
    private sealed class Cell
    {
        public bool Mine;
        public int Adjacent;
        public CellState State;
        public float Hover;
    }

    private readonly Color bg = Color.FromArgb(12, 17, 31);
    private readonly Color panel = Color.FromArgb(20, 28, 47);
    private readonly Color tile = Color.FromArgb(37, 49, 73);
    private readonly Color tileLight = Color.FromArgb(48, 63, 91);
    private readonly Color accent = Color.FromArgb(88, 166, 255);
    private readonly System.Windows.Forms.Timer clock = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer anim = new() { Interval = 16 };
    private readonly Dictionary<string, int> bestTimes = new();
    private Cell[,] board = new Cell[9, 9];
    private int rows = 9, cols = 9, mineCount = 10, flags, seconds;
    private bool started, ended, won;
    private Point? hoverCell;
    private Rectangle boardRect;
    private Rectangle restartRect, beginnerRect, intermediateRect, expertRect;
    private string face = "☺";

    public GameForm()
    {
        Text = "Mines";
        ClientSize = new Size(820, 760);
        MinimumSize = new Size(620, 650);
        BackColor = bg;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        clock.Tick += (_, _) => { if (started && !ended) { seconds++; Invalidate(); } };
        anim.Tick += (_, _) => Animate();
        anim.Start();
        NewGame(0);
    }

    private string DifficultyKey() => $"{cols}x{rows}-{mineCount}";

    private void NewGame(int level)
    {
        if (level == 0) { rows = 9; cols = 9; mineCount = 10; }
        else if (level == 1) { rows = 16; cols = 16; mineCount = 40; }
        else { rows = 16; cols = 30; mineCount = 99; }
        board = new Cell[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++) board[r, c] = new Cell();
        flags = seconds = 0;
        started = ended = won = false;
        face = "☺";
        hoverCell = null;
        clock.Stop();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(bg);

        using var titleFont = new Font("Segoe UI Semibold", 25, FontStyle.Bold);
        using var subFont = new Font("Segoe UI", 10);
        using var smallBold = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
        DrawText(g, "MINES", titleFont, Color.White, new Rectangle(24, 18, 330, 42), ContentAlignment.MiddleLeft);
        DrawText(g, "A little brain workout.", subFont, Color.FromArgb(145, 160, 185), new Rectangle(26, 59, 320, 22), ContentAlignment.MiddleLeft);

        int top = 100;
        int gap = 8;
        int availableW = ClientSize.Width - 48;
        int buttonW = Math.Min(150, (availableW - gap * 2) / 3);
        beginnerRect = new Rectangle(24, top, buttonW, 39);
        intermediateRect = new Rectangle(24 + buttonW + gap, top, buttonW, 39);
        expertRect = new Rectangle(24 + (buttonW + gap) * 2, top, buttonW, 39);
        DrawPill(g, beginnerRect, "BEGINNER", rows == 9, smallBold);
        DrawPill(g, intermediateRect, "INTERMEDIATE", rows == 16 && cols == 16, smallBold);
        DrawPill(g, expertRect, "EXPERT", cols == 30, smallBold);

        int panelTop = 154;
        var statsRect = new Rectangle(24, panelTop, ClientSize.Width - 48, 78);
        FillRound(g, statsRect, panel, 18);
        DrawText(g, "MINES", smallBold, Color.FromArgb(145, 160, 185), new Rectangle(43, panelTop + 10, 80, 18), ContentAlignment.MiddleLeft);
        DrawText(g, Math.Max(0, mineCount - flags).ToString("000"), new Font("Consolas", 25, FontStyle.Bold), Color.FromArgb(255, 115, 126), new Rectangle(42, panelTop + 28, 100, 35), ContentAlignment.MiddleLeft);
        DrawText(g, "TIME", smallBold, Color.FromArgb(145, 160, 185), new Rectangle(statsRect.Right - 127, panelTop + 10, 80, 18), ContentAlignment.MiddleLeft);
        DrawText(g, Math.Min(seconds, 999).ToString("000"), new Font("Consolas", 25, FontStyle.Bold), Color.FromArgb(119, 224, 184), new Rectangle(statsRect.Right - 128, panelTop + 28, 92, 35), ContentAlignment.MiddleLeft);

        restartRect = new Rectangle((ClientSize.Width - 48) / 2, panelTop + 17, 48, 44);
        FillRound(g, restartRect, Color.FromArgb(34, 46, 69), 14);
        DrawText(g, face, new Font("Segoe UI Emoji", 22), Color.FromArgb(255, 215, 110), restartRect, ContentAlignment.MiddleCenter);

        int boardTop = 249;
        int boardBottomPadding = 22;
        int boardW = ClientSize.Width - 48;
        int boardH = ClientSize.Height - boardTop - boardBottomPadding;
        int cellSize = Math.Min(boardW / cols, boardH / rows);
        cellSize = Math.Max(12, cellSize);
        int actualW = cellSize * cols;
        int actualH = cellSize * rows;
        boardRect = new Rectangle((ClientSize.Width - actualW) / 2, boardTop + Math.Max(0, (boardH - actualH) / 2), actualW, actualH);
        FillRound(g, new Rectangle(boardRect.X - 8, boardRect.Y - 8, boardRect.Width + 16, boardRect.Height + 16), panel, 16);

        using var numberFont = new Font("Segoe UI", Math.Max(8, cellSize * .47f), FontStyle.Bold);
        using var mineFont = new Font("Segoe UI Emoji", Math.Max(8, cellSize * .46f));
        using var flagFont = new Font("Segoe UI Emoji", Math.Max(8, cellSize * .46f));
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Cell cell = board[r, c];
                Rectangle rect = new(boardRect.X + c * cellSize + 1, boardRect.Y + r * cellSize + 1, cellSize - 2, cellSize - 2);
                if (cell.State == CellState.Revealed)
                {
                    FillRound(g, rect, cell.Mine ? Color.FromArgb(111, 39, 57) : Color.FromArgb(18, 25, 42), Math.Max(3, cellSize / 5));
                    if (cell.Mine) DrawText(g, "✹", mineFont, Color.FromArgb(255, 111, 126), rect, ContentAlignment.MiddleCenter);
                    else if (cell.Adjacent > 0)
                        DrawText(g, cell.Adjacent.ToString(), numberFont, NumberColor(cell.Adjacent), rect, ContentAlignment.MiddleCenter);
                }
                else
                {
                    float lift = cell.Hover;
                    Color baseColor = Lerp(tile, tileLight, lift);
                    if (ended && cell.Mine) baseColor = Color.FromArgb(70, 40, 54);
                    FillRound(g, rect, baseColor, Math.Max(3, cellSize / 5));
                    if (cell.State == CellState.Flagged)
                        DrawText(g, "⚑", flagFont, Color.FromArgb(255, 117, 128), rect, ContentAlignment.MiddleCenter);
                }
            }
        }

        if (ended)
        {
            var overlay = new Rectangle(boardRect.X, boardRect.Y + boardRect.Height / 2 - 37, boardRect.Width, 74);
            using var shade = new SolidBrush(Color.FromArgb(215, 12, 17, 31));
            g.FillRectangle(shade, overlay);
            using var resultFont = new Font("Segoe UI Semibold", 20, FontStyle.Bold);
            DrawText(g, won ? "BOARD CLEARED!" : "BOOM! TRY AGAIN", resultFont, won ? Color.FromArgb(119, 224, 184) : Color.FromArgb(255, 115, 126), overlay, ContentAlignment.MiddleCenter);
        }
        DrawText(g, "LEFT CLICK  REVEAL     •     RIGHT CLICK  FLAG", new Font("Segoe UI", 8, FontStyle.Bold), Color.FromArgb(102, 119, 147), new Rectangle(20, ClientSize.Height - 18, ClientSize.Width - 40, 16), ContentAlignment.MiddleCenter);
    }

    private static Color NumberColor(int n) => n switch
    {
        1 => Color.FromArgb(104, 177, 255),
        2 => Color.FromArgb(119, 224, 184),
        3 => Color.FromArgb(255, 115, 126),
        4 => Color.FromArgb(174, 145, 255),
        5 => Color.FromArgb(255, 174, 105),
        6 => Color.FromArgb(92, 218, 222),
        7 => Color.FromArgb(245, 245, 255),
        _ => Color.FromArgb(175, 188, 208)
    };

    private static Color Lerp(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    private static void FillRound(Graphics g, Rectangle rect, Color color, int radius)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        using var path = new GraphicsPath();
        int d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    private static void DrawText(Graphics g, string text, Font font, Color color, Rectangle rect, ContentAlignment align)
    {
        using var brush = new SolidBrush(color);
        using var sf = new StringFormat();
        sf.Alignment = align is ContentAlignment.MiddleCenter or ContentAlignment.TopCenter or ContentAlignment.BottomCenter ? StringAlignment.Center :
            align is ContentAlignment.MiddleRight or ContentAlignment.TopRight or ContentAlignment.BottomRight ? StringAlignment.Far : StringAlignment.Near;
        sf.LineAlignment = align is ContentAlignment.MiddleCenter or ContentAlignment.MiddleLeft or ContentAlignment.MiddleRight ? StringAlignment.Center :
            align is ContentAlignment.BottomCenter or ContentAlignment.BottomLeft or ContentAlignment.BottomRight ? StringAlignment.Far : StringAlignment.Near;
        g.DrawString(text, font, brush, rect, sf);
    }

    private void DrawPill(Graphics g, Rectangle rect, string text, bool active, Font font)
    {
        FillRound(g, rect, active ? Color.FromArgb(38, 82, 133) : panel, 12);
        if (active) FillRound(g, new Rectangle(rect.X + 2, rect.Bottom - 3, rect.Width - 4, 2), accent, 1);
        DrawText(g, text, font, active ? Color.White : Color.FromArgb(161, 176, 200), rect, ContentAlignment.MiddleCenter);
    }

    private void Animate()
    {
        bool changed = false;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                float target = hoverCell.HasValue && hoverCell.Value.X == c && hoverCell.Value.Y == r && board[r, c].State == CellState.Hidden ? 1f : 0f;
                float old = board[r, c].Hover;
                board[r, c].Hover = old + (target - old) * .27f;
                if (Math.Abs(target - old) > .02f) changed = true;
            }
        if (changed) Invalidate(boardRect);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point? next = null;
        if (boardRect.Contains(e.Location))
        {
            int size = boardRect.Width / cols;
            int c = (e.X - boardRect.X) / size, r = (e.Y - boardRect.Y) / size;
            if (r >= 0 && r < rows && c >= 0 && c < cols) next = new Point(c, r);
        }
        if (next != hoverCell) { hoverCell = next; }
        Cursor = hoverCell.HasValue || restartRect.Contains(e.Location) || beginnerRect.Contains(e.Location) || intermediateRect.Contains(e.Location) || expertRect.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e) { hoverCell = null; base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (restartRect.Contains(e.Location)) { NewGame(cols == 30 ? 2 : cols == 16 ? 1 : 0); return; }
        if (beginnerRect.Contains(e.Location)) { NewGame(0); return; }
        if (intermediateRect.Contains(e.Location)) { NewGame(1); return; }
        if (expertRect.Contains(e.Location)) { NewGame(2); return; }
        if (!boardRect.Contains(e.Location) || ended) return;
        int size = boardRect.Width / cols;
        int c = (e.X - boardRect.X) / size, r = (e.Y - boardRect.Y) / size;
        if (r < 0 || r >= rows || c < 0 || c >= cols) return;
        if (e.Button == MouseButtons.Right) ToggleFlag(r, c);
        else if (e.Button == MouseButtons.Left) Reveal(r, c);
        Invalidate();
    }

    private void PlaceMines(int safeR, int safeC)
    {
        var options = new List<Point>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (Math.Abs(r - safeR) > 1 || Math.Abs(c - safeC) > 1) options.Add(new Point(c, r));
        var rng = Random.Shared;
        foreach (var p in options.OrderBy(_ => rng.Next()).Take(mineCount)) board[p.Y, p.X].Mine = true;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (!board[r, c].Mine) board[r, c].Adjacent = Neighbors(r, c).Count(p => board[p.Y, p.X].Mine);
    }

    private IEnumerable<Point> Neighbors(int r, int c)
    {
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                int nr = r + dr, nc = c + dc;
                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols) yield return new Point(nc, nr);
            }
    }

    private void Reveal(int r, int c)
    {
        if (board[r, c].State != CellState.Hidden) return;
        if (!started)
        {
            PlaceMines(r, c);
            started = true;
            clock.Start();
        }
        var cell = board[r, c];
        if (cell.Mine)
        {
            cell.State = CellState.Revealed;
            ended = true; won = false; face = "×";
            clock.Stop();
            for (int rr = 0; rr < rows; rr++)
                for (int cc = 0; cc < cols; cc++)
                    if (board[rr, cc].Mine && board[rr, cc].State == CellState.Hidden) board[rr, cc].State = CellState.Revealed;
            return;
        }
        FloodReveal(r, c);
        int safeCount = rows * cols - mineCount;
        int revealed = board.Cast<Cell>().Count(x => x.State == CellState.Revealed);
        if (revealed >= safeCount)
        {
            ended = won = true; face = "★"; clock.Stop();
            if (!bestTimes.TryGetValue(DifficultyKey(), out int best) || seconds < best) bestTimes[DifficultyKey()] = seconds;
        }
    }

    private void FloodReveal(int r, int c)
    {
        var stack = new Stack<Point>();
        stack.Push(new Point(c, r));
        while (stack.Count > 0)
        {
            var p = stack.Pop();
            var cell = board[p.Y, p.X];
            if (cell.State != CellState.Hidden || cell.Mine) continue;
            cell.State = CellState.Revealed;
            if (cell.Adjacent == 0)
                foreach (var n in Neighbors(p.Y, p.X))
                    if (board[n.Y, n.X].State == CellState.Hidden) stack.Push(n);
        }
    }

    private void ToggleFlag(int r, int c)
    {
        if (!started || ended || board[r, c].State == CellState.Revealed) return;
        if (board[r, c].State == CellState.Flagged) { board[r, c].State = CellState.Hidden; flags--; }
        else if (flags < mineCount) { board[r, c].State = CellState.Flagged; flags++; }
    }
}
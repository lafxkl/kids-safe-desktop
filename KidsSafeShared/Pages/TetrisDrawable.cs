using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 俄罗斯方块的画布：左侧棋盘 + 右侧「下一个」预览。
/// 格子大小按可用空间自适应，窄屏（手机竖屏）与宽屏（Windows 窗口）都能铺满。
/// </summary>
public class TetrisDrawable : IDrawable
{
    public TetrisGameViewModel? Vm { get; set; }

    private static readonly Color BoardBg = Color.FromArgb("#37474F");
    private static readonly Color EmptyCell = Color.FromArgb("#2E3B45");
    private static readonly Color GridLine = Color.FromArgb("#455A64");
    private static readonly Color InfoBg = Color.FromArgb("#263238");

    public void Draw(ICanvas canvas, RectF rect)
    {
        var vm = Vm;
        if (vm is null || rect.Width < 40 || rect.Height < 40) return;

        const float pad = 10f;
        var availW = rect.Width - pad * 2;
        var availH = rect.Height - pad * 2;

        // 右侧信息栏宽度约为棋盘的 0.42 倍，先按高度算格子，再按宽度收敛
        var cell = Math.Min(availH / TetrisGameViewModel.Rows, availW / (TetrisGameViewModel.Cols + 4.2f));
        cell = Math.Max(cell, 4f);

        var boardW = cell * TetrisGameViewModel.Cols;
        var boardH = cell * TetrisGameViewModel.Rows;
        var infoW = cell * 4.2f;
        var totalW = boardW + infoW + pad;

        var originX = rect.X + (rect.Width - totalW) / 2f;
        var originY = rect.Y + (rect.Height - boardH) / 2f;

        DrawBoard(canvas, vm, originX, originY, cell, boardW, boardH);

        var infoX = originX + boardW + pad;
        DrawInfo(canvas, vm, infoX, originY, infoW, boardH, cell);
    }

    private static void DrawBoard(ICanvas canvas, TetrisGameViewModel vm,
        float x, float y, float cell, float w, float h)
    {
        canvas.SaveState();

        canvas.FillColor = BoardBg;
        canvas.FillRoundedRectangle(x - 6, y - 6, w + 12, h + 12, 14);

        // 空格
        for (var r = 0; r < TetrisGameViewModel.Rows; r++)
        {
            for (var c = 0; c < TetrisGameViewModel.Cols; c++)
            {
                var cx = x + c * cell;
                var cy = y + r * cell;
                canvas.FillColor = EmptyCell;
                canvas.FillRoundedRectangle(cx + 1, cy + 1, cell - 2, cell - 2, cell * 0.16f);
            }
        }

        // 已经落下的方块
        for (var r = 0; r < TetrisGameViewModel.Rows; r++)
        {
            for (var c = 0; c < TetrisGameViewModel.Cols; c++)
            {
                var v = vm.Board[r, c];
                if (v == 0) continue;
                DrawBlock(canvas, x + c * cell, y + r * cell, cell, Color.FromArgb(TetrisGameViewModel.PieceColors[v - 1]), 1f);
            }
        }

        // 幽灵落点：只描边，告诉孩子「按直落会停在这」
        var ghost = vm.GhostY;
        if (ghost > vm.PieceY && !vm.GameOver)
        {
            var ghostColor = Color.FromArgb(TetrisGameViewModel.PieceColors[vm.PieceType]);
            for (var r = 0; r < 4; r++)
            {
                for (var c = 0; c < 4; c++)
                {
                    if (vm.Piece[r, c] == 0) continue;
                    var gx = x + (vm.PieceX + c) * cell;
                    var gy = y + (ghost + r) * cell;
                    canvas.StrokeColor = ghostColor.WithAlpha(0.35f);
                    canvas.StrokeSize = Math.Max(1f, cell * 0.08f);
                    canvas.DrawRoundedRectangle(gx + 2, gy + 2, cell - 4, cell - 4, cell * 0.16f);
                }
            }
        }

        // 当前方块
        if (!vm.GameOver)
        {
            var color = Color.FromArgb(TetrisGameViewModel.PieceColors[vm.PieceType]);
            for (var r = 0; r < 4; r++)
            {
                for (var c = 0; c < 4; c++)
                {
                    if (vm.Piece[r, c] == 0) continue;
                    DrawBlock(canvas, x + (vm.PieceX + c) * cell, y + (vm.PieceY + r) * cell, cell, color, 1f);
                }
            }
        }

        canvas.RestoreState();
    }

    private static (int W, int H) Box(int[,] m)
    {
        var minR = 4; var maxR = -1; var minC = 4; var maxC = -1;
        for (var r = 0; r < 4; r++)
        {
            for (var c = 0; c < 4; c++)
            {
                if (m[r, c] == 0) continue;
                if (r < minR) minR = r;
                if (r > maxR) maxR = r;
                if (c < minC) minC = c;
                if (c > maxC) maxC = c;
            }
        }
        return maxR < 0 ? (0, 0) : (maxC - minC + 1, maxR - minR + 1);
    }

    private static void DrawBlock(ICanvas canvas, float x, float y, float cell, Color color, float alpha)
    {
        var radius = cell * 0.16f;

        canvas.FillColor = color.WithAlpha(alpha);
        canvas.FillRoundedRectangle(x + 1, y + 1, cell - 2, cell - 2, radius);

        // 顶部高光：让方块看起来像糖块
        canvas.FillColor = Colors.White.WithAlpha(0.22f * alpha);
        canvas.FillRoundedRectangle(x + cell * 0.16f, y + cell * 0.12f, cell * 0.68f, cell * 0.2f, cell * 0.08f);
    }

    private static void DrawInfo(ICanvas canvas, TetrisGameViewModel vm,
        float x, float y, float w, float h, float cell)
    {
        canvas.SaveState();

        canvas.FillColor = InfoBg;
        canvas.FillRoundedRectangle(x, y, w, cell * 6.2f, 14);

        canvas.FontColor = Colors.White.WithAlpha(0.75f);
        canvas.FontSize = Math.Max(10f, cell * 0.62f);
        canvas.DrawString("下一个", x, y + cell * 0.55f, w, cell,
            HorizontalAlignment.Center, VerticalAlignment.Center);

        var next = TetrisGameViewModel.Shapes[vm.NextType];
        var color = Color.FromArgb(TetrisGameViewModel.PieceColors[vm.NextType]);

        // 找出预览方块的包围盒，居中画在面板里
        var (bw, bh) = Box(next);
        var minR = 4; var minC = 4;
        for (var r = 0; r < 4; r++)
            for (var c = 0; c < 4; c++)
                if (next[r, c] != 0)
                {
                    if (r < minR) minR = r;
                    if (c < minC) minC = c;
                }

        var previewCell = Math.Min(cell * 0.8f, w / 5.2f);
        var startX = x + (w - bw * previewCell) / 2f;
        var startY = y + cell * 1.5f + (cell * 4f - bh * previewCell) / 2f;

        for (var r = 0; r < 4; r++)
        {
            for (var c = 0; c < 4; c++)
            {
                if (next[r, c] == 0) continue;
                DrawBlock(canvas, startX + (c - minC) * previewCell,
                    startY + (r - minR) * previewCell, previewCell, color, 1f);
            }
        }

        // 下方：「已消行数」不用页面顶栏重复显示，这里放一条小提示
        canvas.FontColor = Colors.White.WithAlpha(0.55f);
        canvas.FontSize = Math.Max(9f, cell * 0.5f);
        canvas.DrawString($"消行 {vm.Lines}", x, y + cell * 5.4f, w, cell * 0.9f,
            HorizontalAlignment.Center, VerticalAlignment.Center);

        canvas.RestoreState();
    }
}

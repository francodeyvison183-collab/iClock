using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;
using System.Media;
using System.Net;
using System.Drawing.Imaging;
using System.Globalization;
using System.Collections;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Web.Script.Serialization;

internal sealed class MenuSwitchItem : ToolStripMenuItem
{
    public bool On;
}

internal sealed class ModernContextMenu : ContextMenuStrip
{
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int w = ClientSize.Width;
        // 第一次测量时客户区可能被撑得比菜单宽，按那个宽度排会让定位用的宽度偏大。
        if (MaximumSize.Width > 0 && w > MaximumSize.Width)
            w = MaximumSize.Width;
        if (w <= 0) return;
        foreach (ToolStripItem item in Items)
        {
            if (item != null && item.Width != w)
                item.Size = new Size(w, item.Height);
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size size = base.GetPreferredSize(proposedSize);
        // 弹出位置用的是建议宽度。把它收成菜单的实际宽度，方向仍走原来的计算。
        if (MaximumSize.Width > 0)
            size.Width = MaximumSize.Width;
        return size;
    }
}

internal sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
{
    public ModernMenuRenderer() : base(new ModernMenuColorTable()) { }

    // 快捷键与开机开关共用这条右边线，和「Alt + 拖拽」右缘对齐。
    internal const int MenuTextLeft = 32;
    internal const int MenuShortcutRight = 24;
    internal const int MenuSwitchWidth = 40;

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Enabled) return;
        if (e.Item.Selected)
        {
            Rectangle rc = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
            using (GraphicsPath path = AppContext.GetRoundedRectPath(rc, 4))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(238, 238, 238)))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
            }
        }
        MenuSwitchItem sw = e.Item as MenuSwitchItem;
        if (sw != null) DrawMenuSwitch(e.Graphics, e.Item.Width, e.Item.Height, sw.On);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        int left = MenuTextLeft;
        MenuSwitchItem sw = e.Item as MenuSwitchItem;
        ToolStripMenuItem mi = e.Item as ToolStripMenuItem;
        int rightPad = sw != null ? MenuShortcutRight + MenuSwitchWidth + 8
            : (mi != null && mi.HasDropDownItems ? MenuShortcutRight + 16 : MenuShortcutRight);
        int right = e.Item.Width - rightPad;
        int width = Math.Max(0, right - left);
        e.TextRectangle = new Rectangle(left, 0, width, e.Item.Height);

        bool isShortcut = mi != null && !string.IsNullOrEmpty(mi.ShortcutKeyDisplayString) && e.Text == mi.ShortcutKeyDisplayString;

        if (isShortcut)
        {
            e.TextColor = (e.Text.Contains("") || e.Text.Contains(""))
                ? Color.FromArgb(202, 80, 16)
                : AppContext.Win11ShortcutGray;
            e.TextFormat = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        }
        else
        {
            e.TextFormat = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            if (e.Item.Enabled && !e.Item.ForeColor.IsEmpty && e.Item.ForeColor != SystemColors.ControlText && e.Item.ForeColor != AppContext.Win11TextPrimary)
                e.TextColor = e.Item.ForeColor;
            else if (e.Item.Enabled)
                e.TextColor = AppContext.Win11TextPrimary;
        }
        base.OnRenderItemText(e);
    }

    private static void DrawMenuSwitch(Graphics g, int itemWidth, int itemHeight, bool on)
    {
        int w = MenuSwitchWidth;
        int h = 22;
        int x = itemWidth - MenuShortcutRight - w;
        int y = Math.Max(0, (itemHeight - h) / 2);
        SmoothingMode prev = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle track = new Rectangle(x + 1, y + 2, w - 3, h - 5);
        int diameter = track.Height;
        using (GraphicsPath path = new GraphicsPath())
        {
            path.AddArc(track.X, track.Y, diameter, diameter, 90, 180);
            path.AddArc(track.Right - diameter, track.Y, diameter, diameter, 270, 180);
            path.CloseFigure();
            using (SolidBrush brush = new SolidBrush(on ? AppContext.Win11Accent : Color.FromArgb(190, 196, 204)))
                g.FillPath(brush, path);
        }
        int knobSize = Math.Max(4, diameter - 4);
        int knobX = on ? track.Right - knobSize - 2 : track.X + 2;
        using (SolidBrush knob = new SolidBrush(Color.White))
            g.FillEllipse(knob, knobX, track.Y + 2, knobSize, knobSize);
        g.SmoothingMode = prev;
    }

    // 子菜单箭头右缘与开机开关、快捷键同一条线（item.Width - 24）。
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        if (e.Item == null) return;
        int tipX = e.Item.Width - MenuShortcutRight;
        int cy = e.Item.Height / 2;
        Color c = e.Item.Enabled ? AppContext.Win11ShortcutGray : Color.FromArgb(180, 184, 190);
        SmoothingMode prev = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (Pen p = new Pen(c, 1.5f))
        {
            p.StartCap = LineCap.Round;
            p.EndCap = LineCap.Round;
            e.Graphics.DrawLine(p, tipX - 5, cy - 4, tipX, cy);
            e.Graphics.DrawLine(p, tipX, cy, tipX - 5, cy + 4);
        }
        e.Graphics.SmoothingMode = prev;
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using (Pen p = new Pen(AppContext.Win11Border, 1f))
        {
            e.Graphics.DrawLine(p, 32, y, e.Item.Width - 32, y);
        }
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using (Pen p = new Pen(AppContext.Win11Border, 1f))
        {
            e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }
}

internal sealed class ModernMenuColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground { get { return AppContext.Win11Bg; } }
    public override Color ImageMarginGradientBegin { get { return AppContext.Win11Bg; } }
    public override Color ImageMarginGradientMiddle { get { return AppContext.Win11Bg; } }
    public override Color ImageMarginGradientEnd { get { return AppContext.Win11Bg; } }
    public override Color MenuBorder { get { return AppContext.Win11Border; } }
    public override Color MenuItemBorder { get { return Color.Transparent; } }
}


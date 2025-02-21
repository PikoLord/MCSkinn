﻿//
//    MCSkinn, a 3d skin management studio for Minecraft
//    Copyright (C) 2013 Altered Softworks & MCSkinn Team
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Drawing;
using System.Windows.Forms;
using MCSkinn.Forms.Controls;
using MCSkinn.Scripts.Paril.Drawing;
using MCSkinn.Scripts.Paril.OpenGL;
using MCSkinn.Scripts.Setting;
using Brush = MCSkinn.Forms.Controls.Brush;
using Brushes = MCSkinn.Forms.Controls.Brushes;

namespace MCSkinn.Scripts.Tools
{
    public class StampTool : ITool
    {
        private Brush _brushThatWasStamped;
        private Point _oldPixel = new Point(-1, -1);
        private ColorPixel[,] _stampedBrush;
        private PixelsChangedUndoable _undo;

        private bool HoldingShift
        {
            get { return (Control.ModifierKeys & Keys.Shift) != 0; }
        }

        public bool IsPreview { get; private set; }

        #region ITool Members

        public bool MouseMoveOnSkin(ColorGrabber pixels, Skin skin, int x, int y)
        {
            return MouseMoveOnSkin(pixels, skin, x, y, GlobalSettings.PencilIncremental);
        }

        public void SelectedBrushChanged()
        {
            _stampedBrush = null;
        }

        public virtual void BeginClick(Skin skin, Point p, MouseEventArgs e)
        {
            _undo = new PixelsChangedUndoable(Editor.GetLanguageString("U_PIXELSCHANGED"),
                                              Editor.MainForm.SelectedTool.MenuItem.Text);
        }

        public virtual void MouseMove(Skin skin, MouseEventArgs e)
        {
        }

        public virtual bool RequestPreview(ColorGrabber pixels, Skin skin, int x, int y)
        {
            Brush brush = Brushes.SelectedBrush;
            if (_brushThatWasStamped != brush)
                _stampedBrush = null;
            if (x == -1)
                return false;
            if (_stampedBrush == null && !HoldingShift)
                return false;

            int startX = x - brush.Width / 2;
            int startY = y - brush.Height / 2;
            IsPreview = true;

            for (int ry = 0; ry < brush.Height; ++ry)
            {
                for (int rx = 0; rx < brush.Width; ++rx)
                {
                    int xx = startX + rx;
                    int yy = startY + ry;

                    if (xx < 0 || xx >= skin.Width ||
                        yy < 0 || yy >= skin.Height)
                        continue;

                    if (brush[rx, ry] == 0.0f)
                        continue;

                    ColorPixel c = pixels[xx, yy];
                    Color newColor;

                    if (!HoldingShift)
                    {
                        Color oldColor = Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue);
                        Color color = Color.FromArgb(_stampedBrush[rx, ry].Alpha, _stampedBrush[rx, ry].Red, _stampedBrush[rx, ry].Green,
                                                     _stampedBrush[rx, ry].Blue);
                        color = Color.FromArgb((byte)(brush[rx, ry] * 255 * (color.A / 255.0f)), color);

                        newColor = BlendColor(color, oldColor);
                    }
                    else
                        newColor = Color.FromArgb(c.Alpha, Color.White);

                    pixels[xx, yy] = new ColorPixel(newColor.R | newColor.G << 8 | newColor.B << 16 | newColor.A << 24);
                }
            }

            return true;
        }

        public virtual bool EndClick(ColorGrabber pixels, Skin skin, MouseEventArgs e)
        {
            if (_undo.Points.Count != 0)
            {
                skin.Undo.AddBuffer(_undo);
                Editor.MainForm.CheckUndo();
                _oldPixel = new Point(-1, -1);
            }

            _undo = null;

            return false;
        }

        public string GetStatusLabelText()
        {
            return Editor.GetLanguageString("T_STAMP");
        }

        #endregion

        public Color BlendColor(Color l, Color r)
        {
            return (Color)ColorBlending.AlphaBlend(l, r);
        }

        public bool MouseMoveOnSkin(ColorGrabber pixels, Skin skin, int x, int y, bool incremental)
        {
            Brush brush = Brushes.SelectedBrush;
            if (_brushThatWasStamped != brush)
                _stampedBrush = null;

            if (x == _oldPixel.X && y == _oldPixel.Y)
                return false;
            if (_stampedBrush == null && !HoldingShift)
                return false;

            IsPreview = false;

            int startX = x - brush.Width / 2;
            int startY = y - brush.Height / 2;

            for (int ry = 0; ry < brush.Height; ++ry)
            {
                for (int rx = 0; rx < brush.Width; ++rx)
                {
                    int xx = startX + rx;
                    int yy = startY + ry;

                    if (xx < 0 || xx >= skin.Width ||
                        yy < 0 || yy >= skin.Height)
                        continue;

                    if (brush[rx, ry] == 0.0f)
                        continue;

                    ColorPixel c = pixels[xx, yy];

                    if (HoldingShift)
                    {
                        if (_stampedBrush == null)
                            _stampedBrush = new ColorPixel[brush.Width, brush.Height];

                        _brushThatWasStamped = brush;
                        _stampedBrush[rx, ry] = c;
                        continue;
                    }

                    Color oldColor = Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue);
                    Color color = Color.FromArgb(_stampedBrush[rx, ry].Alpha, _stampedBrush[rx, ry].Red, _stampedBrush[rx, ry].Green,
                                                 _stampedBrush[rx, ry].Blue);

                    byte maxAlpha = color.A;
                    var alphaToAdd =
                        (float)
                        (byte)(brush[rx, ry] * 255 * (Editor.MainForm.ColorPanel.SelectedColor.A / 255.0f * (color.A / 255.0f)));

                    if (!incremental && _undo.Points.ContainsKey(new Point(xx, yy)) &&
                        _undo.Points[new Point(xx, yy)].Item2.TotalAlpha >= maxAlpha)
                        continue;

                    if (!incremental && _undo.Points.ContainsKey(new Point(xx, yy)) &&
                        _undo.Points[new Point(xx, yy)].Item2.TotalAlpha + alphaToAdd >= maxAlpha)
                        alphaToAdd = maxAlpha - _undo.Points[new Point(xx, yy)].Item2.TotalAlpha;

                    color = Color.FromArgb((byte)alphaToAdd, color);

                    Color newColor = BlendColor(color, oldColor);

                    if (oldColor == newColor)
                        continue;

                    if (_undo.Points.ContainsKey(new Point(xx, yy)))
                    {
                        Tuple<Color, ColorAlpha> tupl = _undo.Points[new Point(xx, yy)];
                        _undo.Points[new Point(xx, yy)] = Tuple.Create(tupl.Item1, new ColorAlpha(newColor, tupl.Item2.TotalAlpha + alphaToAdd));
                    }
                    else
                        _undo.Points.Add(new Point(xx, yy), Tuple.Create(oldColor, new ColorAlpha(newColor, alphaToAdd)));

                    pixels[xx, yy] = new ColorPixel(newColor.R | newColor.G << 8 | newColor.B << 16 | newColor.A << 24);
                }
            }

            _oldPixel = new Point(x, y);

            return !HoldingShift;
        }
    }
}
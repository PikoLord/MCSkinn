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
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using MCSkinn.Forms;
using MCSkinn.Scripts.Models;
using MCSkinn.Scripts.Paril.Components.Undo;
using MCSkinn.Scripts.Paril.Imaging;
using MCSkinn.Scripts.Paril.OpenGL;
using Microsoft.VisualBasic.FileIO;
using OpenTK;
using MCSkinn.Scripts.Paril.Drawing;
using Inkore.Common;

namespace MCSkinn.Scripts
{
    [Serializable]
    public class Skin : TreeNode, IDisposable
    {
        private readonly Dictionary<int, bool> _transparentParts = new Dictionary<int, bool>();
        public bool Dirty;
        public Texture GLImage;
        public Bitmap Head;
        public Size Size;
        public UndoBuffer Undo;
        public bool IsLastSkin { get; set; }

        public Skin(string fileName)
        {
            Undo = new UndoBuffer(this);
            Name = Path.GetFileNameWithoutExtension(fileName);

            Program.Log(LogType.Load, string.Format("Loaded skin '{0}'", Path.GetFileName(fileName)), "at MCSkinn.Scripts.Skin(string)");

        }

        public Skin(FileInfo file) :
            this(file.FullName)
        {
        }

        private Model _model;
        public Model Model
        {
            get 
            { 
                return _model; 
            }
            set { _model = value; }
        }

        public int Width
        {
            get { return Size.Width; }
        }

        public int Height
        {
            get { return Size.Height; }
        }

        public Dictionary<int, bool> TransparentParts
        {
            get { return _transparentParts; }
        }

        public new string Name
        {
            get { return base.Name; }
            set
            {
                base.Name = value;
                Text = base.Name = value;
            }
        }

        public DirectoryInfo Directory
        {
            get
            {
                if (Level == 0)
                    return new DirectoryInfo(Editor.RootFolderString);

                return new DirectoryInfo(Parent != null ? Editor.GetFolderForNode(Parent) : "");
            }
        }

        public FileInfo File
        {
            get { return new FileInfo(Directory.FullName + '\\' + Name + ".png"); }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (GLImage != null)
            {
                GLImage.Dispose();
                GLImage = null;
            }

            if (Head != null)
            {
                Head.Dispose();
                Head = null;
            }
        }

        #endregion

        static void SetImage(Skin skin)
        {
            Editor.MainForm.Renderer.MakeCurrent();
            skin.GLImage = new TextureGL(skin.File.FullName);
            skin.GLImage.SetMipmapping(false);
            skin.GLImage.SetRepeat(false);
        }

        public bool SetImages(bool updateGL = true)
        {
            Bitmap image = null;

            try
            {
                using (FileStream file = File.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
                    image = new Bitmap(file);

                Size = image.Size;

                if (Head != null)
                {
                    Head.Dispose();

                    if (updateGL)
                    {
                        Unload();
                        Create();
                    }
                }

                float scale = Size.Width / 64.0f;
                var headSize = (int)(8.0f * scale);
                var helmetLoc = (int)(40.0f * scale);

                Head = new Bitmap(headSize, headSize);
                using (Graphics g = Graphics.FromImage(Head))
                {
                    g.DrawImage(image, new Rectangle(0, 0, headSize, headSize), new Rectangle(headSize, headSize, headSize, headSize),
                                GraphicsUnit.Pixel);
                    g.DrawImage(image, new Rectangle(0, 0, headSize, headSize), new Rectangle(helmetLoc, headSize, headSize, headSize),
                                GraphicsUnit.Pixel);
                }

                if (Model == null)
                {
                    Dictionary<string, string> metadata = PNGMetadata.ReadMetadata(File.FullName);

                    if (metadata.ContainsKey("Model"))
                    {
                        Model = ModelLoader.GetModelByName(metadata["Model"]);

                        if (Model == null)
                        {
                            if (image.Height == 64)
                                Model = ModelLoader.GetModelByName("geometry.humanoid.custom");
                            else
                                Model = ModelLoader.GetModelByName("geometry.humanoid.custom.minimal");
                        }
                    }
                    
                    if (Model == null)
                    {
                        if (image.Height == 64)
                            Model = ModelLoader.GetModelByName("geometry.humanoid.custom");
                        else
                            Model = ModelLoader.GetModelByName("geometry.humanoid.custom.minimal");
                    }
                }
            }
            catch (Exception ex) 
            { 
                Program.Log(ex, false); 
#if DEBUG
            throw;
#endif
                //MessageBox.Show(string.Format(Editor.GetLanguageString("E_SKINERROR"), File.FullName));
                return false;
            }
            finally
            {
                if (image != null)
                    image.Dispose();
            }

            return true;
        }

        public void Create()
        {
            if (Editor.MainForm.IsHandleCreated)
            {
                Form f = Editor.MainForm;

                if (f.InvokeRequired)
                    f.Invoke(SetImage, this);
                else
                    SetImage(this);

                if (f.InvokeRequired)
                    f.Invoke(SetTransparentParts);
                else
                    SetTransparentParts();

            }
            else
            {
                var f =Program.Page_Splash.Dispatcher;

                f.Invoke(SetImage, this);

                f.Invoke(SetTransparentParts);

            }
            //Form f = Editor.MainForm.IsHandleCreated ? Editor.MainForm : (Form)Program.Context.SplashForm;

            //if (f.InvokeRequired)
            //    f.Invoke(SetImage, this);
            //else
            //    SetImage(this);

            //if (f.InvokeRequired)
            //    f.Invoke(SetTransparentParts);
            //else
            //    SetTransparentParts();
        }

        public void Unload()
        {
            GLImage.Dispose();
            GLImage = null;
        }

        public override string ToString()
        {
            //if (Dirty)
            //    return Name + " *";
            return Name;
        }

        public void CommitChanges(Texture currentSkin, bool save)
        {
            using (var grabber = new ColorGrabber(currentSkin, Width, Height))
            {
                grabber.Load();

                if (currentSkin != GLImage)
                {
                    grabber.Texture = GLImage;
                    grabber.Save();
                }

                if (save)
                {
                    var newBitmap = new Bitmap(Width, Height);

                    using (var fp = new FastPixel(newBitmap, true))
                    {
                        for (int y = 0; y < Height; ++y)
                        {
                            for (int x = 0; x < Width; ++x)
                            {
                                ColorPixel c = grabber[x, y];
                                fp.SetPixel(x, y, Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue));
                            }
                        }
                    }

                    newBitmap.Save(File.FullName);
                    newBitmap.Dispose();

                    var md = new Dictionary<string, string>();
                    md.Add("Model", Model.Name);
                    PNGMetadata.WriteMetadata(File.FullName, md);

                    SetImages(true);

                    Dirty = false;
                }
            }
        }

        public bool ChangeName(string newName, bool force = false)
        {
            if (!newName.EndsWith(".png"))
                newName += ".png";

            if (force)
            {
                Name = Path.GetFileNameWithoutExtension(newName);
                return true;
            }

            if (System.IO.File.Exists(Directory.FullName + "\\" + newName))
                return false;

            FileSystem.RenameFile(File.FullName, newName);
            //File.MoveToParent(newName);
            Name = Path.GetFileNameWithoutExtension(newName);

            return true;
        }

        public void Resize(int width, int height, ResizeType type = ResizeType.Scale)
        {
            using (var newBitmap = new Bitmap(width, height))
            {
                using (Graphics g = Graphics.FromImage(newBitmap))
                {
                    g.SmoothingMode = SmoothingMode.None;
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.FromArgb(0, 0, 0, 0));

                    using (Image temp = Image.FromFile(File.FullName))
                    {
                        if (type == ResizeType.Scale)
                            g.DrawImage(temp, 0, 0, newBitmap.Width, newBitmap.Height);
                        else
                            g.DrawImage(temp, 0, 0, temp.Width, temp.Height);
                    }
                }

                newBitmap.Save(File.FullName);

                var md = new Dictionary<string, string>();
                md.Add("Model", Model.Name);
                PNGMetadata.WriteMetadata(File.FullName, md);
            }

            SetImages();

            Undo.Clear();
            Editor.MainForm.CheckUndo();
        }

        public void Delete()
        {
            FileSystem.DeleteFile(File.FullName, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            //File.Delete();
        }

        public void MoveTo(string newPath)
        {
            while (System.IO.File.Exists(newPath))
                newPath = newPath.Insert(newPath.Length - 4, " - " + Editor.GetLanguageString("C_MOVED"));

            FileSystem.MoveFile(File.FullName, newPath);
            //File.MoveTo(newPath);
        }

        public void CheckTransparentPart(ColorGrabber grabber, int index)
        {
            foreach (Face f in Model.Meshes[index].Faces)
            {
                var bounds = new Bounds(new Point(9999, 9999), new Point(-9999, -9999));

                foreach (Vector2 c in f.TexCoords)
                {
                    var coord = new Vector2(c.X * Width, c.Y * Height);
                    bounds.AddPoint(new Point((int)coord.X, (int)coord.Y));
                }

                Rectangle rect = bounds.ToRectangle();
                bool gotOne = false;

                for (int y = rect.Y; !gotOne && y < rect.Y + rect.Height; ++y)
                {
                    for (int x = rect.X; x < rect.X + rect.Width; ++x)
                    {
                        ColorPixel pixel = grabber[x, y];

                        if (pixel.Alpha != 255)
                        {
                            gotOne = true;
                            break;
                        }
                    }
                }

                if (gotOne)
                {
                    TransparentParts[index] = gotOne;
                    return;
                }
            }

            TransparentParts[index] = false;
        }

        public void SetTransparentParts()
        {
            using (var grabber = new ColorGrabber(GLImage, Width, Height))
            {
                grabber.Load();

                int mesh = 0;

                TransparentParts.Clear();

                foreach (Mesh m in Model.Meshes)
                {
                    TransparentParts.Add(mesh, false);

                    CheckTransparentPart(grabber, mesh);
                    mesh++;
                }
            }
        }
    }
}
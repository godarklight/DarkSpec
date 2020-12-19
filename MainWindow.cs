using System;
using System.Threading;
using Gtk;
using Pixbuf = Gdk.Pixbuf;
using Image = Gtk.Image;
using UI = Gtk.Builder.ObjectAttribute;

namespace DarkSpec
{
    class MainWindow : Window
    {
        [UI] private Image gtkImage = null;
        private const int SIZEX = 1280;
        private const int SIZEY = 512;
        private const int FPS = 60;
        private SpecWorker specWorker;
        private byte[] specData;
        private AutoResetEvent drawSync = new AutoResetEvent(false);

        public MainWindow() : this(new Builder("MainWindow.glade")) { }

        private MainWindow(Builder builder) : base(builder.GetObject("MainWindow").Handle)
        {
            builder.Autoconnect(this);
            DeleteEvent += Window_DeleteEvent;
            specData = new byte[SIZEX * SIZEY * 4];
            for (int i = 3; i < specData.Length; i = i + 4)
            {
                specData[i] = 255;
            }
            gtkImage.Pixbuf = new Pixbuf(specData, Gdk.Colorspace.Rgb, true, 8, SIZEX, SIZEY, SIZEX * 4, null);
            specWorker = new SpecWorker(SIZEX, SIZEY, FPS, specData, UpdateFrame, drawSync);
        }

        private void UpdateFrame()
        {
            Application.Invoke(delegate
            {
                gtkImage.Pixbuf = new Pixbuf(specData, Gdk.Colorspace.Rgb, true, 8, SIZEX, SIZEY, SIZEX * 4, null);
                drawSync.Set();
            });
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            specWorker.Stop();
            Application.Quit();
        }
    }
}

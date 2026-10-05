using System;
using System.Drawing;
using System.Windows.Forms;

namespace TbhCompanion
{
    // System-tray host: runs the presence loop on a background thread and shows
    // a tray icon with the current status and a Quit option. No console window.
    public class TrayApp : ApplicationContext
    {
        readonly NotifyIcon _icon;
        readonly PresenceEngine _engine;
        readonly System.Threading.Thread _worker;
        // Kept as fields so a language switch can re-text the menu in place.
        ToolStripMenuItem _statusItem, _presenceItem, _openItem, _quitItem;
        string _lastStatus = Lang.T("starting...");
        StatusForm _form;

        public TrayApp(PresenceEngine engine)
        {
            _engine = engine;

            var menu = new ContextMenuStrip();
            _statusItem = new ToolStripMenuItem(Lang.T("Starting...")) { Enabled = false };
            menu.Items.Add(_statusItem);
            menu.Items.Add(new ToolStripSeparator());
            _presenceItem = new ToolStripMenuItem(Lang.T("Enable presence")) { Checked = _engine.PresenceEnabled, CheckOnClick = true };
            _presenceItem.Click += delegate { _engine.SetPresenceEnabled(_presenceItem.Checked); };
            menu.Items.Add(_presenceItem);
            // keep the check in sync if it was toggled from the settings window
            menu.Opening += delegate { _presenceItem.Checked = _engine.PresenceEnabled; };
            _openItem = new ToolStripMenuItem(Lang.T("Status && Settings..."));
            _openItem.Click += delegate { OpenForm(); };
            menu.Items.Add(_openItem);
            _quitItem = new ToolStripMenuItem(Lang.T("Quit"));
            _quitItem.Click += delegate { ExitThread(); };
            menu.Items.Add(_quitItem);

            _icon = new NotifyIcon();
            _icon.Icon = LoadIcon();
            _icon.Text = "TBH Companion";   // tooltip (<= 63 chars)
            _icon.Visible = true;
            _icon.ContextMenuStrip = menu;
            _icon.DoubleClick += delegate { OpenForm(); };

            // engine reports status text -> reflect in tooltip + menu (marshal to UI thread)
            _engine.OnStatus += delegate(string s)
            {
                // Update the shared status unconditionally (the settings window polls
                // it on its own UI thread). The tray menu/tooltip is only refreshed
                // once its handle exists, to avoid a cross-thread handle exception.
                _lastStatus = s;
                try
                {
                    if (menu.IsDisposed) return;
                    if (menu.IsHandleCreated)
                        menu.BeginInvoke((Action)delegate
                        {
                            _statusItem.Text = s;
                            _icon.Text = Truncate("TBH: " + s, 63);
                        });
                    else
                    {
                        _statusItem.Text = s;
                        _icon.Text = Truncate("TBH: " + s, 63);
                    }
                }
                catch { }
            };

            _worker = new System.Threading.Thread(delegate() { _engine.Run(); });
            _worker.IsBackground = true;
            _worker.Start();

            Lang.Changed += OnLanguageChanged;
            ThreadExit += delegate { Shutdown(); };
        }

        // Re-text the menu in place. Rebuilding the ContextMenuStrip would drop
        // the Opening check-sync and the presence click handler, and risk the
        // cross-thread tray handle exception the OnStatus marshaler guards against.
        void OnLanguageChanged()
        {
            try
            {
                if (_statusItem == null || _statusItem.IsDisposed) return;
                _statusItem.Text = _lastStatus;
                _presenceItem.Text = Lang.T("Enable presence");
                _openItem.Text = Lang.T("Status && Settings...");
                _quitItem.Text = Lang.T("Quit");
                // The presence check state belongs to the engine — leave it alone.
                _icon.Text = Truncate("TBH: " + _lastStatus, 63);
            }
            catch { }
        }

        void OpenForm()
        {
            if (_form == null || _form.IsDisposed)
            {
                _form = new StatusForm(
                    delegate { return _engine.LastStageLabel; },
                    delegate { return _engine.DiscordConnected; },
                    delegate { return _lastStatus; },
                    delegate { return _engine.PresenceEnabled; },
                    delegate(bool on) { _engine.SetPresenceEnabled(on); },
                    delegate { return _engine.WaitingForGame; });
                _form.Show();
            }
            else
            {
                if (_form.WindowState == FormWindowState.Minimized)
                    _form.WindowState = FormWindowState.Normal;
                _form.Activate();
            }
        }

        void Shutdown()
        {
            Lang.Changed -= OnLanguageChanged;
            _engine.Stop();
            try { _worker.Join(3000); } catch { }
            // If a scheduled restart already closed the game, let relaunch finish
            // so Quit cannot leave TaskBarHero dead.
            GameRestart.WaitIdle(60000);
            _icon.Visible = false;
            _icon.Dispose();
        }

        static string Truncate(string s, int max)
        {
            return s != null && s.Length > max ? s.Substring(0, max) : s;
        }

        // The exe's own icon (embedded TBH logo via /win32icon); drawn fallback
        // keeps things working if extraction ever fails.
        static Icon LoadIcon()
        {
            try
            {
                var ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ico != null) return ico;
            }
            catch { }
            return MakeIcon();
        }

        // Draw a tiny icon at runtime so the exe stays a single self-contained file.
        static Icon MakeIcon()
        {
            using (var bmp = new Bitmap(16, 16))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using (var bg = new SolidBrush(Color.FromArgb(88, 101, 242)))   // Discord blurple
                    g.FillRectangle(bg, 2, 2, 12, 12);
                using (var fg = new SolidBrush(Color.White))
                {
                    g.FillRectangle(fg, 4, 9, 2, 3);   // three "hero" bars
                    g.FillRectangle(fg, 7, 6, 2, 6);
                    g.FillRectangle(fg, 10, 8, 2, 4);
                }
                IntPtr h = bmp.GetHicon();
                using (var tmp = Icon.FromHandle(h))
                    return (Icon)tmp.Clone();
            }
        }
    }
}

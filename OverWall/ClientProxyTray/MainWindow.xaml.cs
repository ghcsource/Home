using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ClientProxyTray
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private NotifyIcon notifyIcon;

        public MainWindow()
        {
            InitializeComponent();

            Init();

            Run(null, null);
        }

        private void Init()
        {
            ThreadPool.SetMinThreads(Config.minWorkerThreads, Config.minIOThreads);

            notifyIcon = new NotifyIcon();
            notifyIcon.Text = "OverWall";
            notifyIcon.Visible = true;
            
            System.Windows.Forms.MenuItem run = new System.Windows.Forms.MenuItem("Run");
            run.Visible = false;
            run.Click += Run;
            
            System.Windows.Forms.MenuItem stop = new System.Windows.Forms.MenuItem("Stop");
            stop.Visible = true;
            stop.Click += Stop;

            System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem("Exit");
            exit.Click += Exit;

            System.Windows.Forms.MenuItem[] menu = new System.Windows.Forms.MenuItem[] { run, stop, exit };
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(menu);
        }

        private void Run(object sender, EventArgs e)
        {
            Proxy.Start();

            if (Config.modifySystemProxy == true)
            {
                SystemProxy.Enable("127.0.0.1:" + Config.localPort);
            }

            notifyIcon.ContextMenu.MenuItems[0].Visible = false;
            notifyIcon.ContextMenu.MenuItems[1].Visible = true;
            notifyIcon.Icon = new Icon(System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/icon/server_run.ico")).Stream);
        }

        private void Stop(object sender, EventArgs e)
        {
            Proxy.Stop();

            if (Config.modifySystemProxy == true)
            {
                SystemProxy.Disable();
            }

            notifyIcon.ContextMenu.MenuItems[0].Visible = true;
            notifyIcon.ContextMenu.MenuItems[1].Visible = false;
            notifyIcon.Icon = new Icon(System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/icon/server_stop.ico")).Stream);
        }

        private void Exit(object sender, EventArgs e)
        {
            if (Config.modifySystemProxy == true)
            {
                SystemProxy.Disable();
            }
            notifyIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        }
    }
}

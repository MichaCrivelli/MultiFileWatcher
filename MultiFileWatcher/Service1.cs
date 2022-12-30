using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace MultiFileWatcher
{
    public partial class Service1 : ServiceBase
    {

        FileWatcher fileWatcher;

        public Service1()
        {
            InitializeComponent();
            fileWatcher = new FileWatcher();
        }

        protected override void OnStart(string[] args)
        {
            fileWatcher.Start();
        }

        protected override void OnStop()
        {
        }

        public void OnDebug()
        {
            OnStart(null);
        }
    }
}

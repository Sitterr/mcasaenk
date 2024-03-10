using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Text;
using System.Windows;

namespace Mcasaenk {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        [STAThread]
        protected override void OnStartup(StartupEventArgs e) {
            Debug.WriteLine("int main()?");

            //GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        }
        public const string WORLDPATH = $"C:\\Users\\nikol\\AppData\\Roaming\\.minecraft\\saves\\Niki2_ - Copy";
    }

}

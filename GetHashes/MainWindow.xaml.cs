using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Security.Cryptography;

namespace GetHashes
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        protected string GetMD5(string fileName)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(fileName))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty);
                }
            }
        }

        protected string GetSHA1(string fileName)
        {
            using (var sha1 = SHA1.Create())
            {
                using (var stream = File.OpenRead(fileName))
                {
                    return BitConverter.ToString(sha1.ComputeHash(stream)).Replace("-", string.Empty);
                }
            }
        }
        protected string GetSHA256(string fileName)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(fileName))
                {
                    return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
                }
            }
        }
        protected string GetSHA512(string fileName)
        {
            using (var sha512 = SHA512.Create())
            {
                using (var stream = File.OpenRead(fileName))
                {
                    return BitConverter.ToString(sha512.ComputeHash(stream)).Replace("-", string.Empty);
                }
            }
        }

        protected string GetSSDEEP(string fileName)
        {
            try
            {
                var bytes = File.ReadAllBytes(fileName);
                return SsdeepNET.Hasher.HashBuffer(bytes, bytes.Length);
            }
            catch { return ""; }
        }

        public MainWindow()
        {
            InitializeComponent();
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length == 2)
            {
                if (File.Exists(args[1]))
                {
                    md5textbox.Text = GetMD5(args[1]);
                    sha1textbox.Text = GetSHA1(args[1]);
                    sha256textbox.Text = GetSHA256(args[1]);
                    sha512textbox.Text = GetSHA512(args[1]);
                    ssdeeptextbox.Text = GetSSDEEP(args[1]);
                }
                else
                {
                    MessageBox.Show("Error, the file "+args[1]+" does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                }
            }
            else
            {
                MessageBox.Show("Error, no file in argument.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }
    }
}

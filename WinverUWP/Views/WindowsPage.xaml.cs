using RegistryRT;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources;
using Windows.System.Profile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using WinverUWP.Helpers;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace WinverUWP.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WindowsPage : Page
    {
        ResourceLoader resourceLoader = Singleton.GetInstance<ResourceLoader>();
        Registry registry = Singleton.GetInstance<Registry>();

        public WindowsPage()
        {
            this.InitializeComponent();
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            DataPackage package = new DataPackage();
            string targetText = "";
            string deviceFamilyVersion = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong version = ulong.Parse(deviceFamilyVersion);
            ulong build = (version & 0x00000000FFFF0000L) >> 16;
            if (build >= 19041)
            {
                string[] labels = new[]
                {
                    $"{resourceLoader.GetString("Edition/Text")}",
                    $"{resourceLoader.GetString("Version/Text")}",
                    $"{resourceLoader.GetString("InstalledOn/Text")}",
                    $"{resourceLoader.GetString("OSBuild/Text")}",
                    $"{resourceLoader.GetString("Experience/Text")}",
                    "Kernel expiration"
                };

                int length = labels.Max(f => f.Length) + 4;

                labels = labels.Select(f => string.Format($"{{0,-{length}}}", f)).ToArray();

                targetText =
                    $@"{labels[0]}{Edition.Text}
{labels[1]}{Version.Text}
{labels[2]}{InstalledOn.Text}
{labels[3]}{Build.Text}
{labels[4]}{Experience.Text}
{labels[5]}{KernelExpiration.Text}";
            }
            else
            {
                string[] labels = new[]
                {
                    resourceLoader.GetString("Edition/Text"),
                    resourceLoader.GetString("Version/Text"),
                    resourceLoader.GetString("InstalledOn/Text"),
                    resourceLoader.GetString("OSBuild/Text")
                };

                int length = labels.Max(f => f.Length) + 4;

                labels = labels.Select(f => string.Format($"{{0,-{length}}}", f)).ToArray();

                targetText =
                    $@"{labels[0]}{Edition.Text}
{labels[1]}{Version.Text}
{labels[2]}{InstalledOn.Text}
{labels[3]}{Build.Text}";
            }

            package.SetText(targetText);

            Clipboard.SetContent(package);
            CopyButton.Content = resourceLoader.GetString("Copied");
            await Task.Delay(1000);
            CopyButton.Content = resourceLoader.GetString("CopyButton/Content");
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            string deviceFamilyVersion = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong version = ulong.Parse(deviceFamilyVersion);
            ulong build = (version & 0x00000000FFFF0000L) >> 16;
            var revision = version & 0x000000000000FFFF;

            Build.Text = build.ToString();

            if (revision != 0)
                Build.Text += $".{revision}";

            registry.InitNTDLLEntryPoints();
            var productName = WinverNative.Winbrand.BrandingFormatString("%WINDOWS_LONG%");

            if (build >= 21996)
                productName = productName.Replace("Windows 10", "Windows 11");

            Edition.Text = productName;
            LicensingText.Text = resourceLoader.GetString("Trademark/Text").Replace("Windows", productName);

            var displayVersion = ReturnValueFromRegistry(RegistryHive.HKEY_LOCAL_MACHINE, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "DisplayVersion");
            Version.Text = displayVersion;

            var date = GetWindowsInstallationDateTime().ToLocalTime();
            var userCulture = CultureInfoHelper.GetCurrentCulture();
            InstalledOn.Text = date.ToString("d", userCulture);

            var ownerName = ReturnValueFromRegistry(RegistryHive.HKEY_LOCAL_MACHINE, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "RegisteredOwner");
            OwnerText.Text = ownerName;
            OwnerText.Visibility = string.IsNullOrEmpty(ownerName) ? Visibility.Collapsed : Visibility.Visible;

            var ownerOrg = ReturnValueFromRegistry(RegistryHive.HKEY_LOCAL_MACHINE, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "RegisteredOrganization");
            OrgText.Text = ownerOrg;
            OrgText.Visibility = string.IsNullOrEmpty(ownerOrg) ? Visibility.Collapsed : Visibility.Visible;

            using (X509Certificate cert = X509Certificate.CreateFromSignedFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "ntdll.dll")))
            {
                using (X509Certificate2 cert2 = new X509Certificate2(cert))
                {
                    KernelExpiration.Text = cert2.NotAfter.ToString("d", userCulture);
                }
            }

            if (build < 19041)
            {
                ExperienceLabel.Visibility = Visibility.Collapsed;
                Experience.Visibility = Visibility.Collapsed;
                return;
            }

            registry.GetSubKeyList(RegistryHive.HKEY_LOCAL_MACHINE, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\InboxApplications", out string[] subKeys);
            string cbs = subKeys.First(f => f.StartsWith("MicrosoftWindows.Client.CBS_", StringComparison.CurrentCultureIgnoreCase));
            cbs = cbs.Split('_')[1];
            Experience.Text = $"{resourceLoader.GetString("ExperiencePack")} {cbs}";
        }

        private string ReturnValueFromRegistry(RegistryHive hive, string key, string value)
        {
            var success = registry.QueryValue(hive, key, value, out RegistryType _, out byte[] rawData);
            if (!success)
                return "";
            return Encoding.Unicode.GetString(rawData).Replace("\0", "");
        }

        private DateTime GetWindowsInstallationDateTime()
        {
            bool success = registry.QueryValue(RegistryHive.HKEY_LOCAL_MACHINE, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "InstallDate", out RegistryType _, out byte[] buffer);
            if (!success)
                throw new Exception();

            var seconds = BitConverter.ToInt32(buffer, 0);
            DateTime startDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime installDate = startDate.AddSeconds(seconds);
            return installDate;
        }
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using DialogHostAvalonia;

namespace PSAppDeployToolkitGui;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel != null)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Installer File",
                AllowMultiple = false
            });

            if (files.Any())
            {
                var installerPathTextBox = this.FindControl<TextBox>("InstallerPathTextBox");
                if (installerPathTextBox != null)
                {
                    installerPathTextBox.Text = files[0].TryGetLocalPath();
                }
            }
        }
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        var appName = this.FindControl<TextBox>("AppNameTextBox")?.Text;
        var appVersion = this.FindControl<TextBox>("AppVersionTextBox")?.Text;
        var publisher = this.FindControl<TextBox>("PublisherTextBox")?.Text;
        var installerPath = this.FindControl<TextBox>("InstallerPathTextBox")?.Text;
        var installCommand = this.FindControl<TextBox>("InstallCommandTextBox")?.Text;
        var uninstallCommand = this.FindControl<TextBox>("UninstallCommandTextBox")?.Text;

        if (string.IsNullOrEmpty(appName) || string.IsNullOrEmpty(installerPath))
        {
            await DialogHost.Show(new TextBlock { Text = "Application Name and Installer File are required." });
            return;
        }

        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Destination Folder"
        });

        if (folder.Any())
        {
            var destinationPath = folder[0].TryGetLocalPath();
            if (destinationPath == null) return;

            try
            {
                // Create directory structure
                var filesDir = System.IO.Path.Combine(destinationPath, "Files");
                var supportFilesDir = System.IO.Path.Combine(destinationPath, "SupportFiles");
                var toolkitDir = System.IO.Path.Combine(destinationPath, "AppDeployToolkit");
                System.IO.Directory.CreateDirectory(filesDir);
                System.IO.Directory.CreateDirectory(supportFilesDir);
                System.IO.Directory.CreateDirectory(toolkitDir);

                // Copy Toolkit files
                var sourceToolkitDir = "Toolkit"; // Assuming this is relative to the running app
                CopyDirectory(sourceToolkitDir, toolkitDir);

                // Copy installer file
                System.IO.File.Copy(installerPath, System.IO.Path.Combine(filesDir, System.IO.Path.GetFileName(installerPath)), true);

                // Read template and replace placeholders
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "PSAppDeployToolkitGui.Deploy-Application.ps1.template";
                string template;
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                using (var reader = new System.IO.StreamReader(stream))
                {
                    template = await reader.ReadToEndAsync();
                }
                template = template.Replace("[[appName]]", appName);
                template = template.Replace("[[appVersion]]", appVersion);
                template = template.Replace("[[appVendor]]", publisher);
                template = template.Replace("[[installCommand]]", installCommand);
                template = template.Replace("[[uninstallCommand]]", uninstallCommand);

                // Write the new script
                await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(destinationPath, "Deploy-Application.ps1"), template);

                await DialogHost.Show(new TextBlock { Text = "Package generated successfully!" });
            }
            catch (System.Exception ex)
            {
                await DialogHost.Show(new TextBlock { Text = $"An error occurred: {ex.Message}" });
            }
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new System.IO.DirectoryInfo(sourceDir);
        if (!dir.Exists)
            throw new System.IO.DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        System.IO.Directory.CreateDirectory(destinationDir);

        foreach (System.IO.FileInfo file in dir.GetFiles())
        {
            string targetFilePath = System.IO.Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        foreach (System.IO.DirectoryInfo subDir in dir.GetDirectories())
        {
            string newDestinationDir = System.IO.Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }
}
using System;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PD2ModelParser.UI
{
    public partial class HashPanel : UserControl
    {
        private readonly string localHashlistPath = Path.Combine(AppContext.BaseDirectory, "hashlist");

        public HashPanel()
        {
            InitializeComponent();

            // Wire up events to the designer controls
            this.Load += async (_, __) => await OnLoadAsync();
            updateButton.Click += async (_, __) => await FetchButtonClickedAsync();
        }

        private async Task OnLoadAsync()
        {
            try
            {
                if (File.Exists(localHashlistPath))
                {
                    var info = new FileInfo(localHashlistPath);
                }

                if (checkBox.Checked)
                {
                    bool need = true;
                    if (File.Exists(localHashlistPath))
                    {
                        var ageDays = (DateTime.UtcNow - File.GetLastWriteTimeUtc(localHashlistPath)).TotalDays;
                        need = ageDays > 60;
                    }
                    if (need)
                    {
                        var url = textBox1.Text?.Trim();
                        if (!string.IsNullOrEmpty(url))
                        {
                            await FetchAndSaveHashlist(url);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Auto-check failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task FetchButtonClickedAsync()
        {

            var url = textBox1.Text?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show(this, "Please enter a URL to fetch the hashlist from.", "No URL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            updateButton.Enabled = false;
            try
            {
                await FetchAndSaveHashlist(url);
            }
            finally
            {
                updateButton.Enabled = true;
            }
        }

        private async Task FetchAndSaveHashlist(string url)
        {
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(30);
                var resp = await http.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                var content = await resp.Content.ReadAsStringAsync();

                var dir = Path.GetDirectoryName(localHashlistPath);
                // If a regular file exists where we expect the directory, remove it so we can create the directory.
                if (File.Exists(dir) && !Directory.Exists(dir))
                {
                    try { File.Delete(dir); } catch { }
                }
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                // Write/overwrite the target file
                await File.WriteAllTextAsync(localHashlistPath, content);

                MessageBox.Show(this, "Hashlist downloaded and saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to download hashlist: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

namespace LosaBrowser
{
    /// <summary>
    /// Single-file WinForms browser with a local-only Extensions system.
    /// - No database or network required for extensions.
    /// - Upload extension files (any text-based language) into ./extensions.
    /// - Activate an extension to replace the marked region in a target .cs file.
    /// - Backups are created automatically before any replacement.
    /// 
    /// Safety notes:
    /// - Only install extensions you trust. This tool writes source files.
    /// - The code performs a basic braces balance check before writing.
    /// - The replace region is delimited by markers:
    ///     // &lt;EXTENSIONS_START&gt;
    ///     // &lt;EXTENSIONS_END&gt;
    ///   Keep those markers in the target file you want updated.
    /// </summary>
    public class Browser : Form
    {
        WebView2 view;
        TextBox bar;
        Panel sidebar;
        bool ready = false;

        List<string> history = new List<string>();
        List<string> bookmarks = new List<string>();
        List<string> downloads = new List<string>();

        // Local extensions folder (relative to executable)
        readonly string ExtensionsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extensions");
        readonly string ExtensionsMetaFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extensions", "extensions.json");

        string bookmarksFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bookmarks.json");
        string historyFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.json");
        string downloadsFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads.json");

        FormWindowState previousState = FormWindowState.Normal;
        bool previousTopMost = false;

        public Browser()
        {
            Width = 1100;
            Height = 700;
            Text = "Losa Browser";

            Directory.CreateDirectory(ExtensionsFolder);
            EnsureExtensionsMeta();

            LoadData();

            bar = new TextBox() { Dock = DockStyle.Top, Height = 30 };
            bar.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && ready)
                    Navigate(bar.Text);

                if (e.Control && e.KeyCode == Keys.D && ready)
                {
                    var current = view.CoreWebView2?.Source;
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        AddBookmark(current);
                        MessageBox.Show("Added to bookmarks:\n" + current, "Losa Browser");
                    }
                }
            };

            sidebar = new Panel()
            {
                Dock = DockStyle.Left,
                Width = 60,
                BackColor = System.Drawing.Color.FromArgb(40, 40, 40)
            };

            AddSidebarButton("🏠", "losa://home");
            AddSidebarButton("⚙", "losa://settings");
            AddSidebarButton("★", "losa://bookmarks");
            AddSidebarButton("🕘", "losa://history");
            AddSidebarButton("⬇", "losa://downloads");
            AddSidebarButton("📊", "losa://stats");
            AddSidebarButton("🔌", "losa://extensions");
            AddSidebarButton("⬆", "losa://upload-extension");
            AddSidebarButton("ℹ", "losa://about");

            view = new WebView2() { Dock = DockStyle.Fill };

            Controls.Add(view);
            Controls.Add(sidebar);
            Controls.Add(bar);

            view.CoreWebView2InitializationCompleted += async (s, e) =>
            {
                ready = true;

                view.CoreWebView2.ContainsFullScreenElementChanged += CoreWebView2_ContainsFullScreenElementChanged;
                view.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                view.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;

                view.CoreWebView2.NavigateToString(HomePage());
            };

            _ = view.EnsureCoreWebView2Async();
        }

        void EnsureExtensionsMeta()
        {
            try
            {
                if (!File.Exists(ExtensionsMetaFile))
                {
                    var list = new List<ExtensionMeta>();
                    File.WriteAllText(ExtensionsMetaFile, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch { }
        }

        void CoreWebView2_ContainsFullScreenElementChanged(object sender, object e)
        {
            if (view.CoreWebView2.ContainsFullScreenElement)
            {
                previousState = WindowState;
                previousTopMost = TopMost;

                WindowState = FormWindowState.Maximized;
                TopMost = true;
            }
            else
            {
                WindowState = previousState;
                TopMost = previousTopMost;
            }
        }

        void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess || !ready) return;

            var src = view.CoreWebView2.Source;
            if (!string.IsNullOrWhiteSpace(src))
            {
                bar.Text = src;
                RecordHistory(src);
            }
        }

        void CoreWebView2_DownloadStarting(object sender, CoreWebView2DownloadStartingEventArgs e)
        {
            string info = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {e.DownloadOperation.Uri} -> {e.ResultFilePath}";
            downloads.Add(info);
            SaveData();
        }

        void AddSidebarButton(string icon, string target)
        {
            Button btn = new Button()
            {
                Text = icon,
                Width = 60,
                Height = 60,
                FlatStyle = FlatStyle.Flat,
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.FromArgb(50, 50, 50),
                Font = new System.Drawing.Font("Segoe UI Emoji", 20),
                Dock = DockStyle.Top
            };

            btn.FlatAppearance.BorderSize = 0;

            btn.Click += (s, e) =>
            {
                if (ready)
                    Navigate(target);
            };

            sidebar.Controls.Add(btn);
            sidebar.Controls.SetChildIndex(btn, 0);
        }

        void Navigate(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            // Internal routes
            if (url.StartsWith("losa://home"))
            {
                view.CoreWebView2.NavigateToString(HomePage());
                return;
            }

            if (url.StartsWith("losa://settings"))
            {
                view.CoreWebView2.NavigateToString(SettingsPage());
                return;
            }

            if (url.StartsWith("losa://bookmarks"))
            {
                view.CoreWebView2.NavigateToString(BookmarksPage());
                return;
            }

            if (url.StartsWith("losa://history"))
            {
                view.CoreWebView2.NavigateToString(HistoryPage());
                return;
            }

            if (url.StartsWith("losa://downloads"))
            {
                view.CoreWebView2.NavigateToString(DownloadsPage());
                return;
            }

            if (url.StartsWith("losa://extensions"))
            {
                view.CoreWebView2.NavigateToString(ExtensionsPage());
                return;
            }

            if (url.StartsWith("losa://upload-extension"))
            {
                // Open upload dialog
                UploadExtensionDialog();
                return;
            }

            if (url.StartsWith("losa://run-extension"))
            {
                var q = new Uri(url);
                var query = System.Web.HttpUtility.ParseQueryString(q.Query);
                var name = query.Get("name") ?? "";
                if (!string.IsNullOrWhiteSpace(name))
                {
                    RunExtension(name);
                }
                return;
            }

            // If it's a simple search term, go to search engine
            if (!url.Contains("://") && !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                string searchUrl = "https://losabrowser.share.zrok.io/search?q=" + Uri.EscapeDataString(url);
                view.CoreWebView2.Navigate(searchUrl);
                return;
            }

            // Default: navigate normally
            view.CoreWebView2.Navigate(url);
        }

        void RecordHistory(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            history.Add(url);
            SaveData();
        }

        void AddBookmark(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            if (!bookmarks.Contains(url))
            {
                bookmarks.Add(url);
                SaveData();
            }
        }

        void LoadData()
        {
            try
            {
                if (File.Exists(bookmarksFile))
                {
                    var json = File.ReadAllText(bookmarksFile);
                    var list = JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null) bookmarks = list;
                }

                if (File.Exists(historyFile))
                {
                    var json = File.ReadAllText(historyFile);
                    var list = JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null) history = list;
                }

                if (File.Exists(downloadsFile))
                {
                    var json = File.ReadAllText(downloadsFile);
                    var list = JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null) downloads = list;
                }
            }
            catch { }
        }

        void SaveData()
        {
            try
            {
                File.WriteAllText(bookmarksFile, JsonSerializer.Serialize(bookmarks));
                File.WriteAllText(historyFile, JsonSerializer.Serialize(history));
                File.WriteAllText(downloadsFile, JsonSerializer.Serialize(downloads));
            }
            catch { }
        }

        // -------------------------
        // UI pages (HTML)
        // -------------------------
        string HomePage()
        {
            return @"
<html>
<body style='margin:0;padding:0;font-family:Segoe UI, Arial;background:#0f0f0f;color:white;'>

    <div style='padding:28px 20px 10px 20px;text-align:center;'>
        <h1 style='margin:0;font-size:36px;'>Losa Browser</h1>
        <p style='opacity:0.75;margin-top:6px;'>Local Extensions Manager (no DB)</p>
    </div>

    <div style='display:flex;justify-content:center;margin-top:28px;'>
        <div style='width:70%;max-width:900px;'>
            <div style='display:flex;gap:10px;align-items:center;'>
                <input id='searchBox' placeholder='Search with Losa SearXNG...' 
                    style='flex:1;padding:14px;border-radius:10px;border:1px solid #222;background:#1b1b1b;color:white;font-size:16px;'>
                <button onclick='doSearch()' style='padding:12px 18px;border-radius:10px;border:none;background:#4af;color:black;font-weight:600;font-size:16px;'>
                    Search
                </button>
            </div>

            <div style='display:flex;gap:18px;justify-content:center;margin-top:30px;flex-wrap:wrap;'>
                <a href='losa://extensions' style='display:block;background:#1b1b1b;padding:18px;border-radius:12px;width:220px;text-align:center;text-decoration:none;color:white;font-size:16px;'>🔌 Manage Extensions</a>
                <a href='losa://upload-extension' style='display:block;background:#1b1b1b;padding:18px;border-radius:12px;width:220px;text-align:center;text-decoration:none;color:white;font-size:16px;'>⬆ Upload Extension</a>
            </div>
        </div>
    </div>

    <script>
        function doSearch() {
            var q = document.getElementById('searchBox').value || '';
            if (!q) return;
            window.location.href = 'https://losabrowser.share.zrok.io/search?q=' + encodeURIComponent(q);
        }
        document.getElementById('searchBox').addEventListener('keydown', function(e){
            if (e.key === 'Enter') doSearch();
        });
    </script>
</body>
</html>";
        }

        string SettingsPage() =>
            @"<html><body style='background:#222;color:white;font-family:Segoe UI;padding:20px;'>
                <h1>Settings</h1>
                <p>Local-only extensions. No database used.</p>
                <a href='losa://home' style='color:#4af;'>Back</a>
            </body></html>";

        string BookmarksPage()
        {
            var html = @"<html><body style='background:#222;color:white;font-family:Segoe UI;padding:20px;'>
                <h1>Bookmarks</h1>";

            if (bookmarks.Count == 0)
            {
                html += "<p>No bookmarks yet. Press <b>Ctrl+D</b> in the URL bar to add the current page.</p>";
            }
            else
            {
                html += "<ul>";
                foreach (var b in bookmarks)
                {
                    html += $"<li><a href='{WebUtility.HtmlEncode(b)}' style='color:#4af;'>{WebUtility.HtmlEncode(b)}</a></li>";
                }
                html += "</ul>";
            }

            html += "<a href='losa://home' style='color:#4af;'>Back</a></body></html>";
            return html;
        }

        string HistoryPage()
        {
            var html = @"<html><body style='background:#222;color:white;font-family:Segoe UI;padding:20px;'>
                <h1>History</h1>";

            if (history.Count == 0)
            {
                html += "<p>No history yet.</p>";
            }
            else
            {
                html += "<ul>";
                foreach (var h in history)
                {
                    html += $"<li><a href='{WebUtility.HtmlEncode(h)}' style='color:#4af;'>{WebUtility.HtmlEncode(h)}</a></li>";
                }
                html += "</ul>";
            }

            html += "<a href='losa://home' style='color:#4af;'>Back</a></body></html>";
            return html;
        }

        string DownloadsPage()
        {
            var html = @"<html><body style='background:#222;color:white;font-family:Segoe UI;padding:20px;'>
                <h1>Downloads</h1>";

            if (downloads.Count == 0)
            {
                html += "<p>No downloads yet.</p>";
            }
            else
            {
                html += "<ul>";
                foreach (var d in downloads)
                {
                    html += $"<li>{WebUtility.HtmlEncode(d)}</li>";
                }
                html += "</ul>";
            }

            html += "<a href='losa://home' style='color:#4af;'>Back</a></body></html>";
            return html;
        }

        string AboutPage() =>
            @"<html><body style='background:#111;color:white;font-family:Segoe UI;padding:20px;'>
                <h1>About Losa Browser</h1>
                <p>Local Extensions Manager</p>
                <p>Created by Lampros</p>
                <a href='losa://home' style='color:#4af;'>Back</a>
            </body></html>";

        // -------------------------
        // Extensions (local folder)
        // -------------------------
        string ExtensionsPage()
        {
            var html = @"<html><body style='background:#111;color:white;font-family:Segoe UI;padding:20px;'>
                <h1>Extensions</h1>
                <p>Loaded from local folder: <b>./extensions</b></p>
                <p>Use the Upload button to add an extension file (any text-based language). Click Install/Update to apply it to a target source file.</p>";

            try
            {
                var metas = LoadExtensionsMeta();

                html += "<table style='width:100%;border-collapse:collapse;margin-top:10px;font-size:14px;'>";
                html += "<tr style='background:#222;'><th style='padding:6px;border-bottom:1px solid #333;'>File</th><th style='padding:6px;border-bottom:1px solid #333;'>Language</th><th style='padding:6px;border-bottom:1px solid #333;'>Size</th><th style='padding:6px;border-bottom:1px solid #333;'>Uploaded</th><th style='padding:6px;border-bottom:1px solid #333;'>Action</th></tr>";

                if (metas.Count == 0)
                {
                    html += "<tr><td colspan='5' style='padding:10px;'>No extensions uploaded yet.</td></tr>";
                }
                else
                {
                    foreach (var m in metas.OrderByDescending(x => x.UploadedAt))
                    {
                        var encoded = WebUtility.UrlEncode(m.FileName);
                        html += "<tr>";
                        html += $"<td style='padding:6px;border-bottom:1px solid #333;'>{WebUtility.HtmlEncode(m.FileName)}</td>";
                        html += $"<td style='padding:6px;border-bottom:1px solid #333;'>{WebUtility.HtmlEncode(m.Language)}</td>";
                        html += $"<td style='padding:6px;border-bottom:1px solid #333;'>{m.SizeHuman}</td>";
                        html += $"<td style='padding:6px;border-bottom:1px solid #333;'>{m.UploadedAt:yyyy-MM-dd HH:mm}</td>";
                        html += $"<td style='padding:6px;border-bottom:1px solid #333;'><a href='losa://run-extension?name={encoded}' style='color:#4af;'>Install/Update</a></td>";
                        html += "</tr>";
                    }
                }

                html += "</table>";
            }
            catch (Exception ex)
            {
                html += $"<p style='color:#f66;'>Error reading extensions: {WebUtility.HtmlEncode(ex.Message)}</p>";
            }

            html += "<div style='margin-top:18px;'><a href='losa://upload-extension' style='color:#4af;'>Upload Extension</a></div>";
            html += "<a href='losa://home' style='color:#4af;display:block;margin-top:15px;'>Back</a></body></html>";
            return html;
        }

        // -------------------------
        // Upload extension (file dialog)
        // -------------------------
        void UploadExtensionDialog()
        {
            this.Invoke(() =>
            {
                using var dlg = new OpenFileDialog();
                dlg.Title = "Select extension file to upload (text-based)";
                dlg.Filter = "All files (*.*)|*.*";
                dlg.Multiselect = false;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var src = dlg.FileName;
                    var destName = Path.GetFileName(src);
                    var destPath = Path.Combine(ExtensionsFolder, destName);

                    // If file exists, create a timestamped copy
                    if (File.Exists(destPath))
                    {
                        var backup = destPath + $".old.{DateTime.Now:yyyyMMddHHmmss}";
                        File.Copy(destPath, backup, overwrite: true);
                    }

                    File.Copy(src, destPath, overwrite: true);

                    var content = File.ReadAllText(destPath);
                    var meta = new ExtensionMeta
                    {
                        FileName = destName,
                        Language = DetectLanguageFromFileName(destName, content),
                        SizeBytes = new FileInfo(destPath).Length,
                        UploadedAt = DateTime.Now
                    };

                    var metas = LoadExtensionsMeta();
                    // replace or add
                    metas.RemoveAll(x => x.FileName.Equals(meta.FileName, StringComparison.OrdinalIgnoreCase));
                    metas.Add(meta);
                    File.WriteAllText(ExtensionsMetaFile, JsonSerializer.Serialize(metas, new JsonSerializerOptions { WriteIndented = true }));

                    MessageBox.Show($"Uploaded extension: {meta.FileName}\nLanguage: {meta.Language}\nSize: {meta.SizeHuman}", "Losa Browser");
                    view.CoreWebView2.NavigateToString(ExtensionsPage());
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Upload failed: " + ex.Message, "Losa Browser");
                }
            });
        }

        // -------------------------
        // Run/Install extension (replace marked region in target .cs file)
        // -------------------------
        void RunExtension(string fileName)
        {
            Task.Run(() =>
            {
                try
                {
                    var metas = LoadExtensionsMeta();
                    var meta = metas.FirstOrDefault(m => m.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                    if (meta == null)
                    {
                        this.Invoke(() => MessageBox.Show($"Extension not found: {fileName}", "Losa Browser"));
                        return;
                    }

                    var extPath = Path.Combine(ExtensionsFolder, meta.FileName);
                    if (!File.Exists(extPath))
                    {
                        this.Invoke(() => MessageBox.Show($"Extension file missing: {extPath}", "Losa Browser"));
                        return;
                    }

                    var fetchedCode = File.ReadAllText(extPath);

                    // Ask user for target file to update
                    string targetFile = null;
                    this.Invoke(() =>
                    {
                        using var dlg = new OpenFileDialog();
                        dlg.Title = "Select target .cs file to apply extension (must contain markers)";
                        dlg.Filter = "C# files (*.cs)|*.cs|All files (*.*)|*.*";
                        dlg.Multiselect = false;
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                        {
                            targetFile = dlg.FileName;
                        }
                    });

                    if (string.IsNullOrWhiteSpace(targetFile) || !File.Exists(targetFile))
                    {
                        this.Invoke(() => MessageBox.Show("No valid target file selected. Aborting.", "Losa Browser"));
                        return;
                    }

                    const string startMarker = "// <EXTENSIONS_START>";
                    const string endMarker = "// <EXTENSIONS_END>";

                    string original = File.ReadAllText(targetFile);
                    int startIdx = original.IndexOf(startMarker, StringComparison.Ordinal);
                    int endIdx = original.IndexOf(endMarker, StringComparison.Ordinal);

                    if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx)
                    {
                        this.Invoke(() => MessageBox.Show($"Markers not found or malformed in {targetFile}.\nAdd {startMarker} and {endMarker} to the file.", "Losa Browser"));
                        return;
                    }

                    // Basic safety: check braces balance in fetched code
                    if (!BracesBalanced(fetchedCode))
                    {
                        this.Invoke(() => MessageBox.Show("Fetched code appears to have unbalanced braces. Aborting update to avoid breaking the project.", "Losa Browser"));
                        return;
                    }

                    // Build new content (keep markers themselves)
                    int replaceFrom = startIdx + startMarker.Length;
                    string before = original.Substring(0, replaceFrom);
                    string after = original.Substring(endIdx);

                    // Normalize line endings
                    string normalizedCode = fetchedCode.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);

                    string newContent = before + Environment.NewLine + normalizedCode + Environment.NewLine + after;

                    // Backup original
                    string backupPath = targetFile + $".backup.{DateTime.Now:yyyyMMddHHmmss}";
                    File.Copy(targetFile, backupPath, overwrite: false);

                    // Write new content
                    File.WriteAllText(targetFile, newContent);

                    this.Invoke(() =>
                    {
                        MessageBox.Show($"Updated {Path.GetFileName(targetFile)} from extension '{meta.FileName}'.\nBackup created: {Path.GetFileName(backupPath)}\n\nNow rebuild your project (dotnet build -c Release).", "Losa Browser");
                    });
                }
                catch (Exception ex)
                {
                    this.Invoke(() => MessageBox.Show("Failed to apply extension: " + ex.Message, "Losa Browser"));
                }
            });
        }

        // -------------------------
        // Helpers
        // -------------------------
        List<ExtensionMeta> LoadExtensionsMeta()
        {
            try
            {
                if (!File.Exists(ExtensionsMetaFile))
                    return new List<ExtensionMeta>();

                var json = File.ReadAllText(ExtensionsMetaFile);
                var list = JsonSerializer.Deserialize<List<ExtensionMeta>>(json);
                return list ?? new List<ExtensionMeta>();
            }
            catch
            {
                return new List<ExtensionMeta>();
            }
        }

        static bool BracesBalanced(string code)
        {
            int paren = 0, brace = 0, bracket = 0;
            foreach (char c in code)
            {
                if (c == '(') paren++;
                else if (c == ')') paren--;
                else if (c == '{') brace++;
                else if (c == '}') brace--;
                else if (c == '[') bracket++;
                else if (c == ']') bracket--;
                if (paren < 0 || brace < 0 || bracket < 0) return false;
            }
            return paren == 0 && brace == 0 && bracket == 0;
        }

        static string DetectLanguageFromFileName(string fileName, string content)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext == ".cs") return "C#";
            if (ext == ".js") return "JavaScript";
            if (ext == ".ts") return "TypeScript";
            if (ext == ".py") return "Python";
            if (ext == ".java") return "Java";
            if (ext == ".rb") return "Ruby";
            if (ext == ".go") return "Go";
            if (ext == ".sh") return "Shell";
            if (ext == ".ps1") return "PowerShell";
            if (ext == ".json") return "JSON";
            if (ext == ".xml") return "XML";
            // fallback: inspect content for shebang or common tokens
            if (content.StartsWith("#!")) return "Script";
            if (content.Contains("using System") || content.Contains("namespace ")) return "C#";
            if (content.Contains("def ") && content.Contains(":")) return "Python";
            return "Text";
        }

        class ExtensionMeta
        {
            public string FileName { get; set; }
            public string Language { get; set; }
            public long SizeBytes { get; set; }
            public DateTime UploadedAt { get; set; }

            public string SizeHuman
            {
                get
                {
                    if (SizeBytes < 1024) return $"{SizeBytes} B";
                    if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:F1} KB";
                    return $"{SizeBytes / (1024.0 * 1024.0):F2} MB";
                }
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Browser());
        }
    }
}

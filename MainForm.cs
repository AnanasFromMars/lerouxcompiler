using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LerouxCompiler;

public class MainForm : Form
{
    // Compile tab controls
    private TabControl   tabControl          = null!;
    private TextBox      txtInputFolder      = null!;
    private TextBox      txtOutputFolder     = null!;
    private RadioButton  rbSingle            = null!;
    private RadioButton  rbMultiple          = null!;
    private CheckBox     chkCopyOutput       = null!;
    private DataGridView modelGrid           = null!;
    private Button       btnScan             = null!;
    private Button       btnAdd              = null!;
    private Button       btnRemove           = null!;
    private Button       btnCompileSelected  = null!;
    private Button       btnCompileAll       = null!;
    private Button       btnViewHlmv         = null!;
    private RadioButton  rbCopy              = null!;
    private RadioButton  rbMove              = null!;
    private CheckBox     chkCopyMaterials    = null!;
    private RichTextBox  outputBox           = null!;

    // Settings tab controls
    private TextBox txtStudiomdl = null!;
    private TextBox txtHlmv      = null!;
    private TextBox txtGame      = null!;

    private AppSettings settings    = new();
    private bool        isCompiling = false;

    public MainForm()
    {
        BuildUI();
        LoadSettings();
        PopulateControls();
    }

    // =========================================================================
    // UI BUILD
    // =========================================================================

    private void BuildUI()
    {
        Text          = "Leroux Model Compiler";
        Size          = new Size(980, 720);
        MinimumSize   = new Size(820, 580);
        StartPosition = FormStartPosition.CenterScreen;
        Font          = new Font("Segoe UI", 9f);

        tabControl = new TabControl { Dock = DockStyle.Fill };
        var tabCompile  = new TabPage("  Compile  ");
        var tabSettings = new TabPage("  Settings  ");
        tabControl.TabPages.AddRange(new[] { tabCompile, tabSettings });

        BuildCompileTab(tabCompile);
        BuildSettingsTab(tabSettings);
        Controls.Add(tabControl);
    }

    private void BuildCompileTab(TabPage tab)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(8),
            RowCount = 5, ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // input bar
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // toolbar
        root.RowStyles.Add(new RowStyle(SizeType.Percent,  40)); // grid
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // output bar
        root.RowStyles.Add(new RowStyle(SizeType.Percent,  60)); // log

        root.Controls.Add(BuildInputBar(),   0, 0);
        root.Controls.Add(BuildToolbar(),    0, 1);
        root.Controls.Add(BuildGrid(),       0, 2);
        root.Controls.Add(BuildOutputBar(),  0, 3);
        root.Controls.Add(BuildLog(),        0, 4);

        tab.Controls.Add(root);
    }

    private Panel BuildInputBar()
    {
        var p = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, Padding = new Padding(0, 4, 0, 0)
        };

        p.Controls.Add(new Label { Text = "Input folder:", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 4, 0) });

        txtInputFolder = new TextBox { Width = 360, Height = 24 };
        p.Controls.Add(txtInputFolder);

        var btnBrowseInput = MakeSmallButton("Browse...");
        btnBrowseInput.Click += BtnBrowseInput_Click;
        p.Controls.Add(btnBrowseInput);

        btnScan = MakeSmallButton("⟳ Scan for QC files", Color.FromArgb(70, 130, 180));
        btnScan.Click += BtnScan_Click;
        p.Controls.Add(btnScan);

        // Mode toggle — right-aligned via a filler
        var filler = new Panel { AutoSize = false, Width = 30, Height = 1 };
        p.Controls.Add(filler);

        p.Controls.Add(new Label { Text = "Mode:", AutoSize = true, Padding = new Padding(0, 6, 4, 0) });
        rbSingle   = new RadioButton { Text = "Single",   AutoSize = true, Padding = new Padding(0, 4, 8, 0) };
        rbMultiple = new RadioButton { Text = "Multiple", AutoSize = true, Checked = true, Padding = new Padding(0, 4, 0, 0) };
        rbSingle.CheckedChanged   += ModeChanged;
        rbMultiple.CheckedChanged += ModeChanged;
        p.Controls.Add(rbSingle);
        p.Controls.Add(rbMultiple);

        return p;
    }

    private Panel BuildToolbar()
    {
        var p = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false
        };

        btnAdd    = MakeButton("+ Add QC",  Color.FromArgb(70, 130, 180));
        btnRemove = MakeButton("− Remove",   Color.FromArgb(150, 60, 60));
        btnAdd.Click    += BtnAdd_Click;
        btnRemove.Click += BtnRemove_Click;

        btnCompileSelected = MakeButton("▶  Compile Selected", Color.FromArgb(40, 100, 160));
        btnCompileAll      = MakeButton("▶▶ Compile All",      Color.FromArgb(34, 139, 34));
        btnViewHlmv        = MakeButton("👁 View in HLMV",      Color.FromArgb(100, 60, 140));
        btnCompileSelected.Click += async (s, e) => await CompileSelected();
        btnCompileAll.Click      += async (s, e) => await CompileAll();
        btnViewHlmv.Click        += BtnViewHlmv_Click;

        p.Controls.AddRange(new Control[] { btnAdd, btnRemove, btnCompileSelected, btnCompileAll, btnViewHlmv });
        return p;
    }

    private GroupBox BuildGrid()
    {
        var group = new GroupBox { Text = "Models", Dock = DockStyle.Fill, Padding = new Padding(4) };

        modelGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect           = true,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible     = false,
            BackgroundColor       = Color.White,
            BorderStyle           = BorderStyle.None,
            GridColor             = Color.FromArgb(220, 220, 220),
            AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight   = 26
        };
        modelGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(173, 214, 255);
        modelGrid.DefaultCellStyle.SelectionForeColor = Color.Black;

        var colCheck = new DataGridViewCheckBoxColumn { HeaderText = "", Width = 32, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Name = "colCheck" };
        var colName  = new DataGridViewTextBoxColumn  { HeaderText = "Name",    FillWeight = 25, Name = "colName", ReadOnly = true };
        var colQC    = new DataGridViewTextBoxColumn  { HeaderText = "QC Path", FillWeight = 75, Name = "colQC",   ReadOnly = true };
        modelGrid.Columns.AddRange(colCheck, colName, colQC);

        group.Controls.Add(modelGrid);
        return group;
    }

    private Panel BuildOutputBar()
    {
        var p = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, Padding = new Padding(0, 4, 0, 0)
        };

        chkCopyOutput = new CheckBox { Text = "Send compiled models to:", AutoSize = true, Padding = new Padding(0, 5, 4, 0) };
        chkCopyOutput.CheckedChanged += (s, e) =>
        {
            txtOutputFolder.Enabled = chkCopyOutput.Checked;
            rbCopy.Enabled = chkCopyOutput.Checked;
            rbMove.Enabled = chkCopyOutput.Checked;
        };
        p.Controls.Add(chkCopyOutput);

        txtOutputFolder = new TextBox { Width = 300, Height = 24, Enabled = false };
        p.Controls.Add(txtOutputFolder);

        var btnBrowseOut = MakeSmallButton("Browse...");
        btnBrowseOut.Click += BtnBrowseOutput_Click;
        p.Controls.Add(btnBrowseOut);

        var filler = new Panel { Width = 12, Height = 1 };
        p.Controls.Add(filler);

        rbCopy = new RadioButton { Text = "Copy", AutoSize = true, Checked = true, Enabled = false, Padding = new Padding(0, 4, 6, 0) };
        rbMove = new RadioButton { Text = "Move (cut)", AutoSize = true, Enabled = false, Padding = new Padding(0, 4, 0, 0) };
        p.Controls.Add(rbCopy);
        p.Controls.Add(rbMove);

        var filler2 = new Panel { Width = 20, Height = 1 };
        p.Controls.Add(filler2);

        chkCopyMaterials = new CheckBox
        {
            Text    = "Copy VMT/VTF to game materials folder",
            AutoSize = true,
            Checked  = true,
            Padding  = new Padding(0, 5, 0, 0)
        };
        p.Controls.Add(chkCopyMaterials);

        return p;
    }

    private GroupBox BuildLog()
    {
        var group = new GroupBox { Text = "Output", Dock = DockStyle.Fill, Padding = new Padding(4) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        outputBox = new RichTextBox
        {
            Dock       = DockStyle.Fill, ReadOnly  = true,
            BackColor  = Color.FromArgb(20, 20, 20), ForeColor = Color.FromArgb(204, 204, 204),
            Font       = new Font("Consolas", 9f), WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both, BorderStyle = BorderStyle.None
        };

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var btnClear = new Button { Text = "Clear", AutoSize = true, Height = 24 };
        btnClear.Click += (s, e) => outputBox.Clear();
        bottom.Controls.Add(btnClear);

        layout.Controls.Add(outputBox, 0, 0);
        layout.Controls.Add(bottom,    0, 1);
        group.Controls.Add(layout);
        return group;
    }

    private void BuildSettingsTab(TabPage tab)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(20, 16, 20, 16),
            ColumnCount = 3, RowCount = 5
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label { Text = "studiomdl.exe:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
        txtStudiomdl = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(txtStudiomdl, 1, 0);
        var btnBrowseSmd = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        btnBrowseSmd.Click += BtnBrowseStudiomdl_Click;
        panel.Controls.Add(btnBrowseSmd, 2, 0);

        panel.Controls.Add(new Label { Text = "hlmv.exe:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 1);
        txtHlmv = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(txtHlmv, 1, 1);
        var btnBrowseHlmv = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        btnBrowseHlmv.Click += BtnBrowseHlmv_Click;
        panel.Controls.Add(btnBrowseHlmv, 2, 1);

        panel.Controls.Add(new Label { Text = "-game path:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 2);
        txtGame = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(txtGame, 1, 2);
        var btnBrowseGame = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        btnBrowseGame.Click += BtnBrowseGame_Click;
        panel.Controls.Add(btnBrowseGame, 2, 2);

        var btnSave = MakeButton("Save Settings", Color.FromArgb(34, 139, 34));
        btnSave.Click += BtnSaveSettings_Click;
        panel.Controls.Add(btnSave, 0, 3);
        panel.SetColumnSpan(btnSave, 3);

        var hint = new Label
        {
            Text = "studiomdl.exe and hlmv.exe should be copied to csgo legacy\\bin so they find all required DLLs.\n" +
                   "The -game path is the 'csgo' folder inside your csgo legacy install.",
            Dock = DockStyle.Fill, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8.5f)
        };
        panel.Controls.Add(hint, 0, 4);
        panel.SetColumnSpan(hint, 3);

        tab.Controls.Add(panel);
    }

    // =========================================================================
    // EVENT HANDLERS
    // =========================================================================

    private void ModeChanged(object? sender, EventArgs e)
    {
        bool multi = rbMultiple.Checked;
        modelGrid.MultiSelect    = multi;
        modelGrid.Columns["colCheck"]!.Visible = multi;
    }

    private void BtnBrowseInput_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "Select folder containing QC files" };
        if (dlg.ShowDialog() == DialogResult.OK)
            txtInputFolder.Text = dlg.SelectedPath;
    }

    private void BtnScan_Click(object? sender, EventArgs e)
    {
        var folder = txtInputFolder.Text.Trim();
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            MessageBox.Show("Set a valid input folder first.", "Leroux Compiler");
            return;
        }

        var qcFiles = Directory.GetFiles(folder, "*.qc", SearchOption.AllDirectories);
        if (qcFiles.Length == 0)
        {
            MessageBox.Show("No .qc files found in that folder.", "Leroux Compiler");
            return;
        }

        // Skip duplicates already in the list
        var existing = modelGrid.Rows.Cast<DataGridViewRow>()
                                     .Select(r => r.Cells["colQC"].Value?.ToString() ?? "")
                                     .ToHashSet(StringComparer.OrdinalIgnoreCase);
        int added = 0;
        foreach (var qc in qcFiles)
        {
            if (existing.Contains(qc)) continue;
            var name = Path.GetFileNameWithoutExtension(qc);
            modelGrid.Rows.Add(true, name, qc);
            settings.Models.Add(new ModelEntry { Name = name, QcPath = qc });
            added++;
        }

        SaveSettings();
        AppendLine($"Scanned '{folder}' — added {added} QC file(s), skipped {qcFiles.Length - added} duplicate(s).", Color.Cyan);
    }

    private void BtnBrowseOutput_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "Select folder to copy compiled models into" };
        if (dlg.ShowDialog() == DialogResult.OK)
            txtOutputFolder.Text = dlg.SelectedPath;
    }

    private void BtnBrowseStudiomdl_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Filter = "studiomdl.exe|studiomdl.exe|All files|*.*", Title = "Select studiomdl.exe" };
        if (dlg.ShowDialog() == DialogResult.OK) txtStudiomdl.Text = dlg.FileName;
    }

    private void BtnBrowseHlmv_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Filter = "hlmv.exe|hlmv.exe|All files|*.*", Title = "Select hlmv.exe" };
        if (dlg.ShowDialog() == DialogResult.OK) txtHlmv.Text = dlg.FileName;
    }

    private void BtnViewHlmv_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(settings.HlmvPath) || !File.Exists(settings.HlmvPath))
        {
            MessageBox.Show("hlmv.exe path is not set.\nGo to the Settings tab and set it.", "Leroux Compiler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            tabControl.SelectedIndex = 1;
            return;
        }

        // Get selected QC (works for both single and multiple mode)
        DataGridViewRow? row = null;
        if (rbSingle.Checked)
        {
            row = modelGrid.SelectedRows.Count > 0 ? modelGrid.SelectedRows[0] : null;
        }
        else
        {
            row = modelGrid.Rows.Cast<DataGridViewRow>()
                               .FirstOrDefault(r => !r.IsNewRow && r.Cells["colCheck"].Value is true);
        }

        if (row == null) { MessageBox.Show("Select or check a model to view.", "Leroux Compiler"); return; }

        var qcPath = row.Cells["colQC"].Value?.ToString() ?? "";
        if (!ResolveCompiledMdl(qcPath, out var mdlAbsPath, out var mdlRelPath, gameOnly: true) || mdlAbsPath == null)
        {
            MessageBox.Show("Could not find the compiled .mdl for this model.\nMake sure it has been compiled first.", "Leroux Compiler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // HLMV's AppFramework hardcodes DLL paths to the SDK bin folder.
        // Fix: run hlmv.exe from the SDK bin, but first copy the required
        // DLLs from csgo legacy\bin into the SDK bin so AppFramework finds them.
        var sdkBin    = Path.GetDirectoryName(settings.HlmvPath) ?? "";
        var legacyBin = Path.Combine(Path.GetDirectoryName(settings.GamePath) ?? "", "bin");

        if (!File.Exists(settings.HlmvPath))
        {
            MessageBox.Show($"hlmv.exe not found at:\n{settings.HlmvPath}\n\nCheck the Settings tab.", "Leroux Compiler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // DLLs that HLMV needs from csgo legacy\bin
        var requiredDlls = new[]
        {
            "filesystem_stdio.dll", "tier0.dll", "vstdlib.dll", "steam_api.dll",
            "vphysics.dll", "studiorender.dll", "materialsystem.dll",
            "datacache.dll", "soundemittersystem.dll"
        };

        AppendLine("\nPreparing HLMV — copying required DLLs to SDK bin...", Color.Cyan);
        foreach (var dll in requiredDlls)
        {
            var src  = Path.Combine(legacyBin, dll);
            var dest = Path.Combine(sdkBin, dll);
            if (!File.Exists(dest) && File.Exists(src))
            {
                try   { File.Copy(src, dest); AppendLine($"  + {dll}", Color.DimGray); }
                catch { AppendLine($"  ! could not copy {dll}", Color.OrangeRed); }
            }
        }

        // Copy the model path to clipboard so the user can Ctrl+V it in HLMV's Load Model dialog
        Clipboard.SetText(mdlAbsPath);
        AppendLine($"\nModel found: {mdlAbsPath}", Color.LimeGreen);
        AppendLine($"  Path copied to clipboard — paste it in HLMV's File > Load Model dialog.", Color.Yellow);
        Process.Start("explorer.exe", $"/select,\"{mdlAbsPath}\"");

        // Pass the absolute path — HLMV resolves relative paths against its working dir (745\bin),
        // not against -game, so a relative path would look in the wrong place.
        var hlmvModelArg = mdlAbsPath;

        AppendLine($"Launching HLMV...", Color.Cyan);
        AppendLine($"  exe : {settings.HlmvPath}", Color.DimGray);
        AppendLine($"  mdl : {hlmvModelArg}", Color.DimGray);

        var hlmvArgs = $"-game \"{settings.GamePath}\" \"{hlmvModelArg}\"";
        var psi = new ProcessStartInfo
        {
            FileName         = settings.HlmvPath,
            Arguments        = hlmvArgs,
            WorkingDirectory = sdkBin,
            UseShellExecute  = false,
            CreateNoWindow   = false
        };

        Process? hlmvProc = null;
        try { hlmvProc = Process.Start(psi); }
        catch (Exception ex)
        {
            AppendLine($"  HLMV failed to start: {ex.Message}", Color.OrangeRed);
            AppendLine("  The model file is highlighted in Explorer — you can open it manually.", Color.Yellow);
            return;
        }

        // Check after 5s whether HLMV is still running
        Task.Delay(5000).ContinueWith(_ =>
        {
            if (hlmvProc == null || hlmvProc.HasExited)
                SafeAppend("  HLMV closed. Model file is highlighted in Explorer — open it from File > Load Model.", Color.OrangeRed);
            else
                SafeAppend("  HLMV is running.", Color.LimeGreen);
        });
    }

    // Parses $modelname from the QC.
    // absPath — full filesystem path to the .mdl (for Explorer / file operations)
    // relPath — path relative to game dir e.g. "models\player\..\model.mdl" (for HLMV arg),
    //           null when the model is only in the output folder (outside game tree)
    // gameOnly — when true, only checks the game folder (used by HLMV so materials resolve correctly)
    private bool ResolveCompiledMdl(string qcPath, out string? absPath, out string? relPath, bool gameOnly = false)
    {
        absPath = null; relPath = null;
        if (!File.Exists(qcPath)) return false;

        var match = Regex.Match(File.ReadAllText(qcPath), @"\$modelname\s+""([^""]+)""", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        var modelName = match.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        if (!modelName.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase))
            modelName += ".mdl";

        // Always check the game folder first — HLMV needs models here so materials resolve correctly
        var gameMdl = Path.Combine(settings.GamePath, "models", modelName);
        if (File.Exists(gameMdl))
        {
            absPath = gameMdl;
            relPath = "models" + Path.DirectorySeparatorChar + modelName;
            return true;
        }

        // Output folder fallback — skip when called from HLMV
        if (!gameOnly && chkCopyOutput.Checked && !string.IsNullOrWhiteSpace(txtOutputFolder.Text))
        {
            var outMdl = Path.Combine(txtOutputFolder.Text.Trim(), Path.GetFileName(modelName));
            if (File.Exists(outMdl))
            {
                absPath = outMdl;
                relPath = null;
                return true;
            }
        }

        return false;
    }

    private void BtnBrowseGame_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "Select the csgo game folder" };
        if (dlg.ShowDialog() == DialogResult.OK) txtGame.Text = dlg.SelectedPath;
    }

    private void BtnSaveSettings_Click(object? sender, EventArgs e)
    {
        settings.StudiomdlPath = txtStudiomdl.Text.Trim();
        settings.HlmvPath      = txtHlmv.Text.Trim();
        settings.GamePath      = txtGame.Text.Trim();
        SaveSettings();
        MessageBox.Show("Settings saved.", "Leroux Compiler", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Filter = "QC Files (*.qc)|*.qc|All files|*.*", Title = "Select QC file" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var name = Path.GetFileNameWithoutExtension(dlg.FileName);
        modelGrid.Rows.Add(true, name, dlg.FileName);
        settings.Models.Add(new ModelEntry { Name = name, QcPath = dlg.FileName });
        SaveSettings();
    }

    private void BtnRemove_Click(object? sender, EventArgs e)
    {
        var toRemove = modelGrid.SelectedRows.Cast<DataGridViewRow>()
                                             .Where(r => !r.IsNewRow)
                                             .OrderByDescending(r => r.Index)
                                             .ToList();
        foreach (var row in toRemove)
        {
            settings.Models.RemoveAt(row.Index);
            modelGrid.Rows.Remove(row);
        }
        SaveSettings();
    }

    // =========================================================================
    // COMPILE
    // =========================================================================

    private async Task CompileSelected()
    {
        List<ModelEntry> models;

        if (rbSingle.Checked)
        {
            // Single mode: use the highlighted/selected row
            if (modelGrid.SelectedRows.Count == 0) { MessageBox.Show("Select a row to compile.", "Leroux Compiler"); return; }
            var row = modelGrid.SelectedRows[0];
            models = new List<ModelEntry>
            {
                new() { Name = row.Cells["colName"].Value?.ToString() ?? "", QcPath = row.Cells["colQC"].Value?.ToString() ?? "" }
            };
        }
        else
        {
            // Multiple mode: use checked rows
            models = modelGrid.Rows.Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow && r.Cells["colCheck"].Value is true)
                .Select(r => new ModelEntry { Name = r.Cells["colName"].Value?.ToString() ?? "", QcPath = r.Cells["colQC"].Value?.ToString() ?? "" })
                .ToList();

            if (models.Count == 0) { MessageBox.Show("No models are checked.", "Leroux Compiler"); return; }
        }

        await RunCompile(models);
    }

    private async Task CompileAll() => await RunCompile(settings.Models);

    private async Task RunCompile(List<ModelEntry> models)
    {
        if (isCompiling) return;
        if (!ValidateSettings()) return;

        isCompiling = true;
        SetButtonsEnabled(false);
        outputBox.Clear();

        bool copyOutput  = chkCopyOutput.Checked && !string.IsNullOrWhiteSpace(txtOutputFolder.Text);
        string outFolder = txtOutputFolder.Text.Trim();

        AppendLine($"=== Leroux Model Compiler  —  {models.Count} model(s) ===", Color.Cyan);
        AppendLine($"studiomdl : {settings.StudiomdlPath}", Color.DimGray);
        AppendLine($"game      : {settings.GamePath}",      Color.DimGray);
        if (copyOutput) AppendLine($"copy to   : {outFolder}", Color.DimGray);

        int ok = 0, fail = 0;
        for (int i = 0; i < models.Count; i++)
        {
            var m = models[i];
            AppendLine($"\n[{i + 1}/{models.Count}]  {m.Name}", Color.Yellow);
            AppendLine($"  {m.QcPath}", Color.DimGray);

            bool success = await RunStudiomdl(m.QcPath);

            if (success)
            {
                ok++;
                AppendLine("  ✓  Compiled successfully", Color.LimeGreen);
                if (chkCopyMaterials.Checked)
                    CopyMaterials(m.QcPath);
                if (copyOutput)
                    TransferCompiledFiles(m.QcPath, outFolder, move: rbMove.Checked);
            }
            else
            {
                fail++;
                AppendLine("  ✗  Compile FAILED", Color.OrangeRed);
            }
        }

        AppendLine($"\n=== Done — {ok} succeeded, {fail} failed ===", Color.Cyan);
        isCompiling = false;
        SetButtonsEnabled(true);
    }

    private Task<bool> RunStudiomdl(string qcPath)
    {
        var tcs = new TaskCompletionSource<bool>();
        var psi = new ProcessStartInfo
        {
            FileName               = settings.StudiomdlPath,
            Arguments              = $"-game \"{settings.GamePath}\" \"{qcPath}\"",
            WorkingDirectory       = Path.GetDirectoryName(settings.StudiomdlPath) ?? "",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (s, e) => { if (e.Data != null) SafeAppend(e.Data, Color.FromArgb(204, 204, 204)); };
        proc.ErrorDataReceived  += (s, e) => { if (e.Data != null) SafeAppend(e.Data, Color.OrangeRed); };
        proc.Exited += (s, e) => { bool r = proc.ExitCode == 0; proc.Dispose(); tcs.SetResult(r); };

        try { proc.Start(); proc.BeginOutputReadLine(); proc.BeginErrorReadLine(); }
        catch (Exception ex) { SafeAppend($"  ERROR: {ex.Message}", Color.OrangeRed); tcs.SetResult(false); }

        return tcs.Task;
    }

    // After a successful compile, parse $modelname from the QC and copy or move
    // the resulting MDL + sibling files (.vvd, .dx90.vtx, .sw.vtx, .phy) to outFolder.
    private void TransferCompiledFiles(string qcPath, string outFolder, bool move)
    {
        try
        {
            var qcText = File.ReadAllText(qcPath);
            var match  = Regex.Match(qcText, @"\$modelname\s+""([^""]+)""", RegexOptions.IgnoreCase);
            if (!match.Success) { AppendLine("  (transfer skipped: could not parse $modelname)", Color.DimGray); return; }

            var modelName = match.Groups[1].Value.Replace('/', '\\').TrimStart('\\');
            var srcBase   = Path.ChangeExtension(Path.Combine(settings.GamePath, "models", modelName), null);

            var extensions = new[] { ".mdl", ".vvd", ".dx90.vtx", ".sw.vtx", ".360.vtx", ".phy" };
            int transferred = 0;
            foreach (var ext in extensions)
            {
                var src = srcBase + ext;
                if (!File.Exists(src)) continue;
                var dest = Path.Combine(outFolder, Path.GetFileName(src));
                if (move)
                {
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(src, dest);
                }
                else
                {
                    File.Copy(src, dest, overwrite: true);
                }
                transferred++;
            }
            var verb = move ? "Moved" : "Copied";
            AppendLine($"  ↳ {verb} {transferred} file(s) to {outFolder}", Color.SteelBlue);
        }
        catch (Exception ex)
        {
            AppendLine($"  (transfer error: {ex.Message})", Color.OrangeRed);
        }
    }

    // Parses every $cdmaterials line from the QC, finds VMT/VTF files
    // in the QC folder and up to 2 parent directories, and copies them
    // to <game>/materials/<cdmaterials_path>/.
    private void CopyMaterials(string qcPath)
    {
        try
        {
            var qcText = File.ReadAllText(qcPath);

            // Collect all $cdmaterials paths (skip empty "")
            var cdPaths = Regex.Matches(qcText, @"\$cdmaterials\s+""([^""]+)""", RegexOptions.IgnoreCase)
                               .Cast<Match>()
                               .Select(m => m.Groups[1].Value.Trim('\\', '/').Trim())
                               .Where(p => !string.IsNullOrWhiteSpace(p))
                               .Distinct()
                               .ToList();

            if (cdPaths.Count == 0)
            {
                AppendLine("  (materials: no $cdmaterials paths found in QC)", Color.DimGray);
                return;
            }

            // Search for VMT/VTF files in QC dir and up to 2 parent dirs
            var searchDirs = new List<string>();
            var dir = Path.GetDirectoryName(qcPath) ?? "";
            for (int i = 0; i < 3 && !string.IsNullOrEmpty(dir); i++)
            {
                searchDirs.Add(dir);
                dir = Path.GetDirectoryName(dir) ?? "";
            }

            var matFiles = searchDirs
                .SelectMany(d => Directory.GetFiles(d, "*.vmt")
                                          .Concat(Directory.GetFiles(d, "*.vtf")))
                .Distinct()
                .ToList();

            if (matFiles.Count == 0)
            {
                AppendLine("  (materials: no VMT/VTF files found near QC)", Color.DimGray);
                return;
            }

            int copied = 0;
            foreach (var cdPath in cdPaths)
            {
                var destDir = Path.Combine(settings.GamePath, "materials", cdPath);
                Directory.CreateDirectory(destDir);
                foreach (var src in matFiles)
                {
                    var dest = Path.Combine(destDir, Path.GetFileName(src));
                    File.Copy(src, dest, overwrite: true);
                    copied++;
                }
            }
            AppendLine($"  ↳ Copied {matFiles.Count} material file(s) to {cdPaths.Count} folder(s) in game materials", Color.SteelBlue);
        }
        catch (Exception ex)
        {
            AppendLine($"  (materials error: {ex.Message})", Color.OrangeRed);
        }
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private bool ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(settings.StudiomdlPath) || !File.Exists(settings.StudiomdlPath))
        {
            MessageBox.Show("studiomdl.exe path is not set.\nGo to the Settings tab.", "Leroux Compiler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            tabControl.SelectedIndex = 1;
            return false;
        }
        if (string.IsNullOrWhiteSpace(settings.GamePath) || !Directory.Exists(settings.GamePath))
        {
            MessageBox.Show("-game path is not set.\nGo to the Settings tab.", "Leroux Compiler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            tabControl.SelectedIndex = 1;
            return false;
        }
        return true;
    }

    private void SafeAppend(string text, Color color)
    {
        if (outputBox.InvokeRequired) outputBox.Invoke(() => AppendLine(text, color));
        else AppendLine(text, color);
    }

    private void AppendLine(string text, Color color)
    {
        outputBox.SelectionStart  = outputBox.TextLength;
        outputBox.SelectionLength = 0;
        outputBox.SelectionColor  = color;
        outputBox.AppendText(text + "\n");
        outputBox.SelectionColor  = outputBox.ForeColor;
        outputBox.ScrollToCaret();
    }

    private void SetButtonsEnabled(bool on)
    {
        btnAdd.Enabled = btnRemove.Enabled = btnScan.Enabled =
        btnCompileSelected.Enabled = btnCompileAll.Enabled = btnViewHlmv.Enabled = on;
    }

    // =========================================================================
    // SETTINGS PERSISTENCE
    // =========================================================================

    private string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    private void LoadSettings()
    {
        if (!File.Exists(SettingsPath)) { settings = new AppSettings(); return; }
        try { settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings(); }
        catch { settings = new AppSettings(); }
    }

    private void SaveSettings() =>
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));

    private void PopulateControls()
    {
        txtStudiomdl.Text = settings.StudiomdlPath;
        txtHlmv.Text      = settings.HlmvPath;
        txtGame.Text      = settings.GamePath;
        modelGrid.Rows.Clear();
        foreach (var m in settings.Models)
            modelGrid.Rows.Add(true, m.Name, m.QcPath);
    }

    // =========================================================================
    // BUTTON FACTORIES
    // =========================================================================

    private static Button MakeButton(string text, Color back) => new()
    {
        Text = text, AutoSize = true, Height = 30,
        Padding = new Padding(8, 0, 8, 0),
        BackColor = back, ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
    };

    private static Button MakeSmallButton(string text, Color? back = null)
    {
        var b = new Button { Text = text, AutoSize = true, Height = 24, Margin = new Padding(4, 0, 0, 0), Cursor = Cursors.Hand };
        if (back.HasValue) { b.BackColor = back.Value; b.ForeColor = Color.White; b.FlatStyle = FlatStyle.Flat; }
        return b;
    }
}

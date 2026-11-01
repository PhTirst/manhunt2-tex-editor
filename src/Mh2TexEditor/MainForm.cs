using System.ComponentModel;

namespace Mh2TexEditor;

internal sealed class MainForm : Form
{
    private static readonly Color Primary = Color.FromArgb(37, 99, 235);
    private static readonly Color PrimaryHover = Color.FromArgb(29, 78, 216);
    private static readonly Color Border = Color.FromArgb(216, 221, 230);
    private static readonly Color Muted = Color.FromArgb(100, 111, 130);

    private readonly string? startupPath;
    private readonly DataGridView textureGrid = new();
    private readonly TextBox searchBox = new();
    private readonly Label textureCountLabel = new();
    private readonly TextureViewport viewport = new();
    private readonly ComboBox channelBox = new();
    private readonly ComboBox sourceBox = new();
    private readonly ComboBox backgroundBox = new();
    private readonly NumericUpDown mipBox = new();
    private readonly Label nameValue = new();
    private readonly Label dimensionsValue = new();
    private readonly Label formatValue = new();
    private readonly Label mipValue = new();
    private readonly Label slotValue = new();
    private readonly Label stateValue = new();
    private readonly Label requirementValue = new();
    private readonly ToolStripButton saveButton = new("另存为");
    private readonly ToolStripButton exportButton = new("导出 PNG");
    private readonly ToolStripButton replaceButton = new("替换 PNG");
    private readonly ToolStripButton undoButton = new("撤销");
    private readonly ToolStripButton redoButton = new("重做");
    private readonly ToolStripStatusLabel statusText = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel documentText = new();

    private TexDocument? document;
    private IReadOnlyList<TextureInfo> textures = Array.Empty<TextureInfo>();
    private bool changingSelection;
    private bool busy;

    public MainForm(string? startupPath)
    {
        this.startupPath = startupPath;
        Text = "Manhunt 2 TEX Editor";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 640);
        ClientSize = new Size(1360, 820);
        Font = new Font("Segoe UI", 9f);
        BackColor = Color.FromArgb(245, 247, 250);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        AllowDrop = true;

        BuildInterface();
        WireEvents();
        UpdateCommandState();

        Shown += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(this.startupPath) && File.Exists(this.startupPath))
                await OpenDocumentAsync(this.startupPath);
        };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (busy)
        {
            e.Cancel = true;
            statusText.Text = "正在处理贴图，请完成后再关闭窗口。";
        }
        else if (!ConfirmDiscardChanges())
            e.Cancel = true;
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            document?.Dispose();
        base.Dispose(disposing);
    }

    private void BuildInterface()
    {
        var menu = BuildMenu();
        var toolbar = BuildToolbar();
        var status = new StatusStrip
        {
            SizingGrip = false,
            BackColor = Color.White,
            Renderer = new ToolStripProfessionalRenderer(new EditorColorTable()),
        };
        status.Items.Add(statusText);
        status.Items.Add(documentText);
        statusText.Text = "就绪";

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Border,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Controls.Add(BuildTextureList(), 0, 0);
        content.Controls.Add(BuildPreviewArea(), 1, 0);
        content.Controls.Add(BuildInspector(), 2, 0);

        Controls.Add(content);
        Controls.Add(status);
        Controls.Add(toolbar);
        Controls.Add(menu);
        MainMenuStrip = menu;
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip
        {
            Dock = DockStyle.Top,
            BackColor = Color.White,
            Renderer = new ToolStripProfessionalRenderer(new EditorColorTable()),
        };

        var file = new ToolStripMenuItem("文件(&F)");
        file.DropDownItems.Add(MenuItem("打开 TEX...", Keys.Control | Keys.O, async () => await ChooseAndOpenAsync()));
        file.DropDownItems.Add(MenuItem("另存为...", Keys.Control | Keys.Shift | Keys.S, async () => await SaveAsAsync()));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(MenuItem("导出选中贴图...", Keys.Control | Keys.E, ExportSelected));
        file.DropDownItems.Add(MenuItem("导出全部贴图...", Keys.None, async () => await ExportAllAsync()));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(MenuItem("退出", Keys.Alt | Keys.F4, Close));

        var edit = new ToolStripMenuItem("编辑(&E)");
        edit.DropDownItems.Add(MenuItem("撤销", Keys.Control | Keys.Z, Undo));
        edit.DropDownItems.Add(MenuItem("重做", Keys.Control | Keys.Y, Redo));
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(MenuItem("替换选中贴图...", Keys.Control | Keys.R, ReplaceSelected));
        edit.DropDownItems.Add(MenuItem("从文件夹批量替换...", Keys.None, async () => await BatchReplaceAsync()));
        edit.DropDownItems.Add(MenuItem("还原选中贴图", Keys.None, RevertSelected));

        var view = new ToolStripMenuItem("视图(&V)");
        view.DropDownItems.Add(MenuItem("适应窗口", Keys.Control | Keys.D0, viewport.Fit));
        view.DropDownItems.Add(MenuItem("实际大小", Keys.Control | Keys.D1, viewport.ActualSize));

        var help = new ToolStripMenuItem("帮助(&H)");
        help.DropDownItems.Add(MenuItem("关于", Keys.None, ShowAbout));

        menu.Items.AddRange([file, edit, view, help]);
        return menu;
    }

    private ToolStrip BuildToolbar()
    {
        var toolbar = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(8, 5, 8, 5),
            AutoSize = true,
            BackColor = Color.White,
            Renderer = new ToolStripProfessionalRenderer(new EditorColorTable()),
        };

        var openButton = new ToolStripButton("打开") { ToolTipText = "打开 Manhunt 2 TEX" };
        var exportAllButton = new ToolStripButton("全部导出") { ToolTipText = "将全部贴图导出为 PNG" };
        var batchButton = new ToolStripButton("批量替换") { ToolTipText = "按 PNG 文件名批量匹配贴图" };
        openButton.Click += async (_, _) => await ChooseAndOpenAsync();
        saveButton.Click += async (_, _) => await SaveAsAsync();
        exportButton.Click += (_, _) => ExportSelected();
        exportAllButton.Click += async (_, _) => await ExportAllAsync();
        replaceButton.Click += (_, _) => ReplaceSelected();
        batchButton.Click += async (_, _) => await BatchReplaceAsync();
        undoButton.Click += (_, _) => Undo();
        redoButton.Click += (_, _) => Redo();

        toolbar.Items.AddRange([
            openButton,
            saveButton,
            new ToolStripSeparator(),
            exportButton,
            exportAllButton,
            new ToolStripSeparator(),
            replaceButton,
            batchButton,
            new ToolStripSeparator(),
            undoButton,
            redoButton,
        ]);
        return toolbar;
    }

    private Control BuildTextureList()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(0, 1, 1, 0),
            Padding = new Padding(12),
        };

        searchBox.Dock = DockStyle.Top;
        searchBox.PlaceholderText = "搜索名称或格式";
        searchBox.Height = 32;
        searchBox.BorderStyle = BorderStyle.FixedSingle;

        textureCountLabel.Dock = DockStyle.Bottom;
        textureCountLabel.Height = 28;
        textureCountLabel.ForeColor = Muted;
        textureCountLabel.TextAlign = ContentAlignment.MiddleLeft;
        textureCountLabel.Text = "0 张贴图";

        textureGrid.Dock = DockStyle.Fill;
        textureGrid.BackgroundColor = Color.White;
        textureGrid.BorderStyle = BorderStyle.None;
        textureGrid.RowHeadersVisible = false;
        textureGrid.AllowUserToAddRows = false;
        textureGrid.AllowUserToDeleteRows = false;
        textureGrid.AllowUserToResizeRows = false;
        textureGrid.ReadOnly = true;
        textureGrid.MultiSelect = false;
        textureGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        textureGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        textureGrid.RowTemplate.Height = 26;
        textureGrid.ColumnHeadersHeight = 32;
        textureGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        textureGrid.EnableHeadersVisualStyles = false;
        textureGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(244, 246, 249);
        textureGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(54, 65, 82);
        textureGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(244, 246, 249);
        textureGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
        textureGrid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 40, 76);
        textureGrid.DefaultCellStyle.ForeColor = Color.FromArgb(38, 47, 61);
        textureGrid.GridColor = Color.FromArgb(235, 238, 243);
        textureGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        textureGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "名称", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

        panel.Controls.Add(textureGrid);
        panel.Controls.Add(textureCountLabel);
        panel.Controls.Add(searchBox);
        return panel;
    }

    private Control BuildPreviewArea()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(0, 1, 1, 0),
        };

        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 45),
            Padding = new Padding(10, 8, 10, 5),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Color.White,
        };

        ConfigureCombo(channelBox, 88, ["RGBA", "Alpha", "红色", "绿色", "蓝色"]);
        ConfigureCombo(sourceBox, 94, ["当前结果", "原始贴图"]);
        ConfigureCombo(backgroundBox, 86, ["棋盘背景", "深色背景", "浅色背景"]);
        mipBox.Minimum = 0;
        mipBox.Maximum = 0;
        mipBox.Width = 54;
        mipBox.TextAlign = HorizontalAlignment.Center;

        var fitButton = SmallButton("适应");
        var actualButton = SmallButton("1:1");
        fitButton.Click += (_, _) => viewport.Fit();
        actualButton.Click += (_, _) => viewport.ActualSize();

        controls.Controls.AddRange([
            CompactLabel("通道"), channelBox,
            CompactLabel("来源"), sourceBox,
            CompactLabel("Mip"), mipBox,
            CompactLabel("背景"), backgroundBox,
            fitButton, actualButton,
        ]);

        viewport.Dock = DockStyle.Fill;
        viewport.Margin = Padding.Empty;
        panel.Controls.Add(viewport);
        panel.Controls.Add(controls);
        return panel;
    }

    private Control BuildInspector()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(0, 1, 0, 0),
            Padding = new Padding(18),
        };
        var title = new Label
        {
            Text = "贴图属性",
            Dock = DockStyle.Top,
            Font = new Font(Font, FontStyle.Bold),
            Height = 32,
            ForeColor = Color.FromArgb(35, 45, 60),
        };

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(0, 8, 0, 12),
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddField(fields, "名称", nameValue);
        AddField(fields, "尺寸", dimensionsValue);
        AddField(fields, "格式", formatValue);
        AddField(fields, "Mip", mipValue);
        AddField(fields, "槽位", slotValue);
        AddField(fields, "状态", stateValue);

        requirementValue.Dock = DockStyle.Top;
        requirementValue.Height = 50;
        requirementValue.ForeColor = Muted;
        requirementValue.Padding = new Padding(0, 4, 0, 4);

        var replace = PrimaryButton("替换 PNG");
        var export = SecondaryButton("导出 PNG");
        var revert = SecondaryButton("还原此贴图");
        replace.Click += (_, _) => ReplaceSelected();
        export.Click += (_, _) => ExportSelected();
        revert.Click += (_, _) => RevertSelected();

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(0, 12, 0, 0),
        };
        foreach (var button in new[] { replace, export, revert })
        {
            button.Width = 228;
            actions.Controls.Add(button);
        }

        panel.Controls.Add(actions);
        panel.Controls.Add(requirementValue);
        panel.Controls.Add(fields);
        panel.Controls.Add(title);
        return panel;
    }

    private void WireEvents()
    {
        searchBox.TextChanged += (_, _) => ApplyFilter();
        textureGrid.SelectionChanged += (_, _) => RefreshSelection();
        channelBox.SelectedIndexChanged += (_, _) => RefreshPreview();
        sourceBox.SelectedIndexChanged += (_, _) => RefreshPreview();
        backgroundBox.SelectedIndexChanged += (_, _) =>
        {
            viewport.BackgroundMode = (PreviewBackground)Math.Max(0, backgroundBox.SelectedIndex);
            viewport.Invalidate();
        };
        mipBox.ValueChanged += (_, _) =>
        {
            if (!changingSelection)
                RefreshPreview();
        };
        viewport.PngDropped += (_, path) => ReplaceSelected(path);

        DragEnter += (_, eventArgs) =>
        {
            if (eventArgs.Data?.GetData(DataFormats.FileDrop) is string[] files &&
                files.Length == 1 &&
                string.Equals(Path.GetExtension(files[0]), ".tex", StringComparison.OrdinalIgnoreCase))
                eventArgs.Effect = DragDropEffects.Copy;
        };
        DragDrop += async (_, eventArgs) =>
        {
            if (eventArgs.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1)
                await OpenDocumentAsync(files[0]);
        };
    }

    private async Task ChooseAndOpenAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Manhunt 2 TEX (*.tex)|*.tex|所有文件 (*.*)|*.*",
            Title = "打开 Manhunt 2 TEX",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await OpenDocumentAsync(dialog.FileName);
    }

    private async Task OpenDocumentAsync(string path)
    {
        if (busy || !ConfirmDiscardChanges())
            return;
        var openedSuccessfully = false;
        try
        {
            SetBusy(true, "正在打开 TEX...");
            var opened = await Task.Run(() => TexDocument.Open(path));
            document?.Dispose();
            document = opened;
            textures = document.GetTextures();
            ApplyFilter();
            statusText.Text = $"已打开 {textures.Count} 张贴图";
            documentText.Text = document.Path;
            UpdateTitle();
            openedSuccessfully = true;
        }
        catch (Exception error)
        {
            ShowError("打开 TEX 失败", error);
        }
        finally
        {
            SetBusy(false);
        }
        if (openedSuccessfully)
            RefreshPreview();
    }

    private async Task SaveAsAsync()
    {
        if (document is null || busy)
            return;
        using var dialog = new SaveFileDialog
        {
            Filter = "Manhunt 2 TEX (*.tex)|*.tex",
            Title = "保存 TEX 副本",
            InitialDirectory = Path.GetDirectoryName(document.Path),
            FileName = Path.GetFileNameWithoutExtension(document.Path) + "_edited.tex",
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            SetBusy(true, "正在编码并保存 TEX...");
            await Task.Run(() => document.SaveAs(dialog.FileName));
            textures = document.GetTextures();
            ApplyFilter();
            statusText.Text = "TEX 已安全保存";
            documentText.Text = document.Path;
            UpdateTitle();
        }
        catch (Exception error)
        {
            ShowError("保存 TEX 失败", error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ExportSelected()
    {
        var selected = SelectedTexture;
        if (document is null || selected is null || busy)
            return;
        using var dialog = new SaveFileDialog
        {
            Filter = "PNG 图片 (*.png)|*.png",
            Title = "导出贴图",
            FileName = selected.ExportStem + ".png",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            document.ExportPng(selected.Index, dialog.FileName, sourceBox.SelectedIndex == 1);
            statusText.Text = $"已导出 {selected.DisplayName}";
        }
        catch (Exception error)
        {
            ShowError("导出 PNG 失败", error);
        }
    }

    private async Task ExportAllAsync()
    {
        if (document is null || busy)
            return;
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择全部 PNG 的导出目录",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var currentDocument = document;
        var snapshot = textures;
        var exportOriginal = sourceBox.SelectedIndex == 1;
        var failures = new List<string>();
        try
        {
            SetBusy(true, "正在导出全部贴图...");
            await Task.Run(() =>
            {
                foreach (var texture in snapshot)
                {
                    try
                    {
                        currentDocument.ExportPng(texture.Index, Path.Combine(dialog.SelectedPath, texture.ExportStem + ".png"), exportOriginal);
                    }
                    catch (Exception error)
                    {
                        failures.Add($"{texture.DisplayName}: {error.Message}");
                    }
                }
            });
            statusText.Text = failures.Count == 0
                ? $"已导出 {snapshot.Count} 张贴图"
                : $"导出完成，{failures.Count} 张失败";
            if (failures.Count > 0)
                ShowFailureList("部分贴图导出失败", failures);
        }
        catch (Exception error)
        {
            ShowError("批量导出失败", error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ReplaceSelected() => ReplaceSelected(null);

    private void ReplaceSelected(string? suppliedPath)
    {
        var selected = SelectedTexture;
        if (document is null || selected is null || busy)
            return;

        var path = suppliedPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "PNG 图片 (*.png)|*.png",
                Title = $"替换 {selected.DisplayName}",
                CheckFileExists = true,
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            path = dialog.FileName;
        }

        try
        {
            document.ReplacePng(selected.Index, path);
            RefreshTextureData(selected.Index);
            statusText.Text = $"已替换 {selected.DisplayName}";
        }
        catch (Exception error)
        {
            ShowError("替换贴图失败", error);
        }
    }

    private async Task BatchReplaceAsync()
    {
        if (document is null || busy)
            return;
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择包含同名 PNG 的目录",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        Dictionary<string, string> pngFiles;
        try
        {
            pngFiles = Directory.EnumerateFiles(dialog.SelectedPath, "*.png", SearchOption.TopDirectoryOnly)
            .Select(path => (Name: Path.GetFileNameWithoutExtension(path) ?? string.Empty, Path: path))
            .Where(item => item.Name.Length > 0)
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Path, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception error)
        {
            ShowError("无法读取 PNG 文件夹", error);
            return;
        }
        var matches = textures
            .Select(texture =>
            {
                if (pngFiles.TryGetValue(texture.ExportStem, out var distinctPath))
                    return (Texture: texture, Path: distinctPath);
                return pngFiles.TryGetValue(texture.Name, out var sharedPath)
                    ? (Texture: texture, Path: sharedPath)
                    : (Texture: texture, Path: (string?)null);
            })
            .Where(match => match.Path is not null)
            .ToArray();
        if (matches.Length == 0)
        {
            MessageBox.Show(this, "目录中没有与 TEX 贴图同名的 PNG。", "没有匹配项", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var currentDocument = document;
        var failures = new List<string>();
        var succeeded = 0;
        try
        {
            SetBusy(true, $"正在批量替换 {matches.Length} 张贴图...");
            await Task.Run(() =>
            {
                foreach (var match in matches)
                {
                    try
                    {
                        currentDocument.ReplacePng(match.Texture.Index, match.Path!);
                        succeeded++;
                    }
                    catch (Exception error)
                    {
                        failures.Add($"{match.Texture.DisplayName}: {error.Message}");
                    }
                }
            });
            textures = document.GetTextures();
            ApplyFilter();
            statusText.Text = $"批量替换完成：{succeeded} 成功，{failures.Count} 失败";
            if (failures.Count > 0)
                ShowFailureList("部分贴图替换失败", failures);
        }
        catch (Exception error)
        {
            ShowError("批量替换失败", error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RevertSelected()
    {
        var selected = SelectedTexture;
        if (document is null || selected is null || busy || !selected.Changed)
            return;
        try
        {
            document.RevertTexture(selected.Index);
            RefreshTextureData(selected.Index);
            statusText.Text = $"已还原 {selected.DisplayName}";
        }
        catch (Exception error)
        {
            ShowError("还原贴图失败", error);
        }
    }

    private void Undo()
    {
        if (document is null || !document.CanUndo || busy)
            return;
        try
        {
            document.Undo();
            RefreshTextureData(SelectedTexture?.Index);
            statusText.Text = "已撤销";
        }
        catch (Exception error)
        {
            ShowError("撤销失败", error);
        }
    }

    private void Redo()
    {
        if (document is null || !document.CanRedo || busy)
            return;
        try
        {
            document.Redo();
            RefreshTextureData(SelectedTexture?.Index);
            statusText.Text = "已重做";
        }
        catch (Exception error)
        {
            ShowError("重做失败", error);
        }
    }

    private void ApplyFilter()
    {
        var selectedIndex = SelectedTexture?.Index;
        var query = searchBox.Text.Trim();
        var filtered = (string.IsNullOrEmpty(query)
            ? textures.AsEnumerable()
            : textures.Where(texture =>
                texture.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                texture.Format.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();

        changingSelection = true;
        textureGrid.Rows.Clear();
        foreach (var texture in filtered)
        {
            var rowIndex = textureGrid.Rows.Add(texture.DisplayName);
            var row = textureGrid.Rows[rowIndex];
            row.Tag = texture;
            row.Cells[0].ToolTipText = texture.NameCount > 1
                ? $"TEX 中存在 {texture.NameCount} 条同名物理记录；当前为第 {texture.NameOccurrence} 条"
                : texture.Name;
            if (texture.Changed)
            {
                row.Cells[0].Style.ForeColor = Primary;
                row.Cells[0].Style.Font = new Font(textureGrid.Font, FontStyle.Bold);
            }
        }
        textureCountLabel.Text = $"{filtered.Length} / {textures.Count} 张贴图";

        DataGridViewRow? target = null;
        if (selectedIndex.HasValue)
            target = textureGrid.Rows.Cast<DataGridViewRow>().FirstOrDefault(row => (row.Tag as TextureInfo)?.Index == selectedIndex.Value);
        target ??= textureGrid.Rows.Cast<DataGridViewRow>().FirstOrDefault();
        if (target is not null)
        {
            target.Selected = true;
            textureGrid.CurrentCell = target.Cells[0];
        }
        changingSelection = false;
        RefreshSelection();
        UpdateCommandState();
    }

    private void RefreshTextureData(int? preserveIndex)
    {
        if (document is null)
            return;
        textures = document.GetTextures();
        ApplyFilter();
        if (preserveIndex.HasValue)
        {
            var row = textureGrid.Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(candidate => (candidate.Tag as TextureInfo)?.Index == preserveIndex.Value);
            if (row is not null)
            {
                row.Selected = true;
                textureGrid.CurrentCell = row.Cells[0];
            }
        }
        UpdateTitle();
    }

    private void RefreshSelection()
    {
        if (changingSelection)
            return;
        var selected = SelectedTexture;
        changingSelection = true;
        if (selected is null)
        {
            nameValue.Text = "—";
            dimensionsValue.Text = "—";
            formatValue.Text = "—";
            mipValue.Text = "—";
            slotValue.Text = "—";
            stateValue.Text = "—";
            requirementValue.Text = string.Empty;
            mipBox.Maximum = 0;
            mipBox.Value = 0;
            viewport.SetImage(null);
        }
        else
        {
            nameValue.Text = selected.DisplayName;
            dimensionsValue.Text = $"{selected.Width} × {selected.Height}";
            formatValue.Text = selected.Format;
            mipValue.Text = selected.MipCount.ToString();
            slotValue.Text = FormatBytes(selected.SlotSize);
            stateValue.Text = selected.Changed ? "已修改" : "原始";
            stateValue.ForeColor = selected.Changed ? Primary : Color.FromArgb(50, 60, 75);
            requirementValue.Text = $"PNG 尺寸：{selected.Width} × {selected.Height}\n编码格式：{selected.Format}";
            mipBox.Maximum = Math.Max(0, (int)selected.MipCount - 1);
            mipBox.Value = 0;
        }
        changingSelection = false;
        RefreshPreview();
        UpdateCommandState();
    }

    private void RefreshPreview()
    {
        var selected = SelectedTexture;
        if (document is null || selected is null || busy)
            return;
        try
        {
            var bitmap = document.RenderPreview(
                selected.Index,
                (uint)mipBox.Value,
                sourceBox.SelectedIndex == 1,
                (PreviewChannel)Math.Max(0, channelBox.SelectedIndex));
            viewport.SetImage(bitmap);
            statusText.Text = $"{selected.DisplayName} · {bitmap.Width}×{bitmap.Height} · Mip {mipBox.Value}";
        }
        catch (Exception error)
        {
            viewport.SetImage(null);
            statusText.Text = error.Message.Replace(Environment.NewLine, " ");
        }
    }

    private TextureInfo? SelectedTexture => textureGrid.CurrentRow?.Tag as TextureInfo;

    private void SetBusy(bool value, string? message = null)
    {
        busy = value;
        UseWaitCursor = value;
        if (!string.IsNullOrWhiteSpace(message))
            statusText.Text = message;
        UpdateCommandState();
        if (!value)
            RefreshPreview();
    }

    private void UpdateCommandState()
    {
        var hasDocument = document is not null;
        var hasSelection = SelectedTexture is not null;
        saveButton.Enabled = hasDocument && !busy;
        exportButton.Enabled = hasSelection && !busy;
        replaceButton.Enabled = hasSelection && !busy;
        undoButton.Enabled = hasDocument && !busy && document!.CanUndo;
        redoButton.Enabled = hasDocument && !busy && document!.CanRedo;
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        var name = document is null ? "Manhunt 2 TEX Editor" : Path.GetFileName(document.Path);
        var dirty = !busy && document?.IsDirty == true ? " *" : string.Empty;
        Text = $"{name}{dirty} - Manhunt 2 TEX Editor";
    }

    private bool ConfirmDiscardChanges()
    {
        if (document?.IsDirty != true)
            return true;
        return MessageBox.Show(
            this,
            "当前 TEX 有尚未保存的修改。确定放弃这些修改吗？",
            "未保存的修改",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private static ToolStripMenuItem MenuItem(string text, Keys shortcut, Action action)
    {
        var item = new ToolStripMenuItem(text);
        if (shortcut != Keys.None)
            item.ShortcutKeys = shortcut;
        item.Click += (_, _) => action();
        return item;
    }

    private static void ConfigureCombo(ComboBox combo, int width, object[] items)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Width = width;
        combo.Items.AddRange(items);
        combo.SelectedIndex = 0;
        combo.Margin = new Padding(3, 0, 10, 0);
    }

    private static Label CompactLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Muted,
        Margin = new Padding(0, 5, 2, 0),
    };

    private static Button SmallButton(string text)
    {
        var button = SecondaryButton(text);
        button.AutoSize = true;
        button.Height = 27;
        button.Padding = new Padding(8, 0, 8, 0);
        button.Margin = new Padding(3, 0, 0, 0);
        return button;
    }

    private static Button PrimaryButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Height = 36,
            BackColor = Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 4, 0, 4),
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = PrimaryHover;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 64, 175);
        return button;
    }

    private static Button SecondaryButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Height = 34,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(45, 55, 70),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 4, 0, 4),
        };
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 246, 255);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(219, 234, 254);
        return button;
    }

    private static void AddField(TableLayoutPanel panel, string name, Label value)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label
        {
            Text = name,
            AutoSize = true,
            ForeColor = Muted,
            Margin = new Padding(0, 5, 8, 7),
        }, 0, row);
        value.AutoSize = true;
        value.MaximumSize = new Size(150, 0);
        value.ForeColor = Color.FromArgb(45, 55, 70);
        value.Margin = new Padding(0, 5, 0, 7);
        panel.Controls.Add(value, 1, row);
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            this,
            "Manhunt 2 TEX Editor\n\n仅支持 Manhunt 2 TEX。PNG 导入会保持原贴图尺寸、DDS 格式和 mipmap 数量。",
            "关于",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ShowError(string title, Exception error)
    {
        statusText.Text = title;
        MessageBox.Show(this, error.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void ShowFailureList(string title, IReadOnlyList<string> failures)
    {
        var visible = failures.Take(12).ToList();
        if (failures.Count > visible.Count)
            visible.Add($"……另有 {failures.Count - visible.Count} 项");
        MessageBox.Show(this, string.Join(Environment.NewLine, visible), title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static string FormatBytes(uint bytes) => bytes >= 1024 * 1024
        ? $"{bytes / 1024d / 1024d:F2} MB"
        : bytes >= 1024
            ? $"{bytes / 1024d:F1} KB"
            : $"{bytes} B";
}

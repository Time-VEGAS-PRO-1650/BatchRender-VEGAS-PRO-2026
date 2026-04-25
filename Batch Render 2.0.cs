/**
 * Batch Render Script for VEGAS Pro 2026
  **/
using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;

using ScriptPortal.Vegas;

public class EntryPoint
{
    bool   OverwriteExistingFiles = false;
    String defaultBasePath        = "Untitled_";
    const  int QUICKTIME_MAX_FILE_NAME_LENGTH = 55;

    ScriptPortal.Vegas.Vegas myVegas = null;

    enum RenderMode { Project = 0, Selection, Regions }

    ArrayList SelectedTemplates = new ArrayList();

    Button      BrowseButton;
    TextBox     FileNameBox;
    TreeView    TemplateTree;
    RadioButton RenderProjectButton;
    RadioButton RenderRegionsButton;
    RadioButton RenderSelectionButton;

    // ── Palette ───────────────────────────────────────────────────────────
    static readonly Color VP_BG       = Color.FromArgb(45,  45,  45);
    static readonly Color VP_INPUT    = Color.FromArgb(35,  35,  35);
    static readonly Color VP_BORDER   = Color.FromArgb(68,  68,  68);
    static readonly Color VP_TEXT     = Color.FromArgb(225, 225, 225);
    static readonly Color VP_TEXT_DIM = Color.FromArgb(140, 140, 140);
    static readonly Color VP_TEXT_SEC = Color.FromArgb(185, 185, 185);  // section labels
    static readonly Color VP_BTN      = Color.FromArgb(72,  72,  72);
    static readonly Color VP_BTN_H    = Color.FromArgb(92,  92,  92);
    static readonly Color VP_OK       = Color.FromArgb(55,  100, 155);  // steel blue
    static readonly Color VP_OK_H     = Color.FromArgb(75,  125, 185);

    // ──────────────────────────────────────────────────────────────────────
    public void FromVegas(Vegas vegas)
    {
        myVegas = vegas;

        String projectPath = myVegas.Project.FilePath;
        if (String.IsNullOrEmpty(projectPath))
        {
            String dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            defaultBasePath = Path.Combine(dir, defaultBasePath);
        }
        else
        {
            String dir  = Path.GetDirectoryName(projectPath);
            String name = Path.GetFileNameWithoutExtension(projectPath);
            defaultBasePath = Path.Combine(dir, name + "_");
        }

        DialogResult result = ShowBatchRenderDialog();
        myVegas.UpdateUI();

        if (DialogResult.OK == result)
        {
            String     outPath    = FileNameBox.Text;
            RenderMode renderMode = RenderMode.Project;
            if      (RenderRegionsButton.Checked)   renderMode = RenderMode.Regions;
            else if (RenderSelectionButton.Checked) renderMode = RenderMode.Selection;
            DoBatchRender(SelectedTemplates, outPath, renderMode);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  RENDER LOGIC
    // ══════════════════════════════════════════════════════════════════════
    void DoBatchRender(ArrayList selectedTemplates, String basePath, RenderMode renderMode)
    {
        String outputDir = Path.GetDirectoryName(basePath);
        String baseFile  = Path.GetFileName(basePath);

        if (null == selectedTemplates || selectedTemplates.Count == 0)
            throw new ApplicationException("No render templates selected.");
        if (!Directory.Exists(outputDir))
            throw new ApplicationException("The output directory does not exist.");

        List<RenderArgs> renders = new List<RenderArgs>();

        foreach (RenderItem ri in selectedTemplates)
        {
            String filename = Path.Combine(outputDir,
                                           FixFileName(baseFile) +
                                           FixFileName(ri.Renderer.FileTypeName) + "_" +
                                           FixFileName(ri.Template.Name));

            if (ri.Renderer.ClassID == Renderer.CLSID_CSfQT7RenderFileClass)
            {
                int size = baseFile.Length + ri.Renderer.FileTypeName.Length + 1 + ri.Template.Name.Length;
                if (size > QUICKTIME_MAX_FILE_NAME_LENGTH)
                {
                    int    dif = size - (QUICKTIME_MAX_FILE_NAME_LENGTH - 2);
                    string s1  = ri.Renderer.FileTypeName;
                    string s2  = ri.Template.Name;
                    if (s1.Length < dif + 3) { dif -= s1.Length - 3; s1 = s1.Substring(0, 3); s2 = s2.Substring(dif); }
                    else                      { s1 = s1.Substring(0, s1.Length - dif); }
                    filename = Path.Combine(outputDir, FixFileName(baseFile) + FixFileName(s1) + "--" + FixFileName(s2));
                }
            }

            if (renderMode == RenderMode.Regions)
            {
                int idx = 0;
                foreach (ScriptPortal.Vegas.Region region in myVegas.Project.Regions)
                {
                    RenderArgs a = new RenderArgs();
                    a.OutputFile     = String.Format("{0}[{1}]{2}", filename, idx++, ri.Extension);
                    a.RenderTemplate = ri.Template;
                    a.Start          = region.Position;
                    a.Length         = region.Length;
                    renders.Add(a);
                }
            }
            else
            {
                filename += ri.Extension;
                RenderArgs a = new RenderArgs();
                a.OutputFile     = filename;
                a.RenderTemplate = ri.Template;
                a.UseSelection   = (renderMode == RenderMode.Selection);
                renders.Add(a);
            }
        }

        foreach (RenderArgs a in renders)
        {
            ValidateFilePath(a.OutputFile);
            if (!OverwriteExistingFiles && File.Exists(a.OutputFile))
            {
                DialogResult rs = MessageBox.Show("File(s) exist. Overwrite them?", "Overwrite Files?",
                                                   MessageBoxButtons.OKCancel, MessageBoxIcon.Warning,
                                                   MessageBoxDefaultButton.Button2);
                if (DialogResult.Cancel == rs) return;
                OverwriteExistingFiles = true;
            }
        }

        foreach (RenderArgs a in renders)
            if (RenderStatus.Canceled == DoRender(a)) break;
    }

    RenderStatus DoRender(RenderArgs args)
    {
        RenderStatus status = myVegas.Render(args);
        if (status == RenderStatus.Complete || status == RenderStatus.Canceled) return status;
        StringBuilder msg = new StringBuilder("Render failed:\n");
        msg.Append("\n    File: ").Append(args.OutputFile);
        msg.Append("\n    Template: ").Append(args.RenderTemplate.Name);
        throw new ApplicationException(msg.ToString());
    }

    String FixFileName(String name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
        return name;
    }

    void ValidateFilePath(String filePath)
    {
        if (filePath.Length > 260) throw new ApplicationException("File name too long: " + filePath);
        foreach (char c in Path.GetInvalidPathChars())
            if (filePath.IndexOf(c) >= 0) throw new ApplicationException("Invalid file name: " + filePath);
    }

    class RenderItem
    {
        public readonly Renderer       Renderer;
        public readonly RenderTemplate Template;
        public readonly String         Extension;
        public RenderItem(Renderer r, RenderTemplate t, String e)
        { Renderer = r; Template = t; Extension = (e != null) ? e.TrimStart('*') : null; }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  UI
    // ══════════════════════════════════════════════════════════════════════
    DialogResult ShowBatchRenderDialog()
    {
        float dpiScale = 1.0f;
        using (Graphics gTmp = Graphics.FromHwnd(IntPtr.Zero))
        {
            float d = gTmp.DpiY / 96.0f;
            if (d >= 1.37f) dpiScale = d;
        }

        int W   = (int)(600 * dpiScale);
        int PAD = (int)(12 * dpiScale);
        int SP  = (int)(8  * dpiScale);

        Font fBase  = new Font("Segoe UI", 9f * dpiScale);
        Font fSmall = new Font("Segoe UI", 8f * dpiScale, FontStyle.Bold);

        Form dlg            = new Form();
        dlg.Text            = "Batch Render";
        dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
        dlg.MaximizeBox     = false;
        dlg.MinimizeBox     = false;
        dlg.StartPosition   = FormStartPosition.CenterScreen;
        dlg.Width           = W;
        dlg.BackColor       = VP_BG;
        dlg.ForeColor       = VP_TEXT;
        dlg.Font            = fBase;
        dlg.ShowInTaskbar   = false;
        dlg.FormClosing    += this.HandleFormClosing;

        int titleH = dlg.Height - dlg.ClientSize.Height;
        int y      = PAD;

        // ── Output path ───────────────────────────────────────────────────
        Label lblPath     = new Label();
        lblPath.Text      = "Output base file:";
        lblPath.Left      = PAD;
        lblPath.Top       = y + (int)(3 * dpiScale);
        lblPath.AutoSize  = true;
        lblPath.ForeColor = VP_TEXT_DIM;
        dlg.Controls.Add(lblPath);

        int browseW = (int)(74 * dpiScale);
        int browseH = (int)(24 * dpiScale);

        FileNameBox             = new TextBox();
        FileNameBox.BackColor   = VP_INPUT;
        FileNameBox.ForeColor   = VP_TEXT;
        FileNameBox.BorderStyle = BorderStyle.FixedSingle;
        FileNameBox.Left        = lblPath.Right + SP;
        FileNameBox.Top         = y;
        FileNameBox.Width       = W - PAD - (lblPath.Right + SP) - browseW - SP * 2;
        FileNameBox.Text        = defaultBasePath;
        dlg.Controls.Add(FileNameBox);

        BrowseButton        = MakeButton("Browse…", VP_BTN, VP_BTN_H, fBase);
        BrowseButton.Left   = FileNameBox.Right + SP;
        BrowseButton.Top    = y;
        BrowseButton.Width  = browseW;
        BrowseButton.Height = browseH;
        BrowseButton.Click += new EventHandler(HandleBrowseClick);
        dlg.Controls.Add(BrowseButton);

        y = FileNameBox.Bottom + PAD;
        y = AddDivider(dlg, y, W) + SP;

        // ── Render scope ──────────────────────────────────────────────────
        Label lblScope     = new Label();
        lblScope.Text      = "RENDER SCOPE";
        lblScope.Left      = PAD;
        lblScope.Top       = y;
        lblScope.AutoSize  = true;
        lblScope.Font      = fSmall;
        lblScope.ForeColor = VP_TEXT_SEC;
        dlg.Controls.Add(lblScope);
        y = lblScope.Bottom + SP;

        bool hasSelection = myVegas.SelectionLength.Nanos != 0;
        bool hasRegions   = myVegas.Project.Regions.Count != 0;

        Panel scopeRow     = new Panel();
        scopeRow.Left      = PAD;
        scopeRow.Top       = y;
        scopeRow.Width     = W - PAD * 2;
        scopeRow.Height    = (int)(26 * dpiScale);
        scopeRow.BackColor = VP_BG;
        dlg.Controls.Add(scopeRow);

        RenderProjectButton   = MakeRadio(scopeRow, "Entire Project",  0,                               fBase, true);
        RenderSelectionButton = MakeRadio(scopeRow, "Selection Only",  RenderProjectButton.Right + 24,  fBase, hasSelection);
        RenderRegionsButton   = MakeRadio(scopeRow, "Regions",         RenderSelectionButton.Right + 24, fBase, hasRegions);
        RenderProjectButton.Checked = true;

        y = scopeRow.Bottom + PAD;
        y = AddDivider(dlg, y, W) + SP;

        // ── Templates ─────────────────────────────────────────────────────
        Label lblTpl     = new Label();
        lblTpl.Text      = "RENDER TEMPLATES";
        lblTpl.Left      = PAD;
        lblTpl.Top       = y;
        lblTpl.AutoSize  = true;
        lblTpl.Font      = fSmall;
        lblTpl.ForeColor = VP_TEXT_SEC;
        dlg.Controls.Add(lblTpl);
        y = lblTpl.Bottom + SP;

        TemplateTree             = new TreeView();
        TemplateTree.Left        = PAD;
        TemplateTree.Top         = y;
        TemplateTree.Width       = W - PAD * 2;
        TemplateTree.Height      = (int)(270 * dpiScale);
        TemplateTree.CheckBoxes  = true;
        TemplateTree.BackColor   = VP_INPUT;
        TemplateTree.ForeColor   = VP_TEXT;
        TemplateTree.BorderStyle = BorderStyle.FixedSingle;
        TemplateTree.Font        = new Font("Segoe UI", 8.5f * dpiScale);
        TemplateTree.AfterCheck += new TreeViewEventHandler(HandleTreeViewCheck);
        dlg.Controls.Add(TemplateTree);

        y = TemplateTree.Bottom + PAD;
        y = AddDivider(dlg, y, W) + PAD;

        // ── Buttons ───────────────────────────────────────────────────────
        int btnH = (int)(28 * dpiScale);

        Button cancelBtn       = MakeButton("Cancel", VP_BTN, VP_BTN_H, fBase);
        cancelBtn.Width        = (int)(90 * dpiScale);
        cancelBtn.Height       = btnH;
        cancelBtn.Left         = W - PAD - cancelBtn.Width;
        cancelBtn.Top          = y;
        cancelBtn.DialogResult = DialogResult.Cancel;
        dlg.CancelButton       = cancelBtn;
        dlg.Controls.Add(cancelBtn);

        Button okBtn           = MakeButton("Start Render", VP_OK, VP_OK_H, fBase);
        okBtn.Width            = (int)(100 * dpiScale);
        okBtn.Height           = btnH;
        okBtn.Left             = cancelBtn.Left - PAD - okBtn.Width;
        okBtn.Top              = y;
        okBtn.DialogResult     = DialogResult.OK;
        dlg.AcceptButton       = okBtn;
        dlg.Controls.Add(okBtn);

        dlg.Height = titleH + y + btnH + PAD;

        FillTemplateTree();
        return dlg.ShowDialog(myVegas.MainWindow);
    }

    Button MakeButton(string text, Color normal, Color hover, Font font)
    {
        Button b = new Button();
        b.Text      = text;
        b.Font      = font;
        b.FlatStyle = FlatStyle.Flat;
        b.BackColor = normal;
        b.ForeColor = Color.White;
        b.Cursor    = Cursors.Hand;
        b.FlatAppearance.BorderSize  = 1;
        b.FlatAppearance.BorderColor = Color.FromArgb(
            Math.Max(0, normal.R - 20),
            Math.Max(0, normal.G - 20),
            Math.Max(0, normal.B - 20));
        b.MouseEnter += (s, e) => ((Button)s).BackColor = hover;
        b.MouseLeave += (s, e) => ((Button)s).BackColor = normal;
        return b;
    }

    RadioButton MakeRadio(Panel parent, string text, int left, Font font, bool enabled)
    {
        RadioButton rb = new RadioButton();
        rb.Text      = text;
        rb.Left      = left;
        rb.Top       = (parent.Height - rb.PreferredSize.Height) / 2;
        rb.AutoSize  = true;
        rb.Font      = font;
        rb.ForeColor = enabled ? VP_TEXT : VP_TEXT_DIM;
        rb.Enabled   = enabled;
        rb.FlatStyle = FlatStyle.Flat;
        parent.Controls.Add(rb);
        return rb;
    }

    int AddDivider(Form dlg, int y, int width)
    {
        Panel div     = new Panel();
        div.Left      = 0;
        div.Top       = y;
        div.Width     = width;
        div.Height    = 1;
        div.BackColor = VP_BORDER;
        dlg.Controls.Add(div);
        return div.Bottom;
    }

    // ── Template tree ──────────────────────────────────────────────────────
    void FillTemplateTree()
    {
        int chCount = 0;
        if      (AudioBusMode.Stereo  == myVegas.Project.Audio.MasterBusMode) chCount = 2;
        else if (AudioBusMode.Surround == myVegas.Project.Audio.MasterBusMode) chCount = 6;

        bool hasVideo = ProjectHasVideo();
        bool hasAudio = ProjectHasAudio();

        // ── FIX: Stereo3DMode / Stereo3DOutputMode removed in Vegas Pro 2026 ──
        int videoStreams = hasVideo ? 1 : 0;
        // ─────────────────────────────────────────────────────────────────────

        foreach (Renderer renderer in myVegas.Renderers)
        {
            try
            {
                TreeNode rendNode = new TreeNode(renderer.FileTypeName);
                rendNode.Tag      = new RenderItem(renderer, null, null);

                foreach (RenderTemplate template in renderer.Templates)
                {
                    try
                    {
                        if (!template.IsValid())                                                           continue;
                        if (!hasVideo && template.VideoStreamCount > 0)                                   continue;
                        if (hasVideo  && videoStreams < template.VideoStreamCount)                        continue;
                        if (template.TemplateID == 0 && !AllowDefaultTemplates(renderer.ClassID))        continue;
                        if (!hasAudio && template.VideoStreamCount == 0 && template.AudioStreamCount > 0) continue;
                        if (chCount < template.AudioChannelCount)                                         continue;
                        String[] exts = template.FileExtensions;
                        if (exts.Length != 1) continue;

                        TreeNode tn = new TreeNode(template.Name);
                        tn.Tag      = new RenderItem(renderer, template, exts[0]);
                        rendNode.Nodes.Add(tn);
                    }
                    catch { }
                }

                if (rendNode.Nodes.Count == 0) continue;
                if (rendNode.Nodes.Count == 1 && ((RenderItem)rendNode.Nodes[0].Tag).Template.Index == 0) continue;
                TemplateTree.Nodes.Add(rendNode);
            }
            catch { }
        }
    }

    bool ProjectHasVideo() { foreach (Track t in myVegas.Project.Tracks) if (t.IsVideo()) return true; return false; }
    bool ProjectHasAudio() { foreach (Track t in myVegas.Project.Tracks) if (t.IsAudio()) return true; return false; }

    void UpdateSelectedTemplates()
    {
        SelectedTemplates.Clear();
        foreach (TreeNode node in TemplateTree.Nodes)
            foreach (TreeNode child in node.Nodes)
                if (child.Checked) SelectedTemplates.Add(child.Tag);
    }

    void HandleBrowseClick(Object sender, EventArgs args)
    {
        SaveFileDialog sfd = new SaveFileDialog();
        sfd.Filter          = "All Files (*.*)|*.*";
        sfd.CheckPathExists = true;
        sfd.AddExtension    = false;
        if (FileNameBox != null)
        {
            String dir = Path.GetDirectoryName(FileNameBox.Text);
            if (Directory.Exists(dir)) sfd.InitialDirectory = dir;
            sfd.DefaultExt = Path.GetExtension(FileNameBox.Text);
            sfd.FileName   = Path.GetFileNameWithoutExtension(FileNameBox.Text);
        }
        if (DialogResult.OK == sfd.ShowDialog() && FileNameBox != null)
            FileNameBox.Text = Path.GetFullPath(sfd.FileName);
    }

    void HandleTreeViewCheck(object sender, TreeViewEventArgs args)
    {
        if (args.Action != TreeViewAction.ByKeyboard && args.Action != TreeViewAction.ByMouse) return;
        if (args.Node.Nodes.Count > 0)
        {
            SetChildrenChecked(args.Node, args.Node.Checked);
        }
        else if (args.Node.Parent != null)
        {
            if  (args.Node.Checked && !args.Node.Parent.Checked)
                args.Node.Parent.Checked = true;
            else if (!args.Node.Checked && args.Node.Parent.Checked && !AnyChildrenChecked(args.Node.Parent))
                args.Node.Parent.Checked = false;
        }
    }

    void HandleFormClosing(Object sender, FormClosingEventArgs args)
    {
        Form dlg = sender as Form;
        if (null == dlg || dlg.DialogResult != DialogResult.OK) return;

        String outPath = FileNameBox.Text;
        try   { if (!Directory.Exists(Path.GetDirectoryName(outPath))) throw new Exception(); }
        catch
        {
            MessageBox.Show(dlg, "The output directory does not exist.\nUse Browse to choose a valid path.",
                "Invalid Directory", MessageBoxButtons.OK, MessageBoxIcon.Error);
            args.Cancel = true; return;
        }
        try
        {
            String fn = Path.GetFileName(outPath);
            if (String.IsNullOrEmpty(fn) || fn.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new Exception();
        }
        catch
        {
            MessageBox.Show(dlg, "The base file name is invalid.\nMake sure it contains valid characters.",
                "Invalid File Name", MessageBoxButtons.OK, MessageBoxIcon.Error);
            args.Cancel = true; return;
        }
        UpdateSelectedTemplates();
        if (SelectedTemplates.Count == 0)
        {
            MessageBox.Show(dlg, "No render templates selected.\nCheck one or more templates from the list.",
                "No Templates Selected", MessageBoxButtons.OK, MessageBoxIcon.Error);
            args.Cancel = true;
        }
    }

    void SetChildrenChecked(TreeNode node, bool check)
    { foreach (TreeNode c in node.Nodes) if (c.Checked != check) c.Checked = check; }

    bool AnyChildrenChecked(TreeNode node)
    { foreach (TreeNode c in node.Nodes) if (c.Checked) return true; return false; }

    static readonly Guid[] TheDefaultTemplateRenderClasses =
    {
        Renderer.CLSID_SfWaveRenderClass,
        Renderer.CLSID_SfW64ReaderClass,
        Renderer.CLSID_CSfAIFRenderFileClass,
        Renderer.CLSID_CSfFLACRenderFileClass,
        Renderer.CLSID_CSfPCARenderFileClass,
    };

    bool AllowDefaultTemplates(Guid id)
    { foreach (Guid g in TheDefaultTemplateRenderClasses) if (g == id) return true; return false; }
}
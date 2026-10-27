using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ASU.Localization;
using ASU.Gpu;

namespace ASU.UI
{
    public partial class OptionsForm : Form
    {
        private const string STR_FORM_TITLE = "Alferd Spritesheet Unpacker ver.{0} Options";
        private const string STR_ADVANCED_EXPORT_FILE_FORMAT = "<Advanced>";

        private System.Windows.Forms.Label LanguageLabel;
        private System.Windows.Forms.ComboBox LanguageComboBox;
        private System.Windows.Forms.Label AlgorithmLabel;
        private System.Windows.Forms.ComboBox AlgorithmComboBox;
        private BackgroundDetectionMethod initialAlgorithm;

        public MainForm Main;

        private void OptionsForm_Load(object sender, System.EventArgs e)
        {
            try
            {
                this.SetupAlgorithmSelector();
                this.ApplyLocalisableText();
                this.SetupLanguageSelector();

                this.Text = String.Format(Localisation.T("OptionsFormTitleFormat"), ForkandBeard.Logic.Names.GetApplicationMajorVersion());
                this.CommandLineHelpLink.Text = String.Format(Localisation.T("CommandLineHelpLinkText"), System.IO.Path.GetFileName(System.Configuration.ConfigurationManager.AppSettings["ThirdPartyImageConverter"]));

                if (this.SelectAllOrderComboBox.SelectedIndex == -1)
                {
                    this.SelectAllOrderComboBox.SelectedIndex = 0;
                }

                this.ExportFormatComboBox.Items.Clear();
                this.ExportFormatComboBox.Items.Add(System.Drawing.Imaging.ImageFormat.Png);
                this.ExportFormatComboBox.Items.Add(System.Drawing.Imaging.ImageFormat.Bmp);
                this.ExportFormatComboBox.Items.Add(System.Drawing.Imaging.ImageFormat.Gif);
                this.ExportFormatComboBox.Items.Add(System.Drawing.Imaging.ImageFormat.Tiff);
                this.ExportFormatComboBox.Items.Add(System.Drawing.Imaging.ImageFormat.Jpeg);
                this.ExportFormatComboBox.Items.Add(STR_ADVANCED_EXPORT_FILE_FORMAT);
                this.CommandLineLabel.Text = this.CreateCommandLineArgs();

                if (this.Main != null)
                {
                    this.ExportBGTransparentCheckBox.Checked = MainForm.MakeBackgroundTransparent;
                    this.SelectedColourPanel.BackColor = MainForm.SelectedFill.Color;
                    this.TileBorderColourPanel.BackColor = MainForm.Outline.Color;
                    this.HoverColourPanel.BackColor = MainForm.HoverFill.Color;
                    this.DistanceBetweenTilesUpDown.Value = MainForm.DistanceBetweenTiles;
                    this.OutlineWidthUpDown.Value = Convert.ToDecimal(MainForm.Outline.Width);
                    this.PreservePalletteCheckBox.Checked = MainForm.PreservePallette;
                    this.ExportPaletteCheckBox.Checked = MainForm.ExportPalette;
                    this.PaletteTransparencyCheckBox.Checked = MainForm.PaletteTransparency;
                    this.FlipPaletteCheckBox.Checked = MainForm.FlipPalette;

                    if (MainForm.ExportFormat != null)
                    {
                        this.ExportFormatComboBox.SelectedItem = MainForm.ExportFormat;
                    }
                    else
                    {
                        this.ExportFormatComboBox.Text = STR_ADVANCED_EXPORT_FILE_FORMAT;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Localisation.T("msg.ErrorDuringOptionLoadTitle"));
            }
        }

        private void SetupAlgorithmSelector()
        {
            this.AlgorithmLabel = new System.Windows.Forms.Label();
            this.AlgorithmLabel.Text = Localisation.T("AlgorithmLabel");
            this.AlgorithmLabel.Name = "AlgorithmLabel";
            this.AlgorithmLabel.AutoSize = false;
            this.AlgorithmLabel.Size = new System.Drawing.Size(52, 22);
            this.AlgorithmLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.AlgorithmLabel.Location = new System.Drawing.Point(4, 378);
            this.Controls.Add(this.AlgorithmLabel);

            this.AlgorithmComboBox = new System.Windows.Forms.ComboBox();
            this.AlgorithmComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this.AlgorithmComboBox.Name = "AlgorithmComboBox";
            this.AlgorithmComboBox.Location = new System.Drawing.Point(58, 375);
            this.AlgorithmComboBox.Size = new System.Drawing.Size(180, 24);
            this.AlgorithmComboBox.SelectedIndexChanged += new System.EventHandler(this.AlgorithmComboBox_SelectedIndexChanged);
            this.Controls.Add(this.AlgorithmComboBox);

            this.PopulateAlgorithmItems();
            this.AlgorithmComboBox.SelectedIndex = IndexOfMethod(MainForm.BackgroundMethod);
            this.initialAlgorithm = MethodOfIndex(this.AlgorithmComboBox.SelectedIndex);
        }

        private void PopulateAlgorithmItems()
        {
            foreach (string option in Localisation.T("AlgorithmComboBox").Split('|'))
            {
                this.AlgorithmComboBox.Items.Add(option);
            }
        }

        private static BackgroundDetectionMethod MethodOfIndex(int index)
        {
            switch (index)
            {
                case 1:
                    return BackgroundDetectionMethod.TopLeftColour;
                case 2:
                    return BackgroundDetectionMethod.BorderConnectedBackground;
                default:
                    return BackgroundDetectionMethod.MostVotingColour;
            }
        }

        private static int IndexOfMethod(BackgroundDetectionMethod method)
        {
            switch (method)
            {
                case BackgroundDetectionMethod.TopLeftColour:
                    return 1;
                case BackgroundDetectionMethod.BorderConnectedBackground:
                    return 2;
                default:
                    return 0;
            }
        }

        private void AlgorithmComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.AlgorithmComboBox.SelectedIndex < 0) return;
            MainForm.WriteBackgroundMethodSetting(MethodOfIndex(this.AlgorithmComboBox.SelectedIndex));
        }

        private void SetupLanguageSelector()
        {
            this.LanguageLabel = new System.Windows.Forms.Label();
            this.LanguageLabel.Text = Localisation.T("LanguageLabel");
            this.LanguageLabel.AutoSize = false;
            this.LanguageLabel.Size = new System.Drawing.Size(50, 22);
            this.LanguageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.LanguageLabel.Location = new System.Drawing.Point(58, 349);
            this.LanguageLabel.Name = "LanguageLabel";
            this.Controls.Add(this.LanguageLabel);

            this.LanguageComboBox = new System.Windows.Forms.ComboBox();
            this.LanguageComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (string language in Localisation.Languages)
            {
                this.LanguageComboBox.Items.Add(Localisation.GetLanguageDisplayName(language));
            }
            this.LanguageComboBox.SelectedIndex = this.LanguageComboBox.Items.IndexOf(Localisation.GetLanguageDisplayName(Localisation.Current));
            this.LanguageComboBox.Location = new System.Drawing.Point(110, 346);
            this.LanguageComboBox.Size = new System.Drawing.Size(120, 24);
            this.LanguageComboBox.Name = "LanguageComboBox";
            this.LanguageComboBox.SelectedIndexChanged += new System.EventHandler(this.LanguageComboBox_SelectedIndexChanged);
            this.Controls.Add(this.LanguageComboBox);
        }

        private void LanguageComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int selected = this.LanguageComboBox.SelectedIndex;
            if (selected < 0 || selected >= Localisation.Languages.Length) return;

            string chosen = Localisation.Languages[selected];

            Localisation.SetCurrent(chosen);

            if (this.Main != null)
            {
                this.Main.ApplyLocalisableText();
            }
            this.ApplyLocalisableText();
            this.LanguageComboBox.SelectedIndex = this.LanguageComboBox.Items.IndexOf(Localisation.GetLanguageDisplayName(Localisation.Current));
        }

        private void ApplyLocalisableText()
        {
            Localisation.Apply(this);

            this.Text = String.Format(Localisation.T("OptionsFormTitleFormat"), ForkandBeard.Logic.Names.GetApplicationMajorVersion());
            this.CommandLineHelpLink.Text = String.Format(Localisation.T("CommandLineHelpLinkText"), System.IO.Path.GetFileName(System.Configuration.ConfigurationManager.AppSettings["ThirdPartyImageConverter"]));
            if (this.LanguageLabel != null) this.LanguageLabel.Text = Localisation.T("LanguageLabel");
        }

        private void SelectedColourButton_Click(System.Object sender, System.EventArgs e)
        {
            this.ColorDialog1.Color = this.SelectedColourPanel.BackColor;
            this.ColorDialog1.ShowDialog();
            this.SelectedColourPanel.BackColor = Color.FromArgb(200, this.ColorDialog1.Color);
        }

        private void TileBorderColourButton_Click(System.Object sender, System.EventArgs e)
        {
            this.ColorDialog1.Color = this.TileBorderColourPanel.BackColor;
            this.ColorDialog1.ShowDialog();
            this.TileBorderColourPanel.BackColor = this.ColorDialog1.Color;
        }

        private void HoverColourButton_Click(System.Object sender, System.EventArgs e)
        {
            this.ColorDialog1.Color = this.HoverColourPanel.BackColor;
            this.ColorDialog1.ShowDialog();
            this.HoverColourPanel.BackColor = Color.FromArgb(150, this.ColorDialog1.Color);
        }

        private void CloseButton_Click(System.Object sender, System.EventArgs e)
        {
            try
            {
                if (this.Main != null)
                {
                    bool reloadNeeded = false;

                    MainForm.SelectedFill = new SolidBrush(Color.FromArgb(MainForm.SelectedFill.Color.A, this.SelectedColourPanel.BackColor));
                    MainForm.ZoomPen = new Pen(Color.FromArgb(MainForm.ZoomPen.Color.A, this.SelectedColourPanel.BackColor), MainForm.ZoomPen.Width);
                    MainForm.HoverFill = new SolidBrush(Color.FromArgb(MainForm.HoverFill.Color.A, this.HoverColourPanel.BackColor));
                    MainForm.Outline = new Pen(Color.FromArgb(MainForm.Outline.Color.A, this.TileBorderColourPanel.BackColor), (float)this.OutlineWidthUpDown.Value);
                    MainForm.PromptForDestinationFolder = this.PromptDestinationFolderCheckBox.Checked;
                    MainForm.AutoOpenDestinationFolder = this.OpenExportedDestinationCheckBox.Checked;
                    MainForm.MakeBackgroundTransparent = this.ExportBGTransparentCheckBox.Checked;
                    MainForm.PreservePallette = this.PreservePalletteCheckBox.Checked;
                    MainForm.ExportPalette = this.ExportPaletteCheckBox.Checked;
                    MainForm.PaletteTransparency = this.PaletteTransparencyCheckBox.Checked;
                    MainForm.FlipPalette = this.FlipPaletteCheckBox.Checked;

                    if (this.ExportFormatComboBox.Text != STR_ADVANCED_EXPORT_FILE_FORMAT)
                    {
                        MainForm.ExportNConvertArgs = string.Empty;
                        MainForm.ExportFormat = (System.Drawing.Imaging.ImageFormat)this.ExportFormatComboBox.SelectedItem;
                    }
                    else
                    {
                        MainForm.ExportFormat = null;
                        MainForm.ExportNConvertArgs = this.CreateCommandLineArgs();
                    }

                    if (MainForm.DistanceBetweenTiles != this.DistanceBetweenTilesUpDown.Value)
                    {
                        MainForm.DistanceBetweenTiles = Convert.ToInt32(this.DistanceBetweenTilesUpDown.Value);
                        reloadNeeded = true;
                    }

                    if (MainForm.BackgroundMethod != this.initialAlgorithm)
                    {
                        // Background detection changed; re-unpack the current sheet.
                        reloadNeeded = true;
                    }

                    MainForm.SheetWithBoxes = null;
                    if (reloadNeeded)
                    {
                        this.Main.ReloadOriginal();
                    }
                    else
                    {
                        this.Main.MainPanel.Refresh();
                    }
                }
                this.Hide();
            }
            catch (Exception ex)
            {
                ForkandBeard.Logic.ExceptionHandler.HandleException(ex, "cat@forkandbeard.co.uk");
            }
        }

        private void AboutButton_Click(System.Object sender, System.EventArgs e)
        {
            AboutForm aboutForm = new AboutForm();
            aboutForm.ShowDialog();
        }

        private void ExportFormatComboBox_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            this.NConvertArgsTextBox.Enabled = this.ExportFormatComboBox.Text == STR_ADVANCED_EXPORT_FILE_FORMAT;
            this.ShowCommandLineArgsCheckBox.Enabled = this.NConvertArgsTextBox.Enabled;
            this.CommandLineHelpLink.Visible = this.NConvertArgsTextBox.Enabled && this.ShowCommandLineArgsCheckBox.Checked;
            this.CommandLineLabel.Visible = this.NConvertArgsTextBox.Enabled && this.ShowCommandLineArgsCheckBox.Checked;

            if (this.ExportFormatComboBox.Text == STR_ADVANCED_EXPORT_FILE_FORMAT)
            {
                this.ExportBGTransparentCheckBox.Checked = false;
            }
            else
            {
                this.ShowCommandLineArgsCheckBox.Checked = false;
            }
        }

        private void NConvertArgsTextBox_TextChanged(System.Object sender, System.EventArgs e)
        {
            this.CommandLineLabel.Text = this.CreateCommandLineArgs();
        }

        private string CreateCommandLineArgs()
        {
            string returnString;

            returnString = System.Configuration.ConfigurationManager.AppSettings["ThirdPartyImageConverterCommandArgsExportFormat"].Replace("{text}", this.NConvertArgsTextBox.Text);

            return returnString;
        }

        private void ShowCommandLineArgsCheckBox_CheckedChanged(System.Object sender, System.EventArgs e)
        {
            this.CommandLineLabel.Visible = this.ShowCommandLineArgsCheckBox.Checked;
            this.CommandLineHelpLink.Visible = this.ShowCommandLineArgsCheckBox.Checked;

            if (this.ShowCommandLineArgsCheckBox.Checked)
            {
                this.ExportPanel.Height += 91;
                this.Height += 89;
            }
            else
            {
                this.ExportPanel.Height -= 91;
                this.Height -= 89;
            }
        }

        private void CommandLineHelpLink_LinkClicked(System.Object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
        {
            string thirdPartyImageConverterHelpFile = null;

            thirdPartyImageConverterHelpFile = System.Configuration.ConfigurationManager.AppSettings["ThirdPartyImageConverterHelpFile"];
            if (thirdPartyImageConverterHelpFile.StartsWith("\\"))
            {
                thirdPartyImageConverterHelpFile = AppDomain.CurrentDomain.BaseDirectory + thirdPartyImageConverterHelpFile;
            }
            System.Diagnostics.Process.Start(thirdPartyImageConverterHelpFile);
        }

        private void DownloadLink_LinkClicked(System.Object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/ForkandBeard/Alferd-Spritesheet-Unpacker/releases");
        }

        private void HelpLink_LinkClicked(System.Object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://www.alferdspritesheetunpacker.forkandbeard.co.uk/ForkandBeard/apps/AlferdSpritesheetUnpacker/FAQ.aspx?app=Alferd");
        }

        private void PreservePalletteCheckBox_CheckedChanged(System.Object sender, System.EventArgs e)
        {
            if (this.PreservePalletteCheckBox.Checked)
            {
                this.ExportBGTransparentCheckBox.Checked = false;
                this.ExportBGTransparentCheckBox.Enabled = false;
            }
        }
    }
}

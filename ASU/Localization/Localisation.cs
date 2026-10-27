using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace ASU.Localization
{
    /// <summary>
    /// Lightweight language-pack system. Provides two built-in packs:
    ///   en-US  - the original (English) strings, treated as the reference pack
    ///   zh-CN  - Simplified Chinese translations
    /// Controls and messages are localised via key -> text lookup. Control text is
    /// applied at runtime by walking the control tree and matching each control by
    /// Name, so the Designer.x / .resx English literals remain the fallback.
    /// </summary>
    public static class Localisation
    {
        public const string Default = "en-US";
        public const string ChineseSimplified = "zh-CN";

        public static event EventHandler LanguageChanged;

        private static string _current;

        public static string[] Languages => new[] { Default, ChineseSimplified };

        public static string GetLanguageDisplayName(string code)
        {
            switch (code)
            {
                case ChineseSimplified: return "简体中文 (zh-CN)";
                case Default: return "English (en-US)";
                default: return code;
            }
        }

        public static string Current
        {
            get
            {
                if (_current == null)
                {
                    _current = ReadSaved();
                    if (Array.IndexOf(Languages, _current) < 0) _current = Default;
                }
                return _current;
            }
        }

        public static bool IsEnglish => Current == Default;

        public static void SetCurrent(string language)
        {
            if (Array.IndexOf(Languages, language) < 0) language = Default;
            if (_current == language) return;
            _current = language;
            try { WriteSaved(language); } catch { /* ignore persistence failures */ }
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>Returns the localised text for <paramref name="key"/>.</summary>
        public static string T(string key, params object[] args)
        {
            string template = Lookup(key);
            if (template == null) return key;
            if (args == null || args.Length == 0) return template;
            try { return string.Format(template, args); }
            catch { return template; }
        }

        /// <summary>Applies localised control text by walking the control tree by Name.</summary>
        public static void Apply(Control root)
        {
            if (root == null) return;
            ApplyControl(root);
            foreach (Control child in root.Controls) Apply(child);
        }

        private static void ApplyControl(Control c)
        {
            if (string.IsNullOrEmpty(c.Name)) return;

            if (c is ComboBox && ((ComboBox)c).Items.Count > 0)
            {
                string list = Lookup(c.Name);
                if (list != null)
                {
                    int index = ((ComboBox)c).SelectedIndex;
                    ((ComboBox)c).Items.Clear();
                    ((ComboBox)c).Items.AddRange(list.Split('|'));
                    if (index >= 0 && index < ((ComboBox)c).Items.Count) ((ComboBox)c).SelectedIndex = index;
                }
                return;
            }

            if (c is ComboBox && ((ComboBox)c).DropDownStyle == ComboBoxStyle.DropDownList)
            {
                // DropDownList with no items; leave empty once populated at runtime.
                return;
            }

            string text = Lookup(c.Name) ?? Lookup(c.Name + ".Text");
            if (text != null) c.Text = text;
        }

        private static string Lookup(string key)
        {
            if (Packs.TryGetValue(Current, out Dictionary<string, string> pack))
            {
                if (pack.TryGetValue(key, out string v)) return v;
            }
            if (Packs.TryGetValue(Default, out Dictionary<string, string> english))
            {
                if (english.TryGetValue(key, out string v)) return v;
            }
            return null;
        }

        private static Dictionary<string, Dictionary<string, string>> _packs;
        private static Dictionary<string, Dictionary<string, string>> Packs
        {
            get
            {
                if (_packs == null)
                {
                    _packs = new Dictionary<string, Dictionary<string, string>>
                    {
                        { Default, English },
                        { ChineseSimplified, SimplifiedChinese },
                    };
                }
                return _packs;
            }
        }

        private static string ReadSaved()
        {
            try
            {
                string file = ConfigFile();
                if (File.Exists(file)) return File.ReadAllText(file).Trim();
            }
            catch { }
            try
            {
                string setting = System.Configuration.ConfigurationManager.AppSettings["Language"];
                if (!string.IsNullOrEmpty(setting)) return setting;
            }
            catch { }
            return Default;
        }

        private static void WriteSaved(string language)
        {
            string file = ConfigFile();
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, language);
        }

        private static string ConfigFile()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AlferdSpritesheetUnpacker", "language.txt");
        }

        private static bool Contains_(this string[] source, string value)
        {
            foreach (string s in source) if (s == value) return true;
            return false;
        }

        #region en-US pack (reference)
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            // Form titles
            { "MainForm.Text", "Alferd Spritesheet Unpacker" },
            { "OptionsForm.Text", "Options" },
            { "FormTitleFormat", "Alferd Spritesheet Unpacker with some tweaks ver.{0} {1}" },
            { "OptionsFormTitleFormat", "Alferd Spritesheet Unpacker with some tweaks ver.{0} Options" },
            { "CommandLineHelpLinkText", "{0} command line help file" },

            // MainForm controls
            { "SelectAllButton", "Select All" },
            { "DeSelectAllButton", "De-select All" },
            { "ExportLocationLabel", "export location:" },
            { "SplitFrameCheckBoxButton", "Split " },
            { "SplitFrameCheckBoxButtonSelect", "Select" },
            { "SplitFrameCheckBoxButtonSplit", "Split" },
            { "PasteButton", "Paste Image" },
            { "ReloadButton", "Reload" },
            { "ExportSelectedButton", "Export Selected..." },
            { "OptionsButton", "Options..." },
            { "CombineButton", "Combine Selected" },
            { "DragAndDropLabel", "Drag and drop image(s) here" },
            { "SetBackgroundButton", "Set Background..." },
            { "RefineSplitButton", "Refine Split" },
            { "ExportPaletteButton", "Export Palette" },
            { "ExportPaletteCheckBox", "Export Palette (.act)" },
            { "PaletteTransparencyCheckBox", "Include Transparency" },
            { "FlipPaletteCheckBox", "Flip Palette" },
            { "ExportPrefixLabel", "Prefix:" },
            { "ExportPrefixPlaceholder", "Add prefix..." },
            { "ZoomInButton", "Zoom In" },
            { "ZoomOutButton", "Zoom Out" },
            { "ControlsHelpLabel.Text", "[left click]          select or split frame\n[right click]         toggle select / split mode\n[ctrl + left drag]    select multiple frames\n[cursor keys]         move pointer by single pixel\n[esc]                 unload current sprite sheet" },

            // OptionsForm controls
            { "CloseButton", "Update and Close" },
            { "OutlineWidthLabel", "Tile Outline Width" },
            { "DistanceBetweenTilesLabel", "Distance Between Frames" },
            { "AboutButton", "about..." },
            { "ExportFormatLabel", "File Format" },
            { "Label1", "Advanced File Format" },
            { "ShowCommandLineArgsCheckBox", "Show Advanced Command Line" },
            { "CommandLineHelpLink", "command line help file" },
            { "GroupBox1", "Frame Settings" },
            { "GroupBox2", "Colours" },
            { "SelectedColourButton", "Selected Colour" },
            { "TileBorderColourButton", "Tile Border" },
            { "HoverColourButton", "Hover Colour" },
            { "ExportPanel", "Export Options" },
            { "PreservePalletteCheckBox", "Preserve Pallette" },
            { "ExportBGTransparentCheckBox", "Make Background Transparent" },
            { "DownloadLink", "Download latest version" },
            { "SelectAllOrderComboBox", "Top Left|Bottom Left|Centre" },
            { "Label2", "Select All Order" },
            { "PromptDestinationFolderCheckBox", "Prompt for Export Folder" },
            { "OpenExportedDestinationCheckBox", "Open Folder Exported to" },
            { "HelpLink", "help" },
            { "LanguageLabel", "Language:" },
            { "AlgorithmLabel", "Algorithm:" },
            { "AlgorithmComboBox", "Most Common Colour|Top-Left Colour|Border-Connected Fill" },

            // Messages
            { "msg.SelectBoxesFirst", "Select one or more boxes first (click a box, or Ctrl+drag to select many)." },
            { "msg.ConfirmMultipleUnpack", "You are about to Unpack and automatically export {0} spritesheets to [{1}]. Are you sure you want to continue?" },
            { "msg.ConfirmMultipleUnpackTitle", "Confirm multiple Unpack" },
            { "msg.ThirdPartyWarningImport", "You are trying to load a sprite sheet in a non-standard image file format. This file will be converted to a common image format first using the third party command line tool '{0}'.{1}{1}Your operating system may request confirmation to execute {0}.{1}{1}If you'd like to use a different conversion utility then please do so by editing the 'ASU.dll.config' file found here:{1}{2}{1}{1}Do you want to continue and use '{0}'?" },
            { "msg.ThirdPartyWarningExport", "You are about to export an advanced image file format. This file will be converted to a to common file format first and then converted to the advanced image file format by the third party command line tool '{0}'.{1}{1}Your operating system may request confirmation to execute {0}.{1}{1}If you'd like to use a different conversion utility then please do so by editing the 'ASU.dll.config' file found here:{1}{2}{1}{1}Do you want to continue and use '{0}'?" },
            { "msg.ThirdPartyWarningTitle", "Third Party Converter Warning" },
            { "msg.NotAnImageFile", "File is not an image file format supported by ASU. " },
            { "msg.UnableToImportTitle", "Unable to import file" },
            { "msg.ErrorMouseOverTitle", "Error during mouse over" },
            { "msg.NoSheetCombine", "No spritesheet loaded. Please drop a spritesheet onto this form and select some frames before trying to combine frames." },
            { "msg.NoFramesCombine", "No frames selected. Please select some frames before trying to combine." },
            { "msg.NoSheetExport", "No spritesheet loaded. Please drop a spritesheet onto this form and select some frames before exporting." },
            { "msg.NoFramesExport", "No frames selected. Please select some frames before exporting." },
            { "msg.ExportAbort", "Export of frames is taking a while. Do you want to abort?" },
            { "msg.ExportAbortTitle", "Excessive Export Time" },
            { "msg.ExportConvertFailed", "Exported files failed to be converted.{3}{3}Arguments used:{3}{0}{3}{3}Please see '{1}' documentation at {3}[{2}]." },
            { "msg.NoImageLoaded", "No image loaded. First drag an image onto the form above, or use the paste 'From Clipboard' button." },
            { "msg.PaletteExportDone", "Palette saved to:{1}{0}" },
            { "msg.PaletteExportDoneTitle", "Palette exported" },
            { "msg.NoImageInClipboard", "No image found in Clipboard" },
            { "msg.ErrorLoadingOptionsTitle", "Error loading options" },
            { "msg.ErrorDuringOptionLoadTitle", "Error during option load" },
            { "msg.ErrorDuringTimer", ". CheckForUnpackFinishTimer has been stopped, you may need to restart application" },
            { "msg.ErrorDuringTimerTitle", "Error during timer" },
            { "msg.AboutFeedback", "Any bugs, suggestions, feedback can be left @ https://github.com/ForkandBeard/Alferd-Spritesheet-Unpacker/issues or tweet me up @ForkandBeard" },
        };
        #endregion

        #region zh-CN pack (Simplified Chinese)
        private static readonly Dictionary<string, string> SimplifiedChinese = new Dictionary<string, string>
        {
            // Form titles
            { "MainForm.Text", "Alferd Spriteheet Unpacker" },
            { "OptionsForm.Text", "设置" },
            { "FormTitleFormat", "Alferd Spritesheet Unpacker with some tweaks ver.{0} {1}" },
            { "OptionsFormTitleFormat", "Alferd Spritesheet Unpacker with some tweaks ver.{0} 设置" },
            { "CommandLineHelpLinkText", "{0} 命令行帮助文件" },

            // MainForm controls
            { "SelectAllButton", "全选" },
            { "DeSelectAllButton", "取消全选" },
            { "ExportLocationLabel", "导出位置：" },
            { "SplitFrameCheckBoxButton", "拆分" },
            { "SplitFrameCheckBoxButtonSelect", "选择" },
            { "SplitFrameCheckBoxButtonSplit", "拆分" },
            { "PasteButton", "粘贴图片" },
            { "ReloadButton", "重新加载" },
            { "ExportSelectedButton", "导出所选..." },
            { "OptionsButton", "设置..." },
            { "CombineButton", "合并所选" },
            { "DragAndDropLabel", "将图片拖到此处以拆分" },
            { "SetBackgroundButton", "设置背景色..." },
            { "RefineSplitButton", "细化拆分" },
            { "ExportPaletteButton", "导出调色板" },
            { "ExportPaletteCheckBox", "导出调色板 (.act)" },
            { "PaletteTransparencyCheckBox", "包含透明通道" },
            { "FlipPaletteCheckBox", "翻转调色板" },
            { "ExportPrefixLabel", "前缀：" },
            { "ExportPrefixPlaceholder", "添加前缀..." },
            { "ZoomInButton", "放大" },
            { "ZoomOutButton", "缩小" },
            { "ControlsHelpLabel.Text", "[左键单击]        选择或拆分精灵\n[右键单击]        切换选择/拆分模式\n[ctrl+左键拖拽]   多选精灵\n[方向键]          逐像素移动指针\n[esc]             卸载当前精灵表" },

            // OptionsForm controls
            { "CloseButton", "更新并关闭" },
            { "OutlineWidthLabel", "框线宽度" },
            { "DistanceBetweenTilesLabel", "帧间距" },
            { "AboutButton", "关于..." },
            { "ExportFormatLabel", "文件格式" },
            { "Label1", "高级文件格式" },
            { "ShowCommandLineArgsCheckBox", "显示高级命令行" },
            { "CommandLineHelpLink", "命令行帮助文件" },
            { "GroupBox1", "帧设置" },
            { "GroupBox2", "颜色" },
            { "SelectedColourButton", "选中颜色" },
            { "TileBorderColourButton", "框线颜色" },
            { "HoverColourButton", "悬停颜色" },
            { "ExportPanel", "导出选项" },
            { "PreservePalletteCheckBox", "保留调色板" },
            { "ExportBGTransparentCheckBox", "使背景透明" },
            { "DownloadLink", "下载最新版本" },
            { "SelectAllOrderComboBox", "左上|左下|居中" },
            { "Label2", "全选顺序" },
            { "PromptDestinationFolderCheckBox", "导出前询问文件夹" },
            { "OpenExportedDestinationCheckBox", "打开导出文件夹" },
            { "HelpLink", "帮助" },
            { "LanguageLabel", "语言：" },
            { "AlgorithmLabel", "算法：" },
            { "AlgorithmComboBox", "最多颜色法|左上角颜色法|边界连通填充法" },

            // Messages
            { "msg.SelectBoxesFirst", "请先选择一个或多个框（单击选择，或按住 Ctrl 拖动多选）。" },
            { "msg.ConfirmMultipleUnpack", "即将解包并自动导出 {0} 张精灵表到 [{1}]。是否继续？" },
            { "msg.ConfirmMultipleUnpackTitle", "确认多个解包" },
            { "msg.ThirdPartyWarningImport", "您正在尝试加载非标准图片格式的精灵表。该文件将先由第三方命令行工具 '{0}' 转换为常见图片格式。{1}{1}您的操作系统可能会要求确认执行 {0}。{1}{1}如果您希望使用其他转换工具，请编辑此处的 'ASU.dll.config' 文件:{1}{2}{1}{1}是否继续并使用 '{0}'？" },
            { "msg.ThirdPartyWarningExport", "您即将导出高级图片文件格式。该文件将先转换为常见文件格式，再由第三方命令行工具 '{0}' 转换为高级图片文件格式。{1}{1}您的操作系统可能会要求确认执行 {0}。{1}{1}如果您希望使用其他转换工具，请编辑此处的 'ASU.dll.config' 文件:{1}{2}{1}{1}是否继续并使用 '{0}'？" },
            { "msg.ThirdPartyWarningTitle", "第三方转换工具警告" },
            { "msg.NotAnImageFile", "文件不是 ASU 支持的图片格式。 " },
            { "msg.UnableToImportTitle", "无法导入文件" },
            { "msg.ErrorMouseOverTitle", "鼠标悬停时出错" },
            { "msg.NoSheetCombine", "未加载精灵表。请先将精灵表拖到本窗口并选择一些精灵后再尝试合并。" },
            { "msg.NoFramesCombine", "未选择任何精灵。请先选择一些精灵后再尝试合并。" },
            { "msg.NoSheetExport", "未加载精灵表。请先将精灵表拖到本窗口并选择一些精灵后再导出。" },
            { "msg.NoFramesExport", "未选择任何精灵。请先选择一些精灵后再导出。" },
            { "msg.ExportAbort", "精灵导出耗时较长。是否中止？" },
            { "msg.ExportAbortTitle", "导出时间过长" },
            { "msg.ExportConvertFailed", "导出的文件无法转换。{3}{3}使用的参数:{3}{0}{3}{3}请参阅位于 {3}[{2}] 的 '{1}' 文档。" },
            { "msg.NoImageLoaded", "未加载图片。请先将图片拖到上方窗口，或使用“从剪贴板粘贴”按钮。" },
            { "msg.PaletteExportDone", "调色板已保存到：{1}{0}" },
            { "msg.PaletteExportDoneTitle", "调色板已导出" },
            { "msg.NoImageInClipboard", "剪贴板中没有找到图片" },
            { "msg.ErrorLoadingOptionsTitle", "加载选项时出错" },
            { "msg.ErrorDuringOptionLoadTitle", "加载选项时出错" },
            { "msg.ErrorDuringTimer", "。CheckForUnpackFinishTimer 已停止，您可能需要重启应用程序" },
            { "msg.ErrorDuringTimerTitle", "计时器出错" },
            { "msg.AboutFeedback", "Any bugs, suggestions, feedback can be left @ https://github.com/ForkandBeard/Alferd-Spritesheet-Unpacker/issues or tweet me up @ForkandBeard" },
        };
        #endregion
    }
}
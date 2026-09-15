using System;
using System.Drawing;
using System.Windows.Forms;

namespace TradutorPdfOllama
{
    public partial class MainForm
    {
        private ChatPanel _chat;
        private bool _translationBusy;

        private void InitializeChat()
        {
            var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(900, 600),
                SplitterWidth = 5, SplitterDistance = 530, BackColor = _borderSubtle };
            split.Panel1MinSize = 260;
            split.Panel2MinSize = 260;
            _splitContainer.Panel2.Controls.Remove(_rightPanel);
            split.Panel1.Controls.Add(_rightPanel);
            _chat = new ChatPanel();
            split.Panel2.Controls.Add(_chat);
            _splitContainer.Panel2.Controls.Add(split);
            _chat.TranslationEdited += (before, after) =>
            {
                if (_translationBusy || _pages == null || _pages.Count == 0 || before != _currentTranslation) return;
                RenderTranslation(after);
                string language = _cbLang.SelectedItem == null ? "Português (BR)" : _cbLang.SelectedItem.ToString();
                _cache.SetTranslation(_currentFilePath, _currentPageIndex + 1, language, GetCacheModelKey(), after);
                _statusLabel.Text = "Tradução atualizada pelo chat e salva no cache.";
            };
            EventHandler contextChanged = (s, e) => { if (_pages != null && _pages.Count > 0) UpdatePageView(); };
            _cbModel.SelectedIndexChanged += contextChanged;
            _cbLang.SelectedIndexChanged += contextChanged;
            _rtbOriginal.TextChanged += (s, e) => SyncChat();
            this.Shown += (s, e) =>
            {
                _splitContainer.SplitterDistance = Math.Max(240, _splitContainer.Width / 3);
                split.SplitterDistance = Math.Max(260, Math.Min(split.Width - 265, (int)(split.Width * .55)));
            };
        }

        private void SyncChat()
        {
            if (_chat == null) return;
            bool hasPage = _pages != null && _pages.Count > 0;
            string language = _cbLang.SelectedItem == null ? "Português (BR)" : _cbLang.SelectedItem.ToString();
            string model = _cbModel.SelectedItem == null ? "qwen2.5:7b" : _cbModel.SelectedItem.ToString();
            string key = hasPage ? _currentFilePath + "|" + _currentPageIndex + "|" + language + "|" + GetCacheModelKey() : null;
            _chat.SetContext(key, _rtbOriginal.Text, _currentTranslation, model, language, hasPage && !_translationBusy);
        }
    }
}

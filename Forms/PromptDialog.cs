using System;
using System.Drawing;
using System.Windows.Forms;
using EasyDNS.UI;

namespace EasyDNS.Forms
{
    public class PromptDialog : Form
    {
        private readonly ModernInputField _txtInput;
        private readonly ModernButton _btnOk;
        private readonly ModernButton _btnCancel;

        public string InputText
        {
            get { return _txtInput.Text; }
        }

        public PromptDialog(string title, string promptText, string defaultValue = "")
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.CardBackground;
            ForeColor = Theme.TextPrimary;
            Font = Theme.BodyFont;
            Size = new Size(420, 190);

            var lblPrompt = new Label
            {
                Text = promptText,
                Font = Theme.BodyBoldFont,
                ForeColor = Theme.TextPrimary,
                Location = new Point(20, 18),
                AutoSize = true
            };

            _txtInput = new ModernInputField
            {
                Text = defaultValue,
                Location = new Point(20, 48),
                Size = new Size(365, 30)
            };

            _btnOk = new ModernButton
            {
                Text = "Save",
                NormalColor = Theme.AccentPrimary,
                HoverColor = Theme.AccentPrimaryHover,
                BorderRadius = 6,
                Size = new Size(85, 30),
                Location = new Point(205, 96),
                DialogResult = DialogResult.OK
            };

            _btnCancel = new ModernButton
            {
                Text = "Cancel",
                NormalColor = Theme.InputBackground,
                HoverColor = Theme.CardHover,
                CustomBorderColor = Theme.BorderColor,
                BorderThickness = 1,
                BorderRadius = 6,
                Size = new Size(85, 30),
                Location = new Point(300, 96),
                DialogResult = DialogResult.Cancel
            };

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            Controls.Add(lblPrompt);
            Controls.Add(_txtInput);
            Controls.Add(_btnOk);
            Controls.Add(_btnCancel);
        }

        public static string Show(IWin32Window owner, string title, string prompt, string defaultValue = "")
        {
            using (var dlg = new PromptDialog(title, prompt, defaultValue))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return dlg.InputText;
                }
            }
            return null;
        }
    }
}

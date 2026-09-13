using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    internal sealed class AdaptiveRowPanelContentsEditorForm : Form
    {
        private readonly AdaptiveRowPanel _owner;
        private readonly ListBox _listBox;
        private readonly Button _btnUp;
        private readonly Button _btnDown;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;

        public AdaptiveRowPanelContentsEditorForm(AdaptiveRowPanel owner)
        {
            _owner = owner;
            Text = "Adaptive Row Contents";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(360, 320);

            _listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false
            };
            _listBox.SelectedIndexChanged += (_, _) => UpdateButtonStates();

            _btnUp = new Button
            {
                Text = "Up",
                Width = 80
            };
            _btnUp.Click += (_, _) => MoveSelectedItem(-1);

            _btnDown = new Button
            {
                Text = "Down",
                Width = 80
            };
            _btnDown.Click += (_, _) => MoveSelectedItem(1);

            _btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Width = 80
            };

            _btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 80
            };

            var rightButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 92,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(0),
                WrapContents = false
            };
            rightButtons.Controls.Add(_btnUp);
            rightButtons.Controls.Add(_btnDown);

            var bottomButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0)
            };
            bottomButtons.Controls.Add(_btnCancel);
            bottomButtons.Controls.Add(_btnOk);

            var listHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };
            listHost.Controls.Add(_listBox);
            listHost.Controls.Add(rightButtons);

            Controls.Add(listHost);
            Controls.Add(bottomButtons);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            LoadItems();
            UpdateButtonStates();
        }

        public IReadOnlyList<Control> GetOrderedControls()
        {
            return _listBox.Items.Cast<ControlListItem>().Select(i => i.Control).ToArray();
        }

        private void LoadItems()
        {
            _listBox.Items.Clear();
            foreach (var control in _owner.GetOrderedChildren())
            {
                _listBox.Items.Add(new ControlListItem(control));
            }

            if (_listBox.Items.Count > 0)
            {
                _listBox.SelectedIndex = 0;
            }
        }

        private void MoveSelectedItem(int direction)
        {
            int index = _listBox.SelectedIndex;
            if (index < 0)
            {
                return;
            }

            int newIndex = index + direction;
            if (newIndex < 0 || newIndex >= _listBox.Items.Count)
            {
                return;
            }

            object selected = _listBox.Items[index];
            _listBox.Items.RemoveAt(index);
            _listBox.Items.Insert(newIndex, selected);
            _listBox.SelectedIndex = newIndex;
        }

        private void UpdateButtonStates()
        {
            int index = _listBox.SelectedIndex;
            _btnUp.Enabled = index > 0;
            _btnDown.Enabled = index >= 0 && index < _listBox.Items.Count - 1;
        }

        private sealed class ControlListItem
        {
            public ControlListItem(Control control)
            {
                Control = control;
            }

            public System.Windows.Forms.Control Control { get; }

            public override string ToString()
            {
                string name = string.IsNullOrWhiteSpace(Control.Name) ? "(unnamed)" : Control.Name;
                return $"{name} : {Control.GetType().Name}";
            }
        }
    }
}

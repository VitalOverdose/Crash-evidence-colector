using System;
using System.Drawing;
using System.Windows.Forms;
using static System.Windows.Forms.TabControl;

namespace ProfessorSnowsVideoDownloader.CustomControls.WebBrowserTabControl
{
    /// <summary>
    /// Manages the content area of tabs (the actual tab pages).
    /// Designed to be positioned in Row 3 of TableLayoutPanel.
    /// </summary>
    public partial class TabContentControl : UserControl
    {
        private TabControl innerTabControl = null!;

        public TabContentControl()
        {
            InitializeComponent();
            InitializeCustomComponents();
        }

        private void InitializeCustomComponents()
        {
            innerTabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.FlatButtons,
                ItemSize = new Size(0, 1),  // Hide headers
                SizeMode = TabSizeMode.Fixed
            };
            this.Controls.Add(innerTabControl);
        }

        public int SelectedIndex
        {
            get => innerTabControl.SelectedIndex;
            set => innerTabControl.SelectedIndex = value;
        }

        public TabPageCollection TabPages => innerTabControl.TabPages;

        public TabPage? SelectedTab => innerTabControl.SelectedTab;

        public int TabCount => innerTabControl.TabPages.Count;

        public void AddTabPage(Control content)
        {
            var page = new TabPage();
            content.Dock = DockStyle.Fill;
            page.Controls.Add(content);
            innerTabControl.TabPages.Add(page);
        }

        public void RemoveTabPage(int index)
        {
            if (index >= 0 && index < innerTabControl.TabPages.Count)
            {
                var page = innerTabControl.TabPages[index];
                innerTabControl.TabPages.RemoveAt(index);
                page.Dispose();
            }
        }

        public TabPage GetTabPage(int index)
        {
            return innerTabControl.TabPages[index];
        }
    }
}
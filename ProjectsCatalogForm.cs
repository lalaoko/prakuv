using System;
using System.Drawing;
using System.Windows.Forms;
using MiniCad.Services;

namespace MiniCad
{
    public partial class ProjectsCatalogForm : Form
    {
        private DataGridView dgvProjects;
        private Button btnOpen;
        private Button btnDelete;
        private Button btnRefresh;

        private TextBox txtAuthor;
        private DateTimePicker dtpDate;
        private CheckBox chkDate;
        private Button btnReset;

        private System.Data.DataTable? projectsTable;

        private readonly DatabaseService databaseService =
            new DatabaseService();

        public int? SelectedProjectId { get; private set; }

        public ProjectsCatalogForm()
        {
            InitializeCatalog();
        }

        private void InitializeCatalog()
        {
            Text = "Каталог проектов";
            Size = new Size(900, 550);
            StartPosition = FormStartPosition.CenterParent;

            Panel panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100
            };

            btnOpen = new Button
            {
                Text = "Открыть схему",
                Size = new Size(140, 35),
                Location = new Point(10, 10)
            };

            btnDelete = new Button
            {
                Text = "Удалить",
                Size = new Size(110, 35),
                Location = new Point(160, 10)
            };

            btnRefresh = new Button
            {
                Text = "Обновить",
                Size = new Size(110, 35),
                Location = new Point(280, 10)
            };

            
            Label lblAuthor = new Label
            {
                Text = "Автор:",
                Location = new Point(10, 65),
                Size = new Size(50, 23)
            };

            txtAuthor = new TextBox
            {
                Location = new Point(65, 60),
                Size = new Size(180, 25)
            };

            
            chkDate = new CheckBox
            {
                Text = "Фильтр по дате:",
                Location = new Point(265, 63),
                AutoSize = true
            };

            dtpDate = new DateTimePicker
            {
                Location = new Point(410, 60),
                Size = new Size(140, 25),
                Format = DateTimePickerFormat.Short,
                Enabled = false
            };

            
            btnReset = new Button
            {
                Text = "Сбросить",
                Location = new Point(565, 58),
                Size = new Size(100, 30)
            };

            dgvProjects = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };

            panel.Controls.Add(btnOpen);
            panel.Controls.Add(btnDelete);
            panel.Controls.Add(btnRefresh);
            panel.Controls.Add(lblAuthor);
            panel.Controls.Add(txtAuthor);
            panel.Controls.Add(chkDate);
            panel.Controls.Add(dtpDate);
            panel.Controls.Add(btnReset);

            txtAuthor.TextChanged += (_, _) => ApplyFilters();

            chkDate.CheckedChanged += (_, _) =>
            {
                dtpDate.Enabled = chkDate.Checked;
                ApplyFilters();
            };

            dtpDate.ValueChanged += (_, _) => ApplyFilters();

            btnReset.Click += (_, _) =>
            {
                txtAuthor.Clear();
                chkDate.Checked = false;
                dtpDate.Value = DateTime.Today;

                ApplyFilters();
            };

            Controls.Add(dgvProjects);
            Controls.Add(panel);

            btnRefresh.Click += async (s, e) =>
                await LoadProjectsAsync();

            btnOpen.Click += BtnOpen_Click;

            btnDelete.Click += BtnDelete_Click;

            Load += async (s, e) =>
                await LoadProjectsAsync();
        }

        private int? GetSelectedProjectId()
        {
            if (dgvProjects.SelectedRows.Count == 0)
            {
                MessageBox.Show("Сначала выберите проект.");
                return null;
            }

            return Convert.ToInt32(
                dgvProjects.SelectedRows[0].Cells["ID"].Value);
        }

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            int? projectId = GetSelectedProjectId();

            if (projectId == null)
                return;

            SelectedProjectId = projectId;

            DialogResult = DialogResult.OK;
            Close();
        }

        private async void BtnDelete_Click(object sender, EventArgs e)
        {
            int? projectId = GetSelectedProjectId();

            if (projectId == null)
                return;

            DialogResult result = MessageBox.Show(
                "Вы действительно хотите удалить этот проект?",
                "Подтверждение удаления",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            try
            {
                await databaseService.DeleteProjectAsync(projectId.Value);

                MessageBox.Show("Проект успешно удалён.");

                await LoadProjectsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Ошибка удаления проекта: " + ex.Message);
            }
        }

        private async System.Threading.Tasks.Task LoadProjectsAsync()
        {
            try
            {
                projectsTable = await databaseService.GetProjectsSummaryAsync();

                dgvProjects.DataSource = projectsTable;

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Ошибка загрузки проектов: " + ex.Message);
            }
        }

        
        private void ApplyFilters()
        {
            if (projectsTable == null)
                return;

            List<string> filters = new List<string>();

            
            string author = txtAuthor.Text.Trim();

            if (!string.IsNullOrWhiteSpace(author))
            {
                author = author.Replace("'", "''");

                filters.Add($"[Автор] LIKE '%{author}%'");
            }

            
            if (chkDate.Checked)
            {
                string date = dtpDate.Value.ToString(
                    "MM/dd/yyyy",
                    System.Globalization.CultureInfo.InvariantCulture);

                filters.Add(
                    $"[Последнее изменение] >= #{date}# AND " +
                    $"[Последнее изменение] < #{dtpDate.Value.AddDays(1).ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture)}#");
            }

            projectsTable.DefaultView.RowFilter =
                string.Join(" AND ", filters);
        }
    }
}
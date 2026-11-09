using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using MiniCad.Models;
using MiniCad.Controls;
using MiniCad.Enums;
using MiniCad.Services;
using System.Threading.Tasks;

namespace MiniCad;

public partial class Form1 : Form
{
    private readonly List<Shape> _shapes = new();
    private int? _currentProjectId = null;
    private string _currentProjectTitle = "Мой проект";

    private Shape? _selectedShape;
    private Point _dragStartPoint;
    private bool _isDragging;

    private bool _isCreatingShape;
    private Point _creationStartPoint;
    private ToolMode _currentMode = ToolMode.Select;

    private Color _strokeColor = Color.Black;
    private Color _fillColor = Color.LightSkyBlue;
    private float _strokeWidth = 2f;

    private CanvasPanel canvas = null!;
    private PropertyGrid propertyGrid = null!;
    private ToolStripStatusLabel lblMode = null!;
    private ToolStripStatusLabel lblCoords = null!;
    private ToolStripStatusLabel lblCount = null!;

    public Form1()
    {
        InitializeComponent();
        BuildInterface();

        KeyPreview = true;
        KeyDown += Form1_KeyDown;

        Shown += async (s, e) =>
        {
            DatabaseService db = new DatabaseService();

            bool connected = await db.TestConnectionAsync();

            if (connected)
            {
                MessageBox.Show(
                    "Подключение к базе данных успешно!",
                    "SQL Server");
            }
            else
            {
                MessageBox.Show(
                    "Не удалось подключиться к базе данных.",
                    "Ошибка");
            }
        };
    }

    private void BuildInterface()
    {
        Text = "Mini-CAD — графический редактор";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1100, 700);
        MinimumSize = new Size(800, 500);

        var toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = true
        };

        AddToolButton(toolStrip, "Выделение", ToolMode.Select);
        AddToolButton(toolStrip, "Прямоугольник", ToolMode.Rectangle);
        AddToolButton(toolStrip, "Эллипс", ToolMode.Ellipse);
        AddToolButton(toolStrip, "Ромб", ToolMode.Rhombus);
        AddToolButton(toolStrip, "Текст", ToolMode.Text);
        AddToolButton(toolStrip, "Линия", ToolMode.Line);

        toolStrip.Items.Add(new ToolStripSeparator());

        var strokeButton = new ToolStripButton("Контур");
        strokeButton.Click += (_, _) =>
        {
            using var dialog = new ColorDialog
            {
                Color = _strokeColor
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _strokeColor = dialog.Color;

                if (_selectedShape != null)
                    _selectedShape.StrokeColor = _strokeColor;

                canvas.Invalidate();
                propertyGrid.Refresh();
            }
        };

        toolStrip.Items.Add(strokeButton);

        var fillButton = new ToolStripButton("Заливка");
        fillButton.Click += (_, _) =>
        {
            using var dialog = new ColorDialog
            {
                Color = _fillColor
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _fillColor = dialog.Color;

                if (_selectedShape != null)
                    _selectedShape.FillColor = _fillColor;

                canvas.Invalidate();
                propertyGrid.Refresh();
            }
        };

        toolStrip.Items.Add(fillButton);

        toolStrip.Items.Add(new ToolStripLabel("Толщина:"));

        var thicknessBox = new ToolStripComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        thicknessBox.Items.AddRange(new object[]
        {
            "1", "2", "3", "5", "8"
        });

        thicknessBox.SelectedItem = "2";

        thicknessBox.SelectedIndexChanged += (_, _) =>
        {
            if (float.TryParse(
                thicknessBox.SelectedItem?.ToString(),
                out float width))
            {
                _strokeWidth = width;

                if (_selectedShape != null)
                    _selectedShape.StrokeWidth = width;

                canvas.Invalidate();
                propertyGrid.Refresh();
            }
        };

        toolStrip.Items.Add(thicknessBox);

        toolStrip.Items.Add(new ToolStripSeparator());

        var clearButton = new ToolStripButton("Очистить");
        clearButton.Click += (_, _) =>
        {
            if (MessageBox.Show(
                "Удалить все фигуры с холста?",
                "Очистка холста",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _shapes.Clear();
                _selectedShape = null;
                propertyGrid.SelectedObject = null;
                canvas.Invalidate();
                UpdateCount();
            }
        };

        toolStrip.Items.Add(clearButton);

        var saveButton = new ToolStripButton("Сохранить JSON");
        saveButton.Click += SaveJson;
        toolStrip.Items.Add(saveButton);

        var saveSqlButton = new ToolStripButton("Сохранить в SQL");
        saveSqlButton.Click += async (_, _) =>
        {
            await SaveToDatabaseAsync();
        };

        toolStrip.Items.Add(saveSqlButton);

        var loadSqlButton = new ToolStripButton("Загрузить из SQL");
        loadSqlButton.Click += async (_, _) =>
        {
            await LoadFromDatabaseAsync();
        };

        toolStrip.Items.Add(loadSqlButton);

        var selectProjectButton = new ToolStripButton("Каталог проектов");

        selectProjectButton.Click += async (_, _) =>
        {
            await OpenProjectsCatalogAsync();
        };

        toolStrip.Items.Add(selectProjectButton);

        var renameProjectButton = new ToolStripButton("Переименовать проект");

        renameProjectButton.Click += async (_, _) =>
        {
            await RenameCurrentProjectAsync();
        };

        toolStrip.Items.Add(renameProjectButton);

        var deleteProjectButton = new ToolStripButton("Удалить проект из SQL");

        deleteProjectButton.Click += async (_, _) =>
        {
            await DeleteCurrentProjectAsync();
        };

        toolStrip.Items.Add(deleteProjectButton);

        var loadButton = new ToolStripButton("Загрузить JSON");
        loadButton.Click += LoadJson;
        toolStrip.Items.Add(loadButton);

        var exportButton = new ToolStripButton("Экспорт PNG");
        exportButton.Click += ExportPng;
        toolStrip.Items.Add(exportButton);

        propertyGrid = new PropertyGrid
        {
            Dock = DockStyle.Right,
            Width = 250,
            ToolbarVisible = true,
            HelpVisible = true
        };

        propertyGrid.PropertyValueChanged += (_, _) =>
        {
            canvas.Invalidate();
        };

        canvas = new CanvasPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        canvas.Paint += Canvas_Paint;
        canvas.MouseDown += Canvas_MouseDown;
        canvas.MouseMove += Canvas_MouseMove;
        canvas.MouseUp += Canvas_MouseUp;

        var statusStrip = new StatusStrip();

        lblMode = new ToolStripStatusLabel("Инструмент: Выделение");
        lblCoords = new ToolStripStatusLabel("X: 0, Y: 0");
        lblCount = new ToolStripStatusLabel("Фигур: 0");

        statusStrip.Items.Add(lblMode);
        statusStrip.Items.Add(new ToolStripStatusLabel("|"));
        statusStrip.Items.Add(lblCoords);
        statusStrip.Items.Add(new ToolStripStatusLabel("|"));
        statusStrip.Items.Add(lblCount);

        Controls.Add(canvas);
        Controls.Add(propertyGrid);
        Controls.Add(toolStrip);
        Controls.Add(statusStrip);
    }

    private void AddToolButton(
        ToolStrip strip,
        string title,
        ToolMode mode)
    {
        var button = new ToolStripButton(title)
        {
            CheckOnClick = false
        };

        button.Click += (_, _) =>
        {
            _currentMode = mode;
            lblMode.Text = $"Инструмент: {title}";
        };

        strip.Items.Add(button);
    }

    private void Canvas_Paint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        foreach (Shape shape in _shapes)
        {
            shape.Draw(e.Graphics);
        }
    }

    private void Canvas_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        if (_currentMode == ToolMode.Select)
        {
            if (_selectedShape != null)
                _selectedShape.IsSelected = false;

            _selectedShape = _shapes.LastOrDefault(
                shape => shape.ContainsPoint(e.Location));

            if (_selectedShape != null)
            {
                _selectedShape.IsSelected = true;
                _isDragging = true;
                _dragStartPoint = e.Location;
                propertyGrid.SelectedObject = _selectedShape;
            }
            else
            {
                propertyGrid.SelectedObject = null;
            }

            canvas.Invalidate();
            return;
        }

        Shape? newShape = _currentMode switch
        {
            ToolMode.Rectangle => new RectangleShape(),
            ToolMode.Ellipse => new EllipseShape(),
            ToolMode.Rhombus => new RhombusShape(),
            ToolMode.Text => new TextBlockShape(),
            ToolMode.Line => new LineShape(),
            _ => null
        };

        if (newShape == null)
            return;

        if (_selectedShape != null)
            _selectedShape.IsSelected = false;

        _creationStartPoint = e.Location;
        _isCreatingShape = true;

        newShape.X = e.X;
        newShape.Y = e.Y;
        newShape.Width = 10;
        newShape.Height = 10;
        newShape.StrokeColor = _strokeColor;
        newShape.FillColor = _fillColor;
        newShape.StrokeWidth = _strokeWidth;

        _shapes.Add(newShape);
        _selectedShape = newShape;
        _selectedShape.IsSelected = true;

        propertyGrid.SelectedObject = _selectedShape;

        canvas.Invalidate();
        UpdateCount();
    }

    private void Canvas_MouseMove(object? sender, MouseEventArgs e)
    {
        lblCoords.Text = $"X: {e.X}, Y: {e.Y}";

        // Создание фигуры перетаскиванием мыши
        if (_isCreatingShape && _selectedShape != null)
        {
            int left = Math.Min(_creationStartPoint.X, e.X);
            int top = Math.Min(_creationStartPoint.Y, e.Y);

            int width = Math.Max(10, Math.Abs(e.X - _creationStartPoint.X));
            int height = Math.Max(10, Math.Abs(e.Y - _creationStartPoint.Y));

            _selectedShape.X = left;
            _selectedShape.Y = top;
            _selectedShape.Width = width;
            _selectedShape.Height = height;

            propertyGrid.Refresh();
            canvas.Invalidate();
            return;
        }

        // Перемещение уже созданной фигуры
        if (_isDragging && _selectedShape != null)
        {
            int dx = e.X - _dragStartPoint.X;
            int dy = e.Y - _dragStartPoint.Y;

            _selectedShape.Move(dx, dy);
            _dragStartPoint = e.Location;

            propertyGrid.Refresh();
            canvas.Invalidate();
        }
    }

    private void Canvas_MouseUp(object? sender, MouseEventArgs e)
    {
        _isDragging = false;
        _isCreatingShape = false;
    }

    private void UpdateCount()
    {
        lblCount.Text = $"Фигур: {_shapes.Count}";
    }

    private void SaveJson(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "JSON схема (*.json)|*.json",
            DefaultExt = "json"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = System.Text.Json.JsonSerializer.Serialize(
                _shapes, options);

            System.IO.File.WriteAllText(dialog.FileName, json);

            MessageBox.Show("Схема сохранена.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}");
        }
    }


    private async Task SaveToDatabaseAsync()
    {
        try
        {
            if (_currentProjectId == null)
            {
                string? title = AskProjectTitle();

                if (string.IsNullOrWhiteSpace(title))
                    return;

                _currentProjectTitle = title.Trim();
            }

            DatabaseService db = new DatabaseService();

            int savedProjectId = await db.SaveProjectWithShapesAsync(
                _currentProjectId,
                _currentProjectTitle,
                Environment.UserName,
                canvas.ClientSize.Width,
                canvas.ClientSize.Height,
                _shapes
            );

            _currentProjectId = savedProjectId;

            MessageBox.Show(
                $"Проект «{_currentProjectTitle}» успешно сохранён!\nID проекта: {savedProjectId}",
                "Сохранение",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка сохранения проекта:\n{ex.Message}",
                "Ошибка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }



    private string? AskProjectTitle(string defaultTitle = "Мой проект")
    {
        using Form inputForm = new Form();

        inputForm.Text = "Название проекта";
        inputForm.Width = 350;
        inputForm.Height = 160;
        inputForm.StartPosition = FormStartPosition.CenterParent;
        inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
        inputForm.MaximizeBox = false;
        inputForm.MinimizeBox = false;

        Label label = new Label();
        label.Text = "Введите название проекта:";
        label.Left = 15;
        label.Top = 15;
        label.Width = 300;

        TextBox textBox = new TextBox();
        textBox.Left = 15;
        textBox.Top = 45;
        textBox.Width = 300;
        textBox.Text = defaultTitle;

        Button saveButton = new Button();
        saveButton.Text = "Сохранить";
        saveButton.Left = 145;
        saveButton.Top = 80;
        saveButton.Width = 80;
        saveButton.DialogResult = DialogResult.OK;

        Button cancelButton = new Button();
        cancelButton.Text = "Отмена";
        cancelButton.Left = 235;
        cancelButton.Top = 80;
        cancelButton.Width = 80;
        cancelButton.DialogResult = DialogResult.Cancel;

        inputForm.Controls.Add(label);
        inputForm.Controls.Add(textBox);
        inputForm.Controls.Add(saveButton);
        inputForm.Controls.Add(cancelButton);

        inputForm.AcceptButton = saveButton;
        inputForm.CancelButton = cancelButton;

        if (inputForm.ShowDialog(this) == DialogResult.OK)
            return textBox.Text;

        return null;
    }

    private async Task RenameCurrentProjectAsync()
    {
        // Проверяем, выбран ли проект
        if (_currentProjectId == null)
        {
            MessageBox.Show("Сначала выберите проект из базы данных.");
            return;
        }

        // Запрашиваем новое название
        string? newTitle = AskProjectTitle(_currentProjectTitle);

        if (string.IsNullOrWhiteSpace(newTitle))
        {
            return;
        }

        newTitle = newTitle.Trim();

        try
        {
            DatabaseService db = new DatabaseService();

            await db.RenameProjectAsync(
                _currentProjectId.Value,
                newTitle
            );

            _currentProjectTitle = newTitle;

            MessageBox.Show(
                "Название проекта успешно изменено!",
                "Успех",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Ошибка переименования проекта: " + ex.Message
            );
        }
    }

    private async Task DeleteCurrentProjectAsync()
    {
        if (_currentProjectId == null)
        {
            MessageBox.Show("Сначала выберите проект из базы данных.");
            return;
        }

        DialogResult result = MessageBox.Show(
            $"Вы действительно хотите удалить проект «{_currentProjectTitle}»?\n\nВсе его фигуры также будут удалены!",
            "Удаление проекта",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        );

        if (result != DialogResult.Yes)
            return;

        try
        {
            DatabaseService db = new DatabaseService();

            await db.DeleteProjectAsync(_currentProjectId.Value);

            _currentProjectId = null;
            _currentProjectTitle = "Мой проект";

            _shapes.Clear();
            _selectedShape = null;
            propertyGrid.SelectedObject = null;

            canvas.Invalidate();
            UpdateCount();

            MessageBox.Show(
                "Проект успешно удалён!",
                "Удаление",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Ошибка удаления проекта: " + ex.Message,
                "Ошибка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private async Task LoadFromDatabaseAsync()
    {
        try
        {
            
            if (_currentProjectId == null)
            {
                MessageBox.Show(
                    "Сначала сохраните проект в SQL Server.",
                    "Загрузка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            DatabaseService db = new DatabaseService();

            List<Shape> loadedShapes =
                await db.LoadProjectShapesAsync(_currentProjectId.Value);

            _shapes.Clear();

            foreach (Shape shape in loadedShapes)
            {
                shape.IsSelected = false;
                _shapes.Add(shape);
            }

            _selectedShape = null;
            propertyGrid.SelectedObject = null;

            canvas.Invalidate();
            UpdateCount();

            MessageBox.Show(
                $"Проект успешно загружен!\nКоличество фигур: {_shapes.Count}",
                "Загрузка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка загрузки проекта:\n{ex.Message}",
                "Ошибка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }


    private async Task SelectProjectFromDatabaseAsync()
    {
        try
        {
            DatabaseService db = new DatabaseService();

      
            var projects = await db.GetProjectsAsync();

            if (projects.Count == 0)
            {
                MessageBox.Show(
                    "В базе данных пока нет сохранённых проектов.",
                    "Проекты");

                return;
            }

          
            using Form selectForm = new Form();

            selectForm.Text = "Выбор проекта";
            selectForm.Width = 400;
            selectForm.Height = 180;
            selectForm.StartPosition = FormStartPosition.CenterParent;
            selectForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            selectForm.MaximizeBox = false;
            selectForm.MinimizeBox = false;

            Label label = new Label();
            label.Text = "Выберите проект:";
            label.Left = 15;
            label.Top = 15;
            label.Width = 350;

            ComboBox comboBox = new ComboBox();
            comboBox.Left = 15;
            comboBox.Top = 45;
            comboBox.Width = 350;
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;

            
            foreach (var project in projects)
            {
                comboBox.Items.Add(
                    $"{project.ProjectId} — {project.Title} — {project.Author}");
            }

            comboBox.SelectedIndex = 0;

            Button openButton = new Button();
            openButton.Text = "Открыть";
            openButton.Left = 190;
            openButton.Top = 85;
            openButton.Width = 80;
            openButton.DialogResult = DialogResult.OK;

            Button cancelButton = new Button();
            cancelButton.Text = "Отмена";
            cancelButton.Left = 280;
            cancelButton.Top = 85;
            cancelButton.Width = 80;
            cancelButton.DialogResult = DialogResult.Cancel;

            selectForm.Controls.Add(label);
            selectForm.Controls.Add(comboBox);
            selectForm.Controls.Add(openButton);
            selectForm.Controls.Add(cancelButton);

            selectForm.AcceptButton = openButton;
            selectForm.CancelButton = cancelButton;

            
            if (selectForm.ShowDialog(this) != DialogResult.OK)
                return;

            int selectedIndex = comboBox.SelectedIndex;

            _currentProjectId = projects[selectedIndex].ProjectId;
            _currentProjectTitle = projects[selectedIndex].Title;

            
            List<Shape> loadedShapes =
                await db.LoadProjectShapesAsync(_currentProjectId.Value);

            _shapes.Clear();

            foreach (Shape shape in loadedShapes)
            {
                shape.IsSelected = false;
                _shapes.Add(shape);
            }

            _selectedShape = null;
            propertyGrid.SelectedObject = null;

            canvas.Invalidate();
            UpdateCount();

            MessageBox.Show(
                $"Проект «{projects[selectedIndex].Title}» загружен!\n" +
                $"Количество фигур: {_shapes.Count}",
                "Загрузка проекта");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Ошибка загрузки проекта:\n" + ex.Message,
                "Ошибка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    
    private async Task OpenProjectsCatalogAsync()
    {
        try
        {
            using ProjectsCatalogForm catalog = new ProjectsCatalogForm();

            if (catalog.ShowDialog(this) != DialogResult.OK)
                return;

            if (catalog.SelectedProjectId == null)
                return;

            DatabaseService db = new DatabaseService();

            int projectId = catalog.SelectedProjectId.Value;

           
            var projects = await db.GetProjectsAsync();

            var selectedProject = projects.FirstOrDefault(
                p => p.ProjectId == projectId);

            if (selectedProject.ProjectId == 0)
            {
                MessageBox.Show("Проект не найден.");
                return;
            }

           
            List<Shape> loadedShapes =
                await db.LoadProjectShapesAsync(projectId);

            _shapes.Clear();

            foreach (Shape shape in loadedShapes)
            {
                shape.IsSelected = false;
                _shapes.Add(shape);
            }

            _currentProjectId = projectId;
            _currentProjectTitle = selectedProject.Title;

            _selectedShape = null;
            propertyGrid.SelectedObject = null;

            canvas.Invalidate();
            UpdateCount();

            MessageBox.Show(
                $"Проект «{_currentProjectTitle}» успешно загружен!\n" +
                $"Количество фигур: {_shapes.Count}",
                "Загрузка проекта");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Ошибка открытия проекта:\n" + ex.Message,
                "Ошибка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void LoadJson(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "JSON схема (*.json)|*.json"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            string json = System.IO.File.ReadAllText(dialog.FileName);

            var loaded =
                System.Text.Json.JsonSerializer.Deserialize<List<Shape>>(
                    json);

            if (loaded == null)
                return;

            _shapes.Clear();
            foreach (Shape shape in loaded)
            {
                shape.IsSelected = false;
                _shapes.Add(shape);
            }

            _selectedShape = null;
            propertyGrid.SelectedObject = null;

            canvas.Invalidate();
            UpdateCount();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки: {ex.Message}");
        }
    }

    private void ExportPng(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "PNG изображение (*.png)|*.png",
            DefaultExt = "png"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            using var bitmap = new Bitmap(
                canvas.ClientSize.Width,
                canvas.ClientSize.Height);

            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                foreach (Shape shape in _shapes)
                {
                    shape.Draw(g);
                }
            }

            bitmap.Save(
                dialog.FileName,
                System.Drawing.Imaging.ImageFormat.Png);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка экспорта: {ex.Message}");
        }
    }

    private void Form1_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete && _selectedShape != null)
        {
            _shapes.Remove(_selectedShape);
            _selectedShape = null;

            propertyGrid.SelectedObject = null;
            canvas.Invalidate();
            UpdateCount();

            e.Handled = true;
        }

        if (e.KeyCode == Keys.Escape)
        {
            if (_selectedShape != null)
            {
                _selectedShape.IsSelected = false;
                _selectedShape = null;
                propertyGrid.SelectedObject = null;
                canvas.Invalidate();
            }

            _isDragging = false;
            _isCreatingShape = false;
        }
    }
}
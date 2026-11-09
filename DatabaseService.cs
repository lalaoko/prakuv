
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MiniCad.Models;
using System.Drawing;
using System.Data;

namespace MiniCad.Services
{
    public class DatabaseService
    {
        private readonly string connectionString =
            @"Server=localhost;Database=DiagramDb;Integrated Security=True;TrustServerCertificate=True;";

        // Проверка подключения к SQL Server
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using (SqlConnection connection =
                       new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                return false;
            }
        }

        // Сохранение информации о проекте
        public async Task<int> SaveProjectAsync(
            int? projectId,
            string title,
            string author,
            int canvasWidth,
            int canvasHeight)
        {
            using (SqlConnection connection =
                   new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (SqlCommand command =
                       new SqlCommand("sp_SaveProject", connection))
                {
                    command.CommandType =
                        System.Data.CommandType.StoredProcedure;

                    SqlParameter idParameter =
                        command.Parameters.Add(
                            "@ProjectID",
                            System.Data.SqlDbType.Int);

                    idParameter.Direction =
                        System.Data.ParameterDirection.InputOutput;

                    idParameter.Value =
                        projectId.HasValue ? projectId.Value : 0;

                    command.Parameters.AddWithValue("@Title", title);
                    command.Parameters.AddWithValue("@Author", author);
                    command.Parameters.AddWithValue("@CanvasWidth", canvasWidth);
                    command.Parameters.AddWithValue("@CanvasHeight", canvasHeight);

                    await command.ExecuteNonQueryAsync();

                    return (int)idParameter.Value;
                }
            }
        }



        // Сохранение проекта вместе со всеми фигурами в одной транзакции
        public async Task<int> SaveProjectWithShapesAsync(
            int? projectId,
            string title,
            string author,
            int canvasWidth,
            int canvasHeight,
            List<Shape> shapes)
        {
            using (SqlConnection connection =
                   new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (SqlTransaction transaction =
                       connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Сохраняем информацию о проекте
                        int savedProjectId;

                        using (SqlCommand command =
                               new SqlCommand("sp_SaveProject", connection, transaction))
                        {
                            command.CommandType = CommandType.StoredProcedure;

                            SqlParameter idParameter =
                                command.Parameters.Add(
                                    "@ProjectID", SqlDbType.Int);

                            idParameter.Direction =
                                ParameterDirection.InputOutput;

                            idParameter.Value =
                                projectId.HasValue ? projectId.Value : 0;

                            command.Parameters.AddWithValue("@Title", title);
                            command.Parameters.AddWithValue("@Author", author);
                            command.Parameters.AddWithValue("@CanvasWidth", canvasWidth);
                            command.Parameters.AddWithValue("@CanvasHeight", canvasHeight);

                            await command.ExecuteNonQueryAsync();

                            savedProjectId = Convert.ToInt32(idParameter.Value);
                        }

                        // 2. Сохраняем все фигуры
                        for (int i = 0; i < shapes.Count; i++)
                        {
                            Shape shape = shapes[i];

                            string typeName = GetShapeTypeName(shape);

                            string textContent = null;

                            if (shape is TextBlockShape textShape)
                            {
                                textContent = textShape.Text;
                            }

                            using (SqlCommand command = new SqlCommand(
                                @"INSERT INTO Shapes
                        (ProjectID, ShapeTypeID, X, Y,
                         Width, Height, StrokeColor, FillColor,
                         StrokeWidth, TextContent, ZOrder, FontSize)

                        SELECT
                        @ProjectID, ShapeTypeID, @X, @Y,
                        @Width, @Height, @StrokeColor, @FillColor,
                        @StrokeWidth, @TextContent, @ZOrder, @FontSize

                        FROM ShapeTypes
                        WHERE TypeName = @TypeName;",
                                connection, transaction))
                            {
                                command.Parameters.AddWithValue(
                                    "@ProjectID", savedProjectId);

                                command.Parameters.AddWithValue(
                                    "@TypeName", typeName);

                                command.Parameters.AddWithValue(
                                    "@X", Convert.ToInt32(shape.X));

                                command.Parameters.AddWithValue(
                                    "@Y", Convert.ToInt32(shape.Y));

                                command.Parameters.AddWithValue(
                                    "@Width", Convert.ToInt32(shape.Width));

                                command.Parameters.AddWithValue(
                                    "@Height", Convert.ToInt32(shape.Height));

                                command.Parameters.AddWithValue(
                                    "@StrokeColor", shape.StrokeColorArgb.ToString());

                                command.Parameters.AddWithValue(
                                    "@FillColor", shape.FillColorArgb.ToString());

                                command.Parameters.AddWithValue(
                                    "@StrokeWidth", shape.StrokeWidth);

                                command.Parameters.AddWithValue(
                                    "@TextContent",
                                    (object)textContent ?? DBNull.Value);

                                command.Parameters.AddWithValue(
                                    "@ZOrder", i);
                                command.Parameters.AddWithValue(
                                     "@FontSize",
                                shape is TextBlockShape textBlock
                                     ? textBlock.FontSize
                                     : 12f);

                                int rows = await command.ExecuteNonQueryAsync();

                                if (rows != 1)
                                {
                                    throw new Exception(
                                        "Не удалось сохранить тип фигуры: " + typeName);
                                }
                            }
                        }

                        // 3. Если всё успешно — подтверждаем транзакцию
                        await transaction.CommitAsync();

                        return savedProjectId;
                    }
                    catch
                    {
                        // Если произошла ошибка — отменяем все изменения
                        await transaction.RollbackAsync();

                        throw;
                    }
                }
            }
        }

        // Определение типа фигуры
        private string GetShapeTypeName(Shape shape)
        {
            if (shape is RectangleShape)
                return "Rectangle";

            if (shape is EllipseShape)
                return "Ellipse";

            if (shape is RhombusShape)
                return "Rhombus";

            if (shape is TextBlockShape)
                return "TextBlock";

            if (shape is LineShape)
                return "Line";

            throw new Exception("Неизвестный тип фигуры");
        }

        public async Task<List<Shape>> LoadProjectShapesAsync(int projectId)
        {
            List<Shape> shapes = new List<Shape>();

            using (SqlConnection connection =
                   new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (SqlCommand command =
                       new SqlCommand("sp_GetProjectShapes", connection))
                {
                    command.CommandType =
                        System.Data.CommandType.StoredProcedure;

                    command.Parameters.AddWithValue(
                        "@ProjectID", projectId);

                    using (SqlDataReader reader =
                           await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string typeName =
                                reader["TypeName"].ToString()!;

                            Shape shape = typeName switch
                            {
                                "Rectangle" => new RectangleShape(),
                                "Ellipse" => new EllipseShape(),
                                "Rhombus" => new RhombusShape(),
                                "TextBlock" => new TextBlockShape(),
                                "Line" => new LineShape(),

                                _ => throw new Exception(
                                    "Неизвестный тип фигуры: " + typeName)
                            };

                            shape.X = Convert.ToInt32(reader["X"]);
                            shape.Y = Convert.ToInt32(reader["Y"]);

                            shape.Width = Convert.ToInt32(reader["Width"]);
                            shape.Height = Convert.ToInt32(reader["Height"]);

                            shape.StrokeColor = Color.FromArgb(
                                Convert.ToInt32(reader["StrokeColor"]));

                            shape.FillColor = Color.FromArgb(
                                Convert.ToInt32(reader["FillColor"]));

                            shape.StrokeWidth =
                                Convert.ToSingle(reader["StrokeWidth"]);

                            shape.IsSelected = false;

                            if (shape is TextBlockShape textBlock)
                            {
                                textBlock.FontSize =
                                    Convert.ToSingle(reader["FontSize"]);
                            }

                            if (shape is TextBlockShape textShape)
                            {
                                textShape.Text =
                                    reader["TextContent"] == DBNull.Value
                                    ? ""
                                    : reader["TextContent"].ToString()!;
                            }

                            shapes.Add(shape);
                        }
                    }
                }
            }

            return shapes;
        }


      
        public async Task<List<(int ProjectId, string Title, string Author)>> GetProjectsAsync()
        {
            List<(int ProjectId, string Title, string Author)> projects = new();

            using (SqlConnection connection =
                   new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"
            SELECT ProjectID, Title, Author
            FROM Projects
            ORDER BY ProjectID DESC";

                using (SqlCommand command =
                       new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader =
                           await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            projects.Add((
                                reader.GetInt32(0),
                                reader.GetString(1),
                                reader.GetString(2)
                            ));
                        }
                    }
                }
            }

            return projects;
        }

        public async Task RenameProjectAsync(int projectId, string newTitle)
        {
            using (SqlConnection connection =
                   new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"
            UPDATE Projects
            SET Title = @Title
            WHERE ProjectID = @ProjectID";

                using (SqlCommand command =
                       new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Title", newTitle);
                    command.Parameters.AddWithValue("@ProjectID", projectId);

                    int rows = await command.ExecuteNonQueryAsync();

                    if (rows == 0)
                    {
                        throw new Exception("Проект не найден в базе данных.");
                    }
                }
            }
        }

        public async Task<DataTable> GetProjectsSummaryAsync()
        {
            using (SqlConnection connection =
                   new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"
            SELECT *
            FROM v_ProjectStatistics
            ORDER BY [Последнее изменение] DESC";

                using (SqlCommand command =
                       new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader =
                           await command.ExecuteReaderAsync())
                    {
                        DataTable table = new DataTable();
                        table.Load(reader);

                        return table;
                    }
                }
            }
        }

        public async Task DeleteProjectAsync(int projectId)
        {
            using (SqlConnection connection =
                   new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (SqlTransaction transaction =
                       connection.BeginTransaction())
                {
                    try
                    {
                        string query = @"
                    DELETE FROM Shapes
                    WHERE ProjectID = @ProjectID;

                    DELETE FROM Projects
                    WHERE ProjectID = @ProjectID;

                    SELECT @@ROWCOUNT;";

                        using (SqlCommand command =
                               new SqlCommand(query, connection, transaction))
                        {
                            command.Parameters.AddWithValue(
                                "@ProjectID", projectId);

                            int deletedRows = Convert.ToInt32(
                                await command.ExecuteScalarAsync());

                            if (deletedRows == 0)
                            {
                                throw new Exception(
                                    "Проект не найден в базе данных.");
                            }
                        }

                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }
    }
}
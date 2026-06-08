namespace SManager.Gui;

/// <summary>Márgenes y estilos compartidos de la GUI.</summary>
internal static class UtilidadesInterfaz
{
    public const int AltoMinimoBoton = 30;

    public static readonly Padding MargenContenedor = new(12, 10, 12, 10);
    public static readonly Padding MargenPestana = new(8, 8, 8, 8);
    public static readonly Padding MargenBarraBotones = new(8, 6, 8, 10);

    /// <summary>Aplica relleno automático de columnas y cabeceras legibles.</summary>
    public static void ConfigurarGrid(
        DataGridView grid,
        bool soloLectura = false,
        params (string nombre, string cabecera, float peso, int anchoMinimo)[] columnas)
    {
        grid.AllowUserToAddRows = false;
        grid.AllowUserToResizeColumns = true;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.BorderStyle = BorderStyle.Fixed3D;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.ReadOnly = soloLectura;
        grid.Dock = DockStyle.Fill;
        grid.Margin = new Padding(0);
        grid.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);

        grid.Columns.Clear();
        foreach (var (nombre, cabecera, peso, anchoMinimo) in columnas)
        {
            var columna = new DataGridViewTextBoxColumn
            {
                Name = nombre,
                HeaderText = cabecera,
                FillWeight = peso,
                MinimumWidth = anchoMinimo
            };
            grid.Columns.Add(columna);
        }
    }

    public static void ConfigurarGridPares(DataGridView grid)
    {
        grid.AllowUserToAddRows = false;
        grid.AllowUserToResizeColumns = true;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.BorderStyle = BorderStyle.Fixed3D;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.Dock = DockStyle.Fill;
        grid.Margin = new Padding(0);
        grid.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);

        grid.Columns.Clear();
        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "habilitado",
            HeaderText = "On",
            FillWeight = 28,
            MinimumWidth = 36
        });
        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "pausado",
            HeaderText = "Pausado",
            FillWeight = 40,
            MinimumWidth = 52
        });
        grid.Columns.Add(CrearColumnaTexto("nombre", "Nombre", 70, 80));
        grid.Columns.Add(CrearColumnaTexto("origen", "Origen", 120, 100));
        grid.Columns.Add(CrearColumnaTexto("destino", "Destino", 120, 100));
        grid.Columns.Add(CrearColumnaTexto("inclusion", "Inclusión", 90, 80));
        grid.Columns.Add(CrearColumnaTexto("exclusion", "Exclusión", 90, 80));
    }

    private static DataGridViewTextBoxColumn CrearColumnaTexto(
        string nombre,
        string cabecera,
        float peso,
        int anchoMinimo) =>
        new()
        {
            Name = nombre,
            HeaderText = cabecera,
            FillWeight = peso,
            MinimumWidth = anchoMinimo
        };
}

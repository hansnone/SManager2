using SManager.Gui.Shared;

namespace SManager.Gui;



/// <summary>Diálogo simple para nombrar un perfil nuevo.</summary>

internal static class DialogoNuevoPerfil

{

    public static string? Mostrar(IWin32Window? propietario)

    {

        using var formulario = new Form

        {

            Text = "Nuevo perfil",

            FormBorderStyle = FormBorderStyle.FixedDialog,

            StartPosition = FormStartPosition.CenterParent,

            AutoSize = true,

            AutoSizeMode = AutoSizeMode.GrowAndShrink,

            MaximizeBox = false,

            MinimizeBox = false,

            Font = new Font("Segoe UI", 9F),

            Padding = UtilidadesInterfaz.MargenContenedor

        };



        var layout = new TableLayoutPanel

        {

            AutoSize = true,

            AutoSizeMode = AutoSizeMode.GrowAndShrink,

            ColumnCount = 2,

            Dock = DockStyle.Fill,

            Padding = new Padding(0)

        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));



        var etiqueta = new Label

        {

            Text = "Nombre del perfil (se creará una carpeta en Perfiles configuracion):",

            AutoSize = true,

            MaximumSize = new Size(420, 0),

            Margin = new Padding(0, 0, 0, 8)

        };



        var cajaTexto = new TextBox

        {

            Text = "General",

            Dock = DockStyle.Fill,

            Margin = new Padding(0, 0, 0, 12),

            MinimumSize = new Size(320, UtilidadesInterfaz.AltoMinimoBoton)

        };



        var panelBotones = new FlowLayoutPanel

        {

            FlowDirection = FlowDirection.RightToLeft,

            AutoSize = true,

            AutoSizeMode = AutoSizeMode.GrowAndShrink,

            Dock = DockStyle.Fill,

            WrapContents = false,

            Margin = new Padding(0)

        };



        var botonAceptar = UtilidadesBotones.Crear("Crear");

        botonAceptar.DialogResult = DialogResult.OK;



        var botonCancelar = UtilidadesBotones.Crear("Cancelar");

        botonCancelar.DialogResult = DialogResult.Cancel;



        panelBotones.Controls.AddRange([botonAceptar, botonCancelar]);



        layout.Controls.Add(etiqueta, 0, 0);

        layout.SetColumnSpan(etiqueta, 2);

        layout.Controls.Add(cajaTexto, 0, 1);

        layout.SetColumnSpan(cajaTexto, 2);

        layout.Controls.Add(panelBotones, 0, 2);

        layout.SetColumnSpan(panelBotones, 2);



        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.RowCount = 3;



        formulario.Controls.Add(layout);

        formulario.AcceptButton = botonAceptar;

        formulario.CancelButton = botonCancelar;



        return formulario.ShowDialog(propietario) == DialogResult.OK

            ? cajaTexto.Text.Trim()

            : null;

    }

}



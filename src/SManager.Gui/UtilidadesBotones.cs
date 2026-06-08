namespace SManager.Gui;



/// <summary>Botones con tamaño automático para que el texto no se recorte.</summary>

internal static class UtilidadesBotones

{

    public static Button Crear(string texto)

    {

        var boton = new Button

        {

            Text = texto,

            AutoSize = true,

            AutoSizeMode = AutoSizeMode.GrowAndShrink,

            Padding = new Padding(12, 6, 12, 6),

            Margin = new Padding(6, 4, 6, 4),

            UseCompatibleTextRendering = true

        };



        // Reservar espacio real del texto + padding para evitar recortes en FlowLayout/TableLayout.

        var tamañoTexto = TextRenderer.MeasureText(

            texto,

            boton.Font,

            new Size(int.MaxValue, int.MaxValue),

            TextFormatFlags.SingleLine);



        var ancho = tamañoTexto.Width + boton.Padding.Horizontal + 6;

        var alto = Math.Max(UtilidadesInterfaz.AltoMinimoBoton, tamañoTexto.Height + boton.Padding.Vertical + 4);

        boton.MinimumSize = new Size(ancho, alto);



        return boton;

    }

}



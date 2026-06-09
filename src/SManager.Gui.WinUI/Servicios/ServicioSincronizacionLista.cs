using System.Collections.ObjectModel;



namespace SManager.Gui.WinUI.Servicios;



/// <summary>Actualiza ObservableCollection sin Clear() innecesario para evitar parpadeo en ListView.</summary>

public static class ServicioSincronizacionLista

{

    /// <summary>

    /// Sincroniza por índice reutilizando instancias existentes.

    /// Solo añade, elimina cola o invoca <paramref name="actualizar"/> en filas que ya existen.

    /// </summary>

    public static void SincronizarInPlace<T>(

        ObservableCollection<T> destino,

        int cantidadOrigen,

        Action<int, T> actualizar,

        Func<int, T> crear)

        where T : class

    {

        for (var i = 0; i < cantidadOrigen; i++)

        {

            if (i < destino.Count)

            {

                actualizar(i, destino[i]);

            }

            else

            {

                destino.Add(crear(i));

            }

        }



        while (destino.Count > cantidadOrigen)

        {

            destino.RemoveAt(destino.Count - 1);

        }

    }



    /// <summary>

    /// Actualiza listas tipo historial: conserva prefijo igual, inserta al inicio si hay evento nuevo,

    /// o reconstruye solo la cola sin tocar las filas estables del prefijo.

    /// </summary>

    public static void SincronizarHistorial<T>(

        ObservableCollection<T> destino,

        IReadOnlyList<T> origen,

        Func<T, T, bool> sonIguales)

        where T : class

    {

        if (origen.Count == 0)

        {

            if (destino.Count > 0)

            {

                destino.Clear();

            }



            return;

        }



        if (destino.Count == origen.Count)

        {

            var sinCambios = true;

            for (var i = 0; i < origen.Count; i++)

            {

                if (!sonIguales(destino[i], origen[i]))

                {

                    sinCambios = false;

                    break;

                }

            }



            if (sinCambios)

            {

                return;

            }

        }



        // Evento nuevo al frente: desplaza el historial una posición sin recrear todas las filas.

        if (origen.Count == destino.Count + 1)

        {

            var coincideDesplazamiento = true;

            for (var i = 0; i < destino.Count; i++)

            {

                if (!sonIguales(destino[i], origen[i + 1]))

                {

                    coincideDesplazamiento = false;

                    break;

                }

            }



            if (coincideDesplazamiento)

            {

                destino.Insert(0, origen[0]);

                return;

            }

        }



        var prefijoComun = 0;

        while (prefijoComun < destino.Count

               && prefijoComun < origen.Count

               && sonIguales(destino[prefijoComun], origen[prefijoComun]))

        {

            prefijoComun++;

        }



        if (prefijoComun == origen.Count && prefijoComun == destino.Count)

        {

            return;

        }



        while (destino.Count > prefijoComun)

        {

            destino.RemoveAt(destino.Count - 1);

        }



        for (var i = prefijoComun; i < origen.Count; i++)

        {

            destino.Add(origen[i]);

        }

    }

}



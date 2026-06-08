using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SManager.Gui.WinUI.ViewModels;

namespace SManager.Gui.WinUI;

public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; } = new();

    private bool _desplazarRegistroAlMostrar = true;

    public MainPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.RegistroDesplazarAlFinalSolicitado += ViewModel_RegistroDesplazarAlFinalSolicitado;
        Loaded += MainPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Inicializar();
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (NavPrincipal.MenuItems.Count > 0)
        {
            NavPrincipal.SelectedItem = NavPrincipal.MenuItems[0];
        }

        MostrarSeccion("inicio");
    }

    private void NavPrincipal_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            MostrarSeccion("ajustes");
            return;
        }

        if (args.SelectedItem is NavigationViewItem { Tag: string tag })
        {
            MostrarSeccion(tag);
        }
    }

    private void MostrarSeccion(string tag)
    {
        PanelInicio.Visibility = tag == "inicio" ? Visibility.Visible : Visibility.Collapsed;
        PanelPares.Visibility = tag == "pares" ? Visibility.Visible : Visibility.Collapsed;
        PanelMonitor.Visibility = tag == "monitor" ? Visibility.Visible : Visibility.Collapsed;
        PanelRegistro.Visibility = tag == "registro" ? Visibility.Visible : Visibility.Collapsed;
        PanelAjustes.Visibility = tag == "ajustes" ? Visibility.Visible : Visibility.Collapsed;

        if (tag == "registro" && _desplazarRegistroAlMostrar)
        {
            DesplazarRegistroAlFinal();
            _desplazarRegistroAlMostrar = false;
        }
    }

    private void ViewModel_RegistroDesplazarAlFinalSolicitado(object? sender, EventArgs e)
    {
        if (PanelRegistro.Visibility == Visibility.Visible)
        {
            DesplazarRegistroAlFinal();
        }
        else
        {
            _desplazarRegistroAlMostrar = true;
        }
    }

    /// <summary>Coloca el scroll en la última línea del registro.</summary>
    private void DesplazarRegistroAlFinal()
    {
        // Esperar al layout tras actualizar TextBlock evita quedarse a mitad del log.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            ScrollRegistro.UpdateLayout();
            TextoRegistroBloque.UpdateLayout();
            ScrollRegistro.ChangeView(null, ScrollRegistro.ScrollableHeight, null, disableAnimation: true);
        });
    }

    private void IrASincronizacion_Click(object sender, RoutedEventArgs e) =>
        SeleccionarSeccionPorTag("pares");

    private void IrAMonitor_Click(object sender, RoutedEventArgs e) =>
        SeleccionarSeccionPorTag("monitor");

    private void SeleccionarSeccionPorTag(string tag)
    {
        foreach (var item in NavPrincipal.MenuItems)
        {
            if (item is NavigationViewItem navItem && (string?)navItem.Tag == tag)
            {
                NavPrincipal.SelectedItem = navItem;
                MostrarSeccion(tag);
                return;
            }
        }
    }

    private void Perfil_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0)
        {
            ViewModel.CambiarPerfilCommand.Execute(null);
        }
    }
}

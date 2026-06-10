using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SManager.Gui.WinUI.Models;
using SManager.Gui.WinUI.Servicios;
using SManager.Gui.WinUI.ViewModels;

namespace SManager.Gui.WinUI.Vistas;

/// <summary>Contenedor visual del asistente de primer par (Fase 2).</summary>
public sealed partial class AsistenteOnboardingControl : UserControl
{
    public AsistenteOnboardingViewModel? ViewModel { get; private set; }

    public AsistenteOnboardingControl()
    {
        InitializeComponent();
    }

    /// <summary>Enlaza el ViewModel creado por MainPageViewModel (una sola instancia compartida).</summary>
    public void EnlazarViewModel(AsistenteOnboardingViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    private void Plantilla_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (sender is Button { Tag: string id })
        {
            var plantilla = CatalogoPlantillasPar.BuscarPorId(id);
            if (plantilla is not null)
            {
                ViewModel.SeleccionarPlantillaCommand.Execute(plantilla);
            }
        }
    }
}

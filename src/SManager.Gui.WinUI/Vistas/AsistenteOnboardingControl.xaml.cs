using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SManager.Gui.WinUI.Models;
using SManager.Gui.WinUI.Servicios;
using SManager.Gui.WinUI.ViewModels;

namespace SManager.Gui.WinUI.Vistas;

/// <summary>Contenedor visual del asistente de primer par (Fase 2).</summary>
public sealed partial class AsistenteOnboardingControl : UserControl
{
    /// <summary>ViewModel compartido; DependencyProperty para que x:Bind se actualice sin Bindings.Update().</summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(AsistenteOnboardingViewModel),
            typeof(AsistenteOnboardingControl),
            new PropertyMetadata(null));

    public AsistenteOnboardingViewModel? ViewModel
    {
        get => (AsistenteOnboardingViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public AsistenteOnboardingControl()
    {
        InitializeComponent();
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MahApps.Metro.Controls; // Para MetroWindow
using Microsoft.Extensions.DependencyInjection; // Para GetRequiredService
using System; // Para IServiceProvider
using TuClinica.UI.ViewModels; // Para LoginViewModel

namespace TuClinica.UI.Views
{
    /// <summary>
    /// Lógica de interacción para LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : MetroWindow
    {
        public LoginWindow()
        {
            InitializeComponent();
            DataContext = App.AppHost!.Services.GetRequiredService<LoginViewModel>();
        }
    }
}

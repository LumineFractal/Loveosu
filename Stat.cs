using Avalonia.Controls;

namespace Loveosu
{
    class Stat
    {
        public string Name;
        public bool Enabled;
        public int Order;
        public Control MainCustomControl;
        public Control[] CustomControls;
        public Control[] UserControls;
        private readonly MainWindow MainWindow;

        public Stat(string name, bool enabled, int order, MainWindow mainWindow)
        {
            MainWindow = mainWindow;
            Name = name;
            Enabled = enabled;
            Order = order;
            MainCustomControl = MainWindow.FindControl<Control>(Name+"Custom");
            CustomControls = new Control[]{
                MainWindow.FindControl<Control>(Name+"CustomYes"),
                MainWindow.FindControl<Control>(Name+"CustomNo"),
                MainWindow.FindControl<Control>(Name+"CustomUp"),
                MainWindow.FindControl<Control>(Name+"CustomDown")
            };
            UserControls = new Control[]{
                MainWindow.FindControl<Control>(Name),
                MainWindow.FindControl<Control>(Name+"TextBlock"),
                MainWindow.FindControl<Control>(Name+"Diff"),
            };

        }
    }
}

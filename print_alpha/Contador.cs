using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PrintATH;

/// <summary>Selector numérico compacto: [−] valor [+].</summary>
public sealed class Contador : UserControl
{
    public static readonly DependencyProperty ValorProperty = DependencyProperty.Register(
        nameof(Valor), typeof(int), typeof(Contador),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((Contador)d).AlCambiar()));

    readonly TextBox caja;

    public Contador()
    {
        caja = new TextBox
        {
            Width = 34,
            Height = 26,
            Padding = new Thickness(0),
            MaxLength = 2,
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(0, 1, 0, 1),
        };
        caja.SetResourceReference(Control.BorderBrushProperty, "Borde");
        caja.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        caja.LostFocus += (_, _) => Confirmar();
        caja.KeyDown += (_, e) => { if (e.Key == Key.Enter) Confirmar(); };

        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { Boton("−", -1), caja, Boton("+", 1) },
        };
        caja.Text = Valor.ToString();
    }

    public int Valor { get => (int)GetValue(ValorProperty); set => SetValue(ValorProperty, value); }
    public int Minimo { get; set; } = 1;
    public int Maximo { get; set; } = 99;

    public event EventHandler? ValorCambiado;

    Button Boton(string texto, int paso)
    {
        var boton = new Button { Content = texto, Focusable = false };
        boton.SetResourceReference(StyleProperty, "BotonContador");
        boton.Click += (_, _) => Valor = Math.Clamp(Valor + paso, Minimo, Maximo);
        return boton;
    }

    void Confirmar()
    {
        Valor = int.TryParse(caja.Text, out var v) ? Math.Clamp(v, Minimo, Maximo) : Valor;
        caja.Text = Valor.ToString();
    }

    void AlCambiar()
    {
        caja.Text = Valor.ToString();
        ValorCambiado?.Invoke(this, EventArgs.Empty);
    }
}

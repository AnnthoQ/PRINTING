using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace PrintATH;

static class CatalogoDisenos
{
    /// <summary>Fotos por hoja que se ofrecen en el menú completo, de menor a mayor.</summary>
    public static readonly int[] Cantidades = { 1, 2, 3, 4, 6, 8, 9, 12, 16, 20, 30 };

    /// <summary>Las del menú rápido.</summary>
    public static readonly int[] CantidadesRapidas = { 1, 2, 3, 4 };

    public static List<Diseno> Crear() => Cantidades.Select(Diseno.PorCantidad).ToList();
}

static class Geometria
{
    const double SeparacionMm = 2;

    /// <summary>Proporción de foto típica (cámaras y celulares 3:2 o 4:3) que se busca para cada parte de la hoja.</summary>
    const double ProporcionIdeal = 1.5;

    /// <summary>Marcos donde va cada foto dentro del área imprimible, en orden de lectura.</summary>
    public static List<Rect> Celdas(Diseno diseno, Hoja hoja) => diseno.Tipo == TipoDiseno.Cantidad
        ? PorCantidad(diseno.Cantidad, hoja.Area)
        : PorTamano(Hoja.Mm(diseno.AnchoMm), Hoja.Mm(diseno.AltoMm), hoja.Area);

    /// <summary>
    /// Reparte el área en <paramref name="cantidad"/> partes iguales. De todas las rejillas posibles (2 × 3, 3 × 2…)
    /// elige la que deja cada parte con la forma más parecida a una foto, para que la foto se vea lo más grande posible.
    /// </summary>
    public static List<Rect> PorCantidad(int cantidad, Rect area)
    {
        var celdas = new List<Rect>();
        if (cantidad < 1 || area.Width <= 0 || area.Height <= 0) return celdas;
        if (cantidad == 1)
        {
            celdas.Add(area);
            return celdas;
        }

        double sep = Hoja.Mm(SeparacionMm);
        (int Columnas, int Filas)? mejor = null;
        double mejorDesvio = double.MaxValue;
        for (int columnas = 1; columnas <= cantidad; columnas++)
        {
            if (cantidad % columnas != 0) continue;
            int filas = cantidad / columnas;
            double ancho = (area.Width - sep * (columnas - 1)) / columnas;
            double alto = (area.Height - sep * (filas - 1)) / filas;
            if (ancho <= 0 || alto <= 0) continue;
            double desvio = Math.Abs(Math.Log(Math.Max(ancho, alto) / Math.Min(ancho, alto) / ProporcionIdeal));
            if (desvio < mejorDesvio)
            {
                mejorDesvio = desvio;
                mejor = (columnas, filas);
            }
        }
        if (mejor is not var (c, f)) return celdas;
        return Rejilla(area, c, f, (area.Width - sep * (c - 1)) / c, (area.Height - sep * (f - 1)) / f, sep);
    }

    /// <summary>Celdas de medida fija: se prueba derecha y girada y gana la que mete más (a igualdad, sin girar).</summary>
    static List<Rect> PorTamano(double ancho, double alto, Rect area)
    {
        if (ancho <= 0 || alto <= 0 || area.Width <= 0 || area.Height <= 0) return new List<Rect>();
        double sep = Hoja.Mm(SeparacionMm);
        var (c1, f1) = Cabe(area, ancho, alto, sep);
        var (c2, f2) = Cabe(area, alto, ancho, sep);
        bool girada = c2 * f2 > c1 * f1;
        var (columnas, filas) = girada ? (c2, f2) : (c1, f1);
        if (girada) (ancho, alto) = (alto, ancho);
        return columnas * filas == 0 ? new List<Rect>() : Rejilla(area, columnas, filas, ancho, alto, sep);
    }

    static (int Columnas, int Filas) Cabe(Rect area, double ancho, double alto, double sep)
    {
        // Medio píxel de tolerancia para que un tamaño que entra justo no se pierda por redondeo.
        int columnas = (int)Math.Floor((area.Width + sep + 0.5) / (ancho + sep));
        int filas = (int)Math.Floor((area.Height + sep + 0.5) / (alto + sep));
        return (Math.Max(columnas, 0), Math.Max(filas, 0));
    }

    /// <summary>Rejilla centrada en el área, en orden de lectura.</summary>
    static List<Rect> Rejilla(Rect area, int columnas, int filas, double ancho, double alto, double sep)
    {
        var celdas = new List<Rect>(columnas * filas);
        double x0 = area.X + (area.Width - (columnas * ancho + (columnas - 1) * sep)) / 2;
        double y0 = area.Y + (area.Height - (filas * alto + (filas - 1) * sep)) / 2;
        for (int f = 0; f < filas; f++)
            for (int c = 0; c < columnas; c++)
                celdas.Add(new Rect(x0 + c * (ancho + sep), y0 + f * (alto + sep), ancho, alto));
        return celdas;
    }

    public static string Medida(Rect celda) => $"{celda.Width / 96 * 2.54:0.#} × {celda.Height / 96 * 2.54:0.#} cm";

    /// <summary>Miniatura del diseño sobre el papel actual para la tarjeta de selección.</summary>
    public static ImageSource Esquema(Hoja hoja, IReadOnlyList<Rect> celdas)
    {
        var grupo = new DrawingGroup();
        var borde = new Pen(new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)), hoja.Ancho / 70);
        grupo.Children.Add(new GeometryDrawing(Brushes.White, borde, new RectangleGeometry(new Rect(0, 0, hoja.Ancho, hoja.Alto))));
        var relleno = new SolidColorBrush(Color.FromRgb(0x8C, 0x8C, 0x8C));
        foreach (var c in celdas)
            grupo.Children.Add(new GeometryDrawing(relleno, null, new RectangleGeometry(c)));
        grupo.Freeze();
        var imagen = new DrawingImage(grupo);
        imagen.Freeze();
        return imagen;
    }
}
